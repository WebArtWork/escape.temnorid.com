# Temnorid: Escape — MVP engineering specification and acceptance tests

## 1. Goal and exclusions

Ship a finished, **offline** 10–15 minute escape-room demo with one fixed-camera prison and one unambiguous puzzle route. Use Unity 6000.6 + URP as the current constructor indicates; verify exact installed editor version in repository. The game must compile and run as a standalone **Windows** build, not only in Editor.

No multiplayer, backend, procedural puzzles, combat, online saves, economy, monetization, real-world purchases, AI-generated art services, or actual Lords API writes.

## 2. MVP screens

| Screen | Must display / do |
|---|---|
| Title | `New Game`, `Continue` (only enabled with valid save), `Settings`, `Quit` |
| Settings | Master volume / SFX volume (minimum), controls reference, fullscreen/window toggle if easy |
| Gameplay | World, interaction prompt, compact current objective, optional `Inventory` & `Journal` hints |
| Inventory | List of collected item names/icons/placeholders; inspect description; combine key parts |
| Puzzle UI | Clear centered lock/symbol/lever feedback with `Confirm` and `Back`; cancel safely |
| Pause | Resume, Journal, Restart from Checkpoint, Main Menu |
| Guard caught | Simple message and checkpoint restart (not Game Over screen) |
| Victory | `You Escaped`, elapsed time, detections, `Replay`, `Main Menu`, local/mock Lords outcome |

## 3. Input mapping

Use **only** the installed `3d-unity-input` package and actual available actions. If it lacks the action, extend/fix the shared input contract according to parent AGENTS guidance, **do not read `Input.GetKey*` or device APIs directly in gameplay code**.

| Action | Default desktop control | Notes |
|---|---|---|
| Move | WASD / arrow keys | Do not invert on camera cut |
| Walk faster | Left Shift | Optional safe speed increase; no stamina |
| Interact | E | Uses nearest valid focused interactable |
| Inventory | Tab / I | Opens non-pausing or pausing panel; choose and document consistent behavior |
| Journal | J | Objectives and hints |
| Pause / Back | Escape | Closes popup before pause; Escape again resumes |
| Puzzle selection | Mouse / keyboard | All puzzle UI buttons clickable and focused |

Tuning defaults (agent may adjust based on testing, document adjustments): move speed 2.5 m/s; fast 4.0 m/s; interaction range 2.0 m; camera zone boundary hysteresis 0.3 m; camera blends 0.2–0.4 s (or hard cuts where visual intent requires); minimum nonzero UI text contrast.

## 4. Runtime game state (source of truth)

Track logical data with stable IDs, not scene object pointers:

- Session: `episode_id = watchtower_prison_01`, `version = 1`, `elapsed_seconds`, `detection_count`, `current_objective`.
- Puzzle flags: `cell_latch_unlocked`, `cabinet_open`, `gate_key_assembled`, `gate_key_inserted`, `upper_lever_done`, `lower_lever_done`, `service_gate_open`, `escaped`.
- Inventory: unique item IDs `wire`, `gate_key_half_a`, `gate_key_half_b`, `gate_key_complete` with visible/collected/combined status where applicable.
- World: cell door open, cabinet open, gate open, guard location waypoint index/phase, checkpoint ID (`cell` or `corridor`), player position and heading in safe regions.
- Hints: current puzzle ID and requested hint level (optional to persist).
- Launch/mock capture: `source_game = lords`, `capture_reason = scouting | theft`, `scenario_id = demo_captured_scout` (or `demo_caught_thief`); **never a live remote token**.

State machine milestones (in order):

`CELL_LOCKED → CELL_OPEN → CORRIDOR_ACCESS → CABINET_OPEN → KEY_ASSEMBLED → GATE_UNLOCKED → GATE_OPEN → ESCAPED`.

The guard can catch the player during corridor traversal; detection **does not** regress solved puzzle milestones. Sequence flags must be validated centrally so a gate cannot open before both key parts are obtained and the correct levers activated.

## 5. Object interaction contract

Every interactable has a stable `object_id`, human-readable label, verb, range, availability predicate, and deterministic state-change action. The UI displays exactly one active prompt. Define priority: focused/visible candidate > nearest candidate; avoid selecting objects behind walls. Interactable glow/outline is optional; readable UI prompt is mandatory.

Author object IDs:

