/* ===========================================================
   Tools/TraceRecorder/record.mjs
   Runs the HTML Warm-Up Lab's simulation headless in Node and records
   golden traces for the C# port (Tools/SimParity) to match.

   The lab code is imported UNMODIFIED from its own folder. The only
   thing changed is the global Math object: its transcendental functions
   are replaced by dmath.mjs, the same algorithms SquashSim.Core uses,
   so the two runtimes can agree to the last bit.

   Usage:
     node record.mjs [--lab <path>] [--out <dir>] [--math dmath|native]
                     [--only <scenario>] [--full-every <n>]

   Output: one <scenario>.json.gz per scenario. See README.md for the
   format; Tools/SimParity/Trace/Snapshot.cs must build frames in exactly
   the order snapshot() below does.
=========================================================== */

import { gzipSync } from 'node:zlib';
import { writeFileSync, mkdirSync } from 'node:fs';
import { resolve, dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import * as dmath from './dmath.mjs';
import { SCENARIOS } from './scenarios.mjs';
import { createBot } from './bot.mjs';
import { snapshotBall, snapshotLab, hashFrame } from './snapshot.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, '..', '..');

/* ---------------- args ---------------- */
const args = process.argv.slice(2);
function arg(name, fallback) {
  const i = args.indexOf(`--${name}`);
  return i >= 0 && i + 1 < args.length ? args[i + 1] : fallback;
}
const labDir = resolve(arg('lab', resolve(repoRoot, '..', 'SQUASH FRONTEND MODIFIED', 'lab')));
const outDir = resolve(arg('out', join(repoRoot, 'Tools', 'SimParity', 'Traces')));
const mathMode = arg('math', 'dmath');
const only = arg('only', null);
const fullEvery = Number(arg('full-every', '24'));

if (mathMode === 'dmath') dmath.installOn(Math);
else if (mathMode !== 'native') throw new Error(`--math must be dmath or native, got ${mathMode}`);

/* ---------------- lab modules (imported after Math is patched) ---------------- */
const lab = (p) => import(pathToFileURL(join(labDir, p)).href);
const { PhysicsConfig, resetConfigToDefaults } = await lab('config/PhysicsConfig.js');
const { createCourtSurfaces } = await lab('court/CourtSurfaces.js');
const { createBallState } = await lab('ball/BallState.js');
const { stepBall } = await lab('ball/BallPhysics.js');
const ctrlMod = await lab('player/TestPlayerController.js');
const { createBallLauncher } = await lab('devlab/BallLauncher.js');
const { findPreset, applyPreset } = await lab('devlab/PhysicsTestPreset.js');

/* ---------------- helpers ---------------- */
function deepMerge(dst, src) {
  for (const k of Object.keys(src)) {
    const v = src[k];
    if (v && typeof v === 'object' && !Array.isArray(v) && dst[k] && typeof dst[k] === 'object') deepMerge(dst[k], v);
    else dst[k] = v;
  }
}

function configSnapshot() {
  // JSON drops functions (none exist) and writes Infinity as null; the
  // C# loader maps a null `below` back to +Infinity.
  return JSON.parse(JSON.stringify(PhysicsConfig));
}

function makeIntent() {
  const stroke = () => ({ pressed: false, released: false, held: false, heldSeconds: 0 });
  return {
    move: { x: 0, z: 0 }, moveMagnitude: 0, aimRaw: { x: 0, y: 0 },
    hit: stroke(), touch: stroke(),
    momentumFocus: false, pausePressed: false, hasGamepad: false, device: 'keyboard'
  };
}

/* Input row: [moveX, moveZ, moveMagnitude, aimX, aimY, hitFlags, touchFlags]
   flags: 1 = pressed, 2 = released, 4 = held. */
function applyInputRow(intent, row) {
  intent.move.x = row[0]; intent.move.z = row[1]; intent.moveMagnitude = row[2];
  intent.aimRaw.x = row[3]; intent.aimRaw.y = row[4];
  intent.hit.pressed = (row[5] & 1) !== 0; intent.hit.released = (row[5] & 2) !== 0; intent.hit.held = (row[5] & 4) !== 0;
  intent.touch.pressed = (row[6] & 1) !== 0; intent.touch.released = (row[6] & 2) !== 0; intent.touch.held = (row[6] & 4) !== 0;
}

/* ---------------- scenario runners ---------------- */
function runBallScenario(sc) {
  const preset = findPreset(sc.preset);
  const surfaces = createCourtSurfaces();
  const ball = createBallState();
  const launcher = createBallLauncher(ball);
  launcher.applyConfig(preset.launch);
  if (sc.launch) launcher.applyConfig(sc.launch);
  launcher.fire({ fresh: true });

  const dt = PhysicsConfig.sim.fixedDt;
  const hashes = [], full = [];
  const events = [];
  for (let step = 0; step < sc.steps; step++) {
    events.length = 0;
    stepBall(ball, surfaces, dt, events);
    const frame = snapshotBall(ball, events);
    hashes.push(hashFrame(frame));
    if (step % fullEvery === 0 || events.length > 0) full.push([step, frame]);
  }
  return { inputs: null, hashes, full };
}

function runLabScenario(sc) {
  const preset = findPreset(sc.preset);
  const surfaces = createCourtSurfaces();
  const ball = createBallState();
  const ctrl = ctrlMod.createTestPlayerController();
  const launcher = createBallLauncher(ball);
  const intent = makeIntent();

  // Same as the lab's selectPreset(): configure, park the player, fire fresh.
  applyPreset(preset, {
    launcher, player: ctrl.player,
    resetPlayer: () => ctrlMod.resetTestPlayerController(ctrl)
  });
  if (sc.launch) { launcher.applyConfig(sc.launch); launcher.fire({ fresh: true }); }
  launcher.setRepeat(!!sc.repeat);

  const bot = sc.bot ? createBot(sc.bot, preset) : null;
  const dt = PhysicsConfig.sim.fixedDt;
  const inputs = [], hashes = [], full = [];
  const stepEvents = [];

  for (let step = 0; step < sc.steps; step++) {
    const row = bot ? bot.next(ctrl, ball) : [0, 0, 0, 0, 0, 0, 0];
    inputs.push(row);
    applyInputRow(intent, row);

    // ---- identical to lab/main.js onFixedStep() ----
    stepEvents.length = 0;
    launcher.step(dt);
    const contact = ctrlMod.stepTestPlayerController(ctrl, intent, ball, surfaces, dt);
    intent.hit.pressed = false; intent.hit.released = false;
    intent.touch.pressed = false; intent.touch.released = false;
    if (contact) stepEvents.push(contact);
    stepBall(ball, surfaces, dt, stepEvents);
    // -------------------------------------------------

    const frame = snapshotLab(ctrl, ball, stepEvents);
    hashes.push(hashFrame(frame));
    if (step % fullEvery === 0 || stepEvents.length > 0) full.push([step, frame]);
  }
  return { inputs, hashes, full };
}

/* ---------------- main ---------------- */
mkdirSync(outDir, { recursive: true });
const summary = [];
for (const sc of SCENARIOS) {
  if (only && sc.name !== only) continue;

  resetConfigToDefaults();
  if (sc.config) deepMerge(PhysicsConfig, sc.config);
  const config = configSnapshot();

  const t0 = performance.now();
  const result = sc.kind === 'ball' ? runBallScenario(sc) : runLabScenario(sc);
  const ms = performance.now() - t0;

  const trace = {
    format: 1,
    name: sc.name,
    kind: sc.kind,
    description: sc.description,
    math: mathMode,
    node: process.version,
    preset: sc.preset,
    repeat: !!sc.repeat,
    launchOverride: sc.launch || null,
    steps: sc.steps,
    fullEvery,
    config,
    inputs: result.inputs,
    hashes: result.hashes,
    full: result.full
  };
  const json = JSON.stringify(trace);
  const file = join(outDir, `${sc.name}.json.gz`);
  writeFileSync(file, gzipSync(json, { level: 9 }));

  const contacts = result.full.reduce((n, [, f]) => n + f[f.length - 1].filter((e) => e[0] === 'r').length, 0);
  summary.push(`${sc.name.padEnd(28)} steps=${String(sc.steps).padStart(5)} racketContacts=${String(contacts).padStart(3)} ${(json.length / 1024).toFixed(0)}KB ${ms.toFixed(0)}ms`);
}
console.log(`lab: ${labDir}\nmath: ${mathMode}\nout: ${outDir}`);
console.log(summary.join('\n'));
