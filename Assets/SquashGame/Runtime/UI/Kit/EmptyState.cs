// ===========================================================
// Assets/SquashGame/Runtime/UI/Kit/EmptyState.cs
// 2026-11-21 (the screen sweep: empty pages looked broken - a big space
// and one tiny grey line): a page with nothing on it yet says so plainly -
// a picture, a title, one line of what to do - big enough to read.
// ===========================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public static class EmptyState
    {
        /// <summary>The empty page's panel: <paramref name="art"/> (a CardArt kind), its title, one line.</summary>
        public static VisualElement Add(VisualElement parent, string art, string title, string line, Color? accent = null)
        {
            var box = Kit.Box(parent, "k-empty");
            box.pickingMode = PickingMode.Ignore;
            var pic = Kit.Box(box, "k-empty-art");
            pic.Add(new CardArt(art, new Color32(22, 24, 30, 255), accent ?? (Color)new Color32(200, 255, 61, 255)));
            var words = Kit.Box(box, "k-empty-words");
            Kit.L(words, title, "k-empty-title");
            Kit.L(words, line, "k-empty-line");
            return box;
        }
    }
}
