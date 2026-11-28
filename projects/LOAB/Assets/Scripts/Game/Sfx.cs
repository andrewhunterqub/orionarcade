using System.Collections.Generic;
using UnityEngine;

namespace Loab.Game
{
    /// <summary>Tiny synthesiser: every sound is generated at startup, so the build ships no audio files.</summary>
    public sealed class Sfx : MonoBehaviour
    {
        const int Rate = 22050;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        AudioSource _one, _amb, _music;
        float _ambTarget, _musicTarget;
        string _musicId;
        readonly Dictionary<string, AudioClip> _musicClips = new Dictionary<string, AudioClip>();
        public bool Muted;

        void Awake()
        {
            _one = gameObject.AddComponent<AudioSource>();
            _amb = gameObject.AddComponent<AudioSource>();
            _amb.loop = true; _amb.volume = 0;
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true; _music.volume = 0;
            _clips["till"] = Make(0.45f, (t, i) => Env(t, 0.005f, 0.4f) * (0.5f * Sin(t, t < 0.12f ? 1320 : 1760) + 0.2f * Sin(t, 2640)));
            _clips["bell"] = Make(1.2f, (t, i) => Env(t, 0.002f, 1.1f) * (0.45f * Sin(t, 880) + 0.25f * Sin(t, 1318) + 0.15f * Sin(t, 2210)));
            _clips["place"] = Make(0.18f, (t, i) => Env(t, 0.002f, 0.16f) * (0.7f * Sin(t, 110 - t * 200) + 0.3f * Noise(i)));
            _clips["click"] = Make(0.05f, (t, i) => Env(t, 0.001f, 0.04f) * 0.4f * Sin(t, 1800));
            _clips["error"] = Make(0.25f, (t, i) => Env(t, 0.005f, 0.22f) * 0.35f * Square(t, 140));
            _clips["mop"] = Make(0.35f, (t, i) => Env(t, 0.05f, 0.3f) * 0.35f * Noise(i) * (0.5f + 0.5f * Sin(t, 6)));
            _clips["whistle"] = Make(0.4f, (t, i) => Env(t, 0.02f, 0.35f) * 0.3f * Sin(t, 2100 + 80 * Mathf.Sin(t * 40)));
            _clips["coin"] = Make(0.3f, (t, i) => Env(t, 0.002f, 0.28f) * 0.4f * Sin(t, t < 0.07f ? 988 : 1319));
            _clips["fanfare"] = Make(0.9f, (t, i) => Env(t, 0.01f, 0.85f) * 0.35f * (Sin(t, t < 0.2f ? 523 : t < 0.4f ? 659 : 784) + 0.5f * Sin(t, t < 0.4f ? 1046 : 1568)));
            // A cheer: filtered noise swelling and fading, with a few whoops.
            float lp = 0;
            var cr = new System.Random(7);
            _clips["cheer"] = Make(1.6f, (t, i) =>
            {
                lp += ((float)(cr.NextDouble() * 2 - 1) - lp) * 0.25f;
                float env = Mathf.Clamp01(t / 0.15f) * Mathf.Exp(-Mathf.Max(0, t - 0.3f) * 2.2f);
                float whoop = (t > 0.2f && t < 0.6f ? 0.15f * Sin(t, 520 + 400 * (t - 0.2f)) : 0) + (t > 0.5f && t < 0.9f ? 0.12f * Sin(t, 700 - 200 * (t - 0.5f)) : 0);
                return (lp * 1.3f + whoop) * env;
            });
            // ---- Event moments (task V2) ----
            // Applause: dozens of short clap bursts, thick in the middle, thinning out.
            var ar = new System.Random(11);
            var claps = new List<float>();
            for (int k = 0; k < 70; k++) claps.Add((float)(ar.NextDouble() * ar.NextDouble() * 1.7));
            _clips["applause"] = Make(1.9f, (t, i) =>
            {
                float v = 0;
                foreach (var c in claps) { float d = t - c; if (d >= 0 && d < 0.03f) v += Mathf.Exp(-d * 160f) * Noise(i) * 0.5f; }
                return v * Mathf.Clamp01(1.9f - t);
            });
            // Laughter: a run of "ha"s, a vowel-ish tone pulsing about six times a second, falling in pitch.
            _clips["laugh"] = Make(1.3f, (t, i) =>
            {
                float pulse = Mathf.Max(0, Mathf.Sin(2 * Mathf.PI * 5.5f * t));
                float f = 260f - 60f * t;
                return Env(t, 0.03f, 1.1f) * pulse * pulse * (0.3f * Sin(t, f) + 0.15f * Sin(t, f * 2.7f) + 0.08f * Noise(i));
            });
            // Quiz chime: a soft two-note ding-dong.
            _clips["chime"] = Make(1.0f, (t, i) => (t < 0.45f ? Env(t, 0.003f, 0.5f) * Sin(t, 1047) : Env(t - 0.45f, 0.003f, 0.5f) * Sin(t, 784)) * 0.32f);
            // Guitar riff: three plucked notes with decaying harmonics.
            _clips["riff"] = Make(1.0f, (t, i) =>
            {
                float[] notes = { 164.8f, 220f, 246.9f };
                int n = Mathf.Min(2, (int)(t / 0.22f));
                float lt = t - n * 0.22f;
                float f = notes[n], e = Mathf.Exp(-lt * 6f);
                return 0.3f * e * (Sin(lt, f) + 0.5f * Sin(lt, f * 2) + 0.25f * Sin(lt, f * 3) + 0.12f * Square(lt, f));
            });
            // Karaoke key change: a sung note that slides up a tone, with vibrato.
            _clips["keychange"] = Make(0.9f, (t, i) => Env(t, 0.05f, 0.8f) * 0.3f * (Sin(t, (t < 0.4f ? 392f : 440f) + 6f * Mathf.Sin(t * 35f)) + 0.3f * Sin(t, 880f)));
            // Goal: a stadium horn, then the cheer.
            _clips["goal"] = Make(0.6f, (t, i) => Env(t, 0.02f, 0.55f) * 0.25f * (Square(t, 233) * 0.5f + Sin(t, 466)));

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

        /// Background music in one of the bar's styles (task V1). level 0 fades it out.
        public void Music(string id, float level)
        {
            _musicTarget = Muted || string.IsNullOrEmpty(id) ? 0 : Mathf.Clamp01(level) * 0.22f;
            if (_musicTarget <= 0 || id == _musicId) return;
            var clip = MusicClip(id);
            if (clip == null) return;
            _musicId = id;
            _music.clip = clip;
            _music.Play();
        }

        void Update()
        {
            _amb.volume = Mathf.MoveTowards(_amb.volume, _ambTarget, Time.unscaledDeltaTime * 0.3f);
            _music.volume = Mathf.MoveTowards(_music.volume, _musicTarget, Time.unscaledDeltaTime * 0.15f);
        }

        // ---- Music (task V1): a short loop per style, generated the first time it's needed ----------

        sealed class Recipe
        {
            public float Bpm;
            public int[] Prog;          // chord roots in semitones above A (110 Hz)
            public bool Minor, Sevenths, Power;
            public int Voice;           // 0 soft pad, 1 pluck arpeggio, 2 saw arpeggio, 3 square arpeggio, 4 chord stabs (keys), 5 strummed power chords
            public int ArpRate = 2;     // steps per arpeggio note (16th-note steps)
            public ushort Kick, Snare, Hat, Bass; // 16-step patterns, bit n = step n
            public bool Walking;        // jazz walking bass: a chord tone on every beat
        }

        static ushort P(params int[] steps) { ushort m = 0; foreach (var s in steps) m |= (ushort)(1 << s); return m; }

        static readonly Dictionary<string, Recipe> Recipes = new Dictionary<string, Recipe>
        {
            { "radio",      new Recipe { Bpm = 100, Prog = new[] { 0, 7, 9, 5 }, Voice = 0, Kick = P(0, 8), Snare = P(4, 12), Hat = P(0, 4, 8, 12), Bass = P(0, 8) } },
            { "jazz",       new Recipe { Bpm = 112, Prog = new[] { 2, 7, 0, 9 }, Sevenths = true, Voice = 4, Hat = P(0, 3, 4, 8, 11, 12), Walking = true } },
            { "indie",      new Recipe { Bpm = 122, Prog = new[] { 0, 9, 5, 7 }, Voice = 1, Kick = P(0, 8, 10), Snare = P(4, 12), Hat = P(0, 2, 4, 6, 8, 10, 12, 14), Bass = P(0, 6, 8, 14) } },
            { "rock",       new Recipe { Bpm = 112, Prog = new[] { 0, 5, 7, 5 }, Power = true, Voice = 5, Kick = P(0, 6, 8), Snare = P(4, 12), Hat = P(0, 2, 4, 6, 8, 10, 12, 14), Bass = P(0, 2, 4, 6, 8, 10, 12, 14) } },
            { "pop",        new Recipe { Bpm = 118, Prog = new[] { 0, 7, 9, 5 }, Voice = 1, ArpRate = 1, Kick = P(0, 4, 8, 12), Snare = P(4, 12), Hat = P(2, 6, 10, 14), Bass = P(0, 3, 8, 11) } },
            { "electronic", new Recipe { Bpm = 124, Prog = new[] { 0, 0, 8, 10 }, Minor = true, Voice = 2, ArpRate = 1, Kick = P(0, 4, 8, 12), Snare = P(4, 12), Hat = P(2, 6, 10, 14), Bass = P(2, 6, 10, 14) } },
            { "folk",       new Recipe { Bpm = 96,  Prog = new[] { 0, 5, 7, 0 }, Voice = 1, Kick = P(0), Bass = P(0, 8) } },
            { "soul",       new Recipe { Bpm = 94,  Prog = new[] { 0, 5, 2, 7 }, Sevenths = true, Voice = 4, Kick = P(0, 3, 10), Snare = P(4, 12), Hat = P(0, 2, 4, 6, 8, 10, 12, 14), Bass = P(0, 3, 6, 10) } },
            { "chiptune",   new Recipe { Bpm = 140, Prog = new[] { 0, 8, 10, 7 }, Voice = 3, ArpRate = 1, Kick = P(0, 8), Snare = P(4, 12), Bass = P(0, 4, 8, 12) } },
        };

        AudioClip MusicClip(string id)
        {
            if (_musicClips.TryGetValue(id, out var cached)) return cached;
            if (!Recipes.TryGetValue(id, out var r)) return null;
            float step = 60f / r.Bpm / 4f;                 // a 16th note
            float bar = step * 16f;
            int bars = 8;                                  // two bars per chord
            uint seed = 777;
            float Rand() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed % 2000) / 1000f - 1f; }
            float lp = 0;
            float[] tones = r.Power ? new[] { 1f, 1.4983f, 2f }
                          : r.Sevenths ? new[] { 1f, r.Minor ? 1.1892f : 1.2599f, 1.4983f, 1.7818f }
                          : new[] { 1f, r.Minor ? 1.1892f : 1.2599f, 1.4983f };
            var clip = Make(bars * bar, (t, i) =>
            {
                int barIdx = (int)(t / bar);
                int chord = (barIdx / 2) % r.Prog.Length;
                float root = 110f * Mathf.Pow(2f, r.Prog[chord] / 12f);
                int s16 = (int)(t / step) % 16;
                float st = t % step;                       // time since this step began
                float v = 0;
                // Chords / lead
                switch (r.Voice)
                {
                    case 0: foreach (var k in tones) v += 0.05f * Sin(t, root * 2 * k) * (0.8f + 0.2f * Mathf.Sin(t * 1.5f)); break;
                    case 1:
                    case 2:
                    case 3:
                    {
                        int noteStep = s16 / r.ArpRate;
                        float nt = t % (step * r.ArpRate);
                        float f = root * 2 * tones[noteStep % tones.Length] * (noteStep % 8 >= 4 ? 2 : 1);
                        float e = Mathf.Exp(-nt * (r.Voice == 1 ? 9f : 6f));
                        float w = r.Voice == 1 ? Sin(nt, f) * 0.7f + Sin(nt, f * 2) * 0.2f
                                : r.Voice == 2 ? (2f * ((nt * f) % 1f) - 1f) * 0.5f
                                : Square(nt, f) * 0.35f;
                        v += 0.14f * e * w;
                        break;
                    }
                    case 4:
                        if (s16 == 4 || s16 == 12 || s16 == 7) { float e = Mathf.Exp(-st * 5f); foreach (var k in tones) v += 0.05f * e * (Sin(t, root * 2 * k) + 0.3f * Sin(t, root * 4 * k)); }
                        else foreach (var k in tones) v += 0.012f * Sin(t, root * 2 * k);
                        break;
                    case 5:
                        if (s16 % 2 == 0) { float e = Mathf.Exp(-st * 7f); foreach (var k in tones) v += 0.06f * e * Mathf.Clamp(Square(t, root * k) * 0.8f + (2f * ((t * root * k) % 1f) - 1f) * 0.4f, -1, 1); }
                        break;
                }
                // Bass
                if (r.Walking)
                {
                    int beat = s16 / 4; float bt = t % (step * 4);
                    float f = root * 0.5f * (beat == 0 ? 1f : beat == 1 ? tones[1] : beat == 2 ? tones[2] : 1.122f);
                    v += 0.16f * Mathf.Exp(-bt * 3f) * (Sin(bt, f) + 0.3f * Sin(bt, f * 2));
                }
                else if ((r.Bass >> s16 & 1) != 0) v += 0.15f * Mathf.Exp(-st * 6f) * (Sin(st, root * 0.5f) + 0.25f * Sin(st, root));
                // Drums
                if ((r.Kick >> s16 & 1) != 0) v += 0.28f * Mathf.Exp(-st * 14f) * Sin(st, 48f + 90f * Mathf.Exp(-st * 35f));
                float n = Rand();
                if ((r.Snare >> s16 & 1) != 0) v += 0.12f * Mathf.Exp(-st * 22f) * (n * 0.8f + 0.3f * Sin(st, 190));
                lp += (n - lp) * 0.6f;
                if ((r.Hat >> s16 & 1) != 0) v += 0.05f * Mathf.Exp(-st * 70f) * (n - lp);
                return v;
            });
            _musicClips[id] = clip;
            return clip;
        }
    }
}
