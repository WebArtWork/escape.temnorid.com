using System;
using System.Collections.Generic;
using UnityEngine;

namespace Temnorid.Escape
{
	/// <summary>The numbers of the game in one place.</summary>
	public static class EscapeConfig
	{
		public const string EpisodeId = "watchtower_prison_01";
		public const int SaveVersion = 1;

		public const float MoveSpeed = 2.5f;
		public const float FastSpeed = 4f;
		public const float InteractRange = 2f;
		public const float CameraHysteresis = 0.3f;
		public const float CameraBlend = 0.3f;

		// Guard.
		public const float GuardSpeed = 1.8f;
		public const float SightFullAngle = 80f;
		public const float SightRange = 6f;
		public const float DetectSeconds = 1.0f;
		/// <summary>How fast the guard's suspicion fades when the player is out of sight, as a fraction of full per second.</summary>
		public const float AwarenessDecay = 1.5f;
		/// <summary>After a capture the guard cannot notice the player for this long.</summary>
		public const float GraceSeconds = 2.5f;

		// Hints.
		public const float HintSeconds = 90f;
		public const float StrongHintSeconds = 180f;
	}

	/// <summary>Stable ids of items, objects and puzzles.</summary>
	public static class Ids
	{
		public const string Wire = "wire";
		public const string HalfA = "gate_key_half_a";
		public const string HalfB = "gate_key_half_b";
		public const string KeyComplete = "gate_key_complete";

		public const string CellBed = "cell_bed";
		public const string CellWallClue = "cell_wall_clue";
		public const string CellCup = "cell_cup";
		public const string CellWire = "cell_wire";
		public const string CellLatch = "cell_latch";
		public const string CorridorHidingSpot = "corridor_hiding_spot";
		public const string StoreroomLedger = "storeroom_ledger";
		public const string StoreShelf = "storeroom_shelf";
		public const string RecordsCabinet = "records_cabinet";
		public const string ConfiscatedPouch = "confiscated_pouch";
		public const string WorkbenchKeyPiece = "workbench_key_piece";
		public const string GatePlaque = "service_gate_plaque";
		public const string GateKeyhole = "service_gate_keyhole";
		public const string LeverUpper = "service_lever_upper";
		public const string LeverLower = "service_lever_lower";
		public const string CourtyardExit = "courtyard_exit";

		public const string P1 = "P1";
		public const string P2 = "P2";
		public const string P3 = "P3";

		public const string CheckpointCell = "cell";
		public const string CheckpointCorridor = "corridor";
		public const string CheckpointStore = "store";

		public const string ClueWall = "clue_wall";
		public const string ClueLedger = "clue_ledger";
		public const string ClueNote = "clue_note";
		public const string CluePlaque = "clue_plaque";
	}

	/// <summary>Where the story stands, in order.</summary>
	public enum Milestone
	{
		CellLocked,
		CellOpen,
		CorridorAccess,
		CabinetOpen,
		KeyAssembled,
		GateUnlocked,
		GateOpen,
		Escaped,
	}

	public enum LatchStep
	{
		Left,
		Right,
		Lift,
	}

	public enum Symbol
	{
		Sun,
		Wheat,
		Moon,
		Tree,
	}

	public enum Lever
	{
		Upper,
		Lower,
	}

	public enum PuzzleResult
	{
		/// <summary>The puzzle is not available now (no wire yet, already solved).</summary>
		Blocked,
		/// <summary>A correct step, more to go.</summary>
		Progress,
		/// <summary>A wrong step: the input starts over, nothing is lost.</summary>
		Wrong,
		Solved,
	}

	public enum LeverResult
	{
		/// <summary>The mechanism is locked: the key is not in the lock yet, or the gate is already open.</summary>
		Blocked,
		/// <summary>The upper lever went up; the lower one is next.</summary>
		UpperRaised,
		/// <summary>Lower before upper: both levers return to the start.</summary>
		WrongOrder,
		/// <summary>The order was right and the gate opens.</summary>
		GateOpened,
		AlreadyDone,
	}

