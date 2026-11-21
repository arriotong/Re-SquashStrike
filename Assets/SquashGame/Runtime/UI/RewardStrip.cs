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
        int _coinsShown, _walletShown;
        bool _burst, _counted;

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
            // 2026-10-30 (the kid test): no line of XP sums ("11 rallies +22 · 11 rallies won +33 ..."); the achievements as
            // a count and the first two names, not a row of nine
            if (r.Achieved.Count > 0)
            {
                var names = new System.Collections.Generic.List<string>();
                foreach (var a in r.Achieved) if (names.Count < 2) names.Add(a.Name);
                string more = r.Achieved.Count > 2 ? "  and " + (r.Achieved.Count - 2) + " more" : "";
                var l = new Label((r.Achieved.Count == 1 ? "NEW ACHIEVEMENT:  " : r.Achieved.Count + " NEW ACHIEVEMENTS:  ") + string.Join(",  ", names) + more); l.AddToClassList("rw-ach"); Add(l);
            }
            if (r.Unlocked.Count > 0)
            {
                // P3-R1: each new item as a swatch card, not just a name
                var row = new VisualElement(); row.AddToClassList("rw-unlocks");
                var k = new Label("NEW STUFF"); k.AddToClassList("rw-unlock"); row.Add(k);
                foreach (var it in r.Unlocked) row.Add(Swatch(it));
                Add(row);
            }
            // batch 3: the coins this earned (2026-10-28: the one currency - pulls are paid in them)
            if (r.Coins > 0)
            {
                // 2026-10-29: as the coin icon and amounts - "+ (coin) 120   ·   (coin) 3,350 in all"
                var c = new VisualElement(); c.AddToClassList("rw-coins");
                var plus = new Label("YOU WON"); plus.AddToClassList("rw-tickets"); c.Add(plus);
                // 2026-11-21 the polish pass: the coins fly from what you won into what you have, which counts up as they land
                _coinsShown = Juice.On ? 0 : r.Coins;
                _walletShown = Juice.On ? GearDrops.Wallet.Coins - r.Coins : GearDrops.Wallet.Coins;
                var got = new CoinCount(() => _coinsShown, 18f); got.AddToClassList("k-coins--inline"); c.Add(got);
                var mid = new Label("·   NOW YOU HAVE"); mid.AddToClassList("rw-tickets"); c.Add(mid);
                var all = new CoinCount(() => _walletShown, 18f); all.AddToClassList("k-coins--inline"); c.Add(all);
                Add(c);
                if (Juice.On)
                {
                    int total = r.Coins, flights = Mathf.Clamp(total / 10, 3, 10), each = 0;
                    schedule.Execute(() =>
                    {
                        // count what you won up first, then fly it across
                        float t = 0;
                        got.schedule.Execute(() => { t += 0.016f; _coinsShown = Mathf.RoundToInt(total * Mathf.Clamp01(t / 0.5f)); }).Every(16).Until(() => t >= 0.5f);
                        got.schedule.Execute(() => Juice.FlyCoins(got, all, flights, null)).StartingIn(550);
                        // the wallet counts up as each coin lands (the last one: exactly)
                        all.schedule.Execute(() =>
                        {
                            each++;
                            _walletShown = each >= flights ? GearDrops.Wallet.Coins : GearDrops.Wallet.Coins - total + total * each / flights;
                        }).Every(60).StartingIn(550 + 620).Until(() => each >= flights);
                    }).StartingIn((long)(FillSeconds * 1000) + 150);
                }
            }
            // P3-R1: what the next level brings - always something to play for
            int next = Rewards.NextUnlock(r.XpBefore + r.Xp, out var coming, out int toGo);
            if (next > 0)
            {
                var nl = new Label("LEVEL " + next + " GIVES YOU:  " + coming);
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
            // 2026-11-21 the polish pass: the XP counts up with the bar; a level up bursts
            _xp.text = "+" + Mathf.RoundToInt(_r.Xp * (Juice.On ? Mathf.SmoothStep(0f, 1f, u) : 1f)) + " XP";
            if (up && !_burst && Juice.On && u > 0f)
            {
                _burst = true;
                Sounds.Play("levelup");
                Juice.Bump(_level);
                Juice.Burst(_level, new Color32(200, 255, 61, 255), 16);
                Juice.Rumble(0.5f, 0.5f, 0.25f);
                _level.AddToClassList("rw-level--burst");
            }
            if (u >= 1f && !_counted && Juice.On) { _counted = true; Juice.Bump(_xp); }
            _fill.style.width = Length.Percent(100f * Rewards.Into(xp));
        }
    }
}
