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

## Next: M2 — one full game vs a wall-bot

Referee, serve, PAR-11 + English scoring, ball-vs-body, player capsules,
recovery penalty, split-step.