| ID | Initial state | Interaction result |
|---|---|---|
| `cell_wall_clue` | Visible | Read `LEFT RIGHT RIGHT LIFT` clue |
| `cell_cup` | Untouched | Reveal bent wire |
| `cell_wire` | Hidden under cup | Add `wire` to inventory once |
| `cell_latch` | Locked | P1 puzzle; unlock cell if correct |
| `storeroom_ledger` | Visible | Read `SUN WHEAT MOON` clue |
| `records_cabinet` | Locked | P2 puzzle; unlock if correct |
| `confiscated_pouch` | Inside cabinet | Add `gate_key_half_a` once |
| `workbench_key_piece` | Visible at service workbench | Add `gate_key_half_b` once |
| `service_gate_keyhole` | Locked | Require assembled key; mark key inserted |
| `service_lever_upper` | Idle | Valid first action after key insertion |
| `service_lever_lower` | Idle | Valid second action after upper; opens gate |
| `courtyard_exit` | Locked behind gate | Triggers victory when player crosses and gate open |
| `corridor_hiding_spot` | Visible | Player can enter/exit hiding state |

## 6. Puzzle mechanics / persistence

**P1:** Accept exact sequence `LEFT, RIGHT, RIGHT, LIFT`. Mistake resets input progress; acquired `wire` remains. Door opens; `cell_latch_unlocked` persists. Moving between camera zones does not reset puzzle state.

**P2:** Three slots; four symbols available per slot: SUN, WHEAT, MOON, TREE. Correct left-to-right combination `SUN WHEAT MOON`. Wrong combo gives non-blocking error. Correct opening is permanent. Pouch can be collected once and only once.

**Key combining:** When both halves exist, show `Combine key halves`; consume halves and create `gate_key_complete` atomically (no intermediate broken state). Combination may be automatically prompted on inspecting the gate, but inventory combine remains discoverable. After reload, do not re-add halves if the key is complete.

**P3:** Require `gate_key_complete` and `gate_key_inserted`. Upper lever then lower lever; attempting lower first resets order with clear feedback. Opening gate persists. Levers cannot be permanently stuck, and gate opening cannot leave the player clipped into collision.

**Guard:** Three-point route, constant walk, predictable pauses; sight cone ~80° full angle, 6 m max (adjust to layout), 1.0 s continuous visible detection; raycast for walls; no detection while validly hiding; clear guard alert feedback. On capture restore position to corridor checkpoint, normalize guard patrol, increment `detection_count`. Persist solved puzzles and collected items. A shorter optional grace moment prevents immediate repeat detection at respawn.

**Autosave:** after every puzzle solved, after entering corridor checkpoint, after retrieving important inventory, and at victory. Can also save on pause, but never mid-puzzle with a partial input sequence. Save data atomically to a local persisted representation (e.g., JSON under persistentDataPath; actual approach chosen after package inspection) with version and corruption fallback. No cloud.

**Continue:** reconstruct room flags and inventory before enabling interactions; move player to a safe checkpoint, not into a wall/guard line of sight. If save invalid/corrupt, offer New Game and recover without crashing. New Game resets all fields; Replay resets all fields, including timer and detections.

## 7. Shot list (camera zone IDs)

| Zone/shot ID | Purpose | Cut rule |
|---|---|---|
| `cam_cell_wide` | Entry view of prisoner, door and clue | Default in cell |
| `cam_cell_detail` | Near bed/cup + scratches | Prioritized near wall |
| `cam_corridor_long` | Patrol information visible | On cell doorway crossing |
| `cam_corridor_alcove` | Cover/hiding readability | On alcove entry |
| `cam_store_wide` | Ledger + cabinet in one composition | On storeroom entry |
| `cam_store_desk` | Puzzle-relevant details | On desk area entry |
| `cam_service_gate` | Levers + keyhole | On passage entry |
| `cam_exit_courtyard` | Clean ending picture | On gate crossing |

Implement camera state transitions with volumes, priority and hysteresis. Do not allow repeated rapid switching if the player stands at a zone boundary. On entering a puzzle, take a dedicated inspection UI/camera and restore the preceding shot on exit. Player motion must remain smooth and direction-consistent when shots change.

## 8. HUD and UX

- Current objective top-left or unobtrusive corner; must remain readable in low lighting.
- Interaction prompt near bottom when target is in range (`E — Inspect ledger`).
- Journal: objective, previously observed clues, optional hints.
- Inventory: collected items; when both key halves are owned, a clearly discoverable `Combine` affordance.
- Low-cost readable UI (Unity Canvas / UI Toolkit based on existing project patterns); no external UI suite needed.
- Separate audio controls for master and effects where easy; muted playback should still offer visible confirmation of puzzle actions.
- Camera/interaction UI never blocks Escape Back navigation. On pause and puzzle UI, suspend movement and guard detection consistently, preventing surprise capture while reading a clue.

