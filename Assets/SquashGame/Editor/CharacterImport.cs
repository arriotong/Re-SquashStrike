// ===========================================================
// Assets/SquashGame/Editor/CharacterImport.cs
// Import settings for the athletes' bodies (Resources/Characters: the
// Quaternius Universal Base Characters, CC0): a plain rig (the game
// drives the bones itself - AthleteSkin), no imported materials (the kit
// makes its own), normal maps marked as such, textures capped at 1K.
// Also a dump of a model's bones and meshes, for mapping the rig.
// ===========================================================

using System.Text;
using UnityEditor;
using UnityEngine;

namespace SquashGame.Editor
{
    public sealed class CharacterImport : AssetPostprocessor
    {
        const string Folder = "Assets/SquashGame/Resources/Characters";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var m = (ModelImporter)assetImporter;
            m.animationType = ModelImporterAnimationType.Generic;
            m.optimizeGameObjects = false;
            m.importAnimation = false;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
            m.importBlendShapes = false;
            m.importCameras = false;
            m.importLights = false;
            m.isReadable = true;                 // the kit splits the body into regions at runtime
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var t = (TextureImporter)assetImporter;
            t.maxTextureSize = 1024;
            if (assetPath.Contains("_Normal")) t.textureType = TextureImporterType.NormalMap;
        }

        /// <summary>-executeMethod SquashGame.Editor.CharacterImport.Dump: bones and meshes of each body.</summary>
        public static void Dump()
        {
            var sb = new StringBuilder();
            foreach (var name in new[] { "Superhero_Male_FullBody", "Superhero_Female_FullBody", "Hair/Hair_Buns", "Hair/Eyebrows_Regular" })
            {
                var go = Resources.Load<GameObject>("Characters/" + name);
                sb.AppendLine("==== " + name + (go == null ? " (not found)" : ""));
                if (go == null) continue;
                Walk(go.transform, 0, sb);
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mesh = smr.sharedMesh;
                    sb.AppendLine("  skinned " + smr.name + ": " + mesh.vertexCount + " verts, " + mesh.subMeshCount + " submeshes, " + smr.bones.Length + " bones, root " + (smr.rootBone ? smr.rootBone.name : "-") +
                                  ", bounds " + mesh.bounds);
                }
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                    sb.AppendLine("  mesh " + mf.name + ": " + mf.sharedMesh.vertexCount + " verts, bounds " + mf.sharedMesh.bounds);
            }
            Debug.Log("[CharacterDump]\n" + sb);
            System.IO.File.WriteAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Captures", "CharacterDump.txt")), sb.ToString());
            EditorApplication.Exit(0);
        }

        static void Walk(Transform t, int depth, StringBuilder sb)
        {
            sb.Append(' ', depth * 2).Append(t.name).Append("  pos ").Append(t.localPosition.ToString("F3")).Append(" rot ").Append(t.localRotation.eulerAngles.ToString("F0"))
              .Append(" scale ").Append(t.localScale.ToString("F2")).Append("  world ").AppendLine(t.position.ToString("F3"));
            foreach (Transform c in t) Walk(c, depth + 1, sb);
        }
    }
}
