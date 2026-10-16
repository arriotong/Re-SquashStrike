# Tools — lab parity

These tools prove that `SquashSim.Core` (C#) plays **exactly** like the HTML
Warm-Up Lab (`SQUASH FRONTEND MODIFIED/lab`), bit for bit, at every 1/240 s step.

```
HTML lab (JS, unmodified) ──TraceRecorder──► Traces/*.json.gz   (golden: inputs + per-step fingerprints)
                                                   │
                       SimParity (.NET 8) ◄────────┘  replays them through SquashSim.Core
                                │
                                └──► Replays/*.ssreplay ──► Unity EditMode test (Mono) replays them again
```

## Day-to-day: check the port still matches

```bash
dotnet run --project Tools/SimParity -c Release -- --traces Tools/SimParity/Traces
```

The last line reads `PASS: 19 identical, 0 failed, 42480 steps compared`. On a failure it
prints the first diverging step and, at the next full frame, exactly which
fields differ and by how many ulps.

Unity side (same replays, on Unity's Mono runtime):

```bash
unity test . --mode EditMode
```

## When the lab changes: re-record

```bash
node Tools/TraceRecorder/record.mjs
dotnet run --project Tools/SimParity -c Release -- --traces Tools/SimParity/Traces --export-replays Tools/SimParity/Replays
```

`record.mjs` options: `--lab <path>` (default `../SQUASH FRONTEND MODIFIED/lab`),
`--only <scenario>`, `--full-every <n>` (full-frame interval; `1` for a
detailed diff while debugging), `--math native` (record with Node's own
Math, for comparison only — not for parity).

## The pieces

| Path | What it is |
| --- | --- |
| `TraceRecorder/record.mjs` | Imports the lab's own modules in Node, runs `main.js`'s fixed step, writes traces. |
| `TraceRecorder/dmath.mjs` | Deterministic math (fdlibm ports). Patched into `Math` before the lab loads. Mirrors `Assets/SquashSim/Math/DMath.cs` line for line. |
| `TraceRecorder/scenarios.mjs` | The scenario list: 8 ball-only feeds, 11 full-pipeline rallies. Data only. |
| `TraceRecorder/bot.mjs` | Scripted test player: chases the assist's ideal spot, taps/holds Hit/Touch, swings the aim stick, switches aim late. Its output is recorded as plain input rows, so it never needs porting. |
| `TraceRecorder/snapshot.mjs` | Frame layout + fingerprint. Mirrored by `Assets/SquashSim.Testing/Snapshot.cs`. |
| `SimParity/` | .NET 8 console app. Compiles the same `Assets/SquashSim` sources Unity does, at C# 9. Also checks the recorded config maps completely onto `SimConfig` and that C# defaults equal JS defaults. |
| `SimParity/Traces/` | Golden traces (gzipped JSON). |
| `SimParity/Replays/` | The same scenarios as compact binary replays for runtimes without a JSON library. |

## Why deterministic math

`+ − × ÷` and `sqrt` are exact everywhere, but `sin`, `cos`, `atan2`, `exp`, `pow`
are not specified to the last bit, so different runtimes (and different
CPUs/OSes) may disagree. Rollback netcode requires two PCs to agree exactly,
so the sim uses its own implementations (`DMath`). Measured on this machine:
DMath's `sin/cos/atan/atan2/exp/log/hypot` equal Node's to the bit
(200k–500k samples each); `pow` differs from Node-on-Windows by 1 ulp in
~10% of cases (Node takes `pow` from the Windows C runtime). Therefore the
traces are recorded with DMath patched in. Recording with native math
changes no ball-only scenario and no racket-contact count; full-pipeline
runs drift apart only through that 1-ulp aim-shaping difference.

**Never call `System.Math.Sin/Cos/Tan/Atan/Atan2/Exp/Log/Pow` inside
`Assets/SquashSim`.** Use `DMath`. (`Math.Sqrt`, `Abs`, `Floor` are fine.)
