using System.Collections.Generic;
using System.IO;
using ITKamianets.Engine.Save;
using NUnit.Framework;
using UnityEngine;

namespace Temnorid.Escape.Tests
{
	public class EscapeRulesTests
	{
		static PrisonState Fresh() => new PrisonState();

		static void SolveLatch(PrisonState s)
		{
			s.MoveCup();
			s.TakeWire();
			s.PressLatch(LatchStep.Left);
			s.PressLatch(LatchStep.Right);
			s.PressLatch(LatchStep.Right);
			Assert.AreEqual(PuzzleResult.Solved, s.PressLatch(LatchStep.Lift));
		}

		static void SolveCabinet(PrisonState s)
		{
			s.ReachCorridor();
			Assert.AreEqual(Symbol.Sun, CycleTo(s, 0, Symbol.Sun));
			CycleTo(s, 1, Symbol.Wheat);
			CycleTo(s, 2, Symbol.Moon);
			Assert.AreEqual(PuzzleResult.Solved, s.ConfirmCabinet());
		}

		static Symbol CycleTo(PrisonState s, int slot, Symbol target)
		{
			for (var i = 0; i < 4 && s.Slot(slot) != target; i++) s.CycleSlot(slot);
			return s.Slot(slot);
		}

		static void ReachGate(PrisonState s)
		{
			SolveLatch(s);
			SolveCabinet(s);
			Assert.IsTrue(s.TakePouch());
			Assert.IsTrue(s.TakeHalfB());
			Assert.IsTrue(s.CombineKey());
			Assert.IsTrue(s.InsertKey());
		}

		// ---- P1 ----

		[Test]
		public void Latch_needs_the_wire_first()
		{
			var s = Fresh();
			Assert.AreEqual(PuzzleResult.Blocked, s.PressLatch(LatchStep.Left));
			s.MoveCup();
			Assert.AreEqual(PuzzleResult.Blocked, s.PressLatch(LatchStep.Left), "the wire is under the cup but not taken yet");
			Assert.IsTrue(s.TakeWire());
			Assert.AreEqual(PuzzleResult.Progress, s.PressLatch(LatchStep.Left));
		}

		[Test]
		public void Latch_rejects_a_wrong_press_and_starts_over_without_losing_the_wire()
		{
			var s = Fresh();
			s.MoveCup();
			s.TakeWire();
			Assert.AreEqual(PuzzleResult.Progress, s.PressLatch(LatchStep.Left));
			Assert.AreEqual(PuzzleResult.Progress, s.PressLatch(LatchStep.Right));
			Assert.AreEqual(PuzzleResult.Wrong, s.PressLatch(LatchStep.Lift));
			Assert.AreEqual(0, s.LatchProgress);
			Assert.IsTrue(s.Has(Ids.Wire));
			Assert.IsFalse(s.CellLatchUnlocked);
		}

		[Test]
		public void Latch_accepts_left_right_right_lift_exactly()
		{
			var s = Fresh();
			SolveLatch(s);
			Assert.IsTrue(s.CellLatchUnlocked);
			Assert.AreEqual(Milestone.CellOpen, s.Milestone);
			Assert.AreEqual(PuzzleResult.Blocked, s.PressLatch(LatchStep.Left), "already open");
			Assert.IsTrue(s.Has(Ids.Wire), "the wire stays");
		}

		[Test]
		public void The_wire_is_given_once_and_only_after_the_cup_moves()
		{
			var s = Fresh();
			Assert.IsFalse(s.TakeWire());
			Assert.IsTrue(s.MoveCup());
			Assert.IsFalse(s.MoveCup());
			Assert.IsTrue(s.TakeWire());
			Assert.IsFalse(s.TakeWire());
			Assert.AreEqual(1, s.Items.Count);
		}

		// ---- P2 ----

