// ===========================================================
// Assets/SquashGame/Editor/PlayerArtImport.cs
// 2026-11-01 the real players: how their art comes in.
// - Resources/Players/*.fbx: the MakeHuman bodies (made by the Blender
//   pipeline, T-pose rest), Humanoid avatars, no materials (the game
//   makes them: PlayerModels), no animation.
// - Resources/Players/Anim/*.fbx: motion capture (Quaternius UAL,
//   Mixamo), Humanoid; "_Loop" takes loop; in place (the sim moves the
//   athlete).
// - Resources/Players/Textures: at most 1024, normal maps as such.
// ===========================================================

using UnityEditor;
using UnityEngine;

namespace SquashGame.Editor
{
    public sealed class PlayerArtImport : AssetPostprocessor
    {
        /// <summary>Bumped when the settings change, so Unity imports the art again.</summary>
        public override uint GetVersion() { return 2; }

        static string P(string path) { return path.Replace("\\", "/"); }
        static bool IsAnim(string path) { return P(path).Contains("/Resources/Players/Anim/") && path.ToLowerInvariant().EndsWith(".fbx"); }
        static bool IsBody(string path) { return P(path).Contains("/Resources/Players/") && !IsAnim(path) && path.ToLowerInvariant().EndsWith(".fbx"); }

        void OnPreprocessModel()
        {
            if (!IsBody(assetPath) && !IsAnim(assetPath)) return;
            var imp = (ModelImporter)assetImporter;
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importCameras = false; imp.importLights = false;
            imp.importBlendShapes = false;
            imp.optimizeGameObjects = false;
            imp.importAnimation = IsAnim(assetPath);
            imp.resampleCurves = true;
            imp.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        void OnPreprocessAnimation()
        {
            if (!IsAnim(assetPath)) return;
            var imp = (ModelImporter)assetImporter;
            var clips = imp.defaultClipAnimations;
            // a Mixamo file has one take, always called "mixamo.com": the clip takes the file's name (Dive_Left, Slash_A...)
            if (clips.Length == 1) clips[0].name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            foreach (var c in clips)
            {
                c.loopTime = c.name.Contains("Loop") || c.name.Contains("Idle") || c.name.Contains("Run") || c.name.Contains("Walk");
                c.lockRootRotation = true; c.keepOriginalOrientation = true;
                c.lockRootHeightY = true; c.keepOriginalPositionY = true;
                // the travel across the floor is NOT baked into the pose: it is root motion, which the athlete never applies -
                // the sim moves them (a goalkeeper dive went 2.6 m sideways out of its own body otherwise)
                c.lockRootPositionXZ = false; c.keepOriginalPositionXZ = false;
            }
            imp.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!P(assetPath).Contains("/Resources/Players/Textures/")) return;
            var t = (TextureImporter)assetImporter;
            t.maxTextureSize = 1024;
            t.mipmapEnabled = true;
            string n = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            if (n.Contains("normal")) t.textureType = TextureImporterType.NormalMap;
            else if (n.Contains("spec") || n.Contains("rough")) t.sRGBTexture = false;
            t.alphaIsTransparency = n.Contains("eyebrow") || n.Contains("eyelash") || n.Contains("hair") || n.Contains("short0") || n.Contains("ponytail");
        }
    }
}
