// ===========================================================
// Assets/SquashGame/Runtime/Progression/Cosmetics.cs
// P2-M5: the Locker Room's items, and the kit they make.
//
// Cosmetic only: an item is a colour (or a pose, a title, a banner)
// for the drawn athlete or the player card. Nothing here is read by
// the sim - the checksum test proves it.
//
//   Catalogue   every item: slot, name, colours, how it unlocks
//               (free / a level / an achievement)
//   Loadout     what is worn (profile); Kit = the colours it makes
//   AiKit       a pro's kit, from a seed; AvoidClash: an away shirt
//               when the two shirts would look alike
// ===========================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquashGame.Progression
{
    public enum Slot { Shirt, Shorts, Socks, Shoes, Skin, Headband, Wristband, Frame, Grip, Strings, Celebration, Banner, Title, Hair, HairColour, Body }

    public enum UnlockKind { Free, Level, Achievement }

    public sealed class Item
    {
        public string Id, Name;
        public Slot Slot;
        public Color A, B;                 // main colour; second colour (shirt sleeves, banner stripe)
        public bool None;                  // "no headband"
        public string Pose;                // celebrations: the pose library's name
        public int Style;                  // PR3: hair - the drawn style (AthleteRig.HairStyles)
        public UnlockKind Unlock;
        public int Level;                  // UnlockKind.Level
        public string Achievement;         // UnlockKind.Achievement

        public string HowToUnlock
        {
            get
            {
                switch (Unlock)
                {
                    case UnlockKind.Level: return "Level " + Level;
                    case UnlockKind.Achievement: { var a = Achievements.Find(Achievement); return "Achievement: " + (a != null ? a.Name : Achievement); }
                    default: return "";
                }
            }
        }
    }

    /// <summary>The colours an athlete is drawn in.</summary>
    public struct Kit
    {
        public Color ShirtBody, ShirtSleeves, Shorts, Socks, Shoes, Skin, Headband, Wristband, Frame, Grip, Strings;
        public bool HasHeadband, HasWristband;
        public int HairStyle;              // PR3: 0 shaved .. AthleteRig.HairStyles - 1
        public Color Hair;
        public bool Female;                // 2026-10-24: the body (AthleteSkin)
    }

    [Serializable]
    public sealed class Loadout
    {
        public string Shirt = "shirt_charcoal", Shorts = "shorts_black", Socks = "socks_white", Shoes = "shoes_white", Skin = "skin_3",
                      Headband = "headband_none", Wristband = "wristband_none", Frame = "frame_graphite", Grip = "grip_black",
                      Strings = "strings_white", Celebration = "cel_fist", Banner = "banner_charcoal", Title = "title_rookie",
                      Hair = "hair_short", HairColour = "haircol_darkbrown", Body = "body_male";

        public string Get(Slot s)
        {
            switch (s)
            {
                case Slot.Shirt: return Shirt; case Slot.Shorts: return Shorts; case Slot.Socks: return Socks; case Slot.Shoes: return Shoes;
                case Slot.Skin: return Skin; case Slot.Headband: return Headband; case Slot.Wristband: return Wristband; case Slot.Frame: return Frame;
                case Slot.Grip: return Grip; case Slot.Strings: return Strings; case Slot.Celebration: return Celebration; case Slot.Banner: return Banner;
                case Slot.Hair: return Hair; case Slot.HairColour: return HairColour; case Slot.Body: return Body;
                default: return Title;
            }
        }

        public void Set(Slot s, string id)
        {
            switch (s)
            {
                case Slot.Shirt: Shirt = id; break; case Slot.Shorts: Shorts = id; break; case Slot.Socks: Socks = id; break; case Slot.Shoes: Shoes = id; break;
                case Slot.Skin: Skin = id; break; case Slot.Headband: Headband = id; break; case Slot.Wristband: Wristband = id; break; case Slot.Frame: Frame = id; break;
                case Slot.Grip: Grip = id; break; case Slot.Strings: Strings = id; break; case Slot.Celebration: Celebration = id; break; case Slot.Banner: Banner = id; break;
                case Slot.Hair: Hair = id; break; case Slot.HairColour: HairColour = id; break; case Slot.Body: Body = id; break;
                default: Title = id; break;
            }
        }

        public Loadout Clone() { return (Loadout)MemberwiseClone(); }
    }

    public static class Cosmetics
    {
        public static readonly Slot[] KitSlots = { Slot.Shirt, Slot.Shorts, Slot.Socks, Slot.Shoes, Slot.Skin, Slot.Headband, Slot.Wristband, Slot.Frame, Slot.Grip, Slot.Strings, Slot.Hair, Slot.HairColour, Slot.Body };
        public static readonly string[] SlotNames = { "Shirt", "Shorts", "Socks", "Shoes", "Skin tone", "Headband", "Wristband", "Racket frame", "Grip", "Strings", "Celebration", "Card banner", "Title", "Hair", "Hair colour", "Body" };

        /// <summary>PR3: the hair styles, in AthleteRig's order (item id suffix, name).</summary>
        public static readonly (string id, string name)[] HairStyles =
            { ("shaved", "Shaved"), ("crop", "Buzzed"), ("short", "Side part"), ("curls", "Long"), ("bun", "Buns"), ("ponytail", "Buzzed + beard"), ("mohawk", "Parted + beard") };
        static readonly (string id, string name, uint rgb)[] HairColours =
        {
            ("black", "Black", 0x17130f), ("darkbrown", "Dark brown", 0x352418), ("brown", "Brown", 0x5e3d23), ("auburn", "Auburn", 0x7a3418),
            ("blonde", "Blonde", 0xc49a5c), ("platinum", "Platinum", 0xe4d6b8), ("grey", "Grey", 0x8d8b88),
        };

        static List<Item> _all;
        static Dictionary<string, Item> _byId;

        static Color H(uint rgb) { return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f); }

        // A palette shared by most slots: name, colour.
        static readonly (string id, string name, uint rgb)[] Colours =
        {
            ("white", "White", 0xf2f2ee), ("black", "Black", 0x1c1e22), ("charcoal", "Charcoal", 0x2b2f38), ("lime", "Lime", 0xc8ff3d),
            ("red", "Red", 0xd8382f), ("royal", "Royal blue", 0x2f5bd8), ("navy", "Navy", 0x1d2a4a), ("orange", "Orange", 0xff771c),
            ("teal", "Teal", 0x1fa59a), ("purple", "Purple", 0x7a3fd0), ("sky", "Sky", 0x6fb8ff), ("maroon", "Maroon", 0x6e1e2a),
            ("forest", "Forest", 0x1f5a35), ("pink", "Pink", 0xff6fae), ("gold", "Gold", 0xffb83b), ("grey", "Grey", 0x8a9099),
        };

        static Item Add(Slot slot, string id, string name, Color a, Color b, UnlockKind u = UnlockKind.Level)
        {
            var it = new Item { Id = id, Name = name, Slot = slot, A = a, B = b, Unlock = u };
            _all.Add(it);
            return it;
        }

        static void Colourways(Slot slot, string prefix, string[] ids, int free)
        {
            for (int i = 0; i < ids.Length; i++)
                foreach (var c in Colours)
                    if (c.id == ids[i]) Add(slot, prefix + "_" + c.id, c.name, H(c.rgb), H(c.rgb), i < free ? UnlockKind.Free : UnlockKind.Level);
        }

        public static List<Item> All()
        {
            if (_all != null) return _all;
            _all = new List<Item>();
            // ---- kit ----
            Colourways(Slot.Shirt, "shirt", new[] { "charcoal", "white", "lime", "red", "royal", "orange", "teal", "purple", "sky", "maroon", "forest", "pink" }, 2);
            Add(Slot.Shirt, "shirt_navygold", "Navy / gold", H(0x1d2a4a), H(0xffb83b));
            Add(Slot.Shirt, "shirt_blacklime", "Black / lime", H(0x1c1e22), H(0xc8ff3d));
            Colourways(Slot.Shorts, "shorts", new[] { "black", "white", "navy", "grey", "red", "royal", "lime", "teal", "maroon", "orange" }, 2);
            Colourways(Slot.Socks, "socks", new[] { "white", "black", "lime", "red", "navy", "orange", "teal", "pink" }, 2);
            Colourways(Slot.Shoes, "shoes", new[] { "white", "black", "lime", "red", "royal", "orange", "gold", "pink", "teal", "purple" }, 2);
            uint[] skins = { 0xf3d2b8, 0xe3b48f, 0xc98e66, 0xa56b46, 0x7c4a2e, 0x4f2f1d };
            for (int i = 0; i < skins.Length; i++) Add(Slot.Skin, "skin_" + (i + 1), "Tone " + (i + 1), H(skins[i]), H(skins[i]), UnlockKind.Free);
            // PR3: hair - who you are, not a reward, so every style and natural colour is free
            for (int i = 0; i < HairStyles.Length; i++) Add(Slot.Hair, "hair_" + HairStyles[i].id, HairStyles[i].name, Color.white, Color.white, UnlockKind.Free).Style = i;
            foreach (var c in HairColours) Add(Slot.HairColour, "haircol_" + c.id, c.name, H(c.rgb), H(c.rgb), UnlockKind.Free);
            Add(Slot.Body, "body_male", "Male", Color.white, Color.white, UnlockKind.Free);
            Add(Slot.Body, "body_female", "Female", Color.white, Color.white, UnlockKind.Free).Style = 1;
            Add(Slot.Headband, "headband_none", "None", Color.clear, Color.clear, UnlockKind.Free).None = true;
            Colourways(Slot.Headband, "headband", new[] { "white", "black", "lime", "red", "royal", "gold" }, 0);
            Add(Slot.Wristband, "wristband_none", "None", Color.clear, Color.clear, UnlockKind.Free).None = true;
            Colourways(Slot.Wristband, "wristband", new[] { "white", "black", "lime", "red", "royal", "gold" }, 0);
            // ---- racket ----
            Add(Slot.Frame, "frame_graphite", "Graphite", H(0x5d656f), H(0x5d656f), UnlockKind.Free);
            Colourways(Slot.Frame, "frame", new[] { "white", "lime", "red", "royal", "gold", "orange", "black", "teal", "purple" }, 1);
            Colourways(Slot.Grip, "grip", new[] { "black", "white", "lime", "red", "royal", "orange" }, 1);
            Colourways(Slot.Strings, "strings", new[] { "white", "lime", "red", "gold" }, 1);
            // ---- celebrations (pose library names; see Celebrations) ----
            string[,] cels = { { "cel_fist", "Fist pump", "cel_fist" }, { "cel_raise", "Racket raise", "cel_raise" }, { "cel_wide", "Arms wide", "cel_wide" },
                               { "cel_sky", "Point to the sky", "cel_sky" }, { "cel_bow", "Bow", "cel_bow" }, { "cel_knee", "Roar", "cel_knee" } };
            for (int i = 0; i < cels.GetLength(0); i++) Add(Slot.Celebration, cels[i, 0], cels[i, 1], Color.white, Color.white, i == 0 ? UnlockKind.Free : UnlockKind.Level).Pose = cels[i, 2];
            // ---- the player card ----
            Add(Slot.Banner, "banner_charcoal", "Charcoal", H(0x16181d), H(0xc8ff3d), UnlockKind.Free);
            Add(Slot.Banner, "banner_lime", "Lime stripe", H(0x16181d), H(0xc8ff3d), UnlockKind.Free).B = H(0xc8ff3d);
            foreach (var (id, name, a, b) in new[] { ("banner_red", "Red", 0x3a1414u, 0xff4a40u), ("banner_blue", "Blue", 0x14203au, 0x3ba3ffu), ("banner_gold", "Gold", 0x2a2010u, 0xffb83bu),
                                                       ("banner_purple", "Purple", 0x22143au, 0xb942f5u), ("banner_orange", "Orange", 0x2e1a0cu, 0xff771cu), ("banner_teal", "Teal", 0x0e2a28u, 0x1fa59au),
                                                       ("banner_blackgold", "Black & gold", 0x0a0a0au, 0xffd76au), ("banner_neon", "Neon", 0x10061eu, 0xff2bd6u) })
                Add(Slot.Banner, id, name, H(a), H(b));
            // ---- titles (most come from achievements: Achievements.Reward) ----
            Add(Slot.Title, "title_rookie", "Rookie", Color.white, Color.white, UnlockKind.Free);
            foreach (var (id, name) in new[] { ("title_graduate", "Academy Graduate"), ("title_clubchamp", "Club Champion"), ("title_regional", "Regional Hero"),
                                               ("title_challenger", "Challenger"), ("title_tour", "Tour Winner"), ("title_major", "Major Champion"),
                                               ("title_world", "World Champion"), ("title_no1", "World No. 1"), ("title_nick", "Nick Hunter"),
                                               ("title_marathon", "Marathon Man"), ("title_sharpshooter", "Sharpshooter"), ("title_comeback", "Comeback Kid") })
                Add(Slot.Title, id, name, Color.white, Color.white, UnlockKind.Achievement);

            // achievements' rewards: those items unlock there, not on the level table
            foreach (var a in Achievements.All())
                if (a.Reward != null) { var it = Find(a.Reward); if (it != null) { it.Unlock = UnlockKind.Achievement; it.Achievement = a.Id; } }
            AssignLevels();
            return _all;
        }

        /// <summary>The rest go on the level table, spread so each level gives something different (levels 2..50).</summary>
        static void AssignLevels()
        {
            var bySlot = new Dictionary<Slot, Queue<Item>>();
            foreach (var it in _all)
            {
                if (it.Unlock != UnlockKind.Level) continue;
                if (!bySlot.TryGetValue(it.Slot, out var q)) bySlot[it.Slot] = q = new Queue<Item>();
                q.Enqueue(it);
            }
            var order = new List<Item>();
            Slot[] rota = { Slot.Shirt, Slot.Frame, Slot.Shoes, Slot.Banner, Slot.Shorts, Slot.Headband, Slot.Socks, Slot.Celebration, Slot.Wristband, Slot.Grip, Slot.Strings };
            bool any = true;
            while (any)
            {
                any = false;
                foreach (var s in rota) if (bySlot.TryGetValue(s, out var q) && q.Count > 0) { order.Add(q.Dequeue()); any = true; }
            }
            int levels = Rewards.MaxLevel - 1;                     // levels 2..50
            for (int i = 0; i < order.Count; i++) order[i].Level = 2 + (int)((long)i * levels / order.Count);
        }

        public static Item Find(string id)
        {
            if (_byId == null) { _byId = new Dictionary<string, Item>(); foreach (var it in All()) _byId[it.Id] = it; }
            return id != null && _byId.TryGetValue(id, out var x) ? x : null;
        }

        public static List<Item> InSlot(Slot s) { var l = new List<Item>(); foreach (var it in All()) if (it.Slot == s) l.Add(it); return l; }

        /// <summary>The items a level gives.</summary>
        public static List<Item> AtLevel(int level) { var l = new List<Item>(); foreach (var it in All()) if (it.Unlock == UnlockKind.Level && it.Level == level) l.Add(it); return l; }

        public static Color Of(string id, Color fallback) { var it = Find(id); return it != null ? it.A : fallback; }

        /// <summary>A loadout's colours.</summary>
        public static Kit KitOf(Loadout l)
        {
            var shirt = Find(l.Shirt) ?? Find("shirt_charcoal");
            var head = Find(l.Headband); var wrist = Find(l.Wristband); var hair = Find(l.Hair); var body = Find(l.Body);
            return new Kit
            {
                ShirtBody = shirt.A, ShirtSleeves = shirt.B,
                Shorts = Of(l.Shorts, H(0x1c1e22)), Socks = Of(l.Socks, H(0xf2f2ee)), Shoes = Of(l.Shoes, H(0xf2f2ee)), Skin = Of(l.Skin, H(0xc98e66)),
                HasHeadband = head != null && !head.None, Headband = head != null ? head.A : Color.clear,
                HasWristband = wrist != null && !wrist.None, Wristband = wrist != null ? wrist.A : Color.clear,
                Frame = Of(l.Frame, H(0x5d656f)), Grip = Of(l.Grip, H(0x23272c)), Strings = Of(l.Strings, H(0xdde2e8)),
                HairStyle = hair != null ? hair.Style : 2, Hair = Of(l.HairColour, H(0x352418)), Female = body != null && body.Style == 1,
            };
        }

        /// <summary>A fixed kit for an AI player from a seed (career pros, the personalities).</summary>
        public static Loadout AiKit(uint seed)
        {
            uint s = seed * 2654435761u + 0x9E3779B9u;
            string Pick(Slot slot, bool allowNone = true)
            {
                var items = InSlot(slot);
                if (!allowNone) items.RemoveAll(i => i.None);
                s = s * 1664525u + 1013904223u;
                return items[(int)((s >> 8) % (uint)items.Count)].Id;
            }
            var l = new Loadout
            {
                Shirt = Pick(Slot.Shirt), Shorts = Pick(Slot.Shorts), Socks = Pick(Slot.Socks), Shoes = Pick(Slot.Shoes), Skin = Pick(Slot.Skin),
                Headband = Pick(Slot.Headband), Wristband = Pick(Slot.Wristband), Frame = Pick(Slot.Frame), Grip = Pick(Slot.Grip), Strings = Pick(Slot.Strings),
            };
            l.Hair = Pick(Slot.Hair); l.HairColour = Pick(Slot.HairColour);      // PR3: drawn after the rest, so older kits are unchanged
            l.Body = Pick(Slot.Body);                                            // 2026-10-24: likewise
            return l;
        }

        /// <summary>The quick-match personalities' signature kits.</summary>
        public static Loadout Signature(string personality)
        {
            switch (personality)
            {
                case "attacker": return new Loadout { Shirt = "shirt_red", Shorts = "shorts_black", Socks = "socks_red", Shoes = "shoes_black", Skin = "skin_4", Headband = "headband_red", Frame = "frame_black", Grip = "grip_red", Hair = "hair_crop", HairColour = "haircol_black" };
                case "shotmaker": return new Loadout { Shirt = "shirt_purple", Shorts = "shorts_white", Socks = "socks_white", Shoes = "shoes_purple", Skin = "skin_2", Wristband = "wristband_gold", Frame = "frame_gold", Grip = "grip_white", Hair = "hair_curls", HairColour = "haircol_blonde", Body = "body_female" };
                case "grinder": return new Loadout { Shirt = "shirt_forest", Shorts = "shorts_navy", Socks = "socks_navy", Shoes = "shoes_white", Skin = "skin_5", Headband = "headband_white", Frame = "frame_teal", Hair = "hair_curls", HairColour = "haircol_black" };
                case "wallbot": return new Loadout { Shirt = "shirt_white", Shorts = "shorts_grey", Socks = "socks_white", Shoes = "shoes_white", Skin = "skin_3", Hair = "hair_shaved" };
                default: return new Loadout { Shirt = "shirt_royal", Shorts = "shorts_white", Socks = "socks_white", Shoes = "shoes_royal", Skin = "skin_3", Wristband = "wristband_white", Frame = "frame_white", Grip = "grip_royal", Hair = "hair_short", HairColour = "haircol_brown" };
            }
        }

        /// <summary>How alike two shirts look (0 = the same .. ~1.7): RGB distance, lightness weighted.</summary>
        public static float Distance(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            float dl = (0.3f * a.r + 0.59f * a.g + 0.11f * a.b) - (0.3f * b.r + 0.59f * b.g + 0.11f * b.b);
            return Mathf.Sqrt(dr * dr + dg * dg + db * db + 2f * dl * dl);
        }

        public const float ClashDistance = 0.35f;

        /// <summary>The opponent's kit, with an away shirt if theirs would look like yours.</summary>
        public static Loadout AvoidClash(Loadout mine, Loadout theirs)
        {
            var me = KitOf(mine);
            var them = KitOf(theirs);
            if (Distance(me.ShirtBody, them.ShirtBody) >= ClashDistance) return theirs;     // shirts tell players apart (shorts may match)
            var away = theirs.Clone();
            string best = null; float bestD = -1;
            foreach (var it in InSlot(Slot.Shirt))
            {
                float d = Mathf.Min(Distance(it.A, me.ShirtBody), Distance(it.B, me.ShirtSleeves));
                if (d > bestD) { bestD = d; best = it.Id; }
            }
            away.Shirt = best;
            return away;
        }
    }
}
