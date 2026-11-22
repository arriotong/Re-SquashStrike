// ===========================================================
// Assets/SquashSim.Testing/Editor/ServeFaultTests.cs
// 2026-11-22 (the user: "bots serve fault too much"): the computer's serve
// (the arcade game) is in - at every level and style, a fault is rare.
// The faults were hard serves aimed near the middle: off the front wall too
// steeply, off the far side wall, back down on the server's own side ("wrong
// side of the half-court line"). Quick Match's Abilities (the Special card
// changes the AI's dice) made them show. The hard serve now aims from where
// it's struck to the receiver's back corner.
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;

namespace SquashSim.Tests
{
    public class ServeFaultTests
    {
        [SetUp] public void Defaults() { PhysicsConfig.UseGameProfile(); }
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }
        static double Dt { get { return PhysicsConfig.Current.Sim.FixedDt; } }

        static (int serves, int faults, string why) Serves(string style, double difficulty, uint seed, int rallies)
        {
            var c = PhysicsConfig.Current.Match;
            c.WarmUpEnabled = false; c.GamesToWin = 3;
            var m = new MatchSimulation(0);
            m.Bots[0] = new OpponentBrain(AiProfile.For(style, difficulty), seed);
            m.Bots[1] = new OpponentBrain(AiProfile.For(style, difficulty), seed + 1);
            for (int i = 0; i < 2; i++) { m.Players[i].ArcadeHitEnabled = true; m.Players[i].WallAimEnabled = true; }
            return Count(m, -1, rallies);
        }

        /// <summary>Rallies served by <paramref name="server"/> (-1: either) and the faults among them, with their reasons.</summary>
        static (int serves, int faults, string why) Count(MatchSimulation m, int server, int rallies)
        {
            int serves = 0, faults = 0;
            var why = new Dictionary<string, int>();
            while (m.History.Count < rallies && m.Phase != MatchPhase.MatchOver && m.Time < 7200)
            {
                int before = m.History.Count, by = m.Score.Server;
                m.Step(Dt);
                if (m.Phase == MatchPhase.GameOver) m.NextGame();
                if (m.History.Count == before || (server >= 0 && by != server)) continue;
                serves++;
                var o = m.History[m.History.Count - 1];
                if (o.Kind == RallyEnd.Fault || o.Kind == RallyEnd.FootFault)
                {
                    faults++;
                    string r = o.Reason ?? o.Kind.ToString();
                    why[r] = why.TryGetValue(r, out int n) ? n + 1 : 1;
                }
            }
            var parts = new List<string>();
            foreach (var kv in why) parts.Add(kv.Key + " x" + kv.Value);
            return (serves, faults, string.Join(", ", parts));
        }

        static readonly string[] Kit = { "ace", "special" };   // Quick Match's Abilities kit for the computer

        /// <summary>As Quick Match builds it (ReplayMatch: the star's athlete, signature shots, the Abilities kit); the computer's serves.</summary>
        static (int serves, int faults, string why) GameServes(string opp, float diff, uint seed, bool abilities, int rallies)
        {
            var setup = new SquashSim.Replays.ReplaySetup { Opponent = opp, Difficulty = diff, Scoring = "par", GamesToWin = 3, BotSeed = seed, Opponent0 = "retriever", Difficulty0 = 0.6f, Lets = true };
            if (abilities)
            {
                setup.Mods1 = new List<RunMod>();
                foreach (var k in Kit) setup.Mods1.Add(new RunMod { Id = k, Level = 1 });
                var star = SquashSim.Career.Stars.Find(opp);
                if (star != null) setup.Mods1.AddRange(star.Signature);
            }
            SquashSim.Replays.ReplayMatch.Configure(setup);
            var m = SquashSim.Replays.ReplayMatch.Build(setup);
            for (int i = 0; i < 2; i++) { m.Players[i].ArcadeHitEnabled = true; m.Players[i].WallAimEnabled = true; }
            var r = Count(m, 1, rallies);
            PhysicsConfig.UseGameProfile();
            return r;
        }

        [Test] public void AStarRarelyFaultsItsServeInAQuickMatch()
        {
            int all = 0, bad = 0;
            foreach (var opp in new[] { "star:s01", "star:s04", "star:s20", "star:s45", "attacker" })
                foreach (var d in new[] { 0.4f, 1f })
                    foreach (var ab in new[] { false, true })
                    {
                        var r = GameServes(opp, d, (uint)(31 + (int)(d * 10)), ab, 50);
                        all += r.serves; bad += r.faults;
                        UnityEngine.Debug.Log("[GameServe] " + opp + " " + d + (ab ? " abilities" : "") + ": " + r.faults + " faults in " + r.serves + " serves  " + r.why);
                    }
            double rate = all == 0 ? 0 : (double)bad / all;
            UnityEngine.Debug.Log("[GameServe] all: " + bad + " / " + all + " = " + rate.ToString("0.000"));
            Assert.That(rate, Is.LessThan(0.01), "fewer than 1 serve in 100 a fault");
        }

