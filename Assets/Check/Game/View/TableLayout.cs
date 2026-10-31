using Check.Game.Visuals;
using UnityEngine;

namespace Check.Game.View
{
    /// <summary>Place d'un joueur à table (repère monde).</summary>
    public struct SeatGeometry
    {
        /// <summary>Centre de la main (petit éventail) sur la table.</summary>
        public Vector3 HandCenter;
        /// <summary>Direction horizontale de la place vers le centre de la table.</summary>
        public Vector3 Facing;
        /// <summary>Point d'accroche du badge (nom, cartes, anneau), un peu en retrait derrière la main.</summary>
        public Vector3 BadgePoint;
    }

    /// <summary>
    /// Disposition de 2 à 10 joueurs en paysage : le joueur humain (id 0) en bas (côté -Z), les adversaires
    /// sur l'arc du fond et les côtés, dans le sens des aiguilles d'une montre vu du dessus (gauche, fond, droite).
    /// Les places dépendent de l'identifiant du joueur et du nombre de joueurs au départ : elles ne bougent pas
    /// quand quelqu'un est éliminé.
    /// </summary>
    public static class TableLayout
    {
        static readonly Vector3 Center = new Vector3(0f, TableView.SurfaceY, 0.55f);
        const float RadiusX = 2.75f;
        const float RadiusZ = 3.05f;

        /// <summary>Points à garder dans le cadre de la caméra : les mains adverses et le centre (les badges vont dans les marges).</summary>
        public static readonly Vector3[] FitPoints =
        {
            new Vector3(-3.2f, 0f, -0.8f), new Vector3(3.2f, 0f, -0.8f),
            new Vector3(-3.2f, 0.3f, 3.95f), new Vector3(3.2f, 0.3f, 3.95f),
        };

        /// <summary>Angle (degrés, 0 = +X, 90 = fond) de la place du joueur <paramref name="playerId"/>.</summary>
        public static float SeatAngle(int playerId, int playerCount)
        {
            if (playerId == 0) return -90f;
            int opponents = playerCount - 1;
            if (opponents <= 1) return 90f;
            // Arc plus ou moins ouvert selon le nombre d'adversaires (220° au maximum).
            float span = Mathf.Min(220f, 62f * (opponents - 1));
            float start = 90f + span * 0.5f;
            return start - (playerId - 1) * span / (opponents - 1);
        }

        public static SeatGeometry Seat(int playerId, int playerCount)
        {
            float a = SeatAngle(playerId, playerCount) * Mathf.Deg2Rad;
            var onEllipse = Center + new Vector3(RadiusX * Mathf.Cos(a), 0f, RadiusZ * Mathf.Sin(a));
            var facing = Center - onEllipse;
            facing.y = 0f;
            facing = facing.sqrMagnitude > 1e-4f ? facing.normalized : Vector3.forward;
            if (playerId == 0)
            {
                // Le joueur humain : sa main est tenue devant la caméra ; ce point sert aux animations sur la table.
                var p = new Vector3(0f, TableView.SurfaceY, -2.2f);
                return new SeatGeometry { HandCenter = p, Facing = Vector3.forward, BadgePoint = p };
            }
            return new SeatGeometry
            {
                HandCenter = onEllipse,
                Facing = facing,
                BadgePoint = onEllipse - facing * 0.62f + Vector3.up * 0.05f,
            };
        }
    }
}
