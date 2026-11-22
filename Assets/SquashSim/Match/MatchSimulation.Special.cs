// ===========================================================
// Assets/SquashSim/Match/MatchSimulation.Special.cs
// 2026-11-22 the Special Shot (the user, after Mario Tennis: "like
// sniping ... teleporting to the ball, the screen changes into an aiming
// screen, the meter slowly goes down because you're in slow mo aiming ...
// make the shot insanely powerful ... if you use focus the ball gets
// slower and can be got back easier"):
//
//   start     a full meter (the Special card), the ball coming to you:
//             press Special. (2026-11-22, the user: "teleports you high up
//             in the middle of the court") You jump up above the T, the
//             ball pulled up to your racket; time stands still
//   wind-up   (the user: "a 0.5 sec swinging animation and a little spark
//             so your opponent knows") Hit starts it: WindupSeconds, the
//             world moving again - the opponent sees it coming (the game
//             shows the spark and where it's aimed) - then the strike, and
//             you drop back to the floor
//   aim       move the aim (the ring) anywhere on the front wall - a nick,
//             the tin's edge; the meter drains while you aim
//             (ZoneSeconds from full); Hit fires, an empty meter fires
//   the shot  SpecialPace, straight at the aim; the opponent's reach and
//             timing cut hard (SpecialReachCut, SpecialGraceCut) until
//             their next stroke - almost unreturnable
//   focus     (2026-11-22, its own key, held) any time your opponent's shot
//             is off the front wall and coming to you: slow motion (the
//             game's clock) while the meter drains (FocusDrainSeconds from
//             full). Against a Special Shot: you move FocusSpeed times as
//             fast, with more reach and timing - you can get it back
//   special   (its own key: Q / D-pad up) any time in that same window
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
        /// <summary>The jump: this high above the T (feet), the ball at the racket above and beside; the wind-up's length; the fall.</summary>
        public const double AirHeight = 1.4, WindupSeconds = 0.5, FallSpeed = 6.0;
        /// <summary>Who is winding up a Special Shot (-1: nobody), for how long; where it's going (the front wall: x, y - locked at the
        /// press, or the aim while aiming).</summary>
        public int WindupBy { get; private set; } = -1;
        public double WindupTime { get; private set; }
        public double SpecialAimX { get; private set; }
        public double SpecialAimY { get; private set; }
        int _landBy = -1;
        /// <summary>The athlete in the air now (aiming, winding up, or falling back), and how high.</summary>
        public bool InAir(int i) { return i >= 0 && i < 2 && Players[i].Player.Pos.Y > 0.001; }
        /// <summary>Focus against a Special Shot is time slowed for the defender, not a slower ball (a slowed low kill would only die
        /// shorter): they move FocusSpeed times as fast, with more reach and a wider timing window (negative cuts) until they play it.</summary>
        public const double FocusSpeed = 1.5, DefendedReachCut = 0.6, DefendedGraceCut = 0.7;
        /// <summary>Seconds of (sim) time a full meter lasts while focusing - about four seconds on the slowed clock.</summary>
        public const double FocusDrainSeconds = 1.5;
        readonly bool[] _focusing = new bool[2];
        /// <summary>Player i is focusing now (the game slows its clock for a local one).</summary>
        public bool Focusing(int i) { return i >= 0 && i < 2 && _focusing[i]; }

        /// <summary>The window for Focus and the Special Shot: the opponent's shot has hit the front wall and it's coming to player i.</summary>
        public bool BallComing(int i)
        {
            return Phase == MatchPhase.Rally && Referee.Outcome == null && Referee.Shots > 0 && Referee.ExpectedStriker == i &&
                   Referee.LastHitter == 1 - i && !Ball.Asleep && Ball.FloorSinceFrontWall < 2;
        }

        /// <summary>Player i may focus: the Special card, some meter, the ball coming.</summary>
        public bool CanFocus(int i)
        {
            var mm = M(Players[i]);
            return mm != null && mm.Level("special") > 0 && mm.Power > 0 && ZoneBy < 0 && WindupBy < 0 && BallComing(i);
        }

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
            if (Phase != MatchPhase.Rally || Referee.Outcome != null || Referee.Shots == 0 || ZoneBy >= 0 || WindupBy >= 0) return false;
            if (SpecialBy == 1 - i) return false;                                         // (a Special Shot coming: defend it, no counter)
            return BallComing(i);                                                          // 2026-11-22: any time it's off the wall
        }

        /// <summary>A Special Shot is coming at player j, not yet slowed: Focus defends it.</summary>
        public bool SpecialIncoming(int j) { return SpecialBy == 1 - j && !SpecialDefended && Phase == MatchPhase.Rally && Referee.Outcome == null; }

        /// <summary>Each step, before anyone moves: the Special presses (start, defend), the aiming. True: frozen this step.</summary>
        bool SpecialStep(double dt)
        {
            for (int i = 0; i < 2; i++)
            {
                if (!Intents[i].SpecialPressed) continue;
                Intents[i].SpecialPressed = false;
                if (CanSpecial(i)) StartSpecial(i);
            }
            // Focus, held: the meter drains; against a Special Shot, it slows it (once)
            for (int i = 0; i < 2; i++)
            {
                bool want = Intents[i].FocusHeld && CanFocus(i);
                if (want && SpecialIncoming(i)) DefendSpecial(i);
                var fm = M(Players[i]);
                if (want) fm.Power = System.Math.Max(0, fm.Power - dt / FocusDrainSeconds);
                if (fm != null) fm.FocusBoost = want && SpecialDefended && SpecialBy == 1 - i ? FocusSpeed : 1;
                if (want && !_focusing[i]) Note(i, "focus", 0);
                _focusing[i] = want;
            }
            if (WindupBy >= 0)
            {
                // the wind-up: the world moves, the striker and the ball hang in the air; then the strike
                WindupTime += dt;
                PinWindup();
                var wi = Intents[WindupBy];
                wi.Move.X = 0; wi.Move.Z = 0; wi.MoveMagnitude = 0; wi.Hit.Pressed = false; wi.Touch.Pressed = false; wi.DivePressed = false;
                if (WindupTime >= WindupSeconds) FireSpecial(WindupBy);
                return false;
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
            AimNow(c, out double ax, out double ay); SpecialAimX = ax; SpecialAimY = ay;
            bool fire = (Intents[z].Hit.Pressed || Intents[z].Touch.Pressed) && ZoneTime > 0.15;
            if (fire || mm.Power <= 0) { StartWindup(z); Intents[0].ClearEdges(); Intents[1].ClearEdges(); return true; }
            Intents[0].ClearEdges(); Intents[1].ClearEdges();
            return true;
        }

        static void AimNow(TestPlayerController c, out double tx, out double ty)
        {
            var aim = c.Aim;
            tx = aim.HasWallTarget && aim.TargetWall == 0 ? aim.WallTarget.X : aim.IntendedPoint.X;
            ty = aim.HasWallTarget && aim.TargetWall == 0 ? aim.WallTarget.Y : aim.IntendedPoint.Y;
            tx = System.Math.Max(-HALF_W + 0.05, System.Math.Min(HALF_W - 0.05, tx));
            ty = System.Math.Max(CourtDimensions.TIN_H + 0.04, System.Math.Min(CourtDimensions.FRONT_WALL_OUT_H - 0.2, ty));
        }

        /// <summary>Hit pressed while aiming: the aim locked, the meter spent, the wind-up begins (the world moves again).</summary>
        void StartWindup(int z)
        {
            ZoneBy = -1;
            WindupBy = z; WindupTime = 0;
            M(Players[z]).Power = 0;
            Note(z, "windup", 0);
            PinWindup();
        }

        /// <summary>The striker in the air above the T and the ball at their racket, held there; the swing drawn back.</summary>
        void PinWindup()
        {
            var c = Players[WindupBy];
            Ball.Pos.CopyFrom(_zoneBall); Ball.Vel.Set(0, 0, 0);
            c.Player.Pos.Set(_airAt.X, AirHeight, _airAt.Z); c.Player.Vel.Set(0, 0, 0);
            var sw = c.Swing;
            sw.State = SwingStates.Preparing; sw.Stroke = "hit"; sw.Mode = "prepared"; sw.HoldSeconds = WindupTime;
        }

        /// <summary>The end of each step: the wind-up held through the step's movement; a striker back from the air falls to the floor.</summary>
        void SpecialEndStep(double dt)
        {
            if (WindupBy >= 0) PinWindup();
            if (_landBy >= 0)
            {
                var p = Players[_landBy].Player;
                p.Pos.Y = System.Math.Max(0, p.Pos.Y - FallSpeed * dt);
                if (p.Pos.Y <= 0) { p.Pos.Y = 0; _landBy = -1; }
            }
        }
        readonly V3 _airAt = new V3();

        void StartSpecial(int i)
        {
            var c = Players[i];
            var pr = c.Prediction;
            ZoneBy = i; ZoneTime = 0;
            _focusing[0] = _focusing[1] = false;
            // the jump: up above the T, the ball pulled to the racket (beside and above the head)
            double sp = PhysicsConfig.Current.MovementAssist.PreferredSpacing;
            double x = 0, zz = SHORT_LINE_Z + 0.4;
            _airAt.Set(x, 0, zz);
            _zoneBall.Set(x + System.Math.Max(0.75, sp), AirHeight + 1.5, zz - 0.3);
            c.Player.Pos.Set(x, AirHeight, zz);
            c.Player.Vel.Set(0, 0, 0);
            c.Racket.MarkTeleported();
            // the opponent never stands in the way of it: moved clear (towards the T side away from you), and no call carried over
            var o = Players[1 - i].Player;
            double dx = o.Pos.X - x, dz = o.Pos.Z - zz;
            if (dx * dx + dz * dz < 2.0 * 2.0)
            {
                double ox = o.Pos.X >= 0 ? 1.9 : -1.9, oz = System.Math.Max(3.0, System.Math.Min(CourtDimensions.LENGTH - 1.0, zz + 1.8));
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
            ZoneBy = -1; WindupBy = -1;
            mm.Power = 0;
            double tx = SpecialAimX, ty = SpecialAimY;
            Ball.Pos.CopyFrom(_zoneBall);
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
            _landBy = i;                                                      // back down to the floor
        }

        /// <summary>Focus against a Special Shot (the meter drains as for any Focus): the ball slowed, most of the cut lifted.</summary>
        void DefendSpecial(int j)
        {
            var mm = M(Players[j]);
            if (mm == null) return;
            mm.ReachCut = DefendedReachCut; mm.GraceCut = DefendedGraceCut;
            SpecialDefended = true; SpecialsDefended++;
            Note(j, "defend", 0);
        }

        /// <summary>A stroke by someone: a Special Shot in flight is over (the striker's own one has just begun).</summary>
        void SpecialAtContact(int i) { if (SpecialBy >= 0 && SpecialBy != i) { SpecialBy = -1; SpecialDefended = false; } }

        void SpecialReset()
        {
            ZoneBy = -1; SpecialBy = -1; SpecialDefended = false; _focusing[0] = _focusing[1] = false;
            WindupBy = -1; _landBy = -1;
            for (int i = 0; i < 2; i++) Players[i].Player.Pos.Y = 0;
        }

    }
}
