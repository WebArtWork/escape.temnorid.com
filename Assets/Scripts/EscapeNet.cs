using System;
using System.Collections.Generic;
using System.Globalization;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using Newtonsoft.Json;
using UnityEngine;

namespace Temnorid.Escape
{
	/// <summary>The shared prison as the host knows it: what is sent to a guest so the doors, items and puzzles look the same there.</summary>
	[Serializable]
	public sealed class EscapeStatePacket
	{
		public EscapeSave save;
		public bool escaped;
		public int latch;
		public int[] slots;
	}

	/// <summary>
	/// Makes the prison shared between two prisoners. The host is the authority: it owns the <see cref="PrisonState"/> (puzzles,
	/// items, doors) and the guard with its detection meter, checks every request against the rules and publishes the state. A
	/// guest asks (use, latch step, cabinet symbol, combine) and shows what comes back. Each prisoner is simulated on their own
	/// machine and published as a pose; the other one is a <see cref="RemotePrisoner"/>. Cameras are not shared.
	/// <para>
	/// Events from a guest to the host are named <c>host.*</c> (the session does not pass those on); events from the host are
	/// <c>esc.*</c>. A prisoner in a screen or hiding cannot be seen by the guard; the world keeps running for the other one.
	/// </para>
	/// </summary>
	public sealed class EscapeNet : MonoBehaviour
	{
		public const string HereEvent = "host.here";
		public const string UseEvent = "host.use";
		public const string LatchEvent = "host.latch";
		public const string CycleEvent = "host.cycle";
		public const string ConfirmEvent = "host.confirm";
		public const string CombineEvent = "host.combine";
		public const string CloseEvent = "host.close";
		public const string StateEvent = "esc.state";
		public const string FxEvent = "esc.fx";
		public const string SfxEvent = "esc.sfx";
		public const string CaughtEvent = "esc.caught";
		public const string WinEvent = "esc.win";

		/// <summary>The entity id of the guard.</summary>
		public const int GuardId = 1;
		/// <summary>How near (metres on the floor) a prisoner must be to use something, a little more than the player's own reach.</summary>
		public const float UseRange = EscapeConfig.InteractRange + 1.2f;

		const float PoseEvery = 1f / 15f;
		const float EntityEvery = 0.1f;
		const float ResendEvery = 5f;

		EscapeGame _game;
		NetSession _net;
		RemoteBody _guardBody;
		readonly Dictionary<ulong, RemotePrisoner> _others = new Dictionary<ulong, RemotePrisoner>();
		float _nextPose;
		float _nextEntities;
		float _nextHere;
		float _nextResend;
		bool _dirty;
		bool _gotState;
		bool _quitting;

		public bool IsHost => _net != null && _net.IsHost;
		public bool IsClient => _net != null && _net.IsClient;
		public NetSession Session => _net;
		public IReadOnlyDictionary<ulong, RemotePrisoner> Others => _others;

		/// <summary>0 for the host, 1 for the guest: where in the cell and the corridor this prisoner stands.</summary>
		public int Slot
		{
			get
			{
				for (var i = 0; i < _net.Players.Count; i++)
					if (_net.Players[i].Id == _net.LocalId) return Mathf.Min(i, 1);
				return _net.IsClient ? 1 : 0;
			}
		}

		public void Init(EscapeGame game)
		{
			_game = game;
			_net = NetSession.Instance;
			if (_net.IsClient)
			{
				_game.Guard.Remote = true;
				_guardBody = _game.Guard.gameObject.GetComponent<RemoteBody>() ?? _game.Guard.gameObject.AddComponent<RemoteBody>();
			}
			_net.On(HereEvent, OnHere);
			_net.On(UseEvent, OnUse);
			_net.On(LatchEvent, OnLatch);
			_net.On(CycleEvent, OnCycle);
			_net.On(ConfirmEvent, OnConfirm);
			_net.On(CombineEvent, OnCombine);
			_net.On(CloseEvent, OnClose);
			_net.On(StateEvent, OnState);
			_net.On(FxEvent, OnFx);
			_net.On(SfxEvent, OnSfx);
			_net.On(CaughtEvent, OnCaught);
			_net.On(WinEvent, OnWin);
			_net.Disconnected += OnDisconnected;
		}

		void OnDestroy()
		{
			if (_net == null) return;
			_net.Off(HereEvent, OnHere);
			_net.Off(UseEvent, OnUse);
			_net.Off(LatchEvent, OnLatch);
			_net.Off(CycleEvent, OnCycle);
			_net.Off(ConfirmEvent, OnConfirm);
			_net.Off(CombineEvent, OnCombine);
			_net.Off(CloseEvent, OnClose);
			_net.Off(StateEvent, OnState);
			_net.Off(FxEvent, OnFx);
			_net.Off(SfxEvent, OnSfx);
			_net.Off(CaughtEvent, OnCaught);
			_net.Off(WinEvent, OnWin);
			_net.Disconnected -= OnDisconnected;
			foreach (var other in _others.Values)
				if (other != null) Destroy(other.gameObject);
			_others.Clear();
		}

