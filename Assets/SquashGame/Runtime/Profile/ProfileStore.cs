// ===========================================================
// Assets/SquashGame/Runtime/Profile/ProfileStore.cs
// P2-M1: the player's profile on disk - one JSON file in the
// persistent data folder. Settings now; career, stats and cosmetics
// get their own sections in later milestones.
//
// On first run the old per-setting PlayerPrefs (aim speed, invert,
// control mode, pop-ups) are imported, so nothing is lost. Apply()
// pushes the settings to the parts of the game that read them.
// ===========================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SquashGame.Profile
{
    [Serializable]
    public sealed class GameSettings
    {
        // ---- controls ----
        public bool Assisted;
        public float AimSpeed = 1f;              // IntentSource.MouseSensitivityScale
        public bool InvertMouseY;
        // ---- video ----
        public int ResolutionW, ResolutionH;     // 0 = leave as is
        public int DisplayMode = 0;              // 0 fullscreen window, 1 exclusive, 2 windowed
        public bool VSync = true;
        public int FrameCap = 0;                 // 0 = none, else fps
        public int Quality = -1;                 // QualitySettings level; -1 = leave
        // ---- audio ----
        public float MasterVolume = 0.9f, SfxVolume = 1f, RefereeVolume = 1f, MusicVolume = 0.6f;
        // ---- accessibility ----
        public float BallScale = 1f;             // x the game's drawn ball (render only)
        public int RingColour = 0;               // 0 white, 1 yellow, 2 cyan
        public float CallTextScale = 1f;
        // ---- gameplay ----
        public bool Popups = true;
        public bool Lets = true;                 // P2-M7: appeals and the referee's calls on interference
        public int Camera = 0;                   // match camera: 0 back wall, 1 high, 2 low glass
        public bool LandingMarker = true;        // a light square where the ball will land
        // ---- controls: rebound keys (Input System overrides JSON; "" = the defaults) ----
        public string Bindings = "";

        public GameSettings Clone() { return (GameSettings)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class MenuChoices
    {
        public int Opponent = 0, Difficulty = 1, Length = 2, Scoring = 0;
        public int VsLength = 2, VsScoring = 0;
    }

    /// <summary>P2-M2: a Target Arcade challenge's (or drill's) best.</summary>
    [Serializable]
    public sealed class ArcadeRecord
    {
        public string Id;
        public int Best, Stars, Plays;
        public string When;
    }

    /// <summary>P2-M2: the practice court's Coach panel, as last left.</summary>
    [Serializable]
    public sealed class CoachChoices
    {
        public int Feed = 0, Side = 2, Speed = 1, Interval = 1, Slow = 0;
        public bool Auto = true, LongTrail = false, WarmBall = true;
        /// <summary>The feeder (else free play: no feeds, the ball in your hand - the default).</summary>
        public bool Feeder;
    }

    /// <summary>P2-M3: the Academy - lessons passed, and the one-time suggestion to a new player.</summary>
    [Serializable]
    public sealed class AcademyProgress
    {
        public List<string> Done = new List<string>();
        public bool Finished;
        public bool Suggested;
    }

    /// <summary>P2-M5: an achievement's state.</summary>
    [Serializable]
    public sealed class AchievementState
    {
        public string Id;
        public int Progress;
        public bool Done;
        public string When;
    }

    /// <summary>P2-M5: XP, the Locker Room (owned and worn), achievements, lifetime stats.</summary>
    [Serializable]
    public sealed class Progress
    {
        public int Xp;
        public List<string> Owned = new List<string>();
        public List<string> Seen = new List<string>();          // looked at in the Locker Room (no "NEW" dot)
        public Progression.Loadout Loadout = new Progression.Loadout();
        public List<AchievementState> Achievements = new List<AchievementState>();
        public Progression.LifetimeStats Stats = new Progression.LifetimeStats();
    }

    [Serializable]
    public sealed class PlayerProfile
    {
        public int Version = ProfileStore.CurrentVersion;
        public GameSettings Settings = new GameSettings();
        public MenuChoices Choices = new MenuChoices();
        public List<ArcadeRecord> Arcade = new List<ArcadeRecord>();
        public CoachChoices Coach = new CoachChoices();
        public AcademyProgress Academy = new AcademyProgress();
        public Progress Progress = new Progress();
    }

    public static class ProfileStore
    {
        public const int CurrentVersion = 2;              // 2: P2-M5 progression
        public const string FileName = "profile.json";

        /// <summary>Where the profile lives (tests may point it elsewhere).</summary>
        public static string Folder = null;
        static string PathOf { get { return System.IO.Path.Combine(Folder ?? Application.persistentDataPath, FileName); } }

        static PlayerProfile _current;
        /// <summary>The profile in use (loaded on first access).</summary>
        public static PlayerProfile Current { get { if (_current == null) _current = Load(); return _current; } }

        /// <summary>Forget the cached profile (tests).</summary>
        public static void Reset() { _current = null; }

        public static PlayerProfile Load()
        {
            try
            {
                string path = PathOf;
                if (File.Exists(path))
                {
                    var p = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(path));
                    if (p != null) return Upgrade(p);
                }
            }
            catch (Exception e) { Debug.LogWarning("Profile: could not read it, starting fresh (" + e.Message + ")"); }
            var fresh = new PlayerProfile();
            ImportPlayerPrefs(fresh.Settings);
            return fresh;
        }

        public static void Save()
        {
            try
            {
                string path = PathOf;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(Current, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e) { Debug.LogWarning("Profile: could not save (" + e.Message + ")"); }
        }

        static PlayerProfile Upgrade(PlayerProfile p)
        {
            if (p.Settings == null) p.Settings = new GameSettings();
            if (p.Choices == null) p.Choices = new MenuChoices();
            if (p.Arcade == null) p.Arcade = new List<ArcadeRecord>();
            if (p.Coach == null) p.Coach = new CoachChoices();
            if (p.Academy == null) p.Academy = new AcademyProgress();
            if (p.Academy.Done == null) p.Academy.Done = new List<string>();
            if (p.Progress == null) p.Progress = new Progress();
            var g = p.Progress;
            if (g.Owned == null) g.Owned = new List<string>();
            if (g.Seen == null) g.Seen = new List<string>();
            if (g.Loadout == null) g.Loadout = new Progression.Loadout();
            if (g.Achievements == null) g.Achievements = new List<AchievementState>();
            if (g.Stats == null) g.Stats = new Progression.LifetimeStats();
            // (future versions: migrate fields here)
            p.Version = CurrentVersion;
            return p;
        }

        /// <summary>This challenge's record, or null if never played.</summary>
        public static ArcadeRecord Record(string id)
        {
            foreach (var r in Current.Arcade) if (r.Id == id) return r;
            return null;
        }

        /// <summary>Note a finished play; true when it is a new best. Saves.</summary>
        public static bool Submit(string id, int score, int stars)
        {
            var r = Record(id);
            if (r == null) { r = new ArcadeRecord { Id = id, Best = int.MinValue }; Current.Arcade.Add(r); }
            r.Plays++;
            bool best = score > r.Best;
            if (best) { r.Best = score; r.When = DateTime.Now.ToString("yyyy-MM-dd"); }
            if (stars > r.Stars) r.Stars = stars;
            Save();
            return best;
        }

        public static bool LessonDone(string id) { return Current.Academy.Done.Contains(id); }

        /// <summary>Note a passed lesson; true when this one finished the whole Academy. Saves.</summary>
        public static bool CompleteLesson(string id, IEnumerable<string> all)
        {
            var a = Current.Academy;
            if (!a.Done.Contains(id)) a.Done.Add(id);
            bool every = true;
            foreach (var l in all) if (!a.Done.Contains(l)) { every = false; break; }
            bool first = every && !a.Finished;
            if (every) a.Finished = true;
            Save();
            return first;
        }

        /// <summary>The settings the game kept in PlayerPrefs before the profile existed.</summary>
        public static void ImportPlayerPrefs(GameSettings s)
        {
            try
            {
                s.AimSpeed = PlayerPrefs.GetFloat("aim.mouseSensitivity", s.AimSpeed);
                s.InvertMouseY = PlayerPrefs.GetInt("aim.invertY", s.InvertMouseY ? 1 : 0) == 1;
                s.Assisted = PlayerPrefs.GetInt("controls.assisted", s.Assisted ? 1 : 0) == 1;
                s.Popups = PlayerPrefs.GetInt("ui.popups", s.Popups ? 1 : 0) == 1;
            }
            catch (Exception) { }
        }

        /// <summary>Push the settings to the game (input, ring, pop-ups, audio, video).</summary>
        public static void Apply(GameSettings s, bool video = true)
        {
            IntentSource.MouseSensitivityScale = s.AimSpeed;
            IntentSource.InvertMouseY = s.InvertMouseY;
            IntentSource.Assisted = s.Assisted;
            ArcadePopups.Enabled = s.Popups;
            MatchRunner.Lets = s.Lets;
            GameplayCameraRig.Preset = s.Camera;
            LandingMarkerView.Enabled = s.LandingMarker;
            if (IntentSource.Overrides != (s.Bindings ?? "")) IntentSource.SetOverrides(s.Bindings);
            AimTargetView.RingColourIndex = s.RingColour;
            Audio.AudioDirector.SfxVolume = s.SfxVolume;
            Audio.AudioDirector.RefereeVolume = s.RefereeVolume;
            Audio.MenuMusic.Volume = Mathf.Clamp01(s.MusicVolume);
            AudioListener.volume = Mathf.Clamp01(s.MasterVolume);
            BallView.UserScale = Mathf.Clamp(s.BallScale, 0.75f, 1.6f);
            HudLayer.CallTextScale = Mathf.Clamp(s.CallTextScale, 0.8f, 1.5f);
            if (!video || Application.isBatchMode) return;
            QualitySettings.vSyncCount = s.VSync ? 1 : 0;
            Application.targetFrameRate = s.FrameCap > 0 ? s.FrameCap : -1;
            if (s.Quality >= 0 && s.Quality < QualitySettings.names.Length) QualitySettings.SetQualityLevel(s.Quality, true);
            var modes = new[] { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed };
            var mode = modes[Mathf.Clamp(s.DisplayMode, 0, 2)];
            if (s.ResolutionW > 0 && s.ResolutionH > 0)
            {
                if (Screen.width != s.ResolutionW || Screen.height != s.ResolutionH || Screen.fullScreenMode != mode)
                    Screen.SetResolution(s.ResolutionW, s.ResolutionH, mode);
            }
            else if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
        }
    }
}
