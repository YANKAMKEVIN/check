using System;
using UnityEngine;

namespace Check.Game.Visuals
{
    /// <summary>
    /// Décor de la partie (refonte choisie par Kevin d'après l'image cible n° 3, docs/captures/cible/) :
    ///   peint     : table et cartes en 3D, wagon peint en fond, adversaires illustrés autour de la table (défaut) ;
    ///   classique : ancien wagon généré par code, vue plongeante (« -check-style classique »).
    /// </summary>
    public enum DecorStyle { Peint, Classique }

    /// <summary>Réaction d'un adversaire assis à ce qui se passe dans la partie.</summary>
    public enum SeatReaction { Played, Drew, Finished, Eliminated }

    public static class DecorStyles
    {
        const string Arg = "-check-style";
        static DecorStyle? current;

        public static DecorStyle Current => current ??= Parse();

        public static bool Painted => Current == DecorStyle.Peint;

        static DecorStyle Parse()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == Arg && args[i + 1].ToLowerInvariant() == "classique") return DecorStyle.Classique;
            return DecorStyle.Peint;
        }

        /// <summary>Angle (degrés, 0 = +X, 90 = fond) de la place d'un adversaire : même répartition que TableLayout.SeatAngle.</summary>
        public static float SeatAngle(int playerId, int playerCount)
        {
            int opponents = playerCount - 1;
            if (opponents <= 1) return 90f;
            float span = Mathf.Min(220f, 62f * (opponents - 1));
            return 90f + span * 0.5f - (playerId - 1) * span / (opponents - 1);
        }

        /// <summary>Place d'un adversaire assis : sur l'ellipse des places, juste derrière le bord de la table.</summary>
        public static Vector3 OpponentSpot(int playerId, int playerCount)
        {
            float a = SeatAngle(playerId, playerCount) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * TableView.HalfWidth * 1.2f, TableView.SurfaceY, Mathf.Sin(a) * TableView.HalfDepth * 1.1f + 0.3f);
        }
    }
}
