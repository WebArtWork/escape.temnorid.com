using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ITKamianets.Engine.FixedCamera;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Escape.Tests
{
	public class EscapeRunTests
	{
		string _saveFolder;

		[SetUp]
		public void SetUp()
		{
			_saveFolder = Path.Combine(Path.GetTempPath(), "escape-playmode-" + Guid.NewGuid());
			EscapeGame.SaveRootOverride = _saveFolder;
		}

		[TearDown]
		public void TearDown()
		{
			EscapeGame.SaveRootOverride = null;
			Time.timeScale = 1f;
			if (Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder, true);
		}

		static IEnumerator Load()
		{
			yield return SceneManager.LoadSceneAsync("Prison");
			yield return null;
			yield return null;
		}

		static IEnumerator Frames(int count)
		{
			for (var i = 0; i < count; i++) yield return null;
		}

		/// <summary>Walks in straight lines through the given points, as a player holding the keys would.</summary>
		static IEnumerator Walk(EscapeGame g, bool run, params Vector2[] points)
		{
			foreach (var point in points)
			{
				var start = Time.realtimeSinceStartup;
				while (g.Player.ScriptedWalkTo(point, run) > 0.25f)
				{
					if (g.Mode == Mode.Victory) yield break;
					if (Time.realtimeSinceStartup - start > 40f) Assert.Fail("Stuck on the way to " + point + " at " + g.Player.Position2D + " in mode " + g.Mode);
					yield return null;
				}
				g.Player.ScriptedInput = null;
			}
			yield return null;
			yield return null;
		}

		static IEnumerator UseObject(EscapeGame g, string id)
		{
			yield return Frames(2);
			Assert.IsNotNull(g.Target, "something to use for " + id + " at " + g.Player.Position2D);
			Assert.AreEqual(id, g.Target.Id, "the one prompt at " + g.Player.Position2D);
			Assert.IsTrue(g.PromptText.StartsWith("E — "), g.PromptText);
			Assert.IsTrue(g.Interact());
			yield return Frames(2);
		}

		static float RouteLength(Vector2[] route)
		{
			var length = 0f;
			for (var i = 1; i < route.Length; i++) length += Vector2.Distance(route[i - 1], route[i]);
			return length;
		}

		static Vector2 PointAt(Vector2[] route, float distance)
		{
			for (var i = 1; i < route.Length; i++)
			{
				var leg = Vector2.Distance(route[i - 1], route[i]);
				if (distance <= leg) return Vector2.Lerp(route[i - 1], route[i], leg <= 0f ? 0f : distance / leg);
				distance -= leg;
			}
			return route[route.Length - 1];
		}

		/// <summary>Whether running this route now, from the guard's own schedule, would get the player noticed.</summary>
		static bool SafeToRun(EscapeGame g, Vector2[] route, float speed)
		{
			var schedule = g.Guard.Schedule;
			var meter = new DetectionMeter();
			var start = g.Guard.PatrolTime + 0.3f;
			var length = RouteLength(route);
			for (var t = 0f; t <= length / speed + 0.3f; t += 0.05f)
			{
				var guard = schedule.Sample(start + t);
				meter.Tick(0.05f, GuardVision.InCone(guard.Position, guard.Facing, PointAt(route, t * speed)));
				if (meter.Awareness > 0.4f) return false;
			}
			return true;
		}

		static IEnumerator WaitUntilSafe(EscapeGame g, params Vector2[] route)
		{
			var start = Time.realtimeSinceStartup;
			while (!SafeToRun(g, route, EscapeConfig.FastSpeed))
			{
				if (Time.realtimeSinceStartup - start > 60f) Assert.Fail("The guard never gave a safe moment");
				yield return null;
			}
		}

		static IEnumerator Begin(EscapeGame g)
		{
			g.NewGame();
			yield return Frames(2);
			Assert.AreEqual(Mode.Intro, g.Mode);
			g.BeginPlay();
			Assert.AreEqual(Mode.Playing, g.Mode);
		}

		/// <summary>Puts a game straight into a state by the rules, saves it and loads it, so the world matches the state.</summary>
		static IEnumerator Jump(EscapeGame g, Action<PrisonState> setup)
		{
			setup(g.State);
			g.SaveNow();
			g.ToTitle();
			yield return Frames(2);
			Assert.IsTrue(g.HasSave, "the save is valid");
			Assert.IsTrue(g.ContinueGame());
			yield return Frames(3);
		}

		static void SolveCell(PrisonState s)
		{
			s.MoveCup();
			s.TakeWire();
			s.PressLatch(LatchStep.Left);
			s.PressLatch(LatchStep.Right);
			s.PressLatch(LatchStep.Right);
			s.PressLatch(LatchStep.Lift);
			s.ReachCorridor();
		}

		static void SolveStore(PrisonState s)
		{
			SolveCell(s);
			s.ReachStore();
			s.CycleSlot(0);
			s.CycleSlot(1);
			s.CycleSlot(1);
			s.CycleSlot(2);
			s.CycleSlot(2);
			s.CycleSlot(2);
			s.ConfirmCabinet();
			s.TakePouch();
		}

		// ---- the scene ----

		[UnityTest]
		public IEnumerator The_game_opens_on_the_title_with_the_whole_prison_in_the_scene()
		{
			yield return Load();
			var g = EscapeGame.Instance;
			Assert.IsNotNull(g);
			Assert.AreEqual(Mode.Title, g.Mode);
			Assert.IsFalse(g.HasSave);
			Assert.IsNotNull(g.Player);
			Assert.IsNotNull(g.Guard);
			Assert.IsNotNull(g.Rig);

			var ids = new HashSet<string>();
			foreach (var item in Interactable.All) ids.Add(item.Id);
			foreach (var id in new[] { Ids.CellBed, Ids.CellWallClue, Ids.CellCup, Ids.CellWire, Ids.CellLatch, Ids.CorridorHidingSpot, Ids.StoreroomLedger, Ids.StoreShelf, Ids.RecordsCabinet,
				Ids.ConfiscatedPouch, Ids.WorkbenchKeyPiece, Ids.GatePlaque, Ids.GateKeyhole, Ids.LeverUpper, Ids.LeverLower })
				Assert.IsTrue(ids.Contains(id), "interactable " + id);

			var shots = new HashSet<string>();
			foreach (var volume in CameraShotVolume.All) shots.Add(volume.ShotId);
			foreach (var id in new[] { "cam_cell_wide", "cam_cell_detail", "cam_corridor_long", "cam_corridor_alcove", "cam_store_wide", "cam_store_desk", "cam_service_gate", "cam_exit_courtyard" })
				Assert.IsTrue(shots.Contains(id), "shot " + id);
		}

		[UnityTest]
		public IEnumerator Every_walkable_spot_has_a_camera_shot_and_borders_do_not_flicker()
		{
			yield return Load();
			var rooms = new[]
			{
				new Rect(-15.6f, -3.6f, 7.2f, 7.2f),
				new Rect(-7.6f, -1.6f, 19.2f, 3.2f),
				new Rect(-4.8f, -4.0f, 2.6f, 2.0f),
				new Rect(2.4f, -11.6f, 11.2f, 9.2f),
				new Rect(14.4f, -7.6f, 11.2f, 3.2f),
				new Rect(26.4f, -13.6f, 13.2f, 15.2f),
			};
			foreach (var room in rooms)
				for (var x = room.xMin; x <= room.xMax; x += 0.4f)
					for (var z = room.yMin; z <= room.yMax; z += 0.4f)
						Assert.IsNotNull(CameraShotVolume.Select(CameraShotVolume.All, new Vector3(x, 0f, z), null, 0.3f), "no shot at " + x + "," + z);

			// Pacing back and forth across a border by 20 cm must not change the shot more than the one time it has to.
			CameraShotVolume current = CameraShotVolume.Select(CameraShotVolume.All, new Vector3(0f, 0f, -1.9f), null, 0.3f);
			var changes = 0;
			for (var i = 0; i < 100; i++)
			{
				var z = -2.2f + (i % 2 == 0 ? 0.1f : -0.1f);
				var next = CameraShotVolume.Select(CameraShotVolume.All, new Vector3(8f, 0f, z), current, 0.3f);
				if (next != current) changes++;
				current = next;
			}
			Assert.LessOrEqual(changes, 1, "shot changes while pacing at the corridor/storeroom border");
		}

		// ---- the whole episode ----

		[UnityTest]
		public IEnumerator A_player_can_escape_the_prison_from_new_game_to_victory_and_replay_it()
		{
			yield return Load();
			var g = EscapeGame.Instance;
			Time.timeScale = 3f;
			yield return Begin(g);
			var p = g.Player;
			Assert.AreEqual(Milestone.CellLocked, g.State.Milestone);

			// The cell: the clue, the cup, the wire, the latch.
			yield return Walk(g, false, new Vector2(-10.6f, -3.0f));
			yield return UseObject(g, Ids.CellWallClue);
			Assert.AreEqual(Mode.Reading, g.Mode);
			g.CloseReading();
			Assert.AreEqual(Mode.Playing, g.Mode);

			yield return Walk(g, false, new Vector2(-12.4f, -2.2f));
			yield return UseObject(g, Ids.CellCup);
			yield return Walk(g, false, new Vector2(-12.0f, -2.6f));
			yield return UseObject(g, Ids.CellWire);
			Assert.IsTrue(g.State.Has(Ids.Wire));

			yield return Walk(g, false, new Vector2(-9.5f, -0.5f), new Vector2(-8.7f, 0f));
			yield return UseObject(g, Ids.CellLatch);
			Assert.AreEqual(Mode.Latch, g.Mode);
			Assert.IsTrue(g.Rig.Overriding, "the close-up shot");
			Assert.AreEqual(PuzzleResult.Wrong, g.LatchPress(LatchStep.Lift));
			Assert.AreEqual(PuzzleResult.Progress, g.LatchPress(LatchStep.Left));
			g.ClosePuzzle();
			Assert.AreEqual(Mode.Playing, g.Mode);
			Assert.IsFalse(g.Rig.Overriding, "back to the shot before");
			yield return UseObject(g, Ids.CellLatch);
			g.LatchPress(LatchStep.Left);
			g.LatchPress(LatchStep.Right);
			g.LatchPress(LatchStep.Right);
			Assert.AreEqual(PuzzleResult.Solved, g.LatchPress(LatchStep.Lift));
			Assert.AreEqual(Mode.Playing, g.Mode);
			Assert.IsTrue(g.State.CellLatchUnlocked);
			Assert.IsTrue(g.Saves >= 2, "autosaved");
			Assert.AreEqual(Milestone.CellOpen, g.State.Milestone);

			// The corridor: out of the cell, into the alcove, wait for the guard, across to the storeroom.
			yield return Walk(g, false, new Vector2(-6.6f, -1.1f));
			Assert.IsTrue(g.State.CorridorReached);
			Assert.AreEqual("cam_corridor_long", g.Rig.ShotId);
			Assert.AreEqual(Ids.CheckpointCorridor, g.State.Checkpoint);
			var hideRun = new[] { p.Position2D, new Vector2(-3.5f, -1.4f) };
			yield return WaitUntilSafe(g, hideRun);
			yield return Walk(g, true, new Vector2(-3.5f, -1.4f));
			yield return UseObject(g, Ids.CorridorHidingSpot);
			Assert.IsTrue(g.Hidden);
			Assert.AreEqual("cam_corridor_alcove", g.Rig.ShotId);
			var cross = new[] { PrisonLayout.HideExit, new Vector2(8f, -1.7f), new Vector2(8f, -4.6f) };
			yield return WaitUntilSafe(g, cross);
			g.Use(Ids.CorridorHidingSpot);
			Assert.IsFalse(g.Hidden);
			yield return Walk(g, true, cross[1], cross[2]);
			Assert.AreEqual(Ids.CheckpointStore, g.State.Checkpoint);
			Assert.AreEqual("cam_store_wide", g.Rig.ShotId);

			// The storeroom.
			yield return Walk(g, false, new Vector2(4.2f, -10.9f));
			yield return UseObject(g, Ids.StoreroomLedger);
			Assert.AreEqual(Mode.Reading, g.Mode);
			g.CloseReading();
			yield return Walk(g, false, new Vector2(6f, -8f), new Vector2(12f, -8f), new Vector2(12.1f, -10.6f));
			yield return UseObject(g, Ids.RecordsCabinet);
			Assert.AreEqual(Mode.Cabinet, g.Mode);
			Assert.AreEqual(PuzzleResult.Wrong, g.CabinetConfirm());
			g.CabinetCycle(0);
			g.CabinetCycle(1);
			g.CabinetCycle(1);
			g.CabinetCycle(2);
			g.CabinetCycle(2);
			Assert.AreEqual(PuzzleResult.Wrong, g.CabinetConfirm(), "moon not set yet");
			g.ClosePuzzle();
			yield return UseObject(g, Ids.RecordsCabinet);
			g.CabinetCycle(2);
			Assert.AreEqual(PuzzleResult.Solved, g.CabinetConfirm());
			Assert.IsTrue(g.State.CabinetOpen);
			yield return UseObject(g, Ids.ConfiscatedPouch);
			Assert.AreEqual(Mode.Reading, g.Mode, "the note");
			g.CloseReading();
			Assert.IsTrue(g.State.Has(Ids.HalfA));

			// The service passage.
			yield return Walk(g, false, new Vector2(12f, -6f), new Vector2(16f, -6f), new Vector2(17.5f, -6.4f));
			yield return UseObject(g, Ids.WorkbenchKeyPiece);
			Assert.IsTrue(g.State.Has(Ids.HalfB));
			yield return Walk(g, false, new Vector2(25.2f, -5.0f));
			yield return UseObject(g, Ids.GateKeyhole);
			Assert.IsTrue(g.State.Has(Ids.KeyComplete), "combined at the gate");
			Assert.IsFalse(g.State.Has(Ids.HalfA));
			yield return UseObject(g, Ids.GateKeyhole);
			Assert.IsTrue(g.State.GateKeyInserted);
			Assert.AreEqual(Milestone.GateUnlocked, g.State.Milestone);

			yield return Walk(g, false, new Vector2(22.6f, -5.0f));
			yield return UseObject(g, Ids.LeverLower);
			Assert.IsFalse(g.State.ServiceGateOpen, "lower first does nothing");
			yield return Walk(g, false, new Vector2(21.4f, -5.0f));
			yield return UseObject(g, Ids.LeverUpper);
			Assert.IsTrue(g.State.UpperLeverDone);
			yield return Walk(g, false, new Vector2(22.6f, -5.0f));
			yield return UseObject(g, Ids.LeverLower);
			Assert.IsTrue(g.State.ServiceGateOpen);
			Assert.IsTrue(g.Gate.IsOpen);
			Assert.AreEqual(Milestone.GateOpen, g.State.Milestone);

			// Out.
			yield return Walk(g, true, new Vector2(24f, -6f), new Vector2(29f, -6f), new Vector2(PrisonLayout.ExitLineX + 1.5f, -6f));
			yield return Frames(3);
			Assert.AreEqual(Mode.Victory, g.Mode);
			Assert.IsTrue(g.State.Escaped);
			Assert.AreEqual(0, g.State.Detections, "never caught");
			Assert.IsFalse(g.HasSave, "a finished game cannot be continued");
			Time.timeScale = 1f;

			// Replay starts from nothing.
			g.Replay();
			yield return Frames(3);
			Assert.AreEqual(Mode.Intro, g.Mode);
			Assert.IsFalse(g.State.CellLatchUnlocked);
			Assert.AreEqual(0, g.State.Items.Count);
			Assert.AreEqual(0f, g.State.ElapsedSeconds, 0.01f);
			Assert.AreEqual(0, g.State.Detections);
			Assert.IsFalse(g.CellDoor.IsOpen);
			Assert.IsFalse(g.Gate.IsOpen);
			Assert.AreEqual(PrisonLayout.CellStart.x, g.Player.Position2D.x, 0.1f);
		}

		// ---- the guard ----

		[UnityTest]
		public IEnumerator A_player_in_plain_view_is_caught_and_restarts_at_the_corridor_with_everything_kept()
		{
			yield return Load();
			var g = EscapeGame.Instance;
			g.NewGame();
			yield return Frames(2);
			yield return Jump(g, SolveStore);
			Assert.AreEqual(Mode.Playing, g.Mode);
			var before = g.State.Items.Count;
			g.Player.Teleport(new Vector2(0f, 0f), 90f);
			g.Guard.ResetPatrol(0f);
			Time.timeScale = 4f;
			var start = Time.realtimeSinceStartup;
			while (g.Mode != Mode.Caught)
			{
				if (Time.realtimeSinceStartup - start > 60f) Assert.Fail("never caught in the middle of the corridor");
				yield return null;
			}
			Assert.AreEqual(1, g.State.Detections);
			while (g.Mode == Mode.Caught)
			{
				if (Time.realtimeSinceStartup - start > 90f) Assert.Fail("the restart never came");
				yield return null;
			}
			Time.timeScale = 1f;
			Assert.AreEqual(Mode.Playing, g.Mode);
			Assert.AreEqual(PrisonLayout.CorridorCheckpoint.x, g.Player.Position2D.x, 0.2f);
			Assert.AreEqual(PrisonLayout.CorridorCheckpoint.y, g.Player.Position2D.y, 0.2f);
			Assert.Less(g.Guard.PatrolTime, 1f, "the patrol starts over");
			Assert.IsTrue(g.State.CellLatchUnlocked);
			Assert.IsTrue(g.State.CabinetOpen);
			Assert.AreEqual(before, g.State.Items.Count, "nothing in the inventory is lost");
			Assert.IsTrue(g.CellDoor.IsOpen);

			// A moment of grace: right after the restart the guard cannot notice anything.
			yield return Frames(3);
			Assert.AreEqual(0f, g.GuardAwareness, 0.001f);
		}

		[UnityTest]
		public IEnumerator Hiding_keeps_the_player_unseen_for_a_whole_patrol()
		{
			yield return Load();
			var g = EscapeGame.Instance;
			g.NewGame();
			yield return Frames(2);
			yield return Jump(g, SolveCell);
			g.Player.Teleport(PrisonLayout.HideExit, 0f);
			g.Guard.ResetPatrol(0f);
			g.Use(Ids.CorridorHidingSpot);
			Assert.IsTrue(g.Hidden);
			Time.timeScale = 6f;
			var cycle = g.Guard.Schedule.CycleSeconds;
			var start = Time.realtimeSinceStartup;
			var peak = 0f;
			while (g.Guard.PatrolTime < cycle * 1.2f)
			{
				peak = Mathf.Max(peak, g.GuardAwareness);
				Assert.AreEqual(Mode.Playing, g.Mode, "caught while hiding");
				if (Time.realtimeSinceStartup - start > 60f) Assert.Fail("time did not pass");
				yield return null;
			}
			Time.timeScale = 1f;
			Assert.AreEqual(0f, peak);
			Assert.AreEqual(0, g.State.Detections);
		}

		[UnityTest]
		public IEnumerator Walls_and_shut_doors_block_the_guards_sight()
		{
			yield return Load();
			var g = EscapeGame.Instance;
			g.Player.enabled = false;
			g.Guard.enabled = false;
			var origin = new Vector3(200f, 0f, 200f);
			var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
			wall.transform.position = origin + new Vector3(0f, 1.5f, 2.5f);
			wall.transform.localScale = new Vector3(6f, 3f, 0.3f);
			var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
			floor.transform.position = origin + new Vector3(0f, -0.25f, 3f);
			floor.transform.localScale = new Vector3(20f, 0.5f, 20f);
			yield return new WaitForFixedUpdate();
			Physics.SyncTransforms();
			g.Guard.UseSchedule(new PatrolSchedule(new[]
			{
				new Waypoint("A", new Vector2(200f, 200f), Vector2.up, 100f),
				new Waypoint("B", new Vector2(200f, 201f), Vector2.up, 100f),
			}, 1f));
			g.Player.GetComponent<CharacterController>().enabled = false;
			g.Player.transform.position = origin + new Vector3(0f, 0.05f, 4.5f);
			g.Guard.Target = g.Player;
			var detected = false;
			g.Guard.Detected += () => detected = true;
			g.Guard.ResetPatrol(0f);
			for (var i = 0; i < 150; i++) g.Guard.Step(0.02f);
			Assert.IsFalse(g.Guard.Sees, "a wall is in the way");
			Assert.IsFalse(detected);

			UnityEngine.Object.Destroy(wall);
			yield return null;
			for (var i = 0; i < 80 && !detected; i++) g.Guard.Step(0.02f);
			Assert.IsTrue(g.Guard.Sees, "nothing in the way now");
			Assert.IsTrue(detected, "one second in the open is enough");
			UnityEngine.Object.Destroy(floor);
		}

		[UnityTest]
		public IEnumerator Pause_reading_and_puzzle_screens_stop_the_guard_and_the_clock()
		{
			yield return Load();
			var g = EscapeGame.Instance;
			yield return Begin(g);
			yield return Frames(5);
			var patrol = g.Guard.PatrolTime;
			var elapsed = g.State.ElapsedSeconds;
			g.Pause();
			Assert.AreEqual(Mode.Pause, g.Mode);
			yield return new WaitForSeconds(0.5f);
			Assert.AreEqual(patrol, g.Guard.PatrolTime, 0.0001f, "the guard waits while paused");
			Assert.AreEqual(elapsed, g.State.ElapsedSeconds, 0.0001f);
			g.Resume();
			yield return Frames(5);
			Assert.Greater(g.Guard.PatrolTime, patrol);

			g.OpenPanel(Mode.Inventory);
			Assert.AreEqual(Mode.Inventory, g.Mode);
			patrol = g.Guard.PatrolTime;
			yield return new WaitForSeconds(0.3f);
			Assert.AreEqual(patrol, g.Guard.PatrolTime, 0.0001f);
			g.ClosePanel();
			Assert.AreEqual(Mode.Playing, g.Mode);
		}

		// ---- the camera and movement ----

		[UnityTest]
		public IEnumerator Held_movement_does_not_turn_around_when_the_camera_cuts()
		{
			yield return Load();
			var g = EscapeGame.Instance;
			g.NewGame();
			yield return Frames(2);
			yield return Jump(g, SolveCell);
			g.Player.Teleport(new Vector2(-10.5f, 0f), 90f);
			g.Rig.Snap();
			yield return Frames(3);
			var cuts = g.Rig.CutCount;
			var yawStart = g.Rig.transform.eulerAngles.y;
			// Hold the key that means "east" in the first camera's view, and keep holding it.
			var east = Quaternion.Euler(0f, -yawStart, 0f) * Vector3.right;
			g.Player.ScriptedInput = new Vector2(east.x, east.z);
			g.Player.ScriptedRun = false;
			var start = Time.realtimeSinceStartup;
			while (g.Player.Position2D.x < -2f)
			{
				if (Time.realtimeSinceStartup - start > 20f) Assert.Fail("held input did not carry the player through the door; at " + g.Player.Position2D);
				yield return null;
			}
			g.Player.ScriptedInput = null;
			Assert.Greater(g.Rig.CutCount, cuts, "a camera cut happened on the way");
			Assert.Less(Mathf.Abs(Mathf.DeltaAngle(yawStart, g.Rig.transform.eulerAngles.y)), 360f);
			Assert.Less(Mathf.Abs(g.Player.Position2D.y), 1.2f, "still heading east");
		}

		[UnityTest]
		public IEnumerator Walls_hide_prompts_so_only_visible_things_are_offered()
		{
			yield return Load();
			var g = EscapeGame.Instance;
			yield return Begin(g);
			// From the corridor side of the shut cell door the latch is behind iron.
			g.Player.Teleport(new Vector2(-7.2f, 0f), 270f);
			yield return Frames(3);
			Assert.IsTrue(g.Target == null || g.Target.Id != Ids.CellLatch, "the latch is not offered through the door");
			g.Player.Teleport(new Vector2(-9.2f, 0f), 90f);
			yield return Frames(3);
			Assert.IsNotNull(g.Target);
			Assert.AreEqual(Ids.CellLatch, g.Target.Id);
		}

		// ---- puzzle screens ----

		[UnityTest]
		public IEnumerator Puzzle_screens_can_be_left_and_opened_again_without_losing_anything()
		{
			yield return Load();
			var g = EscapeGame.Instance;
			g.NewGame();
			yield return Frames(2);
			yield return Jump(g, SolveCell);
			g.Player.Teleport(new Vector2(11.5f, -9.5f), 0f);
			// The cabinet is far from the cell; open the cabinet screen from where the player stands for the screen rules.
			g.Player.Teleport(new Vector2(12.1f, -10.6f), 90f);
			yield return Frames(3);
			Assert.AreEqual(Ids.RecordsCabinet, g.Target.Id);
			for (var round = 0; round < 3; round++)
			{
				g.Interact();
				Assert.AreEqual(Mode.Cabinet, g.Mode);
				Assert.IsTrue(g.Rig.Overriding);
				g.CabinetCycle(0);
				g.ClosePuzzle();
				Assert.AreEqual(Mode.Playing, g.Mode);
				Assert.IsFalse(g.Rig.Overriding);
				yield return Frames(2);
			}
			Assert.IsFalse(g.State.CabinetOpen);
			Assert.AreEqual(Symbol.Moon, g.State.Slot(0), "the slot kept its setting between visits: Tree, then one press per visit");
		}

		// ---- saving ----

		[UnityTest]
		public IEnumerator Save_quit_continue_keeps_the_key_half_and_goes_on()
		{
			yield return Load();
			var g = EscapeGame.Instance;
			g.NewGame();
			yield return Frames(2);
			Assert.IsTrue(File.Exists(g.Store.Path), "new game writes the first save");
			yield return Jump(g, SolveStore);
			Assert.AreEqual(Mode.Playing, g.Mode);
			Assert.IsTrue(g.State.Has(Ids.HalfA));
			Assert.IsTrue(g.State.CabinetOpen);
			Assert.IsTrue(g.CabinetLid.IsOpen, "the cabinet is open again after loading");
			Assert.IsFalse(g.PouchObject.activeSelf, "the pouch is not back in the cabinet");
			Assert.IsTrue(g.CellDoor.IsOpen);
			Assert.AreEqual(PrisonLayout.StoreCheckpoint.x, g.Player.Position2D.x, 0.2f);
			Assert.AreEqual(Milestone.CabinetOpen, g.State.Milestone);

			// And play on from there: half B, combine, key in the lock.
			yield return Walk(g, false, new Vector2(12f, -6f), new Vector2(16f, -6f), new Vector2(17.5f, -6.4f));
			yield return UseObject(g, Ids.WorkbenchKeyPiece);
			yield return Walk(g, false, new Vector2(25.2f, -5.0f));
			yield return UseObject(g, Ids.GateKeyhole);
			Assert.IsTrue(g.State.Has(Ids.KeyComplete));
		}

		[UnityTest]
		public IEnumerator A_damaged_save_is_reported_and_new_game_still_works()
		{
			yield return Load();
			var g = EscapeGame.Instance;
			Directory.CreateDirectory(Path.GetDirectoryName(g.Store.Path));
			File.WriteAllText(g.Store.Path, "{ not json at all");
			g.RefreshSave();
			Assert.IsFalse(g.HasSave);
			Assert.IsTrue(g.SaveProblem);
			Assert.IsFalse(g.ContinueGame());
			Assert.AreEqual(Mode.Title, g.Mode);
			g.NewGame();
			yield return Frames(2);
			Assert.AreEqual(Mode.Intro, g.Mode);
			g.BeginPlay();
			Assert.AreEqual(Mode.Playing, g.Mode);
			Assert.IsTrue(g.HasSave, "the new game saved over the damaged file");
			Assert.IsFalse(g.SaveProblem);
		}

		[UnityTest]
		public IEnumerator Restart_from_checkpoint_keeps_the_puzzles_and_settings_are_remembered()
		{
			yield return Load();
			var g = EscapeGame.Instance;
			g.NewGame();
			yield return Frames(2);
			yield return Jump(g, SolveCell);
			g.Player.Teleport(new Vector2(5f, 0f), 0f);
			g.Pause();
			g.RestartFromCheckpoint();
			Assert.AreEqual(Mode.Playing, g.Mode);
			Assert.AreEqual(PrisonLayout.CorridorCheckpoint.x, g.Player.Position2D.x, 0.2f);
			Assert.IsTrue(g.State.CellLatchUnlocked);

			g.SetVolumes(0.3f, 0.5f);
			g.SetFullscreen(false);
			var again = new EscapeSaveStore(_saveFolder).LoadSettings();
			Assert.AreEqual(0.3f, again.masterVolume, 0.001f);
			Assert.AreEqual(0.5f, again.sfxVolume, 0.001f);
			Assert.IsFalse(again.fullscreen);
		}

		[UnityTest]
		public IEnumerator Hints_come_after_waiting_and_can_be_asked_for_in_the_journal()
		{
			yield return Load();
			var g = EscapeGame.Instance;
			yield return Begin(g);
			Assert.AreEqual("", g.HintText);
			g.OpenPanel(Mode.Journal);
			g.RequestHint();
			Assert.AreEqual(1, g.HintLevel);
			Assert.AreEqual(Loc.T("hint.p1.1"), g.HintText);
			g.RequestHint();
			Assert.AreEqual(Loc.T("hint.p1.2"), g.HintText);
			g.ClosePanel();
			Assert.IsFalse(g.State.CellLatchUnlocked, "a hint does not unlock anything");
		}
	}
}
