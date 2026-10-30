using System;
using System.Collections.Generic;

namespace Check.Core
{
    /// <summary>Hasard injectable, pour des parties reproductibles dans les tests.</summary>
    public interface IRandom
    {
        /// <summary>Entier dans [0, max).</summary>
        int Next(int max);
    }

    public sealed class SystemRandom : IRandom
    {
        readonly Random random;
        public SystemRandom(int seed) { random = new Random(seed); }
        public SystemRandom() { random = new Random(); }
        public int Next(int max) => random.Next(max);
    }

    public static class RandomExtensions
    {
        /// <summary>Mélange de Fisher-Yates.</summary>
        public static void Shuffle<T>(this IRandom random, IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