		void OnDisconnected(string reason)
		{
			// The host closed the game or the connection dropped: nothing left to play together.
			if (_quitting) return;
			_quitting = true;
			ShellFlow.QuitToMenu();
		}

		// ---- what the game calls ----

		/// <summary>Host: the shared state changed; tell the guest at the end of the frame.</summary>
		public void MarkDirty() => _dirty = true;

		public void SendUse(string id) => _net.Send(UseEvent, id, true);
		public void SendLatch(LatchStep step) => _net.Send(LatchEvent, step.ToString(), true);
		public void SendCycle(int slot) => _net.Send(CycleEvent, slot.ToString(CultureInfo.InvariantCulture), true);
		public void SendConfirm() => _net.Send(ConfirmEvent, "", true);
		public void SendCombine() => _net.Send(CombineEvent, "", true);
		public void SendClose() => _net.Send(CloseEvent, "", true);

		/// <summary>Host: something only one prisoner should see or open: a line, a text, a puzzle screen. Kinds: toast, read, latch, cabinet, latchmsg, cabmsg, close.</summary>
		public void Fx(ulong player, string kind, string text = "")
		{
			if (!_net.IsHost) return;
			_net.SendTo(player, FxEvent, kind + "|" + text);
		}

		/// <summary>Host: a line for everybody else (the host shows its own).</summary>
		public void FxAll(string kind, string text = "")
		{
			if (_net.IsHost) _net.Send(FxEvent, kind + "|" + text);
		}

		/// <summary>Host: a sound for the guest (the host plays its own).</summary>
		public void Sound(Sfx sfx, float volume)
		{
			if (_net.IsHost) _net.Send(SfxEvent, ((int)sfx).ToString(CultureInfo.InvariantCulture) + "|" + volume.ToString("R", CultureInfo.InvariantCulture));
		}

		public void AnnounceCaught()
		{
			if (_net.IsHost) _net.Send(CaughtEvent, "");
		}

		/// <summary>Host: the escape, with the final state so both screens show the same time and count.</summary>
		public void AnnounceWin()
		{
			if (_net.IsHost) _net.Send(WinEvent, Pack());
		}

		public EscapeStatePacket Snapshot()
		{
			var state = _game.State;
			var save = state.ToSave(Vector2.zero, 0f);
			var escaped = save.escaped;
			save.escaped = false;
			return new EscapeStatePacket { save = save, escaped = escaped, latch = state.LatchProgress, slots = new[] { (int)state.Slot(0), (int)state.Slot(1), (int)state.Slot(2) } };
		}

		string Pack() => JsonConvert.SerializeObject(Snapshot());

		/// <summary>The other prisoner's body (the guard watches it by position). False when alone.</summary>
		public bool TryGetOther(out RemotePrisoner other)
		{
			foreach (var pair in _others)
			{
				if (pair.Value == null) continue;
				other = pair.Value;
				return true;
			}
			other = null;
			return false;
		}

		/// <summary>Where the other prisoners are, by their published poses (the host checks checkpoints and the exit with them).</summary>
		public IEnumerable<Vector2> OtherPositions()
		{
			foreach (var player in _net.Players)
			{
				if (player.Id == _net.LocalId) continue;
				if (_net.TryGetPose(player.Id, out var pose)) yield return new Vector2(pose.Position.x, pose.Position.z);
			}
		}

		public bool TryOtherPosition(ulong player, out Vector2 position)
		{
			if (_net.TryGetPose(player, out var pose))
			{
				position = new Vector2(pose.Position.x, pose.Position.z);
				return true;
			}
			position = Vector2.zero;
			return false;
		}

		// ---- events: guest to host ----

		void OnHere(ulong sender, string payload)
		{
			if (!_net.IsHost) return;
			_net.SendTo(sender, StateEvent, Pack());
		}

		bool Near(ulong sender, string id)
		{
			if (!TryOtherPosition(sender, out var at)) return false;
			foreach (var item in Interactable.All)
			{
				if (item.Id != id) continue;
				var p = item.transform.position;
				if ((new Vector2(p.x, p.z) - at).magnitude <= UseRange) return true;
			}
			return false;
		}

		void OnUse(ulong sender, string payload)
		{
			if (!_net.IsHost || string.IsNullOrEmpty(payload)) return;
			if (!Near(sender, payload)) return;
			_game.HostUse(sender, payload);
		}

		void OnLatch(ulong sender, string payload)
		{
			if (!_net.IsHost || !Enum.TryParse(payload, out LatchStep step)) return;
			if (!Near(sender, Ids.CellLatch)) return;
			_game.HostLatch(sender, step);
		}

		void OnCycle(ulong sender, string payload)
		{
			if (!_net.IsHost || !int.TryParse(payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out var slot) || slot < 0 || slot > 2) return;
			if (!Near(sender, Ids.RecordsCabinet)) return;
			_game.HostCycle(sender, slot);
		}

