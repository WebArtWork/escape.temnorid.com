# Temnorid: Escape ↔ Lords — future integration guide

## 1. World role

`Lords` is the central persistent strategy world. `Escape` is a **separate playable episode** that can be launched when an operative who attempted **scouting** or **resource theft** is **captured** by a village's guards. The prison exists in the village/region selected by the authoritative Lords simulation. Escaping restores the operative's freedom but does **not retroactively make a failed mission succeed**.

This is a future-facing **proposed contract**, not a claim that a Lords endpoint, player inventory contract, or prison system already exists.

## 2. Event flow

1. In Lords, a user initiates `Scout Village` or `Steal Resources` mission.
2. Lords performs authoritative success/failure/detection checks and determines `CAPTURED` where applicable.
3. Lords records a capture case against a specific operative, origin village, captor village, and mission.
4. UI offers `Attempt Escape` and opens/downloads `Temnorid: Escape` with a one-time case reference. MVP substitutes a local static fixture; no account flow required.
5. The game runs an escape instance. On success, game emits a **completion claim**; on quit/failure it may emit `ABANDONED` or remain pending.
6. **Lords backend**, not Unity client, verifies case eligibility, prevents duplicate claims, and applies game-world consequence once.
7. Lords updates operative status from `CAPTURED` to `ESCAPED` or a separately defined `RELEASED`, as its authoritative rules dictate. The original scouting/theft outcome remains stored independently.

**Important**: A downloadable offline game can be modified or its local save forged. Do not allow an unauthenticated client completion event to unilaterally grant resources, free an operative, or alter persistent world state. Trustworthy cross-game consequences require server-side eligibility/attestation or bounded, noncompetitive consequences. This is the key integration design decision for later.

## 3. Suggested future data concepts (proposal only)

| Concept | Fields (examples) | Authority |
|---|---|---|
| `CaptureCase` | `caseId`, `operativeId`, `missionId`, `captureVillageId`, `originVillageId`, `reason`, `state`, `createdAt`, `expiresAt` | Lords |
| `EscapeAttempt` | `attemptId`, `caseId`, `episodeId`, `createdAt`, `status`, `consumedAt` | Lords |
| `EscapeLaunchContext` | `attemptId`, `episodeId`, `villageTheme`, `reason`, `expiresAt`, opaque launch grant | Lords-issued (not MVP) |
| `EscapeClaim` | `attemptId`, `episodeId`, `outcome`, `completedAt`, evidence/validation fields (TBD) | Client claim, server validates |
| `LordsOutcome` | `attemptId`, `operativeState`, `missionState`, `rewardsApplied`, `claimState` | Lords |

`reason` values: `SCOUTING`, `THEFT`. `outcome` values: `ESCAPED`, `ABANDONED` (not all required for MVP). `state` needs explicit cases such as `CAPTURED`, `ESCAPE_AVAILABLE`, `ESCAPED`, `RELEASED`, `EXPIRED`; actual names to align with Lords domain later.

Do not lock this proposal into a database or network schema prematurely. Store only the necessary **local** launch fixture in MVP and expose an adapter seam for future integration.

## 4. Results and economic rules

- **Escape success:** operative is eligible to return to a safe location; optionally suffers travel delay or loses confiscated equipment, if Lords later models it.
- **Escape failure / quit:** operative remains captured; the user can retry according to Lords' eventual rules. No negative resource change from merely closing a game.
- **Scout captured:** no privileged enemy village information is delivered unless the scouting mission already successfully collected it before capture under Lords rules.
- **Thief captured:** no stolen resources are created by Escape. Confiscated goods/equipment remain governed by the source mission; the escape episode does not print resources.
- **Reward:** MVP only a victory screen. Future cosmetic achievement or bounded morale/operative recovery benefit might be added after anti-abuse design.
- **Anti-farming:** no repeated resource payouts from replaying the same prison. Claims linked to single attempt IDs and settled idempotently.

## 5. Security, identity and fairness (later)

- Lords is source of truth for identity, captured status, owner, village, time limits and mission results.
- Validate that a requesting account owns or controls the operative and the capture case is still eligible.
- Use single-use claim semantics, replay protection, token expiration, rate limiting and server-side audit.
- Avoid putting account secrets or long-lived tokens in launcher arguments, file URLs, local saves or error logs.
- Offline-only proof of human puzzle completion is inherently weak; do **not** market it as cheat-proof. For competitive rewards, either use a server-authoritative mini-game or keep the offline win consequence low-stakes and explicitly accept risk.
- Never trust local `completed=true` save field as enough proof to grant benefits.

## 6. Current MVP mock behavior

For now, a local `CaptureContext` fixture selects:

- `reason = SCOUTING` by default (alternative `THEFT` can be selected via a developer debug setting, not an elaborate UI).
- `village_name = Example Village` (fictional placeholder, not a real Temnorid lore canon name).
- `mission_status = CAPTURED`.
- Episode always uses the same authored prison geometry.

At victory, show a clearly marked **demo preview**, e.g.:

> `Demo: Prisoner escaped. In Lords, the captured operative could become available again once an authoritative escape result is accepted. No Lords data was changed.`

Use no API traffic, no cross-game credential storage, and no claims of real synchronization. The adapter should make it possible to implement the real contract later without rewriting puzzles, camera, input or save logic.

## 7. Future integration acceptance (not part of version 0.1.0)

- [ ] A capture case created by Lords can launch an authorized Escape attempt.
- [ ] Case cannot be claimed by another user's account.
- [ ] One attempt's successful claim is idempotent; duplicate submission changes nothing.
- [ ] Failure/quit does not falsely liberate an operative.
- [ ] Scout/theft mission resource and intel state remain unmodified by escape result.
- [ ] Offline save tampering cannot produce competitive/valuable rewards.
- [ ] Replayed and expired cases are rejected deterministically.
- [ ] Graceful UX for unavailable backend, expired case and offline player.

**Do not implement these online integration tests or backend methods during MVP.**
