# Temnorid: Escape (Темнорід: Втеча)

**Escape from the watchtower prison.** A short, offline escape-room adventure for **Windows**, in the world of [Temnorid](https://temnorid.com), built on the [IT Kamianets 3D Engine](https://3d.itkamianets.com) with Unity 6000.6. The game's text is in Ukrainian.

A scout from Lords is caught near a village storehouse and locked in the cell under the watchtower. There are no weapons and no combat: only observation, patience and the things around you. Read the clues, solve three puzzles, slip past one patrolling guard and leave by the service gate. First playthrough about 10–15 minutes, a replay 5–8.

**Status:** version 0.1.0, first playable MVP. Website: https://escape.temnorid.com · Download: not published yet (no repository or release exists, so the page shows "Coming soon" instead of a made-up link).

## How it plays

| Area | What there is |
|---|---|
| Camera | Fixed cinematic shots per room (classic survival-horror), cut or glide at doorways. The shots are chosen by volumes with a priority and a 0.3 m hysteresis, so standing on a border never flickers. |
| Movement | WASD or arrows, Shift to move faster. Movement is relative to the camera as it was when the key went down, so a camera cut never turns held input around. |
| Interaction | One key (E), one prompt at a time, only for things in plain view within 2 m. |
| Puzzles | **P1** the cell latch (use the wire: left, right, right, lift). **P2** the records cabinet (three symbols, four to choose from). **P3** the service gate (combine the key halves, put the key in, upper lever then lower lever). Mistakes only reset the input; nothing is ever lost. |
| Stealth | One guard on a fixed loop (desk, window, end door) with a 5 s pause at the window, an 80° and 6 m sight cone, wall and door blocking, one hiding spot. A small "?" and "!" over the guard and a bar at the bottom show suspicion. One continuous second in view and you are caught: you go back to the corridor checkpoint, nothing in the inventory or the puzzles is lost. |
| Help | Journal (J) with the objective and the clues found, inventory (Tab or I) with the key halves and a Combine button, hints after 90 s and a stronger one after 180 s on the same puzzle, or on request from the journal. A hint never unlocks anything. |
| Saving | One local slot, written atomically after every solved puzzle, checkpoint and important item, at pause and at victory (never in the middle of a puzzle). Continue puts you at the last checkpoint, never in the guard's view. A damaged save is reported and you can start a new game. |
| Online | None. The Lords capture is a local stand-in (`CaptureContext`: scouting or theft, "Example Village"), and the victory screen says plainly that no Lords data was changed. |

### Controls

| Action | Keys |
|---|---|
| Move | WASD or arrow keys |
| Move faster | Left Shift |
| Use / examine / take | E |
| Inventory | Tab or I |
| Journal | J |
| Back, close, pause | Esc |
| Menus and puzzles | Mouse, or arrows and Enter |

### The route (spoilers)

1. Read the scratches on the cell wall, push the cup aside, take the wire, open the latch: left, right, right, lift.
2. Out in the corridor, hide behind the barrels in the alcove. When the guard stops at the window and faces the wall, cross to the storeroom doorway.
3. In the storeroom read the ledger (sun, wheat, moon), set those three symbols on the cabinet, take the pouch (half A of the key, and a note).
4. Through the east door into the service passage: take half B from the workbench, combine the halves at the gate, put the key in the lock.
5. Read the plaque: the upper bolt before the lower. Pull the upper lever, then the lower. Walk out through the gate into the courtyard.

## Run it

1. Open the folder in Unity Hub with Unity **6000.6** (the project references the shared Temnorid packages by local path: `file:../../3d-unity-*`, so keep it next to `D:\work\temnorid\` and those folders).
2. Menu **Temnorid > Escape > Build Scene** rebuilds `Assets/Scenes/Prison.unity` from primitives (it is generated, do not edit it by hand), then press Play.

From the command line:

```bash
Unity.exe -batchmode -nographics -projectPath . -executeMethod Temnorid.Escape.EditorTools.EscapeSceneBuilder.Build -quit
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode
Unity.exe -batchmode -nographics -projectPath . -executeMethod Temnorid.Escape.EditorTools.EscapeReleaseBuilder.BuildWindows -quit
"ISCC.exe" installer\Escape.iss
```

(Do not pass `-quit` together with `-runTests`.) The build is `Builds/Windows/TemnoridEscape/TemnoridEscape.exe` plus a zip; the installer is `Builds/Installer/Temnorid-Escape-Setup.exe`.

## Tests

- **27 edit-mode tests** (pure rules): P1, P2 and P3 accept the right input and reject the wrong one, key halves combine once and atomically, milestones come in order and victory cannot be skipped, save round trip and rejection of damaged or contradicting saves, hints, the patrol loop, the cone, the detection time and grace, and a check that there is a safe window of at least 4 s every cycle to cross to the storeroom while a player who just stands in the corridor is caught.
- **14 play-mode tests** (the real scene): a bot plays the whole episode from New Game to Victory (it reads the guard's own schedule to choose its moment) and then Replay starts from nothing; capture restarts at the corridor with everything kept; hiding for a whole patrol; walls blocking sight; pause, reading and puzzle screens stopping the guard and the clock; held movement not turning when the camera cuts; every walkable spot covered by a shot and no flicker at borders; one prompt only for visible things; puzzle screens left and reopened; save, quit and Continue; a damaged save; restart from checkpoint and remembered settings; hints.

## Layout

```text
Assets/Scenes/Prison.unity      the one scene: title screen, prison and screens (see "Differences")
Assets/Scripts/                 EscapeRules (pure rules: state, puzzles, patrol, detection, hints, save data),
                                PrisonLayout (every position), Loc (all text), EscapeGame (the episode),
                                EscapeHud (screens), Player, GuardPatrol, Interactable, Mover, Figure, EscapeAudio
Assets/Scripts/Editor/          EscapeSceneBuilder, EscapeReleaseBuilder
Assets/Shaders/WorldText.shader depth-tested text for the labels in the world
Assets/Tests/                   EditMode and PlayMode
design/                         the design documents this game was built from (GAME, MVP, LORDS, AGENT, overview)
docs/index.html, docs/CNAME     the website (escape.temnorid.com)
installer/Escape.iss            Inno Setup script
```

Packages (all local, see `Packages/manifest.json`): `3d-unity-core`, `3d-unity-input` (the only input entry point; extra actions `Inventory` and `Journal` are added with `AddMap`), `3d-unity-save` (atomic save), `3d-unity-ui` (screens), `3d-unity-synth` (the generated sounds), `3d-unity-graybox` (asset ids and the scene builder helpers), `3d-unity-feedback`, `3d-unity-combat` (only because feedback depends on it) and the new **`3d-unity-fixed-camera`**.

## Gap report and differences from the guides

- The fixed-view constructor repository and the live package catalog could not be read (no network access in the build environment), so nothing was assumed from them. Instead the camera was written as a small game-neutral package, **`3d-unity-fixed-camera`** (`CameraShotVolume`, `FixedViewCamera`: volumes, priority, hysteresis, cut or blend, close-up override, held-input basis), tested through this game's two-room-and-more scene. `3d-unity-character`, `3d-unity-audio`, `3d-unity-assets` and `3d-unity-scene` are not used: the player is Unity's `CharacterController`, the sounds are generated in code, and the scene is built from primitives.
- The guides say `Main.unity` is the entry and `Prison.unity` the gameplay scene. Here there is **one scene, `Prison.unity`**, with the title screen as an overlay on it, as in the other Temnorid games.
- The state machine has an extra checkpoint, `store` (just inside the storeroom, where the guard never goes), so Continue after the storeroom does not put you back in the guard's corridor. Being caught still always returns to `corridor`.
- The storeroom doorway has no door: "opens when the guard is looking away" is the timing of the crossing, not a lock.
- Texts are Ukrainian only (the English dictionary is a P2 item); all of them are in `Loc.cs`.
- Everything is made of primitives with stable asset ids (`AssetIdTag`); the site's `/models` page lists them (family "Escape: the prison") so they can be replaced with generated models later.

## Not verified / limitations

- Played with a bot through the real scene and looked at in the editor (all shots, the latch and cabinet close-ups, the title and pause screens); not played by a human start to finish with the real keyboard, and the Windows installer was built but not installed.
- No gamepad, no English text, no music; the sounds are simple generated tones.
- Online: no Lords connection. A downloadable offline game can be modified, so a local "completed" flag must never grant anything in Lords; see `design/LORDS.md`.

## Release

Windows installer or zip attached to a GitHub Release, and the "Download" button on `docs/index.html` pointing at that asset. TODO: create the repository and the release; until then the button is disabled.
