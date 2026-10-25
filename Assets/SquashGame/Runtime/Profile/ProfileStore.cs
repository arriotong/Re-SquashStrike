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
        public int Venue = 2;                    // PR1: the arena around the court: 0 off, 1 low, 2 high
        // ---- audio ----
        public float MasterVolume = 0.9f, SfxVolume = 1f, RefereeVolume = 1f, MusicVolume = 0.6f, CrowdVolume = 0.8f;
        // ---- accessibility ----
        public float BallScale = 1f;             // x the game's drawn ball (render only)
        public int RingColour = 0;               // 0 white, 1 yellow, 2 cyan
        public float CallTextScale = 1f;
        // ---- gameplay ----
        public bool Popups = true;
        public bool Lets = true;                 // P2-M7: appeals and the referee's calls on interference
        public int Camera = 0;                   // match camera: 0 classic, 1 high, 2 low glass, 3 TV
        public bool LandingMarker = true;        // a light square where the ball will land
        // ---- controls: rebound keys (Input System overrides JSON; "" = the defaults) ----
        public string Bindings = "";

        public GameSettings Clone() { return (GameSettings)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class MenuChoices
    {
        public int Opponent = 0, Difficulty = 0, Length = 0, Scoring = 0;      // 2026-10-25 playtest: a first match is one game on Easy
        /// <summary>P6: Quick Match's opponent (a Stars.QuickLadder id; Opponent / Difficulty are read once, from an older save).</summary>
        public string OpponentId = "club:retriever:62";
        public int VsLength = 2, VsScoring = 0;
        public int Court = 0, VsCourt = 0;       // 2026-10-24: Courts.All index
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
        public Progression.ChallengeState Challenges = new Progression.ChallengeState();     // P3-R2
        public GauntletState Gauntlet = new GauntletState();                                  // P3-R5
        public List<ScenarioRecord> Scenarios = new List<ScenarioRecord>();                   // P3-R6
        public Progression.Wallet Wallet;                                                    // P4: tickets and coins
        public Progression.GachaState Gacha;                                                 // P4: pulls, pity, the shop
    }

    /// <summary>P3-R6: a scenario's best stars.</summary>
    [Serializable]
    public sealed class ScenarioRecord
    {
        public string Id;
        public int Stars, Plays, Wins;
    }

    /// <summary>P3-R5: the Gauntlet - the run in progress (if any) and the records.</summary>
    [Serializable]
    public sealed class GauntletState
    {
        public SquashSim.Modes.GauntletRun Run;
        public int BestFloor, BestScore, Runs;
        /// <summary>P5: coins kept between runs, the talents bought, the codex (each ability's best level), bosses beaten.</summary>
        public int Coins, BossesBeaten;
        public List<SquashSim.RunMod> Talents = new List<SquashSim.RunMod>();
        public List<SquashSim.RunMod> Codex = new List<SquashSim.RunMod>();
    }

    /// <summary>P2-M8: who you are online - no account: a random id and secret made here the first time (the server
    /// knows you by them), the name you play under, the server, and your rating as the server last said.</summary>
    [Serializable]
    public sealed class OnlineIdentity
    {
        public string Id = "", Secret = "", Name = "", Server = "";
        public int Rating = 1000, Rank, Games;
        /// <summary>The name was picked (asked once, on the first visit to ONLINE).</summary>
        public bool NameChosen;
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
        public OnlineIdentity Online = new OnlineIdentity();
    }

    public static class ProfileStore
    {
        public const int CurrentVersion = 6;              // 2: P2-M5 progression; 3: P3-R2 challenges; 4: P4 Gear Drops; 5: P5 the roguelite Gauntlet; 6: P6 the stars
        public const string FileName = "profile.json";

        /// <summary>Where the profile lives (tests may point it elsewhere).</summary>
        public static string Folder = null;
        static string PathOf { get { return System.IO.Path.Combine(Folder ?? Application.persistentDataPath, FileName); } }

        static PlayerProfile _current;
        /// <summary>The profile in use (loaded on first access).</summary>
        public static PlayerProfile Current { get { if (_current == null) _current = Load(); return _current; } }

        /// <summary>Forget the cached profile (tests).</summary>
        public static void Reset() { _current = null; PlayerNames.Reset(); }

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
            fresh.Progress.Wallet = new Progression.Wallet { Tickets = Progression.GearDrops.StartTickets };   // P4: something to pull straight away
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
            if (p.Online == null) p.Online = new OnlineIdentity();
            var g = p.Progress;
            if (g.Owned == null) g.Owned = new List<string>();
            if (g.Seen == null) g.Seen = new List<string>();
            if (g.Loadout == null) g.Loadout = new Progression.Loadout();
            if (g.Achievements == null) g.Achievements = new List<AchievementState>();
            if (g.Stats == null) g.Stats = new Progression.LifetimeStats();
            if (g.Challenges == null) g.Challenges = new Progression.ChallengeState();
            if (g.Gauntlet == null) g.Gauntlet = new GauntletState();
            if (g.Scenarios == null) g.Scenarios = new List<ScenarioRecord>();
            // P4: a wallet, with the welcome tickets (new players get them in Load; JsonUtility makes an empty wallet, so: by version)
            if (g.Wallet == null) g.Wallet = new Progression.Wallet();
            if (p.Version < 4) g.Wallet.Tickets += Progression.GearDrops.StartTickets;
            if (g.Gacha == null) g.Gacha = new Progression.GachaState();
            if (g.Gacha.Pity == null) g.Gacha.Pity = new List<Progression.BannerPity>();
            if (g.Gacha.ShopBought == null) g.Gacha.ShopBought = new List<string>();
            if (string.IsNullOrEmpty(g.Loadout.Trail)) g.Loadout.Trail = "trail_classic";
            if (g.Gauntlet.Run != null && !g.Gauntlet.Run.Active) g.Gauntlet.Run = null;             // JsonUtility's empty object
            if (p.Version < 6) p.Choices.OpponentId = UI.MatchChoices.OldToId(p.Choices.Difficulty);   // P6: Easy / Medium / Hard -> a club player, a star
            if (System.Array.IndexOf(UI.MatchChoices.Ladder, p.Choices.OpponentId) < 0) p.Choices.OpponentId = UI.MatchChoices.Ladder[0];
            if (p.Version < 5) g.Gauntlet.Run = null;                     // P5: a run of the old Gauntlet ends (its best floor is kept)
            if (g.Gauntlet.Talents == null) g.Gauntlet.Talents = new List<SquashSim.RunMod>();
            if (g.Gauntlet.Codex == null) g.Gauntlet.Codex = new List<SquashSim.RunMod>();
            if (g.Gauntlet.Run != null)
            {
                var r = g.Gauntlet.Run;
                if (r.Abilities == null) r.Abilities = new List<SquashSim.RunMod>();
                if (r.Offer == null) r.Offer = new List<string>();
                if (r.Banished == null) r.Banished = new List<string>();
            }
            if (g.Challenges.Daily == null) g.Challenges.Daily = new List<Progression.ChallengeProgress>();
            if (g.Challenges.Weekly == null) g.Challenges.Weekly = new Progression.ChallengeProgress();
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
            Audio.CrowdAudio.Volume = Mathf.Clamp01(s.CrowdVolume);
            AudioListener.volume = Mathf.Clamp01(s.MasterVolume);
            BallView.UserScale = Mathf.Clamp(s.BallScale, 0.75f, 1.6f);
            VenueView.Detail = Mathf.Clamp(s.Venue, 0, 2);
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