	/// <summary>
	/// The truth of the episode: puzzle flags, the inventory, the counters. Scene objects only show it. Every change goes
	/// through a method here, so a gate cannot open before its key and a milestone cannot be skipped.
	/// </summary>
	public sealed class PrisonState
	{
		static readonly string[] CabinetAnswer = { "Sun", "Wheat", "Moon" };
		static readonly LatchStep[] LatchAnswer = { LatchStep.Left, LatchStep.Right, LatchStep.Right, LatchStep.Lift };

		readonly List<string> _items = new List<string>();
		readonly List<string> _clues = new List<string>();
		readonly Symbol[] _slots = { Symbol.Tree, Symbol.Tree, Symbol.Tree };
		int _latchProgress;

		public float ElapsedSeconds { get; set; }
		public int Detections { get; private set; }
		public string Checkpoint { get; private set; } = Ids.CheckpointCell;

		public bool CupMoved { get; private set; }
		public bool WireTaken { get; private set; }
		public bool CellLatchUnlocked { get; private set; }
		public bool CorridorReached { get; private set; }
		public bool CabinetOpen { get; private set; }
		public bool PouchTaken { get; private set; }
		public bool HalfBTaken { get; private set; }
		public bool GateKeyAssembled { get; private set; }
		public bool GateKeyInserted { get; private set; }
		public bool UpperLeverDone { get; private set; }
		public bool LowerLeverDone { get; private set; }
		public bool ServiceGateOpen { get; private set; }
		public bool Escaped { get; private set; }

		public IReadOnlyList<string> Items => _items;
		public IReadOnlyList<string> Clues => _clues;
		public int LatchProgress => _latchProgress;
		public Symbol Slot(int index) => _slots[index];

		public bool Has(string itemId) => _items.Contains(itemId);

		public bool CanCombine => Has(Ids.HalfA) && Has(Ids.HalfB) && !Has(Ids.KeyComplete);
		public bool CanOpenLatch => Has(Ids.Wire) && !CellLatchUnlocked;

		public Milestone Milestone
		{
			get
			{
				if (Escaped) return Milestone.Escaped;
				if (ServiceGateOpen) return Milestone.GateOpen;
				if (GateKeyInserted) return Milestone.GateUnlocked;
				if (GateKeyAssembled) return Milestone.KeyAssembled;
				if (CabinetOpen) return Milestone.CabinetOpen;
				if (CorridorReached) return Milestone.CorridorAccess;
				if (CellLatchUnlocked) return Milestone.CellOpen;
				return Milestone.CellLocked;
			}
		}

		/// <summary>The puzzle the player is on: P1 the cell latch, P2 the records cabinet, P3 the gate. Empty when all are solved.</summary>
		public string CurrentPuzzle
		{
			get
			{
				if (!CellLatchUnlocked) return Ids.P1;
				if (!CabinetOpen) return Ids.P2;
				if (!ServiceGateOpen) return Ids.P3;
				return "";
			}
		}

		// ---- the cell ----

		public bool MoveCup()
		{
			if (CupMoved) return false;
			CupMoved = true;
			return true;
		}

		public bool TakeWire()
		{
			if (!CupMoved || WireTaken) return false;
			WireTaken = true;
			_items.Add(Ids.Wire);
			return true;
		}

		/// <summary>Presses one control of the latch. A wrong press starts over; the wire is never lost.</summary>
		public PuzzleResult PressLatch(LatchStep step)
		{
			if (!CanOpenLatch) return PuzzleResult.Blocked;
			if (LatchAnswer[_latchProgress] != step)
			{
				_latchProgress = 0;
				return PuzzleResult.Wrong;
			}
			_latchProgress++;
			if (_latchProgress < LatchAnswer.Length) return PuzzleResult.Progress;
			_latchProgress = 0;
			CellLatchUnlocked = true;
			Checkpoint = Ids.CheckpointCell;
			return PuzzleResult.Solved;
		}