		[Test]
		public void Cabinet_rejects_wrong_symbols_and_does_not_lock()
		{
			var s = Fresh();
			SolveLatch(s);
			s.ReachCorridor();
			Assert.AreEqual(PuzzleResult.Wrong, s.ConfirmCabinet(), "all trees");
			CycleTo(s, 0, Symbol.Moon);
			CycleTo(s, 1, Symbol.Wheat);
			CycleTo(s, 2, Symbol.Sun);
			Assert.AreEqual(PuzzleResult.Wrong, s.ConfirmCabinet());
			Assert.AreEqual(PuzzleResult.Wrong, s.ConfirmCabinet(), "it can be tried again");
			Assert.IsFalse(s.CabinetOpen);
		}

		[Test]
		public void Cabinet_opens_with_sun_wheat_moon_once_and_gives_half_a_once()
		{
			var s = Fresh();
			SolveLatch(s);
			Assert.IsFalse(s.TakePouch(), "the pouch is inside a locked cabinet");
			SolveCabinet(s);
			Assert.AreEqual(PuzzleResult.Blocked, s.ConfirmCabinet());
			Assert.IsTrue(s.TakePouch());
			Assert.IsFalse(s.TakePouch());
			Assert.AreEqual(1, Count(s, Ids.HalfA));
			Assert.AreEqual(Milestone.CabinetOpen, s.Milestone);
		}

		[Test]
		public void Each_slot_cycles_through_four_symbols_in_a_fixed_order()
		{
			var s = Fresh();
			var seen = new List<Symbol> { s.Slot(0) };
			for (var i = 0; i < 4; i++) seen.Add(s.CycleSlot(0));
			Assert.AreEqual(new[] { Symbol.Tree, Symbol.Sun, Symbol.Wheat, Symbol.Moon, Symbol.Tree }, seen.ToArray());
		}

		static int Count(PrisonState s, string id)
		{
			var n = 0;
			foreach (var item in s.Items) if (item == id) n++;
			return n;
		}

		// ---- key ----

		[Test]
		public void Key_halves_combine_atomically_exactly_once()
		{
			var s = Fresh();
			SolveLatch(s);
			SolveCabinet(s);
			s.TakePouch();
			Assert.IsFalse(s.CombineKey(), "only half A");
			Assert.IsFalse(s.Has(Ids.KeyComplete));
			Assert.IsTrue(s.TakeHalfB());
			Assert.IsFalse(s.TakeHalfB(), "half B is given once");
			Assert.IsTrue(s.CanCombine);
			Assert.IsTrue(s.CombineKey());
			Assert.IsFalse(s.CombineKey(), "no second key");
			Assert.IsTrue(s.Has(Ids.KeyComplete));
			Assert.IsFalse(s.Has(Ids.HalfA));
			Assert.IsFalse(s.Has(Ids.HalfB));
			Assert.AreEqual(1, Count(s, Ids.KeyComplete));
			Assert.AreEqual(Milestone.KeyAssembled, s.Milestone);
		}

		// ---- P3 ----

		[Test]
		public void Levers_do_nothing_before_the_key_is_in_the_lock()
		{
			var s = Fresh();
			Assert.AreEqual(LeverResult.Blocked, s.PullLever(Lever.Upper));
			Assert.AreEqual(LeverResult.Blocked, s.PullLever(Lever.Lower));
			Assert.IsFalse(s.InsertKey(), "no key yet");
			Assert.IsFalse(s.ServiceGateOpen);
		}

