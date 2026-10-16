/* ===========================================================
   Tools/TraceRecorder/bot.mjs
   A scripted test player that produces INPUT ONLY, the way a person
   would: move toward where the assist system says the hitting spot is,
   press Hit/Touch when a ball is coming, push the aim stick around.

   It reads lab state to decide, but everything it does is recorded as
   per-step input rows, and the C# replay consumes those rows - the bot
   itself never needs porting.

   Deterministic: its only randomness is a seeded LCG.
=========================================================== */

function lcg(seed) {
  let s = seed >>> 0;
  return () => {
    s = (Math.imul(s, 1664525) + 1013904223) >>> 0;
    return s / 4294967296;
  };
}

const z = (v) => (v === 0 ? 0 : v);   // never emit -0 (JSON would lose it)

/**
 * @param opts  { seed, strokes: 'tap'|'hold'|'mixed', touchChance, deception,
 *                analog, triggerLead }
 * @param preset the lab preset, for the home position
 */
export function createBot(opts, preset) {
  const rnd = lcg(opts.seed || 1);
  const home = { x: preset.player.x, z: preset.player.z };
  const triggerLead = opts.triggerLead ?? 0.42;

  let aimX = 0, aimY = 0;          // current stick
  let aimTX = 0, aimTY = 0;        // stick target
  let holdLeft = 0;                // steps the stroke button stays down
  let button = 6;                  // 5 = hit, 6 = touch (index into the row)
  let pressStep = false;
  let releaseStep = false;
  let cooldown = 0;
  let deceiveAt = -1;

  return {
    next(ctrl, ball) {
      const row = [0, 0, 0, 0, 0, 0, 0];
      const p = ctrl.player;
      const ma = ctrl.assist;
      const pred = ctrl.prediction;

      /* ---- movement ---- */
      const target = ma.hasIdealSpot ? ma.idealSpot : home;
      const dx = target.x - p.pos.x, dz = target.z - p.pos.z;
      const d = Math.sqrt(dx * dx + dz * dz);
      if (d > 0.12) {
        row[0] = z(dx / d); row[1] = z(dz / d);
        row[2] = opts.analog ? Math.min(1, 0.35 + d) : 1;
      }

      /* ---- stroke ---- */
      if (cooldown > 0) cooldown--;
      const idle = ctrl.swing.state === 'idle';
      if (holdLeft === 0 && idle && cooldown === 0 && pred.valid && !ball.asleep &&
          pred.time < triggerLead) {
        const kind = opts.strokes === 'mixed' ? (rnd() < 0.5 ? 'tap' : 'hold') : opts.strokes;
        button = rnd() < (opts.touchChance || 0) ? 6 : 5;
        holdLeft = kind === 'tap' ? 1 : 30 + Math.floor(rnd() * 70);   // 0.125 .. 0.42 s
        pressStep = true;
        aimTX = z(Math.round((rnd() * 2 - 1) * 100) / 100);
        aimTY = z(Math.round((rnd() * 2 - 1) * 100) / 100);
        deceiveAt = opts.deception && kind === 'hold' && rnd() < 0.6
          ? Math.floor(holdLeft * (0.5 + rnd() * 0.4)) : -1;
        cooldown = 90;
      }

      let flags = 0;
      if (holdLeft > 0) {
        if (pressStep) { flags |= 1; pressStep = false; }
        holdLeft--;
        if (holdLeft === 0) { releaseStep = true; }
        else flags |= 4;
        if (holdLeft === deceiveAt) { aimTX = z(-aimTX); }   // late switch of side
      } else if (releaseStep) {
        flags |= 2; releaseStep = false;
      }
      if (holdLeft === 0 && releaseStep) { flags |= 2; flags &= ~4; releaseStep = false; }
      row[button] = flags;

      /* ---- aim stick slews toward its target, like a thumb ---- */
      const slew = 0.06;
      aimX += Math.max(-slew, Math.min(slew, aimTX - aimX));
      aimY += Math.max(-slew, Math.min(slew, aimTY - aimY));
      aimX = Math.round(aimX * 1e6) / 1e6; aimY = Math.round(aimY * 1e6) / 1e6;
      row[3] = z(aimX); row[4] = z(aimY);
      return row;
    }
  };
}
