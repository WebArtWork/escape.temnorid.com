# Temnorid: Escape — game design & complete demo episode

## 1. High concept

**Genre:** Fixed-camera adventure / escape-room / light stealth / puzzle.  
**Perspective:** Third-person character moving through spaces shown by **predefined cinematic camera shots** (classic Resident Evil-style shots, **not** over-the-shoulder combat).  
**Setting:** Grounded medieval Temnorid. Magic exists in this world, but there are **no fantasy creatures** and this episode requires no spells.  
**Audience/pace:** 10–15 minutes first completion; approachable, atmospheric, not brutally difficult.  
**Platform:** Windows desktop MVP. No mobile UX obligation.

A Lords scouting or theft mission goes wrong: a village patrol captures the player's operative and locks them in a stone-and-wood prison beneath the village watchtower. The prisoner has no weapons, only observation, patience and the environment. They must recover their seized belongings, slip past a guard and flee through a service exit.

**Fantasy of play:** "I noticed the clue, I figured out the mechanism, and I escaped by being clever." Avoid combat or forced chase sequences. Camera framing should encourage the player to notice key objects without constantly giving away solutions.

## 2. Story setup

**Working episode title:** `The Watchtower Prison` (UA UI: `В'язниця сторожової вежі`).  
**Player:** Captured human scout (gender-neutral presentation or simple placeholder model).  
**Other character:** Human village guard (one reusable character silhouette).  
**Opening text:** `Your mission failed. The village patrol caught you near its storehouse. Your gear was taken. At dawn, you will be brought before the village elder. Find a way out before the guard returns.`

Localization: present Ukrainian text in MVP (`Темнорід: Втеча`); optionally include English via a minimal dictionary without a complex localization package. Keep the UI text centralized rather than hardcoding strings across many components.

Narrative truth: the prisoner is guilty of **attempted scouting or theft**, depending on the mock entry context, not necessarily of successfully stealing anything. Guards are ordinary people doing their duty; no supernatural evil necessary. The same episode mechanics apply for both capture causes.

## 3. Level layout and puzzle chain

A single playable level is preferable to multiple level-loading scenes. Use zones/occlusion to make it feel like a small prison.

```text
[ CELL ] --locked door--> [ CORRIDOR / GUARD PATROL ]
                                 |
                           [ STORE ROOM ]
                                 |
                            [ SERVICE GATE ] ---> [ EXIT COURTYARD / VICTORY ]
```

Visual structure can vary, but maintain logical adjacency and an accessible route to all puzzle-critical objects. Keep all rooms and triggers in `Prison.unity` for MVP.

### Puzzle dependency graph (no ambiguity)

```text
Examine wall scratchings + cup
         ↓
Solve CELL BAR LATCH puzzle (P1)
         ↓
Cell unlocks → corridor access
         ↓
Observe guard route → hide → enter storeroom
         ↓
Examine inventory shelf + ledger
         ↓
Solve STOREROOM LOCK puzzle (P2)
         ↓
Recover confiscated pouch containing gate key half A
         ↓
Find gate key half B at service-workbench
         ↓
Combine halves into gate key
         ↓
Solve SERVICE GATE puzzle (P3) using completed key and lever order
         ↓
Exit trigger → victory screen
```

**Puzzle fairness:** Every clue must exist in the same or immediately accessible prior area, and prompts/inspect views should make them legible. P2/P3 should not rely on random item spawns. No timed lockouts or accidental item destruction.

## 4. Scene-by-scene authored walkthrough

### Scene A — The cell (2–3 min)

**Mood:** Dark, damp stone, a little daylight through a barred window; dripping water, distant boots.  
**Shots:** Wide high diagonal establishing shot; close fixed side shot across bed + barred wall.  
**Player goal:** Open the cell door.

Interactables:
- Wooden bed: inspect flavor text; no item.
- Clay cup on floor: movable/collectible. Under it is a small **bent wire** (`wire`).
- Wall scratchings: `One turn left. Two turns right. Then lift.` (UA clue localized). The camera frames them clearly when approached.
- Cell door: has a damaged latch with three operations.

**P1 — Cell latch:** While near the door, use the bent wire to access a simple 3-step lock UI. Apply `LEFT → RIGHT → RIGHT → LIFT` (four inputs; "two turns right" = two `RIGHT` inputs). Correct sequence unlocks. Mistakes reset the sequence but never destroy the wire. No arbitrary twitch timing.

Visual feedback: each correct step causes a quiet metal click, incorrect step causes an audible dull tap; door opens with short animation. A lock-up close-up can temporarily take focus while normal 3D movement is suspended.

Objective changes from `Find a way out of the cell` to `Avoid the guard and retrieve your belongings`.

### Scene B — Corridor / guard patrol (2–3 min)

**Mood:** Torches, heavy footsteps, glimpses of an occupied guard desk.  
**Shots:** Corridor long-shot camera showing guard turnarounds; doorway cut preserving movement input.  
**Player goal:** Move to the storeroom without being seen.

Guard loop: 3 authored points (`Desk → Window → EndDoor → Desk`), deterministic speed, at least **5 seconds** visible pause near Window, at least **4 seconds** safe crossing window per cycle. Player can hide behind a barrel cluster in a marked alcove. Show a small visual awareness indicator, not an x-ray view of the entire guard route.

If guard sees the player continuously for 1.0 s within a short frontal sight cone and unobstructed line of sight: `Detected` → short "Caught!" feedback → restart from **corridor checkpoint** (cell stays unlocked). The guard's patrol and player positions reset predictably. Nothing in inventory or puzzle state is lost. A hiding spot can break line of sight; the guard must not detect through walls/doors.

