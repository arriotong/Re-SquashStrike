// ===========================================================
// Assets/SquashGame/Runtime/Features.cs
// 2026-11-25 (the user, for v0.7.0: "pull out the online in the release
// because i haven't bought a server yet"): what the game shows.
//
//   Online   the ONLINE section (ranked, quick play, private rooms, the
//            leaderboard) and the profile's online rating. Off in the
//            built game until there is a server; on in the editor so its
//            tests still run. To bring it back: ReleaseOnline = true.
// ===========================================================

namespace SquashGame
{
    public static class Features
    {
        /// <summary>Online in a built game (false: hidden until there's a server to play on).</summary>
        public const bool ReleaseOnline = false;

#if UNITY_EDITOR
        public static bool Online = true;
#else
        public static bool Online = ReleaseOnline;
#endif
    }
}