		public void ResetLatch() => _latchProgress = 0;

		// ---- the corridor and the storeroom ----

		public bool ReachCorridor()
		{
			if (!CellLatchUnlocked || CorridorReached) return false;
			CorridorReached = true;
			Checkpoint = Ids.CheckpointCorridor;
			return true;
		}

		/// <summary>Walking into the storeroom makes it the place to continue from (the guard never goes in).</summary>
		public bool ReachStore()
		{
			if (!CorridorReached || Checkpoint == Ids.CheckpointStore) return false;
			Checkpoint = Ids.CheckpointStore;
			return true;
		}

		public void Caught() => Detections++;

		// ---- the records cabinet ----

		public Symbol CycleSlot(int index)
		{
			if (CabinetOpen) return _slots[index];
			_slots[index] = (Symbol)(((int)_slots[index] + 1) % 4);
			return _slots[index];
		}

		public void ResetCabinet()
		{
			if (!CabinetOpen) for (var i = 0; i < _slots.Length; i++) _slots[i] = Symbol.Tree;
		}

		public PuzzleResult ConfirmCabinet()
		{
			if (CabinetOpen || !CellLatchUnlocked) return PuzzleResult.Blocked;
			for (var i = 0; i < CabinetAnswer.Length; i++)
				if (_slots[i].ToString() != CabinetAnswer[i]) return PuzzleResult.Wrong;
			CabinetOpen = true;
			return PuzzleResult.Solved;
		}

		/// <summary>The pouch gives its half of the key once, and only from an open cabinet.</summary>
		public bool TakePouch()
		{
			if (!CabinetOpen || PouchTaken) return false;
			PouchTaken = true;
			_items.Add(Ids.HalfA);
			return true;
		}

		public bool TakeHalfB()
		{
			if (!CorridorReached || HalfBTaken) return false;
			HalfBTaken = true;
			_items.Add(Ids.HalfB);
			return true;
		}

		// ---- the key and the gate ----

		/// <summary>Joins the two halves into the gate key in one step; the halves are used up and the key is made, or nothing changes.</summary>
		public bool CombineKey()
		{
			if (!CanCombine) return false;
			_items.Remove(Ids.HalfA);
			_items.Remove(Ids.HalfB);
			_items.Add(Ids.KeyComplete);
			GateKeyAssembled = true;
			return true;
		}

		/// <summary>Turns the key in the gate lock. The key stays with the player.</summary>
		public bool InsertKey()
		{
			if (!Has(Ids.KeyComplete) || GateKeyInserted) return false;
			GateKeyInserted = true;
			return true;
		}

		public LeverResult PullLever(Lever lever)
		{
			if (!GateKeyInserted || ServiceGateOpen) return LeverResult.Blocked;
			if (lever == Lever.Upper)
			{
				if (UpperLeverDone) return LeverResult.AlreadyDone;
				UpperLeverDone = true;
				return LeverResult.UpperRaised;
			}
			if (!UpperLeverDone)
			{
				UpperLeverDone = false;
				LowerLeverDone = false;
				return LeverResult.WrongOrder;
			}
			LowerLeverDone = true;
			ServiceGateOpen = true;
			return LeverResult.GateOpened;
		}

		public bool TryEscape()
		{
			if (!ServiceGateOpen || Escaped) return false;
			Escaped = true;
			return true;
		}

		// ---- clues ----

		public bool Read(string clueId)
		{
			if (_clues.Contains(clueId)) return false;
			_clues.Add(clueId);
			return true;
		}

		// ---- saving ----

