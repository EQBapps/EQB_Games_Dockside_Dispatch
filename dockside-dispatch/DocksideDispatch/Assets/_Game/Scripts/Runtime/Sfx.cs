using System.Collections.Generic;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Sound effects from Resources/Audio (original sounds synthesised for this game).
    /// A small pool of AudioSources lets sounds overlap; slight pitch variation keeps repeats from sounding robotic.
    /// The on/off setting is saved in PlayerPrefs ("dd.sound").
    /// </summary>
    public static class Sfx
    {
        private const string Key = "dd.sound";
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        private static AudioSource[] _pool;
        private static int _next;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(Key, 1) == 1;
            set { PlayerPrefs.SetInt(Key, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        private static void EnsurePool()
        {
            if (_pool != null && _pool[0]) return;
            var go = new GameObject("Sfx");
            Object.DontDestroyOnLoad(go);
            _pool = new AudioSource[8];
            for (int i = 0; i < _pool.Length; i++)
            {
                _pool[i] = go.AddComponent<AudioSource>();
                _pool[i].playOnAwake = false;
                _pool[i].spatialBlend = 0f;
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor only: called for every sound played (the promo-video recorder logs these to rebuild the audio).</summary>
        public static System.Action<string, float> EditorPlayed;
#endif

        public static void Play(string name, float volume = 1f, float pitchJitter = 0.04f)
        {
            if (!Enabled) return;
#if UNITY_EDITOR
            EditorPlayed?.Invoke(name, volume);
#endif
            if (!Clips.TryGetValue(name, out var clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + name);
                Clips[name] = clip;
            }
            if (clip == null) return;
            EnsurePool();
            var src = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.PlayOneShot(clip, volume);
        }
    }
}
