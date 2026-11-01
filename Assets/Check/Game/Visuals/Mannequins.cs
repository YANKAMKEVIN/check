using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Check.Game.Visuals
{
    /// <summary>
    /// Prototype « tout 3D » : adversaires modélisés en volumes simples (chef de gare, voyageuse, contrôleur),
    /// assis derrière la table, avec une respiration et un léger balancement. C'est ce qu'on obtient vite en 3D
    /// pure ; des personnages détaillés demanderaient des modèles faits par un artiste.
    /// </summary>
    public sealed class Mannequins : MonoBehaviour
    {
        const float Scale = 1.45f;
        Transform[] bodies;

        public static Mannequins Build(Transform parent)
        {
            var go = new GameObject("Mannequins");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<Mannequins>();
            m.BuildAll();
            return m;
        }

        struct Look
        {
            public Color Coat, Hat, Skin, Accent;
            public bool Cloche;
        }

        void BuildAll()
        {
            var looks = new[]
            {
                new Look { Coat = new Color(0.10f, 0.12f, 0.20f), Hat = new Color(0.08f, 0.10f, 0.17f), Skin = new Color(0.86f, 0.66f, 0.54f), Accent = WagonDecor.Amber },
                new Look { Coat = new Color(0.40f, 0.33f, 0.27f), Hat = new Color(0.55f, 0.45f, 0.36f), Skin = new Color(0.93f, 0.76f, 0.66f), Accent = WagonDecor.Cream, Cloche = true },
                new Look { Coat = new Color(0.12f, 0.15f, 0.25f), Hat = new Color(0.10f, 0.12f, 0.21f), Skin = new Color(0.80f, 0.60f, 0.48f), Accent = WagonDecor.Cream },
            };
            bodies = new Transform[looks.Length];
            for (int i = 0; i < looks.Length; i++)
            {
                var root = new GameObject($"Opponent{i + 1}").transform;
                root.SetParent(transform, false);
                var spot = DecorStyles.OpponentSpot(i + 1, 4);
                root.localPosition = spot;
                var toCenter = new Vector3(0f, 0f, 0.55f) - spot;
                toCenter.y = 0f;
                root.localRotation = Quaternion.LookRotation(toCenter.normalized, Vector3.up);
                root.localScale = Vector3.one * Scale;
                bodies[i] = BuildFigure(root, looks[i]);
            }
        }

        static Transform BuildFigure(Transform root, Look look)
        {
            var coat = NeonMaterials.CreateLitOpaque("Mannequin_Coat", look.Coat, 0f, 0.25f, Color.black);
            var hat = NeonMaterials.CreateLitOpaque("Mannequin_Hat", look.Hat, 0f, 0.35f, Color.black);
            var skin = NeonMaterials.CreateLitOpaque("Mannequin_Skin", look.Skin, 0f, 0.3f, Color.black);
            var accent = NeonMaterials.CreateLitOpaque("Mannequin_Accent", look.Accent, look.Accent == WagonDecor.Amber ? 0.9f : 0f, 0.6f, Color.black);
            var cards = NeonMaterials.CreateLitOpaque("Mannequin_Cards", new Color(0.10f, 0.13f, 0.26f), 0f, 0.4f, Color.black);

            // 0 manteau, 1 chapeau, 2 peau, 3 accent, 4 cartes
            var b = new MeshBuilder(5);
            // Buste (repère local : y = 0 au niveau du plateau, +z vers la table).
            b.Lathe(new List<ProfilePoint>
            {
                new ProfilePoint(0.42f, -0.9f, 0f, 0), new ProfilePoint(0.40f, -0.2f), new ProfilePoint(0.36f, 0.25f),
                new ProfilePoint(0.40f, 0.62f), new ProfilePoint(0.30f, 0.80f), new ProfilePoint(0.10f, 0.86f, 0f, 2),
                new ProfilePoint(0.09f, 0.95f),
            }, 18, new Vector3(0f, 0f, -0.15f));
            // Tête.
            var head = new Vector3(0f, 1.15f, -0.13f);
            var hp = new List<ProfilePoint>();
            for (int k = 0; k <= 8; k++)
            {
                float a = Mathf.PI * k / 8f;
                hp.Add(new ProfilePoint(Mathf.Sin(a) * 0.19f + 0.0001f, -Mathf.Cos(a) * 0.23f, 0f, 2));
            }
            b.Lathe(hp, 16, head);
            // Chapeau : cloche (voyageuse) ou képi à visière.
            if (look.Cloche)
            {
                b.Lathe(new List<ProfilePoint>
                {
                    new ProfilePoint(0.30f, 0.02f, 0f, 1), new ProfilePoint(0.24f, 0.06f, 0f, 1), new ProfilePoint(0.21f, 0.20f, 0f, 1),
                    new ProfilePoint(0.12f, 0.30f, 0f, 1), new ProfilePoint(0.001f, 0.32f, 0f, 1),
                }, 18, head);
                b.Lathe(new List<ProfilePoint> { new ProfilePoint(0.215f, 0.07f, 0f, 3), new ProfilePoint(0.212f, 0.12f) }, 18, head);
            }
            else
            {
                b.Lathe(new List<ProfilePoint>
                {
                    new ProfilePoint(0.20f, 0.08f, 0f, 3), new ProfilePoint(0.20f, 0.13f, 0f, 1), new ProfilePoint(0.23f, 0.26f, 0f, 1),
                    new ProfilePoint(0.001f, 0.27f, 0f, 1),
                }, 18, head);
                b.Box(1, head + new Vector3(0f, 0.09f, 0.2f), new Vector3(0.3f, 0.025f, 0.14f), Quaternion.Euler(-12f, 0f, 0f));
            }
            // Bras repliés vers l'avant, mains et petit éventail de cartes.
            for (int s = -1; s <= 1; s += 2)
            {
                b.Box(0, new Vector3(s * 0.40f, 0.45f, -0.05f), new Vector3(0.16f, 0.5f, 0.18f), Quaternion.Euler(20f, 0f, s * -8f));
                b.Box(0, new Vector3(s * 0.26f, 0.22f, 0.22f), new Vector3(0.15f, 0.15f, 0.48f), Quaternion.Euler(-25f, s * 20f, 0f));
                b.Box(2, new Vector3(s * 0.12f, 0.36f, 0.43f), new Vector3(0.10f, 0.12f, 0.10f), Quaternion.identity);
            }
            for (int c = 0; c < 5; c++)
                b.Box(4, new Vector3(0f, 0.5f, 0.47f), new Vector3(0.20f, 0.30f, 0.01f), Quaternion.Euler(-15f, 0f, (c - 2) * 12f));
            // Boutons dorés ou foulard.
            for (int k = 0; k < 4; k++) b.Box(3, new Vector3(0f, 0.65f - k * 0.17f, 0.24f), new Vector3(0.05f, 0.05f, 0.03f), Quaternion.identity);

            var go = new GameObject("Body");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Mannequin");
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { coat, hat, skin, accent, cards };
            r.shadowCastingMode = ShadowCastingMode.On;
            r.lightProbeUsage = LightProbeUsage.Off;
            return go.transform;
        }

        void Update()
        {
            if (bodies == null) return;
            float t = Time.time;
            for (int i = 0; i < bodies.Length; i++)
            {
                float p = t * 1.3f + i * 2.1f;
                bodies[i].localPosition = new Vector3(0f, Mathf.Sin(p) * 0.008f, 0f);
                bodies[i].localRotation = Quaternion.Euler(Mathf.Sin(p * 0.5f) * 1.2f, Mathf.Sin(p * 0.37f) * 3f, 0f);
            }
        }
    }
}
