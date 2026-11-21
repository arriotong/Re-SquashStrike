// ===========================================================
// Assets/SquashGame/Runtime/UI/SpecialMeter.cs
// 2026-11-20 Quick Match's Special meter (the user: "like Mario Tennis -
// a meter in the top right corner"): a star that lights up when it's
// full, a bar in three parts (each a Focus), what you can do with it.
// Yours big, the opponent's smaller under it. Presentation only.
// ===========================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame
{
    public sealed class SpecialMeter : VisualElement
    {
        readonly Label _title = new Label(), _hint = new Label(), _star = new Label("\u2605");
        readonly VisualElement _fill = new VisualElement();
        float _pulse;

        public SpecialMeter(bool mine)
        {
            AddToClassList("hud-sp");
            AddToClassList(mine ? "hud-sp--mine" : "hud-sp--them");
            pickingMode = PickingMode.Ignore;
            _star.AddToClassList("hud-sp-star");
            var body = new VisualElement(); body.AddToClassList("hud-sp-body");
            _title.AddToClassList("hud-sp-title"); _hint.AddToClassList("hud-sp-hint");
            var track = new VisualElement(); track.AddToClassList("hud-sp-track");
            _fill.AddToClassList("hud-sp-fill"); track.Add(_fill);
            // 2026-11-22 (the user): one smooth bar - Focus drains it slowly, not by thirds
            body.Add(_title); body.Add(track); body.Add(_hint);
            Add(_star); Add(body);
            foreach (var e in new VisualElement[] { _title, _hint, _star, _fill, track, body }) e.pickingMode = PickingMode.Ignore;
        }

        public void Set(float v, string title, string hint)
        {
            _title.text = title;
            _hint.text = hint;
            _hint.style.display = string.IsNullOrEmpty(hint) ? DisplayStyle.None : DisplayStyle.Flex;
            _fill.style.width = Length.Percent(100f * Mathf.Clamp01(v));
            bool ready = v >= 1f;
            EnableInClassList("hud-sp--ready", ready);
            _pulse += Time.unscaledDeltaTime;
            EnableInClassList("hud-sp--pulse", ready && Mathf.Repeat(_pulse, 0.8f) < 0.4f);
        }
    }
}
