// ===========================================================
// Assets/SquashGame/Editor/PoseSheet.cs
// M7: render every pose in the active PoseLibrary to a contact sheet -
// a front three-quarter view and a side view per pose - so the poses can
// be reviewed (and corrected) as pictures.
//
//   Menu:   re:SquashStrike > Capture Pose Sheet
//   Batch:  unity run . -- -executeMethod SquashGame.Editor.PoseSheet.CaptureFromCommandLine [-poseGroup name]
//
// Writes Captures/PoseSheet_tiles/<n>.png per view and Captures/PoseSheet.json
// (the order and names); Tools/PoseSheet/compose.py lays them out with labels.
// ===========================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using SquashGame.Athlete;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SquashGame.Editor
{
    public static class PoseSheet
    {
        const int W = 420, H = 520;

        [MenuItem("re:SquashStrike/Capture Pose Sheet")]
        public static void CaptureMenu() { Capture(null); }

        public static void CaptureFromCommandLine()
        {
            string group = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-poseGroup") group = args[i + 1];
            int code = 0;
            try { Capture(group); }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static void Capture(string group)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var palette = AssetDatabase.LoadAssetAtPath<GreyboxPalette>("Assets/SquashGame/Materials/GreyboxPalette.asset");

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.transform.rotation = Quaternion.Euler(42f, 150f, 0f);
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.62f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.GetComponent<MeshRenderer>().sharedMaterial = palette.LitColor(Greybox.Hex(0xe2dcc8));
            floor.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());

            var athlete = new GameObject("Athlete");
            var rig = athlete.AddComponent<AthleteRig>();
            rig.Build(palette, Greybox.Hex(0x7d93ab));
            rig.HoldRacketInHand();

            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Greybox.Hex(0x1d2129);
            cam.fieldOfView = 46f;
            var rt = new RenderTexture(W, H, 24);
            cam.targetTexture = rt;

            // The athlete faces -Z; their right is -X. Views: front three-quarter (from their front-right) and side (from their right).
            var views = new[]
            {
                (pos: new Vector3(-1.9f, 1.35f, -3.4f), look: new Vector3(-0.1f, 1.05f, -0.2f)),
                (pos: new Vector3(-4.0f, 1.25f, -0.25f), look: new Vector3(0, 1.05f, -0.25f))
            };

            PoseLibrary.ResetActive();
            var lib = PoseLibrary.Active;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "PoseSheet_tiles"));
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            var json = new StringBuilder("[\n");
            int n = 0;
            foreach (var pose in lib.Poses)
            {
                if (group != null && pose.Group != group) continue;
                PoseMath.Apply(rig, pose);
                rig.UpdateBlends();
                for (int v = 0; v < views.Length; v++)
                {
                    cam.transform.position = views[v].pos;
                    cam.transform.LookAt(views[v].look);
                    cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    tex.Apply();
                    File.WriteAllBytes(Path.Combine(dir, n + "_" + v + ".png"), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                }
                json.Append("  {\"i\": ").Append(n).Append(", \"name\": \"").Append(pose.Name).Append("\", \"group\": \"").Append(pose.Group).Append("\"},\n");
                n++;
            }
            if (n > 0) json.Length -= 2;
            json.Append("\n]\n");
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "PoseSheet.json")), json.ToString());
            Debug.Log("[PoseSheet] " + n + " poses -> " + dir);
        }
    }
}
