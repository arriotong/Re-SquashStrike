// ===========================================================
// Assets/SquashGame/Runtime/Progression/GearDrops.cs
// P4: Gear Drops - kit and rackets by the pull, after PokéSurvivor's
// gacha. Cosmetic only: nothing here is read by the sim.
//
//   banners   six, themed after the courts; ~25 items each over five
//             rarities (Legendary 2, Epic 4, Rare 6, Uncommon 6, Common 7)
//   odds      Legendary 1.5%, Epic 6.5%, Rare 17%, Uncommon 30%, Common
//             45%; and 1% that the item comes as its GOLD edition
//   rules     a x10 pull has at least one Epic; 70 pulls on a banner
//             without a Legendary make the next one Legendary (the pity
//             counter is the banner's own); a duplicate pays coins; a
//             gold edition never repeats
//   currency  tickets (1 pull) and x10 tickets, earned by playing only -
//             never bought; coins from duplicates buy the Pro Shop's
//             daily 8 and gold-plating an item you own
//
// Every roll comes from the profile's gacha seed and its pull count, so
// reloading can't re-roll a pull.
// ===========================================================

using System;
using System.Collections.Generic;
using SquashGame.Profile;
using UnityEngine;

namespace SquashGame.Progression
{
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

    public sealed class GearBanner
    {
        public string Id, Name, Blurb, Court;
        public readonly List<Item> Items = new List<Item>();          // the standard editions
        public List<Item> Of(Rarity r) { return Items.FindAll(i => i.Rarity == (int)r); }
    }

    /// <summary>One item out of a pull.</summary>
    public sealed class Drop
    {
        public Item Item;
        public Rarity Rarity;
        public bool Gold, New;
        public int Coins;           // a duplicate's coins (0 when new)
    }

    [Serializable] public sealed class BannerPity { public string Id; public int Count; }

    [Serializable]
    public sealed class Wallet
    {
        public int Tickets, TenTickets, Coins;
        public int WinsTowardTicket;          // (before batch 3: every 3rd match won paid a ticket)
        public int CapDay = -1, CapCoins;     // batch 3: today's coins from local and casual online matches (the soft cap)
    }

    [Serializable]
    public sealed class GachaState
    {
        public uint Seed;
        public int Pulls;
        public List<BannerPity> Pity = new List<BannerPity>();
        public int ShopDay = -1;
        public List<string> ShopBought = new List<string>();
    }

    public static class GearDrops
    {
        public static readonly string[] RarityNames = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };
        public static readonly double[] Odds = { 0.45, 0.30, 0.17, 0.065, 0.015 };
        public const double GoldChance = 0.01;
        public const int Pity = 70, TenPull = 10, ShopSize = 8, StartTickets = 10;
        public static readonly int[] DupeCoins = { 10, 25, 60, 150, 400 };
        public static readonly int[] ShopPrice = { 120, 300, 700, 1800, 5000 };
        public static readonly int[] GoldPlateCost = { 300, 700, 1600, 4000, 10000 };
        public static readonly Color[] RarityColours =
        {
            new Color32(150, 160, 172, 255), new Color32(110, 210, 110, 255), new Color32(80, 170, 255, 255),
            new Color32(190, 110, 255, 255), new Color32(255, 196, 64, 255),
        };
        public static readonly Color GoldAccent = new Color32(255, 215, 106, 255);

        // ---------------- the catalogue ----------------

        static List<GearBanner> _banners;
        public static List<GearBanner> Banners() { if (_banners == null) Cosmetics.All(); return _banners; }
        public static GearBanner Banner(string id) { foreach (var b in Banners()) if (b.Id == id) return b; return null; }