		void OnConfirm(ulong sender, string payload)
		{
			if (!_net.IsHost || !Near(sender, Ids.RecordsCabinet)) return;
			_game.HostConfirm(sender);
		}

		void OnCombine(ulong sender, string payload)
		{
			if (_net.IsHost) _game.HostCombine(sender);
		}

		void OnClose(ulong sender, string payload)
		{
			if (_net.IsHost) _game.HostClosePuzzle();
		}

		// ---- events: host to guest ----

		void OnState(ulong sender, string payload)
		{
			if (!_net.IsClient) return;
			EscapeStatePacket packet;
			try { packet = JsonConvert.DeserializeObject<EscapeStatePacket>(payload); }
			catch (Exception) { return; }
			if (packet == null) return;
			_gotState = true;
			_game.ApplyState(packet);
		}

		void OnFx(ulong sender, string payload)
		{
			if (!_net.IsClient || payload == null) return;
			var parts = payload.Split(new[] { '|' }, 2);
			_game.ApplyFx(parts[0], parts.Length > 1 ? parts[1] : "");
		}

		void OnSfx(ulong sender, string payload)
		{
			if (!_net.IsClient || payload == null) return;
			var parts = payload.Split('|');
			if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kind)) return;
			var volume = 1f;
			if (parts.Length > 1) float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out volume);
			if (Enum.IsDefined(typeof(Sfx), kind)) _game.Audio.Play((Sfx)kind, volume);
		}

		void OnCaught(ulong sender, string payload)
		{
			if (_net.IsClient) _game.CaughtRemote();
		}

		void OnWin(ulong sender, string payload)
		{
			if (!_net.IsClient) return;
			try
			{
				var packet = JsonConvert.DeserializeObject<EscapeStatePacket>(payload);
				if (packet != null) _game.ApplyState(packet);
			}
			catch (Exception)
			{
				// The escape still counts without the numbers.
			}
			_game.WinRemote();
		}

		// ---- every frame ----

		void Update()
		{
			if (_net == null || !_net.IsOnline) return;

			if (_net.IsClient && !_gotState && Time.unscaledTime >= _nextHere)
			{
				_nextHere = Time.unscaledTime + 1f;
				_net.Send(HereEvent, "", true);
			}

			ShowOthers();
			if (_net.IsClient) ShowGuard();

			if (_net.IsHost && (_dirty || Time.unscaledTime >= _nextResend))
			{
				_dirty = false;
				_nextResend = Time.unscaledTime + ResendEvery;
				_net.Send(StateEvent, Pack());
			}

			if (Time.unscaledTime >= _nextPose)
			{
				_nextPose = Time.unscaledTime + PoseEvery;
				_net.PublishPose(MyPose());
			}
			if (_net.IsHost && Time.unscaledTime >= _nextEntities)
			{
				_nextEntities = Time.unscaledTime + EntityEvery;
				PublishGuard();
			}
		}

		NetPose MyPose()
		{
			var player = _game.Player;
			var state = 0;
			if (_game.Hidden) state |= RemotePrisoner.HiddenBit;
			if (_game.LocalBusy) state |= RemotePrisoner.BusyBit;
			return new NetPose
			{
				Position = player.transform.position,
				Yaw = player.transform.eulerAngles.y,
				Speed = player.Speed,
				State = state,
			};
		}

		void ShowOthers()
		{
			var seen = new HashSet<ulong>();
			foreach (var player in _net.Players)
			{
				if (player.Id == _net.LocalId) continue;
				seen.Add(player.Id);
				if (!_others.TryGetValue(player.Id, out var other) || other == null)
				{
					other = RemotePrisoner.Create(player.Id, player.Name, ShellConfig.Current.ColourOf(player.Colour), _game.Player);
					_others[player.Id] = other;
				}
				other.SetName(player.Name);
				if (_net.TryGetPose(player.Id, out var pose)) other.Apply(pose);
			}
			List<ulong> gone = null;
			foreach (var pair in _others)
				if (!seen.Contains(pair.Key)) (gone ?? (gone = new List<ulong>())).Add(pair.Key);
			if (gone == null) return;
			foreach (var id in gone)
			{
				if (_others[id] != null) Destroy(_others[id].gameObject);
				_others.Remove(id);
			}
		}

		void PublishGuard()
		{
			var guard = _game.Guard;
			var t = guard.transform;
			var state = (guard.Sample.Paused ? 1 : 0) | (guard.Sees ? 2 : 0);
			_net.PublishEntities(new List<NetEntity>
			{
				new NetEntity { Id = GuardId, Kind = 0, Position = t.position, Yaw = t.eulerAngles.y, Value = guard.Awareness, State = state },
			});
		}

		void ShowGuard()
		{
			if (!_net.Entities.TryGetValue(GuardId, out var entity)) return;
			_guardBody.Apply(entity);
			var distance = Vector3.Distance(_game.Player.transform.position, _game.Guard.transform.position);
			_game.Guard.ShowRemote(entity.Value, (entity.State & 1) != 0, (entity.State & 2) != 0, distance);
		}
	}
}
