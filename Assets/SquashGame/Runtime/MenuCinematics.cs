// ===========================================================
// Assets/SquashGame/Runtime/MenuCinematics.cs
// The menus' background, after the old SquashStrike's home screen:
// the AI-vs-AI demo rally in slow motion (MatchRunner.DemoSpeed),
// filmed in cuts - a high corner, low by the front wall, along the
// side wall, from the floor at the back - each a slow drifting shot
// that keeps the rally in frame. Each main-menu row has its own set
// of angles (Scene), like the old home's per-tab scenes.
//
// Presentation only: GameplayCameraRig hands the camera over while
// the demo plays.
// ===========================================================

using UnityEngine;

namespace SquashGame
{
    public static class MenuCinematics
    {
        /// <summary>Which set of angles (the main menu's focused row).</summary>
        public static int Scene;
        public const float CutSeconds = 4.6f;

        struct Shot
        {
            public Vector3 Pos, Look, Pan;      // sim metres (x right, y up, z from the front wall); pan in m/s
            public float Fov, Follow;           // Follow: how far the aim leans to the action (0..1)
            public Shot(float px, float py, float pz, float lx, float ly, float lz, float fov, float follow, float vx, float vy, float vz)
            { Pos = new Vector3(px, py, pz); Look = new Vector3(lx, ly, lz); Fov = fov; Follow = follow; Pan = new Vector3(vx, vy, vz); }
        }

        static readonly Shot[][] Scenes =
        {
            // Quick Match: the old home's "play" cuts
            new[]
            {
                new Shot(-3.5f, 4.5f, 11.0f,   0f, 0.5f, 4.0f,  40, 0.35f,   0.05f, -0.03f, -0.10f),
                new Shot( 1.0f, 0.5f,  2.0f,  -1.5f, 1.0f, 8.0f,  60, 0.45f,  -0.10f, 0f, 0.14f),
                new Shot( 3.0f, 0.4f,  3.5f,   1.5f, 0.6f, 1.0f,  62, 0.50f,   0f, 0.04f, -0.10f),
                new Shot( 1.5f, 0.2f,  8.0f,   0f, 2.0f, 0f,     60, 0.40f,  -0.10f, 0.04f, -0.14f),
            },
            // Local Versus: side-on, high
            new[]
            {
                new Shot( 2.9f, 3.6f, 9.4f,   -0.5f, 0.8f, 3.5f,  55, 0.45f,  -0.06f, 0f, -0.05f),
                new Shot(-2.9f, 3.6f, 9.4f,    0.5f, 0.8f, 3.5f,  55, 0.45f,   0.06f, 0f, -0.05f),
            },
            // Career: the broadcast view, low behind the glass
            new[]
            {
                new Shot( 0f, 1.6f, 12.5f,    0f, 1.2f, 3.0f,   42, 0.30f,   0.08f, 0.02f, -0.06f),
                new Shot(-2.0f, 2.4f, 12.0f,  0.5f, 1.0f, 2.5f,  44, 0.35f,   0.06f, 0f, -0.05f),
            },
            // Target Arcade: up close to the front wall
            new[]
            {
                new Shot( 0f, 2.0f, 6.5f,     -1.0f, 1.2f, 0f,   50, 0.50f,   0.10f, 0f, -0.04f),
                new Shot( 2.6f, 1.4f, 5.5f,    0f, 1.2f, 1.0f,   55, 0.40f,  -0.05f, 0f, -0.10f),
            },
            // Practice Court: along the side wall, low
            new[]
            {
                new Shot( 3.0f, 1.2f, 7.5f,   0f, 1.0f, 2.0f,    55, 0.45f,   0f, -0.02f, -0.12f),
                new Shot(-3.0f, 0.6f, 2.0f,   1.0f, 0.8f, 8.0f,  60, 0.45f,   0.04f, 0.03f, 0.10f),
            },
            // Academy: from above
            new[]
            {
                new Shot( 0f, 6.0f, 12.0f,    0f, 0f, 4.0f,     45, 0.25f,   0f, 0f, 0f),
                new Shot( 1.5f, 5.2f, 6.0f,   0f, 0f, 4.5f,     60, 0.35f,  -0.08f, 0f, 0f),
            },
        };

        static int _scene = -1, _shot;
        static float _age;
        static Vector3 _action;
        static bool _haveAction;

        /// <summary>Restart the cuts (a new scene, or the demo started again).</summary>
        public static void Reset() { _scene = -1; _haveAction = false; }

        /// <summary>Film the demo rally with this camera (instead of the gameplay camera).</summary>
        public static void Apply(Camera cam, SimRunnerBase runner, float dt)
        {
            int scene = ((Scene % Scenes.Length) + Scenes.Length) % Scenes.Length;
            var set = Scenes[scene];
            if (scene != _scene) { _scene = scene; _shot = 0; _age = 0; }
            _age += dt;
            if (_age > CutSeconds) { _age = 0; _shot = (_shot + 1) % set.Length; }
            var s = set[_shot];

            // the action: between the ball and the players, smoothed
            Vector3 act = s.Look;
            if (runner != null && runner.Ball != null)
            {
                var b = runner.Ball.Pos;
                var a = runner.Athletes;
                double px = 0, pz = 0;
                for (int i = 0; a != null && i < a.Length; i++) { px += a[i].Player.Pos.X; pz += a[i].Player.Pos.Z; }
                int n = a != null && a.Length > 0 ? a.Length : 1;
                act = new Vector3((float)(b.X * 0.6 + px / n * 0.4), Mathf.Clamp((float)b.Y, 0.4f, 2.2f), (float)(b.Z * 0.6 + pz / n * 0.4));
            }
            float k = 1f - Mathf.Exp(-2.2f * dt);
            if (!_haveAction) { _action = act; _haveAction = true; } else _action += (act - _action) * k;

            Vector3 pos = s.Pos + s.Pan * _age;
            Vector3 look = Vector3.Lerp(s.Look, _action, s.Follow);
            cam.fieldOfView = s.Fov;
            cam.transform.position = SimToWorld.Pos(pos.x, pos.y, pos.z);
            cam.transform.LookAt(SimToWorld.Pos(look.x, look.y, look.z), Vector3.up);
        }
    }
}
