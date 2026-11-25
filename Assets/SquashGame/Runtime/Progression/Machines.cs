// ===========================================================
// Assets/SquashGame/Runtime/Progression/Machines.cs
// 2026-11-24 (the user: "i think we should have more things for players to
// pull like different pulling machienes" - picked: Pro Cards, Signature
// Specials, Walk-on & Celebration, and "something that changes game play a
// bit but not much, just a little progression items"): four more machines,
// each a GearBanner, so they pull, pity and reveal like Gear Drops.
//
//   PRO CARDS    the 100 opponents (50 pros, 50 club players) as cards;
//                rarer the better they are. Copies raise a card's tier:
//                Bronze 1, Silver 3, Gold 6, Holo 10 (then coins).
//   SPECIALS     the Special Shot's cut-in: its colours, its pattern
//                (speed lines, rays, comic dots, glitch bars) and its words.
//                Looks only.
//   WALK-ON      walk-on entrances (the match intro card: colours, a line
//                under your name, confetti / sparks / smoke / spotlights /
//                fireworks) and six new celebrations.
//   PERKS        training perks: a small nudge to a stat or two. Copies level
//                one up (1, 3, 6 copies: level 1..3). Three at most at a time;
//                each stat's total is capped at +MaxPerStat (a 4% edge at
//                most). Quick Match, career and the Gauntlet only - never
//                Local Versus, online, the challenges or the Academy.
//
// Coins only, never real money.
// ===========================================================

using System;
using System.Collections.Generic;
using SquashGame.Profile;
using SquashSim;
using UnityEngine;

namespace SquashGame.Progression
{
    [Serializable] public sealed class ItemCount { public string Id; public int N; }

    public static class Machines
    {
        public const string ProCardsId = "procards", SpecialsId = "specials", WalkOnId = "walkon", PerksId = "perks";

        static List<GearBanner> _list;
        /// <summary>The four machines' banners (not the cosmetics chips: GearDrops.Banners).</summary>
        public static List<GearBanner> All() { if (_list == null) Cosmetics.All(); return _list; }
        public static GearBanner Find(string id) { foreach (var b in All()) if (b.Id == id) return b; return null; }

        static Color H(uint rgb) { return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f); }

        // ---------------- tiers and levels ----------------

        public static readonly string[] TierNames = { "Bronze", "Silver", "Gold", "Holo" };
        public static readonly int[] TierAt = { 1, 3, 6, 10 };
        public static readonly Color[] TierColours = { new Color32(205, 127, 50, 255), new Color32(200, 210, 222, 255), new Color32(255, 200, 60, 255), new Color32(120, 240, 255, 255) };
        public static readonly int[] PerkLevelAt = { 1, 3, 6 };
        public static readonly float[] PerkLevelScale = { 1f, 1.5f, 2f };
        public const int MaxPerks = 3;
        public const float MaxPerStat = 1.0f;

        /// <summary>Whether an item counts copies (pro cards, perks) instead of paying coins for a duplicate.</summary>
        public static bool Counts(Item it) { return it != null && (it.Slot == Slot.ProCard || it.Slot == Slot.Perk); }
        static int[] StepsOf(Item it) { return it.Slot == Slot.ProCard ? TierAt : PerkLevelAt; }

        static Progress P { get { return ProfileStore.Current.Progress; } }
        public static int CopiesOf(string id)
        {
            if (P.Copies == null) return 0;
            foreach (var c in P.Copies) if (c.Id == id) return c.N;
            return 0;
        }

        /// <summary>The step reached with n copies: -1 none, 0.. the tier / level - 1.</summary>
        public static int StepFor(int[] steps, int n) { int s = -1; for (int i = 0; i < steps.Length; i++) if (n >= steps[i]) s = i; return s; }
        /// <summary>A pro card's tier (-1: not owned; 0 Bronze .. 3 Holo).</summary>
        public static int TierOf(string id) { return StepFor(TierAt, CopiesOf(id)); }
        /// <summary>A perk's level, 1..3 (0: not owned).</summary>
        public static int LevelOf(string id) { return StepFor(PerkLevelAt, CopiesOf(id)) + 1; }
        /// <summary>Copies to the next tier / level (0: the top).</summary>
        public static int ToNext(Item it)
        {
            var steps = StepsOf(it); int n = CopiesOf(it.Id);
            foreach (var s in steps) if (n < s) return s - n;
            return 0;
        }