		[Test]
		public void Lower_first_resets_the_levers_then_upper_and_lower_open_the_gate()
		{
			var s = Fresh();
			ReachGate(s);
			Assert.AreEqual(Milestone.GateUnlocked, s.Milestone);
			Assert.AreEqual(LeverResult.WrongOrder, s.PullLever(Lever.Lower));
			Assert.IsFalse(s.UpperLeverDone);
			Assert.AreEqual(LeverResult.UpperRaised, s.PullLever(Lever.Upper));
			Assert.AreEqual(LeverResult.AlreadyDone, s.PullLever(Lever.Upper));
			Assert.AreEqual(LeverResult.GateOpened, s.PullLever(Lever.Lower));
			Assert.IsTrue(s.ServiceGateOpen);
			Assert.AreEqual(Milestone.GateOpen, s.Milestone);
			Assert.IsTrue(s.Has(Ids.KeyComplete), "the key is not used up");
			Assert.AreEqual(LeverResult.Blocked, s.PullLever(Lever.Upper), "the gate stays open");
		}

		[Test]
		public void Milestones_come_in_order_and_victory_cannot_be_skipped()
		{
			var s = Fresh();
			Assert.IsFalse(s.TryEscape());
			Assert.AreEqual(Milestone.CellLocked, s.Milestone);
			Assert.IsFalse(s.ReachCorridor(), "the cell is still locked");
			SolveLatch(s);
			Assert.AreEqual(Milestone.CellOpen, s.Milestone);
			Assert.IsTrue(s.ReachCorridor());
			Assert.AreEqual(Milestone.CorridorAccess, s.Milestone);
			Assert.IsFalse(s.TryEscape());
			SolveCabinet(s);
			s.TakePouch();
			s.TakeHalfB();
			s.CombineKey();
			Assert.AreEqual(Milestone.KeyAssembled, s.Milestone);
			Assert.IsFalse(s.TryEscape());
			s.InsertKey();
			Assert.AreEqual(Milestone.GateUnlocked, s.Milestone);
			Assert.IsFalse(s.TryEscape());
			s.PullLever(Lever.Upper);
			Assert.IsFalse(s.TryEscape());
			s.PullLever(Lever.Lower);
			Assert.AreEqual(Milestone.GateOpen, s.Milestone);
			Assert.IsTrue(s.TryEscape());
			Assert.AreEqual(Milestone.Escaped, s.Milestone);
			Assert.IsFalse(s.TryEscape());
		}

		[Test]
		public void The_current_puzzle_follows_the_story()
		{
			var s = Fresh();
			Assert.AreEqual(Ids.P1, s.CurrentPuzzle);
			SolveLatch(s);
			Assert.AreEqual(Ids.P2, s.CurrentPuzzle);
			SolveCabinet(s);
			Assert.AreEqual(Ids.P3, s.CurrentPuzzle);
			s.TakePouch();
			s.TakeHalfB();
			s.CombineKey();
			s.InsertKey();
			s.PullLever(Lever.Upper);
			s.PullLever(Lever.Lower);
			Assert.AreEqual("", s.CurrentPuzzle);
		}

		// ---- saving ----

		[Test]
		public void Save_round_trips_flags_and_inventory()
		{
			var s = Fresh();
			ReachGate(s);
			s.Read(Ids.ClueWall);
			s.Caught();
			s.Caught();
			s.ElapsedSeconds = 123.5f;
			var save = s.ToSave(new Vector2(3f, -4f), 90f);

			var path = Path.Combine(Path.GetTempPath(), "escape-edit-" + System.Guid.NewGuid().ToString("N"));
			try
			{
				var store = new EscapeSaveStore(path);
				Assert.AreEqual(SaveStatus.Ok, store.Save(save));
				var loaded = store.Load(out var status);
				Assert.AreEqual(SaveStatus.Ok, status);
				var t = Fresh();
				Assert.IsTrue(t.Load(loaded));
				Assert.AreEqual(Milestone.GateUnlocked, t.Milestone);
				Assert.IsTrue(t.Has(Ids.KeyComplete));
				Assert.IsFalse(t.Has(Ids.HalfA));
				Assert.AreEqual(2, t.Detections);
				Assert.AreEqual(123.5f, t.ElapsedSeconds, 0.001f);
				Assert.AreEqual(1, t.Clues.Count);
				Assert.AreEqual(3f, loaded.playerX);
				Assert.AreEqual(-4f, loaded.playerZ);
			}
			finally
			{
				if (Directory.Exists(path)) Directory.Delete(path, true);
			}
		}

