// ===========================================================
// Assets/SquashGame/Runtime/Views/ClubVenues.cs
// Courts per tournament, part 1 (2026-10-27): 40 of the Challenger Tour's
// venues, and 14 generic city clubs where no venue was found or sure (the
// user's call); the rest reuse their city's World Tour court once built.
// The 40 come from the user's research (squash_challenger_references.md:
// the venue each Challenger has used, its city). Not modelled from
// photos yet - each is the nearest of three club layouts, dressed as the
// place: its own name on the banners, its country's flags, its colours,
// how many courts in the row, windows on a sunny city, bleachers for an
// event hall.
//
//   Shed       a row of courts in a big hall: a walkway behind the
//              glass, benches and kit bags, a steel (or timber) roof
//   Corridor   a members' club: a carpeted corridor behind the glass,
//              chairs, a low ceiling of light panels, honours boards
//   Centre     a national or event centre: the all-glass show court in
//              a sports hall, stands on three sides, a scoreboard
//
// A venue is a court theme with the id "venue:<key>" (Courts.Find makes
// it); career events find theirs by name (Courts.ForCareerEvent), any
// other event plays on its tier's court as before. When photos come in,
// a venue's line here is what gets refined.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame
{
    public enum ClubKind { Shed, Corridor, Centre }

    public sealed class ClubVenue
    {
        public string Key, Venue, City, Nat;
        public string[] Events;                 // the calendar's events played here
        public ClubKind Kind;
        public int Neighbours;                  // courts either side in the row (Shed, Corridor)
        public Color Lines, Plaster, Floor, Hall, Accent, Accent2;
        public bool Windows, Timber, Bleachers;
        public Color Sky = new Color(0.55f, 0.75f, 1f);
        public string Motto = "";
    }

    public static class ClubVenues
    {
        static Color H(uint rgb) { return Greybox.Hex(rgb); }
        static readonly Color Red = H(0xc8352e), Blue = H(0x1d4fb0), Black = H(0x202226), Yellow = H(0xe8c22a), Green = H(0x2a7a44);
        static readonly Color White = H(0xf2f1ed), Cream = H(0xece2cc), Grey = H(0xc9cbce), Concrete = H(0x9a9ea4), PaleBlue = H(0xdfe8f0);
        static readonly Color Oak = new Color(0.94f, 0.86f, 0.74f), Pine = new Color(0.98f, 0.9f, 0.76f), Dark = new Color(0.78f, 0.66f, 0.54f);
        static readonly Color Warm = new Color(1f, 0.82f, 0.55f), Tropic = new Color(0.45f, 0.82f, 1f), Grey2 = new Color(0.7f, 0.76f, 0.84f);

        static ClubVenue V(string key, string venue, string city, string nat, ClubKind kind, int n, Color lines, Color plaster, Color floor, Color hall,
                           Color accent, Color accent2, string motto, bool windows = false, bool timber = false, bool bleachers = false, Color? sky = null, string events = null, string also = null)
        {
            return new ClubVenue { Key = key, Venue = venue, City = city, Nat = nat, Kind = kind, Neighbours = n, Lines = lines, Plaster = plaster, Floor = floor,
                                   Hall = hall, Accent = accent, Accent2 = accent2, Motto = motto, Windows = windows, Timber = timber, Bleachers = bleachers,
                                   Sky = sky ?? new Color(0.55f, 0.75f, 1f), Events = also != null ? new[] { events, also } : new[] { events } };
        }

        public static readonly ClubVenue[] All =
        {
            // ---- Challenger 15 ----
            V("chennai", "Indian Squash Academy", "Chennai", "IND", ClubKind.Centre, 0, Red, White, Oak, H(0xe8e4dc), H(0x1d4fb0), H(0xf08a24), "HOME OF INDIAN SQUASH", windows: true, sky: Warm, events: "Chennai Open"),
            V("bega", "Bega Country Club", "Bega", "AUS", ClubKind.Corridor, 1, Red, Cream, Pine, H(0xd8cbb0), H(0x2a6a3a), H(0xe8c22a), "COUNTRY SQUASH AT ITS BEST", windows: true, timber: true, sky: Warm, events: "Bega Open"),
            V("lagos", "Teslim Balogun Stadium", "Lagos", "NGR", ClubKind.Centre, 0, Red, White, Oak, Concrete, H(0x1f7a3a), H(0xf2f1ed), "SURULERE - LAGOS", bleachers: true, events: "Lagos Open"),
            // ---- Challenger 12 ----
            V("nimy", "Aramis Club", "Nimy", "BEL", ClubKind.Shed, 2, Blue, White, Oak, H(0xd2d4d8), H(0xe86a1c), H(0x202226), "ARAMIS - NIMY - BELGIQUE", events: "Nimy Open"),
            V("madrid", "Tres Cantos Squash", "Madrid", "ESP", ClubKind.Shed, 2, Black, White, Oak, H(0xd6d8dc), H(0xc8352e), H(0xe8c22a), "THE BIGGEST SQUASH CLUB IN SPAIN", events: "Madrid Open"),
            V("sydney", "Thornleigh Squash Centre", "Sydney", "AUS", ClubKind.Corridor, 1, Red, White, Pine, H(0xdfe0e2), H(0x1f6a3c), H(0xe8c22a), "THORNLEIGH - NSW", windows: true, sky: Tropic, events: "Sydney Open"),
            V("mumbai", "Juhu Vile Parle Gymkhana", "Mumbai", "IND", ClubKind.Centre, 0, Red, White, Oak, H(0xe4ddd0), H(0x7a1f2b), H(0xe8c22a), "JVPG - JUHU", windows: true, sky: Warm, events: "Mumbai Open", also: "Mumbai Challenger"),
            // ---- Challenger 9 ----
            V("winchester", "Winchester Racquets Club", "Winchester", "ENG", ClubKind.Corridor, 1, Red, White, Oak, H(0xe3e1dc), H(0x1d3f7a), H(0xc8352e), "HAMPSHIRE OPEN", timber: true, events: "Winchester Challenger"),
            V("bengaluru", "Lakshyan Academy of Sports", "Bengaluru", "IND", ClubKind.Shed, 2, Blue, White, Oak, H(0xdcdde0), H(0x1d4fb0), H(0xf08a24), "LAKSHYAN - BENGALURU", windows: true, sky: Warm, events: "Bengaluru Challenger"),
            V("hongkong", "Hong Kong Sports Institute", "Hong Kong", "HKG", ClubKind.Centre, 0, Red, White, Oak, H(0xe6e8ea), H(0xc8352e), H(0x202226), "HKSI - SHA TIN", windows: true, sky: Grey2, events: "Hong Kong Challenger"),
            V("yokohama", "Yokohama SQ-CUBE", "Yokohama", "JPN", ClubKind.Centre, 0, Black, White, Pine, H(0x2a2d33), H(0x2ab8d8), H(0xf2f1ed), "SQ-CUBE YOKOHAMA", bleachers: true, events: "Yokohama Challenger"),
            V("prague", "Squash Club Strahov", "Prague", "CZE", ClubKind.Shed, 2, Blue, White, Oak, Concrete, H(0x1d4fb0), H(0xc8352e), "STRAHOV - PRAHA", events: "Prague Challenger"),
            // ---- Challenger 6 ----
            V("devonport", "Devonport Squash Centre", "Devonport", "AUS", ClubKind.Shed, 1, Red, Cream, Pine, H(0xd9d2c4), H(0x1f6a3c), H(0xe8c22a), "TASMANIAN OPEN", timber: true, events: "Devonport Challenger"),
            V("johnscreek", "Johns Creek Athletic Club", "Johns Creek", "USA", ClubKind.Corridor, 1, Black, White, Oak, H(0x3a3d44), H(0xe8c22a), H(0x202226), "JOHNS CREEK - GEORGIA", events: "Johns Creek Challenger"),
            V("johannesburg", "Parkview Squash Centre", "Johannesburg", "RSA", ClubKind.Corridor, 1, Red, White, Oak, H(0xe2ded4), H(0x1f7a3a), H(0xe8c22a), "PARKVIEW - JOBURG", windows: true, sky: Warm, events: "Johannesburg Challenger"),
            V("dublin", "Fitzwilliam Lawn Tennis Club", "Dublin", "IRL", ClubKind.Corridor, 1, Red, Cream, Dark, H(0xe0d6c0), H(0x1f6a3c), H(0xf2f1ed), "FITZWILLIAM - SINCE 1877", timber: true, events: "Dublin Challenger"),
            V("islamabad", "Mushaf Squash Complex", "Islamabad", "PAK", ClubKind.Centre, 0, Red, White, Oak, H(0xdcdcd4), H(0x1f6a3c), H(0xf2f1ed), "MUSHAF SQUASH COMPLEX", bleachers: true, windows: true, sky: Warm, events: "Islamabad Challenger"),
            V("kualalumpur", "National Squash Centre", "Kuala Lumpur", "MAS", ClubKind.Centre, 0, Red, White, Oak, H(0xe2e4e8), H(0x1d3f7a), H(0xe8c22a), "BUKIT JALIL - KUALA LUMPUR", bleachers: true, events: "Kuala Lumpur Challenger"),
            V("mexico", "Pro Squash Polanco", "Mexico City", "MEX", ClubKind.Corridor, 1, Black, White, Oak, H(0x2e3138), H(0x1f7a3a), H(0xc8352e), "POLANCO - CDMX", events: "Mexico City Challenger"),
            V("amsterdam", "Frans Otten Stadion", "Amsterdam", "NED", ClubKind.Shed, 2, Blue, White, Oak, H(0xd4d6da), H(0xf07a1c), H(0x1d3f7a), "FRANS OTTEN STADION", bleachers: true, events: "Amsterdam Challenger"),
            V("kuwait", "Sheikh Salem Al Sabah Squash Complex", "Kuwait City", "KUW", ClubKind.Centre, 0, Red, White, Pine, H(0xefece4), H(0x1f7a3a), H(0xc8352e), "HAWALLY - KUWAIT", bleachers: true, windows: true, sky: Warm, events: "Kuwait City Challenger"),
            V("karachi", "Roshan Khan Jahangir Khan Squash Complex", "Karachi", "PAK", ClubKind.Centre, 0, Red, White, Oak, H(0xdcdcd4), H(0x1d2f5a), H(0xf2f1ed), "RKJK - KARACHI", bleachers: true, events: "Karachi Challenger"),
            V("auckland", "North Shore Squash Club", "Auckland", "NZL", ClubKind.Shed, 2, Black, White, Pine, H(0xd8dadc), H(0x202226), H(0xf2f1ed), "NORTH SHORE - TAKAPUNA", windows: true, sky: Tropic, events: "Auckland Challenger"),
            V("nairobi", "Parklands Sports Club", "Nairobi", "KEN", ClubKind.Corridor, 1, Red, Cream, Oak, H(0xe2d8c4), H(0xc8352e), H(0x1f6a3c), "PARKLANDS - NAIROBI", windows: true, sky: Warm, events: "Nairobi Challenger"),
            // ---- Challenger 3 ----
            V("seremban", "Tan Sri Mohd Said Sports Complex", "Seremban", "MAS", ClubKind.Centre, 0, Red, White, Oak, H(0xe2e2dc), H(0xe8c22a), H(0x202226), "SEREMBAN 2 - NEGERI SEMBILAN", windows: true, sky: Tropic, events: "Seremban Challenger"),
            V("helsinki", "Talihalli", "Helsinki", "FIN", ClubKind.Shed, 2, Blue, White, Pine, H(0xdfe2e6), H(0x1d4fb0), H(0xf2f1ed), "TALIHALLI - HELSINKI", events: "Helsinki Challenger"),
            V("colombo", "Air Force Squash Complex", "Colombo", "SRI", ClubKind.Centre, 0, Red, White, Oak, H(0xdde4ea), H(0x2a5a9a), H(0xe8c22a), "RATMALANA - SRI LANKA", windows: true, sky: Tropic, events: "Colombo Challenger"),
            V("saopaulo", "Esporte Clube Pinheiros", "Sao Paulo", "BRA", ClubKind.Corridor, 1, Black, White, Oak, H(0xe0e2e2), H(0x1f7a3a), H(0xe8c22a), "PINHEIROS - SAO PAULO", windows: true, sky: Tropic, events: "Sao Paulo Challenger"),
            V("stourbridge", "Stourbridge Lawn Tennis and Squash Club", "Stourbridge", "ENG", ClubKind.Corridor, 1, Red, Cream, Dark, H(0xe2d8c4), H(0x1f6a3c), H(0xf2f1ed), "STOURBRIDGE - SINCE 1877", timber: true, events: "Stourbridge Challenger"),
            V("manila", "National Squash Center", "Manila", "PHI", ClubKind.Centre, 0, Red, White, Oak, H(0xe2e4e8), H(0x1d3f9a), H(0xc8352e), "RIZAL MEMORIAL - MANILA", windows: true, sky: Tropic, events: "Manila Challenger"),
            V("theplains", "Wakefield School", "The Plains", "USA", ClubKind.Shed, 1, Blue, White, Oak, H(0xd8dade), H(0x1d2f5a), H(0xe8c22a), "WAKEFIELD - THE PLAINS VA", timber: true, events: "The Plains Challenger"),
            V("funchal", "Galo Active Health Club", "Funchal", "POR", ClubKind.Corridor, 1, Black, White, Pine, H(0xe6e6e2), H(0x1f8a8a), H(0xf2f1ed), "MADEIRA OPEN", windows: true, sky: Tropic, events: "Funchal Challenger"),
            V("lima", "Club Regatas Lima", "Lima", "PER", ClubKind.Corridor, 1, Red, White, Oak, H(0xe2e2de), H(0xc8352e), H(0xf2f1ed), "REGATAS - CHORRILLOS", windows: true, sky: Grey2, events: "Lima Challenger"),
            V("edinburgh", "Edinburgh Sports Club", "Edinburgh", "SCO", ClubKind.Corridor, 1, Red, Cream, Dark, H(0xdcd2bc), H(0x1d2f5a), H(0xf2f1ed), "EDINBURGH SPORTS CLUB", timber: true, events: "Edinburgh Challenger"),
            V("nottingham", "Nottingham Squash Rackets Club", "Nottingham", "ENG", ClubKind.Corridor, 1, Red, White, Dark, H(0xe0d8c8), H(0x1f6a3c), H(0xe8c22a), "THE PARK - NOTTINGHAM", timber: true, events: "Nottingham Challenger"),
            V("amman", "Al Hassan International Squash Centre", "Amman", "JOR", ClubKind.Centre, 0, Red, White, Pine, H(0xeae6dc), H(0x1f6a3c), H(0xc8352e), "AL HASSAN - AMMAN", bleachers: true, windows: true, sky: Warm, events: "Amman Challenger"),
            V("penang", "Nicol David International Squash Centre", "Penang", "MAS", ClubKind.Centre, 0, Red, White, Oak, H(0xe2e6ea), H(0x6a2a9a), H(0xe8c22a), "NICOL DAVID SQUASH CENTRE", bleachers: true, windows: true, sky: Tropic, events: "Penang Challenger"),
            V("singapore", "Serangoon Gardens Country Club", "Singapore", "SGP", ClubKind.Corridor, 1, Red, White, Oak, H(0xe4e4e0), H(0xc8352e), H(0xf2f1ed), "SERANGOON GARDENS", windows: true, sky: Tropic, events: "Singapore Challenger"),
            V("bogota", "Centro de Alto Rendimiento", "Bogota", "COL", ClubKind.Centre, 0, Red, White, Oak, H(0xe0e2e4), H(0xe8c22a), H(0x1d3f9a), "ALTO RENDIMIENTO - BOGOTA", windows: true, sky: Grey2, events: "Bogota Challenger"),
            // ---- generic city clubs (the user, 2026-10-27): no venue found, or not sure of it - a club dressed for its city ----
            V("tashkent", "Tashkent Squash Club", "Tashkent", "UZB", ClubKind.Shed, 1, Blue, White, Oak, H(0xd8dadc), H(0x1d8ab8), H(0x1f7a3a), "TASHKENT OPEN", windows: true, sky: Warm, events: "Tashkent Challenger"),
            V("warsaw", "Warsaw Squash Club", "Warsaw", "POL", ClubKind.Shed, 2, Red, White, Pine, H(0xd6d8dc), H(0xc8352e), H(0xf2f1ed), "WARSZAWA SQUASH", events: "Warsaw Challenger"),
            V("buenosaires", "Buenos Aires Squash Club", "Buenos Aires", "ARG", ClubKind.Corridor, 1, Black, White, Oak, H(0xe2e2de), H(0x5aa8e0), H(0xf2f1ed), "BUENOS AIRES SQUASH", windows: true, sky: Tropic, events: "Buenos Aires Challenger"),
            V("jaipur", "Jaipur Squash Club", "Jaipur", "IND", ClubKind.Corridor, 1, Red, Cream, Oak, H(0xe8d8c0), H(0xd86a4a), H(0x1f7a3a), "THE PINK CITY", windows: true, sky: Warm, events: "Jaipur Challenger"),
            V("seoul", "Seoul Squash Club", "Seoul", "KOR", ClubKind.Shed, 2, Blue, White, Pine, H(0xdcdee2), H(0x1d4fb0), H(0xc8352e), "SEOUL SQUASH", events: "Seoul Challenger"),
            V("vancouver", "Vancouver Squash Club", "Vancouver", "CAN", ClubKind.Corridor, 1, Red, White, Pine, H(0xdfe2e4), H(0xc8352e), H(0xf2f1ed), "VANCOUVER - BC", windows: true, timber: true, sky: Grey2, events: "Vancouver Challenger"),
            V("brest", "Brest Squash Club", "Brest", "FRA", ClubKind.Shed, 1, Blue, White, Oak, H(0xd8dadc), H(0x1d3f7a), H(0xf2f1ed), "BREST - BRETAGNE", events: "Brest Challenger"),
            V("tokyo", "Tokyo Squash Club", "Tokyo", "JPN", ClubKind.Shed, 2, Black, White, Pine, H(0x3a3d44), H(0xc8352e), H(0xf2f1ed), "TOKYO SQUASH", events: "Tokyo Challenger"),
            V("lahore", "Lahore Squash Complex", "Lahore", "PAK", ClubKind.Centre, 0, Red, White, Oak, H(0xe0ded4), H(0x1f5a32), H(0xf2f1ed), "LAHORE - PUNJAB", windows: true, sky: Warm, events: "Lahore Open"),
            V("quimper", "Quimper Squash Club", "Quimper", "FRA", ClubKind.Corridor, 1, Blue, White, Oak, H(0xe2e2de), H(0x1d3f7a), H(0xe8c22a), "QUIMPER - FINISTERE", timber: true, events: "Quimper Challenger"),
            V("nantes", "Nantes Squash Club", "Nantes", "FRA", ClubKind.Shed, 2, Blue, White, Oak, H(0xd8dadc), H(0xe8c22a), H(0x202226), "NANTES SQUASH", events: "Nantes Challenger"),
            V("melbourne", "Melbourne Squash Club", "Melbourne", "AUS", ClubKind.Shed, 2, Red, White, Pine, H(0xdcdee0), H(0x1d2f5a), H(0xf2f1ed), "MELBOURNE - VICTORIA", windows: true, sky: Tropic, events: "Melbourne Challenger"),
            V("stockholm", "Stockholm Squash Club", "Stockholm", "SWE", ClubKind.Corridor, 1, Blue, White, Pine, H(0xe2e4e6), H(0x1d5fb0), H(0xe8c22a), "STOCKHOLM SQUASH", timber: true, events: "Stockholm Challenger"),
            V("boston", "Boston Squash Club", "Boston", "USA", ClubKind.Corridor, 1, Red, White, Oak, H(0xe0dcd4), H(0x8a1f2b), H(0xf2f1ed), "BOSTON - MASSACHUSETTS", timber: true, events: "Boston Challenger"),
        };

        static Dictionary<string, ClubVenue> _byEvent, _byKey;

        /// <summary>The venue a career event is played at, or null (its tier's court).</summary>
        public static ClubVenue ForEvent(string eventName)
        {
            if (_byEvent == null)
            {
                _byEvent = new Dictionary<string, ClubVenue>();
                foreach (var v in All) foreach (var e in v.Events) _byEvent[e] = v;
            }
            if (string.IsNullOrEmpty(eventName)) return null;
            if (eventName.EndsWith(" Qualifying")) eventName = eventName.Substring(0, eventName.Length - " Qualifying".Length);
            return _byEvent.TryGetValue(eventName, out var x) ? x : null;
        }

        public static ClubVenue Find(string key)
        {
            if (_byKey == null) { _byKey = new Dictionary<string, ClubVenue>(); foreach (var v in All) _byKey[v.Key] = v; }
            return key != null && _byKey.TryGetValue(key, out var x) ? x : null;
        }

        /// <summary>A country's colours for its flags along the hall (three stripes).</summary>
        public static Color[] Flag(string nat)
        {
            switch (nat)
            {
                case "IND": return new[] { H(0xf08a24), H(0xf2f1ed), H(0x1f7a3a) };
                case "AUS": return new[] { H(0x1d2f6a), H(0xf2f1ed), H(0xc8352e) };
                case "NGR": return new[] { H(0x1f7a3a), H(0xf2f1ed), H(0x1f7a3a) };
                case "BEL": return new[] { H(0x202226), H(0xe8c22a), H(0xc8352e) };
                case "EGY": return new[] { H(0xc8352e), H(0xf2f1ed), H(0x202226) };
                case "ESP": return new[] { H(0xc8352e), H(0xe8c22a), H(0xc8352e) };
                case "ENG": return new[] { H(0xf2f1ed), H(0xc8352e), H(0xf2f1ed) };
                case "HKG": return new[] { H(0xc8352e), H(0xf2f1ed), H(0xc8352e) };
                case "JPN": return new[] { H(0xf2f1ed), H(0xc8352e), H(0xf2f1ed) };
                case "CZE": return new[] { H(0xf2f1ed), H(0xc8352e), H(0x1d3f9a) };
                case "USA": return new[] { H(0x1d2f6a), H(0xf2f1ed), H(0xc8352e) };
                case "RSA": return new[] { H(0x1f7a3a), H(0xe8c22a), H(0x1d2f6a) };
                case "IRL": return new[] { H(0x1f7a3a), H(0xf2f1ed), H(0xf08a24) };
                case "PAK": return new[] { H(0x1f5a32), H(0xf2f1ed), H(0x1f5a32) };
                case "MAS": return new[] { H(0x1d2f6a), H(0xe8c22a), H(0xc8352e) };
                case "MEX": return new[] { H(0x1f7a3a), H(0xf2f1ed), H(0xc8352e) };
                case "NED": return new[] { H(0xc8352e), H(0xf2f1ed), H(0x1d3f9a) };
                case "KUW": return new[] { H(0x1f7a3a), H(0xf2f1ed), H(0xc8352e) };
                case "NZL": return new[] { H(0x1d2f6a), H(0xc8352e), H(0xf2f1ed) };
                case "KEN": return new[] { H(0x202226), H(0xc8352e), H(0x1f7a3a) };
                case "FIN": return new[] { H(0xf2f1ed), H(0x1d4fb0), H(0xf2f1ed) };
                case "SRI": return new[] { H(0x8a1f2b), H(0xe8c22a), H(0x1f7a3a) };
                case "BRA": return new[] { H(0x1f7a3a), H(0xe8c22a), H(0x1d3f9a) };
                case "PHI": return new[] { H(0x1d3f9a), H(0xf2f1ed), H(0xc8352e) };
                case "POR": return new[] { H(0x1f7a3a), H(0xc8352e), H(0xe8c22a) };
                case "PER": return new[] { H(0xc8352e), H(0xf2f1ed), H(0xc8352e) };
                case "SCO": return new[] { H(0x1d4fb0), H(0xf2f1ed), H(0x1d4fb0) };
                case "JOR": return new[] { H(0x202226), H(0xf2f1ed), H(0x1f7a3a) };
                case "SGP": return new[] { H(0xc8352e), H(0xf2f1ed), H(0xc8352e) };
                case "COL": return new[] { H(0xe8c22a), H(0x1d3f9a), H(0xc8352e) };
                case "UZB": return new[] { H(0x1d8ab8), H(0xf2f1ed), H(0x1f7a3a) };
                case "POL": return new[] { H(0xf2f1ed), H(0xc8352e), H(0xf2f1ed) };
                case "ARG": return new[] { H(0x5aa8e0), H(0xf2f1ed), H(0x5aa8e0) };
                case "KOR": return new[] { H(0xf2f1ed), H(0xc8352e), H(0x1d3f9a) };
                case "CAN": return new[] { H(0xc8352e), H(0xf2f1ed), H(0xc8352e) };
                case "FRA": return new[] { H(0x1d3f9a), H(0xf2f1ed), H(0xc8352e) };
                case "SWE": return new[] { H(0x1d5fb0), H(0xe8c22a), H(0x1d5fb0) };
                default: return new[] { H(0x2b4a8a), H(0xf2f1ed), H(0xc8352e) };
            }
        }

        /// <summary>A sign's text: upper case, only the characters the LED font has.</summary>
        public static string Sign(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s.ToUpperInvariant())
                sb.Append((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == ' ' || ch == '-' || ch == '.' || ch == ':' ? ch : ' ');
            return sb.ToString();
        }
    }
}
