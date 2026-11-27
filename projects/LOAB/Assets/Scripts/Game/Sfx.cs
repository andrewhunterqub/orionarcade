using System.Collections.Generic;
using UnityEngine;

namespace Loab.Game
{
    /// <summary>Tiny synthesiser: every sound is generated at startup, so the build ships no audio files.</summary>
    public sealed class Sfx : MonoBehaviour
    {
        const int Rate = 22050;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        AudioSource _one, _amb;
        float _ambTarget;
        public bool Muted;

        void Awake()
        {
            _one = gameObject.AddComponent<AudioSource>();
            _amb = gameObject.AddComponent<AudioSource>();
            _amb.loop = true; _amb.volume = 0;
            _clips["till"] = Make(0.45f, (t, i) => Env(t, 0.005f, 0.4f) * (0.5f * Sin(t, t < 0.12f ? 1320 : 1760) + 0.2f * Sin(t, 2640)));
            _clips["bell"] = Make(1.2f, (t, i) => Env(t, 0.002f, 1.1f) * (0.45f * Sin(t, 880) + 0.25f * Sin(t, 1318) + 0.15f * Sin(t, 2210)));
            _clips["place"] = Make(0.18f, (t, i) => Env(t, 0.002f, 0.16f) * (0.7f * Sin(t, 110 - t * 200) + 0.3f * Noise(i)));
            _clips["click"] = Make(0.05f, (t, i) => Env(t, 0.001f, 0.04f) * 0.4f * Sin(t, 1800));
            _clips["error"] = Make(0.25f, (t, i) => Env(t, 0.005f, 0.22f) * 0.35f * Square(t, 140));
            _clips["mop"] = Make(0.35f, (t, i) => Env(t, 0.05f, 0.3f) * 0.35f * Noise(i) * (0.5f + 0.5f * Sin(t, 6)));
            _clips["whistle"] = Make(0.4f, (t, i) => Env(t, 0.02f, 0.35f) * 0.3f * Sin(t, 2100 + 80 * Mathf.Sin(t * 40)));
            _clips["coin"] = Make(0.3f, (t, i) => Env(t, 0.002f, 0.28f) * 0.4f * Sin(t, t < 0.07f ? 988 : 1319));
            _clips["fanfare"] = Make(0.9f, (t, i) => Env(t, 0.01f, 0.85f) * 0.35f * (Sin(t, t < 0.2f ? 523 : t < 0.4f ? 659 : 784) + 0.5f * Sin(t, t < 0.4f ? 1046 : 1568)));
            // Crowd murmur: brown noise with slow swells and the odd glass clink.
            float brown = 0;
            var rng = new System.Random(3);
            _amb.clip = Make(4f, (t, i) =>
            {
                brown = Mathf.Clamp(brown + (float)(rng.NextDouble() * 2 - 1) * 0.02f, -1, 1) * 0.995f;
                float clink = (i % 9973 < 400) ? Env((i % 9973) / (float)Rate, 0.001f, 0.015f) * 0.15f * Sin(t, 3200) : 0;
                return brown * (0.7f + 0.3f * Mathf.Sin(t * 1.3f)) * 1.6f + clink;
            });
            _amb.Play();
        }

        static float Sin(float t, float f) => Mathf.Sin(2 * Mathf.PI * f * t);
        static float Square(float t, float f) => Mathf.Sign(Mathf.Sin(2 * Mathf.PI * f * t));
        static float Env(float t, float a, float d) => t < a ? t / a : Mathf.Exp(-(t - a) / (d * 0.3f));
        static uint _n = 12345;
        static float Noise(int i) { _n ^= _n << 13; _n ^= _n >> 17; _n ^= _n << 5; return (_n % 2000) / 1000f - 1f; }

        AudioClip Make(float seconds, System.Func<float, int, float> f)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, i), -1f, 1f);
            var clip = AudioClip.Create("sfx", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void Play(string id, float volume = 0.6f)
        {
            if (Muted || !_clips.TryGetValue(id, out var c)) return;
            _one.PlayOneShot(c, volume);
        }

        public void Ambience(float level) => _ambTarget = Muted ? 0 : Mathf.Clamp01(level) * 0.35f;

        void Update() => _amb.volume = Mathf.MoveTowards(_amb.volume, _ambTarget, Time.unscaledDeltaTime * 0.3f);
    }
}
