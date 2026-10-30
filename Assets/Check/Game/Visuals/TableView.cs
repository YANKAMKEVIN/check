using UnityEngine;

namespace Check.Game.Visuals
{
    /// <summary>
    /// La table et son décor. CONTRAT public utilisé par la couche de jeu : garder ces signatures.
    /// Version provisoire (disque gris) : le rendu définitif remplace l'intérieur.
    /// Repère : table centrée en (0, 0, 0), plateau à y = <see cref="SurfaceY"/>, le joueur humain
    /// est assis côté -Z (bas de l'écran), les adversaires répartis sur l'arc du fond.
    /// </summary>
    public sealed class TableView : MonoBehaviour
    {
        public const float SurfaceY = 0f;
        /// <summary>Demi-largeur (X) et demi-profondeur (Z) de la surface de jeu.</summary>
        public const float HalfWidth = 3.2f;
        public const float HalfDepth = 4.2f;

        public Vector3 DrawPilePosition => new Vector3(-0.55f, SurfaceY, 0.35f);
        public Vector3 DiscardPosition => new Vector3(0.55f, SurfaceY, 0.35f);

        public void Build()
        {
            var top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            top.name = "Table";
            top.transform.SetParent(transform, false);
            top.transform.localPosition = new Vector3(0f, SurfaceY - 0.05f, 0f);
            top.transform.localScale = new Vector3(HalfWidth * 2f, 0.05f, HalfDepth * 2f);
            top.GetComponent<Renderer>().material.color = new Color(0.08f, 0.08f, 0.12f);
        }

        /// <summary>Indique au décor à qui est le tour (halo, lumière…). Seat = place à table, n = joueurs.</summary>
        public void SetActiveSeat(Vector3 seatPosition) { }

        /// <summary>Teinte d'ambiance selon la pénalité en attente (0 = aucune).</summary>
        public void SetTension(int pendingDraw) { }
    }
}
