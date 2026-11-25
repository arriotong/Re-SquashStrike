// ===========================================================
// Assets/SquashGame/Runtime/MatchRunner.cs
// A match on the shared frame loop: SquashSim's MatchSimulation, set up
// by the front end (UI/FrontEnd: Quick Match / Local Versus), against the AI or a second
// local player.
//
//   vs AI      athlete 0 = every device; athlete 1 = OpponentBrain (or
//              the wall-bot), playing through the same InputIntent
//   Local 2P   athlete 0 = keyboard + mouse (or pad 1 when two pads are
//              connected), athlete 1 = a pad of their own
//
// Flow: setup -> knock-up (cold ball) -> games, with a break between
// them in which the ball cools -> result -> back to setup. Ready
// (Enter / pad Y) skips the knock-up or the break; no debug keys needed.
// After every let / no let / stroke call the referee's view is replayed
// automatically (V replays the last rally on demand).
//
// P2-M6: every match is built from a ReplaySetup (SquashSim.Replays), and
// a real match (not the menus' demo) is recorded by a MatchRecorder: the
// sim steps on the quantised copy of each person's input. When the
// match ends - or is left - ReplayFinished hands the file on.
//
// P2-M8 online: BeginOnline plays a match against someone on another
// machine. A LockstepSession steps the sim a tick (4 steps) at a time
// from both players' inputs; this machine steers athlete Me. No pause
// (Esc opens the menu over the match, which plays on), no slow motion,
// no automatic reviews; neutral attributes; the session records it.
// ===========================================================

