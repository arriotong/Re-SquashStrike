// ===========================================================
// Assets/SquashGame/Runtime/Audio/CrowdAudio.cs
// PR4: the crowd in the venue. Like a real squash crowd it hushes while
// the ball is in play and talks between rallies; it applauds every rally
// (longer rallies, bigger hands), goes "ooh" at a tin or a nick in a
// rally worth watching, and cheers a game. Only with a venue to sit in
// (VenueView.Detail > 0), never over the menus' demo or a replay.
// Settings > Audio > Crowd. Presentation only.
// ===========================================================

using System.Collections.Generic;
using SquashSim;
using UnityEngine;

namespace SquashGame.Audio
{
    public sealed class CrowdAudio : MonoBehaviour
    {
        /// <summary>Settings > Audio > Crowd.</summary>
        public static float Volume = 0.8f;
        /// <summary>The practice court is quieter: a few people watching.</summary>
        public float Presence = 1f;

        public SimRunnerBase Runner;
        public readonly Dictionary<string, int> Played = new Dictionary<string, int>();

        static Dictionary<string, AudioClip[]> _clips;
        AudioSource _bed;
        readonly List<AudioSource> _voices = new List<AudioSource>();
        int _next, _lastRallies = -1;
        MatchPhase _lastPhase;
        float _bedLevel, _oohCooldown;

        public void Build(SimRunnerBase runner, float presence = 1f)
        {
            Runner = runner;
            Presence = presence;
            if (_clips == null)
            {
                _clips = new Dictionary<string, AudioClip[]>();
                foreach (var n in CrowdSounds.Names)
                {
                    var one = Resources.Load<AudioClip>("Audio/" + n);          // a recording wins
                    if (one != null) { _clips[n] = new[] { one }; continue; }
                    var takes = new AudioClip[CrowdSounds.Takes(n)];
                    for (int k = 0; k < takes.Length; k++) takes[k] = CrowdSounds.Clip(n, k);
                    _clips[n] = takes;
                }
            }
            _bed = NewSource("CrowdBed");
            _bed.clip = _clips["crowdBed"][0];
            _bed.loop = true;
            _bed.volume = 0f;
            _bed.Play();
            for (int i = 0; i < 4; i++) _voices.Add(NewSource("CrowdVoice" + i));
            runner.Stepped += OnStepped;
        }

        AudioSource NewSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;                 // all around you
            s.dopplerLevel = 0f;
            return s;
        }

        void OnDestroy() { if (Runner != null) Runner.Stepped -= OnStepped; }

        bool Live
        {
            get
            {
                if (VenueView.Detail <= 0 || Runner == null || Runner.ReplayActive || Runner.FramesActive) return false;
                var mr = Runner as MatchRunner;
                return mr == null || (!mr.Demo && !mr.InSetup);
            }
        }

        public void Play(string name, float volume)
        {
            if (!_clips.TryGetValue(name, out var takes) || volume <= 0.01f) return;
            var src = _voices[_next];
            _next = (_next + 1) % _voices.Count;
            src.clip = takes[Random.Range(0, takes.Length)];
            src.volume = Mathf.Clamp01(volume) * Volume * Presence;
            src.pitch = 1f + Random.Range(-0.03f, 0.03f);
            src.Play();
            Played[name] = Played.TryGetValue(name, out int n) ? n + 1 : 1;
        }

        void OnStepped(IReadOnlyList<SimEvent> events)
        {
            if (!Live) return;
            var mr = Runner as MatchRunner;
            int shots = mr != null && mr.Sim != null ? mr.Sim.Referee.Shots : 0;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Type != SimEventType.Impact || e.Resting || _oohCooldown > 0) continue;
                // A tin or a nick, in a rally that had them watching.
                if ((e.SurfaceClass == "tin" && shots >= 4) || (e.Nick && shots >= 2))
                {
                    Play("ooh", 0.45f + Mathf.Clamp01(shots / 16f) * 0.45f);
                    _oohCooldown = 2f;
                }
            }
        }

        void Update()
        {
            _oohCooldown -= Time.deltaTime;
            var mr = Runner as MatchRunner;
            bool live = Live;
            bool rally = false;
            if (mr != null && mr.Sim != null)
            {
                MatchSimulation m = mr.Sim;
                rally = m.Phase == MatchPhase.Rally;
                if (_lastRallies < 0 || m.History.Count < _lastRallies) _lastRallies = m.History.Count;
                if (m.History.Count > _lastRallies)
                {
                    _lastRallies = m.History.Count;
                    int shots = m.ShotsPerRally.Count > 0 ? m.ShotsPerRally[m.ShotsPerRally.Count - 1] : 0;
                    bool gameEnd = m.Score.GameOver || m.Score.MatchOver;
                    if (live && !gameEnd && m.LastOutcome != null && m.LastOutcome.Kind != RallyEnd.Let)
                        Play("applause", 0.25f + Mathf.Clamp01(shots / 18f) * 0.7f);       // longer rally, bigger hands
                }
                if (live && (m.Phase == MatchPhase.GameOver || m.Phase == MatchPhase.MatchOver) && _lastPhase != m.Phase)
                    Play("cheer", m.Phase == MatchPhase.MatchOver ? 1f : 0.8f);
                _lastPhase = m.Phase;
            }
            // The bed: hushed in a rally, talking between them.
            float want = !live ? 0f : rally ? 0.07f : 0.30f;
            float k = 1f - Mathf.Exp(-(rally ? 6f : 1.2f) * Time.unscaledDeltaTime);  // hush fast, come back slowly
            _bedLevel += (want - _bedLevel) * k;
            if (_bed != null) _bed.volume = _bedLevel * Volume * Presence;
        }

        /// <summary>The bed's current level, 0..1 before the volume setting (tests).</summary>
        public float BedLevel { get { return _bedLevel; } }
    }
}
