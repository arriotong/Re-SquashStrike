// ===========================================================
// Assets/SquashSim/Career/SeasonRunner.cs
// P2-M4: a career played with no one at the controls - for the tests
// and MatchBench. Each month "you" enter the highest event your ranking
// opens and play your matches through a callback (the rally model at a
// fixed true strength in the tests; real sim matches with the human-
// style model in MatchBench --season).
// ===========================================================

using System;

namespace SquashSim.Career
{
    public static class SeasonRunner
    {
        /// <summary>Your match against a pro: return won, the score, rallies won / played.</summary>
        public delegate bool PlayMatch(CareerSession c, Pro opp, out string score, out int ralliesWon, out int ralliesTotal);

        /// <summary>Months until the season's end (or count months), entering the best event each time.
        /// <paramref name="spendPoints"/>: spend training points as they come, round the attributes (as a player would).</summary>
        public static void Months(CareerSession c, int months, PlayMatch play, Action<CareerSession, EventState> afterEvent = null, bool spendPoints = false)
        {
            int next = 0;
            for (int k = 0; k < months; k++)
            {
                if (spendPoints)
                    for (int guard = 0; c.S.TrainingPoints > 0 && guard < 16; guard++)
                        c.Spend(CareerSession.AttributeNames[next++ % CareerSession.AttributeNames.Length]);
                Tier best = (Tier)(-1);
                foreach (var t in c.EventsNow()) if (c.CanEnter(t) && (int)t > (int)best) best = t;
                if ((int)best < 0) { c.Skip(); afterEvent?.Invoke(c, null); continue; }
                c.Enter(best);
                int opp;
                while ((opp = c.NextOpponent()) >= 0)
                {
                    bool won = play(c, c.Pro(opp), out string score, out int w, out int n);
                    c.RecordYours(won, score, w, n);
                }
                c.FinishSlot();
                afterEvent?.Invoke(c, c.S.Last);
            }
        }

        /// <summary>The rally model at a fixed true strength (your rating still learns from it).</summary>
        public static PlayMatch Model(double trueStrength)
        {
            return (CareerSession c, Pro opp, out string score, out int w, out int n) =>
            {
                var rng = new CareerRng(c.S.Seed ^ 0xBEEFu, c.S.Slot, c.PlayersLeft);
                var r = RallyModel.Play(RallyModel.YouRally(trueStrength, opp.Strength), c.S.GamesToWin, c.S.English, rng);
                score = r.Score; w = r.RalliesA; n = r.RalliesA + r.RalliesB;
                return r.AWon;
            };
        }
    }
}
