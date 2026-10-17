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

## M5 — match flow, audio, local 2-player

- **Setup panel** (`MatchSetupPanel`): opponent (AI personality + difficulty, or Player 2), match length (1 / best of 3 / best of 5), PAR-11 or English, knock-up on/off. Keyboard, mouse or any pad; replaces the F6/F7 debug keys.
- **Knock-up** (`MatchPhase.WarmUp`): a new ball starts cold (16 °C) and is warmed by the real thermal model. Each player has a 45 s half; dead balls are fed again; the AI knocks up with straight drives back to itself. Enter / pad Y skips it.
- **Best of 5**: a 45 s break between games (the ball cools to about 24 °C, players recover). It starts the next game by itself, or Enter / Y starts it now. At the match result, Enter / Y returns to setup.
- **Local 2P**: each player has an `IntentSource` restricted to their own devices. With one pad: keyboard + mouse vs the pad. With two pads: one pad each (the keyboard stays with P1). Pause from either player; play pauses if a pad is unplugged.
- **Audio** (`Audio/AudioDirector`, `ProceduralSounds`): every sound is synthesised at start-up (no files): strings and frame clack, front / side / glass / tin / floor / nick, bodies, shoe squeaks on hard stops, and a referee chime.
- Core defaults are unchanged (warm-up off, one game), so the lab, the benches and the earlier tests behave exactly as before. The Match scene turns the full flow on.
- MatchBench: `--games-to-win 3 --warmup` plays full matches; `--knockup` traces one knock-up half.

## Match ball — double yellow at PSA pace

`PhysicsConfig.UseMatchBall()` is the game's ball, layered on the lab defaults. The lab itself, and SimParity, stay on `Defaults()`. The Match scene, the warm-up court (`LabSimRunner.MatchBall`, on by default) and MatchBench all use it (`--lab-ball` for the lab's ball).

