// ===========================================================
// Assets/SquashGame/Runtime/UI/RewardStrip.cs
// P2-M5: what a result earned, on every result screen (post-match,
// arcade results, lesson complete, career event): the XP lines, the
// level bar filling from where it was, LEVEL UP, the items unlocked,
// the achievements earned. Achievements also pop a toast (FrontEnd).
// ===========================================================

using SquashGame.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class RewardStrip : VisualElement
    {
        readonly RewardResult _r;
        readonly VisualElement _fill = new VisualElement();
        readonly Label _level = new Label(), _xp = new Label();
        float _t;
        const float FillSeconds = 1.2f;

        public RewardStrip(RewardResult r)
        {
            _r = r;
            AddToClassList("rw");
            pickingMode = PickingMode.Ignore;
            var head = new VisualElement(); head.AddToClassList("rw-head");
            _level.AddToClassList("rw-level"); _xp.AddToClassList("rw-xp");
            head.Add(_level); head.Add(_xp);
            Add(head);
            var bar = new VisualElement(); bar.AddToClassList("rw-bar");
            _fill.AddToClassList("rw-fill");
            bar.Add(_fill);
            Add(bar);
            var plain = r.Lines.FindAll(x => !x.StartsWith("Achievement:"));             // the achievements get their own line
            var lines = new Label(string.Join("    ·    ", plain)); lines.AddToClassList("rw-lines"); Add(lines);
            if (r.Achieved.Count > 0)
            {
                var names = new System.Collections.Generic.List<string>();
                int xp = 0;
                foreach (var a in r.Achieved) { names.Add(a.Name); xp += a.Xp; }
                var l = new Label("ACHIEVEMENTS  +" + xp + "   " + string.Join("  ·  ", names)); l.AddToClassList("rw-ach"); Add(l);
            }
            if (r.Unlocked.Count > 0)
            {
                // P3-R1: each new item as a swatch card, not just a name
                var row = new VisualElement(); row.AddToClassList("rw-unlocks");
                var k = new Label("UNLOCKED"); k.AddToClassList("rw-unlock"); row.Add(k);
                foreach (var it in r.Unlocked) row.Add(Swatch(it));
                Add(row);
            }
            // batch 3: the coins this earned (2026-10-28: the one currency - pulls are paid in them)
            if (r.Coins > 0)
            {
                var c = new Label("COINS   +" + r.Coins.ToString("N0") + "   ·   " + GearDrops.Wallet.Coins.ToString("N0") + " in all   ·   " + (GearDrops.Wallet.Coins / GearDrops.PullCost) + " pulls in Gear Drops (You)");
                c.AddToClassList("rw-tickets"); Add(c);
            }
            // P3-R1: what the next level brings - always something to play for
            int next = Rewards.NextUnlock(r.XpBefore + r.Xp, out var coming, out int toGo);
            if (next > 0)
            {
                var nl = new Label("NEXT LEVEL   " + next + "  ·  " + coming + "  ·  " + toGo.ToString("N0") + " XP to go");
                nl.AddToClassList("rw-next"); Add(nl);
            }
            schedule.Execute(Tick).Every(16);
            Show(0f);
        }

        /// <summary>An item as a small card: its colours and name.</summary>
        static VisualElement Swatch(Item it)
        {
            var card = new VisualElement(); card.AddToClassList("rw-swatch");
            var chip = new VisualElement(); chip.AddToClassList("rw-swatch-chip");
            chip.style.backgroundColor = it.A;
            chip.style.borderRightColor = it.B; chip.style.borderRightWidth = 6;
            card.Add(chip);
            var n = new Label(it.Name); n.AddToClassList("rw-swatch-name"); card.Add(n);
            var sl = new Label(Cosmetics.SlotNames[(int)it.Slot].ToUpperInvariant()); sl.AddToClassList("rw-swatch-slot"); card.Add(sl);
            return card;
        }

        void Tick()
        {
            _t += 0.016f;
            Show(Mathf.Clamp01(_t / FillSeconds));
        }

        /// <summary>The bar at fraction u of the way from the XP before to after.</summary>
        void Show(float u)
        {
            int xp = _r.XpBefore + Mathf.RoundToInt(_r.Xp * Mathf.SmoothStep(0f, 1f, u));
            int level = Rewards.LevelOf(xp);
            bool up = level > _r.FromLevel;
            _level.text = (up ? "LEVEL UP!   " : "LEVEL ") + level;
            _level.EnableInClassList("rw-level--up", up);
            _xp.text = "+" + _r.Xp + " XP";
            _fill.style.width = Length.Percent(100f * Rewards.Into(xp));
        }
    }
}
