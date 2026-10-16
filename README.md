# re:SquashStrike

PC squash game, Unity 6 (6000.3 LTS, URP). Plans:
[Phase 1 — gameplay & engine](../SQUASH%20FRONTEND%20MODIFIED/RE_SQUASHSTRIKE_GAMEPLAY_PLAN.md) ·
[Phase 2 — modes, UI, progression, online](../SQUASH%20FRONTEND%20MODIFIED/RE_SQUASHSTRIKE_PHASE2_PLAN.md)

## Status: M0 — engine setup & sim port

| Exit test | Result |
| --- | --- |
| All lab presets bit-identical in C# | ✅ 8 ball-only scenarios, every step identical |
| Free-rally traces within tolerance | ✅ exceeded: 11 bot-played rallies (47 racket contacts) **bit-identical**, not just within tolerance |
| Same input log → same checksum on a second runtime | ✅ .NET 8 (SimParity) and Unity 6.3 Mono (EditMode, 21/21 tests) |
| Same checksum on two different PCs | needs a second machine: run the two commands below on it |

## Layout

```
Assets/SquashSim/            SquashSim.Core — the simulation. NO UnityEngine references
  Math/      DMath (deterministic fdlibm math), V3
  Config/    PhysicsConfig — every tunable number (port of lab/config/PhysicsConfig.js)
  Court/     CourtDimensions, CourtSurfaces (analytic planes)
  Ball/      BallState, BallTemperature, BallPhysics, SimEvent
  Player/    movement, facing, aim, prediction, assist, swing planner/controller,
             commitment solver (deception), racket physics, contact assist, controller
  Input/     InputIntent — the semantic input the sim consumes
  Lab/       BallLauncher, TestPresets, LabSimulation (lab/main.js fixed step)
Assets/SquashSim.Testing/    snapshot/fingerprint, binary replays, EditMode parity tests
Tools/TraceRecorder/         Node: records golden traces from the unmodified HTML lab
Tools/SimParity/             .NET 8: replays traces through SquashSim.Core, diffs every step
```

Every file in `Assets/SquashSim` is a line-for-line port of the lab module
named in its header. The lab README's invariants all still hold; one is
added: **the sim calls `DMath`, never `System.Math`, for transcendental
functions** (see `Tools/README.md`).

Sim coordinates are the lab's (right-handed, front wall at z = 0). Only the
view layer will convert to Unity's left-handed world (M1).

## Verify

```bash
dotnet run --project Tools/SimParity -c Release -- --traces Tools/SimParity/Traces
unity test . --mode EditMode
```

## M1 — Unity warm-up court

Open `Assets/Scenes/WarmUpCourt.unity` and press Play (rebuild it any time with
**re:SquashStrike > Build Warm-Up Court Scene**). It is the HTML lab, in Unity:
same sim (`LabSimulation`), same presets, same feel.

| Piece | Where |
| --- | --- |
| Fixed 240 Hz step on real time, time scale, pause, single step, 12-step cap | `SquashSim.Lab.SimulationClock` (Core) driven by `LabSimRunner` in `Update` |
| Render interpolation between the last two steps | `RenderSnapshot` in `LabSimRunner` |
| Input System → `InputIntent` (WASD/stick, mouse = relative virtual stick, LMB/A Hit, RMB/B Touch) | `IntentSource` |
| Sim → Unity coordinates (x mirrored; the only place that converts) | `SimToWorld` |
| Greybox court, ball (visual scale, temperature colour, real spin), capsule athlete with shoulder line, racket from the physics transform | `Views/` |
| Back-wall TV camera | `GameplayCameraRig` |
| Lab tools: F1 panel, F2 debug view, 1-4 time scale, P, `.`, R, Ctrl+R, F, G, [ ], H, F3 | `DebugOverlay` |

Click the court to capture the mouse (aim is relative, like a stick); Esc pauses
and frees it.

Tests: EditMode `ClockTests` drive golden rallies through the clock at 30/60/144/240 fps,
jittery frames and 0.1x — every step bit-identical. PlayMode `WarmUpCourtTests` boot the
scene, drive a virtual gamepad, and save `Captures/M1_WarmUpCourt.png`.

## M2 — one full game vs the wall-bot

Open `Assets/Scenes/Match.unity` and press Play (scenes are rebuilt by
**re:SquashStrike > Build Scenes**). You serve first from the right box: Hit to
toss, Hit again to strike — aim up and across.

