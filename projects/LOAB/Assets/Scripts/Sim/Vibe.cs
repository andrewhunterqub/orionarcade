using System;
using System.Collections.Generic;

namespace Loab.Sim
{
    public sealed class ThemeStatus
    {
        public ThemeDef Def;
        public int Count;
        public float Share;
        public bool Active;
        public float Progress; // 0..1 towards recognition
    }

    public sealed class VibeReport
    {
        public float[] Axes = new float[Axis.Count];
        public float Coherence;           // 0..100
        public List<ThemeStatus> Themes = new List<ThemeStatus>();
        public ThemeStatus ActiveTheme;   // strongest recognised theme or null
        public float[] SegMatch = new float[Content.Segments.Count]; // 0..1 vibe match per segment
        public int Seats, Toilets, BarPoints, Games, Screens;
        public bool HasStage, HasSpeakers;
        public float Comfort;             // average seat comfort
        public string Summary;
    }

    public static class VibeCalc
    {
        public static float Squash(float raw) => 50f + 50f * (float)Math.Tanh(raw / 45f);

        public static VibeReport Compute(GameState s)
        {
            var r = new VibeReport();
            var sum = new float[Axis.Count];
            var floor = Content.Floor(s.floor);
            var wall = Content.Wall(s.wall);
            var music = Content.Genre(s.music);
            Add(sum, floor.Vibe, 1.5f);
            Add(sum, wall.Vibe, 1.5f);
            Add(sum, music.Vibe, 1.3f);

            sum[Axis.Intimacy] += (50 - s.lighting) * 0.14f;
            sum[Axis.Energy] += (s.lighting - 50) * 0.05f;
            sum[Axis.Energy] += (s.volume - 50) * 0.14f;
            sum[Axis.Intimacy] -= (s.volume - 50) * 0.08f;

            // Items, with diminishing returns on repeats of the same thing.
            var copies = new Dictionary<string, int>();
            var styled = new List<float[]> { floor.Vibe, wall.Vibe, music.Vibe };
            int seatsComfortSum = 0, cellsUsed = 0;
            foreach (var it in s.items)
            {
                var d = Content.Item(it.id);
                if (d == null) continue;
                copies.TryGetValue(d.Id, out int k);
                copies[d.Id] = k + 1;
                float weight = (float)Math.Pow(0.8, k) * (1f - it.wear / 250f);
                Add(sum, d.Vibe, weight);
                if (!IsZero(d.Vibe)) styled.Add(d.Vibe);
                cellsUsed += d.W * d.H;
                switch (d.Fn)
                {
                    case ItemFn.Seat: r.Seats += d.Capacity; seatsComfortSum += d.Comfort * d.Capacity; break;
                    case ItemFn.Toilet: r.Toilets += d.Capacity; break;
                    case ItemFn.BarCounter: r.BarPoints += d.Capacity; break;
                    case ItemFn.Games: r.Games++; break;
                    case ItemFn.Screen: r.Screens++; break;
                    case ItemFn.Stage: r.HasStage = true; break;
                    case ItemFn.Speaker: r.HasSpeakers = true; break;
                }
            }
            r.Comfort = r.Seats > 0 ? seatsComfortSum / (float)r.Seats : 0;

            // A sparse room feels big and open; a packed one feels cosy.
            float fill = cellsUsed / (float)(s.gridW * s.gridH);
            sum[Axis.Intimacy] += (fill - 0.3f) * 30f;

            // Prices nudge Polish.
            float ratio = PriceRatio(s);
            sum[Axis.Polish] += (ratio - 1f) * 35f;

            // Mascot dog adds quirk.
            if (s.GetFlag("dog") == 1) sum[Axis.Quirk] += 6;

            for (int a = 0; a < Axis.Count; a++) r.Axes[a] = Squash(sum[a]);

            // Coherence: how aligned the styled elements are.
            var mean = new float[Axis.Count];
            foreach (var v in styled)
            {
                float len = Len(v);
                if (len < 0.01f) continue;
                for (int a = 0; a < Axis.Count; a++) mean[a] += v[a] / len;
            }
            float coh = styled.Count > 0 ? Len(mean) / styled.Count : 0;
            r.Coherence = Clamp(coh * 110f, 0, 100);

            // Themes
            foreach (var t in Content.Themes)
            {
                int count = 0;
                var perId = new Dictionary<string, int>();
                foreach (var it in s.items)
                {
                    var d = Content.Item(it.id);
                    if (Array.IndexOf(d.Themes, t.Id) < 0) continue;
                    perId.TryGetValue(d.Id, out int n);
                    if (n < 3) count++;
                    perId[d.Id] = n + 1;
                }
                foreach (var st in new[] { floor.Themes, wall.Themes, music.Themes })
                    if (Array.IndexOf(st, t.Id) >= 0) count += 2;
                foreach (var m in s.menu)
                {
                    if (!m.onMenu) continue;
                    var dd = Content.Drink(m.id);
                    if (Array.IndexOf(dd.Themes, t.Id) >= 0) count += 1;
                }
                int total = Math.Max(1, styled.Count + 3);
                float share = Math.Min(1f, count / (float)total);
                var ts = new ThemeStatus { Def = t, Count = count, Share = share };
                ts.Progress = Math.Min(1f, count / 12f) * Math.Min(1f, share / 0.45f);
                ts.Active = count >= 12 && share >= 0.45f;
                r.Themes.Add(ts);
                if (ts.Active && (r.ActiveTheme == null || ts.Count > r.ActiveTheme.Count)) r.ActiveTheme = ts;
            }
            r.Themes.Sort((a, b) => b.Progress.CompareTo(a.Progress));
            if (r.ActiveTheme != null) r.Coherence = Math.Max(r.Coherence, 75f) + 10f;
            r.Coherence = Clamp(r.Coherence, 0, 100);

            for (int i = 0; i < Content.Segments.Count; i++) r.SegMatch[i] = SegmentMatch(r.Axes, Content.Segments[i]);
            r.Summary = Describe(r.Axes);
            return r;
        }

