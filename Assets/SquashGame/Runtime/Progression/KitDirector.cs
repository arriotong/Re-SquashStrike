// ===========================================================
// Assets/SquashGame/Runtime/Progression/KitDirector.cs
// P2-M5: who wears what on court.
//
//   you (athlete 0)   your Locker Room loadout (the menus' demo too, so
//                     the Locker Room's preview is on the court)
//   the opponent      a Quick Match personality's signature kit, a
//                     career pro's kit (MatchSettings.KitSeed), or
//                     player 2's kit in Local Versus, or what an online
//                     opponent wears (P2-M8) - then an away shirt if it
//                     would look like yours
//   online            you may be athlete 1: "you" is the runner's
//                     HumanIndex, not athlete 0
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

        /// <summary>A celebration shown on your athlete in the menus (the Locker Room, browsing celebrations); null = none.</summary>
        public static string PreviewPose;

        static readonly Loadout PlayerTwo = new Loadout { Shirt = "shirt_sky", Shorts = "shorts_navy", Socks = "socks_navy", Shoes = "shoes_white", Skin = "skin_2", Frame = "frame_royal", Grip = "grip_white" };

        static MatchSettings _for;
        static string _mine;
        static Loadout _theirs;

        /// <summary>The match moment athlete i shows: at match over the winner celebrates (you: your Locker Room
        /// celebration), the loser hangs their head. Null otherwise (and in the menus' demo).</summary>
        public static string Moment(MatchRunner r, int i)
        {
            if (r != null && r.Demo && i == 0 && PreviewPose != null) return PreviewPose;
            if (r == null || r.Sim == null || r.Demo || r.Sim.Phase != SquashSim.MatchPhase.MatchOver) return null;
            int w = r.Sim.Score.MatchWinner;
            if (w < 0) return null;
            if (w != i) return "dejected";
            int me = r.HumanIndex;
            if (i == me || r.Settings.TwoPlayer) { var c = Cosmetics.Find(i == me ? You().Celebration : "cel_fist"); return c != null && c.Pose != null ? c.Pose : "cel_fist"; }
            return "cel_raise";
        }

        /// <summary>Athlete i's kit in this match: yours on your athlete, the opponent's on theirs.</summary>
        public static Loadout For(MatchRunner r, int i) { return i == (r != null ? r.HumanIndex : 0) ? Geared(You(), r) : Opponent(r); }

        /// <summary>P7 phase 5: in a career match your gear shows - its racket frame, shoes and shirt over your Locker loadout.</summary>
        static Loadout Geared(Loadout you, MatchRunner r)
        {
            var looks = r != null && r.Settings != null && r.Settings.Career ? r.Settings.GearLooks : null;
            if (looks == null || looks.Length == 0 || you == null) return you;
            var l = you.Clone();
            foreach (var id in looks) { var it = Cosmetics.Find(id); if (it != null) l.Set(it.Slot, id); }
            return l;
        }

        /// <summary>An online opponent's kit as they sent it (their Locker Room loadout; a signature kit if it does not read).</summary>
        static Loadout Worn(string json)
        {
            try { var l = string.IsNullOrEmpty(json) ? null : UnityEngine.JsonUtility.FromJson<Loadout>(json); if (l != null && !string.IsNullOrEmpty(l.Shirt)) return l; }
            catch (System.Exception) { }
            return Cosmetics.Signature("attacker");
        }

        public static Loadout Opponent(MatchRunner r)
        {
            var s = r != null ? r.Settings : null;
            var me = You();
            if (OpponentPreview != null) return Cosmetics.AvoidClash(me, OpponentPreview);
            string mine = me.Shirt;
            if (_theirs != null && ReferenceEquals(s, _for) && mine == _mine) return _theirs;
            Loadout raw = s == null ? Cosmetics.Signature("attacker")
                        : s.Online ? Worn(s.OppKit)
                        : s.TwoPlayer ? PlayerTwo
                        : s.KitSeed != 0 ? Cosmetics.AiKit(s.KitSeed)
                        : Cosmetics.Signature(s.Opponent);
            _theirs = Cosmetics.AvoidClash(me, raw);
            _for = s; _mine = mine;
            return _theirs;
        }
    }
}