		[Test]
		public void A_loaded_state_does_not_hand_out_the_halves_again()
		{
			var s = Fresh();
			ReachGate(s);
			var t = Fresh();
			Assert.IsTrue(t.Load(s.ToSave(Vector2.zero, 0f)));
			Assert.IsFalse(t.TakePouch());
			Assert.IsFalse(t.TakeHalfB());
			Assert.IsFalse(t.CombineKey());
			Assert.AreEqual(1, Count(t, Ids.KeyComplete));
		}

		[Test]
		public void Load_rejects_wrong_versions_other_episodes_finished_games_and_contradictions()
		{
			var good = Fresh().ToSave(Vector2.zero, 0f);
			var s = Fresh();
			Assert.IsTrue(s.Load(good));

			var bad = Fresh().ToSave(Vector2.zero, 0f);
			bad.version = 99;
			Assert.IsFalse(Fresh().Load(bad));

			bad = Fresh().ToSave(Vector2.zero, 0f);
			bad.episodeId = "other";
			Assert.IsFalse(Fresh().Load(bad));

			bad = Fresh().ToSave(Vector2.zero, 0f);
			bad.escaped = true;
			Assert.IsFalse(Fresh().Load(bad));

			bad = Fresh().ToSave(Vector2.zero, 0f);
			bad.serviceGateOpen = true; // the gate cannot be open without its key
			Assert.IsFalse(Fresh().Load(bad));

			bad = Fresh().ToSave(Vector2.zero, 0f);
			bad.pouchTaken = true; // from a cabinet that is shut
			Assert.IsFalse(Fresh().Load(bad));

			Assert.IsFalse(Fresh().Load(null));
		}

		[Test]
		public void Corrupt_save_files_are_reported_not_thrown()
		{
			var path = Path.Combine(Path.GetTempPath(), "escape-edit-" + System.Guid.NewGuid().ToString("N"));
			try
			{
				var store = new EscapeSaveStore(path);
				Directory.CreateDirectory(Path.GetDirectoryName(store.Path));
				File.WriteAllText(store.Path, "{ this is not json");
				var loaded = store.Load(out var status);
				Assert.IsNull(loaded);
				Assert.AreNotEqual(SaveStatus.Ok, status);
			}
			finally
			{
				if (Directory.Exists(path)) Directory.Delete(path, true);
			}
		}

		[Test]
		public void Being_caught_changes_only_the_counter()
		{
			var s = Fresh();
			ReachGate(s);
			var before = s.ToSave(Vector2.zero, 0f);
			s.Caught();
			var after = s.ToSave(Vector2.zero, 0f);
			Assert.AreEqual(before.detectionCount + 1, after.detectionCount);
			Assert.AreEqual(Milestone.GateUnlocked, s.Milestone);
			Assert.IsTrue(s.Has(Ids.KeyComplete));
			Assert.AreEqual(before.items.Length, after.items.Length);
		}

		[Test]
		public void Checkpoints_follow_the_player_and_never_go_back_to_the_cell_after_it_opens()
		{
			var s = Fresh();
			Assert.AreEqual(Ids.CheckpointCell, s.Checkpoint);
			SolveLatch(s);
			s.ReachCorridor();
			Assert.AreEqual(Ids.CheckpointCorridor, s.Checkpoint);
			Assert.IsTrue(s.ReachStore());
			Assert.AreEqual(Ids.CheckpointStore, s.Checkpoint);
			Assert.AreEqual(PrisonLayout.StoreCheckpoint, PrisonLayout.CheckpointPosition(s.Checkpoint));
		}

		// ---- hints ----

