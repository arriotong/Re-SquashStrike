// ===========================================================
// Assets/SquashGame/Runtime/Progression/ArcadePlatinum.cs
// 2026-11-25 (the user: "work on the thin ones"): Target Arcade past its
// 54 stars - a PLATINUM score on every level (40% past 3 stars), coins the
// first time, and a title (Arcade Royalty) and a bonus for all of them; the
// results show your five best.
// ===========================================================

using SquashGame.Profile;
using SquashSim.Arcade;

namespace SquashGame.Progression
{
    public static class ArcadePlatinum
    {
        public const string Title = "title_arcadeplat";
        public const int Coins = 200, AllCoins = 3000;

        static System.Collections.Generic.IEnumerable<Challenge> Levels()
        {
            foreach (var c in SquashSim.Arcade.Challenges.All()) if (c.Mode != ArcadeMode.Drill && c.Platinum > 0) yield return c;
        }

        public static int Total { get { int n = 0; foreach (var _ in Levels()) n++; return n; } }
        public static int Count { get { int n = 0; foreach (var c in Levels()) { var r = ProfileStore.Record(c.Id); if (r != null && r.Platinum) n++; } return n; } }
        public static bool Has(string id) { var r = ProfileStore.Record(id); return r != null && r.Platinum; }

        /// <summary>A play's score: platinum reached for the first time (coins paid), and whether that made every level platinum
        /// (the title and the bonus). Saves.</summary>
        public static bool Check(Challenge c, int score, out bool all)
        {
            all = false;
            var r = ProfileStore.Record(c.Id);
            if (r == null || c.Platinum <= 0 || r.Platinum || score < c.Platinum) return false;
            r.Platinum = true;
            GearDrops.Wallet.Coins += Coins;
            if (Count == Total)
            {
                all = true;
                GearDrops.Wallet.Coins += AllCoins;
                var p = ProfileStore.Current.Progress;
                if (!p.Owned.Contains(Title)) p.Owned.Add(Title);
            }
            ProfileStore.Save();
            return true;
        }
    }
}
