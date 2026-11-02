using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Check.Game.Visuals
{
    /// <summary>
    /// Décor peint (d'après l'image cible n° 3) : le wagon peint remplit le fond de l'écran, et chaque adversaire est
    /// un personnage illustré, détouré, assis derrière la table 3D qui le masque à la taille. Les personnages
    /// respirent, se redressent quand c'est leur tour et réagissent aux coups (pose, pioche, « Games », élimination).
    /// </summary>
    public sealed class PaintedScene : MonoBehaviour
    {
        const float BackdropDistance = 45f;

        /// <summary>Personnage illustré (Resources/Painted/perso_N) : genre, sens du regard (+1 vers la droite de
        /// l'image, -1 vers la gauche, 0 de face), hauteur des mains dans l'image (0 = bas) et hauteur en unités monde.</summary>
        readonly struct Portrait
        {
            public readonly int Index;
            public readonly bool Female;
            public readonly int Facing;
            public readonly float Hands, Height;

            public Portrait(int index, bool female, int facing, float hands = 0.42f, float height = 4f)
            {
                Index = index; Female = female; Facing = facing; Hands = hands; Height = height;
            }
        }

        static readonly Portrait[] Cast =
        {
            new Portrait(1, false, 1),               // chef de gare
            new Portrait(2, true, 0, 0.52f, 4.6f),  // voyageuse au chapeau cloche (en pied, de face)
            new Portrait(3, false, -1),              // contrôleur
            // Les suivants sont cadrés plus en pied : hauteur choisie pour garder la même taille de tête.
            new Portrait(4, true, 1, 0.66f, 6.8f),   // dame en satin émeraude
            new Portrait(5, true, 1, 0.55f, 5.2f),   // maman au foulard en pagne
            new Portrait(6, true, 1, 0.55f, 5.2f),   // reporter au béret
            new Portrait(7, true, 1, 0.59f, 5.7f),   // jeune femme au voile indigo
            new Portrait(8, false, 1, 0.55f, 5.2f),  // gentleman au feutre
            new Portrait(9, false, 1, 0.56f, 5.3f),  // jeune gars à la casquette
        };

        sealed class Figure
        {
            public Transform T;
            public Material Mat;
            public Portrait P;
            public Vector3 Base, ToCenter;
            public float Width, Height, Phase, Lift, Glow = 1f, Play, Draw, Hop, Out;
            public bool Mirror, Eliminated;
        }

        static readonly Dictionary<int, Texture2D> textures = new Dictionary<int, Texture2D>();
        static readonly System.Random rng = new System.Random();

        Transform backdrop;
        float backdropAspect = 16f / 9f;
        Figure[] seats = new Figure[0];
        int active = -1;

        public static PaintedScene Build(Transform parent)
        {
            var go = new GameObject("PaintedScene");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<PaintedScene>();
            s.BuildBackdrop();
            return s;
        }

        static Material Painted(string name, Texture2D tex, bool transparent)
        {
            // Couleur de base noire, image en émission : la peinture garde ses propres lumières.
            // Les personnages sont prémultipliés (RVB nul là où l'image est transparente).
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
            var tex = Resources.Load<Texture2D>("Painted/fond");
            if (tex == null) { Debug.LogError("[Check] Painted/fond introuvable"); return; }
            backdropAspect = (float)tex.width / tex.height;
            backdrop = Quad("Backdrop", Painted("Painted_Backdrop", tex, false));
            backdrop.SetParent(transform, false);
        }

        static Texture2D Load(int index)
        {
            if (!textures.TryGetValue(index, out var tex))
            {
                tex = Resources.Load<Texture2D>($"Painted/perso_{index}");
                if (tex == null) Debug.LogWarning($"[Check] Painted/perso_{index} introuvable");
                textures[index] = tex;
            }
            return tex;
        }

        // ------------------------------------------------------------------ places

        /// <summary>Assied un personnage par adversaire, de son genre quand c'est possible, sans doublon.</summary>
        public void SetPlayers(bool[] female)
        {
            foreach (var f in seats) if (f != null) Destroy(f.T.gameObject);
            int n = female?.Length ?? 0;
            seats = new Figure[n];
            active = -1;
            var free = new List<Portrait>();
            foreach (var p in Cast) if (Load(p.Index) != null) free.Add(p);
            if (free.Count == 0) return;
            for (int i = free.Count - 1; i > 0; i--) { int k = rng.Next(i + 1); (free[i], free[k]) = (free[k], free[i]); }
            var all = new List<Portrait>(free);

            for (int id = 1; id < n; id++)
            {
                if (free.Count == 0) free.AddRange(all); // plus d'adversaires que de portraits : on recommence
                var spot = DecorStyles.OpponentSpot(id, n);
                // De face de préférence au fond, de trois quarts sur les côtés ; sinon le premier du bon genre.
                bool center = Mathf.Abs(spot.x) < 1.2f;
                int pick = -1;
                for (int pass = 0; pass < 3 && pick < 0; pass++)
                    for (int k = 0; k < free.Count && pick < 0; k++)
                    {
                        var p = free[k];
                        bool genre = p.Female == female[id];
                        bool fits = (p.Facing == 0) == center;
                        if ((pass == 0 && genre && fits) || (pass == 1 && genre) || pass == 2) pick = k;
                    }
                seats[id] = Seat(free[pick], spot, id);
                free.RemoveAt(pick);
            }
        }

        Figure Seat(Portrait p, Vector3 spot, int id)
        {
            var tex = Load(p.Index);
            // Places proches de la caméra (parties à beaucoup de joueurs) : plus petits et plus écartés,
            // pour ne pas masquer la table.
            float near = Mathf.InverseLerp(2.5f, -1.5f, spot.z);
            float size = 1f - 0.3f * near;
            spot.x *= 1f + 0.15f * near;
            var mat = Painted($"Painted_Figure{id}", tex, true);
            var t = Quad($"Opponent{id}", mat);
            t.SetParent(transform, false);
            var toCenter = new Vector3(0f, 0f, 0.55f) - spot;
            toCenter.y = 0f;
            var f = new Figure
            {
                T = t, Mat = mat, P = p, ToCenter = toCenter.normalized,
                Width = p.Height * size * tex.width / tex.height,
                Height = p.Height * size,
                Base = spot + Vector3.up * (p.Height * size * (0.5f - p.Hands) + 0.05f),
                Phase = id * 2.1f,
                // Chacun regarde vers le centre de la table : on retourne l'image si besoin.
                Mirror = p.Facing * spot.x > 0f,
            };
            t.localPosition = f.Base;
            return f;
        }

        public void SetActive(int playerId) => active = playerId;

        public void React(int playerId, SeatReaction reaction)
        {
            if (playerId <= 0 || playerId >= seats.Length || seats[playerId] == null) return;
            var f = seats[playerId];
            switch (reaction)
            {
                case SeatReaction.Played: f.Play = 1f; break;
                case SeatReaction.Drew: f.Draw = 1f; break;
                case SeatReaction.Finished: f.Hop = 1f; break;
                case SeatReaction.Eliminated: f.Eliminated = true; break;
            }
        }

        // ------------------------------------------------------------------ rendu

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

            float t = Time.time, dt = Time.deltaTime;
            for (int id = 1; id < seats.Length; id++)
            {
                var f = seats[id];
                if (f == null) continue;
                bool isActive = id == active && !f.Eliminated;
                f.Lift = Mathf.MoveTowards(f.Lift, isActive ? 1f : 0f, dt * 4f);
                f.Out = Mathf.MoveTowards(f.Out, f.Eliminated ? 1f : 0f, dt * 1.5f);
                f.Play = Mathf.MoveTowards(f.Play, 0f, dt * 2.6f);
                f.Draw = Mathf.MoveTowards(f.Draw, 0f, dt * 2.2f);
                f.Hop = Mathf.MoveTowards(f.Hop, 0f, dt * 1.2f);

                // Se pencher vers la table pour poser, plonger un peu pour piocher, sautiller sur « Games ».
                float lean = Mathf.Sin(f.Play * Mathf.PI) * 0.28f;
                float dip = Mathf.Sin(f.Draw * Mathf.PI) * 0.1f;
                float hop = Mathf.Abs(Mathf.Sin(f.Hop * Mathf.PI * 3f)) * f.Hop * 0.35f;
                float breath = Mathf.Sin(t * 1.3f + f.Phase);
                f.T.localPosition = f.Base + f.ToCenter * lean
                    + Vector3.up * (f.Lift * 0.12f - dip + hop - f.Out * 0.35f + breath * 0.01f);

                // Pivot vertical seulement, tourné vers la caméra.
                var look = f.T.position - ct.position;
                look.y = 0f;
                if (look.sqrMagnitude > 1e-4f) f.T.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                f.T.localScale = new Vector3(f.Mirror ? -f.Width : f.Width, f.Height * (1f + breath * 0.006f), 1f);

                // Celui qui joue est un peu plus lumineux ; un éliminé s'éteint.
                float glow = (active > 0 && !isActive ? 0.88f : 1f) + f.Lift * 0.14f + hop * 0.3f;
                glow = Mathf.Lerp(glow, 0.32f, f.Out);
                if (Mathf.Abs(glow - f.Glow) > 0.002f)
                {
                    f.Glow = glow;
                    f.Mat.SetColor(NeonMaterials.EmissionColorProperty, Color.white * glow);
                }
            }
        }
    }
}
