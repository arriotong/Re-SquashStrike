// ===========================================================
// Assets/SquashGame/Editor/PoseLab.cs
// M7: the Pose Lab - edit the athlete's key poses live.
//
//   Menu:  re:SquashStrike > Pose Lab
//
// Pick a pose, change its numbers (crouch, lean/twist rotations, feet,
// racket sweet spot / direction / string facing, free hand) and the
// preview athlete in the open scene follows at once. "Save" writes the
// library to Assets/SquashGame/Resources/PoseLibrary.asset, which the
// game then uses instead of the poses authored in code (DefaultPoses).
// "Reload from code" throws the asset's edits away.
// ===========================================================

using SquashGame.Athlete;
using UnityEditor;
using UnityEngine;

namespace SquashGame.Editor
{
    public sealed class PoseLab : EditorWindow
    {
        const string AssetPath = "Assets/SquashGame/Resources/PoseLibrary.asset";

        PoseLibrary _lib;
        SerializedObject _so;
        int _selected;
        Vector2 _scroll;
        AthleteRig _preview;

        [MenuItem("re:SquashStrike/Pose Lab")]
        public static void Open() { GetWindow<PoseLab>("Pose Lab"); }

        void OnEnable() { Load(); }

        void Load()
        {
            _lib = AssetDatabase.LoadAssetAtPath<PoseLibrary>(AssetPath);
            if (_lib == null)
            {
                _lib = CreateInstance<PoseLibrary>();
                _lib.Poses = DefaultPoses.Build();
            }
            _so = new SerializedObject(_lib);
        }

        void EnsurePreview()
        {
            if (_preview != null) return;
            var existing = GameObject.Find("PoseLab Athlete");
            if (existing != null) _preview = existing.GetComponent<AthleteRig>();
            if (_preview != null) return;
            var palette = AssetDatabase.LoadAssetAtPath<GreyboxPalette>("Assets/SquashGame/Materials/GreyboxPalette.asset");
            var go = new GameObject("PoseLab Athlete") { hideFlags = HideFlags.DontSave };
            _preview = go.AddComponent<AthleteRig>();
            _preview.Build(palette, Greybox.Hex(0x7d93ab));
            _preview.HoldRacketInHand();
        }

        void OnGUI()
        {
            if (_lib == null || _so == null) Load();
            _so.Update();
            var poses = _so.FindProperty("Poses");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preview in scene")) { EnsurePreview(); ApplySelected(); }
            if (GUILayout.Button("Save")) Save();
            if (GUILayout.Button("Reload from code"))
            {
                if (EditorUtility.DisplayDialog("Pose Lab", "Discard the saved pose edits and reload the poses authored in code?", "Reload", "Cancel"))
                {
                    _lib.Poses = DefaultPoses.Build();
                    _so = new SerializedObject(_lib);
                    if (AssetDatabase.LoadAssetAtPath<PoseLibrary>(AssetPath) != null) Save();
                    ApplySelected();
                    return;
                }
            }
            if (GUILayout.Button("Capture pose sheet")) { Save(); PoseSheet.Capture(null); }
            EditorGUILayout.EndHorizontal();

            string[] names = new string[poses.arraySize];
            for (int i = 0; i < names.Length; i++)
            {
                var p = poses.GetArrayElementAtIndex(i);
                names[i] = (i + 1) + "  " + p.FindPropertyRelative("Group").stringValue + " / " + p.FindPropertyRelative("Name").stringValue;
            }
            if (names.Length == 0) { EditorGUILayout.HelpBox("No poses.", MessageType.Info); return; }
            _selected = Mathf.Clamp(EditorGUILayout.Popup("Pose", _selected, names), 0, names.Length - 1);

            EditorGUILayout.HelpBox("Athlete local axes: +X = their LEFT, +Y = up, +Z = BEHIND (they face -Z). " +
                                    "Racket: Sweet = sweet spot position, Racket Dir = butt to tip, Racket Normal = where the strings face.",
                                    MessageType.None);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUI.BeginChangeCheck();
            var sel = poses.GetArrayElementAtIndex(_selected);
            foreach (string field in new[] { "Name", "Group", "Pelvis", "FeetIK", "FootR", "FootL", "FootYawR", "FootYawL",
                                             "RacketIK", "Sweet", "RacketDir", "RacketNormal", "ElbowR", "HandIK", "HandL", "ElbowL",
                                             "LevelHead", "Rots" })
                EditorGUILayout.PropertyField(sel.FindPropertyRelative(field), true);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUILayout.EndScrollView();
            _so.ApplyModifiedProperties();
            if (changed && _preview != null) ApplySelected();
        }

        void ApplySelected()
        {
            if (_preview == null || _lib.Poses.Count == 0) return;
            PoseMath.Apply(_preview, _lib.Poses[Mathf.Clamp(_selected, 0, _lib.Poses.Count - 1)]);
            _preview.UpdateBlends();
            SceneView.RepaintAll();
        }

        void Save()
        {
            if (AssetDatabase.LoadAssetAtPath<PoseLibrary>(AssetPath) == null)
            {
                System.IO.Directory.CreateDirectory("Assets/SquashGame/Resources");
                var copy = CreateInstance<PoseLibrary>();
                foreach (var p in _lib.Poses) copy.Poses.Add(p.Clone());
                AssetDatabase.CreateAsset(copy, AssetPath);
                _lib = copy;
                _so = new SerializedObject(_lib);
            }
            EditorUtility.SetDirty(_lib);
            AssetDatabase.SaveAssets();
            PoseLibrary.ResetActive();
        }

        void OnDestroy()
        {
            if (_preview != null) DestroyImmediate(_preview.gameObject);
        }
    }
}