		[Test]
		public void Hints_come_after_90_and_180_seconds_on_one_puzzle_and_reset_when_it_changes()
		{
			var t = new HintTimer();
			t.Tick(30f, Ids.P1);
			Assert.AreEqual(0, t.OfferedLevel);
			t.Tick(60f, Ids.P1);
			Assert.AreEqual(1, t.OfferedLevel);
			t.Tick(90f, Ids.P1);
			Assert.AreEqual(2, t.OfferedLevel);
			t.Tick(1f, Ids.P2);
			Assert.AreEqual(0, t.OfferedLevel);
			t.Tick(5f, "");
			Assert.AreEqual(0, t.OfferedLevel);
			Assert.AreEqual(Loc.T("hint.p1.1"), Hints.Text(Ids.P1, 1));
			Assert.AreEqual(Loc.T("hint.p3.2"), Hints.Text(Ids.P3, 2));
		}

		[Test]
		public void Every_text_key_used_by_the_game_exists()
		{
			foreach (var id in new[] { Ids.CellBed, Ids.CellWallClue, Ids.CellCup, Ids.CellWire, Ids.CellLatch, Ids.CorridorHidingSpot, Ids.StoreroomLedger, Ids.StoreShelf, Ids.RecordsCabinet,
				Ids.ConfiscatedPouch, Ids.WorkbenchKeyPiece, Ids.GatePlaque, Ids.GateKeyhole, Ids.LeverUpper, Ids.LeverLower })
				Assert.IsTrue(Loc.Has("o." + id), id);
			foreach (var id in new[] { Ids.Wire, Ids.HalfA, Ids.HalfB, Ids.KeyComplete })
			{
				Assert.IsTrue(Loc.Has("i." + id), id);
				Assert.IsTrue(Loc.Has("i." + id + ".d"), id);
			}
			foreach (Symbol symbol in System.Enum.GetValues(typeof(Symbol))) Assert.IsTrue(Loc.Has("s." + symbol));
			foreach (var clue in new[] { Ids.ClueWall, Ids.ClueLedger, Ids.ClueNote, Ids.CluePlaque }) Assert.IsTrue(Loc.Has(clue));
		}

		// ---- the guard ----

		[Test]
		public void The_patrol_is_deterministic_and_loops_desk_window_end_door_desk()
		{
			var schedule = PrisonLayout.CreateSchedule();
			Assert.AreEqual(3, schedule.Count);
			Assert.AreEqual("Desk", schedule.Sample(0.5f).Waypoint);
			Assert.IsTrue(schedule.Sample(0.5f).Paused);
			Assert.AreEqual(schedule.Sample(7.3f).Position, schedule.Sample(7.3f).Position);
			Assert.AreEqual(schedule.Sample(7.3f).Position.x, schedule.Sample(7.3f + schedule.CycleSeconds).Position.x, 0.001f);

			var seen = new List<string>();
			for (var t = 0f; t < schedule.CycleSeconds; t += 0.1f)
			{
				var s = schedule.Sample(t);
				if (s.Paused && (seen.Count == 0 || seen[seen.Count - 1] != s.Waypoint)) seen.Add(s.Waypoint);
			}
			Assert.AreEqual(new[] { "Desk", "Window", "EndDoor" }, seen.ToArray());
		}

		[Test]
		public void The_guard_pauses_at_least_five_seconds_at_the_window_looking_at_the_wall()
		{
			var schedule = PrisonLayout.CreateSchedule();
			var window = 0f;
			for (var t = 0f; t < schedule.CycleSeconds; t += 0.05f)
			{
				var s = schedule.Sample(t);
				if (s.Paused && s.Waypoint == "Window")
				{
					window += 0.05f;
					if (window > 1f && window < 4f) Assert.Greater(Vector2.Dot(s.Facing, Vector2.up), 0.99f, "facing the wall, away from the corridor");
				}
			}
			Assert.GreaterOrEqual(window, 5f - 0.1f);
		}

