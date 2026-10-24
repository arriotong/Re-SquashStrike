// ===========================================================
// Assets/SquashGame/Runtime/Views/Courts.cs
// The courts a match can be played on (2026-10-24). Each is a theme:
// the court's own walls, lines and floor (CourtView) and everything
// around it - hall or open air, crowd, lights, sky (VenueView). The
// court's size and the ball's physics are the same everywhere: this is
// presentation only.
// ===========================================================

using UnityEngine;

namespace SquashGame
{
    public enum WallStyle
    {
        ShowBlue,      // glass sides and back, a deep-blue branded front wall
        Glass,         // all glass: the front wall frosted so the ball still reads
        Plaster,       // a club court: white plaster walls, a glass back
    }

    public sealed class CourtTheme
    {
        public string Id, Name, Blurb;
        public WallStyle Walls;
        public Color Lines = new Color(0.93f, 0.95f, 1f);
        public Color FloorTint = new Color(0.84f, 0.80f, 0.76f);
        public bool Outdoor;
        public float FarClip = 120f;
        /// <summary>Does a match open on the high establishing shot? (Not in a club's low room.)</summary>
        public bool Establishing = true;
    }

    public static class Courts
    {
        public static readonly CourtTheme[] All =
        {
            new CourtTheme { Id = "show", Name = "Show Court", Blurb = "A wood-panelled wintergarden under an arched glass roof", Walls = WallStyle.ShowBlue },
            new CourtTheme { Id = "club", Name = "Club Court", Blurb = "White walls, red lines and a gallery: where everyone starts", Walls = WallStyle.Plaster,
                             Lines = new Color(0.85f, 0.22f, 0.18f), FloorTint = new Color(0.95f, 0.90f, 0.84f), Establishing = false },
            new CourtTheme { Id = "desert", Name = "Desert Night", Blurb = "Glass on the sand, the pyramids floodlit behind the front wall", Walls = WallStyle.Glass,
                             Outdoor = true, FarClip = 900f },
            new CourtTheme { Id = "station", Name = "Station Hall", Blurb = "In the concourse: marble, great arched windows, a ceiling of stars", Walls = WallStyle.Glass,
                             FarClip = 160f },
            new CourtTheme { Id = "rooftop", Name = "City Rooftop", Blurb = "Up on a roof at dusk, the city lit all round", Walls = WallStyle.Glass,
                             Outdoor = true, FarClip = 700f },
            new CourtTheme { Id = "arena", Name = "Arena", Blurb = "A full arena: steep stands, LED ribbons, the big cube overhead", Walls = WallStyle.ShowBlue,
                             FarClip = 150f },
        };

        /// <summary>The court being drawn (CourtView rebuilds when it changes).</summary>
        public static string CurrentId = "show";

        public static CourtTheme Current { get { return Find(CurrentId) ?? All[0]; } }

        public static CourtTheme Find(string id)
        {
            foreach (var c in All) if (c.Id == id) return c;
            return null;
        }

        public static int IndexOf(string id) { for (int i = 0; i < All.Length; i++) if (All[i].Id == id) return i; return 0; }

        public static string[] Names { get { var n = new string[All.Length]; for (int i = 0; i < n.Length; i++) n[i] = All[i].Name; return n; } }
    }
}
