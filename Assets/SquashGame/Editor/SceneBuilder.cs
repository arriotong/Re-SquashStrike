// ===========================================================
// Assets/SquashGame/Editor/SceneBuilder.cs
// Builds the scenes (M2 match, M1 warm-up court) and their greybox
// material assets from code, so they are reproducible and diffable.
//
//   Menu:   re:SquashStrike > Build Scenes (Warm-Up Court + Match)
//   Batch:  unity run . -- -executeMethod SquashGame.Editor.SceneBuilder.BuildFromCommandLine
// ===========================================================

using SquashGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SquashGame.Editor
{
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/WarmUpCourt.unity";
        const string MaterialDir = "Assets/SquashGame/Materials";
        const string PalettePath = MaterialDir + "/GreyboxPalette.asset";

        public const string MatchScenePath = "Assets/Scenes/Match.unity";

        [MenuItem("re:SquashStrike/Build Scenes (Warm-Up Court + Match)")]
        public static void Build()
        {
            GreyboxPalette palette = EnsurePalette();

            Camera cam;
            var scene = NewCourtScene(out cam);
            var lab = new GameObject("WarmUpCourt").AddComponent<WarmUpCourt>();
            lab.Palette = palette;
            lab.GameplayCamera = cam;
            EditorSceneManager.SaveScene(scene, ScenePath);

            scene = NewCourtScene(out cam);
            var match = new GameObject("MatchCourt").AddComponent<MatchCourt>();
            match.Palette = palette;
            match.GameplayCamera = cam;
            EditorSceneManager.SaveScene(scene, MatchScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MatchScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] Built " + MatchScenePath + " and " + ScenePath);
        }

        static UnityEngine.SceneManagement.Scene NewCourtScene(out Camera cam)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<GameplayCameraRig>();
            cam.fieldOfView = 52f;
            cam.transform.position = new Vector3(0f, 3.95f, 16.35f);
            cam.transform.LookAt(new Vector3(0f, 1.55f, 3.2f));
            return scene;
        }

        public static void BuildFromCommandLine()
        {
            Build();
            EditorApplication.Exit(0);
        }

        static GreyboxPalette EnsurePalette()
        {
            if (!AssetDatabase.IsValidFolder("Assets/SquashGame/Materials"))
                AssetDatabase.CreateFolder("Assets/SquashGame", "Materials");

            var palette = AssetDatabase.LoadAssetAtPath<GreyboxPalette>(PalettePath);
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<GreyboxPalette>();
                AssetDatabase.CreateAsset(palette, PalettePath);
            }

            palette.Lit = EnsureMaterial("Greybox_Lit", "Universal Render Pipeline/Lit", false);
            palette.Unlit = EnsureMaterial("Greybox_Unlit", "Universal Render Pipeline/Unlit", false);
            palette.Transparent = EnsureMaterial("Greybox_Transparent", "Universal Render Pipeline/Unlit", true);
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            return palette;
        }

        static Material EnsureMaterial(string name, string shaderName, bool transparent)
        {
            string path = MaterialDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new System.Exception("Shader not found: " + shaderName);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);

            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetFloat("_Cull", (float)CullMode.Off);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
