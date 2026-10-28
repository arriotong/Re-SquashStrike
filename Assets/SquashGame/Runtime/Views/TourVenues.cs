// ===========================================================
// Assets/SquashGame/Runtime/Views/TourVenues.cs
// Courts per tournament, part 2 (2026-10-27): the World Tour's stages,
// built from the user's photos (RE_SQUASHSTRIKE_COURT_PHOTO_NOTES.md),
// "as epic as possible" (the user). Each venue has its own builder
// (VenueView.Tour.cs) and its own court: the front wall's colours and
// marks, the glass's tint, the lines, the floor, its night.
//
// A venue is a court theme with the id "tour:<key>" (Courts.Find). Career
// events find theirs by name (and the Tour Finals by the season's host
// city); a Challenger in the same city shares it where the research said
// it reuses the World Tour venue.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame
{
    public sealed class TourVenue
    {
        public string Key, Name, City;
        public string[] Events = new string[0];
        public string FinalsCity;                               // the Tour Finals' host city it serves (null: none)
        public WallStyle Walls = WallStyle.ShowBlue;            // ShowBlue: a branded front wall; Glass: frosted
        public Color Deep = new Color(0.07f, 0.20f, 0.48f), Mid = new Color(0.13f, 0.34f, 0.70f);   // the front wall's colours
        public string Mark = "", Sub = "";                      // the event's mark on the front wall
        public string[] Names = { "RE:SQUASHSTRIKE", "WORLD TOUR", "PSA" };
        public Color Lines = new Color(0.93f, 0.95f, 1f), Floor = new Color(0.84f, 0.80f, 0.76f);
        public Color Glass = new Color(0.62f, 0.80f, 0.95f, 0.09f);    // the side and back glass
        public Color Frame = Greybox.Hex(0x1d5fb8);             // the coping and plinth round the glass
        public Color Tin = new Color(0.05f, 0.09f, 0.18f), TinEdge = new Color(0.45f, 0.72f, 1f);
        public bool Night = true;                               // the pro night look (false: bright, daylit or broadcast)
        public Color Spill = new Color(0.6f, 0.68f, 1f), Wash = new Color(0, 0, 0, 0);
        public float AmbientK = 0.65f, FarClip = 160f;
        public bool Establishing = true;                        // the high opening shot (not under a low club ceiling)
    }

    public static class TourVenues
    {
        static Color H(uint rgb) { return Greybox.Hex(rgb); }
        static Color Hc(uint rgb, float a) { var c = Greybox.Hex(rgb); c.a = a; return c; }

        public static readonly TourVenue[] All =
        {
            new TourVenue { Key = "pyramids", Name = "The Pyramids", City = "Giza", Events = new[] { "Egyptian Open", "Giza Challenger" },
                Deep = H(0x0a1f5c), Mid = H(0x1a44a8), Mark = "EGYPTIAN OPEN", Sub = "GIZA PYRAMIDS", Names = new[] { "CAIRO", "GIZA", "EGYPT", "PSA WORLD TOUR" },
                Lines = H(0xe8542a), Floor = new Color(0.62f, 0.66f, 0.72f), Glass = new Color(0.55f, 0.72f, 0.95f, 0.10f), Frame = H(0x0c1424),
                Spill = new Color(0.45f, 0.55f, 1f), Wash = new Color(0.55f, 0.25f, 1.4f, 0.22f), AmbientK = 0.6f, FarClip = 900f },
            new TourVenue { Key = "grandcentral", Name = "Grand Central Terminal", City = "New York", Events = new[] { "Tournament of Champions" },
                Walls = WallStyle.Glass, Mark = "TOURNAMENT OF CHAMPIONS", Sub = "GRAND CENTRAL", Names = new[] { "TOURNAMENT OF CHAMPIONS", "NEW YORK" },
                Lines = H(0xd02a2a), Floor = new Color(0.9f, 0.84f, 0.74f), Glass = new Color(0.7f, 0.82f, 0.95f, 0.08f), Frame = H(0x1a1c22),
                Night = false, FarClip = 200f },
            new TourVenue { Key = "unionstation", Name = "Union Station Great Hall", City = "Chicago", Events = new[] { "World Championship", "Chicago Open" },
                Deep = H(0x0b0f24), Mid = H(0x1a2a5c), Mark = "WORLD CHAMPIONSHIP", Sub = "CHICAGO", Names = new[] { "PSA WORLD CHAMPIONSHIP", "CHICAGO" },
                Lines = H(0xd8302a), Floor = new Color(0.9f, 0.86f, 0.8f), Glass = new Color(0.55f, 0.7f, 0.95f, 0.10f), Frame = H(0x14161c),
                Spill = new Color(1f, 0.78f, 0.5f), Wash = new Color(1.4f, 0.8f, 0.3f, 0.16f), AmbientK = 0.7f, FarClip = 200f },
            new TourVenue { Key = "mallofarabia", Name = "Mall of Arabia", City = "Cairo", FinalsCity = "Cairo", Events = new[] { "Cairo Challenger" },
                Deep = H(0x0a2a6a), Mid = H(0x1c5ac0), Mark = "PSA TOUR FINALS", Sub = "CAIRO", Names = new[] { "PSA WORLD TOUR FINALS", "CAIRO" },
                Lines = H(0xe03a2a), Floor = new Color(0.7f, 0.72f, 0.76f), Glass = new Color(0.45f, 0.65f, 0.95f, 0.16f), Frame = H(0xb81e24),
                Spill = new Color(0.9f, 0.75f, 0.6f), Wash = new Color(1.4f, 0.6f, 0.2f, 0.16f), AmbientK = 0.7f, FarClip = 700f },
            new TourVenue { Key = "library", Name = "Bibliotheca Alexandrina", City = "Alexandria", Events = new[] { "Alexandria Open" },
                Deep = H(0x0c2a74), Mid = H(0x2050b8), Mark = "ALEXANDRIA OPEN", Sub = "BIBLIOTHECA ALEXANDRINA", Names = new[] { "ALEXANDRIA", "EGYPT" },
                Lines = H(0xd8302a), Floor = new Color(0.86f, 0.76f, 0.62f), Glass = new Color(0.6f, 0.75f, 0.95f, 0.10f), Frame = H(0xd8541e),
                Spill = new Color(0.5f, 0.6f, 1f), Wash = new Color(0.2f, 0.5f, 1.4f, 0.2f), AmbientK = 0.65f, FarClip = 600f },
            new TourVenue { Key = "cirque", Name = "Cirque d'Hiver", City = "Paris", FinalsCity = "Paris", Events = new[] { "Paris Squash" },
                Walls = WallStyle.Glass, Mark = "PARIS SQUASH", Sub = "CIRQUE D HIVER", Names = new[] { "PARIS SQUASH", "CIRQUE D HIVER" },
                Lines = H(0x7af0a0), Floor = new Color(0.45f, 0.72f, 0.78f), Glass = new Color(0.5f, 0.8f, 0.9f, 0.10f), Frame = H(0xb8902a),
                Spill = new Color(0.4f, 0.55f, 1.1f), Wash = new Color(0.2f, 0.45f, 1.4f, 0.24f), AmbientK = 0.55f, FarClip = 200f },
            new TourVenue { Key = "khalifa", Name = "Khalifa International Squash Complex", City = "Doha", FinalsCity = "Doha",
                Events = new[] { "Qatar Classic", "QSF No.5", "QSF No.2", "Doha Challenger" },
                Deep = H(0x14308a), Mid = H(0x2a5ad0), Mark = "QATAR CLASSIC", Sub = "DOHA", Names = new[] { "QATAR", "QATAR CLASSIC", "QATAR" },
                Lines = H(0xe03030), Floor = new Color(0.42f, 0.32f, 0.26f), Glass = new Color(0.3f, 0.5f, 1f, 0.22f), Frame = H(0x1a3a9a),
                Tin = H(0x101216), TinEdge = H(0x3a6ad8), Night = false, FarClip = 160f },
            new TourVenue { Key = "harbour", Name = "Harbourfront, Central", City = "Hong Kong", FinalsCity = "Hong Kong", Events = new[] { "Hong Kong Open" },
                Deep = H(0x0a1e6a), Mid = H(0x1e4ac8), Mark = "HONG KONG OPEN", Sub = "VICTORIA HARBOUR", Names = new[] { "HONG KONG", "SQUASH OPEN" },
                Lines = H(0xe8e8f0), Floor = new Color(0.35f, 0.5f, 0.85f), Glass = new Color(0.6f, 0.75f, 0.95f, 0.10f), Frame = H(0xc8c8d0),
                Spill = new Color(0.8f, 0.55f, 1f), Wash = new Color(1.3f, 0.3f, 1f, 0.2f), AmbientK = 0.6f, FarClip = 900f },
            new TourVenue { Key = "usopen", Name = "Arlen Specter US Squash Center", City = "Philadelphia", Events = new[] { "U.S. Open", "Philadelphia Challenger" },
                Deep = H(0x0a1438), Mid = H(0x16285c), Mark = "U.S. OPEN", Sub = "PHILADELPHIA", Names = new[] { "US SQUASH", "U.S. OPEN", "PHILADELPHIA" },
                Lines = H(0xf0f2f6), Floor = new Color(0.62f, 0.64f, 0.66f), Glass = new Color(0.3f, 0.55f, 1f, 0.24f), Frame = H(0x1a2a6a),
                Spill = new Color(0.9f, 0.4f, 0.5f), Wash = new Color(1.4f, 0.2f, 0.3f, 0.16f), AmbientK = 0.6f, FarClip = 200f },
            new TourVenue { Key = "marina", Name = "Abu Tig Marina", City = "El Gouna", Events = new[] { "El Gouna International" },
                Deep = H(0x0c2a86), Mid = H(0x2a5ae0), Mark = "EL GOUNA INTERNATIONAL", Sub = "RED SEA", Names = new[] { "EL GOUNA", "SQUASH OPEN" },
                Lines = H(0xe03a2a), Floor = new Color(0.88f, 0.88f, 0.9f), Glass = new Color(0.5f, 0.7f, 1f, 0.14f), Frame = H(0x101218),
                Spill = new Color(0.75f, 0.6f, 1f), Wash = new Color(0.9f, 0.4f, 1.4f, 0.2f), AmbientK = 0.7f, FarClip = 600f },
            new TourVenue { Key = "squashzone", Name = "Squash Zone", City = "Redwood City", Events = new[] { "Silicon Valley Open" },
                Deep = H(0x0a1230), Mid = H(0x182456), Mark = "SILICON VALLEY OPEN", Sub = "REDWOOD CITY", Names = new[] { "SILICON VALLEY", "SQUASH ZONE" },
                Lines = H(0xf2c81e), Floor = new Color(0.6f, 0.62f, 0.64f), Glass = new Color(0.35f, 0.55f, 1f, 0.20f), Frame = H(0x14182a),
                Tin = H(0x0c0d10), TinEdge = H(0xf2c81e), Establishing = false,
                Spill = new Color(0.4f, 0.55f, 1.1f), Wash = new Color(0.2f, 0.4f, 1.4f, 0.22f), AmbientK = 0.65f, FarClip = 160f },
            new TourVenue { Key = "ocbc", Name = "OCBC Arena", City = "Singapore", Events = new[] { "Singapore Open" },
                Deep = H(0x0a3a78), Mid = H(0x18a0d8), Mark = "SINGAPORE OPEN", Sub = "OCBC ARENA", Names = new[] { "SINGAPORE", "SQUASH OPEN" },
                Lines = H(0xff4a8a), Floor = new Color(0.82f, 0.88f, 0.92f), Glass = new Color(0.3f, 0.85f, 1f, 0.22f), Frame = H(0x101820),
                Spill = new Color(0.5f, 0.5f, 1.2f), Wash = new Color(0.7f, 0.3f, 1.5f, 0.24f), AmbientK = 0.55f, FarClip = 200f },
            new TourVenue { Key = "texas", Name = "Houston Squash Club", City = "Houston", Events = new[] { "Texas Open" },
                Deep = H(0x0a2aa8), Mid = H(0x2050e0), Mark = "TEXAS OPEN", Sub = "HOUSTON", Names = new[] { "TEXAS", "SQUASH OPEN", "HOUSTON" },
                Lines = H(0xf6c81a), Floor = new Color(0.22f, 0.26f, 0.38f), Glass = new Color(0.15f, 0.3f, 1f, 0.30f), Frame = H(0x0a1a6a),
                Tin = H(0x0c0e14), TinEdge = H(0xf6c81a),
                Spill = new Color(0.55f, 0.5f, 1f), Wash = new Color(0.3f, 0.3f, 1.4f, 0.2f), AmbientK = 0.6f, FarClip = 160f },
            new TourVenue { Key = "brisbane", Name = "Brisbane Arena", City = "Brisbane", Events = new[] { "Australian Open", "Brisbane Open" },
                Deep = H(0x0a2a78), Mid = H(0x1e56c8), Mark = "AUSTRALIAN OPEN", Sub = "BRISBANE", Names = new[] { "SQUASH AUSTRALIA", "AUSTRALIAN OPEN" },
                Lines = new Color(0.3f, 1.6f, 0.9f), Floor = new Color(0.6f, 0.64f, 0.7f), Glass = new Color(0.4f, 0.75f, 0.95f, 0.14f), Frame = H(0x0e1218),
                Spill = new Color(0.4f, 0.9f, 0.7f), Wash = new Color(0.2f, 1.2f, 0.6f, 0.18f), AmbientK = 0.55f, FarClip = 200f },
            new TourVenue { Key = "rep", Name = "Birmingham Rep", City = "Birmingham", Events = new[] { "British Open" },
                Deep = H(0x6a0c2a), Mid = H(0xd8346a), Mark = "BRITISH OPEN", Sub = "BIRMINGHAM", Names = new[] { "BRITISH OPEN", "BIRMINGHAM" },
                Lines = H(0xf2f2f4), Floor = new Color(0.6f, 0.6f, 0.64f), Glass = new Color(0.95f, 0.6f, 0.75f, 0.10f), Frame = H(0x0e0e12),
                Spill = new Color(1f, 0.4f, 0.8f), Wash = new Color(1.4f, 0.2f, 1.2f, 0.24f), AmbientK = 0.5f, FarClip = 160f },
            new TourVenue { Key = "allypally", Name = "Alexandra Palace Theatre", City = "London", FinalsCity = "London", Events = new[] { "London Championships", "London Challenger" },
                Deep = H(0x5a0a1a), Mid = H(0xb81e3a), Mark = "LONDON SQUASH CLASSIC", Sub = "ALEXANDRA PALACE", Names = new[] { "LONDON", "SQUASH CLASSIC" },
                Lines = H(0xff6ab4), Floor = new Color(0.62f, 0.62f, 0.64f), Glass = new Color(0.95f, 0.55f, 0.7f, 0.10f), Frame = H(0x8a1022),
                Tin = H(0x0c0c10), TinEdge = H(0xff6ab4),
                Spill = new Color(0.8f, 0.45f, 1f), Wash = new Color(1.2f, 0.2f, 0.6f, 0.2f), AmbientK = 0.5f, FarClip = 160f },
            new TourVenue { Key = "zurich", Name = "Zurich Hauptbahnhof", City = "Zurich", Events = new[] { "Grasshopper Cup" },
                Deep = H(0x0a2470), Mid = H(0x1e50c0), Mark = "GRASSHOPPER CUP", Sub = "ZURICH HB", Names = new[] { "GRASSHOPPER CUP", "ZURICH" },
                Lines = H(0xf2f2f6), Floor = new Color(0.86f, 0.8f, 0.7f), Glass = new Color(0.45f, 0.7f, 1f, 0.14f), Frame = H(0x101218),
                Spill = new Color(0.6f, 0.75f, 1.1f), Wash = new Color(0.3f, 0.8f, 1.4f, 0.18f), AmbientK = 0.65f, FarClip = 200f },
            new TourVenue { Key = "pool", Name = "Oriental Sports Center", City = "Shanghai", Events = new[] { "China Open", "Shanghai Open" },
                Deep = H(0x0c2a8a), Mid = H(0x2456d8), Mark = "CHINA OPEN", Sub = "SHANGHAI", Names = new[] { "CHINA OPEN", "SQUASH" },
                Lines = H(0xff3c9a), Floor = new Color(0.8f, 0.86f, 0.88f), Glass = new Color(0.3f, 0.95f, 0.85f, 0.14f), Frame = H(0x8a3ad8),
                Spill = new Color(0.85f, 0.5f, 1f), Wash = new Color(1f, 0.3f, 1.4f, 0.2f), AmbientK = 0.7f, FarClip = 900f },

            // ---- part 3 (2026-10-28): the Silver, Bronze and Copper events - #18 to #22 from the user's photos, #23 to #39 from
            // photos Claude looked up (RE_SQUASHSTRIKE_COURT_PHOTO_NOTES.md). The club courts are plaster (WallStyle.Plaster). ----
            new TourVenue { Key = "cleveland", Name = "Drusinsky Sports Medicine Institute", City = "Cleveland", Events = new[] { "Squash In The Land" },
                Walls = WallStyle.Glass, Mark = "SQUASH IN THE LAND", Sub = "CLEVELAND", Names = new[] { "SQUASH IN THE LAND", "CLEVELAND" },
                Lines = H(0x2a5ae0), Floor = new Color(0.58f, 0.52f, 0.46f), Glass = new Color(0.25f, 0.45f, 1f, 0.26f), Frame = H(0x16181e),
                Tin = H(0x101216), TinEdge = H(0xf07a1a), Night = false, FarClip = 200f },
            new TourVenue { Key = "bac", Name = "Birmingham Athletic Club", City = "Bloomfield Hills", Events = new[] { "Motor City Open" },
                Walls = WallStyle.Plaster, Mark = "MOTOR CITY OPEN", Names = new[] { "MOTOR CITY OPEN" },
                Lines = H(0xc8352e), Floor = new Color(0.96f, 0.88f, 0.74f), Tin = H(0x1a1c22), TinEdge = H(0xc8352e), Night = false, Establishing = false, FarClip = 100f },
            new TourVenue { Key = "steelcity", Name = "Steel City Squash", City = "Pittsburgh", Events = new[] { "Pittsburgh Open" },
                Deep = H(0x121418), Mid = H(0x2a2e36), Mark = "PITTSBURGH OPEN", Sub = "STEEL CITY", Names = new[] { "PITTSBURGH", "STEEL CITY SQUASH" },
                Lines = H(0xd8302a), Floor = new Color(0.42f, 0.36f, 0.32f), Glass = new Color(0.4f, 0.55f, 0.9f, 0.14f), Frame = H(0xf2b81e),
                Tin = H(0x0e0f12), TinEdge = H(0xf2b81e), Spill = new Color(1f, 0.7f, 0.4f), Wash = new Color(1.4f, 0.7f, 0.2f, 0.2f), AmbientK = 0.55f, FarClip = 160f },
            new TourVenue { Key = "theatreroyal", Name = "Isaac Theatre Royal", City = "Christchurch", Events = new[] { "New Zealand Open" },
                Walls = WallStyle.Glass, Mark = "NEW ZEALAND OPEN", Sub = "CHRISTCHURCH", Names = new[] { "NEW ZEALAND OPEN", "CHRISTCHURCH" },
                Lines = H(0x2a6ad8), Floor = new Color(0.30f, 0.30f, 0.32f), Glass = new Color(0.35f, 0.55f, 1f, 0.18f), Frame = H(0x0e0f14),
                Spill = new Color(1f, 0.8f, 0.55f), Wash = new Color(1.2f, 0.7f, 0.3f, 0.16f), AmbientK = 0.6f, FarClip = 160f },
            new TourVenue { Key = "opensquash", Name = "Open Squash FiDi", City = "New York", Events = new[] { "Open Squash Classic" },
                Deep = H(0x0a2aa0), Mid = H(0x1e5af0), Mark = "OPEN SQUASH CLASSIC", Sub = "NEW YORK", Names = new[] { "OPEN SQUASH", "FINANCIAL DISTRICT" },
                Lines = H(0x2a6aff), Floor = new Color(0.72f, 0.76f, 0.82f), Glass = new Color(0.12f, 0.32f, 1f, 0.32f), Frame = H(0x0a1a4a),
                Tin = H(0x0c0e16), TinEdge = H(0x3a7aff), Spill = new Color(0.4f, 0.55f, 1.2f), Wash = new Color(0.2f, 0.4f, 1.5f, 0.24f), AmbientK = 0.6f, FarClip = 400f },
            new TourVenue { Key = "thecurve", Name = "The Curve", City = "Petaling Jaya", Events = new[] { "Malaysia Squash Cup" },
                Deep = H(0x5a0a3a), Mid = H(0xc81e78), Mark = "MALAYSIA SQUASH CUP", Sub = "PETALING JAYA", Names = new[] { "MALAYSIA", "SQUASH CUP" },
                Lines = H(0xf6f2f6), Floor = new Color(0.66f, 0.66f, 0.70f), Glass = new Color(1f, 0.45f, 0.8f, 0.12f), Frame = H(0xff3ca0),
                Tin = H(0x1a0a14), TinEdge = H(0xff3ca0), Night = false, FarClip = 200f },
            new TourVenue { Key = "bowvalley", Name = "Bow Valley Square", City = "Calgary", Events = new[] { "Canadian Men's Open" },
                Deep = H(0x0a1a3a), Mid = H(0x1e3a7a), Mark = "CANADIAN OPEN", Sub = "CALGARY", Names = new[] { "CANADA", "CALGARY" },
                Lines = H(0xd8302a), Floor = new Color(0.6f, 0.6f, 0.62f), Glass = new Color(0.4f, 0.6f, 1f, 0.16f), Frame = H(0xc8352e),
                Spill = new Color(0.7f, 0.75f, 1f), Wash = new Color(0.4f, 0.6f, 1.4f, 0.18f), AmbientK = 0.6f, FarClip = 1100f },
            new TourVenue { Key = "sportwerk", Name = "Sportwerk", City = "Hamburg", Events = new[] { "German Open" },
                Deep = H(0x0a1840), Mid = H(0x1a3a8a), Mark = "GERMAN OPEN", Sub = "HAMBURG", Names = new[] { "GERMAN OPEN", "HAMBURG" },
                Lines = H(0xd8302a), Floor = new Color(0.86f, 0.78f, 0.64f), Glass = new Color(0.3f, 0.55f, 1f, 0.2f), Frame = H(0x2a7a44),
                Spill = new Color(0.8f, 0.8f, 1f), Wash = new Color(0.5f, 0.7f, 1.3f, 0.16f), AmbientK = 0.6f, FarClip = 400f },
            new TourVenue { Key = "sportcity", Name = "National Squash Centre", City = "Manchester", Events = new[] { "Manchester Open", "Manchester Challenger" },
                Deep = H(0x0a2a6a), Mid = H(0x2060d0), Mark = "MANCHESTER OPEN", Sub = "SPORTCITY", Names = new[] { "MANCHESTER", "SQUASH OPEN" },
                Lines = H(0xd8302a), Floor = new Color(0.62f, 0.64f, 0.68f), Glass = new Color(0.3f, 0.6f, 1f, 0.2f), Frame = H(0x1a3a9a),
                Spill = new Color(0.5f, 0.65f, 1.2f), Wash = new Color(0.3f, 0.5f, 1.5f, 0.22f), AmbientK = 0.55f, FarClip = 200f },
            new TourVenue { Key = "squashonfire", Name = "Squash on Fire", City = "Washington", Events = new[] { "Squash on Fire Open" },
                Deep = H(0x1a7ac8), Mid = H(0x48b8f0), Mark = "SQUASH ON FIRE OPEN", Sub = "WASHINGTON DC", Names = new[] { "SQUASH ON FIRE", "WASHINGTON" },
                Lines = H(0xf2d21e), Floor = new Color(0.9f, 0.82f, 0.68f), Glass = new Color(0.4f, 0.75f, 1f, 0.14f), Frame = H(0xff6a1a),
                Tin = H(0x101216), TinEdge = H(0xff6a1a), Spill = new Color(1f, 0.6f, 0.35f), Wash = new Color(1.4f, 0.5f, 0.15f, 0.18f), AmbientK = 0.6f, FarClip = 700f },
            new TourVenue { Key = "nash", Name = "London Squash and Fitness", City = "London, Ontario", Events = new[] { "NASH Cup" },
                Walls = WallStyle.Plaster, Mark = "NASH CUP", Names = new[] { "NASH CUP" },
                Lines = H(0xc8352e), Floor = new Color(0.97f, 0.89f, 0.74f), Tin = H(0x6a0c14), TinEdge = H(0xf2f1ed), Night = false, Establishing = false, FarClip = 100f },
            new TourVenue { Key = "citysquash", Name = "City Squash Club", City = "Budapest", Events = new[] { "Budapest Open" },
                Deep = H(0x0a3a3a), Mid = H(0x1a7a72), Mark = "BUDAPEST OPEN", Sub = "CITY SQUASH", Names = new[] { "BUDAPEST", "SQUASH OPEN" },
                Lines = H(0xd8302a), Floor = new Color(0.40f, 0.32f, 0.26f), Glass = new Color(0.3f, 0.9f, 0.8f, 0.14f), Frame = H(0xf08a24),
                Tin = H(0x0a1414), TinEdge = H(0xf08a24), Spill = new Color(1f, 0.8f, 0.45f), Wash = new Color(1.3f, 0.8f, 0.3f, 0.16f), AmbientK = 0.55f, FarClip = 500f },
            new TourVenue { Key = "mcarthur", Name = "McArthur Squash Center", City = "Charlottesville", Events = new[] { "Charlottesville Open" },
                Deep = H(0x0a1a4a), Mid = H(0x1a3a8a), Mark = "CHARLOTTESVILLE OPEN", Sub = "VIRGINIA", Names = new[] { "CHARLOTTESVILLE", "VIRGINIA" },
                Lines = H(0xf08a24), Floor = new Color(0.9f, 0.82f, 0.68f), Glass = new Color(0.55f, 0.75f, 0.95f, 0.10f), Frame = H(0xe8742a),
                Tin = H(0x0a1a4a), TinEdge = H(0xf08a24), Night = false, FarClip = 200f },
            new TourVenue { Key = "waterfront", Name = "Jack Poole Plaza", City = "Vancouver", Events = new[] { "Vancouver Open" },
                Walls = WallStyle.Glass, Mark = "VANCOUVER OPEN", Sub = "COAL HARBOUR", Names = new[] { "VANCOUVER", "SQUASH OPEN" },
                Lines = H(0xd8302a), Floor = new Color(0.62f, 0.64f, 0.68f), Glass = new Color(0.55f, 0.75f, 0.95f, 0.10f), Frame = H(0xe8e8ec),
                Spill = new Color(1f, 0.65f, 0.6f), Wash = new Color(1.4f, 0.5f, 0.6f, 0.18f), AmbientK = 0.7f, FarClip = 1300f },
            new TourVenue { Key = "galleria", Name = "Allen Lambert Galleria", City = "Toronto", Events = new[] { "Toronto Classic", "Toronto Open" },
                Deep = H(0x0a1a5a), Mid = H(0x2048b8), Mark = "TORONTO CLASSIC", Sub = "BROOKFIELD PLACE", Names = new[] { "TORONTO", "SQUASH CLASSIC" },
                Lines = H(0xd8302a), Floor = new Color(0.86f, 0.82f, 0.76f), Glass = new Color(0.45f, 0.65f, 1f, 0.14f), Frame = H(0xe8e8ec),
                Spill = new Color(0.7f, 0.75f, 1.1f), Wash = new Color(0.5f, 0.6f, 1.4f, 0.16f), AmbientK = 0.65f, FarClip = 700f },
            new TourVenue { Key = "viktoria", Name = "Centrum Viktoria", City = "Brno", Events = new[] { "Czech Open" },
                Walls = WallStyle.Plaster, Mark = "CZECH OPEN", Names = new[] { "CZECH OPEN" },
                Lines = H(0xc8352e), Floor = new Color(0.95f, 0.87f, 0.74f), Tin = H(0x1a1a1e), TinEdge = H(0xf07a1a), Night = false, Establishing = false, FarClip = 300f },
            new TourVenue { Key = "wintergarden", Name = "East Wintergarden", City = "London", Events = new[] { "London Open" },
                Deep = H(0x0a1a4a), Mid = H(0x1e40a0), Mark = "LONDON OPEN", Sub = "CANARY WHARF", Names = new[] { "LONDON", "SQUASH OPEN" },
                Lines = H(0xd8302a), Floor = new Color(0.66f, 0.66f, 0.70f), Glass = new Color(0.4f, 0.6f, 1f, 0.14f), Frame = H(0x14161c),
                Spill = new Color(0.6f, 0.7f, 1.2f), Wash = new Color(0.4f, 0.5f, 1.4f, 0.2f), AmbientK = 0.6f, FarClip = 700f },
            new TourVenue { Key = "wpcc", Name = "Western Province Cricket Club", City = "Cape Town", Events = new[] { "Cape Town Open" },
                Deep = H(0x0a3a7a), Mid = H(0x1a6ac8), Mark = "CAPE TOWN OPEN", Sub = "TABLE MOUNTAIN", Names = new[] { "CAPE TOWN", "SQUASH OPEN" },
                Lines = H(0xf2f2f6), Floor = new Color(0.58f, 0.74f, 0.90f), Glass = new Color(0.4f, 0.65f, 1f, 0.14f), Frame = H(0x1f6a3c),
                Spill = new Color(1f, 0.75f, 0.5f), Wash = new Color(1.4f, 0.7f, 0.3f, 0.18f), AmbientK = 0.75f, FarClip = 1500f },
            new TourVenue { Key = "gymkhana", Name = "Bombay Gymkhana", City = "Mumbai", Events = new[] { "Indian Open" },
                Deep = H(0x0a2a6a), Mid = H(0x1a4ab8), Mark = "INDIAN OPEN", Sub = "BOMBAY GYMKHANA", Names = new[] { "INDIA", "SQUASH OPEN", "MUMBAI" },
                Lines = H(0xd8302a), Floor = new Color(0.66f, 0.68f, 0.72f), Glass = new Color(0.4f, 0.8f, 0.9f, 0.14f), Frame = H(0x1f9a9a),
                Spill = new Color(1f, 0.7f, 0.4f), Wash = new Color(1.4f, 0.6f, 0.15f, 0.2f), AmbientK = 0.65f, FarClip = 800f },
            new TourVenue { Key = "bermuda", Name = "Bermuda Squash and Fitness Club", City = "Devonshire", Events = new[] { "Bermuda Open" },
                Walls = WallStyle.Plaster, Mark = "BERMUDA OPEN", Names = new[] { "BERMUDA OPEN" },
                Lines = H(0x1d4fb0), Floor = new Color(0.95f, 0.88f, 0.76f), Tin = H(0x0e2a4a), TinEdge = H(0x3ad8d0), Night = false, Establishing = false, FarClip = 700f },
        };

        static Dictionary<string, TourVenue> _byKey, _byEvent, _byFinals;

        static void Index()
        {
            if (_byKey != null) return;
            _byKey = new Dictionary<string, TourVenue>(); _byEvent = new Dictionary<string, TourVenue>(); _byFinals = new Dictionary<string, TourVenue>();
            foreach (var v in All)
            {
                _byKey[v.Key] = v;
                foreach (var e in v.Events) _byEvent[e] = v;
                if (v.FinalsCity != null) _byFinals[v.FinalsCity] = v;
            }
        }

        public static TourVenue Find(string key) { Index(); return key != null && _byKey.TryGetValue(key, out var x) ? x : null; }

        /// <summary>The venue an event is played at (the Tour Finals: by the season's host city), or null.</summary>
        public static TourVenue ForEvent(string eventName, string city)
        {
            Index();
            if (string.IsNullOrEmpty(eventName)) return null;
            if (eventName.EndsWith(" Qualifying")) eventName = eventName.Substring(0, eventName.Length - " Qualifying".Length);
            if (eventName == "Tour Finals") return city != null && _byFinals.TryGetValue(city, out var f) ? f : null;
            return _byEvent.TryGetValue(eventName, out var x) ? x : null;
        }
    }
}