        /// <summary>A copy pulled: counted; what it did - "NEW", "SILVER" / "LEVEL 2", or null (a copy towards the next); and
        /// coins when it was at the top already.</summary>
        public static string Copy(Item it, Rarity r, out int coins)
        {
            coins = 0;
            if (P.Copies == null) P.Copies = new List<ItemCount>();
            var c = P.Copies.Find(x => x.Id == it.Id);
            if (c == null) { c = new ItemCount { Id = it.Id }; P.Copies.Add(c); }
            var steps = StepsOf(it);
            int before = StepFor(steps, c.N);
            if (before == steps.Length - 1) { coins = GearDrops.DupeCoins[(int)r]; return null; }
            c.N++;
            if (!P.Owned.Contains(it.Id)) P.Owned.Add(it.Id);
            int after = StepFor(steps, c.N);
            if (before < 0) { if (it.Slot == Slot.ProCard) CheckSets(); return "NEW"; }
            if (after == before) return null;
            return it.Slot == Slot.ProCard ? TierNames[after].ToUpperInvariant() : "LEVEL " + (after + 1);
        }

        // ---------------- 2026-11-25 the pro cards do things (the user: "1234 should all be added") ----------------
        //   1  your card: on your player tag and your walk-on; a tier badge on that pro in the opponent picker
        //   2  sets: the top 10, all 50 pros, all 50 club players, all 100 - coins and a title each
        //   3  your card coaches you: a small edge in its class's stat, by tier (with the perks, under their cap)
        //   4  the world's top 10 are locked in Quick Match until you have their card

        /// <summary>A pro card's item by opponent id (null: none).</summary>
        public static Item CardOf(string opponentId)
        {
            var b = Find(ProCardsId);
            return b == null || opponentId == null ? null : b.Items.Find(i => i.Ref == opponentId);
        }

        /// <summary>Your card (an owned pro card's id), or null.</summary>
        public static string YourCard
        {
            get { var id = P.ProCard; return !string.IsNullOrEmpty(id) && TierOf(id) >= 0 ? id : null; }
        }

        /// <summary>Make a card yours (again: no card). False: not owned.</summary>
        public static bool SetYourCard(string id)
        {
            if (id != null && TierOf(id) < 0) return false;
            P.ProCard = id != null && id == P.ProCard ? null : id;
            ProfileStore.Save();
            return true;
        }

        /// <summary>"GOLD CARD · Zayn Mahrous" (your tag, your walk-on); null with no card.</summary>
        public static string YourCardLine()
        {
            var id = YourCard; if (id == null) return null;
            var it = Cosmetics.Find(id);
            return TierNames[TierOf(id)].ToUpperInvariant() + " CARD  ·  " + SquashSim.Career.Stars.Describe(it.Ref).name;
        }

        /// <summary>The coach's edge by tier (Bronze .. Holo) on the 0..10 scale (a point is 4%).</summary>
        public static readonly float[] CoachAt = { 0.1f, 0.2f, 0.3f, 0.4f };

        /// <summary>The stat a card coaches (0 speed, 1 stamina, 2 power, 3 control): its pro's class family.</summary>
        public static int CoachStat(Item card)
        {
            if (card == null || card.Ref == null) return -1;
            string fam = PlayerClasses.FamilyOf(SquashGame.UI.SetupFlow.OpponentClass(card.Ref));
            switch (fam) { case "power": return 2; case "speed": return 0; case "accuracy": case "touch": return 3; default: return 1; }
        }

        /// <summary>What your card adds now, stat by stat.</summary>
        public static float[] CoachGives(string id = null)
        {
            var d = new float[4];
            id = id ?? YourCard;
            var it = id != null ? Cosmetics.Find(id) : null;
            int tier = id != null ? TierOf(id) : -1, s = CoachStat(it);
            if (tier >= 0 && s >= 0) d[s] = CoachAt[tier];
            return d;
        }

        public sealed class CardSet
        {
            public string Id, Name, Title;
            public int Coins;
            public System.Func<Item, bool> In;
        }

