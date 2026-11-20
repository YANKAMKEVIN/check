using System;

namespace Check.Core
{
    /// <summary>
    /// Ordre de jeu vu de la table : les joueurs encore en course jouent par identifiant croissant
    /// (les places d'un tour suivent l'ordre des survivants), en sautant ceux qui sont qualifiés ou éliminés.
    /// </summary>
    public static class TurnOrder
    {
        /// <summary>Joueur qui joue après <paramref name="player"/>, -1 si personne d'autre n'est en course.</summary>
        public static int Next(int player, int playerCount, Func<int, bool> isOut)
        {
            if (player < 0 || playerCount <= 1) return -1;
            for (int i = 1; i < playerCount; i++)
            {
                int p = (player + i) % playerCount;
                if (!isOut(p)) return p;
            }
            return -1;
        }
    }
}