		public EscapeSave ToSave(Vector2 position, float heading)
		{
			return new EscapeSave
			{
				version = EscapeConfig.SaveVersion,
				episodeId = EscapeConfig.EpisodeId,
				elapsedSeconds = ElapsedSeconds,
				detectionCount = Detections,
				checkpoint = Checkpoint,
				cupMoved = CupMoved,
				wireTaken = WireTaken,
				cellLatchUnlocked = CellLatchUnlocked,
				corridorReached = CorridorReached,
				cabinetOpen = CabinetOpen,
				pouchTaken = PouchTaken,
				halfBTaken = HalfBTaken,
				gateKeyAssembled = GateKeyAssembled,
				gateKeyInserted = GateKeyInserted,
				upperLeverDone = UpperLeverDone,
				lowerLeverDone = LowerLeverDone,
				serviceGateOpen = ServiceGateOpen,
				escaped = Escaped,
				items = _items.ToArray(),
				clues = _clues.ToArray(),
				playerX = position.x,
				playerZ = position.y,
				heading = heading,
			};
		}

		/// <summary>Rebuilds the state from a save, checking that it makes sense. Returns false (and leaves a fresh state) for a save that contradicts itself.</summary>
		public bool Load(EscapeSave save)
		{
			if (save == null || save.version != EscapeConfig.SaveVersion || save.episodeId != EscapeConfig.EpisodeId || save.escaped) return false;
			if (!Consistent(save)) return false;

			_items.Clear();
			_clues.Clear();
			_latchProgress = 0;
			for (var i = 0; i < _slots.Length; i++) _slots[i] = Symbol.Tree;

			ElapsedSeconds = Mathf.Max(0f, save.elapsedSeconds);
			Detections = Mathf.Max(0, save.detectionCount);
			Checkpoint = save.checkpoint == Ids.CheckpointStore || save.checkpoint == Ids.CheckpointCorridor ? save.checkpoint : Ids.CheckpointCell;
			CupMoved = save.cupMoved;
			WireTaken = save.wireTaken;
			CellLatchUnlocked = save.cellLatchUnlocked;
			CorridorReached = save.corridorReached;
			CabinetOpen = save.cabinetOpen;
			PouchTaken = save.pouchTaken;
			HalfBTaken = save.halfBTaken;
			GateKeyAssembled = save.gateKeyAssembled;
			GateKeyInserted = save.gateKeyInserted;
			UpperLeverDone = save.upperLeverDone;
			LowerLeverDone = save.lowerLeverDone;
			ServiceGateOpen = save.serviceGateOpen;
			Escaped = false;
			foreach (var item in save.items ?? new string[0]) if (!_items.Contains(item)) _items.Add(item);
			foreach (var clue in save.clues ?? new string[0]) if (!_clues.Contains(clue)) _clues.Add(clue);
			return true;
		}

		static bool Consistent(EscapeSave s)
		{
			if (s.gateKeyInserted && !s.gateKeyAssembled) return false;
			if (s.serviceGateOpen && !s.gateKeyInserted) return false;
			if (s.gateKeyAssembled && !(s.pouchTaken && s.halfBTaken)) return false;
			if (s.pouchTaken && !s.cabinetOpen) return false;
			if (s.cabinetOpen && !s.cellLatchUnlocked) return false;
			if (s.corridorReached && !s.cellLatchUnlocked) return false;
			if (s.wireTaken && !s.cupMoved) return false;
			if (s.cellLatchUnlocked && !s.wireTaken) return false;
			if (s.halfBTaken && !s.corridorReached) return false;
			return true;
		}
	}

	/// <summary>What is written to disk: plain ids and flags, never scene objects.</summary>
	[Serializable]
	public sealed class EscapeSave
	{
		public int version;
		public string episodeId;
		public float elapsedSeconds;
		public int detectionCount;
		public string checkpoint;
		public bool cupMoved;
		public bool wireTaken;
		public bool cellLatchUnlocked;
		public bool corridorReached;
		public bool cabinetOpen;
		public bool pouchTaken;
		public bool halfBTaken;
		public bool gateKeyAssembled;
		public bool gateKeyInserted;
		public bool upperLeverDone;
		public bool lowerLeverDone;
		public bool serviceGateOpen;
		public bool escaped;
		public string[] items;
		public string[] clues;
		public float playerX;
		public float playerZ;
		public float heading;
	}

