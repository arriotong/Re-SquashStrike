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
            // batch 5: clubs and a mid-tier event, after the user's club photos (at the end: saved choices keep their courts)
            new CourtTheme { Id = "warehouse", Name = "Warehouse Club", Blurb = "A row of courts in a steel shed, the green walkway, bags on the benches", Walls = WallStyle.Plaster,
                             Lines = new Color(0.16f, 0.32f, 0.78f), FloorTint = new Color(0.92f, 0.86f, 0.78f), FarClip = 90f, Establishing = false },
            new CourtTheme { Id = "leisure", Name = "Leisure Centre", Blurb = "Three red-lined courts, the corridor behind the glass, chairs and a TV", Walls = WallStyle.Plaster,
                             Lines = new Color(0.82f, 0.18f, 0.16f), FloorTint = new Color(0.96f, 0.88f, 0.76f), FarClip = 90f, Establishing = false },
            new CourtTheme { Id = "county", Name = "County Open", Blurb = "The club on event day: bleachers behind the glass, the scoreboard up", Walls = WallStyle.Plaster,
                             Lines = new Color(0.16f, 0.32f, 0.78f), FloorTint = new Color(0.92f, 0.86f, 0.78f), FarClip = 90f, Establishing = false },
        };

        /// <summary>The court being drawn (CourtView rebuilds when it changes).</summary>
        public static string CurrentId = "show";

        /// <summary>How full the stands are, 0..1 (CourtView rebuilds when it changes). A career's small events play to a
        /// handful of people; the big ones are packed.</summary>
        public static float CrowdFill = 1f;

        public static CourtTheme Current { get { return Find(CurrentId) ?? All[0]; } }

        public static CourtTheme Find(string id)
        {
            if (id != null && id.StartsWith(VenuePrefix)) return VenueTheme(id.Substring(VenuePrefix.Length));
            if (id != null && id.StartsWith(TourPrefix)) return TourTheme(id.Substring(TourPrefix.Length));
            foreach (var c in All) if (c.Id == id) return c;
            return null;
        }

        public static int IndexOf(string id) { for (int i = 0; i < All.Length; i++) if (All[i].Id == id) return i; return 0; }

        public static string[] Names { get { var n = new string[All.Length]; for (int i = 0; i < n.Length; i++) n[i] = All[i].Name; return n; } }

        // The career's stages (SquashSim.Career.Tier order, Challenger 3 .. World Tour Finals): the club court for the small
        // Challengers, then up through the glass courts - the rooftop, the station, the pyramids, the show court - to the
        // full arena for the World Championship and the Finals; the stands fill as the events grow.
        static readonly string[] CareerCourts = { "club", "warehouse", "leisure", "county", "rooftop", "station", "station", "desert", "desert", "show", "show", "arena", "arena" };   // batch 5: the clubs first
        static readonly float[] CareerCrowd = { 0.18f, 0.28f, 0.4f, 0.5f, 0.6f, 0.68f, 0.76f, 0.84f, 0.9f, 0.95f, 1f, 1f, 1f };

        // ---- courts per tournament (2026-10-27): a career event at a venue of its own (ClubVenues); the rest by tier ----

        public const string VenuePrefix = "venue:", TourPrefix = "tour:";
        static readonly System.Collections.Generic.Dictionary<string, CourtTheme> _venues = new System.Collections.Generic.Dictionary<string, CourtTheme>();

        /// <summary>A venue's court theme (null if there is no such venue).</summary>
        static CourtTheme VenueTheme(string key)
        {
            if (_venues.TryGetValue(key, out var t)) return t;
            var v = ClubVenues.Find(key);
            if (v == null) return null;
            t = new CourtTheme
            {
                Id = VenuePrefix + key, Name = v.Venue, Blurb = v.City, Walls = v.Kind == ClubKind.Centre ? WallStyle.Glass : WallStyle.Plaster,
                Lines = v.Lines, FloorTint = v.Floor, FarClip = 100f, Establishing = v.Kind == ClubKind.Centre,
            };
            _venues[key] = t;
            return t;
        }

        /// <summary>A World Tour venue's court theme (TourVenues; null if there is no such venue).</summary>
        static CourtTheme TourTheme(string key)
        {
            if (_venues.TryGetValue(TourPrefix + key, out var t)) return t;
            var v = TourVenues.Find(key);
            if (v == null) return null;
            t = new CourtTheme { Id = TourPrefix + key, Name = v.Name, Blurb = v.City, Walls = v.Walls, Lines = v.Lines, FloorTint = v.Floor, FarClip = v.FarClip, Establishing = v.Establishing };
            _venues[TourPrefix + key] = t;
            return t;
        }

        /// <summary>The court a career event is played on: its World Tour stage or its Challenger venue when it has one (the
        /// Tour Finals: the season's host city's), else its tier's.</summary>
        public static string ForCareerEvent(string eventName, int tier, string city = null)
        {
            var t = TourVenues.ForEvent(eventName, city);
            if (t != null) return TourPrefix + t.Key;
            if (eventName == "Tour Finals" && city == "Kuala Lumpur") return VenuePrefix + "kualalumpur";     // the season 6 Finals: Bukit Jalil
            var v = ClubVenues.ForEvent(eventName);
            return v != null ? VenuePrefix + v.Key : ForCareerTier(tier);
        }

        /// <summary>A built venue's court (a tournament's own, not a tier's)?</summary>
        public static bool IsVenue(string id) { return id != null && (id.StartsWith(VenuePrefix) || id.StartsWith(TourPrefix)); }

        public static string ForCareerTier(int tier) { return CareerCourts[Mathf.Clamp(tier, 0, CareerCourts.Length - 1)]; }
        public static float CrowdForCareerTier(int tier) { return CareerCrowd[Mathf.Clamp(tier, 0, CareerCrowd.Length - 1)]; }
    }
}
