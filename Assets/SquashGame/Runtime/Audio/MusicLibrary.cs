// ===========================================================
// Assets/SquashGame/Runtime/Audio/MusicLibrary.cs
// Batch 6 (2026-10-26): the menus' songs - indie and hip-hop with
// vocals, picked by the user, every one Creative Commons Attribution
// (credited in Resources/Music/CREDITS.md and on the settings screen).
// Each belongs to a mood: the main menu, the career, the Gauntlet, the
// Locker and online. A song not in Resources/Music is skipped (so the
// game runs with any of them); with none, the old made-up track plays.
// ===========================================================

using System.Collections.Generic;

namespace SquashGame.Audio
{
    public enum MusicMood { Menu, Career, Gauntlet, Locker }

    public sealed class Song
    {
        public string File, Title, Artist, Licence, Url;
        public MusicMood Mood;
        public string Credit { get { return "\"" + Title + "\" by " + Artist + " (" + Licence + ")"; } }
    }

    public static class MusicLibrary
    {
        public const string Folder = "Music/";

        static Song S(MusicMood mood, string file, string title, string artist, string licence, string url)
        { return new Song { Mood = mood, File = file, Title = title, Artist = artist, Licence = licence, Url = url }; }

        public static readonly Song[] All =
        {
            S(MusicMood.Menu, "tiny_spaceships", "Tiny Spaceships", "hansatom", "CC BY 3.0", "https://ccmixter.org/files/hansatom/60364"),
            S(MusicMood.Menu, "hands_up", "Hands Up (cdk Island Mix)", "cdk", "CC BY 3.0", "https://ccmixter.org/files/cdk/39541"),
            S(MusicMood.Menu, "put_your_hands_up", "Put Your Hands Up", "Coruscate", "CC BY 3.0", "https://ccmixter.org/files/Coruscate/35893"),
            S(MusicMood.Menu, "jw_crazyglue", "Crazy Glue", "Josh Woodward", "CC BY 4.0", "https://www.joshwoodward.com/song/CrazyGlue"),
            S(MusicMood.Career, "sunshine", "Sunshine (feat Andres Franco)", "Funnywico", "CC BY 3.0", "https://ccmixter.org/files/Funnywico/45681"),
            S(MusicMood.Career, "free_hearts", "Free Hearts", "royalarc305", "CC BY 3.0", "https://ccmixter.org/files/royalarc305/39304"),
            S(MusicMood.Career, "jw_letitin", "Let It In", "Josh Woodward", "CC BY 4.0", "https://www.joshwoodward.com/song/LetItIn"),
            S(MusicMood.Career, "could_be", "Could Be (BABADI Remix)", "BABADI", "CC BY 3.0", "https://ccmixter.org/files/BABADI/51563"),
            S(MusicMood.Gauntlet, "rich", "Rich", "hansatom", "CC BY 3.0", "https://ccmixter.org/files/hansatom/32556"),
            S(MusicMood.Gauntlet, "spatial_voyage", "A Spatial Voyage From A To B", "Robbero feat. Kamihamiha", "CC BY 3.0", "https://ccmixter.org/files/Robbero/43359"),
            S(MusicMood.Gauntlet, "thanks_to_bob", "Thanks To Bob!", "Coruscate", "CC BY 3.0", "https://ccmixter.org/files/Coruscate/40961"),
            S(MusicMood.Locker, "strangers", "Strangers To Ourselves (ft Grapes & Gurdonark)", "donnieozone", "CC BY 3.0", "https://ccmixter.org/files/donnieozone/42821"),
            S(MusicMood.Locker, "overreacting", "Overreacting", "Neurowaxx", "CC BY 2.5", "https://ccmixter.org/files/Neurowaxx/13937"),
        };

        public static List<Song> Of(MusicMood mood) { var l = new List<Song>(); foreach (var s in All) if (s.Mood == mood) l.Add(s); return l; }

        /// <summary>The settings screen's line: every song in the game, credited (CC BY asks for it).</summary>
        public static string Credits(System.Func<Song, bool> present)
        {
            var parts = new List<string>();
            foreach (var s in All) if (present(s)) parts.Add(s.Credit);
            return parts.Count == 0 ? "" : "Music: " + string.Join("; ", parts) + ".";
        }
    }
}
