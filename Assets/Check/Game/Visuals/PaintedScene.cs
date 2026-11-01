using UnityEngine;
using UnityEngine.Rendering;

namespace Check.Game.Visuals
{
    /// <summary>
    /// Prototype « hybride » : le wagon peint (image générée d'après la cible n° 3) remplit le fond de l'écran,
    /// et les adversaires illustrés, détourés, sont assis autour de la table 3D. La table les masque à la taille.
    /// </summary>
    public sealed class PaintedScene : MonoBehaviour
    {
        const float BackdropDistance = 45f;
        // Par personnage (gauche, fond, droite) : hauteur de l'image en unités monde, et hauteur des mains
        // dans l'image (0 = bas, 1 = haut), posée juste au-dessus du plateau.
        static readonly float[] FigureHeight = { 4.0f, 4.6f, 4.0f };
        static readonly float[] HandsFraction = { 0.42f, 0.52f, 0.42f };

        Transform backdrop;
        float backdropAspect = 16f / 9f;
        Transform[] figures;
        float[] phase;

        public static PaintedScene Build(Transform parent)
        {
            var go = new GameObject("PaintedScene");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<PaintedScene>();
            s.BuildBackdrop();
            s.BuildFigures();
            return s;
        }

        static Material Painted(string name, Texture2D tex, bool transparent)
        {
            // Couleur de base noire, image en émission : la peinture garde ses propres lumières.
            var m = transparent
                ? NeonMaterials.CreateLitTransparent(name, new Color(0f, 0f, 0f, 1f), 0f, 0f, Color.white)
                : NeonMaterials.CreateLitOpaque(name, Color.black, 0f, 0f, Color.white);
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_EmissionMap", tex);
            m.EnableKeyword("_EMISSION");
            return m;
        }

        void BuildBackdrop()
        {
            var tex = Resources.Load<Texture2D>("Proto/fond");
            if (tex == null) { Debug.LogError("[Check] Proto/fond introuvable"); return; }
            backdropAspect = (float)tex.width / tex.height;
            backdrop = Quad("Backdrop", Painted("Proto_Backdrop", tex, false));
            backdrop.SetParent(transform, false);
        }

        void BuildFigures()
        {
            figures = new Transform[3];
            phase = new float[3];
            for (int i = 0; i < 3; i++)
            {
                var tex = Resources.Load<Texture2D>($"Proto/perso_{i + 1}");
                if (tex == null) { Debug.LogError($"[Check] Proto/perso_{i + 1} introuvable"); continue; }
                var f = Quad($"Figure{i + 1}", Painted($"Proto_Figure{i + 1}", tex, true));
                f.SetParent(transform, false);
                float w = FigureHeight[i] * tex.width / tex.height;
                f.localScale = new Vector3(w, FigureHeight[i], 1f);
                var spot = DecorStyles.OpponentSpot(i + 1, 4);
                f.localPosition = spot + Vector3.up * (FigureHeight[i] * (0.5f - HandsFraction[i]) + 0.05f);
                figures[i] = f;
                phase[i] = i * 2.1f;
            }
        }

        static Transform Quad(string name, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go.transform;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var ct = cam.transform;
            if (backdrop != null)
            {
                // Toujours face à la caméra, au loin, en remplissant l'écran (recadrage façon « cover »).
                float h = 2f * BackdropDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float w = h * cam.aspect;
                if (w / h > backdropAspect) h = w / backdropAspect; else w = h * backdropAspect;
                backdrop.SetPositionAndRotation(ct.position + ct.forward * BackdropDistance, ct.rotation);
                backdrop.localScale = new Vector3(w, h, 1f);
            }
            if (figures == null) return;
            float t = Time.time;
            for (int i = 0; i < figures.Length; i++)
            {
                var f = figures[i];
                if (f == null) continue;
                // Pivot vertical seulement, tourné vers la caméra ; légère respiration.
                var look = f.position - ct.position;
                look.y = 0f;
                if (look.sqrMagnitude > 1e-4f) f.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                float breath = Mathf.Sin(t * 1.3f + phase[i]);
                var s = f.localScale;
                f.localScale = new Vector3(s.x, FigureHeight[i] * (1f + breath * 0.006f), 1f);
            }
        }
    }
}
