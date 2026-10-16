/* ===========================================================
   Tools/TraceRecorder/snapshot.mjs
   What a trace frame contains, and how it is fingerprinted.

   MIRRORED BY Tools/SimParity/Trace/Snapshot.cs. The two must build
   frames with the same fields in the same order, or every hash
   mismatches. Encoding rules (both sides):
     - finite number   -> the number
     - non-finite      -> the string 'Inf' / '-Inf' / 'NaN'
     - boolean         -> 1 / 0
     - absent / null   -> null
     - -0              -> 0   (JSON cannot carry the sign of zero)
=========================================================== */

export function num(v) {
  if (v === null || v === undefined) return null;
  if (typeof v === 'boolean') return v ? 1 : 0;
  if (Number.isNaN(v)) return 'NaN';
  if (v === Infinity) return 'Inf';
  if (v === -Infinity) return '-Inf';
  return v === 0 ? 0 : v;
}
const str = (s) => (s === null || s === undefined ? null : String(s));

function ballPart(b) {
  return [
    num(b.pos.x), num(b.pos.y), num(b.pos.z),
    num(b.vel.x), num(b.vel.y), num(b.vel.z),
    num(b.omega.x), num(b.omega.y), num(b.omega.z),
    num(b.temperature), num(b.resting), num(b.asleep),
    num(b.floorSinceFrontWall), num(b.floorBounces), num(b.simTime)
  ];
}

function eventPart(ev) {
  if (ev.type === 'impact') {
    return ['i', str(ev.surfaceId),
      num(ev.point.x), num(ev.point.y), num(ev.point.z),
      num(ev.impactSpeed), num(ev.normalSpeed), num(ev.restitution),
      num(ev.energyLost), num(ev.temperature), num(ev.resting), num(ev.nick), num(ev.time)];
  }
  const d = ev.deception || {};
  return ['r',
    num(ev.point.x), num(ev.point.y), num(ev.point.z),
    str(ev.stroke), str(ev.mode), str(ev.technique), num(ev.side), num(ev.rushed),
    num(ev.headSpeed), num(ev.faceNormal.x), num(ev.faceNormal.y), num(ev.faceNormal.z),
    num(ev.offCentre), num(ev.offCentreNorm), num(ev.restitution), num(ev.assistApplied),
    num(ev.ballSpeedOut), num(ev.outVel.x), num(ev.outVel.y), num(ev.outVel.z),
    num(ev.energyLost), num(ev.temperature),
    str(ev.label), str(ev.reachState), num(ev.balance), num(ev.timingCorrection),
    num(d.shortfallDeg), num(d.lateBySeconds), num(d.achieved), num(d.rangeScale)];
}

export function snapshotBall(ball, events) {
  return [ballPart(ball), events.map(eventPart)];
}