- **Bounce height follows the WSF double yellow:** from a 100 in drop, 15% at 23 °C and 25% at 45 °C (spec 12–14% / 26–33%). A warm ball gives players a little more height and time.
- **Hard hits still die:** at high impact speed the temperature effect fades (`Temperature.HotSpeedFade`) and restitution falls further. Hot or cold, a hard ball comes off the front wall at nearly the same pace (about 6% apart at 40 m/s), and a 40 m/s length drive dies in the back corner.
- **PSA pace:** off the racket the median is 28 m/s, the 90th percentile 36 and the maximum ~50 (the lab's were 20 / 25 / 30). Lobs and serves keep the lab's soft pace.
- **Temperature:** heating is slowed so a rally ball runs at ~44 °C (median) instead of sitting on the 48 °C cap. It cools to ~33 °C over a break and warms up again in play.
- `MatchBench --ballcheck` measures all of this; `--drive 40 --temp 45` traces one drive.

## Windows build

`Builds/Windows/reSquashStrike.exe` (git-ignored; copy the whole `Builds/Windows` folder to move it).

```
unity build . --target StandaloneWindows64 --execute-method SquashGame.Editor.BuildPlayer.BuildWindows -o Builds/Windows/reSquashStrike.exe
```

or the editor menu **re:SquashStrike > Build Windows Player**. It opens on the match setup panel. The panel has START, PRACTICE (the warm-up court; M / pad View returns) and QUIT. In a match, Esc / Start pauses; while paused, M / View quits to the menu. Alt+Enter toggles fullscreen.

## M6 — controls, reach and athlete animation

Plan and research data: `RE_SQUASHSTRIKE_M6_CONTROLS_PLAN.md`. Measured with `MatchBench --feel` (PSA tracking data as targets) and PlayMode `AthleteTests` / `AimTargetTests`.

- **Always-on aim target** (`Views/AimTargetView`): a ring on the front wall where your aim would send the ball now.
  - It uses `ShotPreview`: the swing's own launch maths (shared with the AI), flown through the real ball physics.
  - The ring's size is the expected spread. It is dim before a contact is predicted, red when the shot would go down.
  - Measured: a median 0.3–0.4 m from where the ball really hits the wall.
- **Mouse:** the mouse moves the target and no longer snaps back after a stroke. Mouse aim speed and invert-Y are on the setup panel, saved per machine.
- **Timing note** (`Views/TimingFeedbackView`): GOOD / EARLY / LATE / MISS by the target after every stroke, from the swing phase at contact.
- **Animation** (`AthleteFigure`):
  - The body turns at a human rate (≤ 540°/s) and favours the front wall instead of spinning to face the ball.
  - Feet stay planted (0 mm slide) and take real steps; the lunge is a step.
  - The pose keys on committed swings, not on the flickering reach prediction.
  - Between swings the racket is a smoothed ready racket; from swing start it locks exactly onto the simulated racket (0 mm at every contact).
- **Visible lunge** (`LungeStep`, game profile):
  - Once a swing commits, the body steps toward the contact so that at contact the ball is at most an arm plus a racket (0.95 m) from the body.
  - Total reach is a little less than before (1.45 m vs 1.55 m).
  - The contact assist's pull on the ball is capped at 0.12 m (was 0.30 m).
- **Movement:**
  - The AI reads low balls (a double yellow often stays under knee height) and eases off inside braking distance. That brought mean movement speed to ~2.0 m/s (PSA: 1.88).
  - The lab's acceleration and braking are kept: slower values cost the AI accuracy, and there's no published squash figure to justify them.
- **Front-wall target aim** (`WallAim`, local players only):
  - The stick or mouse picks a point on the front wall (±2.75 m across, 0.85–4.0 m up, centre 1.7 m) that stays put however the athlete moves.
  - Yaw and pitch are solved through the launch model and real ball flight so the ball goes there.
  - The lowest point is kept above the tin.
  - Scripted human-style play: 94% of strokes reach the front wall (63% before), 0.7% go down (7% before).
  - **Boasts:** push the aim past the edge of the front wall (the outer 15% of left/right travel). The target moves onto that side wall, between you and the front; further = nearer the front. The ring sits there tagged BOAST, and the shot is solved to strike the side wall first.
    - Scripted play: 93% hit the chosen side wall first, 91% go on to the front wall, 3% go down.
  - The AI keeps the lab aim.
- `PhysicsConfig.UseGameProfile()` = the match ball + the M6 reach + the wall aim. The lab and SimParity stay on `Defaults()`.

## M7 (in progress) — a stable, Mario Tennis-style athlete

Plan: `RE_SQUASHSTRIKE_M7_ANIMATION_PLAN.md`.

Step 1 (pose tooling):
- **Authored key poses:** `Athlete/Poses`, with `DefaultPoses` in code and an optional `Resources/PoseLibrary.asset`.
  - Feet are grounded by IK and the head is kept level.
  - The racket arm is posed by target: sweet spot, shaft direction, string facing. That's the same handle the game will use to put the racket on the ball at contact.
- **Pose Lab** (`re:SquashStrike > Pose Lab`): edit poses live and save.
- **Pose sheet:** `unity run . -- -executeMethod SquashGame.Editor.PoseSheet.CaptureFromCommandLine`, then `python Tools/PoseSheet/compose.py` → `Captures/PoseSheet.png`.

Steps 2–3 (in the game), `AthleteFigure`:
- **Locomotion layer:** ready / shuffle / back-pedal / run poses, blended by velocity in the athlete's frame, with planted stepping feet.
- **Stroke layer:** backswing → contact → follow-through, chosen when the swing commits (side, contact height, reach, touch) and played on the sim's swing phase.
- **Racket:** always in the hand. As the simulated racket closes on the ball (within 0.75 m) it blends onto it; it is exact within 0.3 m and at contact (0 mm). The body moves by the arm's exact shortfall.
- **Head:** level, turning to the ball at most 45° sideways and 25° up/down.

## M8 (in progress) — swing registration and controls

Plan: `RE_SQUASHSTRIKE_M8_CONTROLS_PLAN.md`. The tool is `MatchBench --press-audit`: a human-style player's Hit presses, each followed to what it led to.

Step 1 (`PhysicsConfig.UseGameSwing`):
- **Re-timing:** a scheduled swing keeps re-timing to the latest prediction until it starts. Before, it aimed at a contact point that moved as the athlete ran on, and fired early.
- **No air swings:** a held stroke released with no ball in reach waits instead of swinging at air.
- **Result:** presses that connect went from 47% to 87%, and whiffs from 48% to under 1%. The human-style player wins 36% of rallies against the easy AI (was 8%). The AI is unchanged; parity is 19/19.

Simple hitting system (`ArcadeStroke`, local players; replaces the physical swing for them), after the original SquashStrike:
- **Hold to charge, release to hit:** if the ball is within 1.5 m (and below 2.6 m) it's hit; released up to 0.22 s early, it still connects as the ball comes in.
- **The shot:** goes to the aim ring, solved through the real ball flight. Distance from the 0.85 m sweet spot sets the accuracy (PERFECT / GOOD / STRETCH); a full charge adds pace.
- **Auto step:** while the stroke is ready and the ball is about to arrive, the athlete steps up to 0.7 m toward it.
- **Serve:** kept above the service line.
- **AI:** keeps its physical swing.
- **Press audit:** when the scripted human gets to the ball, about 95% of releases connect.

Playtest round (feedback before playing):
- **Pop-ups:** smaller; a hit is a little "Hit!".
- **Serve:** one click, struck from the hand (no toss).
- **Knock-up:** removed from the game.
- **Speed:** the game runs at 85% speed (`SimRunnerBase.GameSpeed`). Every flight and distance is unchanged, there's just more time.
- **High shots:** they are played softer when they would reach the back wall on the full above 1.4 m. High-aim play: out off the back wall went from 4% of lost rallies to 0%.
- **Ball:** drawn 2.2× its real size (physics unchanged) with a short trail.
- **Mouse:** captured as soon as play starts, so the first click is already a stroke.

Playtest round 2:
- **Athletes:** drawn at 88% size (gameplay distances unchanged) and 12% quicker (top speed, acceleration, braking; AI too).
- **Normal shots** are accurate: half the old aim error at a stretch, and gross misses at most 12% (at full stretch).
- *(The charged kill / drop, added in this round, was removed again: a held aim for a lob kept turning into a kill.)*

## M9 — drag-to-aim (the Joy-Con stick)

Plan: `RE_SQUASHSTRIKE_M9_DRAG_AIM_PLAN.md`.
- **Every stroke starts aimed at the middle.**
- **Mouse:** hold Hit / Touch and drag (about 120 px = full aim; setup panel: "Drag distance"), then let go to hit. After the stroke the aim snaps back to the middle. Moving the mouse without holding does nothing.
- **No charge system:** how long you hold does not matter. High aim = lob, low aim = low drive, Touch = soft.
- **The circle on the front wall** is small (16 cm) and soft. It appears only once you drag off the middle, follows your aim (on the side wall, tagged BOAST, in the boast zone), and fades out after the shot.
- **Serve:** with no drag, it goes cross-court.

## Next: M7
