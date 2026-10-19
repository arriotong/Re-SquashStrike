// ===========================================================
// Assets/SquashGame/Runtime/Audio/MenuMusic.cs
// The menus' music: plays on the title and the menus (over the demo
// rally), quieter in the replay screens, fades out when a match or
// practice starts. The track is rendered off the main thread at start-up
// (MenuMusicTrack); a recording in Resources/Audio/menu_music (wav / ogg)
// is used instead if there is one. Settings > Audio > Music.
// ===========================================================

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

        AudioSource _src;
        Task<float[]> _render;
        float _level;
        /// <summary>The track is ready and playing (tests).</summary>
        public bool Playing { get { return _src != null && _src.isPlaying && _src.volume > 0.001f; } }
        public float Level { get { return _level; } }

        void Awake()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.loop = true; _src.playOnAwake = false; _src.spatialBlend = 0f; _src.volume = 0f;
            var rec = Resources.Load<AudioClip>("Audio/menu_music");
            if (rec != null) _src.clip = rec;
            else _render = Task.Run(() => MenuMusicTrack.Render());
        }

        void Update()
        {
            if (_src.clip == null && _render != null && _render.IsCompleted)
            {
                if (_render.Status == TaskStatus.RanToCompletion)
                {
                    float[] s = _render.Result;
                    var clip = AudioClip.Create("menu_music", s.Length / 2, 2, MenuMusicTrack.Rate, false);
                    clip.SetData(s, 0);
                    _src.clip = clip;
                }
                else Debug.LogWarning("[Music] the menu track could not be made: " + _render.Exception);
                _render = null;
            }
            if (_src.clip == null) return;
            _level = Mathf.MoveTowards(_level, Mathf.Clamp01(Want), Time.unscaledDeltaTime / (Want > _level ? 1.2f : 0.8f));
            _src.volume = _level * Volume * 0.8f;
            if (_level > 0.001f && !_src.isPlaying) _src.Play();
            else if (_level <= 0.001f && _src.isPlaying) _src.Pause();
        }
    }
}