The storeroom opens from corridor when the guard is looking away. No pickpocket mechanic.

### Scene C — Storeroom (3–4 min)

**Mood:** Shelves with flour, rope, crates, a small records desk.  
**Shots:** Ceiling corner establishing shot, closer records-desk shot; avoid an instant camera cut while a puzzle UI is open.  
**Player goal:** Get seized belongings and a part of the service gate key.

Interactables:
- Records ledger, containing a three-symbol filing hint: `SUN — WHEAT — MOON` (use clear symbols on the objects; neutral medieval decoration).
- Chest/cabinet with three rotatable symbol slots.
- Open shelving with sacks, flavor objects, optional coin (no currency gameplay in MVP).
- Confiscated pouch inside chest, containing `gate_key_half_a` and a readable note.

**P2 — Records cabinet:** Rotate three independent symbol slots and confirm `SUN / WHEAT / MOON`. Use only three symbols as valid choices plus clearly different decoys, e.g. `SUN, WHEAT, MOON, TREE` selectable per slot. No random ordering. Wrong answer does not lock chest; simple audio/visual failure feedback. Open chest and acquire key half A automatically upon interacting with pouch.

Small story reward: note explains that the service gate has a two-part key as an anti-theft precaution.

### Scene D — Service workbench & gate (2–3 min)

**Mood:** Narrow utility passage with lever mechanisms, a strong exterior wind by the gate.  
**Shots:** Long medium shot showcasing lever panels and gate; detail shot on keyhole.  
**Player goal:** Unlock and open the gate.

Interactables:
- Workbench holds `gate_key_half_b` (unlocked access after reaching this zone).
- Two key parts can be **combined from inventory** into `gate_key_complete`. Alternatively, when the player inspects the gate while holding both, prompt `Combine key pieces`.
- Gate plaque: `Raise the upper bolt before lowering the lower bolt` (UA equivalent). This is the full, readable rule, no hidden ordering knowledge.
- Upper lever `lever_upper`, lower lever `lever_lower`.
- Gate keyhole: consumes no key; key stays flagged as present.

**P3 — Gate mechanism:** Completed key turns the lock (unlocks mechanism). Then activate `UPPER` lever, followed by `LOWER` lever. Wrong lever order returns levers to starting position; never traps the player. Gate opens and exit courtyard becomes reachable.

### Scene E — Exit courtyard & victory (1 min)

**Mood:** Night air, trees, distant village light, slight sense of relief.  
**Shots:** Static establishing wide view with prison behind character; authored end-frame when crossing final trigger.  
**Player goal:** Reach exit boundary.

Show `You Escaped` (`Втеча вдалася`), elapsed in-game time, optional number of detections (no ranking), buttons `Replay` and `Main Menu`. Display **mock Lords outcome**: `The captured operative escaped. Mission outcome is still determined by Lords.` Do **not** falsely claim stolen goods were earned or village resources changed.

## 5. Interaction and camera rules

### Fixed-view movement

- WASD / left stick moves an on-screen player relative to stable **world/navigation direction** or constructor-defined cut-safe directional mode. A camera cut must not unexpectedly reverse sustained player input.
- Camera controls are **not** freely orbiting. Per-zone designer sets camera transform, field of view, blend style, safe space and priorities.
- Camera cuts should not happen in less than ~1.0 m apart while a player walks near zone edges: use hysteresis / explicit camera volume priority to prevent rapid oscillation.
- UI close-ups lock regular movement and use a dedicated inspection shot/UI rather than unplanned camera manipulations.
- Objects cannot be hidden permanently behind geometry. A simple highlight/prompt reveals interactables when in range.

### Interactions

- Walk within 2 m of an object; when facing it (or nearest interactable if camera framing limits precise facing), show one prompt `E — Examine / Take / Use`.
- Press E once; do not require pixel-perfect mouse clicking or an unseen cursor.
- Buttons for inspect/collect/rotate/confirm/back must work with mouse/keyboard; gamepad can be additional but not mandatory for MVP.
- Never remove an object without applying its game-state change. Never allow two prompts to fight over the same input.

## 6. Difficulty and hints

- Low-stress adventure, not a twitch game.
- No health, food, fatigue, combat, weapon upgrades, cash economy or irreversible failure.
- Journal objective always points to the current **area**, not a complete spoiler.
- After 90 seconds without completing the current puzzle, offer an optional contextual hint; at 180 seconds offer a stronger hint. Hints can be requested from pause/journal at any time.
- Hints are authored per puzzle: P1 `Read the wall by the cup`; P2 `The ledger gives the cabinet's order`; P3 `The plaque tells you which lever goes first`.
- Second-level hints may explicitly disclose each solution; unlocking must never depend on a hint timer.

## 7. Art direction

- Visual: grounded low-poly medieval stone/wood/iron, modest torch lighting, shadows; no monsters, zombies, science-fiction technology, guns, or advanced fantasy props.
- Use primitive cubes/cylinders and affordable default materials; character initially a capsule or simple humanoid placeholder.
- Distinguish interactables (wire, ledger, key halves, levers) with silhouette and contrast, not tiny unreadable textures.
- Music optional; avoid jump-scare dependency. Guard footsteps and door feedback matter more.

## 8. Expansion candidates (not MVP)

New authored prisons by kingdom/region, tougher guards, prison breaks with multiple exits, optional clue routes, breaking friends out, diplomacy/bribery, limited spells for deception, prison facilities in Lords, multiplayer co-op. **Do not implement these now.**
