// ===========================================================
// Assets/SquashGame/Runtime/Progression/KitDirector.cs
// P2-M5: who wears what on court.
//
//   you (athlete 0)   your Locker Room loadout (the menus' demo too, so
//                     the Locker Room's preview is on the court)
//   the opponent      a Quick Match personality's signature kit, a
//                     career pro's kit (MatchSettings.KitSeed), or
//                     player 2's kit in Local Versus - then an away
//                     shirt if it would look like yours
// ===========================================================

using SquashGame.Profile;

namespace SquashGame.Progression
{
    public static class KitDirector
    {
        /// <summary>A loadout shown instead of yours (the Locker Room's preview); null = yours.</summary>
        public static Loadout Preview;

        public static Loadout You() { return Preview ?? ProfileStore.Current.Progress.Loadout; }

        /// <summary>The opponent's kit shown instead (the versus screen's pick, on the menus' demo athlete); null = theirs.</summary>
        public static Loadout OpponentPreview;

        static readonly Loadout PlayerTwo = new Loadout { Shirt = "shirt_sky", Shorts = "shorts_navy", Socks = "socks_navy", Shoes = "shoes_white", Skin = "skin_2", Frame = "frame_royal", Grip = "grip_white" };

        static MatchSettings _for;
        static string _mine;
        static Loadout _theirs;

        /// <summary>The match moment athlete i shows: at match over the winner celebrates (you: your Locker Room
        /// celebration), the loser hangs their head. Null otherwise (and in the menus' demo).</summary>
        public static string Moment(MatchRunner r, int i)
        {
            if (r == null || r.Sim == null || r.Demo || r.Sim.Phase != SquashSim.MatchPhase.MatchOver) return null;
            int w = r.Sim.Score.MatchWinner;
            if (w < 0) return null;
            if (w != i) return "dejected";
            if (i == 0 || r.Settings.TwoPlayer) { var c = Cosmetics.Find(i == 0 ? You().Celebration : "cel_fist"); return c != null && c.Pose != null ? c.Pose : "cel_fist"; }
            return "cel_raise";
        }

        public static Loadout Opponent(MatchRunner r)
        {
            var s = r != null ? r.Settings : null;
            var me = You();
            if (OpponentPreview != null) return Cosmetics.AvoidClash(me, OpponentPreview);
            string mine = me.Shirt;
            if (_theirs != null && ReferenceEquals(s, _for) && mine == _mine) return _theirs;
            Loadout raw = s == null ? Cosmetics.Signature("attacker")
                        : s.TwoPlayer ? PlayerTwo
                        : s.KitSeed != 0 ? Cosmetics.AiKit(s.KitSeed)
                        : Cosmetics.Signature(s.Opponent);
            _theirs = Cosmetics.AvoidClash(me, raw);
            _for = s; _mine = mine;
            return _theirs;
        }
    }
}
