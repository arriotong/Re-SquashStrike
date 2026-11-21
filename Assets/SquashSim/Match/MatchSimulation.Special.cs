// ===========================================================
// Assets/SquashSim/Match/MatchSimulation.Special.cs
// 2026-11-22 the Special Shot (the user, after Mario Tennis: "like
// sniping ... teleporting to the ball, the screen changes into an aiming
// screen, the meter slowly goes down because you're in slow mo aiming ...
// make the shot insanely powerful ... if you use focus the ball gets
// slower and can be got back easier"):
//
//   start     a full meter (the Special card), the ball coming to you:
//             press Special (the Focus key). You teleport to where you'd
//             meet it; the ball hangs there; time stands still
//   aim       move the aim (the ring) anywhere on the front wall - a nick,
//             the tin's edge; the meter drains while you aim
//             (ZoneSeconds from full); Hit fires, an empty meter fires
//   the shot  SpecialPace, straight at the aim; the opponent's reach and
//             timing cut hard (SpecialReachCut, SpecialGraceCut) until
//             their next stroke - almost unreturnable
//   defend    Focus (the same key) while a Special Shot comes at you, with
//             FocusCost of meter: the ball slows to FocusSlow of its pace
//             and most of the cut is lifted - you can get it back
//
// A sim action from the intent (InputIntent.SpecialPressed), so replays
// and both machines online see the same; the AI uses it too
// (OpponentBrain). Deterministic, UnityEngine-free.
// ===========================================================

using static SquashSim.CourtDimensions;

namespace SquashSim
{
    public sealed partial class MatchSimulation
    {
        public const double ZoneSeconds = 2.6, SpecialPace = 64.0, SpecialReachCut = 0.7, SpecialGraceCut = 0.8;
        public const double FocusSlow = 0.5, DefendedReachCut = 0.1, DefendedGraceCut = 0.1;

        /// <summary>Who is aiming a Special Shot now (-1: nobody) and for how long; who struck the Special Shot in flight (-1: none),
        /// and whether its target slowed it with Focus.</summary>
        public int ZoneBy { get; private set; } = -1;
        public double ZoneTime { get; private set; }
        public int SpecialBy { get; private set; } = -1;
        public bool SpecialDefended { get; private set; }
        /// <summary>The Special Shots fired and defended so far (tests).</summary>
        public int SpecialsFired { get; private set; }
        public int SpecialsDefended { get; private set; }
        readonly V3 _zoneBall = new V3();

        static RunMods M(TestPlayerController c) { return c.Player.Mods; }

        /// <summary>Player i may start a Special Shot now: the card, a full meter, the ball on its way to them.</summary>
        public bool CanSpecial(int i)
        {
            var mm = M(Players[i]);
            if (mm == null || mm.Level("special") <= 0 || mm.Power < 1) return false;
            if (Phase != MatchPhase.Rally || Referee.Outcome != null || Referee.Shots == 0 || ZoneBy >= 0) return false;
            if (Referee.PendingStriker != i || SpecialBy == 1 - i) return false;         // (a Special Shot coming: defend it, no counter)
            var pr = Players[i].Prediction;
            return pr.Valid && !Ball.Asleep && Ball.FloorSinceFrontWall < 2;
        }

        /// <summary>A Special Shot is coming at player j, not yet slowed: Focus defends it.</summary>
        public bool SpecialIncoming(int j) { return SpecialBy == 1 - j && !SpecialDefended && Phase == MatchPhase.Rally && Referee.Outcome == null; }

        /// <summary>Each step, before anyone moves: the Special presses (start, defend), the aiming. True: frozen this step.</summary>
        bool SpecialStep(double dt)
        {
            SlowIfDue();
            for (int i = 0; i < 2; i++)
            {
                if (!Intents[i].SpecialPressed) continue;
                Intents[i].SpecialPressed = false;
                if (CanSpecial(i)) StartSpecial(i);
                else if (SpecialIncoming(i)) DefendSpecial(i);
            }
            if (ZoneBy < 0) return false;
            int z = ZoneBy;
            var c = Players[z];
            var mm = M(c);
            ZoneTime += dt;
            mm.Power = System.Math.Max(0, mm.Power - dt / ZoneSeconds);
            Ball.Pos.CopyFrom(_zoneBall); Ball.Vel.Set(0, 0, 0);
            for (int i = 0; i < 2; i++) { Players[i].Player.Vel.Set(0, 0, 0); }
            c.AimOnly(Intents[z], Ball, Surfaces);
            bool fire = (Intents[z].Hit.Pressed || Intents[z].Touch.Pressed) && ZoneTime > 0.15;
            if (fire || mm.Power <= 0) { FireSpecial(z); return false; }
            Intents[0].ClearEdges(); Intents[1].ClearEdges();
            return true;
        }

