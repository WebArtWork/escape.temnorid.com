using System.Collections.Generic;
using ITKamianets.Engine.FixedCamera;
using ITKamianets.Engine.Graybox;
using ITKamianets.Engine.Graybox.EditorTools;
using ITKamianets.Engine.Shell.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Temnorid.Escape.EditorTools
{
	/// <summary>
	/// Builds the whole prison from Unity primitives: the cell, the corridor with its alcove, the storeroom, the service
	/// passage with the gate and the courtyard; plus the prisoner, the guard, the camera shots, the lights and the screens.
	/// Run it from the menu (Temnorid > Escape > Build Scene) or the command line
	/// (<c>-executeMethod Temnorid.Escape.EditorTools.EscapeSceneBuilder.Build</c>); it rebuilds
	/// <c>Assets/Scenes/Prison.unity</c> from scratch each time, so do not edit that scene by hand. Positions come from
	/// <see cref="PrisonLayout"/>.
	/// </summary>
	public static class EscapeSceneBuilder
	{
		public const string ScenePath = "Assets/Scenes/Prison.unity";
		const string MaterialDir = "Assets/Materials";
		const float H = PrisonLayout.WallHeight;
		const float T = 0.4f;

		static Material _stone, _stoneDark, _wood, _woodDark, _iron, _gold, _paper, _cloth, _skin, _red, _ember, _glow, _grass, _dirt, _sack, _sun, _wheat, _moon, _leaf, _dark, _steel, _moonlight;

		[MenuItem("Temnorid/Escape/Build Scene")]
		public static void Build()
		{
			ShellSceneKit.ApplyPlatformSettings("Temnorid", "Temnorid Escape", "com.itkamianets.escape", "0.2.0");
			PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.itkamianets.temnoridescape");
			PlayerSettings.runInBackground = true;
			PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;

			var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
			RenderSettings.ambientLight = new Color(0.4f, 0.41f, 0.5f);
			MakeMaterials();

			var world = new GameObject("World").transform;
			var props = new GameObject("Props").transform;
			var lights = new GameObject("Lights").transform;
			var shots = new GameObject("CameraShots").transform;
			var talk = new GameObject("Interactables").transform;

			BuildShell(world, props);
			BuildCell(world, props, talk, shots, out var cellDoor, out var cup, out var wire);
			BuildCorridor(world, props, talk, shots);
			BuildStoreroom(world, props, talk, shots, out var lid, out var pouch, out var labels, out var cabinetCam, out var cabinetLook);
			BuildPassage(world, props, talk, shots, out var gate, out var upper, out var lower, out var halfB, out var keyInLock);
			BuildCourtyard(world, props, shots);
			BuildLights(lights);

			var player = BuildPlayer();
			var guard = BuildGuard();
			var rig = BuildCamera(player);

			var latchCam = Empty(shots, "LatchCamera", new Vector3(-10.3f, 1.7f, -1.1f));
			var latchLook = Empty(shots, "LatchLook", new Vector3(-8.1f, 0.75f, 0.4f));

			var gameObject = new GameObject("Game");
			var audio = gameObject.AddComponent<EscapeAudio>();
			var game = gameObject.AddComponent<EscapeGame>();
			game.Player = player;
			game.Guard = guard;
			game.Rig = rig;
			game.Audio = audio;
			game.CellDoor = cellDoor;
			game.Cup = cup;
			game.WireObject = wire;
			game.CabinetLid = lid;
			game.PouchObject = pouch;
			game.CabinetLabels = labels;
			game.Gate = gate;
			game.LeverUpper = upper;
			game.LeverLower = lower;
			game.HalfBObject = halfB;
			game.KeyInLock = keyInLock;
			game.LatchCamera = latchCam;
			game.LatchLook = latchLook;
			game.CabinetCamera = cabinetCam;
			game.CabinetLook = cabinetLook;
			var hud = gameObject.AddComponent<EscapeHud>();
			hud.Game = game;

			EditorSceneManager.SaveScene(scene, ScenePath);
			// The shell scenes open new scenes: the game scene is saved first and nothing from it is needed after.
			ShellSceneKit.BuildBoot();
			ShellSceneKit.BuildMenu();
			ShellSceneKit.SetBuildOrder(ScenePath);
			AssetDatabase.SaveAssets();
			Debug.Log("[Escape] Scene built: " + ScenePath);
		}

		static void MakeMaterials()
		{
			_stone = Mat("Stone", new Color(0.46f, 0.45f, 0.47f));
			_stoneDark = Mat("StoneDark", new Color(0.3f, 0.29f, 0.32f));
			_wood = Mat("Wood", new Color(0.45f, 0.3f, 0.16f));
			_woodDark = Mat("WoodDark", new Color(0.26f, 0.17f, 0.1f));
			_iron = Mat("Iron", new Color(0.2f, 0.21f, 0.24f));
			_gold = Mat("Gold", new Color(0.95f, 0.75f, 0.25f));
			_paper = Mat("Paper", new Color(0.9f, 0.86f, 0.72f));
			_cloth = Mat("Cloth", new Color(0.4f, 0.4f, 0.46f));
			_skin = Mat("Skin", new Color(0.8f, 0.66f, 0.55f));
			_red = Mat("Red", new Color(0.65f, 0.12f, 0.12f));
			_ember = Mat("Ember", new Color(1f, 0.6f, 0.2f));
			_glow = Mat("Glow", new Color(0.85f, 0.92f, 1f));
			_grass = Mat("Grass", new Color(0.14f, 0.24f, 0.15f));
			_dirt = Mat("Dirt", new Color(0.28f, 0.23f, 0.17f));
			_sack = Mat("Sack", new Color(0.62f, 0.55f, 0.4f));
			_sun = Mat("SymbolSun", new Color(1f, 0.8f, 0.2f));
			_wheat = Mat("SymbolWheat", new Color(0.85f, 0.65f, 0.25f));
			_moon = Mat("SymbolMoon", new Color(0.82f, 0.86f, 0.95f));
			_leaf = Mat("Leaf", new Color(0.1f, 0.3f, 0.12f));
			_dark = Mat("Dark", new Color(0.06f, 0.06f, 0.08f));
			_steel = Mat("Steel", new Color(0.72f, 0.74f, 0.8f));
			_moonlight = Mat("Moonlight", new Color(0.7f, 0.78f, 1f));
		}

		// ---- the shell: floors and walls ----

		static void BuildShell(Transform world, Transform props)
		{
			Box(world, "Floor", new Vector3(12f, -0.25f, -4f), new Vector3(60f, 0.5f, 24f), _stoneDark);
			Prim(PrimitiveType.Cube, "CellRug", props, new Vector3(-12f, 0.01f, 0f), new Vector3(3.5f, 0.02f, 2.2f), _woodDark);
			Prim(PrimitiveType.Cube, "CourtyardGround", props, new Vector3(33f, 0.01f, -6f), new Vector3(14f, 0.02f, 16f), _grass);
			Prim(PrimitiveType.Cube, "CourtyardPath", props, new Vector3(33f, 0.03f, -6f), new Vector3(14f, 0.02f, 2.2f), _dirt);
			Prim(PrimitiveType.Cube, "SkyBackdrop", props, new Vector3(33f, 4f, -16f), new Vector3(16f, 10f, 0.2f), Mat("Sky", new Color(0.04f, 0.05f, 0.1f)));
		}

		/// <summary>An axis-aligned wall strip between two points on the floor.</summary>
		static GameObject Wall(Transform parent, string name, float x0, float z0, float x1, float z1, Material material = null)
		{
			var centre = new Vector3((x0 + x1) * 0.5f, H * 0.5f, (z0 + z1) * 0.5f);
			var size = new Vector3(Mathf.Abs(x1 - x0) + (Mathf.Abs(x1 - x0) < 0.01f ? T : 0f), H, Mathf.Abs(z1 - z0) + (Mathf.Abs(z1 - z0) < 0.01f ? T : 0f));
			return Box(parent, name, centre, size, material ?? _stone);
		}

		// ---- cell ----

		static void BuildCell(Transform world, Transform props, Transform talk, Transform shots, out Mover door, out Mover cup, out GameObject wire)
		{
			Wall(world, "CellWest", -16f, -4f, -16f, 4f);
			Wall(world, "CellNorth", -16f, 4f, -8f, 4f);
			Wall(world, "CellSouth", -16f, -4f, -8f, -4f);
			Wall(world, "CellEastS", -8f, -4f, -8f, -1f);
			Wall(world, "CellEastN", -8f, 1f, -8f, 4f);
			var top = Wall(world, "CellEastTop", -8f, -1f, -8f, 1f);
			top.transform.localScale = new Vector3(T, H - 2.4f, 2f);
			top.transform.localPosition = new Vector3(-8f, 2.4f + (H - 2.4f) * 0.5f, 0f);

			// The door: iron bars in a frame, sliding up into the wall when the latch gives way.
			var doorRoot = new GameObject("CellDoor").transform;
			doorRoot.SetParent(world, false);
			doorRoot.position = new Vector3(-8f, 0f, 0f);
			var frame = BoxAt(doorRoot, "Frame", new Vector3(-8f, 1.2f, 0f), new Vector3(0.2f, 2.4f, 2f), _iron);
			frame.GetComponent<MeshRenderer>().enabled = false; // only the collider: the bars below are what the player sees
			for (var i = -4; i <= 4; i++) PrimAt(PrimitiveType.Cube, "Bar" + i, doorRoot, new Vector3(-8f, 1.2f, i * 0.22f), new Vector3(0.12f, 2.3f, 0.06f), _steel);
			PrimAt(PrimitiveType.Cube, "CrossBar", doorRoot, new Vector3(-8f, 1.0f, 0f), new Vector3(0.14f, 0.08f, 1.9f), _steel);
			PrimAt(PrimitiveType.Cube, "LatchPlate", doorRoot, new Vector3(-8.12f, 1.1f, 0.5f), new Vector3(0.06f, 0.5f, 0.45f), _iron);
			PrimAt(PrimitiveType.Cube, "Latch", doorRoot, new Vector3(-8.2f, 1.1f, 0.5f), new Vector3(0.1f, 0.22f, 0.28f), _gold);
			door = doorRoot.gameObject.AddComponent<Mover>();
			door.ClosedPosition = doorRoot.localPosition;
			door.OpenPosition = doorRoot.localPosition + new Vector3(0f, 2.5f, 0f);
			door.Seconds = 0.9f;
			door.Blockers = new[] { frame.GetComponent<Collider>() };
			doorRoot.gameObject.AddComponent<AssetIdTag>().Id = "props/cell-door";
			Interact(talk, Ids.CellLatch, new Vector3(-8.7f, 0f, 0f));

			// A barred window with a little daylight.
			Prim(PrimitiveType.Cube, "WindowGlow", props, new Vector3(-12f, 2.1f, 3.78f), new Vector3(1.3f, 0.9f, 0.05f), _glow);
			for (var i = -2; i <= 2; i++) Prim(PrimitiveType.Cube, "WindowBar" + i, props, new Vector3(-12f + i * 0.28f, 2.1f, 3.7f), new Vector3(0.05f, 1f, 0.05f), _iron);

			// The bed.
			var bed = new GameObject("Bed").transform;
			bed.SetParent(props, false);
			Box(bed, "Frame", new Vector3(-14.4f, 0.3f, 2.7f), new Vector3(2.2f, 0.4f, 1f), _woodDark);
			Prim(PrimitiveType.Cube, "Straw", bed, new Vector3(-14.4f, 0.55f, 2.7f), new Vector3(2.0f, 0.15f, 0.85f), _sack);
			bed.gameObject.AddComponent<AssetIdTag>().Id = "props/bed";
			Interact(talk, Ids.CellBed, new Vector3(-14.4f, 0f, 1.6f));

			// The cup, with the wire under it.
			var cupRoot = new GameObject("Cup").transform;
			cupRoot.SetParent(props, false);
			cupRoot.position = new Vector3(-12.4f, 0f, -3.1f);
			PrimAt(PrimitiveType.Cylinder, "Body", cupRoot, new Vector3(-12.4f, 0.14f, -3.1f), new Vector3(0.28f, 0.14f, 0.28f), _wood);
			cupRoot.gameObject.AddComponent<AssetIdTag>().Id = "props/clay-cup";
			cup = cupRoot.gameObject.AddComponent<Mover>();
			cup.ClosedPosition = cupRoot.localPosition;
			cup.OpenPosition = cupRoot.localPosition + new Vector3(0.55f, 0f, 0.35f);
			cup.OpenEuler = new Vector3(0f, 0f, 70f);
			cup.Seconds = 0.4f;
			Interact(talk, Ids.CellCup, new Vector3(-12.4f, 0f, -2.2f));

			wire = new GameObject("Wire");
			wire.transform.SetParent(props, false);
			Prim(PrimitiveType.Cylinder, "Bent", wire.transform, new Vector3(-12.4f, 0.03f, -3.1f), new Vector3(0.025f, 0.2f, 0.025f), _gold).transform.rotation = Quaternion.Euler(0f, 0f, 90f);
			Prim(PrimitiveType.Cylinder, "Hook", wire.transform, new Vector3(-12.25f, 0.03f, -3.0f), new Vector3(0.025f, 0.08f, 0.025f), _gold).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			wire.AddComponent<AssetIdTag>().Id = "items/bent-wire";
			Interact(talk, Ids.CellWire, new Vector3(-12.0f, 0f, -2.6f));
			wire.SetActive(false);

			// Scratches on the south wall.
			var scratches = new GameObject("Scratches").transform;
			scratches.SetParent(props, false);
			var s = 0;
			foreach (var x in new[] { -10.9f, -10.7f, -10.5f, -10.3f })
			{
				Prim(PrimitiveType.Cube, "Scratch" + s++, scratches, new Vector3(x, 1.3f + (s % 2) * 0.1f, -3.78f), new Vector3(0.04f, 0.5f, 0.02f), _paper);
			}
			Prim(PrimitiveType.Cube, "ScratchSlash", scratches, new Vector3(-10.6f, 1.3f, -3.78f), new Vector3(0.9f, 0.04f, 0.02f), _paper);
			scratches.gameObject.AddComponent<AssetIdTag>().Id = "props/wall-scratches";
			Interact(talk, Ids.CellWallClue, new Vector3(-10.6f, 0f, -3.0f));

			// Shots.
			var wide = Shot(shots, "cam_cell_wide", new Vector3(-12f, 1.5f, 0f), new Vector3(8.4f, 6f, 8.4f), new Vector3(-15.4f, 3.0f, 3.4f), 58f, 0, true);
			var detail = Shot(shots, "cam_cell_detail", new Vector3(-11.4f, 1.5f, -2.6f), new Vector3(6.4f, 6f, 2.6f), new Vector3(-8.7f, 3.0f, 3.3f), 50f, 5, false);
			wide.Cut = true;
			detail.Cut = false;
		}

		// ---- corridor ----

		static void BuildCorridor(Transform world, Transform props, Transform talk, Transform shots)
		{
			Wall(world, "CorridorNorth", -8f, 2f, 12f, 2f);
			Wall(world, "CorridorEnd", 12f, -2f, 12f, 2f);
			Wall(world, "CorridorSouthA", -8f, -2f, -5f, -2f);
			Wall(world, "CorridorSouthB", -2f, -2f, 7f, -2f);
			Wall(world, "CorridorSouthC", 9f, -2f, 14f, -2f);

			// The alcove with the barrels.
			Wall(world, "AlcoveWest", -5f, -4.2f, -5f, -2f);
			Wall(world, "AlcoveEast", -2f, -4.2f, -2f, -2f);
			Wall(world, "AlcoveSouth", -5f, -4.2f, -2f, -4.2f);
			var barrels = new GameObject("Barrels").transform;
			barrels.SetParent(props, false);
			foreach (var x in new[] { -4.35f, -3.5f, -2.65f })
			{
				var barrel = Prim(PrimitiveType.Cylinder, "Barrel", barrels, new Vector3(x, 0.5f, -2.6f), new Vector3(0.7f, 0.5f, 0.7f), _wood, true);
				Prim(PrimitiveType.Cylinder, "Hoop", barrels, new Vector3(x, 0.7f, -2.6f), new Vector3(0.74f, 0.03f, 0.74f), _iron);
				Prim(PrimitiveType.Cylinder, "Hoop2", barrels, new Vector3(x, 0.3f, -2.6f), new Vector3(0.74f, 0.03f, 0.74f), _iron);
			}
			barrels.gameObject.AddComponent<AssetIdTag>().Id = "props/barrels";
			var hide = Interact(talk, Ids.CorridorHidingSpot, new Vector3(-3.5f, 0f, -1.5f));
			hide.IgnoreSight = true;
			Prim(PrimitiveType.Cube, "HideMark", props, new Vector3(-3.5f, 0.02f, -1.4f), new Vector3(1.8f, 0.02f, 0.5f), Mat("HideMark", new Color(0.3f, 0.45f, 0.3f)));

			// The guard's desk, the window, the locked end door.
			var desk = new GameObject("GuardDesk").transform;
			desk.SetParent(props, false);
			Box(desk, "Top", new Vector3(-5f, 0.85f, 1.45f), new Vector3(1.8f, 0.1f, 0.8f), _wood);
			Box(desk, "LegL", new Vector3(-5.8f, 0.4f, 1.45f), new Vector3(0.1f, 0.8f, 0.7f), _woodDark);
			Box(desk, "LegR", new Vector3(-4.2f, 0.4f, 1.45f), new Vector3(0.1f, 0.8f, 0.7f), _woodDark);
			Prim(PrimitiveType.Cube, "Lamp", desk, new Vector3(-5.4f, 1.0f, 1.45f), new Vector3(0.16f, 0.2f, 0.16f), _ember);
			desk.gameObject.AddComponent<AssetIdTag>().Id = "props/guard-desk";

			Prim(PrimitiveType.Cube, "WindowGlow", props, new Vector3(3f, 1.9f, 1.78f), new Vector3(1.4f, 1f, 0.05f), _moonlight);
			for (var i = -2; i <= 2; i++) Prim(PrimitiveType.Cube, "WindowBar" + i, props, new Vector3(3f + i * 0.3f, 1.9f, 1.7f), new Vector3(0.05f, 1.1f, 0.05f), _iron);
			Box(props, "EndDoor", new Vector3(11.8f, 1.1f, 0f), new Vector3(0.2f, 2.2f, 1.6f), _woodDark);

			var shotLong = Shot(shots, "cam_corridor_long", new Vector3(2f, 1.5f, 0f), new Vector3(20.4f, 6f, 4.4f), new Vector3(4f, 3.0f, -1.7f), 56f, 0, true);
			shotLong.Cut = true;
			Shot(shots, "cam_corridor_alcove", new Vector3(-3.5f, 1.5f, -2.7f), new Vector3(3.4f, 6f, 3.4f), new Vector3(-7.2f, 2.6f, 1.4f), 55f, 6, false);
		}

		// ---- storeroom ----

		static void BuildStoreroom(Transform world, Transform props, Transform talk, Transform shots, out Mover lid, out GameObject pouch, out TextMesh[] labels, out Transform cabinetCam, out Transform cabinetLook)
		{
			Wall(world, "StoreWest", 2f, -12f, 2f, -2f);
			Wall(world, "StoreSouth", 2f, -12f, 14f, -12f);
			Wall(world, "StoreEastS", 14f, -12f, 14f, -7f);
			Wall(world, "StoreEastN", 14f, -5f, 14f, -2f);
			var lintel = Wall(world, "StoreEastTop", 14f, -7f, 14f, -5f);
			lintel.transform.localScale = new Vector3(T, H - 2.4f, 2f);
			lintel.transform.position = new Vector3(14f, 2.4f + (H - 2.4f) * 0.5f, -6f);

			// Shelves, sacks and crates along the west wall.
			var shelves = new GameObject("Shelves").transform;
			shelves.SetParent(props, false);
			Box(shelves, "Rack", new Vector3(2.55f, 1.1f, -7f), new Vector3(0.6f, 2.2f, 3.2f), _woodDark);
			for (var i = 0; i < 4; i++) Prim(PrimitiveType.Sphere, "Sack" + i, shelves, new Vector3(3.15f, 0.3f, -8.2f + i * 0.9f), new Vector3(0.6f, 0.6f, 0.6f), _sack);
			Prim(PrimitiveType.Cylinder, "Rope", shelves, new Vector3(3.1f, 1.2f, -5.9f), new Vector3(0.4f, 0.1f, 0.4f), _wood);
			shelves.gameObject.AddComponent<AssetIdTag>().Id = "props/shelves";
			Interact(talk, Ids.StoreShelf, new Vector3(4.0f, 0f, -7f));
			var crates = new GameObject("Crates").transform;
			crates.SetParent(props, false);
			Box(crates, "CrateA", new Vector3(8.5f, 0.5f, -10.8f), new Vector3(1.2f, 1f, 1.2f), _wood);
			Box(crates, "CrateB", new Vector3(9.8f, 0.4f, -10.9f), new Vector3(0.9f, 0.8f, 0.9f), _woodDark);
			Box(crates, "CrateC", new Vector3(9.1f, 1.3f, -10.8f), new Vector3(0.8f, 0.8f, 0.8f), _wood);
			crates.gameObject.AddComponent<AssetIdTag>().Id = "props/crates";

			// The records desk with the ledger and its three symbols on the wall.
			var desk = new GameObject("RecordsDesk").transform;
			desk.SetParent(props, false);
			Box(desk, "Top", new Vector3(3.2f, 0.85f, -10.9f), new Vector3(1.0f, 0.1f, 1.8f), _wood);
			Box(desk, "LegA", new Vector3(3.2f, 0.4f, -11.6f), new Vector3(0.9f, 0.8f, 0.1f), _woodDark);
			Box(desk, "LegB", new Vector3(3.2f, 0.4f, -10.2f), new Vector3(0.9f, 0.8f, 0.1f), _woodDark);
			var ledger = new GameObject("Ledger").transform;
			ledger.SetParent(desk, false);
			Prim(PrimitiveType.Cube, "Book", ledger, new Vector3(3.2f, 0.95f, -10.9f), new Vector3(0.55f, 0.08f, 0.4f), _red);
			Prim(PrimitiveType.Cube, "Pages", ledger, new Vector3(3.2f, 0.97f, -10.9f), new Vector3(0.5f, 0.06f, 0.36f), _paper);
			ledger.gameObject.AddComponent<AssetIdTag>().Id = "props/ledger";
			desk.gameObject.AddComponent<AssetIdTag>().Id = "props/records-desk";
			DrawSymbol(props, Symbol.Sun, new Vector3(2.25f, 1.8f, -11.5f), Quaternion.Euler(0f, -90f, 0f), 0.9f);
			DrawSymbol(props, Symbol.Wheat, new Vector3(2.25f, 1.8f, -10.9f), Quaternion.Euler(0f, -90f, 0f), 0.9f);
			DrawSymbol(props, Symbol.Moon, new Vector3(2.25f, 1.8f, -10.3f), Quaternion.Euler(0f, -90f, 0f), 0.9f);
			Interact(talk, Ids.StoreroomLedger, new Vector3(4.2f, 0f, -10.9f));

			// The records cabinet: a hollow cabinet with a door, three symbol slots and the pouch inside.
			var cabinet = new GameObject("RecordsCabinet").transform;
			cabinet.SetParent(props, false);
			Box(cabinet, "Back", new Vector3(13.66f, 1f, -10.6f), new Vector3(0.08f, 2f, 1.4f), _woodDark);
			Box(cabinet, "Top", new Vector3(13.3f, 2.0f, -10.6f), new Vector3(0.8f, 0.08f, 1.4f), _woodDark);
			Box(cabinet, "Bottom", new Vector3(13.3f, 0.04f, -10.6f), new Vector3(0.8f, 0.08f, 1.4f), _woodDark);
			Box(cabinet, "SideN", new Vector3(13.3f, 1f, -9.94f), new Vector3(0.8f, 2f, 0.08f), _woodDark);
			Box(cabinet, "SideS", new Vector3(13.3f, 1f, -11.26f), new Vector3(0.8f, 2f, 0.08f), _woodDark);
			Box(cabinet, "Shelf", new Vector3(13.3f, 0.9f, -10.6f), new Vector3(0.8f, 0.05f, 1.3f), _wood);
			cabinet.gameObject.AddComponent<AssetIdTag>().Id = "props/records-cabinet";

			var door = new GameObject("CabinetDoor").transform;
			door.SetParent(props, false);
			door.position = new Vector3(12.92f, 0f, -10.6f);
			var panel = BoxAt(door, "Panel", new Vector3(12.92f, 1f, -10.6f), new Vector3(0.06f, 1.9f, 1.3f), _wood);
			PrimAt(PrimitiveType.Sphere, "Knob", door, new Vector3(12.86f, 1f, -10.1f), new Vector3(0.1f, 0.1f, 0.1f), _gold);
			lid = door.gameObject.AddComponent<Mover>();
			lid.ClosedPosition = door.localPosition;
			lid.OpenPosition = door.localPosition + new Vector3(0f, 0f, 1.35f);
			lid.Seconds = 0.8f;
			lid.Blockers = new[] { panel.GetComponent<Collider>() };

			labels = new TextMesh[3];
			for (var i = 0; i < 3; i++)
			{
				var label = Text(door, "Slot" + i, "ДЕРЕВО", new Vector3(12.87f, 1.55f - i * 0.3f, -10.6f), Quaternion.Euler(0f, 90f, 0f), 0.018f, new Color(0.95f, 0.88f, 0.6f));
				labels[i] = label;
			}

			pouch = new GameObject("Pouch");
			pouch.transform.SetParent(props, false);
			Prim(PrimitiveType.Sphere, "Bag", pouch.transform, new Vector3(13.3f, 1.05f, -10.6f), new Vector3(0.35f, 0.3f, 0.3f), _sack);
			Prim(PrimitiveType.Cylinder, "Tie", pouch.transform, new Vector3(13.3f, 1.2f, -10.6f), new Vector3(0.12f, 0.03f, 0.12f), _woodDark);
			pouch.AddComponent<AssetIdTag>().Id = "items/confiscated-pouch";
			pouch.SetActive(false);

			Interact(talk, Ids.RecordsCabinet, new Vector3(12.1f, 0f, -10.6f));
			Interact(talk, Ids.ConfiscatedPouch, new Vector3(12.1f, 0f, -10.6f));

			cabinetCam = Empty(shots, "CabinetCamera", new Vector3(10.7f, 1.7f, -9.3f));
			cabinetLook = Empty(shots, "CabinetLook", new Vector3(13.0f, 0.55f, -10.6f));

			Shot(shots, "cam_store_wide", new Vector3(8f, 1.5f, -7.2f), new Vector3(12.4f, 6f, 10.4f), new Vector3(2.6f, 3.0f, -2.7f), 62f, 0, true).Cut = true;
			Shot(shots, "cam_store_desk", new Vector3(4.2f, 1.5f, -10f), new Vector3(4.4f, 6f, 4.4f), new Vector3(6.4f, 2.4f, -7.9f), 48f, 5, false);
		}

		// ---- service passage ----

		static void BuildPassage(Transform world, Transform props, Transform talk, Transform shots, out Mover gate, out Mover upper, out Mover lower, out GameObject halfB, out GameObject keyInLock)
		{
			Wall(world, "PassageNorth", 14f, -4f, 26f, -4f);
			Wall(world, "PassageSouth", 14f, -8f, 26f, -8f);

			// The workbench with the second half of the key.
			var bench = new GameObject("Workbench").transform;
			bench.SetParent(props, false);
			Box(bench, "Top", new Vector3(17.5f, 0.9f, -7.45f), new Vector3(2.4f, 0.1f, 0.8f), _wood);
			Box(bench, "LegL", new Vector3(16.4f, 0.45f, -7.45f), new Vector3(0.12f, 0.9f, 0.7f), _woodDark);
			Box(bench, "LegR", new Vector3(18.6f, 0.45f, -7.45f), new Vector3(0.12f, 0.9f, 0.7f), _woodDark);
			Prim(PrimitiveType.Cube, "Vise", bench, new Vector3(16.6f, 1.05f, -7.5f), new Vector3(0.3f, 0.2f, 0.25f), _iron);
			Prim(PrimitiveType.Cylinder, "Hammer", bench, new Vector3(18.3f, 0.98f, -7.4f), new Vector3(0.06f, 0.2f, 0.06f), _woodDark).transform.rotation = Quaternion.Euler(0f, 0f, 90f);
			bench.gameObject.AddComponent<AssetIdTag>().Id = "props/workbench";

			halfB = new GameObject("KeyHalfB");
			halfB.transform.SetParent(props, false);
			Prim(PrimitiveType.Cylinder, "Shaft", halfB.transform, new Vector3(17.5f, 0.99f, -7.4f), new Vector3(0.05f, 0.14f, 0.05f), _steel).transform.rotation = Quaternion.Euler(0f, 0f, 90f);
			Prim(PrimitiveType.Cube, "Teeth", halfB.transform, new Vector3(17.67f, 0.96f, -7.4f), new Vector3(0.1f, 0.08f, 0.04f), _steel);
			Prim(PrimitiveType.Cylinder, "GlowRing", halfB.transform, new Vector3(17.5f, 0.951f, -7.4f), new Vector3(0.5f, 0.005f, 0.5f), _gold);
			halfB.AddComponent<AssetIdTag>().Id = "items/gate-key-half-b";
			Interact(talk, Ids.WorkbenchKeyPiece, new Vector3(17.5f, 0f, -6.4f));

			// The plaque, the levers and the keyhole.
			var plaque = new GameObject("Plaque").transform;
			plaque.SetParent(props, false);
			Prim(PrimitiveType.Cube, "Plate", plaque, new Vector3(24f, 1.6f, -4.22f), new Vector3(1.1f, 0.7f, 0.06f), _iron);
			Text(plaque, "Text", "1  ВЕРХНІЙ\n2  НИЖНІЙ", new Vector3(24f, 1.6f, -4.27f), Quaternion.identity, 0.014f, new Color(0.95f, 0.78f, 0.35f));
			plaque.gameObject.AddComponent<AssetIdTag>().Id = "props/gate-plaque";
			Interact(talk, Ids.GatePlaque, new Vector3(24f, 0f, -5.0f));

			Prim(PrimitiveType.Cube, "LeverPlate", props, new Vector3(22f, 1.5f, -4.22f), new Vector3(2.0f, 1.0f, 0.06f), _iron);
			upper = Lever(props, "LeverUpper", new Vector3(21.4f, 1.5f, -4.3f), "ВЕРХНІЙ");
			lower = Lever(props, "LeverLower", new Vector3(22.6f, 1.5f, -4.3f), "НИЖНІЙ");
			Interact(talk, Ids.LeverUpper, new Vector3(21.4f, 0f, -5.0f));
			Interact(talk, Ids.LeverLower, new Vector3(22.6f, 0f, -5.0f));

			Prim(PrimitiveType.Cube, "KeyholePlate", props, new Vector3(25.2f, 1.3f, -4.22f), new Vector3(0.5f, 0.7f, 0.06f), _iron);
			Prim(PrimitiveType.Cube, "Keyhole", props, new Vector3(25.2f, 1.3f, -4.27f), new Vector3(0.08f, 0.2f, 0.04f), _dark);
			keyInLock = new GameObject("KeyInLock");
			keyInLock.transform.SetParent(props, false);
			Prim(PrimitiveType.Cylinder, "KeyShaft", keyInLock.transform, new Vector3(25.2f, 1.3f, -4.4f), new Vector3(0.05f, 0.1f, 0.05f), _steel).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			Prim(PrimitiveType.Cube, "KeyBow", keyInLock.transform, new Vector3(25.2f, 1.3f, -4.52f), new Vector3(0.22f, 0.22f, 0.04f), _gold);
			keyInLock.AddComponent<AssetIdTag>().Id = "items/gate-key-complete";
			keyInLock.SetActive(false);
			Interact(talk, Ids.GateKeyhole, new Vector3(25.2f, 0f, -5.0f));

			// The gate itself slides up into the roof when it opens.
			var gateRoot = new GameObject("ServiceGate").transform;
			gateRoot.SetParent(world, false);
			gateRoot.position = new Vector3(26f, 0f, -6f);
			var leaf = BoxAt(gateRoot, "Leaf", new Vector3(26f, 1.55f, -6f), new Vector3(0.3f, 3.1f, 4f), _iron);
			BoxAt(gateRoot, "BoltUpper", new Vector3(25.8f, 2.4f, -6f), new Vector3(0.12f, 0.12f, 3.6f), _steel).GetComponent<Collider>().enabled = false;
			BoxAt(gateRoot, "BoltLower", new Vector3(25.8f, 0.6f, -6f), new Vector3(0.12f, 0.12f, 3.6f), _steel).GetComponent<Collider>().enabled = false;
			for (var i = -3; i <= 3; i++) PrimAt(PrimitiveType.Cube, "Plank" + i, gateRoot, new Vector3(25.84f, 1.55f, -6f + i * 0.55f), new Vector3(0.04f, 3.0f, 0.06f), _stoneDark);
			gate = gateRoot.gameObject.AddComponent<Mover>();
			gate.ClosedPosition = gateRoot.localPosition;
			gate.OpenPosition = gateRoot.localPosition + new Vector3(0f, 3.4f, 0f);
			gate.Seconds = 1.6f;
			gate.Blockers = new[] { leaf.GetComponent<Collider>() };
			gateRoot.gameObject.AddComponent<AssetIdTag>().Id = "props/service-gate";

			Shot(shots, "cam_service_gate", new Vector3(20f, 1.5f, -6f), new Vector3(12.4f, 6f, 4.4f), new Vector3(14.6f, 3.0f, -4.5f), 58f, 0, true).Cut = true;
			Shot(shots, "cam_service_detail", new Vector3(22.8f, 1.5f, -6f), new Vector3(5.6f, 6f, 4.4f), new Vector3(19.6f, 2.4f, -7.3f), 52f, 5, false);
		}

		static Mover Lever(Transform props, string name, Vector3 pivot, string label)
		{
			var root = new GameObject(name).transform;
			root.SetParent(props, false);
			root.position = pivot;
			PrimAt(PrimitiveType.Cylinder, "Hub", root, pivot, new Vector3(0.14f, 0.04f, 0.14f), _steel).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			var arm = PrimAt(PrimitiveType.Cylinder, "Arm", root, pivot + new Vector3(0f, -0.25f, -0.06f), new Vector3(0.05f, 0.25f, 0.05f), _steel);
			PrimAt(PrimitiveType.Sphere, "Knob", root, pivot + new Vector3(0f, -0.5f, -0.06f), new Vector3(0.12f, 0.12f, 0.12f), _red);
			Text(root, "Label", label, pivot + new Vector3(0f, 0.38f, 0.0f), Quaternion.identity, 0.011f, new Color(0.95f, 0.78f, 0.35f));
			root.gameObject.AddComponent<AssetIdTag>().Id = "props/lever";
			var mover = root.gameObject.AddComponent<Mover>();
			mover.ClosedPosition = root.localPosition;
			mover.OpenPosition = root.localPosition;
			mover.ClosedEuler = Vector3.zero;
			mover.OpenEuler = new Vector3(160f, 0f, 0f);
			mover.Seconds = 0.4f;
			return mover;
		}

		// ---- courtyard ----

		static void BuildCourtyard(Transform world, Transform props, Transform shots)
		{
			Wall(world, "CourtyardWallS", 26f, -14f, 26f, -8f);
			Wall(world, "CourtyardWallN", 26f, -4f, 26f, 2f);
			Wall(world, "PrisonBackWall", 26f, 2f, 40f, 2f);
			var fence = new GameObject("Fence").transform;
			fence.SetParent(world, false);
			Box(fence, "East", new Vector3(40f, 0.6f, -6f), new Vector3(0.3f, 1.2f, 16f), _woodDark);
			Box(fence, "South", new Vector3(33f, 0.6f, -14f), new Vector3(14f, 1.2f, 0.3f), _woodDark);

			var random = new System.Random(4);
			for (var i = 0; i < 9; i++)
			{
				var x = 28f + (float)random.NextDouble() * 11f;
				var z = -13f + (float)random.NextDouble() * 5f;
				if (i % 3 == 0) z = 0.8f - (float)random.NextDouble() * 0.5f;
				Tree(props, new Vector3(x, 0f, z), 3.2f + (float)random.NextDouble() * 2f);
			}
			// The way out: two posts and a lantern at the exit line.
			var exit = new GameObject("ExitPosts").transform;
			exit.SetParent(props, false);
			foreach (var z in new[] { -7.4f, -4.6f })
			{
				Box(exit, "Post", new Vector3(PrisonLayout.ExitLineX, 1.2f, z), new Vector3(0.25f, 2.4f, 0.25f), _wood);
				Prim(PrimitiveType.Sphere, "Lantern", exit, new Vector3(PrisonLayout.ExitLineX, 2.5f, z), new Vector3(0.35f, 0.35f, 0.35f), _ember);
			}
			exit.gameObject.AddComponent<AssetIdTag>().Id = "props/exit-posts";
			Shot(shots, "cam_exit_courtyard", new Vector3(33.2f, 1.5f, -6f), new Vector3(14.8f, 6f, 16.4f), new Vector3(27.4f, 3.0f, -3.0f), 58f, 3, true).Cut = true;
		}

		static void Tree(Transform parent, Vector3 position, float height)
		{
			var tree = new GameObject("Tree").transform;
			tree.SetParent(parent, false);
			Prim(PrimitiveType.Cylinder, "Trunk", tree, position + Vector3.up * (height * 0.2f), new Vector3(0.35f, height * 0.2f, 0.35f), _woodDark);
			Prim(PrimitiveType.Sphere, "Crown", tree, position + Vector3.up * (height * 0.65f), new Vector3(height * 0.55f, height * 0.6f, height * 0.55f), _leaf);
			tree.gameObject.AddComponent<AssetIdTag>().Id = "props/tree";
		}

		// ---- lights ----

		static void BuildLights(Transform lights)
		{
			Torch(lights, new Vector3(-9.5f, 2.2f, 3.6f), "Cell");
			Torch(lights, new Vector3(-13.5f, 2.2f, -3.5f), "Cell2");
			Daylight(lights, new Vector3(-12f, 2.6f, 3.2f));
			foreach (var x in new[] { -6.5f, 0f, 6f, 10.5f }) Torch(lights, new Vector3(x, 2.4f, 1.6f), "Corridor");
			Torch(lights, new Vector3(-3.5f, 2.6f, -3.8f), "Alcove", 1.2f);
			Torch(lights, new Vector3(4f, 2.5f, -3f), "Store");
			Torch(lights, new Vector3(9f, 2.5f, -9f), "Store2");
			Torch(lights, new Vector3(13f, 2.4f, -6f), "StoreEast", 1.4f);
			Torch(lights, new Vector3(17f, 2.4f, -5f), "Passage");
			Torch(lights, new Vector3(23f, 2.4f, -7f), "Passage2");
			foreach (var z in new[] { -10f, -2f })
			{
				var yard = new GameObject("YardLight").transform;
				yard.SetParent(lights, false);
				yard.position = new Vector3(31f, 4.2f, z);
				var yardLight = yard.gameObject.AddComponent<Light>();
				yardLight.type = LightType.Point;
				yardLight.color = new Color(0.62f, 0.72f, 1f);
				yardLight.intensity = 6f;
				yardLight.range = 16f;
				yardLight.shadows = LightShadows.None;
			}
			var moon = new GameObject("Moon");
			moon.transform.SetParent(lights, false);
			moon.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
			var light = moon.AddComponent<Light>();
			light.type = LightType.Directional;
			light.intensity = 0.25f;
			light.color = new Color(0.6f, 0.7f, 1f);
			light.shadows = LightShadows.None;
			light.cullingMask = 1; // outside only matters little: indoors the point lights dominate
		}

		static void Torch(Transform lights, Vector3 position, string name, float strength = 1.8f)
		{
			var root = new GameObject("Torch" + name).transform;
			root.SetParent(lights, false);
			root.position = position;
			var light = root.gameObject.AddComponent<Light>();
			light.type = LightType.Point;
			light.color = new Color(1f, 0.72f, 0.42f);
			light.intensity = strength * 1.3f;
			light.range = 9f;
			light.shadows = LightShadows.None;
			root.gameObject.AddComponent<TorchFlicker>();
			Prim(PrimitiveType.Cube, "Handle", root, position + Vector3.down * 0.15f, new Vector3(0.08f, 0.35f, 0.08f), _woodDark);
			Prim(PrimitiveType.Sphere, "Flame", root, position, new Vector3(0.18f, 0.24f, 0.18f), _ember);
		}

		static void Daylight(Transform lights, Vector3 position)
		{
			var root = new GameObject("WindowLight").transform;
			root.SetParent(lights, false);
			root.position = position;
			root.rotation = Quaternion.Euler(70f, 180f, 0f);
			var light = root.gameObject.AddComponent<Light>();
			light.type = LightType.Spot;
			light.color = new Color(0.75f, 0.85f, 1f);
			light.intensity = 5f;
			light.range = 10f;
			light.spotAngle = 80f;
			light.shadows = LightShadows.None;
		}

		// ---- people ----

		static Player BuildPlayer()
		{
			var root = new GameObject("Player");
			root.transform.position = new Vector3(PrisonLayout.CellStart.x, 0.05f, PrisonLayout.CellStart.y);
			var body = root.AddComponent<CharacterController>();
			body.height = 1.8f;
			body.radius = 0.3f;
			body.center = new Vector3(0f, 0.9f, 0f);
			body.stepOffset = 0.25f;
			var player = root.AddComponent<Player>();
			var figure = BuildFigure(root.transform, "Prisoner", new Color(0.42f, 0.4f, 0.36f), new Color(0.25f, 0.22f, 0.2f), false);
			root.AddComponent<AssetIdTag>().Id = "characters/prisoner";
			return player;
		}

		static GuardPatrol BuildGuard()
		{
			var root = new GameObject("Guard");
			var guard = root.AddComponent<GuardPatrol>();
			guard.Figure = BuildFigure(root.transform, "Guard", new Color(0.65f, 0.12f, 0.12f), new Color(0.18f, 0.18f, 0.22f), true);
			root.AddComponent<AssetIdTag>().Id = "characters/guard";
			// The "?" and "!" over the head.
			var mark = Text(root.transform, "Mark", "?", new Vector3(0f, 2.3f, 0f), Quaternion.identity, 0.05f, new Color(1f, 0.85f, 0.2f));
			mark.fontStyle = FontStyle.Bold;
			mark.transform.localPosition = new Vector3(0f, 2.35f, 0f);
			mark.gameObject.SetActive(false);
			guard.Mark = mark;
			var solid = root.AddComponent<CapsuleCollider>();
			solid.height = 1.8f;
			solid.radius = 0.3f;
			solid.center = new Vector3(0f, 0.9f, 0f);
			solid.isTrigger = true;
			return guard;
		}

		static Figure BuildFigure(Transform parent, string name, Color tunic, Color trousers, bool guard)
		{
			var rig = new GameObject(name + "Visual").transform;
			rig.SetParent(parent, false);
			var figure = rig.gameObject.AddComponent<Figure>();
			var tunicMat = Mat(name + "Tunic", tunic);
			var trouserMat = Mat(name + "Trousers", trousers);
			var body = new GameObject("Body").transform;
			body.SetParent(rig, false);
			figure.Body = body;
			LocalPrim(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 1.15f, 0f), new Vector3(0.55f, 0.7f, 0.3f), tunicMat);
			LocalPrim(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 1.7f, 0f), new Vector3(0.3f, 0.32f, 0.3f), _skin);
			if (guard)
			{
				LocalPrim(PrimitiveType.Cylinder, "Helmet", body, new Vector3(0f, 1.82f, 0f), new Vector3(0.34f, 0.1f, 0.34f), _steel);
				LocalPrim(PrimitiveType.Cylinder, "Spear", body, new Vector3(0.45f, 1.2f, 0.1f), new Vector3(0.04f, 1.0f, 0.04f), _woodDark);
				LocalPrim(PrimitiveType.Cube, "SpearHead", body, new Vector3(0.45f, 2.25f, 0.1f), new Vector3(0.08f, 0.22f, 0.03f), _steel);
			}
			else
			{
				LocalPrim(PrimitiveType.Cube, "Hair", body, new Vector3(0f, 1.86f, -0.02f), new Vector3(0.3f, 0.1f, 0.3f), _woodDark);
			}
			figure.LegL = Limb(rig, "LegL", new Vector3(-0.14f, 0.8f, 0f), new Vector3(0.18f, 0.8f, 0.2f), trouserMat);
			figure.LegR = Limb(rig, "LegR", new Vector3(0.14f, 0.8f, 0f), new Vector3(0.18f, 0.8f, 0.2f), trouserMat);
			figure.ArmL = Limb(body, "ArmL", new Vector3(-0.38f, 1.45f, 0f), new Vector3(0.14f, 0.65f, 0.16f), tunicMat);
			figure.ArmR = Limb(body, "ArmR", new Vector3(0.38f, 1.45f, 0f), new Vector3(0.14f, 0.65f, 0.16f), tunicMat);
			return figure;
		}

		static Transform Limb(Transform parent, string name, Vector3 pivot, Vector3 size, Material material)
		{
			var joint = new GameObject(name).transform;
			joint.SetParent(parent, false);
			joint.localPosition = pivot;
			var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
			mesh.name = "Mesh";
			Object.DestroyImmediate(mesh.GetComponent<Collider>());
			mesh.transform.SetParent(joint, false);
			mesh.transform.localPosition = new Vector3(0f, -size.y * 0.5f, 0f);
			mesh.transform.localScale = size;
			mesh.GetComponent<Renderer>().sharedMaterial = material;
			return joint;
		}

		static void LocalPrim(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
		{
			var go = GameObject.CreatePrimitive(type);
			go.name = name;
			Object.DestroyImmediate(go.GetComponent<Collider>());
			go.transform.SetParent(parent, false);
			go.transform.localPosition = localPosition;
			go.transform.localScale = scale;
			go.GetComponent<Renderer>().sharedMaterial = material;
		}

		static FixedViewCamera BuildCamera(Player player)
		{
			var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
			cameraObject.transform.position = new Vector3(-15.4f, 3f, 3.4f);
			var camera = cameraObject.AddComponent<Camera>();
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = new Color(0.03f, 0.04f, 0.08f);
			camera.fieldOfView = 58f;
			camera.nearClipPlane = 0.1f;
			camera.farClipPlane = 120f;
			cameraObject.AddComponent<AudioListener>();
			var rig = cameraObject.AddComponent<FixedViewCamera>();
			rig.Subject = player.transform;
			rig.Hysteresis = EscapeConfig.CameraHysteresis;
			rig.BlendSeconds = EscapeConfig.CameraBlend;
			player.Rig = rig;
			return rig;
		}

		// ---- small helpers ----

		static CameraShotVolume Shot(Transform parent, string id, Vector3 centre, Vector3 size, Vector3 anchor, float fov, int priority, bool cut)
		{
			var volume = new GameObject(id).transform;
			volume.SetParent(parent, false);
			volume.position = centre;
			var shot = volume.gameObject.AddComponent<CameraShotVolume>();
			shot.ShotId = id;
			shot.Size = size;
			shot.Fov = fov;
			shot.Priority = priority;
			shot.Cut = cut;
			shot.Anchor = Empty(volume, id + "_anchor", anchor);
			return shot;
		}

		static Transform Empty(Transform parent, string name, Vector3 position)
		{
			var go = new GameObject(name).transform;
			go.SetParent(parent, false);
			go.position = position;
			return go;
		}

		static Interactable Interact(Transform parent, string id, Vector3 position)
		{
			var go = new GameObject(id);
			go.transform.SetParent(parent, false);
			go.transform.position = position;
			var item = go.AddComponent<Interactable>();
			item.Id = id;
			return item;
		}

		/// <summary>A line of text at a world position (see <see cref="GrayboxBuilder.Text"/>).</summary>
		static TextMesh Text(Transform parent, string name, string text, Vector3 position, Quaternion rotation, float size, Color colour)
		{
			var mesh = GrayboxBuilder.Text(parent, name, text, Vector3.zero, Quaternion.identity, size, colour, MaterialDir);
			mesh.transform.SetPositionAndRotation(position, rotation);
			return mesh;
		}

		/// <summary>A primitive at a WORLD position, for the parts of a root that already sits at its own pivot (the helpers of the graybox package take local ones).</summary>
		static GameObject PrimAt(PrimitiveType type, string name, Transform parent, Vector3 worldPosition, Vector3 scale, Material material, bool keepCollider = false)
		{
			var go = GrayboxBuilder.Prim(type, name, parent, Vector3.zero, scale, material, keepCollider);
			go.transform.position = worldPosition;
			return go;
		}

		static GameObject BoxAt(Transform parent, string name, Vector3 worldPosition, Vector3 scale, Material material) => PrimAt(PrimitiveType.Cube, name, parent, worldPosition, scale, material, true);

		/// <summary>A flat symbol of the cabinet puzzle (sun, wheat, moon, tree) made of a few primitives, drawn in the local XY plane of a pivot at a world position, so the plaque faces local +Z.</summary>
		static void DrawSymbol(Transform parent, Symbol symbol, Vector3 position, Quaternion rotation, float size)
		{
			var pivot = new GameObject("Symbol_" + symbol).transform;
			pivot.SetParent(parent, false);
			pivot.SetPositionAndRotation(position, rotation);
			pivot.gameObject.AddComponent<AssetIdTag>().Id = "props/symbol-" + symbol.ToString().ToLowerInvariant();
			var s = size * 0.5f;
			Prim(PrimitiveType.Cube, "Plaque", pivot, Vector3.zero, new Vector3(s * 1.2f, s * 1.2f, 0.04f), _woodDark);
			switch (symbol)
			{
				case Symbol.Sun:
					Prim(PrimitiveType.Cylinder, "Disc", pivot, new Vector3(0f, 0f, 0.03f), new Vector3(s * 0.55f, 0.01f, s * 0.55f), _sun).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
					for (var i = 0; i < 8; i++)
					{
						var ray = Prim(PrimitiveType.Cube, "Ray" + i, pivot, Vector3.zero, new Vector3(s * 0.08f, s * 0.3f, 0.02f), _sun);
						ray.transform.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
						ray.transform.localPosition = ray.transform.localRotation * new Vector3(0f, s * 0.5f, 0.03f);
					}
					break;
				case Symbol.Wheat:
					Prim(PrimitiveType.Cube, "Stem", pivot, new Vector3(0f, -s * 0.1f, 0.03f), new Vector3(s * 0.06f, s * 1.0f, 0.02f), _wheat);
					for (var i = 0; i < 4; i++)
					{
						Prim(PrimitiveType.Cube, "GrainL" + i, pivot, new Vector3(-s * 0.14f, s * (0.1f + i * 0.17f), 0.03f), new Vector3(s * 0.14f, s * 0.08f, 0.02f), _wheat).transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
						Prim(PrimitiveType.Cube, "GrainR" + i, pivot, new Vector3(s * 0.14f, s * (0.1f + i * 0.17f), 0.03f), new Vector3(s * 0.14f, s * 0.08f, 0.02f), _wheat).transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
					}
					break;
				case Symbol.Moon:
					Prim(PrimitiveType.Cylinder, "Disc", pivot, new Vector3(0f, 0f, 0.03f), new Vector3(s * 0.8f, 0.01f, s * 0.8f), _moon).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
					Prim(PrimitiveType.Cylinder, "Bite", pivot, new Vector3(s * 0.28f, s * 0.1f, 0.045f), new Vector3(s * 0.7f, 0.01f, s * 0.7f), _woodDark).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
					break;
				default:
					Prim(PrimitiveType.Cube, "Trunk", pivot, new Vector3(0f, -s * 0.3f, 0.03f), new Vector3(s * 0.14f, s * 0.5f, 0.02f), _wood);
					Prim(PrimitiveType.Cylinder, "Crown", pivot, new Vector3(0f, s * 0.2f, 0.03f), new Vector3(s * 0.7f, 0.01f, s * 0.7f), _leaf).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
					break;
			}
		}

		static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material) => GrayboxBuilder.Box(parent, name, position, scale, material);

		static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool keepCollider = false) => GrayboxBuilder.Prim(type, name, parent, position, scale, material, keepCollider);

		static Material Mat(string name, Color colour) => GrayboxBuilder.Mat(name, colour, MaterialDir);
	}
}