        public static float SegmentMatch(float[] axes, SegmentDef seg)
        {
            float d2 = 0;
            for (int a = 0; a < Axis.Count; a++)
            {
                float diff = axes[a] - seg.VibePref[a];
                d2 += diff * diff;
            }
            d2 /= Axis.Count;
            return (float)Math.Exp(-d2 / (2 * seg.VibeTolerance * seg.VibeTolerance));
        }

        /// <summary>Average price relative to defaults for drinks on the menu.</summary>
        public static float PriceRatio(GameState s)
        {
            float t = 0; int n = 0;
            foreach (var m in s.menu)
            {
                if (!m.onMenu) continue;
                var d = Content.Drink(m.id);
                t += m.price / d.DefaultPrice; n++;
            }
            return n == 0 ? 1f : t / n;
        }

        /// <summary>The biggest mismatch between the bar and a segment, as a readable complaint (or null).</summary>
        public static string Complaint(float[] axes, SegmentDef seg)
        {
            int worst = -1; float worstDiff = 0;
            for (int a = 0; a < Axis.Count; a++)
            {
                float diff = axes[a] - seg.VibePref[a];
                if (Math.Abs(diff) > Math.Abs(worstDiff)) { worstDiff = diff; worst = a; }
            }
            if (Math.Abs(worstDiff) < 22) return null;
            bool tooHigh = worstDiff > 0;
            switch (worst)
            {
                case Axis.Energy: return tooHigh ? "Bit too loud and hectic for me" : "Felt a bit dead in here";
                case Axis.Polish: return tooHigh ? "A bit too posh for my liking" : "Could do with a bit of a tidy-up and some class";
                case Axis.Era: return tooHigh ? "Trying a bit too hard to be trendy" : "Feels stuck in the past";
                case Axis.Intimacy: return tooHigh ? "Cramped and dim" : "Big and echoey, not very cosy";
                default: return tooHigh ? "Too weird for me" : "Bit bland and generic";
            }
        }

        public static string Describe(float[] axes)
        {
            var words = new List<string>();
            for (int a = 0; a < Axis.Count; a++)
            {
                if (axes[a] >= 65) words.Add(Axis.High[a].ToLowerInvariant());
                else if (axes[a] <= 35) words.Add(Axis.Low[a].ToLowerInvariant());
            }
            if (words.Count == 0) return "Middle of the road";
            var txt = string.Join(", ", words);
            return char.ToUpperInvariant(txt[0]) + txt.Substring(1);
        }

        static void Add(float[] sum, float[] v, float w) { for (int a = 0; a < Axis.Count; a++) sum[a] += v[a] * w; }
        static bool IsZero(float[] v) { foreach (var x in v) if (x != 0) return false; return true; }
        static float Len(float[] v) { float t = 0; foreach (var x in v) t += x * x; return (float)Math.Sqrt(t); }
        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