        void StartSpecial(int i)
        {
            var c = Players[i];
            var pr = c.Prediction;
            ZoneBy = i; ZoneTime = 0;
            _zoneBall.Set(pr.Point.X, System.Math.Max(0.5, pr.Point.Y), pr.Point.Z);
            // the teleport: to where you'd meet it, the ball at your racket
            double sp = PhysicsConfig.Current.MovementAssist.PreferredSpacing;
            double x = System.Math.Max(-HALF_W + 0.3, System.Math.Min(HALF_W - 0.3, pr.Point.X - sp));
            double zz = System.Math.Max(0.8, System.Math.Min(CourtDimensions.LENGTH - 0.3, pr.Point.Z + 0.15));
            c.Player.Pos.Set(x, 0, zz);
            c.Player.Vel.Set(0, 0, 0);
            c.Racket.MarkTeleported();
            // the opponent never stands in the way of it: moved clear (towards the T side away from you), and no call carried over
            var o = Players[1 - i].Player;
            double dx = o.Pos.X - x, dz = o.Pos.Z - zz;
            if (dx * dx + dz * dz < 2.0 * 2.0)
            {
                double ox = x >= 0 ? x - 2.2 : x + 2.2, oz = System.Math.Max(3.0, System.Math.Min(CourtDimensions.LENGTH - 1.0, zz + 1.2));
                o.Pos.Set(System.Math.Max(-HALF_W + 0.4, System.Math.Min(HALF_W - 0.4, ox)), 0, oz);
                o.Vel.Set(0, 0, 0);
                Players[1 - i].Racket.MarkTeleported();
            }
            Interference.Reset();
            Ball.Pos.CopyFrom(_zoneBall); Ball.Vel.Set(0, 0, 0);
            Note(i, "zone", 0);
        }

        void FireSpecial(int i)
        {
            var c = Players[i];
            var mm = M(c);
            ZoneBy = -1;
            mm.Power = 0;
            var aim = c.Aim;
            double tx = aim.HasWallTarget && aim.TargetWall == 0 ? aim.WallTarget.X : aim.IntendedPoint.X;
            double ty = aim.HasWallTarget && aim.TargetWall == 0 ? aim.WallTarget.Y : aim.IntendedPoint.Y;
            tx = System.Math.Max(-HALF_W + 0.05, System.Math.Min(HALF_W - 0.05, tx));
            ty = System.Math.Max(CourtDimensions.TIN_H + 0.04, System.Math.Min(CourtDimensions.FRONT_WALL_OUT_H - 0.2, ty));
            SimEvent hit = c.Arcade.SpecialNow(c, Ball, Surfaces, tx, ty, SpecialPace);
            hit.PlayerIndex = i;
            hit.Time = Time;
            StepEvents.Add(hit);
            if (Referee.Shots > 0) RallyContacts[i]++;
            OnRacketContact(i, hit);
            mm.Power = 0;                                                     // (the stroke's own meter share: not for this one)
            SpecialBy = i; SpecialDefended = false; SpecialsFired++;
            var op = ModsOf(Players[1 - i].Player);
            op.ReachCut = SpecialReachCut; op.GraceCut = SpecialGraceCut;
            Note(i, "special", 0);
        }

        /// <summary>Focus against a Special Shot: FocusCost of the meter, the ball slowed, most of the cut lifted.</summary>
        void DefendSpecial(int j)
        {
            var mm = M(Players[j]);
            if (mm == null || !mm.SpendFocus()) return;
            _slowPending = true;                                              // slowed once it's off the front wall (SlowIfDue)
            SlowIfDue();
            mm.ReachCut = DefendedReachCut; mm.GraceCut = DefendedGraceCut;
            SpecialDefended = true; SpecialsDefended++;
            Note(j, "defend", 0);
        }

        /// <summary>A stroke by someone: a Special Shot in flight is over (the striker's own one has just begun).</summary>
        void SpecialAtContact(int i) { if (SpecialBy >= 0 && SpecialBy != i) { SpecialBy = -1; SpecialDefended = false; _slowPending = false; } }

        void SpecialReset() { ZoneBy = -1; SpecialBy = -1; SpecialDefended = false; _slowPending = false; }

        bool _slowPending;
        /// <summary>Focus slows a Special Shot coming back off the front wall (slowed on its way in, it would only die short).</summary>
        void SlowIfDue()
        {
            if (!_slowPending || Ball.Vel.Z <= 0) return;
            _slowPending = false;
            Ball.Vel.Set(Ball.Vel.X * FocusSlow, Ball.Vel.Y * FocusSlow, Ball.Vel.Z * FocusSlow);
            Ball.SpecialPace = false;
        }
    }
}