| Piece | Where |
| --- | --- |
| Two athletes, one ball, serve flow, turn gating | `SquashSim/Match/MatchSimulation` |
| Rules: tin, out lines (lines are out), down, not up, serve faults (service line, front wall first, opposite back quarter), foot fault, dropped toss, let / hit-self | `RallyReferee` — an observer, never touches the ball |
| PAR-11 (win by 2) and English (hand-out, 8-8 set one/two), box alternation, lets | `Scoring` (`MatchScore`) |
| Ball vs body (dead bounce, swept so 70 m/s cannot slip through), body vs body | `BodyCollision` |
| Recovery after a stroke (longer when stretched) | `Recovery` config + `PlayerMovement` modifiers |
| Split-step (Space / LT) timed to the opponent's contact: boost, or planted if mistimed | `SplitStepTracker` |
| The opponent: plays through `InputIntent` only, reads the ball with the real integrator | `WallBot` |
| Score bug, referee call banner, serve prompt, game over | `SquashGame/MatchHud` |

The lab simulation is untouched: every match mechanic is off unless
`MatchSimulation` drives it, and SimParity still reports all 19 lab traces
bit-identical.

```bash
dotnet run --project Tools/MatchBench -c Release -- --games 20 [--scoring english] [--verbose] [--trace]
```

plays wall-bot vs wall-bot headless (~250x real time), prints how rallies end
and shots per rally, and replays every game to prove it is deterministic.

Tests: EditMode `MatchTests` (34: every referee call, both scoring systems,
collisions, recovery, split-step, full deterministic games); PlayMode
`MatchPlayTests` serve with a virtual gamepad through to a referee call.

## M3 — AI opponent v1 + interference v1

The Match scene now plays the AI (default: Retriever, medium). F6 cycles the
opponent (Retriever, Attacker, Shot-maker, Grinder, wall-bot), F7 the difficulty.

| Piece | Where |
| --- | --- |
| AI v1: perceives with a reaction delay; picks shots by rolling out the real ball integrator for ~50 candidate aims (+ Touch drops) from the predicted contact, using RacketPhysics' contact maths for the planned swing; scores pressure, length, width, height and risk margins sized to its own measured accuracy; re-plans until the stroke commits; acts only through InputIntent (stick slew, timing jitter, aim error) | `SquashSim/Match/OpponentBrain` |
| Personalities (weights) x difficulty (execution) | `AiProfile` |
| Recovery to a T shaded by the opponent's recent shots; clearing out of the striker's way; split-step timed to the opponent's contact; appeals when an obstruction costs it the shot | `OpponentBrain` |
| A player's read of where the ball will be playable | `BallReader` (shared with the wall-bot) |
| Obstruction detection: access to the ball, swing space, line to the front wall | `Interference` |
| Appeal (Q / pad Y): obstructed this turn -> Let, clear -> No let (appellant loses) | `RallyReferee.OnAppeal` |
| Referee replay: last 3.5 s of render state at 0.5x from an overhead camera, automatic after let / no let, V on demand; the simulation waits | `SimRunnerBase` replay buffer, `GameplayCameraRig` |

`Tools/MatchBench` now pits any two brains (`--p0/--p1 retriever|attacker|shotmaker|grinder|wallbot`,
`--d0/--d1` difficulty) and reports wins, endings, shots per rally, rally duration and
metres run (the fatigue-tuning data). `--calibrate` measures the AI's model error.

Where AI v1 stands (8-game benches, hard): Grinder vs Retriever mean 3.1 shots per rally,
rallies up to 12; AI beats the wall-bot 8-0; hard beats easy 7-1 with the same personality;
every game replays identically. Rallies are still shorter than real squash (club players
average ~5-6 shots): most rallies end on a winner the receiver cannot reach or an
unforced error. Raising that is AI tuning work for M3.5 alongside fatigue.

Tests: EditMode `InterferenceAndAiTests` (detection geometry, Let / No let, who may appeal,
AI beats the wall-bot, hard beats easy, determinism, rally stats); PlayMode
`AppealReplayTests` appeals with a virtual pad and checks the replay pauses and resumes play.
80 EditMode + 6 PlayMode pass.

## M3.5 — interference v2, fatigue, AI rally length

