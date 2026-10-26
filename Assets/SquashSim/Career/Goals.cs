// ===========================================================
// Assets/SquashSim/Career/Goals.cs
// P3-R4: what makes each season its own - three goals set from where
// you stand, sponsors who back you while you deliver, and the tour's
// storylines (news made from what really happened).
//
//   goals      at each season's start, from your ranking then:
//                rank    finish inside #(60% of your rank)
//                titles  win 1 (or 2 low down)
//                tier    reach a quarter-final one tier above the
//                        highest you can enter now
//                scalp   beat a player from a band above you
//              each pays training points and money when it is done
//              (the rank goal at the season's end)
//   sponsors   ranked players get two offers at the season's start:
//              a retainer paid at the season's end if you stay inside
//              the clause (else you are dropped), a bonus per title,
//              and the brand's kit. Fictional brands only.
//
// Deterministic from the career seed and the season, like the rest.
// ===========================================================

using System;
using System.Collections.Generic;

namespace SquashSim.Career
{
    [Serializable]
    public sealed class GoalRec
    {
        public string Kind;          // rank / titles / tier / scalp
        public int Target;           // rank: the place; titles: how many; tier: the Tier; scalp: the opponent's rank
        public string Text;
        public int Points, Money;
        public int Progress;
        public bool Done, Failed;
        public int DoneSlot = -1;
    }

    [Serializable]
    public sealed class SponsorDeal
    {
        public string Brand, ItemId;
        public int Retainer, TitleBonus, Clause;      // Clause: stay inside this ranking
        public int Season;
    }

    public sealed partial class CareerSession
    {
        // ---------------- goals ----------------

        /// <summary>This season's goals (set at its start).</summary>
        public List<GoalRec> Goals { get { if (S.Goals == null) S.Goals = new List<GoalRec>(); return S.Goals; } }

        static int Nice(int n) { if (n <= 10) return Math.Max(1, n); if (n <= 50) return (n + 4) / 5 * 5; if (n <= 200) return (n + 9) / 10 * 10; return (n + 24) / 25 * 25; }

        /// <summary>Set the coming season's goals from where you stand now.</summary>
        void SetGoals()
        {
            S.Goals = new List<GoalRec>();
            S.GoalsSeason = S.Season;
            int rank = RankOf(You);
            bool ranked = rank != Unranked;
            int scale = ranked ? rank : 450;
            // money scales with how far up you are: a few hundred at the bottom, tens of thousands at the top
            int money = (int)Math.Round(300 * Math.Pow(450.0 / Math.Max(1, scale), 0.75) / 50) * 50;

            int target = ranked ? Nice((int)Math.Ceiling(rank * 0.6)) : 400;
            if (ranked && rank <= 3) target = 1;
            S.Goals.Add(new GoalRec
            {
                Kind = "rank", Target = target, Points = 3, Money = money * 2,
                Text = target == 1 ? "Finish the season as world No. 1" : "Finish the season inside the top " + target,
            });

            int titles = !ranked || rank > 200 ? 2 : 1;
            S.Goals.Add(new GoalRec { Kind = "titles", Target = titles, Points = 2, Money = money, Text = titles == 1 ? "Win a title" : "Win " + titles + " titles" });

            // a quarter-final one tier above the best you can enter now
            Tier best = Tier.Challenger3;
            foreach (var info in Tour.Tiers)
                if (info.Tier != Tier.WorldTourFinals && rank <= Tour.TypicalLastIn(info.Tier) && info.Tier > best) best = info.Tier;     // P7: where your ranking gets you in
            Tier aim = best >= Tier.WorldChampionship ? Tier.WorldChampionship : best + 1;
            S.Goals.Add(new GoalRec { Kind = "tier", Target = (int)aim, Points = 2, Money = money, Text = "Reach a " + Tour.Info(aim).Name + " quarter-final" });

            int scalp = scale > 300 ? 200 : scale > 150 ? 100 : scale > 60 ? 40 : scale > 20 ? 10 : 3;
            S.Goals.Add(new GoalRec { Kind = "scalp", Target = scalp, Points = 2, Money = money, Text = scalp <= 3 ? "Beat a top-3 player" : "Beat a top-" + scalp + " player" });
            // three of the four: the rank goal always, two of the others by the season's seed
            var rng = new CareerRng(S.Seed, 7, S.Season);
            S.Goals.RemoveAt(1 + rng.Range(3));
        }

        void GoalDone(GoalRec g)
        {
            if (g.Done || g.Failed) return;
            g.Done = true; g.Progress = Math.Max(g.Progress, g.Kind == "titles" ? g.Target : 1);
            g.DoneSlot = S.Slot;
            GainXp(g.Points * GoalXpPerPoint);                          // P7: a goal's reward is XP now
            Earn(g.Money);
            S.GoalsDone++;
        }

        /// <summary>Your match is won: a scalp?</summary>
        void GoalsAfterWin(int oppRank)
        {
            foreach (var g in Goals) if (g.Kind == "scalp" && !g.Done && oppRank <= g.Target) GoalDone(g);
        }

        /// <summary>Your event is over: titles, a quarter-final at the tier.</summary>
        void GoalsAfterEvent(EventState e, int reached)
        {
            foreach (var g in Goals)
            {
                if (g.Done) continue;
                if (g.Kind == "titles" && reached == 0) { g.Progress++; if (g.Progress >= g.Target) GoalDone(g); }
                if (g.Kind == "tier" && e.Tier >= g.Target && reached <= 3) GoalDone(g);
            }
        }

