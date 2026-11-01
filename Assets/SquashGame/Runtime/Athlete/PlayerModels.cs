// ===========================================================
// Assets/SquashGame/Runtime/Athlete/PlayerModels.cs
// 2026-11-01 the real players: the MakeHuman bodies made by the Blender
// pipeline (Resources/Players/<id>.fbx, a Humanoid avatar, T-pose rest)
// and their parts list (<id>.json: each mesh's kind - skin, shirt,
// shorts, socks, shoes, hair, eyes, eyebrows, eyelashes, teeth - and
// its textures in Resources/Players/Textures). The game makes the
// materials: the skin as painted, the kit in the player's colours over
// plain fabric (the garment's folds kept), hair in its colour.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame.Athlete
{
    public static class PlayerModels
    {
        public const string Folder = "Players/";

        [System.Serializable] public sealed class MeshInfo { public string name, kind, style, albedo, normal, rough; }
        [System.Serializable] public sealed class Info { public MeshInfo[] meshes; public bool female; public string skin, hair; }

        /// <summary>One drawn part of a player: its renderer, what it is, its material.</summary>
        public sealed class Part { public SkinnedMeshRenderer R; public string Kind, Style; public Material M; }

        /// <summary>The Locker's skin tones (Cosmetics), lightest first: the bodies are made one per tone (m1..m6, f1..f6).</summary>
        public static readonly Color[] Tones =
        {
            new Color32(0xf3, 0xd2, 0xb8, 255), new Color32(0xe3, 0xb4, 0x8f, 255), new Color32(0xc9, 0x8e, 0x66, 255),
            new Color32(0xa5, 0x6b, 0x46, 255), new Color32(0x7c, 0x4a, 0x2e, 255), new Color32(0x4f, 0x2f, 0x1d, 255)
        };
        /// <summary>The Locker's hair styles in order (Cosmetics.HairStyles): the style mesh each body carries ("" = none).</summary>
        public static readonly string[] HairStyleIds = { "shaved", "crop", "short", "curls", "bun", "ponytail", "mohawk" };

        /// <summary>The nearest of the Locker's tones to a skin colour (0..5).</summary>
        public static int ToneOf(Color skin)
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < Tones.Length; i++)
            {
                float d = (Tones[i].r - skin.r) * (Tones[i].r - skin.r) + (Tones[i].g - skin.g) * (Tones[i].g - skin.g) + (Tones[i].b - skin.b) * (Tones[i].b - skin.b);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>The body for a build and a tone ("f1".."f6" for the women, made with MakeHuman).</summary>
        public static string IdFor(bool female, int tone) { return (female ? "f" : "m") + (Mathf.Clamp(tone, 0, Tones.Length - 1) + 1); }

        /// <summary>2026-11-01 the players the user chose on Mixamo: you are Ch42, a male opponent Remy (both in shirt, shorts and
        /// sneakers). The women stay the MakeHuman bodies until there are Mixamo ones.</summary>
        public const string You = "ch42", Opponent = "remy";

        /// <summary>The body for an athlete: you or an opponent, the kit's build and tone.</summary>
        public static string BodyFor(bool you, bool female, int tone) { return female ? IdFor(true, tone) : you ? You : Opponent; }

        /// <summary>Each body's height as made, before it was drawn at the figure's (tests).</summary>
        public static readonly Dictionary<string, float> Statures = new Dictionary<string, float>();

        public static Info Load(string id)
        {
            var t = Resources.Load<TextAsset>(Folder + id);
            return t != null ? JsonUtility.FromJson<Info>(t.text) : null;
        }

        static Texture2D Tex(string name) { return string.IsNullOrEmpty(name) ? null : Resources.Load<Texture2D>(Folder + "Textures/" + name); }

        /// <summary>The kinds dressed in the player's kit colours: plain fabric (the garment's print dropped) with its folds.</summary>
        public static bool IsKit(string kind) { return kind == "shirt" || kind == "shorts" || kind == "socks"; }

        /// <summary>Make a player under the parent: the model, its materials (from the lit template). Null if the art isn't there.</summary>
        public static GameObject Spawn(string id, Transform parent, Material litTemplate, List<Part> parts)
        {
            var prefab = Resources.Load<GameObject>(Folder + id);
            var info = Load(id);
            if (prefab == null || info == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = "Player_" + id;
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            var byName = new Dictionary<string, MeshInfo>();
            foreach (var m in info.meshes) byName[m.name] = m;
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                byName.TryGetValue(r.name, out var mi);
                string kind = mi != null ? mi.kind : "clothes";
                var mat = new Material(litTemplate) { name = "player_" + kind };
                var albedo = IsKit(kind) ? null : Tex(mi?.albedo);
                if (albedo != null) mat.SetTexture("_BaseMap", albedo);
                mat.SetColor("_BaseColor", Color.white);
                var normal = Tex(mi?.normal);
                if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", kind == "skin" ? 0.32f : kind == "eyes" ? 0.85f : kind == "hair" ? 0.25f : kind == "shoes" ? 0.35f : 0.12f);
                if (kind == "hair" || kind == "eyebrows" || kind == "eyelashes")
                {
                    mat.SetFloat("_AlphaClip", 1f); mat.SetFloat("_Cutoff", kind == "hair" ? 0.35f : 0.5f); mat.EnableKeyword("_ALPHATEST_ON");
                    mat.SetFloat("_Cull", 0f);                                          // both sides of a hair card
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                }
                r.sharedMaterial = mat;
                r.updateWhenOffscreen = true;
                if (kind == "teeth" || kind == "eyelashes" || kind == "eyebrows") r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                parts?.Add(new Part { R = r, Kind = kind, Style = mi != null ? mi.style : "", M = mat });
            }
            // every body at the figure's stature (the game is sized for it: reach, the cameras' framing); the files differ
            // (MakeHuman 1.89 m, Mixamo's at 1.8 or twice that)
            float top = 0f, foot = float.MaxValue;
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh == null) continue;
                var m = r.transform.localToWorldMatrix; var bb = r.sharedMesh.bounds;          // the bind pose (T): the box's top is the crown
                for (int c = 0; c < 8; c++)
                {
                    var y = m.MultiplyPoint3x4(new Vector3((c & 1) == 0 ? bb.min.x : bb.max.x, (c & 2) == 0 ? bb.min.y : bb.max.y, (c & 4) == 0 ? bb.min.z : bb.max.z)).y;
                    top = Mathf.Max(top, y); foot = Mathf.Min(foot, y);
                }
            }
            float scaleY = go.transform.lossyScale.y;
            float height = (top - foot) / Mathf.Max(1e-4f, scaleY);
            Statures[id] = height;
            if (height > 0.5f) go.transform.localScale = Vector3.one * (GateA.Crown / height);
            return go;
        }

        /// <summary>Dress a player in a kit: shirt, shorts, socks and shoes in its colours; the hair style shown (one, or none
        /// shaved) in its colour; the skin in its tone (the painted skin is the lightest; darker tones multiply it down).</summary>
        public static void Dress(List<Part> parts, Color shirt, Color shorts, Color socks, Color shoes, Color hair, int hairStyle, Color skin)
        {
            string style = hairStyle >= 0 && hairStyle < HairStyleIds.Length ? HairStyleIds[hairStyle] : "short";
            Color tone = new Color(Mathf.Clamp01(skin.r / Tones[0].r), Mathf.Clamp01(skin.g / Tones[0].g), Mathf.Clamp01(skin.b / Tones[0].b));
            foreach (var p in parts)
            {
                if (p.Kind == "hair" && !string.IsNullOrEmpty(p.Style)) p.R.enabled = p.Style == style;
                switch (p.Kind)
                {
                    case "skin": p.M.SetColor("_BaseColor", tone); break;
                    case "shirt": p.M.SetColor("_BaseColor", shirt); break;
                    case "shorts": p.M.SetColor("_BaseColor", shorts); break;
                    case "socks": p.M.SetColor("_BaseColor", socks); break;
                    case "shoes": p.M.SetColor("_BaseColor", Color.Lerp(Color.white, shoes, 0.75f)); break;
                    case "hair": case "eyebrows": p.M.SetColor("_BaseColor", HairTint(hair)); break;
                }
            }
        }

        /// <summary>The painted hair is mid-brown: a colour over it, brightened so blonde reads blonde and black stays black.</summary>
        public static Color HairTint(Color c)
        {
            var brown = new Color(0.36f, 0.25f, 0.17f);
            return new Color(Mathf.Clamp01(c.r / brown.r * 0.55f), Mathf.Clamp01(c.g / brown.g * 0.55f), Mathf.Clamp01(c.b / brown.b * 0.55f));
        }

        /// <summary>The motion-capture clips (Resources/Players/Anim), by name.</summary>
        public static Dictionary<string, AnimationClip> Clips()
        {
            var d = new Dictionary<string, AnimationClip>();
            foreach (var c in Resources.LoadAll<AnimationClip>(Folder + "Anim"))
            {
                if (c.name.StartsWith("__preview__")) continue;
                string n = c.name; int dot = n.LastIndexOf('.');
                if (dot > 0 && int.TryParse(n.Substring(dot + 1), out _)) n = n.Substring(0, dot);     // Blender's "Jog_Fwd_Loop.001"
                d[n] = c;
            }
            return d;
        }
    }
}