        public static readonly CardSet[] Sets =
        {
            new CardSet { Id = "top10", Name = "The world's top 10", Title = "title_top10", Coins = 2500, In = i => IsTop(i.Ref, 10) },
            new CardSet { Id = "clubs", Name = "All 50 club players", Title = "title_clubhouse", Coins = 1500, In = i => i.Ref != null && !i.Ref.StartsWith("star:") },
            new CardSet { Id = "pros", Name = "All 50 pros", Title = "title_tourcollector", Coins = 5000, In = i => i.Ref != null && i.Ref.StartsWith("star:") },
            new CardSet { Id = "all", Name = "All 100 players", Title = "title_completionist", Coins = 10000, In = i => true },
        };

        static bool IsTop(string opponentId, int n) { var s = SquashSim.Career.Stars.Find(opponentId); return s != null && s.Rank <= n; }

        public static int SetSize(CardSet s) { return Find(ProCardsId).Items.FindAll(i => s.In(i)).Count; }
        public static int SetHave(CardSet s) { return Find(ProCardsId).Items.FindAll(i => s.In(i) && TierOf(i.Id) >= 0).Count; }
        public static bool SetDone(CardSet s) { return P.CardSets != null && P.CardSets.Contains(s.Id); }

        /// <summary>The sets the last pull completed (the reveal says so; cleared by whoever shows them).</summary>
        public static readonly List<CardSet> JustCompleted = new List<CardSet>();

        /// <summary>Any set newly complete: its coins and title given, once.</summary>
        public static void CheckSets()
        {
            if (P.CardSets == null) P.CardSets = new List<string>();
            foreach (var s in Sets)
            {
                if (P.CardSets.Contains(s.Id) || SetHave(s) < SetSize(s)) continue;
                P.CardSets.Add(s.Id);
                GearDrops.Wallet.Coins += s.Coins;
                if (!P.Owned.Contains(s.Title)) P.Owned.Add(s.Title);
                JustCompleted.Add(s);
            }
        }

        /// <summary>Quick Match: the world's top 10 need their card (the developer's mode opens them).</summary>
        public const int LockedTop = 10;
        public static bool Locked(string opponentId) { return IsTop(opponentId, LockedTop) && !DevMode.On && TierOf(CardOf(opponentId) != null ? CardOf(opponentId).Id : "") < 0; }

        // ---------------- perks ----------------

        public static readonly string[] StatNames = { "Speed", "Stamina", "Power", "Control" };

        public static List<string> Equipped
        {
            get
            {
                if (P.Perks == null) P.Perks = new List<string>();
                P.Perks.RemoveAll(id => Cosmetics.Find(id) == null || !GearDrops.Owns(Cosmetics.Find(id)));
                return P.Perks;
            }
        }

        /// <summary>Train with a perk, or stop (A on it). False: three already (take one off first).</summary>
        public static bool Toggle(string id)
        {
            var it = Cosmetics.Find(id);
            if (it == null || it.Slot != Slot.Perk || !GearDrops.Owns(it)) return false;
            var l = Equipped;
            if (l.Contains(id)) { l.Remove(id); ProfileStore.Save(); return true; }
            if (l.Count >= MaxPerks) return false;
            l.Add(id); ProfileStore.Save();
            return true;
        }

        /// <summary>What one perk adds now, stat by stat (its level's).</summary>
        public static float[] Gives(Item it, int level)
        {
            var d = new float[4];
            if (it == null || level <= 0) return d;
            float k = PerkLevelScale[Mathf.Clamp(level - 1, 0, PerkLevelScale.Length - 1)];
            if (it.Stat >= 0) d[it.Stat] += it.Amount * k;
            if (it.Stat2 >= 0) d[it.Stat2] += it.Amount2 * k;
            return d;
        }

        /// <summary>The equipped perks together, each stat capped.</summary>
        public static float[] Total(IEnumerable<string> ids = null)
        {
            var t = new float[4];
            foreach (var id in ids ?? Equipped)
            {
                var it = Cosmetics.Find(id);
                var g = Gives(it, LevelOf(id));
                for (int k = 0; k < 4; k++) t[k] += g[k];
            }
            if (ids == null) { var c = CoachGives(); for (int k = 0; k < 4; k++) t[k] += c[k]; }       // 2026-11-25 your card coaches you (same cap)
            for (int k = 0; k < 4; k++) t[k] = Mathf.Min(t[k], MaxPerStat);
            return t;
        }

