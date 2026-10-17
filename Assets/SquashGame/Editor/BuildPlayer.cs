// ===========================================================
// Assets/SquashGame/Editor/BuildPlayer.cs
// The Windows build: Builds/Windows/reSquashStrike.exe, with the Match
// scene (setup panel = the menu) first and the warm-up court as the
// practice court.
//
//   Menu:   re:SquashStrike > Build Windows Player
//   CLI:    unity build . --target StandaloneWindows64
//             --execute-method SquashGame.Editor.BuildPlayer.BuildWindows
//             -o Builds/Windows/reSquashStrike.exe
// ===========================================================

using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SquashGame.Editor
{
    public static class BuildPlayer
    {
        public const string DefaultOutput = "Builds/Windows/reSquashStrike.exe";

        [MenuItem("re:SquashStrike/Build Windows Player")]
        public static void BuildFromMenu() { Build(DefaultOutput); }

        /// <summary>Command-line entry: honours -buildOutput, else the default path. Exits non-zero on failure.</summary>
        public static void BuildWindows()
        {
            string output = DefaultOutput;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-buildOutput") output = args[i + 1];
            bool ok = Build(output);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Build(string output)
        {
            // Both scenes from code, so the build never depends on stale scene files.
            SceneBuilder.Build();

            PlayerSettings.companyName = "arriotong";
            PlayerSettings.productName = "re:SquashStrike";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.visibleInBackground = true;

            string dir = Path.GetDirectoryName(Path.GetFullPath(output));
            Directory.CreateDirectory(dir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.MatchScenePath, SceneBuilder.ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log("[Build] " + s.result + "  " + output + "  " + (s.totalSize / (1024 * 1024)) + " MB in " +
                      s.totalTime.TotalSeconds.ToString("0") + " s, " + s.totalErrors + " errors");
            return s.result == BuildResult.Succeeded;
        }
    }
}
