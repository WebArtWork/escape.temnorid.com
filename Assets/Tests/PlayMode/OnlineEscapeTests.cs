using System;
using System.Collections;
using System.IO;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Escape.Tests
{
	/// <summary>
	/// The real prison with this machine as the host and a second prisoner (a detached session in the same process) as the guest:
	/// the guest is shown on the host from its pose, the host's guard reaches the guest as an entity, the guest's requests change
	/// the host's state (and far-away ones are refused), and the guard catching the guest sends both back to the corridor.
	/// </summary>
	public class OnlineEscapeTests
	{
		NetSession _guest;
		string _saveFolder;

		static IEnumerator Until(Func<bool> condition, string what, float seconds = 15f)
		{
			var end = Time.realtimeSinceStartup + seconds;
			while (!condition())
			{
				if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
				yield return null;
			}
		}

		[SetUp]
		public void SetUp()
		{
			_saveFolder = Path.Combine(Path.GetTempPath(), "escape-online-" + Guid.NewGuid());
			EscapeGame.SaveRootOverride = _saveFolder;
		}

		[TearDown]
		public void TearDown()
		{
			Time.timeScale = 1f;
			EscapeGame.SaveRootOverride = null;
			EscapeGame.ForceTouch = false;
			if (_guest != null) { _guest.Leave(); UnityEngine.Object.Destroy(_guest.gameObject); _guest = null; }
			NetSession.Instance.Leave();
			if (Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder, true);
		}

		IEnumerator StartOnline()
		{
			var host = NetSession.Instance;
			host.Leave();
			host.GameId = "escape";
			host.GameVersion = Application.version;
			host.LocalName = "Hosty";
			host.MaxPlayers = 2;
			Assert.IsTrue(host.HostDirect(7807), host.LastError);

			_guest = NetSession.CreateDetached();
			_guest.GameId = "escape";
			_guest.GameVersion = Application.version;
			_guest.LocalName = "Guesty";
			_guest.LocalColour = 3;
			Assert.IsTrue(_guest.JoinDirect("127.0.0.1:7807"), _guest.LastError);
			yield return Until(() => host.Players.Count == 2 && _guest.Players.Count == 2 && !_guest.Connecting, "the guest to join");
		}

		IEnumerator GuestStandsAt(Vector2 position, int state = 0)
		{
			var host = NetSession.Instance;
			var at = new Vector3(position.x, 0.05f, position.y);
			for (var i = 0; i < 6; i++)
			{
				_guest.PublishPose(new NetPose { Position = at, Yaw = 0f, Speed = 0f, State = state });
				yield return new WaitForSeconds(0.1f);
			}
			yield return Until(() => host.TryGetPose(_guest.LocalId, out var pose) && (pose.Position - at).magnitude < 0.1f, "the host to know where the guest is");
		}

		[UnityTest]
		[Timeout(180000)]
		public IEnumerator A_guest_is_shown_the_guard_is_published_requests_are_checked_and_a_catch_sends_both_back()
		{
			yield return StartOnline();
			string state = null;
			string fx = null;
			string caught = null;
			_guest.On(EscapeNet.StateEvent, (who, text) => state = text);
			_guest.On(EscapeNet.FxEvent, (who, text) => fx = text);
			_guest.On(EscapeNet.CaughtEvent, (who, text) => caught = "yes");

			yield return SceneManager.LoadSceneAsync("Prison");
			yield return null;
			yield return null;
			var g = EscapeGame.Instance;
			Assert.IsNotNull(g.Net, "an online prison has the link to the other prisoner");
			Assert.IsTrue(g.Net.IsHost);
			Assert.IsFalse(g.SaveEnabled, "an online game never overwrites a saved game");
			Assert.AreEqual(Mode.Intro, g.Mode, "the host starts a new episode");
			Assert.IsFalse(g.Guard.Remote);

			// The guest is shown on the host from its pose, as a picture without a body.
			var cellSpot = new Vector2(-12.5f, -0.3f);
			yield return GuestStandsAt(cellSpot);
			yield return Until(() => g.Net.Others.Count == 1, "the guest's prisoner to be built");
			RemotePrisoner shown = null;
			foreach (var other in g.Net.Others.Values) shown = other;
			yield return Until(() => ((Vector2)new Vector2(shown.transform.position.x, shown.transform.position.z) - cellSpot).magnitude < 0.3f, "the guest to be shown");
			Assert.IsNull(shown.GetComponentInChildren<Collider>(), "the other prisoner is a picture, not a body");
			Assert.IsTrue(shown.Exposed);

			// The host starts; the guard is published to the guest as an entity, and the guest hears the state.
			g.BeginPlay();
			yield return Until(() => state != null, "the guest to get the shared state");
			yield return Until(() => _guest.Entities.ContainsKey(EscapeNet.GuardId), "the guard's state");
			var guardNow = _guest.Entities[EscapeNet.GuardId].Position;
			Assert.Less(Vector2.Distance(new Vector2(guardNow.x, guardNow.z), new Vector2(g.Guard.transform.position.x, g.Guard.transform.position.z)), 3f);

			// A request from far away is refused, a near one changes the shared state and comes back to everybody.
			state = null;
			_guest.Send(EscapeNet.UseEvent, Ids.LeverUpper, true);
			yield return new WaitForSeconds(0.5f);
			Assert.IsFalse(g.State.UpperLeverDone);
			Assert.IsNull(state);

			yield return GuestStandsAt(new Vector2(-12.4f, -2.2f));
			_guest.Send(EscapeNet.UseEvent, Ids.CellCup, true);
			yield return Until(() => g.State.CupMoved, "the host to move the cup");
			yield return Until(() => state != null && state.Contains("\"cupMoved\":true"), "the new state to reach the guest");
			StringAssert.Contains("toast|", fx, "the answer to the one who asked");

			// The wire is taken by the guest and is in everybody's inventory.
			yield return GuestStandsAt(new Vector2(-12.0f, -2.6f));
			state = null;
			_guest.Send(EscapeNet.UseEvent, Ids.CellWire, true);
			yield return Until(() => g.State.Has(Ids.Wire), "the wire to be taken");
			yield return Until(() => state != null && state.Contains(Ids.Wire), "the inventory to reach the guest");

			// The guest leaves the cell and stands in the guard's view: the host's guard catches the guest and both go to the corridor.
			g.Guard.ResetPatrol(0f);
			var corridor = new Vector2(0f, 0f);
			var start = Time.realtimeSinceStartup;
			while (g.Mode != Mode.Caught)
			{
				if (Time.realtimeSinceStartup - start > 60f) Assert.Fail("the guest was never caught in the open");
				_guest.PublishPose(new NetPose { Position = new Vector3(corridor.x, 0.05f, corridor.y) });
				yield return null;
			}
			yield return Until(() => caught != null, "the guest to be told");
			Assert.AreEqual(1, g.State.Detections);
			yield return Until(() => g.Mode == Mode.Playing, "the host to be back at the corridor", 20f);
			Assert.AreEqual(PrisonLayout.CorridorCheckpoint.x, g.Player.Position2D.x, 0.2f);
			Assert.Less(g.Guard.PatrolTime, 2f, "the patrol starts over for both");

			// A guest who leaves does not leave the host stuck.
			_guest.Leave();
			yield return Until(() => g.Net.Others.Count == 0, "the guest's prisoner to go");
			yield return new WaitForSeconds(0.3f);
			Assert.IsNull(g.Guard.Other);
			Assert.AreEqual(Mode.Playing, g.Mode);
		}

		[UnityTest]
		[Timeout(60000)]
		public IEnumerator Touch_controls_exist_on_a_phone_and_the_prison_sits_between_the_bars()
		{
			EscapeGame.ForceTouch = true;
			yield return SceneManager.LoadSceneAsync("Prison");
			yield return null;
			yield return null;
			var g = EscapeGame.Instance;
			g.NewGame();
			g.BeginPlay();
			yield return null;
			yield return null;
			Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<TouchStick>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
			Assert.AreEqual(3, UnityEngine.Object.FindObjectsByType<TouchButton>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
			Assert.AreEqual(Vector2.zero, g.Player.TouchMove, "the stick drives the prisoner, and it is at rest");
			Assert.IsNotNull(ShellChrome.Current, "the shell's bars are in the prison");
			Assert.IsFalse(g.PromptText.StartsWith("E — "), "no key names on a phone");
		}
	}
}