        static Color H(uint rgb) { return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f); }

        /// <summary>A banner colour, nudged per item (hue, saturation, value) so no two pieces look the same.</summary>
        static Color Shade(Color c, int i)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            h = Mathf.Repeat(h + ((i * 37) % 11 - 5) * 0.014f, 1f);
            s = Mathf.Clamp01(s * (0.8f + ((i * 13) % 5) * 0.1f));
            v = Mathf.Clamp(v * (0.78f + ((i * 7) % 6) * 0.08f), 0.06f, 1f);
            return Color.HSVToRGB(h, s, v);
        }

        // the order a banner's items are made in: rarity, slot (25)
        static readonly (Rarity r, Slot s)[] Plan =
        {
            (Rarity.Legendary, Slot.Frame), (Rarity.Legendary, Slot.Trail),
            (Rarity.Epic, Slot.Shirt), (Rarity.Epic, Slot.Frame), (Rarity.Epic, Slot.Shoes), (Rarity.Epic, Slot.Banner),
            (Rarity.Rare, Slot.Shirt), (Rarity.Rare, Slot.Shorts), (Rarity.Rare, Slot.Grip), (Rarity.Rare, Slot.Strings), (Rarity.Rare, Slot.Headband), (Rarity.Rare, Slot.Trail),
            (Rarity.Uncommon, Slot.Socks), (Rarity.Uncommon, Slot.Wristband), (Rarity.Uncommon, Slot.Shoes), (Rarity.Uncommon, Slot.Shorts), (Rarity.Uncommon, Slot.Grip), (Rarity.Uncommon, Slot.Title),
            (Rarity.Common, Slot.Shirt), (Rarity.Common, Slot.Socks), (Rarity.Common, Slot.Headband), (Rarity.Common, Slot.Wristband), (Rarity.Common, Slot.Strings), (Rarity.Common, Slot.Shorts), (Rarity.Common, Slot.Banner),
        };

        /// <summary>The banners: id, name, blurb, court, a palette (main colours, then accents) and 25 names in Plan's order.</summary>
        static readonly (string id, string name, string blurb, string court, uint[] pal, string[] names)[] Defs =
        {
            ("club", "Club Classics", "Varsity colours from the club's old trophy cabinet.", "club",
             new uint[] { 0x1f5a35, 0x6e1e2a, 0xf2f2ee, 0x1d2a4a, 0xc8a24a, 0xd8382f },
             new[] { "Founder's Crest", "Old Boys' Comet", "Heritage", "Trophy Room", "Pavilion", "Club Crest", "Ivy League", "Blazer", "Leather Grip", "Gut", "Rowing Stripe", "Clubhouse Spark",
                     "Varsity", "Prep", "Brogue", "Chino", "Twine", "Club Stalwart", "Cricket", "Argyle", "Cable Knit", "Terry", "Catgut", "Flannel", "Honours Board" }),
            ("rooftop", "City Rooftop", "Dusk over the skyline: violet, coral and city lights.", "rooftop",
             new uint[] { 0x2a1a4a, 0xff6f61, 0xffb14a, 0x3d2c8d, 0xff2bd6, 0x0e1430 },
             new[] { "Skyline", "Last Light", "Golden Hour", "Penthouse", "Neon Sign", "Billboard", "Dusk", "Fire Escape", "Rooftop", "Antenna", "Sunset", "Street Glow",
                     "Taxi", "Graffiti", "Parkour", "Overpass", "Rebar", "Rooftop Regular", "Brick", "Tag", "Chimney", "Gutter", "Wire", "Alley", "Night Shift" }),
            ("station", "Station Hall", "Brass, marble and a ceiling of stars.", "station",
             new uint[] { 0xb8862b, 0x1c1e22, 0xe4d6b8, 0x2b2f38, 0x14203a, 0x8a9099 },
             new[] { "Grand Concourse", "Starry Vault", "Departures", "Clockwork", "Brass Rail", "Timetable", "Platform 9", "Conductor", "Ticket Stub", "Signal", "Porter", "Lantern",
                     "Commuter", "Terminus", "Rail", "Carriage", "Rivet", "Season Ticket", "Marble", "Steam", "Whistle", "Valise", "Coupling", "Sleeper", "Arrivals" }),
            ("desert", "Desert Night", "Sand, gold and the pyramids lit up behind the glass.", "desert",
             new uint[] { 0xc49a5c, 0x0e2a3a, 0xff8a1c, 0x2e1a0c, 0xffd76a, 0x6fb8ff },
             new[] { "Pharaoh", "Shooting Star", "Sandstorm", "Scarab", "Oasis", "Dune Sea", "Mirage", "Nomad", "Caravan", "Sun Disc", "Lotus", "Desert Wind",
                     "Dune", "Obelisk", "Sandal", "Camel", "Papyrus", "Sand Walker", "Sahara", "Ochre", "Palm", "Kohl", "Reed", "Nile", "Night Sky" }),
            ("arena", "Arena Neon", "LED ribbons and a sold-out crowd.", "arena",
             new uint[] { 0x10061e, 0x00e5ff, 0xff2bd6, 0xc8ff3d, 0x2f5bd8, 0xff4a40 },
             new[] { "Headliner", "Laser Show", "Encore", "Pyro", "Spotlight", "Jumbotron", "Strobe", "Hype", "Glowstick", "Bassline", "Crowd Surf", "Afterglow",
                     "Mosh", "Backstage", "Riser", "Pit", "Cable", "Arena Regular", "Setlist", "Lanyard", "Merch", "Wristband", "Feedback", "Soundcheck", "Main Stage" }),
            ("show", "Show Court Gold", "The glass court's finest: blue, white and gold.", "show",
             new uint[] { 0x14306e, 0xffffff, 0xffb83b, 0x0a1a3a, 0x3ba3ff, 0xc0c8d4 },
             new[] { "Champion", "Golden Ace", "World Tour", "Crown", "Centre Court", "Glass Box", "Front Wall", "Tin", "Nick", "Volley", "Boast", "Kill Shot",
                     "Drive", "Lob", "Drop", "Rally", "Let", "Show Court Star", "Service Box", "T Position", "Short Line", "Half Court", "Out Line", "Knock-up", "Match Ball" }),
        };

        /// <summary>Called by Cosmetics.All(): add every banner's items and their gold editions to the catalogue.</summary>
        internal static void AddTo(Func<Slot, string, string, Color, Color, UnlockKind, Item> add)
        {
            _banners = new List<GearBanner>();
            foreach (var d in Defs)
            {
                var b = new GearBanner { Id = d.id, Name = d.name, Blurb = d.blurb, Court = d.court };
                for (int i = 0; i < Plan.Length; i++)
                {
                    var (r, slot) = Plan[i];
                    // colours: the banner's palette, a different pairing for each item (rarer: the accents)
                    Color a = Shade(H(d.pal[(i * 5 + (int)r * 3) % d.pal.Length]), i);
                    Color c = Shade(H(d.pal[(i * 5 + (int)r * 3 + 2) % d.pal.Length]), i + 7);
                    string id = "gd_" + d.id + "_" + i;
                    var it = add(slot, id, d.names[i], a, slot == Slot.Shirt || slot == Slot.Banner ? c : a, UnlockKind.Gacha);
                    it.Rarity = (int)r; it.Banner = d.id;
                    it.Glow = slot == Slot.Frame && r >= Rarity.Epic;
                    b.Items.Add(it);
                    // the gold edition: a gold accent, gold on the rest
                    var g = add(slot, id + "_gold", d.names[i] + " Gold", slot == Slot.Shirt || slot == Slot.Banner ? a : Color.Lerp(a, GoldAccent, 0.55f), GoldAccent, UnlockKind.Gacha);
                    g.Rarity = (int)r; g.Banner = d.id; g.Gold = true; g.GoldOf = id; g.Glow = slot == Slot.Frame || it.Glow;
                }
                _banners.Add(b);
            }
        }

        // ---------------- state ----------------

        static Progress P { get { return ProfileStore.Current.Progress; } }
        public static Wallet Wallet { get { if (P.Wallet == null) P.Wallet = new Wallet(); return P.Wallet; } }
        public static GachaState State
        {
            get
            {
                if (P.Gacha == null) P.Gacha = new GachaState();
                if (P.Gacha.Seed == 0) P.Gacha.Seed = unchecked((uint)Environment.TickCount * 2654435761u) | 1u;
                return P.Gacha;
            }
        }

        public static int PityOf(string banner) { foreach (var p in State.Pity) if (p.Id == banner) return p.Count; return 0; }
        static void SetPity(string banner, int n)
        {
            foreach (var p in State.Pity) if (p.Id == banner) { p.Count = n; return; }
            State.Pity.Add(new BannerPity { Id = banner, Count = n });
        }

        static uint Hash(uint x) { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return x; }
        /// <summary>0..1 from the profile's seed, its pull count and a salt.</summary>
        static double Roll(int pull, uint salt) { return Hash(State.Seed ^ Hash((uint)pull * 2654435761u + salt)) / 4294967296.0; }

        public static bool Owns(Item it) { return it != null && P.Owned.Contains(it.Id); }
        public static int OwnedIn(GearBanner b, out int gold)
        {
            int n = 0; gold = 0;
            foreach (var it in b.Items) { if (Owns(it)) n++; if (P.Owned.Contains(it.Id + "_gold")) gold++; }
            return n;
        }

        public static bool CanPull(int n) { return n == TenPull ? Wallet.TenTickets > 0 || Wallet.Tickets >= TenPull : Wallet.Tickets >= n; }

        // ---------------- pulling ----------------

        /// <summary>Pull 1 or 10 on a banner (paid in tickets: a x10 ticket first for a x10). Empty when it can't be paid. Saves.</summary>
        public static List<Drop> Pull(string bannerId, int n)
        {
            var b = Banner(bannerId);
            var drops = new List<Drop>();
            if (b == null || (n != 1 && n != TenPull) || !CanPull(n)) return drops;
            if (n == TenPull && Wallet.TenTickets > 0) Wallet.TenTickets--; else Wallet.Tickets -= n;
            for (int i = 0; i < n; i++)
            {
                int pull = State.Pulls++;
                Rarity r = RarityFor(Roll(pull, 1));
                if (PityOf(b.Id) >= Pity - 1) r = Rarity.Legendary;                                  // the 70th without one
                if (n == TenPull && i == n - 1 && r < Rarity.Epic && !drops.Exists(x => x.Rarity >= Rarity.Epic)) r = Rarity.Epic;   // a x10 has an Epic
                SetPity(b.Id, r == Rarity.Legendary ? 0 : PityOf(b.Id) + 1);
                var pool = b.Of(r);
                var item = pool[(int)(Roll(pull, 2) * pool.Count) % pool.Count];
                bool gold = Roll(pull, 3) < GoldChance && !P.Owned.Contains(item.Id + "_gold");       // a gold edition never repeats
                drops.Add(Give(gold ? Cosmetics.Find(item.Id + "_gold") : item, r, gold));
            }
            ProfileStore.Save();
            return drops;
        }

        public static Rarity RarityFor(double u)
        {
            double acc = 0;
            for (int k = (int)Rarity.Legendary; k >= 0; k--) { acc += Odds[k]; if (u < acc) return (Rarity)k; }
            return Rarity.Common;
        }

        static Drop Give(Item it, Rarity r, bool gold)
        {
            var d = new Drop { Item = it, Rarity = r, Gold = gold };
            if (P.Owned.Contains(it.Id)) { d.Coins = DupeCoins[(int)r]; Wallet.Coins += d.Coins; }
            else { P.Owned.Add(it.Id); d.New = true; }
            return d;
        }

        // ---------------- batch 3: coins buy tickets and the kit store ----------------

        public const int TicketPrice = 150;

        public static bool BuyTicket()
        {
            if (Wallet.Coins < TicketPrice) return false;
            Wallet.Coins -= TicketPrice;
            Wallet.Tickets++;
            ProfileStore.Save();
            return true;
        }

        /// <summary>The kit store: the kit that levels used to give, for coins (dearer the later its old level).</summary>
        public static bool InStore(Item it) { return it != null && it.Unlock == UnlockKind.Level; }
        public static int StorePrice(Item it) { return InStore(it) ? 100 + (it.Level * 8 + 5) / 10 * 10 : 0; }

        public static bool BuyFromStore(Item it)
        {
            if (!InStore(it) || Owns(it) || Wallet.Coins < StorePrice(it)) return false;
            Wallet.Coins -= StorePrice(it);
            P.Owned.Add(it.Id);
            ProfileStore.Save();
            return true;
        }

        // ---------------- coins: gold-plating and the Pro Shop ----------------

        /// <summary>The coins to gold-plate an item you own (0 when it can't be: not a banner item, gold already).</summary>
        public static int PlateCost(Item it)
        {
            if (it == null || it.Unlock != UnlockKind.Gacha || it.Gold || !Owns(it) || P.Owned.Contains(it.Id + "_gold")) return 0;
            return GoldPlateCost[Mathf.Clamp(it.Rarity, 0, 4)];
        }

        public static bool GoldPlate(Item it)
        {
            int cost = PlateCost(it);
            if (cost <= 0 || Wallet.Coins < cost) return false;
            Wallet.Coins -= cost;
            P.Owned.Add(it.Id + "_gold");
            ProfileStore.Save();
            return true;
        }

        /// <summary>Today's 8 in the Pro Shop (the same all day, new at midnight): standard editions from every banner.</summary>
        public static List<Item> ShopToday()
        {
            int day = Challenges.DayKey(Challenges.Now());
            if (State.ShopDay != day) { State.ShopDay = day; State.ShopBought = new List<string>(); }
            var pool = new List<Item>();
            foreach (var b in Banners()) pool.AddRange(b.Items);
            var list = new List<Item>();
            uint h = Hash((uint)day * 0x9E3779B9u + 0xA11CEu);
            while (list.Count < ShopSize && pool.Count > 0)
            {
                h = Hash(h + 0x6D2B79F5u);
                var it = pool[(int)(h % (uint)pool.Count)];
                pool.Remove(it);
                if (it.Rarity == (int)Rarity.Legendary && list.Exists(x => x.Rarity == (int)Rarity.Legendary)) continue;   // one Legendary a day at most
                list.Add(it);
            }
            return list;
        }

        public static int Price(Item it) { return ShopPrice[Mathf.Clamp(it.Rarity, 0, 4)]; }

        public static bool Buy(Item it)
        {
            if (it == null || Owns(it) || !ShopToday().Contains(it) || Wallet.Coins < Price(it)) return false;
            Wallet.Coins -= Price(it);
            P.Owned.Add(it.Id);
            State.ShopBought.Add(it.Id);
            ProfileStore.Save();
            return true;
        }

        public static string RestockIn() { var now = Challenges.Now(); var left = now.Date.AddDays(1) - now; return left.Hours + "h " + left.Minutes + "m"; }
    }
}
