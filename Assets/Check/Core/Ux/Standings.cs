using System.Collections.Generic;

namespace Check.Core
{
    /// <summary>Classement final d'une partie : le vainqueur, puis les éliminés du dernier au premier.</summary>
    public static class Standings
    {
        public static List<int> Order(int? winner, IReadOnlyList<int> eliminated)
        {
            var order = new List<int>();
            if (winner.HasValue) order.Add(winner.Value);
            for (int i = eliminated.Count - 1; i >= 0; i--)
                if (!order.Contains(eliminated[i])) order.Add(eliminated[i]);
            return order;
        }

        public static List<int> Of(Match match) => Order(match.Winner, match.Eliminated);

        /// <summary>Place du podium (0 = 2e à gauche, 1 = 1er au centre, 2 = 3e à droite) pour le rang 1, 2 ou 3.</summary>
        public static int PodiumSlot(int rank) => rank switch { 1 => 1, 2 => 0, 3 => 2, _ => -1 };
    }
}
