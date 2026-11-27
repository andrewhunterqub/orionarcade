using System;
using System.Collections.Generic;

namespace Loab.Sim
{
    /// <summary>Small deterministic RNG (xorshift) so nights are reproducible from a seed.</summary>
    public sealed class Rng
    {
        ulong _s;

        public Rng(ulong seed)
        {
            // splitmix64 so nearby seeds (day 1, day 2...) give unrelated streams
            ulong z = seed + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            _s = z == 0 ? 0x9E3779B97F4A7C15UL : z;
        }

        public ulong NextU64()
        {
            _s ^= _s << 13; _s ^= _s >> 7; _s ^= _s << 17;
            return _s;
        }

        /// <summary>[0, 1)</summary>
        public double Next() => (NextU64() >> 11) * (1.0 / (1UL << 53));
        public float NextF() => (float)Next();
        public float Range(float min, float max) => min + (max - min) * NextF();
        /// <summary>[min, max)</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(NextU64() % (ulong)(max - min));
        public bool Chance(double p) => Next() < p;

        public float Gaussian()
        {
            double u1 = 1.0 - Next(), u2 = Next();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }

        public T Pick<T>(IList<T> list) => list[Range(0, list.Count)];

        /// <summary>Poisson-distributed count with the given mean (Knuth for small means, normal approx for large).</summary>
        public int Poisson(double mean)
        {
            if (mean <= 0) return 0;
            if (mean > 30) return Math.Max(0, (int)Math.Round(mean + Math.Sqrt(mean) * Gaussian()));
            double l = Math.Exp(-mean), p = 1.0;
            int k = 0;
            do { k++; p *= Next(); } while (p > l);
            return k - 1;
        }

        public int WeightedIndex(IList<float> weights)
        {
            float total = 0;
            for (int i = 0; i < weights.Count; i++) total += Math.Max(0, weights[i]);
            if (total <= 0) return -1;
            float r = NextF() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                r -= Math.Max(0, weights[i]);
                if (r <= 0) return i;
            }
            return weights.Count - 1;
        }
    }
}