        /// <summary>The season is over: the rank goal, and whatever was not done has failed.</summary>
        void SettleGoals(int finalRank)
        {
            foreach (var g in Goals)
            {
                if (g.Kind == "rank" && !g.Done && finalRank <= g.Target) GoalDone(g);
                if (!g.Done) g.Failed = true;
            }
            if (S.LastGoals == null) S.LastGoals = new List<GoalRec>();
            S.LastGoals = new List<GoalRec>(Goals);
        }

        public int GoalsDoneNow { get { int n = 0; foreach (var g in Goals) if (g.Done) n++; return n; } }

        // ---------------- sponsors ----------------

        public static readonly (string brand, string item)[] Brands =
        {
            ("Kestrel Sportswear", "sp_kestrel"), ("Halden Rackets", "sp_halden"), ("Northline Energy", "sp_northline"), ("Bluecrest Bank", "sp_bluecrest"),
            ("Vireo Water", "sp_vireo"), ("Marrow & Finch", "sp_marrow"), ("Tarn Watches", "sp_tarn"), ("Ostra Air", "sp_ostra"),
            ("Copperleaf Tea", "sp_copperleaf"), ("Pallas Motors", "sp_pallas"),
        };

        /// <summary>The offers for the coming season: two, if you are ranked.</summary>
        void MakeOffers()
        {
            S.Offers = new List<SponsorDeal>();
            S.OffersSeason = S.Season;
            int rank = RankOf(You);
            if (rank == Unranked) return;
            var rng = new CareerRng(S.Seed, 11, S.Season);
            double strength = Math.Pow(450.0 / Math.Max(1, rank), 0.85);          // 1 at the bottom, ~180 at No. 1 (about $110k a season)
            int a = rng.Range(Brands.Length), b = (a + 1 + rng.Range(Brands.Length - 1)) % Brands.Length;
            foreach (int k in new[] { a, b })
            {
                bool safe = S.Offers.Count == 0;                                      // one safe, one bolder deal
                int retainer = (int)Math.Round(600 * strength * (safe ? 1.0 : 1.6) / 100) * 100;
                int clause = safe ? Nice((int)(rank * 1.5) + 30) : Nice((int)(rank * 1.05) + 8);
                S.Offers.Add(new SponsorDeal
                {
                    Brand = Brands[k].brand, ItemId = Brands[k].item, Season = S.Season,
                    Retainer = Math.Max(500, retainer), TitleBonus = Math.Max(200, (int)Math.Round(retainer * 0.25 / 50) * 50), Clause = Math.Min(clause, 480),
                });
            }
        }

        /// <summary>Sign one of the offers (or none: index -1).</summary>
        public SponsorDeal Sign(int index)
        {
            var offers = S.Offers ?? new List<SponsorDeal>();
            S.Sponsor = index >= 0 && index < offers.Count ? offers[index] : null;
            S.Offers = new List<SponsorDeal>();
            return S.Sponsor;
        }

        public bool OffersWaiting { get { return S.Offers != null && S.Offers.Count > 0; } }

        void SponsorTitle() { if (S.Sponsor != null && !string.IsNullOrEmpty(S.Sponsor.Brand)) { Earn(S.Sponsor.TitleBonus); S.SponsorPaid += S.Sponsor.TitleBonus; } }

        /// <summary>The season's end: the retainer if you kept the clause, else the deal ends.</summary>
        void SettleSponsor(int finalRank)
        {
            var d = S.Sponsor;
            S.SponsorNews = null;
            if (d == null || string.IsNullOrEmpty(d.Brand)) return;
            if (finalRank <= d.Clause)
            {
                Earn(d.Retainer); S.SponsorPaid += d.Retainer;
                S.SponsorNews = d.Brand + " pays your $" + d.Retainer.ToString("N0") + " retainer";
            }
            else
            {
                S.SponsorNews = d.Brand + " drops you: you finished outside the top " + d.Clause;
                S.Sponsor = null;
            }
        }

        // ---------------- storylines ----------------

        /// <summary>The strongest pro who retired at the end of last season, or -1.</summary>
        public int LatestRetiree()
        {
            int best = -1; double s = -1;
            foreach (var p in S.Roster) if (p.Retired && p.RetiredSeason == S.Season - 1 && p.Strength > s) { s = p.Strength; best = p.Id; }
            return best;
        }

        /// <summary>Last month's upset: a winner of a Copper-or-bigger event ranked 60+ places below the seeds. -1 if none.</summary>
        public int UpsetWinner(out int rankThen, out int tier)
        {
            rankThen = 0; tier = -1;
            int prev = S.Slot - 1, who = -1;
            var before = RankingAt(prev);
            var rankThenOf = new Dictionary<int, int>();
            foreach (var r in before) rankThenOf[r.Id] = r.Rank;
            foreach (var r in S.Results)
            {
                if (r.Slot != prev || r.Reached != 0 || r.Player == You || r.Tier < (int)Tier.Copper) continue;
                int rk = rankThenOf.TryGetValue(r.Player, out int x) ? x : Unranked;
                if (rk >= 60 && rk > rankThen) { rankThen = rk; who = r.Player; tier = r.Tier; }
            }
            return who;
        }
    }
}
