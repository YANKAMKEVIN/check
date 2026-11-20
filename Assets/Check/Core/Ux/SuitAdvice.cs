using System.Collections.Generic;

namespace Check.Core
{
    /// <summary>Aide au choix de la couleur après un Valet : cartes de chaque couleur qu'il restera en main.</summary>
    public static class SuitAdvice
    {
        /// <summary>Cartes par couleur (index = <see cref="Suit"/>) dans <paramref name="hand"/>, sans celles posées ni les jokers.</summary>
        public static int[] Counts(IEnumerable<Card> hand, IEnumerable<Card> played = null)
        {
            var skip = played != null ? new HashSet<Card>(played) : new HashSet<Card>();
            var counts = new int[4];
            if (hand == null) return counts;
            foreach (var c in hand)
                if (!c.IsJoker && !skip.Contains(c)) counts[(int)c.Suit]++;
            return counts;
        }

        /// <summary>La couleur la plus fournie (la première dans l'ordre ♠ ♥ ♦ ♣ en cas d'égalité), null si aucune.</summary>
        public static Suit? Best(int[] counts)
        {
            int best = -1, max = 0;
            for (int i = 0; i < counts.Length; i++)
                if (counts[i] > max) { max = counts[i]; best = i; }
            return best >= 0 ? (Suit)best : (Suit?)null;
        }
    }
}
