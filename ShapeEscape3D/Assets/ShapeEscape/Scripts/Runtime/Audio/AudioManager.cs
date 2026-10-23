using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ShapeEscape
{
    public enum Sfx
    {
        Tap,
        Remove,
        Blocked,
        Locked,
        Unlock,
        Rotate,
        Chain,
        Complete,
        Coin,
        UiClick,
        Hint,
        Reveal,
        Whoosh
    }

    /// <summary>
    /// Sound effects (pooled AudioSources, gentle pitch variation, rising pentatonic pitch for removal combos)
    /// and looping background music with fade in/out. Volumes and on/off switches come from the save data.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        const int PoolSize = 8;
        public const string OverridePath = "ShapeEscape/Audio/";

        // Semitone offsets: removing pieces quickly climbs a major pentatonic scale.
        static readonly int[] ComboSteps = { 0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24 };

        readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        readonly List<AudioSource> _pool = new List<AudioSource>();
        readonly Dictionary<Sfx, float> _lastPlayed = new Dictionary<Sfx, float>();
        AudioSource _music;
        GameManager _game;
        int _next;
        float _musicTargetVolume;

        public void Initialize(GameManager game)
        {
            _game = game;
            for (int i = 0; i < PoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _pool.Add(src);
            }
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _music.volume = 0f;
            _music.priority = 0;

            Register(Sfx.Tap, ProceduralAudio.Tap);
            Register(Sfx.Remove, ProceduralAudio.Remove);
            Register(Sfx.Blocked, ProceduralAudio.Blocked);
            Register(Sfx.Locked, ProceduralAudio.Locked);
            Register(Sfx.Unlock, ProceduralAudio.Unlock);
            Register(Sfx.Rotate, ProceduralAudio.Rotate);
            Register(Sfx.Chain, ProceduralAudio.Chain);
            Register(Sfx.Complete, ProceduralAudio.Complete);
            Register(Sfx.Coin, ProceduralAudio.Coin);
            Register(Sfx.UiClick, ProceduralAudio.UiClick);
            Register(Sfx.Hint, ProceduralAudio.Hint);
            Register(Sfx.Reveal, ProceduralAudio.Reveal);
            Register(Sfx.Whoosh, ProceduralAudio.Whoosh);

            StartCoroutine(LoadMusic());
        }

        void Register(Sfx id, System.Func<AudioClip> synth)
        {
            var custom = Resources.Load<AudioClip>(OverridePath + id);
            _clips[id] = custom != null ? custom : synth();
        }

        IEnumerator LoadMusic()
        {
            var custom = Resources.Load<AudioClip>(OverridePath + "Music");
            if (custom != null)
            {
                _music.clip = custom;
            }
            else
            {
                var task = Task.Run(() => ProceduralAudio.ComputeMusic(20261023));
                while (!task.IsCompleted) yield return null;
                if (task.IsFaulted) yield break;
                _music.clip = ProceduralAudio.MusicClip(task.Result);
            }
            ApplySettings();
        }

        SaveData.SettingsData Settings { get { return _game.Save.Data.settings; } }

        /// <summary>Re-reads volume / toggle settings (call after the settings screen changes them).</summary>
        public void ApplySettings()
        {
            var s = Settings;
            _musicTargetVolume = s.musicOn ? 0.45f * s.musicVolume : 0f;
            if (_music.clip != null && _musicTargetVolume > 0f && !_music.isPlaying) _music.Play();
        }

        void Update()
        {
            if (_music == null) return;
            _music.volume = Mathf.MoveTowards(_music.volume, _musicTargetVolume, Time.unscaledDeltaTime * 0.35f);
            if (_music.isPlaying && _music.volume <= 0.001f && _musicTargetVolume <= 0f) _music.Pause();
            else if (!_music.isPlaying && _musicTargetVolume > 0f && _music.clip != null) _music.Play();
        }

        /// <summary>Ducks the music (e.g. while a rewarded ad plays).</summary>
        public void SetMusicDucked(bool ducked)
        {
            var s = Settings;
            _musicTargetVolume = ducked || !s.musicOn ? 0f : 0.45f * s.musicVolume;
        }

        public void Play(Sfx id, float delay = 0f, float pitch = 1f, float volume = 1f)
        {
            if (_game == null || !Settings.sfxOn) return;
            AudioClip clip;
            if (!_clips.TryGetValue(id, out clip) || clip == null) return;

            // Avoid machine-gun stacking of the exact same sound in one frame.
            float last;
            if (_lastPlayed.TryGetValue(id, out last) && Time.unscaledTime - last < 0.025f && delay <= 0f) return;
            _lastPlayed[id] = Time.unscaledTime;

            var src = _pool[_next];
            _next = (_next + 1) % _pool.Count;
            src.clip = clip;
            src.pitch = pitch * Random.Range(0.97f, 1.03f);
            src.volume = Settings.sfxVolume * volume;
            if (delay > 0f) src.PlayDelayed(delay);
            else src.Play();
        }

        /// <summary>Removal sound whose pitch climbs with quick successive removals.</summary>
        public void PlayRemove(int combo)
        {
            int step = ComboSteps[Mathf.Clamp(combo, 0, ComboSteps.Length - 1)];
            Play(Sfx.Remove, 0f, Mathf.Pow(2f, step / 12f) * 0.9f);
        }
    }
}
