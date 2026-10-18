// ===========================================================
// Assets/SquashGame/Runtime/UI/Kit/Vignettes.cs
// UI redesign (U1): live 3D pictures for cards. The menus' court is
// already being drawn (the slow-motion rally); a card can show it from
// its own angle - your athlete up close, the court from above, the
// front wall - in a small render texture.
//
// One camera per visible picture, rendered by hand at 15 fps (not
// every frame), and only while its card is on screen; freed when the
// card leaves the panel. Presentation only.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class Vignettes : MonoBehaviour
    {
        public const float Fps = 15f;
        public static Vignettes Active { get; private set; }
        public SimRunnerBase Runner;

        sealed class Entry { public VisualElement Owner; public string Shot; public Camera Cam; public RenderTexture Rt; public float Orbit; }
        readonly List<Entry> _live = new List<Entry>();
        float _since;

        /// <summary>How many pictures exist (tests).</summary>
        public int Count { get { return _live.Count; } }
        /// <summary>How many are on screen now - the ones being drawn (tests).</summary>
        public int Showing { get { int n = 0; foreach (var e in _live) if (Shown(e.Owner)) n++; return n; } }
        /// <summary>Frames rendered so far (tests).</summary>
        public int Rendered { get; private set; }

        void Awake() { Active = this; }
        void OnDestroy() { foreach (var e in _live) Free(e); _live.Clear(); if (Active == this) Active = null; }

        /// <summary>Show a live picture in this element's background. Shots: "you", "court", "front", "racket", "versus".</summary>
        public static void Attach(VisualElement art, string shot, int width = 512, int height = 288)
        {
            art.RegisterCallback<AttachToPanelEvent>(e => { if (Active != null) Active.Add(art, shot, width, height); });
            art.RegisterCallback<DetachFromPanelEvent>(e => { if (Active != null) Active.Remove(art); });
            if (art.panel != null && Active != null) Active.Add(art, shot, width, height);
        }

        void Add(VisualElement owner, string shot, int w, int h)
        {
            foreach (var e in _live) if (e.Owner == owner) return;
            var go = new GameObject("Vignette_" + shot);
            go.transform.SetParent(transform, false);
            var cam = go.AddComponent<Camera>();
            var main = Camera.main;
            if (main != null) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; cam.cullingMask = main.cullingMask; }
            cam.enabled = false;
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 80f;
            var rt = new RenderTexture(w, h, 16) { name = "vignette_" + shot, antiAliasing = 2 };
            cam.targetTexture = rt;
            var entry = new Entry { Owner = owner, Shot = shot, Cam = cam, Rt = rt, Orbit = Random.value * 6f };
            _live.Add(entry);
            owner.style.backgroundImage = Background.FromRenderTexture(rt);
            Draw(entry, 0f);
        }

        void Remove(VisualElement owner)
        {
            for (int i = _live.Count - 1; i >= 0; i--) if (_live[i].Owner == owner) { Free(_live[i]); _live.RemoveAt(i); }
        }

        static void Free(Entry e)
        {
            if (e.Cam != null) { e.Cam.targetTexture = null; Destroy(e.Cam.gameObject); }
            if (e.Rt != null) { e.Rt.Release(); Destroy(e.Rt); }
        }

        static bool Shown(VisualElement v)
        {
            if (v.panel == null) return false;
            for (var p = v; p != null; p = p.parent) if (p.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            foreach (var e in _live) e.Orbit += dt * 0.25f;
            _since += dt;
            if (_since < 1f / Fps) return;
            float step = _since; _since = 0f;
            foreach (var e in _live) if (Shown(e.Owner)) Draw(e, step);
        }

        void Draw(Entry e, float dt)
        {
            Vector3 you = new Vector3(0, 0, 5f), them = new Vector3(0, 0, 6f);
            var a = Runner != null ? Runner.Athletes : null;
            if (a != null && a.Length > 0) you = new Vector3((float)a[0].Player.Pos.X, 0, (float)a[0].Player.Pos.Z);
            if (a != null && a.Length > 1) them = new Vector3((float)a[1].Player.Pos.X, 0, (float)a[1].Player.Pos.Z);
            Vector3 pos, look; float fov;
            switch (e.Shot)
            {
                case "you":
                    pos = you + new Vector3(Mathf.Sin(e.Orbit) * 1.6f, 1.25f, -2.2f); look = you + new Vector3(0, 0.85f, 0); fov = 40; break;
                case "them":
                    pos = them + new Vector3(Mathf.Sin(e.Orbit) * 1.6f, 1.25f, -2.2f); look = them + new Vector3(0, 0.85f, 0); fov = 40; break;
                case "racket":
                    pos = you + new Vector3(0.7f, 1.15f, -1.0f); look = you + new Vector3(-0.3f, 1.0f, 0); fov = 36; break;
                case "front":
                    pos = new Vector3(1.6f, 0.5f, 1.6f); look = (you + them) * 0.5f + new Vector3(0, 1.0f, 0); fov = 55; break;
                case "versus":
                    pos = (you + them) * 0.5f + new Vector3(2.9f, 1.4f, 0.2f); look = (you + them) * 0.5f + new Vector3(0, 0.9f, 0); fov = 50; break;
                default:                                                       // "court": from high behind the glass
                    pos = new Vector3(Mathf.Sin(e.Orbit * 0.5f) * 1.5f, 5.6f, 12.5f); look = new Vector3(0, 0, 4.5f); fov = 45; break;
            }
            pos.x = Mathf.Clamp(pos.x, -3.1f, 3.1f); pos.z = Mathf.Max(pos.z, 0.25f);
            e.Cam.fieldOfView = fov;
            e.Cam.transform.position = SimToWorld.Pos(pos.x, pos.y, pos.z);
            e.Cam.transform.LookAt(SimToWorld.Pos(look.x, look.y, look.z), Vector3.up);
            e.Cam.Render();
            Rendered++;
            e.Owner.MarkDirtyRepaint();
        }
    }
}
