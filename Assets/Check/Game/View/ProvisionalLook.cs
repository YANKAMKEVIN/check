using Check.Game.Visuals;
using UnityEngine;

namespace Check.Game.View
{
    /// <summary>
    /// ADAPTATEUR TEMPORAIRE : les versions provisoires de CardView / TableView sont des primitives Unity avec le
    /// matériau par défaut, dont les variantes de shader sont retirées du build (affichage magenta). On leur donne un matériau
    /// URP pour pouvoir juger la disposition et l'interface. Sans effet sur le rendu définitif (aucun shader Standard).
    /// À supprimer quand les cartes et la table définitives seront livrées.
    /// </summary>
    public static class ProvisionalLook
    {
        static Material shared;

        public static void Apply(GameObject root)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                // Seules les primitives Unity (Cube, Cylinder) sans matériau néon du projet sont concernées.
                var mf = r.GetComponent<MeshFilter>();
                string mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "";
                if (mesh != "Cube" && mesh != "Cylinder") continue;
                if (m != null && m.name.StartsWith("Check_")) continue;
                if (shared == null) shared = NeonMaterials.CreateLitOpaque("Check_Provisional", Color.white, 0f, 0.35f, Color.black);
                var color = m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                    : m != null && m.HasProperty("_Color") ? m.color : Color.white;
                r.sharedMaterial = shared;
                r.material.color = color; // instance : CardView reprend la main sur la couleur ensuite
            }
        }
    }
}