	/// <summary>Volume and window mode, kept apart from the game save so they are remembered from the title screen on.</summary>
	[Serializable]
	public sealed class EscapeSettings
	{
		public float masterVolume = 0.8f;
		public float sfxVolume = 0.9f;
		public bool fullscreen = true;
	}

	/// <summary>The local stand-in for the capture that Lords will send later. Nothing is read from or sent to any server.</summary>
	public sealed class CaptureContext
	{
		public string SourceGame = "lords";
		public string CaptureReason = "scouting";
		public string ScenarioId = "demo_captured_scout";
		public string VillageName = "Example Village";
		public string MissionStatus = "CAPTURED";

		public static CaptureContext Default(bool theft = false)
		{
			return theft
				? new CaptureContext { CaptureReason = "theft", ScenarioId = "demo_caught_thief" }
				: new CaptureContext();
		}
	}

	/// <summary>How long the player has been stuck on a puzzle, and which hint that earns.</summary>
	public sealed class HintTimer
	{
		string _puzzle = "";
		float _seconds;

		public string Puzzle => _puzzle;
		public float Seconds => _seconds;

		/// <summary>0 for none yet, 1 for the contextual hint (90 s), 2 for the strong hint (180 s).</summary>
		public int OfferedLevel => _puzzle == "" ? 0 : _seconds >= EscapeConfig.StrongHintSeconds ? 2 : _seconds >= EscapeConfig.HintSeconds ? 1 : 0;

		public void Tick(float dt, string puzzle)
		{
			if (puzzle != _puzzle)
			{
				_puzzle = puzzle;
				_seconds = 0f;
			}
			if (_puzzle != "") _seconds += dt;
		}
	}

	/// <summary>The authored hints. They only help; no lock depends on them.</summary>
	public static class Hints
	{
		public static string Text(string puzzle, int level)
		{
			switch (puzzle)
			{
				case Ids.P1: return level >= 2 ? Loc.T("hint.p1.2") : Loc.T("hint.p1.1");
				case Ids.P2: return level >= 2 ? Loc.T("hint.p2.2") : Loc.T("hint.p2.1");
				case Ids.P3: return level >= 2 ? Loc.T("hint.p3.2") : Loc.T("hint.p3.1");
				default: return "";
			}
		}
	}

	// ---- the guard ----

	[Serializable]
	public struct Waypoint
	{
		public string Name;
		public Vector2 Position;
		/// <summary>Where the guard looks while waiting here.</summary>
		public Vector2 Facing;
		public float Pause;

		public Waypoint(string name, Vector2 position, Vector2 facing, float pause)
		{
			Name = name;
			Position = position;
			Facing = facing.normalized;
			Pause = pause;
		}
	}

	public struct PatrolSample
	{
		public Vector2 Position;
		public Vector2 Facing;
		public bool Paused;
		public string Waypoint;
	}

	/// <summary>
	/// The guard's loop, worked out from the time alone: wait at a waypoint, walk to the next one, and so on, turning to
	/// look where each waypoint says. The same time always gives the same place, so the patrol is predictable and testable.
	/// </summary>
	public sealed class PatrolSchedule
	{
		readonly Waypoint[] _points;
		readonly float _speed;
		readonly float _turn;
		readonly float[] _legSeconds;
		readonly float _cycle;

