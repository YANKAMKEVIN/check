using System;
using UnityEngine;

namespace Check.Game.Visuals
{
    /// <summary>
    /// Prototypes de rendu pour choisir la refonte visuelle (image cible n° 3), lancés avec « -check-style » :
    ///   classique : décor actuel (par défaut) ;
    ///   hybride   : table et cartes en 3D, wagon peint en fond, adversaires illustrés autour de la table ;
    ///   3d        : tout en 3D, wagon texturé (bois, cuir) et adversaires modélisés.
    /// </summary>
    public enum DecorStyle { Classique, Hybride, Tout3D }

    public static class DecorStyles
    {
        const string Arg = "-check-style";
        static DecorStyle? current;

        public static DecorStyle Current => current ??= Parse();

        /// <summary>Les prototypes cadrent la table plus bas, comme l'image cible.</summary>
        public static bool LowCamera => Current != DecorStyle.Classique;

        static DecorStyle Parse()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != Arg) continue;
                switch (args[i + 1].ToLowerInvariant())
                {
                    case "hybride": return DecorStyle.Hybride;
                    case "3d": return DecorStyle.Tout3D;
                }
            }
            return DecorStyle.Classique;
        }

        /// <summary>
        /// Place d'un adversaire autour de la table (partie à 4 joueurs du prototype) : sur l'ellipse des places,
        /// repoussée juste derrière le bord de la table.
        /// </summary>
        public static Vector3 OpponentSpot(int playerId, int playerCount)
        {
            // Même répartition que TableLayout.SeatAngle (couche de jeu, hors de cet assembly).
            int opponents = playerCount - 1;
            float span = Mathf.Min(220f, 62f * (opponents - 1));
            float deg = opponents <= 1 ? 90f : 90f + span * 0.5f - (playerId - 1) * span / (opponents - 1);
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * TableView.HalfWidth * 1.2f, TableView.SurfaceY, Mathf.Sin(a) * TableView.HalfDepth * 1.1f + 0.3f);
        }
    }
}
