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
                var names = new System.Collections.Generic.List<string>();
                foreach (var it in r.Unlocked) names.Add(it.Name + " (" + Cosmetics.SlotNames[(int)it.Slot].ToLowerInvariant() + ")");
                var l = new Label("UNLOCKED   " + string.Join(", ", names)); l.AddToClassList("rw-unlock"); Add(l);
            }
            schedule.Execute(Tick).Every(16);
            Show(0f);
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