		[Test]
		public void The_cone_is_80_degrees_6_metres_and_in_front_only()
		{
			var guard = Vector2.zero;
			var facing = Vector2.up;
			Assert.IsTrue(GuardVision.InCone(guard, facing, new Vector2(0f, 5f)));
			Assert.IsFalse(GuardVision.InCone(guard, facing, new Vector2(0f, 6.5f)), "too far");
			Assert.IsTrue(GuardVision.InCone(guard, facing, new Vector2(3f, 4f)), "37 degrees");
			Assert.IsFalse(GuardVision.InCone(guard, facing, new Vector2(4f, 3f)), "53 degrees");
			Assert.IsFalse(GuardVision.InCone(guard, facing, new Vector2(0f, -3f)), "behind the guard");
		}

		[Test]
		public void Detection_takes_one_continuous_second_and_fades_when_the_player_is_out_of_sight()
		{
			var m = new DetectionMeter();
			for (var i = 0; i < 9; i++) m.Tick(0.1f, true);
			Assert.IsFalse(m.Detected);
			m.Tick(0.2f, true);
			Assert.IsTrue(m.Detected);

			m.Reset();
			m.Tick(0.6f, true);
			m.Tick(0.4f, false);
			Assert.Less(m.Awareness, 0.1f);
			m.Tick(0.6f, true);
			Assert.IsFalse(m.Detected, "a glimpse is forgiven");
		}

		[Test]
		public void Right_after_a_capture_the_guard_cannot_notice_anything_for_the_grace_time()
		{
			var m = new DetectionMeter();
			m.Reset(EscapeConfig.GraceSeconds);
			for (var t = 0f; t < EscapeConfig.GraceSeconds - 0.1f; t += 0.1f) m.Tick(0.1f, true);
			Assert.IsFalse(m.Detected);
			Assert.AreEqual(0f, m.Awareness);
		}

		/// <summary>A runner crossing from the alcove to the storeroom doorway in plain sight of the guard (no walls in the way).</summary>
		static bool RunIsSafe(PatrolSchedule schedule, float start, float speed)
		{
			var from = PrisonLayout.HideExit;
			var to = PrisonLayout.StoreDoorway;
			var length = Vector2.Distance(from, to);
			var meter = new DetectionMeter();
			for (var t = 0f; t <= length / speed; t += 0.02f)
			{
				var player = Vector2.Lerp(from, to, t * speed / length);
				var guard = schedule.Sample(start + t);
				meter.Tick(0.02f, GuardVision.InCone(guard.Position, guard.Facing, player));
				if (meter.Detected) return false;
			}
			return true;
		}

		[Test]
		public void There_is_a_safe_window_of_at_least_four_seconds_every_cycle_to_cross_to_the_storeroom()
		{
			var schedule = PrisonLayout.CreateSchedule();
			// The longest run of start times that get across unseen, walking at the normal speed.
			var best = 0f;
			var current = 0f;
			for (var start = 0f; start < schedule.CycleSeconds * 2f; start += 0.05f)
			{
				if (RunIsSafe(schedule, start, EscapeConfig.MoveSpeed)) current += 0.05f;
				else current = 0f;
				if (current > best) best = current;
			}
			Assert.GreaterOrEqual(best, 4f, "safe window (s): " + best);
			Assert.Less(best, schedule.CycleSeconds - 4f, "the player cannot just ignore the guard: " + best);
		}

		[Test]
		public void A_player_standing_in_the_middle_of_the_corridor_is_caught_by_the_patrol()
		{
			var schedule = PrisonLayout.CreateSchedule();
			var meter = new DetectionMeter();
			var player = new Vector2(0f, 0f);
			for (var t = 0f; t < schedule.CycleSeconds; t += 0.02f)
			{
				var guard = schedule.Sample(t);
				meter.Tick(0.02f, GuardVision.InCone(guard.Position, guard.Facing, player));
			}
			Assert.IsTrue(meter.Detected);
		}
	}
}