| Piece | Where |
| --- | --- |
| Referee judgement on an appeal: no obstruction, unreachable ball or no effort -> **No let**; in the swing or in the line to the front wall -> **Stroke**; in the way but clearing -> **Let**; in the way and not clearing -> **Stroke**. Effort and clearing are measured as speeds over the turn | `Interference.Judge` / `Decide` |
| Ball hits a player: own shot -> striker loses; opponent, ball going straight to the front wall -> Stroke; via a side wall -> Let; would not have been good -> striker loses; after the front wall, touches the next striker -> they lose; hits the non-striker -> Stroke | `RallyReferee.OnBodyHit` (projects the deflected ball's path) |
| Fatigue: stamina drains with (speed/max)^2 and lunges, recovers standing and between rallies; costs a little top speed, acceleration and reach balance; shown on the body (stoop + breathing), never as a bar | `Match/Fatigue`, `AthleteView` |
| Contact prediction respects the one-bounce rule in matches (the lab's is unchanged) | `Prediction.MaxFloorBounces` |
| AI: intercept-based ball read, plants when within reach, waits outside the striker's corridor/swing/line, starts clearing early, appeals only when it expects the decision | `BallReader`, `OpponentBrain` |

Tuning, from `Tools/MatchBench` (10 games, hard Grinder vs Retriever):

| | M3 | M3.5 |
| --- | --- | --- |
| shots per rally (mean / median / max) | 3.1 / 2 / 12 | **4.6 / 4 / 17** |
| rallies with 6+ shots | 17 of 211 | **59 of 190** |
| AI rally strokes contacted | ~87% | **95%** |
| stamina at rally end (mean / after 10 s+ rallies / lowest) | - | **0.89 / 0.72 / 0.51** |

Diagnostics added to MatchBench: `--notup` (was a failed retrieval reachable? did the
player swing?), `--calibrate` (AI model accuracy). Interference still ends about 30% of
evenly matched AI rallies (lets ~11%, strokes ~18%), on the high side of club squash.
Real squash rally lengths (club ~5-6, pro ~10+) remain ahead.

Tests: 99 EditMode (referee judgement table, every ball-hits-player case, fatigue
drain/recovery/slowdown, fresh athlete moves exactly as in the lab) + 7 PlayMode
(including a tired athlete visibly stooping) pass; lab parity still bit-identical.

## M4 — the athlete in (built in code)

Both scenes now show the Gate A athlete instead of the greybox capsule, built at
runtime with no Blender and no new packages:

| Piece | Where |
| --- | --- |
| Gate B rig contract: 74 bones at frozen rest positions, racket sockets, helpers | `SquashGame/Runtime/Athlete/RigContract` (port of `lab/character/RigDefinition.js`) |
| Gate A body: superellipse lofts with Catmull-Rom smoothing, surface normals, mirrored limbs, joint blends - each form riding its Gate B bone | `Loft`, `MannequinForms` (port of `SurfaceSweep.js` / `MannequinBuilder.js`) |
| Gate A racket A.1 (684 mm, teardrop 301 x 188, sweet spot 570 mm) | `RacketMesh` (port of `RacketProxy.js`) |
| Skeleton + meshes assembled; rig frame -> Unity mirror happens here only | `AthleteRig` |
| Our own analytic two-bone IK | `TwoBoneIK` |
| Posing from sim state every frame: root (gameplay), crouch by reach rung and low balls, gait, torso twist to the commitment solver's body yaw, lean, head on the ball, leg IK to gait/lunge targets, the body travelling toward far balls, racket placed exactly on the simulated racket, hand IK onto its grip socket, free-arm balance, fatigue stoop | `AthleteFigure` |

The racket is never animated: its sweet spot is the simulated racket head and its strings
face the simulated face normal, so the racket you see is the racket that hits. Stroke
"poses" are sampled by the swing itself - arm, torso and lunge follow the sim racket.

M4 exit test (PlayMode `AthleteTests`, ~42k frames of live feeds with a virtual pad):
rendered sweet spot vs simulated racket **0.00 mm** worst; hand on the grip at racket
contact **<= 3.4 mm**; median grip gap 0 mm. Off-contact, the biggest backswings still
leave the hand up to ~0.1-0.2 m off the grip (95th percentile) where the sim's swing
radius exceeds an arm plus a racket. Known Gate A artefacts remain (thin seams where
separate forms meet under twist); the Gate B skinned mesh replaces the forms later
without touching the skeleton or the poser.

EditMode `AthleteRigTests` re-derive the frozen Gate A measurements from the C# bones
(segment lengths, separations, stature, socket 62.3 mm from the wrist), check the body
stands at 1.800 m on the floor, the racket is within the WSF length, and the IK.

## Next: M5 — local 2-player and match flow

Warm-up phase, best-of-5, breaks with ball cooling, an audio pass, two local players.
