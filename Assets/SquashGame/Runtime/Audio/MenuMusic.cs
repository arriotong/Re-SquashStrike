// ===========================================================
// Assets/SquashGame/Runtime/Audio/MenuMusic.cs
// The menus' music: plays on the title and the menus (over the demo
// rally), quieter in the replay screens, fades out when a match or
// practice starts. Settings > Audio > Music.
//
// Batch 6 (2026-10-26): songs (MusicLibrary), by mood - the front end
// says which (the main menu, the career, the Gauntlet, the Locker and
// online). A new mood cross-fades to one of its songs; a song that ends
// gives way to another of the mood's (shuffled, never the same twice
// running). Each new song is announced (OnSong: "Now playing").
// No songs at all: the track made at start-up (MenuMusicTrack), looped.
// ===========================================================

using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SquashGame.Audio
{
    public sealed class MenuMusic : MonoBehaviour
    {
        /// <summary>Settings > Audio > Music.</summary>
        public static float Volume = 0.6f;
        /// <summary>0 silent, 1 full - what the menus want now (FrontEnd sets it every frame).</summary>
        public float Want;
        /// <summary>Which songs the screen on show wants (FrontEnd sets it every frame).</summary>
        public MusicMood Mood = MusicMood.Menu;
        /// <summary>A song starts (title, artist): the front end shows it.</summary>
        public System.Action<Song> OnSong;
        /// <summary>The song playing (null: the made-up track).</summary>
        public Song Current { get; private set; }

        const float Crossfade = 1.4f;
        AudioSource _a, _b;                    // _a: the song now; _b: the one fading out
        float _fadeB;
        Task<float[]> _render;
        AudioClip _madeUp;
        float _level;
        MusicMood _playingMood;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly HashSet<string> _missing = new HashSet<string>();
        readonly System.Random _rng = new System.Random();
        bool _paused;

        /// <summary>The track is ready and playing (tests).</summary>
        public bool Playing { get { return _a != null && _a.isPlaying && _a.volume > 0.001f; } }
        public float Level { get { return _level; } }

        AudioSource Source()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = false; s.playOnAwake = false; s.spatialBlend = 0f; s.volume = 0f;
            return s;
        }

        void Awake()
        {
            _a = Source(); _b = Source();
            if (!AnySongs()) _render = Task.Run(() => MenuMusicTrack.Render());
        }

        /// <summary>Is this song in the build (Resources/Music)?</summary>
        public bool Has(Song s) { return Clip(s) != null; }

        AudioClip Clip(Song s)
        {
            if (s == null || _missing.Contains(s.File)) return null;
            if (_clips.TryGetValue(s.File, out var c)) return c;
            c = Resources.Load<AudioClip>(MusicLibrary.Folder + s.File);
            if (c == null) { _missing.Add(s.File); return null; }
            _clips[s.File] = c;
            return c;
        }

        bool AnySongs() { foreach (var s in MusicLibrary.All) if (Has(s)) return true; return false; }

        /// <summary>A song for this mood (another mood's if it has none of its own), not the one just played.</summary>
        Song Pick(MusicMood mood)
        {
            var pool = MusicLibrary.Of(mood).FindAll(Has);
            if (pool.Count == 0) pool = new List<Song>(System.Array.FindAll(MusicLibrary.All, Has));
            if (pool.Count == 0) return null;
            if (pool.Count > 1 && Current != null) pool.Remove(Current);
            return pool[_rng.Next(pool.Count)];
        }

        void Begin(Song s)
        {
            // the old one to the fading source, the new one in
            var t = _b; _b = _a; _a = t;
            _fadeB = _b.isPlaying ? 1f : 0f;
            Current = s;
            _a.clip = Clip(s); _a.loop = false; _a.volume = 0f; _a.time = 0f;
            _a.Play();
            _playingMood = Mood;
            OnSong?.Invoke(s);
        }

        void Update()
        {
            _level = Mathf.MoveTowards(_level, Mathf.Clamp01(Want), Time.unscaledDeltaTime / (Want > _level ? 1.2f : 0.8f));
            float gain = _level * Volume * 0.8f;

            // no songs: the made-up track, looped (as before)
            if (_render != null || _madeUp != null)
            {
                if (_madeUp == null && _render.IsCompleted)
                {
                    if (_render.Status == TaskStatus.RanToCompletion)
                    {
                        float[] smp = _render.Result;
                        _madeUp = AudioClip.Create("menu_music", smp.Length / 2, 2, MenuMusicTrack.Rate, false);
                        _madeUp.SetData(smp, 0);
                        _a.clip = _madeUp; _a.loop = true;
                    }
                    else Debug.LogWarning("[Music] the menu track could not be made: " + _render.Exception);
                    _render = null;
                }
                if (_a.clip == null) return;
                _a.volume = gain;
                if (_level > 0.001f && !_a.isPlaying) _a.Play();
                else if (_level <= 0.001f && _a.isPlaying) _a.Pause();
                return;
            }

            if (_level <= 0.001f)
            {
                if (!_paused) { _a.Pause(); _b.Stop(); _paused = true; }
                return;
            }
            if (_paused) { _paused = false; if (_a.clip != null) _a.UnPause(); }
            // a new mood, nothing yet, or the song over: the next
            bool over = _a.clip != null && !_a.isPlaying && !_paused;
            if (_a.clip == null || over || (Mood != _playingMood && (Current == null || Current.Mood != Mood)))
            {
                var next = Pick(Mood);
                if (next != null) Begin(next); else _playingMood = Mood;
            }
            else if (Mood != _playingMood) _playingMood = Mood;              // (the song on already suits it)

            _fadeB = Mathf.MoveTowards(_fadeB, 0f, Time.unscaledDeltaTime / Crossfade);
            float inA = Mathf.Clamp01(1f - _fadeB);
            _a.volume = gain * inA;
            _b.volume = gain * _fadeB;
            if (_fadeB <= 0f && _b.isPlaying) _b.Stop();
        }
    }
}
