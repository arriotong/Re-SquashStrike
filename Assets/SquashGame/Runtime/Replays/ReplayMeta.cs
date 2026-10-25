// ===========================================================
// Assets/SquashGame/Runtime/Replays/ReplayMeta.cs
// P2-M6: what a replay's library card and theatre show that the sim
// does not need - the names on the score bug, the mode, the opponent's
// label, the career event and round, both kits as they were worn.
// Filled when a recording ends (MatchRunner.DescribeReplay).
// ===========================================================

using SquashGame.Profile;
using SquashGame.Progression;
using SquashSim.Replays;
using UnityEngine;

namespace SquashGame.Replays
{
    public static class ReplayMeta
    {
        public const string Mode = "mode", P1 = "p1", P2 = "p2", Opponent = "opponent", Event = "event", Round = "round", Career = "career", Kit0 = "kit0", Kit1 = "kit1";

        public static void Hook() { MatchRunner.DescribeReplay = Describe; }

        public static void Describe(ReplayFile f, MatchRunner r)
        {
            var s = r.Settings;
            f.Meta[Mode] = s.Online ? (s.Ranked ? "ranked" : "online") : s.Career ? "career" : s.TwoPlayer ? "versus" : "quick";
            f.Meta["you"] = r.HumanIndex.ToString();                        // P2-M8: online you may be athlete 1
            f.Meta[P1] = r.Names[0];
            f.Meta[P2] = r.Names[1];
            if (s.Online) f.Meta[Opponent] = string.IsNullOrEmpty(s.P2Name) ? "Online" : s.P2Name;
            else if (s.TwoPlayer) f.Meta[Opponent] = "Player 2";
            else if (s.Opponent == "wallbot") f.Meta[Opponent] = "Wall-bot";
            else
            {
                int d = s.Difficulty < 0.34f ? 0 : s.Difficulty < 0.67f ? 1 : 2;
                f.Meta[Opponent] = SquashSim.Career.Roster.StyleName(s.Opponent) + " (" + UI.MatchChoices.DifficultyLabels[d] + ")";
            }
            var c = CareerStore.Current;
            if (s.Career && c != null)
            {
                f.Meta[Career] = c.S.Seed.ToString();
                if (c.S.Current != null) { f.Meta[Event] = c.S.Current.Name; f.Meta[Round] = SquashSim.Career.Tour.RoundName(c.PlayersLeft); }
                int opp = c.NextOpponent();
                if (opp >= 0) f.Meta[Opponent] = c.NameOf(opp);
            }
            f.Meta[Kit0] = JsonUtility.ToJson(KitDirector.For(r, 0));
            f.Meta[Kit1] = JsonUtility.ToJson(KitDirector.For(r, 1));
        }

        /// <summary>A kit as it was worn in the replay (null: not recorded).</summary>
        public static Loadout Kit(ReplayFile f, int i)
        {
            string json = f.Get(i == 0 ? Kit0 : Kit1, null);
            if (string.IsNullOrEmpty(json))
            {
                string sig = f.Get(i == 0 ? "sig0" : "sig1", null);          // a demo match: the personalities' own kits
                return string.IsNullOrEmpty(sig) ? null : Cosmetics.Signature(sig);
            }
            try { return JsonUtility.FromJson<Loadout>(json); } catch { return null; }
        }
    }
}