export function snapshotLab(ctrl, ball, events) {
  const p = ctrl.player, a = ctrl.aim, pr = ctrl.prediction, ma = ctrl.assist;
  const s = ctrl.swing, ri = ctrl.intentSolver, r = ctrl.racket;
  return [
    ballPart(ball),
    [num(p.pos.x), num(p.pos.z), num(p.vel.x), num(p.vel.z), num(p.facing), num(p.speed),
      num(p.balance), num(p.swingSide), str(p.movementState), str(p.reachState), num(p.adjusting)],
    [num(a.biasX), num(a.biasY), num(a.desiredYaw), num(a.desiredPitch), num(a.neutralPitch),
      str(a.intendedWall)],
    [num(pr.valid), num(pr.time), num(pr.point.x), num(pr.point.y), num(pr.point.z),
      num(pr.vel.x), num(pr.vel.y), num(pr.vel.z), num(pr.distance), num(pr.bouncesBefore),
      num(pr.isVolley), num(pr.stepsSinceRefresh)],
    [num(ma.active), num(ma.travelUsed), num(ma.hasIdealSpot), num(ma.idealSpot.x), num(ma.idealSpot.z),
      str(ma.reachState), num(ma.balance), num(ma.quality), num(ma.correction.x), num(ma.correction.z)],
    [str(s.state), str(s.stroke), str(s.mode), num(s.holdSeconds), num(s.prepQuality), num(s.armedAge),
      num(s.expectedStartIn), s.buffered ? str(s.buffered.stroke) : null, num(s.bufferedAge),
      num(s.startIn), num(s.rushed), num(s.timingCorrection), num(s.swingTime), num(s.duration),
      num(s.phase), num(s.totalArc), num(s.startAngle), num(s.contactAngle), num(s.contactHeight),
      num(s.center.x), num(s.center.z), num(s.reach), num(s.side), num(s.pathYaw),
      num(s.hasContacted), num(s.cooldown),
      s.plan ? str(s.plan.technique) : null, s.plan ? num(s.plan.headSpeed) : null],
    [str(ri.phase), num(ri.bodyYaw), num(ri.faceYaw), num(ri.facePitch), num(ri.pathYaw),
      num(ri.rangeScale), num(ri.faceLimited)],
    [num(r.anchor.x), num(r.anchor.y), num(r.anchor.z),
      num(r.headPos.x), num(r.headPos.y), num(r.headPos.z),
      num(r.headVel.x), num(r.headVel.y), num(r.headVel.z),
      num(r.faceNormal.x), num(r.faceNormal.y), num(r.faceNormal.z),
      num(r.angle), num(r.reach), num(r.headSpeed), num(r.assistApplied), num(r.assistMiss)],
    str(ctrl.interpretation.label),
    events.map(eventPart)
  ];
}

/* ---------------- fingerprint ----------------
   Two independent 32-bit FNV-1a passes (different offset bases) over a
   depth-first walk of the frame:
     number -> its 8 IEEE-754 bytes, little-endian, preceded by tag 1
     string -> tag 2, then each UTF-16 unit as 2 bytes, then 0xFF 0xFF
     null   -> tag 3
     array  -> tag 4, then length (4 bytes LE), then each element */
const _f = new Float64Array(1);
const _u8 = new Uint8Array(_f.buffer);
const PRIME = 16777619;

function feed(h, byte) { return Math.imul(h ^ byte, PRIME) >>> 0; }

function walk(state, v) {
  if (v === null) { state.a = feed(state.a, 3); state.b = feed(state.b, 3); return; }
  if (typeof v === 'number') {
    state.a = feed(state.a, 1); state.b = feed(state.b, 1);
    _f[0] = v;
    for (let i = 0; i < 8; i++) { state.a = feed(state.a, _u8[i]); state.b = feed(state.b, _u8[i]); }
    return;
  }
  if (typeof v === 'string') {
    state.a = feed(state.a, 2); state.b = feed(state.b, 2);
    for (let i = 0; i < v.length; i++) {
      const c = v.charCodeAt(i);
      state.a = feed(feed(state.a, c & 0xff), c >>> 8);
      state.b = feed(feed(state.b, c & 0xff), c >>> 8);
    }
    state.a = feed(feed(state.a, 0xff), 0xff); state.b = feed(feed(state.b, 0xff), 0xff);
    return;
  }
  if (Array.isArray(v)) {
    state.a = feed(state.a, 4); state.b = feed(state.b, 4);
    const n = v.length;
    for (let i = 0; i < 4; i++) {
      const byte = (n >>> (8 * i)) & 0xff;
      state.a = feed(state.a, byte); state.b = feed(state.b, byte);
    }
    for (const e of v) walk(state, e);
    return;
  }
  throw new Error(`unencodable value in frame: ${typeof v}`);
}

export function hashFrame(frame) {
  const state = { a: 2166136261, b: 0x9747b28c };
  walk(state, frame);
  return state.a.toString(16).padStart(8, '0') + state.b.toString(16).padStart(8, '0');
}
