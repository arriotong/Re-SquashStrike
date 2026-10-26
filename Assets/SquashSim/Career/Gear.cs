// ===========================================================
// Assets/SquashSim/Career/Gear.cs
// P7 phase 5 (2026-10-27): the career's gear - a racket, shoes and a
// kit (shirt and shorts), bought in the Pro Shop with career money.
// Every item trades: it picks a way of playing, it isn't simply better
// (the user: "stats do not just straightforwardly make you strong but
// they allow you to have different play styles"). Dearer gear trades
// less, but never for free.
//
//   stats      Speed, Power, Accuracy (Control), Stamina - your levels -
//              and Stability, the gear's own: off-centre mishits and the
//              spray of a stretched, jammed or late contact
//   limits     gear moves a stat by at most +-2 levels (all three items
//              together); a stat with gear stays within 0..10
//   looks      each item wears an existing kit look (a frame, shoes, a
//              shirt) in your career matches
//
// Career only: Quick Match and the other modes never read it.
// ===========================================================

using System;
using System.Collections.Generic;

namespace SquashSim.Career
{
    public enum GearSlot { Racket, Shoes, Kit }

    public sealed class GearItem
    {
        public string Id, Name, Blurb, Look;
        public GearSlot Slot;
        public int Price;                                   // US$ (0: the starter you own)
        public int Speed, Power, Accuracy, Stamina, Stability;

        /// <summary>"+2 POW  -1 ACC  -1 SPD" (nothing: "NEUTRAL").</summary>
        public string Line
        {
            get
            {
                var parts = new List<string>();
                void P(int v, string k) { if (v != 0) parts.Add((v > 0 ? "+" : "−") + Math.Abs(v) + " " + k); }
                P(Speed, "SPD"); P(Power, "POW"); P(Accuracy, "ACC"); P(Stamina, "STA"); P(Stability, "STB");
                return parts.Count == 0 ? "NEUTRAL" : string.Join("  ", parts);
            }
        }
    }

    public static class Gear
    {
        public const int MaxShift = 2;

        static GearItem I(string id, GearSlot slot, string name, int price, string look, string blurb, int spd = 0, int pow = 0, int acc = 0, int sta = 0, int stb = 0)
        {
            return new GearItem { Id = id, Slot = slot, Name = name, Price = price, Look = look, Blurb = blurb, Speed = spd, Power = pow, Accuracy = acc, Stamina = sta, Stability = stb };
        }

        public static readonly GearItem[] All =
        {
            // rackets: the frame's weight and balance
            I("rk_club",     GearSlot.Racket, "Club 120",        0,    "frame_graphite", "The racket you started with. Even, forgiving, nothing special."),
            I("rk_feather",  GearSlot.Racket, "Feather 110",     900,  "frame_white",    "Light and quick to prepare - but it twists on an off-centre hit.", spd: 1, pow: -1, stb: -1),
            I("rk_control",  GearSlot.Racket, "Control 120",     1200, "frame_teal",     "Head-light: it goes where you put it, with less behind it.", acc: 2, pow: -1),
            I("rk_power",    GearSlot.Racket, "Power 130",       1500, "frame_red",      "Head-heavy: the ball flies - when you're in time.", pow: 2, acc: -1, spd: -1),
            I("rk_tourfeat", GearSlot.Racket, "Tour Feather 115",4000, "frame_lime",     "A pro's light frame: quick and true, a little light at the back.", spd: 1, acc: 1, pow: -1),
            I("rk_balance",  GearSlot.Racket, "Pro Balance 125", 6500, "frame_gold",     "The tour's favourite: pace, touch and a solid sweet spot. Slower to swing.", pow: 1, acc: 1, stb: 1, spd: -1),
            // shoes
            I("sh_club",     GearSlot.Shoes,  "Club Trainers",   0,    "shoes_white",    "Your first court shoes. Fine."),
            I("sh_speed",    GearSlot.Shoes,  "Court Speed",     600,  "shoes_lime",     "Feather-light and fast - with less support late in a match.", spd: 2, sta: -1),
            I("sh_cushion",  GearSlot.Shoes,  "Cushion Pro",     800,  "shoes_royal",    "Soft landings: your legs last, a touch slower off the mark.", sta: 1, spd: -1),
            I("sh_grip",     GearSlot.Shoes,  "Grip Max",        700,  "shoes_black",    "Bite on every lunge and turn: steadier at full stretch, heavier.", stb: 2, spd: -1),
            I("sh_glide",    GearSlot.Shoes,  "Elite Glide",     3500, "shoes_gold",     "Quick and planted - a pro's shoe, built light for it.", spd: 1, stb: 1, sta: -1),
            // shirt and shorts
            I("kt_club",     GearSlot.Kit,    "Club Cotton",     0,    "shirt_charcoal", "A cotton tee and shorts."),
            I("kt_dryfit",   GearSlot.Kit,    "Dry-Fit",         500,  "shirt_sky",      "Wicks the sweat: less drain deep in the long rallies.", sta: 1),
            I("kt_feather",  GearSlot.Kit,    "Featherweight",   700,  "shirt_white",    "Barely there: quicker about the court, cooks you sooner.", spd: 1, sta: -1),
            I("kt_compress", GearSlot.Kit,    "Compression",     1800, "shirt_navygold", "Holds you together: steadier and fresher, a little stiffer.", sta: 1, stb: 1, spd: -1),
        };