        /// <summary>2026-11-22 evening (the user: "still serves out pretty frequently"; their replay: a hard serve off the front wall
        /// at twice the mirror's angle, the far side wall, back down on the server's half): the hard serve lands well over the half
        /// line, and the cuts a Special Shot put on the receiver are gone by the next serve.</summary>
        [Test] public void HardServesLandWellOverTheLineAndNoCutOutlivesItsRally()
        {
            int hard = 0, side = 0, awaits = 0, landed = 0; bool watching = false; double worst = 9;
            for (uint seed = 77; seed < 77 + 6 * 13 && landed < 5; seed += 13)
            {
            var setup = new SquashSim.Replays.ReplaySetup { Opponent = "star:s01", Difficulty = 1f, Scoring = "par", GamesToWin = 3, BotSeed = seed, Opponent0 = "retriever", Difficulty0 = 0.2f, Lets = true };
            setup.Mods0 = new List<RunMod>(); setup.Mods1 = new List<RunMod>();
            foreach (var k in Kit) { setup.Mods0.Add(new RunMod { Id = k, Level = 1 }); setup.Mods1.Add(new RunMod { Id = k, Level = 1 }); }
            SquashSim.Replays.ReplayMatch.Configure(setup);
            var m = SquashSim.Replays.ReplayMatch.Build(setup);
            for (int i = 0; i < 2; i++) { m.Players[i].ArcadeHitEnabled = true; m.Players[i].WallAimEnabled = true; }
            // the receiver: every other game, a player who lets the serve land (an AI volleys every hard one)
            var keep = m.Bots[0];
            watching = false;
            while (m.Phase != MatchPhase.MatchOver && m.Time < 3600)
            {
                var before = m.Phase;
                m.Step(Dt);
                if (m.Phase == MatchPhase.GameOver) m.NextGame();
                if (m.Phase == MatchPhase.AwaitToss && before != MatchPhase.AwaitToss)
                {
                    awaits++;
                    bool still = m.Score.Server == 1 && (m.Score.Games[0] + m.Score.Games[1]) % 2 == 1;
                    m.Bots[0] = still ? null : keep;
                    if (still) m.Intents[0] = new InputIntent();
                    for (int i = 0; i < 2; i++)
                    {
                        var mm = m.Players[i].Player.Mods;
                        Assert.That(mm.ReachCut, Is.EqualTo(0), "no reach cut carried into the serve");
                        Assert.That(mm.GraceCut, Is.EqualTo(0), "no timing cut carried into the serve");
                    }
                }
                foreach (var ev in m.StepEvents)
                {
                    if (ev.Type == SimEventType.RacketContact && m.Referee.Shots == 1 && V3.Length(m.Ball.Vel) > 30) { hard++; watching = true; side = m.Score.ServeSide; }
                    if (watching && m.Referee.Shots > 1) watching = false;
                    if (watching && ev.Type == SimEventType.Impact && ev.SurfaceId == "floor") { watching = false; landed++; worst = System.Math.Min(worst, -side * ev.Point.X); }
                }
            }
            PhysicsConfig.UseGameProfile();
            }
            UnityEngine.Debug.Log("[HardServe] " + hard + " hard serves, " + landed + " landed, the closest " + worst.ToString("0.00") + " m over the half line; " + awaits + " serves");
            Assert.That(landed, Is.GreaterThanOrEqualTo(3), "some hard serves landed");
            Assert.That(worst, Is.GreaterThan(1.2), "every hard serve lands well over the half line");
        }

        [Test] public void TheComputerRarelyFaultsItsServe()
        {
            int all = 0, bad = 0;
            foreach (var style in new[] { "attacker", "retriever", "star:s01", "star:s04", "star:s20", "star:s45" })
                foreach (var d in new[] { 0.0, 0.4, 1.0 })
                {
                    var r = Serves(style, d, (uint)(7 + (int)(d * 10)), 60);
                    all += r.serves; bad += r.faults;
                    UnityEngine.Debug.Log("[Serve] " + style + " " + d.ToString("0.0") + ": " + r.faults + " faults in " + r.serves + " rallies  " + r.why);
                }
            double rate = all == 0 ? 0 : (double)bad / all;
            UnityEngine.Debug.Log("[Serve] all: " + bad + " / " + all + " = " + rate.ToString("0.000"));
            Assert.That(rate, Is.LessThan(0.01), "fewer than 1 serve in 100 a fault");
        }
    }
}
