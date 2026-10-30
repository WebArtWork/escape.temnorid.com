# Temnorid: Escape — agent implementation guide

**Status:** MVP implementation specification (2026-10-30)  
**Target:** Desktop Unity game, Windows first  
**Constructor:** [`IT-Kamianets/3d-constructor-fixed-view`](https://github.com/IT-Kamianets/3d-constructor-fixed-view)  
**Game:** `Temnorid: Escape`  
**Suggested repository:** `IT-Kamianets/temnorid-escape` (create only if authorized)  
**Version:** `0.1.0-mvp`

## Mission

Build one complete, replayable medieval prison escape episode using **fixed, scene-controlled cinematic cameras**. The player was captured while scouting or attempting to steal resources from a village in the central Temnorid world (`Lords`). They must escape a guarded prison through observation, interacting with objects, combining clues, solving puzzles, and avoiding a guard. **No weapons or combat in the MVP.**

**Core promise:** A short escape-room adventure with real third-person movement, readable fixed-camera shots, logical clues, no softlocks, and a satisfying escape.

**Design constraints:** Real animals/humans only; magic may exist as a tool or environmental mechanism, but no fantasy species or monsters. Do not require magical mechanics for the first episode. Favor cheap geometric/blockout art and solid gameplay. No backend or Lords dependency in MVP.

## Read in order

1. [`AGENT.md`](./AGENT.md) — instructions, boundaries, source repo, implementation order.
2. [`GAME.md`](./GAME.md) — story, rooms, puzzle graph, scene beats, player experience, all puzzle solutions.
3. [`MVP.md`](./MVP.md) — detailed features, data, state transitions, tuning, save/load, test cases, release requirements.
4. [`LORDS.md`](./LORDS.md) — future capture-to-escape contract and security boundaries; **not MVP backend scope**.

## MVP in one screen

| Area | Decision |
|---|---|
| Playtime | 10–15 minutes on first playthrough; 5–8 on replay |
| Character | 1 playable prisoner (humanoid capsule placeholder is acceptable) |
| Environment | 1 contiguous prison level, 4 gameplay areas + exit |
| Cameras | Fixed position/rotation per zone; automatic cuts / short blends |
| Interaction | One interact action, contextual prompt; examine, collect, combine/use |
| Puzzles | 3 required puzzles forming one unbroken dependency chain |
| Stealth | One predictable patrol guard and one hiding spot |
| UI | Title, pause, HUD hints, inventory, journal/objective, victory |
| Saving | Local single-slot autosave + new game / continue |
| Sound | Minimal UI, doors, footsteps, guard alert, ambience |
| Deployment | Windows desktop build attached to GitHub Release; `/docs/index.html` landing page |
| Online | None; all Lords hooks simulated locally using launch-context fixture |

## Player success

Open the cell, recover the confiscated belongings, bypass the guard, unlock the service gate, and leave the prison. A completion screen states the escape result and shows a mock Lords event preview (informational only; no real cross-game mutation).

## Out of scope

Procedural level generation, combat, multiple prison maps, randomized puzzle layouts, real NPC conversation AI, inventory weight, crafting trees, online leaderboards, multiplayer, real-world purchases, backend APIs, persistent Lords consequences, elaborate character/asset generation, localization framework beyond a small string table, advanced cinematics, paid content.

## Definition of done

A fresh player can launch the Windows build, understand the controls, complete the entire episode without console/engine tooling, save/quit/continue, and reach the victory screen. An automated test suite verifies the puzzle chain, camera switching and save state. A human smoke test verifies the release build and the `/docs/index.html` download link.