using System.Collections.Generic;
using SquashSim;
using SquashSim.Net;
using SquashSim.Replays;
using SquashGame.Online;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SquashGame
{
    [System.Serializable]
    public sealed class MatchSettings
    {
        public bool TwoPlayer;
        public string Opponent = "retriever";
        [Range(0, 1)] public float Difficulty = 0.5f;
        public int GamesToWin = 3;
        public string Scoring = "par";
        public bool WarmUp = false;              // batch 4: the knock-up, when Settings > Gameplay > Warm-up is on (Quick Match, Versus, career)
        /// <summary>P2-M4: a Career match (the post-match and pause screens go back to the career).</summary>
        public bool Career;
        /// <summary>Score-bug names instead of "YOU" and the AI's personality (null = the defaults).</summary>
        public string P1Name, P2Name;
        /// <summary>P2-M5: the opponent's kit from this seed (a career pro); 0 = the personality's signature kit.</summary>
        public uint KitSeed;
        /// <summary>The court to play on (Courts.All id); null = the one being drawn.</summary>
        public string Court;
        /// <summary>Camera work: a prestige match's title ("THE FINAL  ·  BRITISH OPEN") - it opens with the fly-around; null: none.</summary>
        public string Prestige;
        /// <summary>How full the stands are, 0..1 (0 = full: the default).</summary>
        public float CrowdFill;
        /// <summary>P3-R5: a Gauntlet floor (the post-match and pause screens go back to the run).</summary>
        public bool Gauntlet;
        /// <summary>P3-R6: a scenario's id (the post-match screen scores its stars); null otherwise.</summary>
        public string Scenario;
        /// <summary>P3-C1: your athlete's attributes (null = neutral: the game as it always was).</summary>
        public SquashSim.AthleteAttributes Attributes;
        /// <summary>P3-C1: start part-way (Scenarios): points [you, them], games [you, them], who serves (-1: the usual).</summary>
        public int StartPoints0, StartPoints1, StartGames0, StartGames1, StartServer = -1;
        /// <summary>P3-C1: every game starts at this score (the Gauntlet's head start).</summary>
        public int EachGame0, EachGame1;
        /// <summary>P2-M8: an online match (ranked or not; the server's id for it); the opponent's kit as they wear it (JSON).</summary>
        public bool Online, Ranked;
        public long MatchId;
        public string OppKit;
        /// <summary>P5 the Gauntlet: a game to this many (0: the scoring's own); the opponent's attributes; both sides' run abilities.</summary>
        public int ParPoints;
        /// <summary>2026-11-21 a Gauntlet floor (the arcade remake): its target, rallies, multiplier and wheel - the game's own score
        /// doesn't decide it. Null: an ordinary match.</summary>
        public SquashSim.Modes.FloorPlay GauntletFloor;
        /// <summary>P5: Focus charges each game (the Gauntlet's slow motion; 0: none).</summary>
        public int FocusCharges;
        /// <summary>2026-11-20 Quick Match's Abilities: Focus is paid for from the Special meter (RunMods.FocusCost), not charges.</summary>
        public bool MeterFocus;
        public SquashSim.AthleteAttributes OppAttributes;
        /// <summary>P7 phase 5: your career gear's looks (cosmetic ids: a frame, shoes, a shirt) over your loadout.</summary>
        public string[] GearLooks;
        public List<SquashSim.RunMod> Mods, OppMods;
        /// <summary>2026-11-22 your class (PlayerClasses id; "" none) and player 2's in local versus.</summary>
        public string Class = "", Class2 = "";
        /// <summary>2026-11-24 your training perks on top (Quick Match, career, the Gauntlet; never versus or online).</summary>
        public bool Perks;
        /// <summary>2026-11-25 an Academy match drill's lesson id (MatchDrills); null otherwise.</summary>
        public string Drill;

        public MatchSettings Clone() { return (MatchSettings)MemberwiseClone(); }

        /// <summary>P6: a star or club opponent's rating sets the difficulty; a star brings its attributes, signature shots
        /// and kit (unless these settings already carry their own). Returns itself.</summary>
        public MatchSettings Dressed()
        {
            Difficulty = (float)SquashSim.Career.Stars.DifficultyFor(Opponent, Difficulty);
            var st = SquashSim.Career.Stars.Find(Opponent);
            if (st == null) return this;
            if (OppAttributes == null) OppAttributes = st.Attributes.Clone();
            if (OppMods == null) OppMods = new List<SquashSim.RunMod>(st.Signature);
            if (KitSeed == 0) KitSeed = st.KitSeed;
            return this;
        }
        /// <summary>One game, no knock-up: straight to the first serve (tests, quick play).</summary>
        public static MatchSettings QuickGame(string opponent = "retriever")
        {
            return new MatchSettings { Opponent = opponent, GamesToWin = 1, WarmUp = false };
        }
    }

    public sealed class MatchRunner : SimRunnerBase
    {
        [Tooltip("Show the pre-match panel on load. Off = start Settings at once.")]
        public bool ShowSetup = true;
        public MatchSettings Settings = new MatchSettings();
        [Tooltip("Seed for the opponent's choices (deterministic).")]
        public uint BotSeed = 12345;
        [Tooltip("Seconds of play shown in a referee replay (V), and its speed.")]
        public float ReplaySeconds = 3.5f, ReplaySpeed = 0.5f;
        [Tooltip("Seconds of play in the automatic review of a stroke.")]
        public float ReviewSeconds = 2f;

        /// <summary>The last rally's call on interference (the sim's MatchSimulation.LastCall; null when it ended any other way).</summary>
        public CallSnapshot LastCall { get; private set; }
        /// <summary>The review on screen is of LastCall (an automatic stroke review, or V after a call).</summary>
        public bool ReviewingCall { get; private set; }
        int _reviewShown;
        /// <summary>Who was going for the ball when the last call was made (-1: no call).</summary>
        public int LastCallStriker { get; private set; } = -1;
        int _reviewFrames;

        public string OpponentName { get; private set; }

        /// <summary>2026-10-30 (the kid test): a name without the grown-ups' "(Medium)" after it.</summary>
        public static string PlainName(string name)
        {
            int i = name.IndexOf(" (", System.StringComparison.Ordinal);
            return i > 0 ? name.Substring(0, i) : name;
        }
        /// <summary>HUD names for athlete 0 and 1.</summary>
        public readonly string[] Names = { "YOU", "OPPONENT" };
        /// <summary>The second local player's input (Local 2P), else null.</summary>
        public IntentSource Input2 { get; private set; }
        /// <summary>The pre-match panel is up; the simulation waits.</summary>
        public bool InSetup { get; private set; }
        /// <summary>P2-M1: the menus' background - an AI-vs-AI rally on the live court, no local player, no HUD.</summary>
        public bool Demo { get; private set; }
        protected override bool PauseAllowed { get { return !Demo && !InSetup && Puzzle == null; } }

        /// <summary>The menus' demo rally runs in slow motion (the old home screen: 0.4x).</summary>
        public const double DemoSpeed = GameTuning.Speed.MenuDemo;

        /// <summary>Play the menus' demo: two AIs, on and on (a new match when one ends).</summary>
        public void BeginDemo()
        {
            EndOnline(true);
            EndRecording(true);
            Settings = new MatchSettings { Opponent = "attacker", Difficulty = 0.42f, GamesToWin = 1, Scoring = "par", WarmUp = false };
            Input.UseDevices(null);
            if (Input2 != null) { Input2.Dispose(); Input2 = null; }
            ApplyConfig(Settings);
            BotSeed = BotSeed * 1664525u + 1013904223u;
            NewMatch();
            Sim.Intents[0] = new InputIntent();                          // the player's own input is not used
            Sim.Bots[0] = new OpponentBrain(AiProfile.ByName("retriever").WithDifficulty(0.42), BotSeed ^ 0x9E3779B9u);
            Sim.Players[0].WallAimEnabled = true;
            Sim.Players[0].ArcadeHitEnabled = true;
            Names[0] = "RETRIEVER"; Names[1] = "ATTACKER";
            ClearDebugHistory();
            Puzzle = null;
            Demo = true;
            InSetup = false;
            DeviceLost = null;
            Clock.Resume();
            Clock.TimeScale = DemoSpeed;
            Clock.Flush();
            SnapViews();
        }
        /// <summary>A local player's pad was unplugged: play is paused until it is back.</summary>
        public string DeviceLost { get; private set; }

        bool _replayRequested;
        string _replayCaption;

        public MatchSimulation Sim { get; private set; }

        /// <summary>P2-M6: the match being recorded (null in the demo and the setup panel).</summary>
        public MatchRecorder Recorder { get; private set; }
        /// <summary>A recording ended: the match was played out, or left (file.Retired). Raised once per match.</summary>
        public static event System.Action<ReplayFile> ReplayFinished;
        ReplaySetup _setup;
        /// <summary>The setup this match was built from (tests: the perks on your attributes).</summary>
        public ReplaySetup Setup { get { return _setup; } }
        IAthleteBrain _autopilot;
        InputIntent _autoIntent;

        /// <summary>Tests: an AI plays for athlete 0 through the same input path a person's input takes (so it is recorded).</summary>
        public void SetAutopilot(IAthleteBrain brain)
        {
            _autopilot = brain != null ? new PersonStandIn(brain) : null;
            if (brain == null || Demo || InSetup) return;
            if (OnlineMatch != null) { _autoIntent = new InputIntent(); return; }       // online: the session records it
            _autoIntent = new InputIntent();
            Recorder = new MatchRecorder(Sim, _setup, new[] { _autoIntent, Settings.TwoPlayer && Input2 != null ? Input2.Intent : null },
                                         Application.version, System.DateTime.Now.Ticks);
        }

        /// <summary>The recording ends (the match was left, or a new one starts): hand it on if it holds anything.</summary>
        void EndRecording(bool left)
        {
            var rec = Recorder;
            Recorder = null;
            _autopilot = null;
            if (rec == null) return;
            bool was = rec.Finished;
            rec.Finish(left);
            if (was) return;
            var d = DescribeReplay; if (d != null) d(rec.File, this);
            Publish(rec);
        }

        static void Publish(MatchRecorder rec)
        {
            if (rec.Invalid || rec.File.Rallies.Count == 0) return;
            var h = ReplayFinished; if (h != null) h(rec.File);
        }

        /// <summary>Meta for the replay's library card (names, mode, kits): set by the front end when a match starts.</summary>
        public static System.Action<ReplayFile, MatchRunner> DescribeReplay;

        public override BallState Ball { get { return Sim.Ball; } }
        public override TestPlayerController[] Athletes { get { return Sim.Players; } }
        public override IReadOnlyList<SimEvent> StepEvents { get { return Sim.StepEvents; } }
        public override List<Surface> Surfaces { get { return Sim.Surfaces; } }

        protected override void CreateSimulation()
        {
            ApplyConfig(ShowSetup ? MatchSettings.QuickGame(Settings.Opponent) : Settings);
            NewMatch();
            InSetup = ShowSetup;
        }

        /// <summary>The Lets setting (Settings > Gameplay): taken by each new match, and recorded with it.</summary>
        public static bool Lets = true;

        /// <summary>The setup a match is built from (and recorded with).</summary>
        ReplaySetup SetupOf(MatchSettings s)
        {
            var setup = new ReplaySetup
            {
                TwoPlayer = s.TwoPlayer && Input2 != null, Opponent = s.Opponent, Difficulty = s.Difficulty, Scoring = s.Scoring,
                GamesToWin = Mathf.Max(1, s.GamesToWin), WarmUp = s.WarmUp, BotSeed = BotSeed, FirstServer = 0, Lets = Lets,
                Attr0 = s.Attributes != null ? s.Attributes.Clone() : new AthleteAttributes(),
                StartPoints0 = s.StartPoints0, StartPoints1 = s.StartPoints1, StartGames0 = s.StartGames0, StartGames1 = s.StartGames1,
                StartServer = s.StartServer, EachGame0 = s.EachGame0, EachGame1 = s.EachGame1,
                ParPoints = s.ParPoints, Attr1 = s.OppAttributes != null ? s.OppAttributes.Clone() : null,
                Mods0 = s.Mods != null ? new List<RunMod>(s.Mods) : null, Mods1 = s.OppMods != null ? new List<RunMod>(s.OppMods) : null,
            };
            // 2026-11-22 the classes: your class (and player 2's in local versus) as attributes and cards on top of the rest
            if (!string.IsNullOrEmpty(s.Class))
            {
                setup.Attr0 = PlayerClasses.Attributes(setup.Attr0, s.Class);
                setup.Mods0 = PlayerClasses.Mods(setup.Mods0, s.Class);
                setup.Class0 = s.Class;
            }
            if (s.Perks && !setup.TwoPlayer) setup.Attr0 = Progression.Machines.Boost(setup.Attr0);
            // 2026-11-25 the developer's tool: your Special meter full every rally (the Special card and "practice"; recorded with the match)
            if (Profile.DevMode.FullMeter && !s.Online && !setup.TwoPlayer)
            {
                var mods = setup.Mods0 != null ? new List<RunMod>(setup.Mods0) : new List<RunMod>();
                if (!mods.Exists(x => x.Id == "special")) mods.Add(new RunMod { Id = "special", Level = 1 });
                if (!mods.Exists(x => x.Id == "practice")) mods.Add(new RunMod { Id = "practice", Level = 1 });
                setup.Mods0 = mods;
            }      // 2026-11-24 (recorded with the match)
            if (setup.TwoPlayer && !string.IsNullOrEmpty(s.Class2))
            {
                setup.Attr1 = PlayerClasses.Attributes(setup.Attr1, s.Class2);
                setup.Mods1 = PlayerClasses.Mods(setup.Mods1, s.Class2);
                setup.Class1 = s.Class2;
            }
            return setup;
        }

        void ApplyConfig(MatchSettings s) { ReplayMatch.Configure(SetupOf(s)); }

        void NewMatch()
        {
            if (Sim != null) Sim.RallyEnded -= OnRallyEnded;
            _setup = SetupOf(Settings);
            Sim = ReplayMatch.Build(_setup);                 // the config, the opponent, who plays the simple system
            Sim.Intents[0] = Input.Intent;
            if (_setup.TwoPlayer)
            {
                Sim.Intents[1] = Input2.Intent;
                OpponentName = "PLAYER 2";
                Names[0] = "PLAYER 1"; Names[1] = "PLAYER 2";
            }
            else
            {
                var star = SquashSim.Career.Stars.Find(Settings.Opponent);
                OpponentName = Settings.Opponent == "wallbot" ? "WALL-BOT"
                             : star != null ? SquashSim.Career.Stars.LastName(star).ToUpperInvariant()           // P6: the score bug's surname
                             : PlainName(AiProfile.For(Settings.Opponent, Settings.Difficulty).Name).ToUpperInvariant();
                if (!string.IsNullOrEmpty(Settings.P2Name)) OpponentName = Settings.P2Name;
                Names[0] = string.IsNullOrEmpty(Settings.P1Name) ? "YOU" : Settings.P1Name; Names[1] = OpponentName;
            }
            Sim.RallyEnded += OnRallyEnded;
            // 2026-11-21 a Gauntlet floor: a fresh copy of its play (a restart plays it from the start again)
            Floor = Settings.GauntletFloor != null ? Settings.GauntletFloor.Clone() : null;
            MatchOverAt = -1f;
            FloorOver = false; FloorPops = 0; _floorPts = Sim.Score.Points[0];
        }

        // ---------------- 2026-11-21 the Gauntlet's arcade floors ----------------

        /// <summary>The floor being played (null: an ordinary match), its last rally's pop, the floor over (cleared or failed).</summary>
        public SquashSim.Modes.FloorPlay Floor { get; private set; }
        public SquashSim.Modes.FloorScore LastFloorPop { get; private set; }
        public float LastFloorPopAt { get; private set; } = -99f;
        public int FloorPops { get; private set; }
        public bool FloorOver { get; private set; }
        /// <summary>The wheel is due a spin (the front end shows it, the match paused).</summary>
        public bool WheelDue { get { return Floor != null && Floor.SpinDue && !FloorOver; } }
        int _floorPts;

        void FloorTick()
        {
            if (Floor != null && Sim != null && Sim.Phase == MatchPhase.Rally) _floorPts = Sim.Score.Points[HumanIndex >= 0 ? HumanIndex : 0];
        }

        /// <summary>A rally of the floor ended: how it was won (its game points: the Jackpot cards' extra ones), scored.</summary>
        void FloorRally(RallyOutcome o)
        {
            int me = HumanIndex >= 0 ? HumanIndex : 0;
            int pts = Sim.Score.Points[me];
            int gp = pts - _floorPts; _floorPts = pts;
            bool let = o == null || o.Kind == RallyEnd.Let || o.Winner < 0;
            bool won = !let && o.Winner == me;
            bool notUp = !let && o.Kind == RallyEnd.NotUp;
            var touch = Sim.Ball.LastRacketContact;
            var r = new SquashSim.Modes.FloorRally
            {
                Won = won, Let = let, Kind = let ? RallyEnd.Let : o.Kind, Shots = Sim.Referee.Shots, GamePoints = gp,
                Ace = won && notUp && Sim.Referee.Shots == 1,
                Nick = won && notUp && Sim.NickBy == me,
                Kill = won && notUp && Sim.LastStriker == me && touch != null && touch.Technique == "kill",
            };
            if (let) return;
            LastFloorPop = SquashSim.Modes.GauntletFloor.Rally(Floor, r);
            LastFloorPopAt = Time.unscaledTime; FloorPops++;
            if (!Floor.SpinDue && Floor.Done) EndFloor();
        }

        /// <summary>The wheel has spun (WheelScreen): on with the floor, or its end.</summary>
        public void WheelSpun()
        {
            if (Floor == null) return;
            if (Floor.Done) EndFloor();
            else Clock.Resume();
        }

        void EndFloor() { FloorOver = true; Clock.Pause(); }

        // ---------------- devices ----------------

        /// <summary>Is Local 2P possible right now? 2026-11-23: any two controls that don't clash - two controllers, a controller and a
        /// keyboard, two keyboards, or one keyboard's two halves (InputSetup).</summary>
        public static bool CanPlayTwoPlayer { get { return InputSetup.CanPlayTwo; } }

        /// <summary>2026-11-23 (the user: "they can use anything or even both using controllers"): the players' chosen controls (Local
        /// Versus's Controls step); unset, gone, or clashing - the usual pair (InputSetup.Defaults).</summary>
        public static void ChosenControls(out PlayerControls p1, out PlayerControls p2)
        {
            var c = Profile.ProfileStore.Current.Choices;
            string a = c.VsControls1, am = c.VsMouse1, b = c.VsControls2, bm = c.VsMouse2;
            var prim = InputSetup.Primaries();
            if (InputSetup.Find(prim, a) == null || InputSetup.Find(prim, b) == null || InputSetup.Clash(a, b)) InputSetup.Defaults(out a, out am, out b, out bm);
            if (!string.IsNullOrEmpty(am) && am == bm) bm = "";
            p1 = InputSetup.Resolve(a, am); p2 = InputSetup.Resolve(b, bm);
        }

        /// <summary>Start a match with these settings (from the setup panel, or a test).</summary>
        public void BeginMatch(MatchSettings s)
        {
            EndOnline(true);
            EndRecording(true);
            Settings = s.Clone();
            _focusGame = -1;                                                  // P5: a fresh set of Focus charges
            if (!string.IsNullOrEmpty(Settings.Court)) Courts.CurrentId = Settings.Court;     // CourtView rebuilds on the next frame
            Courts.CrowdFill = Settings.CrowdFill > 0f ? Settings.CrowdFill : 1f;
            if (Settings.TwoPlayer && !CanPlayTwoPlayer) Settings.TwoPlayer = false;
            if (Settings.TwoPlayer)
            {
                ChosenControls(out PlayerControls p1, out PlayerControls p2);
                Input.UseControls(p1);
                if (Input2 == null) Input2 = new IntentSource(p2.Devices);
                Input2.UseControls(p2);
            }
            else
            {
                Input.UseControls(null);
                if (Input2 != null) { Input2.Dispose(); Input2 = null; }
            }
            BotSeed = BotSeed * 1664525u + 1013904223u;   // a different (still deterministic) bot each match
            ApplyConfig(Settings);
            NewMatch();
            Recorder = new MatchRecorder(Sim, _setup, new[] { Input.Intent, _setup.TwoPlayer ? Input2.Intent : null },
                                         Application.version, System.DateTime.Now.Ticks);
            ClearDebugHistory();
            Demo = false;
            InSetup = false;
            DeviceLost = null;
            Clock.Resume();
            Clock.TimeScale = GameSpeed;
            Clock.Flush();
            SnapViews();
        }

        /// <summary>Back to the pre-match panel.</summary>
        public void ReturnToSetup()
        {
            EndOnline(true);
            EndRecording(true);
            InSetup = true;
            if (ReplayActive) StopReplay();
        }

        protected override bool SampleExtraInput(float dt)
        {
            if (Input2 == null) return false;
            Input2.Sample(dt);
            return Input2.ConsumePause();
        }

        protected override IntentSource SourceFor(int i)
        {
            if (Puzzle != null) return null;                                 // a shot puzzle: both are AIs, played as measured
            if (OnlineMatch != null) return i == OnlineMatch.Me ? Input : null;
            if (i == 0) return Input;
            return i == 1 && Settings.TwoPlayer ? Input2 : null;
        }

        protected override void OnDestroy()
        {
            if (Input2 != null) Input2.Dispose();
            EndOnline(true);
            EndRecording(true);
            base.OnDestroy();
        }

        protected override void SimStep(double dt)
        {
            var rec = Recorder;
            if (rec == null)
            {
                Sim.Step(dt);
                // a shot puzzle: the rally freezes at its moment, to the step (Clock stops within the frame)
                // (2026-11-21 the book's puzzles start at their moment, frozen: no rally to watch first)
                return;
            }
            if (_autopilot != null && _autoIntent != null) _autopilot.Fill(_autoIntent, Sim, 0, dt);
            rec.BeforeStep();
            Sim.Step(dt);
            bool was = rec.Finished;
            rec.AfterStep();
            if (!was && rec.Finished)
            {
                var d = DescribeReplay; if (d != null) d(rec.File, this);
                Publish(rec);
            }
        }

        // ---------------- 2026-11-21 the polish pass: big shots and the match won ----------------

        /// <summary>A winner, a nick or a kill died here (world point; kind 0 winner, 1 nick, 2 kill): the views burst.</summary>
        public event System.Action<Vector3, int> BigHit;
        /// <summary>When the match ended (real time; -1: not yet): the celebration's clock.</summary>
        public float MatchOverAt { get; private set; } = -1f;
        public float CelebrationTime { get { return MatchOverAt < 0 ? -1f : Time.unscaledTime - MatchOverAt; } }
        /// <summary>The freeze-frames so far (tests).</summary>
        public int HitStops { get; private set; }
        public const float HitStopSeconds = 0.11f, MatchPointSlowSeconds = 1.1f;

        void Juicy(RallyOutcome o)
        {
            if (Demo || Puzzle != null || o == null || o.Winner < 0) return;
            bool online = OnlineMatch != null;
            if (Sim.Score.MatchOver)
            {
                MatchOverAt = Time.unscaledTime;
                if (!online && UI.Juice.Big) SlowMo(MatchPointSlowSeconds, 0.3);           // the last ball dies in slow motion
                UI.Juice.Rumble(0.8f, 0.6f, 0.6f);
            }
            if (o.Kind != RallyEnd.NotUp) return;
            var touch = Sim.Ball.LastRacketContact;
            int kind = Sim.NickBy == o.Winner ? 1 : Sim.LastStriker == o.Winner && touch != null && touch.Technique == "kill" ? 2 : Sim.LastStriker == o.Winner ? 0 : -1;
            if (kind < 0) return;                                                           // (not up off their own shot: no show)
            BigHit?.Invoke(SimToWorld.Pos(o.Point), kind);
            if (!Sim.Score.MatchOver && !online && UI.Juice.Big) { SlowMo(HitStopSeconds, 0.04); HitStops++; }
            GameplayCameraRig.Kick(kind == 2 ? 0.16f : kind == 1 ? 0.12f : 0.08f);
            bool mine = o.Winner == HumanIndex || Settings.TwoPlayer;
            if (mine) UI.Juice.Rumble(kind == 2 ? 0.7f : 0.45f, kind == 1 ? 0.7f : 0.35f, 0.16f);
        }

        void OnRallyEnded(RallyOutcome o)
        {
            if (Floor != null) FloorRally(o);
            Juicy(o);
            LastCall = null; LastCallStriker = -1;
            if (!UI.CallWords.IsCall(o)) return;
            // The interference state is still the call's (it moves on at the end of this step).
            var it = Sim.Interference;
            LastCallStriker = it.Striker;
            LastCall = Sim.LastCall;
            // P2-M7: strokes are reviewed automatically on the referee camera; any call on V.
            if (o.Kind == RallyEnd.Stroke) { _replayRequested = true; _replayCaption = CallCaption(o); }
        }

        /// <summary>"STROKE: Retriever was in the line to the front wall".</summary>
        public string CallCaption(RallyOutcome o)
        {
            if (!UI.CallWords.IsCall(o)) return o.Reason;
            return UI.CallWords.Decision(o) + ": " + UI.CallWords.Reason(o, Names, LastCallStriker, Settings.TwoPlayer ? -1 : HumanIndex);
        }

        /// <summary>Anything pressed this frame (skips a review).</summary>
        static bool AnyPress()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
            var ms = Mouse.current;
            if (ms != null && (ms.leftButton.wasPressedThisFrame || ms.rightButton.wasPressedThisFrame)) return true;
            foreach (var g in Gamepad.all)
                if (g.buttonSouth.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame || g.buttonNorth.wasPressedThisFrame ||
                    g.buttonWest.wasPressedThisFrame || g.startButton.wasPressedThisFrame || g.selectButton.wasPressedThisFrame) return true;
            return false;
        }

        /// <summary>A local player's pad went away (unplugged, battery): pause until it is back.</summary>
        void WatchDevices()
        {
            if (!Settings.TwoPlayer || Input2 == null) { DeviceLost = null; return; }
            bool lost = false;
            foreach (var src in new[] { Input, Input2 })
            {
                if (src.Devices != null)
                    foreach (var d in src.Devices) if (d is Gamepad && !d.added) lost = true;
                var k = src.Controls != null ? src.Controls.Keys as RawKeys : null;             // 2026-11-23: a keyboard unplugged
                if (k != null && RawInput.Find(k.Device.Path) == null) lost = true;
            }
            if (lost && DeviceLost == null)
            {
                DeviceLost = "A controller or keyboard was disconnected - reconnect it to carry on";
                Clock.Pause();
            }
            else if (DeviceLost != null)
            {
                // back: the same controls again (found by name - a controller plugged back in is a new device), and play on
                var c = Profile.ProfileStore.Current.Choices;
                var prim = InputSetup.Primaries();
                bool chosen = !string.IsNullOrEmpty(c.VsControls1) || !string.IsNullOrEmpty(c.VsControls2);
                if (chosen && (InputSetup.Find(prim, c.VsControls1) == null || InputSetup.Find(prim, c.VsControls2) == null)) return;
                ChosenControls(out PlayerControls p1, out PlayerControls p2);
                if (!p1.Lost && !p2.Lost)
                {
                    Input.UseControls(p1); Input2.UseControls(p2);
                    DeviceLost = null;
                    Clock.Resume();
                }
            }
        }

        protected override void Update()
        {
            if (OnlineMatch != null) { OnlineUpdate(); return; }
            if (FramesActive) { base.Update(); return; }   // P2-M6: the replay theatre plays recorded frames; the demo waits
            if (InSetup) return;          // the panel is up: nothing moves (the views keep the last frame)
            if (Demo)
            {
                // The menus' demo rally: on and on.
                if (Sim.Phase == MatchPhase.MatchOver || Sim.Phase == MatchPhase.GameOver) { BeginDemo(); return; }
                Input.ConsumeReady();
                bool hold = Showcase || EmptyCourt;
                if (hold != Clock.Paused) { if (hold) Clock.Pause(); else Clock.Resume(); }
                base.Update();
                if (EmptyCourt) View = EmptyView(View);
                else if (Showcase) View = ShowcaseView(View);
                return;
            }

            WatchDevices();
            Cutscene();
            PuzzleTick(Time.unscaledDeltaTime);
            FloorTick();
            // A review is skipped with any key (not on the frame it started).
            if (ReplayActive) { if (_reviewFrames++ > 0 && AnyPress()) StopReplay(); }
            else { _reviewFrames = 0; ReviewingCall = false; }
            if (_replayRequested && !ReplayActive)
            {
                _replayRequested = false;
                StartReplay(ReviewSeconds, ReplaySpeed, _replayCaption);
                ReviewingCall = ReplayActive; _reviewFrames = 0;
            }
            AutoReplay();
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool view = (kb != null && kb.vKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame);
            if (view && !ReplayActive && _reviewFrames == 0 && Sim.LastOutcome != null &&
                (Sim.Phase == MatchPhase.RallyOver || Sim.Phase == MatchPhase.AwaitToss))
            {
                StartReplay(ReplaySeconds + 1.5f, ReplaySpeed, CallCaption(Sim.LastOutcome));
                ReviewingCall = ReplayActive && UI.CallWords.IsCall(Sim.LastOutcome); _reviewFrames = 0;
            }

            // 2026-11-22 evening, the video review: F / D-pad down challenges the last call (a command - the recording keeps it);
            // a review plays the moment again, then the verdict
            bool c0 = Input.ConsumeChallenge(), c1 = Input2 != null && Input2.ConsumeChallenge();
            if (!ReplayActive && Sim.ReviewOpen && OnlineMatch == null && Puzzle == null)
            {
                int hi0 = Settings.TwoPlayer ? 0 : HumanIndex;
                if (c0 && Sim.Challenge(hi0) && Recorder != null) Recorder.Command(hi0 == 0 ? ReplayCommand.Challenge0 : ReplayCommand.Challenge1);
                if (c1 && Settings.TwoPlayer && Sim.Challenge(1) && Recorder != null) Recorder.Command(ReplayCommand.Challenge1);
            }
            if (Sim.ReviewBy >= 0 && _reviewShown != Sim.Reviews && !ReplayActive)
            {
                _reviewShown = Sim.Reviews;
                StartReplay(ReplaySeconds + 1.5f, ReplaySpeed, "VIDEO REVIEW");
                ReviewingCall = ReplayActive; _reviewFrames = 0;
            }

            // P5 Focus: a charge of slow motion in a rally (charges each game)
            bool focus = Input.ConsumeFocus();
            int gamesNow = Sim.Score.Games[0] + Sim.Score.Games[1];
            if (gamesNow != _focusGame) { _focusGame = gamesNow; FocusLeft = Settings.FocusCharges; }
            // 2026-11-22 with the Special card, Focus is the sim's (held, the meter draining, only while the ball comes to you): here
            // only its slow motion, while a local player focuses
            int hi = HumanIndex >= 0 && HumanIndex < 2 ? HumanIndex : 0;
            var hm = Sim.Players[hi].Player.Mods;
            if (hm != null && hm.Level("special") > 0)
            {
                focus = false;
                bool held = Sim.Focusing(hi) || (Settings.TwoPlayer && Sim.Focusing(1 - hi));
                if (held && !Clock.Paused) { SlowMo(0.12f, FocusScale); _focusing = true; }
            }
            if (focus && Sim.Phase == MatchPhase.Rally && !SlowMoActive && !Clock.Paused)
            {
                var mine = HumanIndex >= 0 && HumanIndex < 2 ? Sim.Players[HumanIndex].Player.Mods : null;
                if (FocusLeft > 0) { FocusLeft--; SlowMo(FocusSeconds, FocusScale); _focusing = true; }
                else if (Settings.MeterFocus && mine != null && mine.SpendFocus()) { SlowMo(FocusSeconds, FocusScale); _focusing = true; }
            }
            if (!SlowMoActive) _focusing = false;
            // the Special Shot (anyone's ultimate): a moment of slow motion to see it
            if (_ultRead > Sim.Chaos.Count) _ultRead = 0;                    // (a new match: a new list)
            while (_ultRead < Sim.Chaos.Count)
            {
                var n = Sim.Chaos[_ultRead++];
                if (n.Kind == "ult" && !SlowMoActive) { SlowMo(SpecialSlowSeconds, SpecialSlowScale); _focusing = false; }
                if (n.Kind == "special")
                {
                    GameplayCameraRig.Kick(0.22f); UI.Juice.Rumble(0.9f, 0.9f, 0.3f);
                    // 2026-11-22 evening (the user, after Captain Tsubasa): the strike's cut-in - the striker's face, the world all but
                    // stopped (offline only: online, both clocks keep step)
                    if (OnlineMatch == null && !ReplayActive) { SlowMo(CutInSeconds, CutInScale); _focusing = false; GameplayCameraRig.CutIn(n.Player, CutInSeconds); }
                }
                if (n.Kind == "charge" && n.Player == hi) UI.Juice.Rumble(0.4f, 0.4f, 0.25f);
                if (n.Kind == "zone" && n.Player == hi) UI.Juice.Rumble(0.3f, 0.6f, 0.15f);
                if (n.Kind == "windup") UI.Juice.Rumble(0.2f, 0.5f, 0.5f);
            }

            // Ready (Enter / pad Y) from either player. Consumed every frame so a Y pressed
            // as an appeal in a rally never skips the next break.
            bool ready = Input.ConsumeReady();
            if (Input2 != null) ready |= Input2.ConsumeReady();
            if (ready && !ReplayActive)
            {
                if (Sim.Phase == MatchPhase.WarmUp) { if (Recorder != null) Recorder.Command(ReplayCommand.SkipWarmUp); Sim.SkipWarmUp(); SnapViews(); }
                else if (Sim.Phase == MatchPhase.GameOver) { if (Recorder != null) Recorder.Command(ReplayCommand.NextGame); Sim.NextGame(); SnapViews(); }
                // (match over: the post-match screen takes it from here)
            }
            base.Update();
        }

        /// <summary>The same settings again (new opponent seed).</summary>
        public void RestartMatch() { BeginMatch(Settings); }

        // ---------------- playable challenges (2026-10-28): the shot puzzle ----------------

        public enum PuzzleStep { Watching, Choosing, Playing, Over }
        /// <summary>The shot puzzle being played (null: none) and where it is.</summary>
        public SquashSim.Modes.BookPuzzle Puzzle { get; private set; }
        public PuzzleStep PuzzleStage { get; private set; }
        /// <summary>The option chosen (-1: not yet) and how the rally then went for you (1 won, 0 lost, 0.5 neither).</summary>
        public int PuzzleChoice { get; private set; } = -1;
        public double PuzzleResult { get; private set; }
        public const double PuzzleReplaySpeed = 0.6;
        /// <summary>How long your shot is played out before the answer (a rally still going: "it went on").</summary>
        public const double PuzzlePlayoutSeconds = 6;

        /// <summary>2026-11-21 play a shot puzzle (the book's - BookPuzzles): its moment built on the court (you, an AI standing in,
        /// the opponent, the ball on its way to you) and frozen; then it waits for Choose.</summary>
        public void BeginPuzzle(SquashSim.Modes.BookPuzzle p, string court)
        {
            EndOnline(true);
            EndRecording(true);
            var setup = SquashSim.Modes.BookPuzzles.Setup(p);
            Settings = new MatchSettings { Opponent = setup.Opponent, Difficulty = setup.Difficulty, GamesToWin = 1, Scoring = "par", WarmUp = false, Court = court, CrowdFill = 1f };
            if (!string.IsNullOrEmpty(court)) Courts.CurrentId = court;
            Courts.CrowdFill = 1f;
            Input.UseDevices(null);
            if (Input2 != null) { Input2.Dispose(); Input2 = null; }
            ReplayMatch.Configure(setup);
            if (Sim != null) Sim.RallyEnded -= OnRallyEnded;
            _setup = setup;
            Sim = ReplayMatch.Build(setup);
            Sim.Intents[0] = new InputIntent();                          // the stand-in AI plays you: no device input
            Names[0] = "YOU"; Names[1] = AiProfile.ByName(setup.Opponent).Name.ToUpperInvariant();
            OpponentName = Names[1];
            Recorder = null;
            var mo = p.Moment;
            SquashSim.Modes.BookPuzzles.Flight(mo, out V3 bp, out V3 bv, out int bounces);
            Sim.BeginMoment(new V3(mo.MeX, 0, mo.MeZ), new V3(mo.OppX, 0, mo.OppZ), bp, bv, bounces);
            Puzzle = p; PuzzleStage = PuzzleStep.Choosing; PuzzleChoice = -1; PuzzleResult = 0.5;
            Floor = null; FloorOver = false;
            ClearDebugHistory();
            Demo = false; InSetup = false; DeviceLost = null;
            Clock.TimeScale = GameSpeed;
            Clock.Flush();
            Clock.Pause();
            SnapViews();
        }

        /// <summary>Your shot: forced from the frozen moment (the puzzle's trial 0) and played out, slow.</summary>
        public void ChoosePuzzle(int option)
        {
            if (Puzzle == null || PuzzleStage != PuzzleStep.Choosing || option < 0 || option >= Puzzle.Options.Count) return;
            PuzzleChoice = option;
            var c = SquashSim.Modes.BookPuzzles.Choice(Puzzle.Options[option]);
            if (c != null && c.Let) { PuzzleStage = PuzzleStep.Over; return; }        // a let: the rally stops there
            if (Sim.Bots[0] is OpponentBrain me) me.ForceAim = SquashSim.Modes.BookPuzzles.AimFor(c, Puzzle.Moment);
            PuzzleStage = PuzzleStep.Playing;
            _puzzleFrom = Sim.Time;
            Clock.Resume();
            Clock.TimeScale = GameSpeed * PuzzleReplaySpeed;
        }

        /// <summary>2026-10-29 ("in the choosing kit page the character keeps on moving so I couldn't tell what I'm wearing"): the
        /// Locker Room holds the menus' rally - you stand still mid-court in your ready stance, the opponent and the ball out of shot.</summary>
        public static bool Showcase;
        protected override void Awake() { Showcase = false; EmptyCourt = false; Studio = false; StudioPair = false; Backdrop = MenuWorld.None; Dioramas.Reset(); base.Awake(); }
        /// <summary>2026-11-20 the title: the courts empty (the user: "no one playing in the courts") - the rally holds, the
        /// athletes and the ball are put away under the floor.</summary>
        public static bool EmptyCourt;
        /// <summary>2026-11-20 a menu's toon world is up (Dioramas): the menu camera films it, held still. None: the court.</summary>
        public static MenuWorld Backdrop;

        static RenderSnapshot EmptyView(RenderSnapshot v)
        {
            v.A0 = Still(v.A0, new Vector3(0f, -60f, 0f));
            if (v.Count > 1) v.A1 = Still(v.A1, new Vector3(2f, -60f, 0f));
            v.BallPos = new Vector3(0f, -60f, 0f);
            return v;
        }   // (a new scene: no Locker Room open)
        /// <summary>Where you stand for it: sim (x, 0, z) for the cameras, and in the world.</summary>
        public static readonly Vector3 ShowcaseSim = new Vector3(0f, 0f, 5.2f);
        public static readonly Vector3 ShowcaseSpot = SimToWorld.Pos(ShowcaseSim.x, 0.0, ShowcaseSim.z);
        /// <summary>2026-11-20 the Locker Room's photo studio (KitStudio): you stand in it, away from the court; the opponent is put
        /// away.</summary>
        public static bool Studio;
        /// <summary>The match setup's pair in the studio: the opponent stands in it too (else they're put away).</summary>
        public static bool StudioPair;

        /// <summary>Where the opponent stands for it (2026-11-20: in shot for the match setup's picture of them).</summary>
        public static readonly Vector3 ShowcaseThemSim = new Vector3(1.8f, 0f, 8.2f);

        static RenderSnapshot ShowcaseView(RenderSnapshot v)
        {
            if (Studio)
            {
                v.A0 = Still(v.A0, KitStudio.World);
                if (v.Count > 1) v.A1 = Still(v.A1, StudioPair ? KitStudio.ThemWorld : new Vector3(2f, -60f, 0f));
                v.BallPos = new Vector3(0f, -60f, 0f);
                return v;
            }
            v.A0 = Still(v.A0, ShowcaseSpot);
            if (v.Count > 1) v.A1 = Still(v.A1, SimToWorld.Pos(ShowcaseThemSim.x, 0.0, ShowcaseThemSim.z));
            v.BallPos = new Vector3(0f, -20f, 0f);
            return v;
        }

        static AthleteSnapshot Still(AthleteSnapshot a, Vector3 at)
        {
            var shift = at - a.Pos;
            a.Pos = at;
            a.RacketAnchor += shift; a.RacketHead += shift;
            a.Forward = SimToWorld.YawForward(0); a.ShoulderForward = a.Forward;
            a.Velocity = Vector3.zero;
            a.Swinging = false; a.Swing = SwingPhase.Idle; a.SwingT = 0; a.HasPred = false; a.Diving = false; a.GetUp = 0; a.Reach = 0; a.Stamina = 1f;
            return a;
        }

        float _puzzleOverFor;
        double _puzzleFrom;
        void PuzzleTick(float dt)
        {
            if (Puzzle == null || PuzzleStage != PuzzleStep.Playing) return;
            // as the bank measured it: a rally still going after PlayoutSeconds is "the rally went on"
            bool capped = Sim.Phase == MatchPhase.Rally && Sim.Time - _puzzleFrom >= PuzzlePlayoutSeconds;
            if (Sim.Phase == MatchPhase.Rally && !capped) { _puzzleOverFor = 0; return; }
            var o = Sim.LastOutcome;
            if (_puzzleOverFor == 0) PuzzleResult = capped || o == null || o.Kind == RallyEnd.Let || o.Winner < 0 ? 0.5 : o.Winner == 0 ? 1 : 0;
            if (capped) { PuzzleStage = PuzzleStep.Over; Clock.Pause(); return; }
            _puzzleOverFor += dt;
            if (_puzzleOverFor > 1.2f) { PuzzleStage = PuzzleStep.Over; Clock.Pause(); }
        }

        // ---------------- camera work (2026-10-28): the cutscenes ----------------

        object _cutFor;

        /// <summary>A new match: the walk-on (the prestige fly-around first, for a final) - Settings > Camera > Cutscenes.
        /// Never online (the match there can't wait), nor when the match runs sped up.</summary>
        void Cutscene()
        {
            if (_cutFor == Sim) return;
            if (Puzzle != null) { _cutFor = Sim; return; }
            _cutFor = Sim;
            if (!Profile.ProfileStore.Current.Settings.Cutscenes || OnlineMatch != null || Sim.History.Count > 0 || Clock.TimeScale > 1.01 || VenueView.Detail <= 0) return;
            bool orbit = !string.IsNullOrEmpty(Settings.Prestige);
            var frames = Replays.Cutscenes.Build(View, orbit, out float share);
            var court = Courts.Find(Settings.Court ?? Courts.CurrentId);
            string where = court != null ? court.Name + (string.IsNullOrEmpty(court.Blurb) || court.Blurb.Length > 24 ? "" : "  ·  " + court.Blurb.ToUpperInvariant()) : "";
            PlayCinematic(frames, Replays.Cutscenes.Intro, share, orbit ? Settings.Prestige : null, where);
            _reviewFrames = 0;
        }

        // ---------------- camera work (2026-10-28): replays between rallies ----------------

        int _autoFor = -1;
        MatchPhase _phaseWas;
        /// <summary>The caption of the big moment being replayed ("WHAT A RALLY - 18 SHOTS"); null when none.</summary>
        public string MomentCaption { get; private set; }

        /// <summary>A rally just ended on a big moment (or every point, by the setting): replay it, cut like television.
        /// Never online (the match there can't wait), nor when the match runs sped up (tests, fast-forward).</summary>
        void AutoReplay()
        {
            if (Sim.Phase == MatchPhase.Rally && _phaseWas != MatchPhase.Rally) RallyNick = false;
            _phaseWas = Sim.Phase;
            if (Sim.Phase != MatchPhase.RallyOver || Sim.History.Count == _autoFor || ReplayActive || _replayRequested || Puzzle != null) return;
            _autoFor = Sim.History.Count;
            var o = Sim.LastOutcome;
            int mode = Profile.ProfileStore.Current.Settings.Replays;
            if (o == null || o.Kind == RallyEnd.Let || o.Winner < 0 || mode <= 0 || OnlineMatch != null || Clock.TimeScale > 1.01) return;
            string moment = Moment(o);
            if (moment == null && mode < 2) return;
            StartReplay(ReplaySeconds + 1.5f, ReplaySpeed, moment ?? "");
            ReplayBig = moment != null;
            MomentCaption = moment ?? "REPLAY";
            ReviewingCall = false; _reviewFrames = 0;
        }

        /// <summary>What made the rally just ended a big moment (null: an ordinary point).</summary>
        string Moment(RallyOutcome o)
        {
            int shots = Sim.ShotsPerRally.Count > 0 ? Sim.ShotsPerRally[Sim.ShotsPerRally.Count - 1] : 0;
            bool winner = o.Kind == RallyEnd.NotUp;
            for (int i = Sim.Chaos.Count - 1; i >= 0 && Sim.Chaos[i].Time >= Sim.Time - 8; i--)
                if (Sim.Chaos[i].Kind == "bonus" && Sim.Chaos[i].Value >= 2) return (1 + Sim.Chaos[i].Value) + "-POINT WINNER";
            if (Sim.Score.MatchOver) return "MATCH";
            if (Sim.Score.GameOver) return "GAME";
            if (winner && RallyNick) return "IN THE NICK";
            if (winner && LastShotDived) return "FROM A DIVE";
            if (winner && shots >= 12) return "WHAT A RALLY  ·  " + shots + " SHOTS";
            return null;
        }

        /// <summary>P5 Focus: charges left this game; how long and how slow a charge is.</summary>
        public int FocusLeft { get; private set; }
        /// <summary>The slow motion now is a Focus (the HUD dims the screen for it).</summary>
        public bool Focusing { get { return _focusing && SlowMoActive; } }
        bool _focusing;
        int _ultRead;
        public const float SpecialSlowSeconds = 0.7f;
        public const double SpecialSlowScale = 0.3;
        int _focusGame = -1;
        public const float FocusSeconds = 1.6f;
        /// <summary>The Special Shot's cut-in: real seconds, and the clock's scale meanwhile (all but stopped).</summary>
        public const float CutInSeconds = 0.95f;
        public const double CutInScale = 0.03;
        public const double FocusScale = 0.35;

        // ---------------- P2-M8 online ----------------

        /// <summary>The online match playing (null: not online). This machine plays athlete OnlineMatch.Me.</summary>
        public LockstepSession OnlineMatch { get; private set; }
        public override int HumanIndex { get { return OnlineMatch != null ? OnlineMatch.Me : 0; } }
        public override bool IsServing(int athlete) { return Sim != null && Sim.Referee.InServe && Sim.Referee.Server == athlete; }
        readonly InputIntent _still = new InputIntent();
        double _sinceTick;
        bool _onlinePublished;
        ITransport _link;
        /// <summary>Ticks in a row ready to run that count as catching up (no sounds for them).</summary>
        public const int CatchUpTicks = 12;

        /// <summary>Play the match the server started, over this link.</summary>
        public void BeginOnline(MatchStartInfo info, ITransport link)
        {
            EndOnline(false);
            EndRecording(true);
            Settings = new MatchSettings
            {
                Online = true, Ranked = info.Ranked, MatchId = info.MatchId, GamesToWin = info.Setup.GamesToWin, Scoring = info.Setup.Scoring,
                Court = Courts.Find(info.Court) != null ? info.Court : "show", P2Name = info.OppName, OppKit = info.OppKit,
            };
            Courts.CurrentId = Settings.Court;
            Courts.CrowdFill = 1f;
            Input.UseDevices(null);
            if (Input2 != null) { Input2.Dispose(); Input2 = null; }
            if (Sim != null) Sim.RallyEnded -= OnRallyEnded;
            var s = new LockstepSession(info, link, Application.version, System.DateTime.Now.Ticks);
            s.OnChat = p => Said(false, p);                                    // batch 4
            ChatText = null; ChatAt = -99f;
            _link = link;
            OnlineMatch = s;
            OnlineService.Session = s;
            _setup = info.Setup;
            Sim = s.Sim;
            Sim.RallyEnded += OnRallyEnded;
            Floor = null; FloorOver = false;
            s.Core.Stepped += OnOnlineStep;
            OpponentName = string.IsNullOrEmpty(info.OppName) ? "OPPONENT" : info.OppName.ToUpperInvariant();
            Names[s.Me] = "YOU"; Names[s.Them] = OpponentName;
            ClearDebugHistory();
            Demo = false; InSetup = false; DeviceLost = null;
            _onlinePublished = false; _sinceTick = 0; _replayRequested = false;
            Clock.Resume();
            Clock.TimeScale = GameSpeed;
            Clock.Flush();
            SnapViews();
        }

        void OnOnlineStep(int i) { AfterSimStep(i == 0); _sinceTick = 0; }

        // ---------------- batch 4: quick chat (online) ----------------

        float _chatSent = -9f;
        /// <summary>The last thing said in this match - by you or them - and when (the HUD shows it a few seconds).</summary>
        public string ChatText { get; private set; }
        public bool ChatMine { get; private set; }
        public float ChatAt { get; private set; } = -99f;

        void Said(bool mine, int phrase)
        {
            ChatText = SquashSim.Net.QuickChat.Of(phrase);
            ChatMine = mine;
            ChatAt = Time.unscaledTime;
            if (!mine) UI.Sounds.Play("tick");
        }

        static int QuickChatKey()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) return 0;
                if (kb.digit2Key.wasPressedThisFrame) return 1;
                if (kb.digit3Key.wasPressedThisFrame) return 2;
                if (kb.digit4Key.wasPressedThisFrame) return 3;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.up.wasPressedThisFrame) return 0;
                if (pad.dpad.right.wasPressedThisFrame) return 1;
                if (pad.dpad.down.wasPressedThisFrame) return 2;
                if (pad.dpad.left.wasPressedThisFrame) return 3;
            }
            return -1;
        }

        /// <summary>Done with the online match: a forfeit first if it is still on (<paramref name="leave"/>); the recording is handed on.</summary>
        void EndOnline(bool leave)
        {
            var s = OnlineMatch;
            if (s == null) return;
            if (leave && !s.Finished) OnlineService.Leave();
            PublishOnline();
            s.Core.Stepped -= OnOnlineStep;
            OnlineMatch = null;
            if (OnlineService.Session == s) OnlineService.Session = null;
            CatchingUp = false;
            Clock.Resume();
        }

        void PublishOnline()
        {
            var s = OnlineMatch;
            if (s == null || _onlinePublished) return;
            _onlinePublished = true;
            if (!s.Finished) s.Core.Close(false);                       // left part-way: retired
            if (s.File.Rallies.Count == 0) return;
            var d = DescribeReplay; if (d != null) d(s.File, this);
            var h = ReplayFinished; if (h != null) h(s.File);
        }

        /// <summary>The online frame: input in, the link read, this client's input out for each tick due, every tick
        /// both inputs are in for stepped, the view drawn between the last two ticks.</summary>
        void OnlineUpdate()
        {
            var s = OnlineMatch;
            float rawDt = Time.unscaledDeltaTime;
            Input.Sample(rawDt);
            if (Input.ConsumePause() && !UI.FrontEnd.BlocksGamePause) Clock.TogglePause();   // the menu only: the match plays on
            OnlineService.Pump();
            if (OnlineMatch != s) return;                                     // the link brought the match again (back after a drop)
            if (_link != null && _link != OnlineService.Transport)            // a link of its own (tests): read here
                while (_link.TryReceive(out var msg)) s.Handle(msg);
            bool ready = Input.ConsumeReady();
            if (ready && !Clock.Paused && Sim.Phase == MatchPhase.GameOver) s.Command(ReplayCommand.NextGame);
            if (ready && !Clock.Paused && Sim.Phase == MatchPhase.WarmUp) s.Command(ReplayCommand.SkipWarmUp);   // batch 4: either player skips
            // batch 4: quick chat - 1..4 or the pad's d-pad
            int say = QuickChatKey();
            if (say >= 0 && !Clock.Paused && Time.unscaledTime - _chatSent > 1.5f) { _chatSent = Time.unscaledTime; s.Chat(say); Said(true, say); }
            float dt = rawDt * (float)(Clock.TimeScale / GameSpeed);           // tests run it faster (SetTimeScale); play: 1
            _sinceTick += dt;
            if (!s.Finished)
            {
                CatchingUp = s.Core.ReadyTicks(CatchUpTicks) >= CatchUpTicks;
                InputIntent live = Input.Intent;
                if (Clock.Paused) { live.ClearEdges(); live = _still; }        // the menu is up: stand still
                System.Action<InputIntent> fill = null;
                if (_autopilot != null && _autoIntent != null) { live = _autoIntent; fill = x => _autopilot.Fill(x, Sim, s.Me, LockstepSession.TickSeconds); }
                s.Pump(dt, live, IntentSource.Assisted, ShotLines, fill);
                CatchingUp = false;
            }
            if (s.Finished && !_onlinePublished) PublishOnline();
            ShowBetween((float)(_sinceTick / LockstepSession.TickRealSeconds));
        }

        /// <summary>Leave the online match for the menus (a forfeit if it is still on).</summary>
        public void LeaveOnline() { EndOnline(true); }
    }
}
