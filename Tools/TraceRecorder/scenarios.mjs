/* ===========================================================
   Tools/TraceRecorder/scenarios.mjs
   The golden-trace set. Each entry is data; adding a scenario never
   touches a code path.

   kind 'ball' - ball physics only: launcher + stepBall, nothing else.
   kind 'lab'  - the full lab/main.js fixed step: launcher, the whole
                 player pipeline (movement, prediction, assist, facing,
                 aim, swing, commitment, racket, contact), then the ball.

   config: deep-merged over PhysicsConfig defaults before the run.
   Steps are 1/240 s: 1440 = 6 s, 2400 = 10 s, 3600 = 15 s.
=========================================================== */

const PRESETS = ['free', 'straightLength', 'dropLowBall', 'backWall', 'tightRail', 'highSpeed'];

export const SCENARIOS = [
  ...PRESETS.map((preset) => ({
    name: `ball_${preset}`, kind: 'ball', preset, steps: 1440,
    description: `Ball-only feed from the ${preset} preset.`
  })),
  {
    name: 'ball_spin_magnus', kind: 'ball', preset: 'free', steps: 1440,
    description: 'Topspin + sidespin feed with Magnus switched on: exercises the cross products and spin/friction coupling.',
    launch: { spin: { x: -180, y: 60, z: 25 }, speed: 24, elevationDeg: 12, azimuthDeg: 14 },
    config: { world: { magnusK: 0.0035 } }
  },
  {
    name: 'ball_hot_cold', kind: 'ball', preset: 'straightLength', steps: 2400,
    description: 'Very cold ball at speed: temperature curve ends, heating and cooling.',
    launch: { temperature: 8.0, speed: 45 }
  },

  {
    name: 'lab_free_idle', kind: 'lab', preset: 'free', repeat: true, steps: 2400,
    description: 'Warm-up feed with nobody touching the controls.'
  },
  {
    name: 'lab_free_tap', kind: 'lab', preset: 'free', repeat: true, steps: 3600,
    description: 'Quick (tap) strokes only, random aim.',
    bot: { seed: 11, strokes: 'tap' }
  },
  {
    name: 'lab_free_hold', kind: 'lab', preset: 'free', repeat: true, steps: 3600,
    description: 'Prepared (hold) strokes with late aim switches (deception).',
    bot: { seed: 22, strokes: 'hold', deception: true }
  },
  {
    name: 'lab_free_mixed_touch', kind: 'lab', preset: 'free', repeat: true, steps: 3600,
    description: 'Mixed tap/hold, 40% Touch, analog movement.',
    bot: { seed: 33, strokes: 'mixed', touchChance: 0.4, analog: true, deception: true }
  },
  {
    name: 'lab_free_noassist', kind: 'lab', preset: 'free', repeat: true, steps: 2400,
    description: 'Contact and movement assistance off.',
    bot: { seed: 44, strokes: 'mixed', touchChance: 0.2 },
    config: { assist: { enabled: false }, movementAssist: { enabled: false } }
  },
  {
    name: 'lab_free_early', kind: 'lab', preset: 'free', repeat: true, steps: 2400,
    description: 'Presses far too early: input buffer expiry, armed timeouts, rushed strokes.',
    bot: { seed: 55, strokes: 'mixed', triggerLead: 1.4 }
  },
  ...PRESETS.filter((p) => p !== 'free').map((preset, i) => ({
    name: `lab_${preset}_mixed`, kind: 'lab', preset, repeat: true, steps: 2400,
    description: `Bot playing the ${preset} feed.`,
    bot: { seed: 100 + i, strokes: 'mixed', touchChance: 0.25, deception: true }
  }))
];