        /// <summary>Attributes with the equipped perks on top (a new set).</summary>
        public static AthleteAttributes Boost(AthleteAttributes a)
        {
            var b = a != null ? a.Clone() : new AthleteAttributes();
            var t = Total();
            b.Speed = Math.Min(AthleteAttributes.Max, b.Speed + t[0]);
            b.Stamina = Math.Min(AthleteAttributes.Max, b.Stamina + t[1]);
            b.Power = Math.Min(AthleteAttributes.Max, b.Power + t[2]);
            b.Control = Math.Min(AthleteAttributes.Max, b.Control + t[3]);
            return b;
        }

        public static string Describe(float[] t)
        {
            var parts = new List<string>();
            for (int k = 0; k < 4; k++) if (t[k] > 0.001f) parts.Add(StatNames[k] + " +" + Percent(t[k]));
            return parts.Count == 0 ? "nothing yet" : string.Join("  ·  ", parts);
        }
        /// <summary>A stat bump as the % it makes (a point on the 0..10 scale is 4%).</summary>
        public static string Percent(float v) { return (v * 4f).ToString("0.#") + "%"; }

        // ---------------- specials and walk-ons ----------------

        public static readonly string[] SpecialPatterns = { "lines", "rays", "dots", "glitch" };
        public static readonly string[] WalkOnFx = { "none", "confetti", "sparks", "smoke", "spotlights", "fireworks" };

        public static Item SpecialOf(Loadout l) { return Cosmetics.Find(l != null ? l.Special : null) ?? Cosmetics.Find("sp_classic"); }
        public static Item WalkOnOf(Loadout l) { return Cosmetics.Find(l != null ? l.WalkOn : null) ?? Cosmetics.Find("wo_classic"); }

        // ---------------- the catalogue ----------------

        internal static void AddTo(Func<Slot, string, string, Color, Color, UnlockKind, Item> add)
        {
            _list = new List<GearBanner>();
            _list.Add(ProCards(add));
            _list.Add(Specials(add));
            _list.Add(WalkOns(add));
            _list.Add(Perks(add));
            // 2026-11-25 the card sets' titles
            foreach (var s in Sets) { var t = add(Slot.Title, s.Title, TitleName(s.Id), Color.white, Color.white, UnlockKind.Challenge); t.Achievement = "Pro Cards: " + s.Name.ToLowerInvariant(); }
        }

        static string TitleName(string set) { switch (set) { case "top10": return "Top Ten Collector"; case "clubs": return "Clubhouse Legend"; case "pros": return "Tour Collector"; default: return "Completionist"; } }

        static GearBanner ProCards(Func<Slot, string, string, Color, Color, UnlockKind, Item> add)
        {
            var b = new GearBanner { Id = ProCardsId, Name = "Pro Cards", Blurb = "Every pro and club player in the game, as a card. Get one again and it shines brighter.", Court = "show" };
            var stars = SquashSim.Career.Stars.All();
            for (int i = 0; i < stars.Count; i++)
            {
                var s = stars[i];
                Rarity r = s.Rank <= 5 ? Rarity.Legendary : s.Rank <= 15 ? Rarity.Epic : s.Rank <= 30 ? Rarity.Rare : Rarity.Uncommon;
                string oid = "star:" + s.Id;
                var it = add(Slot.ProCard, "pc_" + s.Id, SquashSim.Career.Stars.Describe(oid).name, GearDrops.RarityColours[(int)r], H(0x16181d), UnlockKind.Gacha);
                it.Rarity = (int)r; it.Banner = ProCardsId; it.Ref = oid;
                b.Items.Add(it);
            }
            foreach (var c in SquashSim.Career.Stars.Clubs())
            {
                Rarity r = c.Rating >= 76 ? Rarity.Uncommon : Rarity.Common;
                var it = add(Slot.ProCard, "pc_" + c.Id.Replace(':', '_'), c.Name, GearDrops.RarityColours[(int)r], H(0x16181d), UnlockKind.Gacha);
                it.Rarity = (int)r; it.Banner = ProCardsId; it.Ref = c.Id;
                b.Items.Add(it);
            }
            return b;
        }

