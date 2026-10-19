// ===========================================================
// Assets/SquashGame/Runtime/UI/PracticeHud.cs
// Free play on the practice court: what to press while the ball is in
// your hand, and how you are doing - shots to the front wall, the run
// you are on and your best run. Reads state only.
// ===========================================================

using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class PracticeHud : VisualElement
    {
        readonly LabSimRunner _runner;
        readonly VisualElement _strip = new VisualElement(), _count = new VisualElement();
        readonly Label _key = new Label(), _text = new Label(), _hits = new Label(), _run = new Label();

        /// <summary>The hint shown now, or null (tests).</summary>
        public string HintText { get { return _strip.style.display == DisplayStyle.Flex ? _text.text : null; } }

        public PracticeHud(LabSimRunner runner)
        {
            _runner = runner;
            AddToClassList("fe-layer");
            pickingMode = PickingMode.Ignore;
            _strip.AddToClassList("hud-strip");
            _key.AddToClassList("hud-strip-key"); _text.AddToClassList("hud-strip-text");
            _strip.Add(_key); _strip.Add(_text); Add(_strip);
            _count.AddToClassList("ph-count");
            _hits.AddToClassList("ph-hits"); _run.AddToClassList("ph-run");
            _count.Add(_hits); _count.Add(_run); Add(_count);
            foreach (var e in new VisualElement[] { _strip, _count, _key, _text, _hits, _run }) e.pickingMode = PickingMode.Ignore;
        }

        public void Refresh(bool allowed)
        {
            var f = _runner != null ? _runner.Free : null;
            bool on = allowed && f != null;
            style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (!on) return;
            _strip.style.display = f.InHand ? DisplayStyle.Flex : DisplayStyle.None;
            _key.text = IntentSource.Keys("Hit");
            _text.text = "Ball in hand  ·  walk anywhere  ·  tap: drop and hit  ·  hold: harder  ·  TAB: coach";
            _hits.text = f.WallHits + "  TO THE WALL";
            _run.text = "RUN " + f.Streak + "   ·   BEST " + f.BestStreak;
        }
    }
}
