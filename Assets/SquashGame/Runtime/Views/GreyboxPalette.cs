// ===========================================================
// Assets/SquashGame/Runtime/Views/GreyboxPalette.cs
// The greybox material palette: real material assets, so URP shaders
// are never stripped from a build. (Unity requires a ScriptableObject
// to live in a file named after its class.)
// ===========================================================

using UnityEngine;

namespace SquashGame
{
    [CreateAssetMenu(menuName = "re:SquashStrike/Greybox Palette", fileName = "GreyboxPalette")]
    public sealed class GreyboxPalette : ScriptableObject
    {
        [Tooltip("URP Lit, opaque.")] public Material Lit;
        [Tooltip("URP Unlit, opaque.")] public Material Unlit;
        [Tooltip("URP Unlit, transparent (alpha blend).")] public Material Transparent;

        public Material LitColor(Color c) { var m = new Material(Lit) { color = c }; return m; }
        public Material UnlitColor(Color c) { var m = new Material(Unlit) { color = c }; return m; }
        public Material Clear(Color c) { var m = new Material(Transparent) { color = c }; return m; }
    }
}