        static GearBanner Specials(Func<Slot, string, string, Color, Color, UnlockKind, Item> add)
        {
            var free = add(Slot.Special, "sp_classic", "Classic", H(0xc8ff3d), H(0x16181d), UnlockKind.Free);
            free.Style = 0; free.Pose = "SPECIAL SHOT";
            var b = new GearBanner { Id = SpecialsId, Name = "Signature Specials", Blurb = "How your Special Shot looks: the cut-in's colours, pattern and words. Looks only.", Court = "arena" };
            var defs = new (string id, string name, Rarity r, uint a, uint c, int style, string words)[]
            {
                ("sp_dragon", "Dragon Fire", Rarity.Legendary, 0xff3b1c, 0xffd23f, 1, "DRAGON FIRE"),
                ("sp_starfall", "Starfall", Rarity.Legendary, 0x1b1446, 0xfff2a8, 2, "STARFALL"),
                ("sp_thunder", "Thunder Clap", Rarity.Epic, 0x2a3cff, 0xf5f7ff, 0, "THUNDER CLAP"),
                ("sp_glitch", "Glitch", Rarity.Epic, 0x0b0b12, 0x39ffb0, 3, "SYSTEM ERROR"),
                ("sp_blossom", "Cherry Blossom", Rarity.Epic, 0xff8fc0, 0xfff0f6, 1, "FULL BLOOM"),
                ("sp_ice", "Ice Breaker", Rarity.Rare, 0x7fd8ff, 0xffffff, 0, "ICE BREAKER"),
                ("sp_comic", "Comic Pow", Rarity.Rare, 0xffd400, 0xff2d55, 2, "KA-POW!"),
                ("sp_tidal", "Tidal Wave", Rarity.Rare, 0x0a5fa8, 0x8ff3ff, 1, "TIDAL WAVE"),
                ("sp_neon", "Neon Pulse", Rarity.Uncommon, 0xff2bd6, 0x30e8ff, 0, "NEON PULSE"),
                ("sp_shadow", "Shadow Step", Rarity.Uncommon, 0x141418, 0x9a7bff, 3, "SHADOW STEP"),
                ("sp_golden", "Golden Hour", Rarity.Uncommon, 0xff9a2e, 0xfff1c2, 1, "GOLDEN HOUR"),
                ("sp_mint", "Fresh Mint", Rarity.Common, 0x1fd39a, 0xeafff6, 0, "SPECIAL SHOT"),
                ("sp_crimson", "Crimson", Rarity.Common, 0xc4142c, 0xffffff, 0, "SPECIAL SHOT"),
                ("sp_royal", "Royal", Rarity.Common, 0x2f5bd8, 0xffb83b, 2, "SPECIAL SHOT"),
            };
            foreach (var d in defs)
            {
                var it = add(Slot.Special, d.id, d.name, H(d.a), H(d.c), UnlockKind.Gacha);
                it.Rarity = (int)d.r; it.Banner = SpecialsId; it.Style = d.style; it.Pose = d.words;
                b.Items.Add(it);
            }
            return b;
        }

        static GearBanner WalkOns(Func<Slot, string, string, Color, Color, UnlockKind, Item> add)
        {
            var free = add(Slot.WalkOn, "wo_classic", "Classic", H(0xc8ff3d), H(0x16181d), UnlockKind.Free);
            free.Style = 0; free.Pose = "";
            var b = new GearBanner { Id = WalkOnId, Name = "Walk-on & Celebration", Blurb = "Make an entrance and celebrate the win: walk-on effects, your line under your name, new celebrations.", Court = "station" };
            var walk = new (string id, string name, Rarity r, uint a, uint c, int fx, string line)[]
            {
                ("wo_fireworks", "Grand Finale", Rarity.Legendary, 0xff3d6e, 0xffd23f, 5, "THE MAIN EVENT"),
                ("wo_spot", "Spotlight", Rarity.Epic, 0xfff4c2, 0x1a1a22, 4, "ALL EYES ON ME"),
                ("wo_storm", "The Storm", Rarity.Epic, 0x5d6bff, 0xe6ecff, 2, "HERE COMES THE STORM"),
                ("wo_smoke", "Smoke Machine", Rarity.Rare, 0xb9bec8, 0x2b2f38, 3, "OUT OF THE SMOKE"),
                ("wo_party", "Party Time", Rarity.Rare, 0xff6fae, 0x30e8ff, 1, "LET'S HAVE FUN"),
                ("wo_sparks", "Sparks", Rarity.Uncommon, 0xff771c, 0xfff1c2, 2, "READY TO GO"),
                ("wo_confetti", "Confetti", Rarity.Uncommon, 0xc8ff3d, 0xffffff, 1, "THE CHALLENGER"),
                ("wo_cool", "Ice Cool", Rarity.Common, 0x6fb8ff, 0xffffff, 0, "STAY COOL"),
            };
            foreach (var d in walk)
            {
                var it = add(Slot.WalkOn, d.id, d.name, H(d.a), H(d.c), UnlockKind.Gacha);
                it.Rarity = (int)d.r; it.Banner = WalkOnId; it.Style = d.fx; it.Pose = d.line;
                b.Items.Add(it);
            }
            var cels = new (string id, string name, Rarity r)[]
            {
                ("cel_guitar", "Racket guitar", Rarity.Legendary), ("cel_flex", "Double flex", Rarity.Epic), ("cel_point", "Point to the crowd", Rarity.Rare),
                ("cel_salute", "Salute", Rarity.Uncommon), ("cel_shush", "Shush", Rarity.Rare), ("cel_cool", "Arms crossed", Rarity.Common),
            };
            foreach (var d in cels)
            {
                var it = add(Slot.Celebration, d.id, d.name, Color.white, Color.white, UnlockKind.Gacha);
                it.Rarity = (int)d.r; it.Banner = WalkOnId; it.Pose = d.id;
                b.Items.Add(it);
            }
            return b;
        }