		public PatrolSchedule(IReadOnlyList<Waypoint> points, float speed, float turnSeconds = 0.4f)
		{
			_points = new Waypoint[points.Count];
			for (var i = 0; i < points.Count; i++) _points[i] = points[i];
			_speed = speed;
			_turn = turnSeconds;
			_legSeconds = new float[_points.Length];
			_cycle = 0f;
			for (var i = 0; i < _points.Length; i++)
			{
				var next = _points[(i + 1) % _points.Length];
				_legSeconds[i] = Vector2.Distance(_points[i].Position, next.Position) / speed;
				_cycle += _points[i].Pause + _legSeconds[i];
			}
		}

		public float CycleSeconds => _cycle;
		public int Count => _points.Length;
		public Waypoint Point(int index) => _points[index];

		static float AngleOf(Vector2 v) => Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg;

		static Vector2 FromAngle(float degrees)
		{
			var r = degrees * Mathf.Deg2Rad;
			return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
		}

		Vector2 LegDirection(int index)
		{
			var next = _points[(index + 1) % _points.Length];
			return (next.Position - _points[index].Position).normalized;
		}

		public PatrolSample Sample(float time)
		{
			var t = _cycle <= 0f ? 0f : Mathf.Repeat(time, _cycle);
			for (var i = 0; i < _points.Length; i++)
			{
				var point = _points[i];
				if (t < point.Pause)
				{
					var incoming = AngleOf(LegDirection((i + _points.Length - 1) % _points.Length));
					var outgoing = AngleOf(LegDirection(i));
					var look = AngleOf(point.Facing);
					float angle;
					if (t < _turn) angle = Mathf.LerpAngle(incoming, look, t / _turn);
					else if (t > point.Pause - _turn) angle = Mathf.LerpAngle(look, outgoing, (t - (point.Pause - _turn)) / _turn);
					else angle = look;
					return new PatrolSample { Position = point.Position, Facing = FromAngle(angle), Paused = true, Waypoint = point.Name };
				}
				t -= point.Pause;
				if (t < _legSeconds[i])
				{
					var next = _points[(i + 1) % _points.Length];
					var direction = LegDirection(i);
					return new PatrolSample { Position = Vector2.Lerp(point.Position, next.Position, t / _legSeconds[i]), Facing = direction, Paused = false, Waypoint = "" };
				}
				t -= _legSeconds[i];
			}
			return new PatrolSample { Position = _points[0].Position, Facing = _points[0].Facing, Paused = true, Waypoint = _points[0].Name };
		}
	}

	/// <summary>The guard's eyes: a narrow cone in front of the guard. Walls and hiding are checked by the caller.</summary>
	public static class GuardVision
	{
		public static bool InCone(Vector2 guard, Vector2 facing, Vector2 target, float fullAngle = EscapeConfig.SightFullAngle, float range = EscapeConfig.SightRange)
		{
			var to = target - guard;
			var distance = to.magnitude;
			if (distance > range) return false;
			if (distance < 0.35f) return true;
			var angle = Vector2.Angle(facing, to);
			return angle <= fullAngle * 0.5f;
		}
	}

	/// <summary>Suspicion: fills while the guard sees the player, fades when not. Full means caught.</summary>
	public sealed class DetectionMeter
	{
		public float Awareness { get; private set; }
		public float Grace { get; private set; }
		public bool Detected { get; private set; }

		/// <summary>Forgets everything and, for <paramref name="grace"/> seconds, cannot notice anything.</summary>
		public void Reset(float grace = 0f)
		{
			Awareness = 0f;
			Detected = false;
			Grace = grace;
		}

		public void Tick(float dt, bool visible)
		{
			if (Detected) return;
			if (Grace > 0f)
			{
				Grace = Mathf.Max(0f, Grace - dt);
				Awareness = 0f;
				return;
			}
			if (visible) Awareness += dt / EscapeConfig.DetectSeconds;
			else Awareness -= dt * EscapeConfig.AwarenessDecay;
			Awareness = Mathf.Clamp01(Awareness);
			if (Awareness >= 1f) Detected = true;
		}
	}
}