## 9. Scene content and performance

One `Prison.unity` scene. Primitive geometry is acceptable: stone blocks, bars, desk, cabinet, torches, gate, barrel, wall scratches as text/decal, lantern. 1 simple player model and 1 guard model; capsule placeholders acceptable. Use built-in or properly licensed audio; if no audio exists, ensure game still functions and log TODO. No required texture generation/modeling pipeline. Keep visual collision simple, no complex procedural navigation; guard may follow authored waypoints. Explicit layers/masks for interactions and visibility.

Target a stable 60 FPS on a typical desktop; do not fail functional acceptance due to cosmetic frame fluctuations. Ensure no game-breaking exceptions in Editor or standalone player.

## 10. Tests and acceptance criteria

### Pure logic (EditMode)

- [ ] `P1` rejects wrong sequence and resets, accepts correct sequence exactly.
- [ ] `P2` rejects incorrect symbols, accepts SUN/WHEAT/MOON and opens cabinet once.
- [ ] Key halves combine atomically, exactly once; cannot duplicate.
- [ ] `P3` rejects lever use before key, rejects LOWER before UPPER, then opens gate after correct order.
- [ ] Objective/milestone transitions occur in order; cannot jump straight to victory.
- [ ] Save serialization round-trips flags/inventory, version is validated, corrupt data handled gracefully.
- [ ] Capture resets only guard and player to checkpoint, not solved puzzle state.

### Scene/interaction (PlayMode or scripted smoke test)

- [ ] Can interact with every required object, with one prompt at a time.
- [ ] Player walks from cell into corridor without reversing held movement because of camera change.
- [ ] Camera zones cover walkable space and do not flicker at borders.
- [ ] Guard respects walls/hiding spot and has a safe crossing opportunity.
- [ ] UI close-ups/pause block movement and guard timers appropriately.
- [ ] All puzzle UIs can be exited, reopened, and solved; no softlocks.
- [ ] Save after P2, quit, Continue, recover key half and proceed normally.
- [ ] Escape sequence reaches victory; Replay restarts from initial state.
- [ ] Windows executable runs without Unity Editor; controls and audio work.

### Manual QA: must run before a release

1. `New Game → P1 → guard crossing → P2 → collect half A → collect half B → combine → P3 → exit → victory`.
2. Intentionally enter wrong inputs on every puzzle, recover and finish.
3. Be detected twice, finish with no lost items; check `detection_count`.
4. Pause while guard near; no unexpected capture on resume.
5. Save/quit after each milestone and continue.
6. Trigger camera edges repeatedly; no reversal/flicker/hidden target.
7. Start a second new game and confirm state fully resets.
8. Run standalone Windows build and verify `/docs/index.html` release asset points to an existing GitHub file.

## 11. `/docs/index.html` and release

**Must create** a responsive static page in `docs/index.html` (no framework needed) explaining the game. Include hero title, short description, 3–4 gameplay features, controls, screenshot placeholders (replace with actual screenshots from the implemented game), minimum system requirements (avoid unsupported numeric claims), and prominent `Download for Windows` link to the asset attached to the latest **actual** GitHub release.

A GitHub release flow should create a Windows ZIP or installer containing all required executable/data files. Asset naming example `Temnorid-Escape-Windows-v0.1.0.zip`; choose consistent naming across README and page. If no release exists yet, display a disabled `Coming soon` state or clear TODO rather than a broken/fictional URL. HTML must remain accessible via relative assets when served as GitHub Pages or a static site.

Suggested menu labels in UA:

- `Нова гра`, `Продовжити`, `Налаштування`, `Вихід`
- `Знайти вихід із камери`, `Уникай охоронця`, `Поверни свої речі`, `Відкрий службові ворота`
- `Інвентар`, `Щоденник`, `Підказка`, `Пауза`, `Повторити`, `До меню`, `Втеча вдалася`

## 12. Priorities if time/resources are constrained

**P0 (must ship):** working fixed camera/movement, room layout, interaction, all 3 solvable puzzles, guard checkpoint, local save, victory, Windows build, static `docs/index.html`.

**P1 (worthwhile):** footstep sound, animated gates, atmospheric lighting, polished clues, settings, keyboard focus improvements.

**P2 (defer):** character animation polish, English locale toggle, richer music, additional dynamic cameras, gamepad, procedural prison, additional guards, richer cinematic intro.

Do not trade away P0 gameplay for P1 or P2. If something is blocked by absent engine/constructor implementations, finish the smallest accurate local vertical slice and surface the blocker with evidence, not a made-up completion claim.