        static GearBanner Perks(Func<Slot, string, string, Color, Color, UnlockKind, Item> add)
        {
            var b = new GearBanner { Id = PerksId, Name = "Training Perks", Blurb = "Small edges from training: a little more speed, stamina, power or control. Wear three; get one again to level it up.", Court = "club" };
            // stats: 0 speed, 1 stamina, 2 power, 3 control (sizes on the 0..10 scale; a point is 4%)
            var defs = new (string id, string name, Rarity r, int s, float a, int s2, float a2, uint col)[]
            {
                ("pk_routine", "Champion's Routine", Rarity.Legendary, 3, 0.40f, 2, 0.20f, 0xffc440),
                ("pk_lungs", "Iron Lungs", Rarity.Legendary, 1, 0.40f, 0, 0.20f, 0xffc440),
                ("pk_ghosting", "Ghosting Sessions", Rarity.Epic, 0, 0.30f, 1, 0.15f, 0xbe6eff),
                ("pk_wrist", "Wrist Work", Rarity.Epic, 2, 0.30f, 3, 0.15f, 0xbe6eff),
                ("pk_video", "Video Coach", Rarity.Epic, 3, 0.30f, 0, 0.15f, 0xbe6eff),
                ("pk_sprints", "Sprint Drills", Rarity.Rare, 0, 0.25f, -1, 0, 0x50aaff),
                ("pk_icebath", "Ice Bath", Rarity.Rare, 1, 0.25f, -1, 0, 0x50aaff),
                ("pk_gym", "Gym Session", Rarity.Rare, 2, 0.25f, -1, 0, 0x50aaff),
                ("pk_yoga", "Yoga Mat", Rarity.Uncommon, 3, 0.20f, -1, 0, 0x6ed26e),
                ("pk_insoles", "Pro Insoles", Rarity.Uncommon, 0, 0.20f, -1, 0, 0x6ed26e),
                ("pk_strings", "Tight Strings", Rarity.Uncommon, 2, 0.20f, -1, 0, 0x6ed26e),
                ("pk_gel", "Energy Gel", Rarity.Common, 1, 0.15f, -1, 0, 0x96a0ac),
                ("pk_tape", "Ankle Tape", Rarity.Common, 0, 0.15f, -1, 0, 0x96a0ac),
                ("pk_grip", "Fresh Grip", Rarity.Common, 3, 0.15f, -1, 0, 0x96a0ac),
                ("pk_banana", "Banana Break", Rarity.Common, 2, 0.15f, -1, 0, 0x96a0ac),
            };
            foreach (var d in defs)
            {
                var it = add(Slot.Perk, d.id, d.name, H(d.col), H(0x16181d), UnlockKind.Gacha);
                it.Rarity = (int)d.r; it.Banner = PerksId; it.Stat = d.s; it.Amount = d.a; it.Stat2 = d.s2; it.Amount2 = d.a2;
                b.Items.Add(it);
            }
            return b;
        }
    }
}
