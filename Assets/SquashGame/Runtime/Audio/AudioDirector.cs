// ===========================================================
// Assets/SquashGame/Runtime/Audio/AudioDirector.cs
// The M5 audio pass. Listens to the simulation's event stream
// (SimRunnerBase.Stepped) and plays the procedural sound set from a
// pool of 3D sources at the event's court position:
//
//   impacts        by surface (front / side / glass / tin / floor / nick),
//                  loudness from the impact's normal speed
//   racket         strings on the sweet spot, a frame clack off-centre,
//                  loudness from the head speed
//   bodies         ball-hits-player, player-vs-player
//   shoes          a squeak when an athlete stops hard (from the drawn
//                  velocity, so it follows what you see)
//   referee        a chime on every call, a longer one at the end of a game
//
// Presentation only: it never writes simulation state, and random pitch
// variation uses Unity's RNG, never the sim's.
// ===========================================================

using System.Collections.Generic;
using SquashSim;
using UnityEngine;

namespace SquashGame.Audio
{
    public sealed class AudioDirector : MonoBehaviour
    {
        public SimRunnerBase Runner;
        [Range(0, 1)] public float MasterVolume = 0.9f;
        /// <summary>Settings: the referee's calls and every other sound (the master volume is the AudioListener's).</summary>
        public static float SfxVolume = 1f, RefereeVolume = 1f;
        public int Voices = 14;

        /// <summary>Sounds started so far, by name (tests and diagnostics).</summary>
        public readonly Dictionary<string, int> Played = new Dictionary<string, int>();
        public int PlayedTotal { get; private set; }
        public string LastSound { get; private set; }

        Dictionary<string, AudioClip> _clips;
        readonly List<AudioSource> _pool = new List<AudioSource>();
        int _next;
        readonly Vector3[] _lastVel = new Vector3[2];
        readonly float[] _squeakCooldown = new float[2];
        int _lastRallies = -1;
        MatchPhase _lastPhase;

        public void Build(SimRunnerBase runner)
        {
            Runner = runner;
            _clips = ProceduralSounds.AllClips();
            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0.8f;                 // mostly positional, a little "in the room"
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 3f;
                src.maxDistance = 40f;
                src.dopplerLevel = 0f;
                _pool.Add(src);
            }
            if (FindFirstObjectByType<AudioListener>() == null)
            {
                var cam = Camera.main;
                (cam != null ? cam.gameObject : gameObject).AddComponent<AudioListener>();
            }
            Runner.Stepped += OnStepped;
        }

        void OnDestroy()
        {
            if (Runner != null) Runner.Stepped -= OnStepped;
        }

        /// <summary>Play a sound at a world point. Volume 0..1; pitch around 1.</summary>
        public void Play(string name, Vector3 at, float volume, float pitch = 1f)
        {
            if (_clips == null || volume <= 0.01f || !_clips.TryGetValue(name, out AudioClip clip)) return;
            // Steal the next voice in turn (the oldest sound is the least missed).
            AudioSource src = _pool[_next];
            _next = (_next + 1) % _pool.Count;
            src.transform.position = at;
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume) * MasterVolume * (name == "call" ? RefereeVolume : SfxVolume);
            src.pitch = pitch;
            src.Play();
            Played[name] = Played.TryGetValue(name, out int n) ? n + 1 : 1;
            PlayedTotal++;
            LastSound = name;
        }

        static float Vary(float amount) { return 1f + Random.Range(-amount, amount); }

        void OnStepped(IReadOnlyList<SimEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                Vector3 at = SimToWorld.Pos(e.Point);
                switch (e.Type)
                {
                    case SimEventType.Impact:
                    {
                        if (e.Resting) break;
                        float v = Mathf.Clamp01((float)e.NormalSpeed / 18f);
                        v = Mathf.Sqrt(v);                                  // perceived loudness
                        string s;
                        if (e.Nick) s = "nick";
                        else switch (e.SurfaceClass)
                        {
                            case "frontWall": s = "front"; break;
                            case "sideWall": s = "side"; break;
                            case "backWall": s = "glass"; break;
                            case "tin": s = "tin"; v = Mathf.Max(v, 0.6f); break;
                            case "floor": s = "floor"; v *= 0.8f; break;
                            default: s = null; break;                       // ceiling / out of court: the call says it
                        }
                        if (s != null) Play(s, at, v, Vary(0.04f));
                        break;
                    }
                    case SimEventType.RacketContact:
                    {
                        float v = Mathf.Clamp01(0.25f + (float)e.HeadSpeed / 28f);
                        bool frame = e.OffCentreNorm > 0.65;
                        Play(frame ? "frame" : "ping", at, v, Vary(0.05f) * (e.Stroke == "touch" ? 1.06f : 1f));
                        break;
                    }
                    case SimEventType.BodyHit:
                        Play("body", at, 0.8f, Vary(0.05f));
                        break;
                    case SimEventType.PlayerContact:
                        Play("body", at, 0.45f, 0.8f * Vary(0.05f));
                        break;
                }
            }
        }

        void Update()
        {
            if (Runner == null || _clips == null) return;
            var mr = Runner as MatchRunner;
            if (mr != null && mr.InSetup) return;
            float dt = Mathf.Max(1e-4f, Time.deltaTime);

            // ---- shoes: a hard stop squeaks ----
            int count = Runner.View.Count;
            for (int i = 0; i < count && i < 2; i++)
            {
                AthleteSnapshot a = Runner.View.Athlete(i);
                Vector3 v = a.Velocity; v.y = 0;
                Vector3 pv = _lastVel[i];
                _lastVel[i] = v;
                _squeakCooldown[i] -= dt;
                float decel = (pv.magnitude - v.magnitude) / dt;
                float turn = Vector3.Angle(pv, v);
                bool hardStop = pv.magnitude > 2.2f && decel > 9f;
                bool hardTurn = pv.magnitude > 2.5f && v.magnitude > 1.5f && turn > 50f;
                if ((hardStop || hardTurn) && _squeakCooldown[i] <= 0 && !Runner.ReplayActive)
                {
                    _squeakCooldown[i] = 0.45f;
                    Play("squeak", a.Pos, Mathf.Clamp01(0.35f + decel / 40f), Vary(0.08f));
                }
            }

            // ---- referee ----
            if (mr != null)
            {
                MatchSimulation m = mr.Sim;
                if (_lastRallies < 0 || m.History.Count < _lastRallies) _lastRallies = m.History.Count;
                Vector3 refPos = SimToWorld.Pos(0, 3.0, CourtDimensions.LENGTH + 1.5);
                if (m.History.Count > _lastRallies)
                {
                    _lastRallies = m.History.Count;
                    if (!(m.Score.GameOver || m.Score.MatchOver)) Play("call", refPos, 0.6f);
                }
                if ((m.Phase == MatchPhase.GameOver || m.Phase == MatchPhase.MatchOver) && _lastPhase != m.Phase)
                    Play("gameCall", refPos, 0.7f);
                _lastPhase = m.Phase;
            }
        }
    }
}
