// ===========================================================
// Assets/SquashSim/Academy/InterferenceStage.cs
// P2-M7: the Academy's "Lets & strokes" situations, played for real.
// A staged moment in a live rally (MatchSimulation.StageRally, as the
// referee's scenario tests): the opponent has just driven the ball down
// the left wall, the striker goes for it, the opponent stands, clears or
// is clear, and the striker asks as the ball arrives. The lesson shows
// it (the session mirrors the two athletes and the ball), and the quiz's
// right answer is the real referee's call on it - so a question can
// never be marked wrongly.
// ===========================================================

using SquashSim.Replays;

namespace SquashSim.Academy
{
    public sealed class InterferenceStage
    {
        public static readonly V3 From = new V3(-1.0, 1.0, 8.0), Drive = new V3(0.0, 7.0, -16.0);

        /// <summary>Where the striker (athlete 0) and the opponent (athlete 1) start, from the hitting point.</summary>
        public double Sx, Sz, Ox, Oz;
        /// <summary>The striker goes for the ball (else stands: not trying).</summary>
        public bool Runs = true;
        /// <summary>The opponent moves off, slowly (else stands).</summary>
        public bool Clears;
        /// <summary>The striker asks this long before the ball arrives.</summary>
        public double AskAt = 0.3;

        public MatchSimulation Sim { get; private set; }
        /// <summary>The hitting point (where the striker is going).</summary>
        public double TargetX { get; private set; }
        public double TargetZ { get; private set; }

        public InterferenceStage Clone() { return (InterferenceStage)MemberwiseClone(); }

        public static InterferenceStage InTheWay() { return new InterferenceStage { Sx = 3.0, Sz = 0.2, Ox = 1.5, Oz = 0.25 }; }
        public static InterferenceStage Clearing() { return new InterferenceStage { Sx = 3.0, Sz = 0.2, Ox = 1.5, Oz = 0.25, Clears = true, AskAt = 0.45 }; }
        public static InterferenceStage InTheLine() { return new InterferenceStage { Sx = 1.2, Sz = 1.2, Ox = 0, Oz = -1.4 }; }
        public static InterferenceStage InTheSwing() { return new InterferenceStage { Sx = 1.6, Sz = 0.6, Ox = -0.65, Oz = 0.3 }; }
        public static InterferenceStage NotTrying() { return new InterferenceStage { Sx = 3.0, Sz = 0.2, Ox = 1.5, Oz = 0.25, Runs = false }; }
        public static InterferenceStage OpponentClear() { return new InterferenceStage { Sx = 1.8, Sz = -2.2, Ox = 3.4, Oz = -1.4 }; }

        static MatchSimulation NewSim() { return ReplayMatch.Build(new ReplaySetup { TwoPlayer = true, GamesToWin = 1 }); }

        /// <summary>Set the moment up (again).</summary>
        public void Begin()
        {
            var m = NewSim();
            m.StageRally(From, Drive, 0, 5.5, 2.4, 2.5);
            var rd = new BallReader();
            if (rd.Read(m.Ball, m.Surfaces, m.Players[0].Player, true)) { TargetX = rd.X; TargetZ = rd.Z; }
            else { TargetX = -1.0; TargetZ = 3.9; }
            Sim = NewSim();
            Sim.StageRally(From, Drive, TargetX + Sx, TargetZ + Sz, TargetX + Ox, TargetZ + Oz);
        }

        /// <summary>The striker is about to ask (the quiz stops here and asks you).</summary>
        public bool AboutToAsk
        {
            get { var it = Sim.Interference; return it.Striker == 0 && it.TimeToBall > 0 && it.TimeToBall <= AskAt; }
        }

        /// <summary>One fixed step of the moment; <paramref name="ask"/>: the striker appeals when it is time.
        /// False once the rally is over.</summary>
        public bool Step(double dt, bool ask)
        {
            if (Sim.Referee.Outcome != null) return false;
            var match = PhysicsConfig.Current.Match;
            bool keep = match.AppealsEnabled;
            match.AppealsEnabled = true;                     // the lesson is about them, whatever the Lets setting
            var me = Sim.Players[0].Player; var a = Sim.Intents[0]; var b = Sim.Intents[1];
            double tx = Sim.Interference.Striker == 0 ? Sim.Interference.TargetX : TargetX, tz = Sim.Interference.Striker == 0 ? Sim.Interference.TargetZ : TargetZ;
            double dx = tx - me.Pos.X, dz = tz - me.Pos.Z, d = System.Math.Sqrt(dx * dx + dz * dz);
            if (Runs && d > 0.5) { a.Move.X = dx / d; a.Move.Z = dz / d; a.MoveMagnitude = 1; } else { a.Move.X = 0; a.Move.Z = 0; a.MoveMagnitude = 0; }
            b.Move.X = 0; b.Move.Z = Clears ? 1 : 0; b.MoveMagnitude = Clears ? 0.35 : 0;
            if (ask && AboutToAsk) a.AppealPressed = true;
            Sim.Step(dt);
            match.AppealsEnabled = keep;
            return Sim.Referee.Outcome == null;
        }

        /// <summary>The referee's call on this moment, played out: "Let", "No let" or "Stroke" (null: none).</summary>
        public string Call()
        {
            var s = Clone();
            s.Begin();
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            for (int i = 0; i < 240 * 3 && s.Step(dt, true); i++) { }
            var o = s.Sim.Referee.Outcome;
            if (o == null) return null;
            switch (o.Kind)
            {
                case RallyEnd.Let: return "Let";
                case RallyEnd.NoLet: return "No let";
                case RallyEnd.Stroke: return "Stroke";
                default: return null;
            }
        }
    }
}