        static Dictionary<string, GearItem> _byId;
        public static GearItem Find(string id)
        {
            if (_byId == null) { _byId = new Dictionary<string, GearItem>(); foreach (var g in All) _byId[g.Id] = g; }
            return id != null && _byId.TryGetValue(id, out var x) ? x : null;
        }

        public static List<GearItem> In(GearSlot slot) { var l = new List<GearItem>(); foreach (var g in All) if (g.Slot == slot) l.Add(g); return l; }
        public static GearItem Starter(GearSlot slot) { foreach (var g in All) if (g.Slot == slot && g.Price == 0) return g; return null; }

        static int Shift(int sum) { return sum < -MaxShift ? -MaxShift : sum > MaxShift ? MaxShift : sum; }
        static double Clamp(double v) { return v < 0 ? 0 : v > AthleteAttributes.Max ? AthleteAttributes.Max : v; }

        /// <summary>Your levels with this gear on: each stat moved by the items' sum (at most +-2), kept within 0..10;
        /// Stability from neutral.</summary>
        public static AthleteAttributes Apply(AthleteAttributes stats, IEnumerable<GearItem> items)
        {
            int spd = 0, pow = 0, acc = 0, sta = 0, stb = 0;
            foreach (var g in items) { if (g == null) continue; spd += g.Speed; pow += g.Power; acc += g.Accuracy; sta += g.Stamina; stb += g.Stability; }
            var a = (stats ?? new AthleteAttributes()).Clone();
            a.Speed = Clamp(a.Speed + Shift(spd)); a.Power = Clamp(a.Power + Shift(pow)); a.Control = Clamp(a.Control + Shift(acc));
            a.Stamina = Clamp(a.Stamina + Shift(sta)); a.Stability = Clamp(AthleteAttributes.Neutral + Shift(stb));
            return a;
        }
    }

    public sealed partial class CareerSession
    {
        /// <summary>What you have on in a slot (the starter until you change it).</summary>
        public GearItem Equipped(GearSlot slot)
        {
            string id = slot == GearSlot.Racket ? S.Racket : slot == GearSlot.Shoes ? S.Shoes : S.KitGear;
            var g = Gear.Find(id);
            return g != null && g.Slot == slot ? g : Gear.Starter(slot);
        }

        public IEnumerable<GearItem> Wearing() { yield return Equipped(GearSlot.Racket); yield return Equipped(GearSlot.Shoes); yield return Equipped(GearSlot.Kit); }

        /// <summary>Your athlete as they play: your levels, and your gear.</summary>
        public AthleteAttributes Effective { get { return Gear.Apply(S.Attributes, Wearing()); } }

        public bool Owns(string id) { var g = Gear.Find(id); return g != null && (g.Price == 0 || S.GearOwned.Contains(id)); }

        /// <summary>Null when you can buy it now, else why not.</summary>
        public string CantBuy(string id)
        {
            var g = Gear.Find(id);
            if (g == null) return "no such item";
            if (Owns(id)) return "yours already";
            if (S.Cash < g.Price) return "not enough cash";
            return null;
        }

        /// <summary>Buy an item (it goes on at once). False when you can't.</summary>
        public bool Buy(string id)
        {
            if (CantBuy(id) != null) return false;
            var g = Gear.Find(id);
            S.Cash -= g.Price; S.GearSpent += g.Price;
            S.GearOwned.Add(id);
            Equip(id);
            return true;
        }

        /// <summary>Put on an item you own.</summary>
        public bool Equip(string id)
        {
            var g = Gear.Find(id);
            if (g == null || !Owns(id)) return false;
            if (g.Slot == GearSlot.Racket) S.Racket = id; else if (g.Slot == GearSlot.Shoes) S.Shoes = id; else S.KitGear = id;
            return true;
        }
    }
}
