using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Check.Game.Visuals
{
    /// <summary>
    /// Décor « Dernier Métro » : un wagon rétro aménagé en salon privé qui traverse une ville la nuit.
    /// La table est au milieu de l'allée ; la caméra regarde le long du wagon (+Z). Parois latérales
    /// percées de grandes fenêtres, banquettes, barres en laiton, plafond à rampes lumineuses, porte
    /// vitrée au fond. Dehors, des immeubles éclairés défilent (deux plans de parallaxe) et des bandes
    /// de lumière (lampadaires du quai) balaient les fenêtres. Tout est généré par code.
    /// Le centre de l'écran reste calme : aucun mouvement ni reflet sur la table.
    /// </summary>
    public sealed class WagonDecor : MonoBehaviour
    {
        // Palette (Codex) : bleu pétrole, ambre, bleu nuit, crème.
        public static readonly Color Petrol = new Color(0.090f, 0.247f, 0.275f);
        public static readonly Color Amber = new Color(0.851f, 0.608f, 0.349f);
        public static readonly Color Night = new Color(0.141f, 0.188f, 0.278f);
        public static readonly Color Cream = new Color(0.953f, 0.898f, 0.808f);
        /// <summary>Couleur du ciel nocturne derrière les fenêtres (fond de caméra et brouillard).</summary>
        public static readonly Color Sky = new Color(0.030f, 0.045f, 0.085f);

        const float WallX = 6.4f;          // paroi intérieure
        const float WallThickness = 0.25f;
        const float SillY = 0.35f;          // bas des fenêtres
        const float LintelY = 3.9f;         // haut des fenêtres
        const float CeilingY = 5.2f;
        const float ZStart = -10f, ZEnd = 13.5f;
        const float WindowLength = 3.1f, PillarLength = 0.9f;

        const float CitySpeed = 7.5f;       // vitesse apparente du train
        const float SegmentLength = 48f;

        float floorY;
        readonly List<Transform> segments = new List<Transform>();
        readonly List<float> segmentSpeed = new List<float>();
        Transform[] bands;
        float[] bandSpeed, bandWait;
        Material bandMat;
        System.Random rnd = new System.Random(1983);

        public static WagonDecor Build(Transform parent, float floorY)
        {
            var go = new GameObject("Wagon");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<WagonDecor>();
            w.floorY = floorY;
            w.BuildInterior();
            w.BuildCity();
            w.BuildBands();
            return w;
        }

        // ------------------------------------------------------------------ intérieur

        void BuildInterior()
        {
            var wall = NeonMaterials.CreateLitOpaque("Wagon_Wall", new Color(0.74f, 0.68f, 0.58f), 0f, 0.35f, Color.black);
            var lower = NeonMaterials.CreateLitOpaque("Wagon_Lower", Petrol * 0.9f, 0f, 0.45f, Color.black);
            var wood = NeonMaterials.CreateLitOpaque("Wagon_Wood", new Color(0.36f, 0.21f, 0.12f), 0f, 0.55f, Color.black);
            var brass = NeonMaterials.CreateLitOpaque("Wagon_Brass", Amber, 0.9f, 0.72f, Amber * 0.04f);
            var leather = NeonMaterials.CreateLitOpaque("Wagon_Leather", new Color(0.42f, 0.13f, 0.12f), 0f, 0.5f, Color.black);
            var floor = NeonMaterials.CreateLitOpaque("Wagon_Floor", new Color(0.16f, 0.15f, 0.15f), 0f, 0.4f, Color.black);
            var lamp = NeonMaterials.CreateLitOpaque("Wagon_Lamp", Color.black, 0f, 0.5f, new Color(1f, 0.82f, 0.58f) * 1.3f);
            var glass = NeonMaterials.CreateLitOpaque("Wagon_DoorGlass", new Color(0.02f, 0.03f, 0.05f), 0f, 0.95f, new Color(0.05f, 0.08f, 0.14f));

            // 0 paroi crème, 1 soubassement pétrole, 2 bois, 3 laiton, 4 cuir, 5 sol, 6 lampes, 7 vitre de porte
            var b = new MeshBuilder(8);
            float len = ZEnd - ZStart, midZ = (ZStart + ZEnd) * 0.5f;

            // Sol : linoléum sombre, avec une bande d'allée plus claire sous la table.
            b.Box(5, new Vector3(0f, floorY - 0.1f, midZ), new Vector3(2f * WallX + 1f, 0.2f, len), Quaternion.identity);
            b.Box(1, new Vector3(0f, floorY + 0.005f, midZ), new Vector3(3.2f, 0.01f, len), Quaternion.identity);

            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * (WallX + WallThickness * 0.5f);
                // Soubassement et frise sous les fenêtres.
                b.Box(1, new Vector3(x, (floorY + SillY) * 0.5f, midZ), new Vector3(WallThickness, SillY - floorY, len), Quaternion.identity);
                b.Box(2, new Vector3(x - s * 0.18f, SillY - 0.05f, midZ), new Vector3(0.22f, 0.12f, len), Quaternion.identity); // appui en bois
                // Bandeau au-dessus des fenêtres jusqu'au plafond.
                b.Box(0, new Vector3(x, (LintelY + CeilingY) * 0.5f, midZ), new Vector3(WallThickness, CeilingY - LintelY, len), Quaternion.identity);
                b.Box(3, new Vector3(x - s * 0.14f, LintelY + 0.04f, midZ), new Vector3(0.05f, 0.05f, len), Quaternion.identity); // filet laiton

                // Montants entre les fenêtres (les vides sont les fenêtres).
                for (float z = ZStart; z < ZEnd; z += WindowLength + PillarLength)
                {
                    b.Box(0, new Vector3(x, (SillY + LintelY) * 0.5f, z + PillarLength * 0.5f), new Vector3(WallThickness, LintelY - SillY, PillarLength), Quaternion.identity);
                    // Cadre arrondi suggéré : petits congés en haut de la fenêtre.
                    float wz0 = z + PillarLength, wz1 = wz0 + WindowLength;
                    b.Box(0, new Vector3(x - s * 0.02f, LintelY - 0.12f, wz0 + 0.12f), new Vector3(WallThickness * 0.8f, 0.24f, 0.24f), Quaternion.Euler(45f, 0f, 0f));
                    b.Box(0, new Vector3(x - s * 0.02f, LintelY - 0.12f, wz1 - 0.12f), new Vector3(WallThickness * 0.8f, 0.24f, 0.24f), Quaternion.Euler(45f, 0f, 0f));
                }

                // Banquettes en cuir le long des parois, en face à face de part et d'autre de la table.
                float seatX = s * (WallX - 0.75f);
                for (float z = ZStart + 1f; z < ZEnd - 1f; z += 7.2f)
                {
                    float zc = z + 2.6f;
                    b.Box(2, new Vector3(seatX, floorY + 0.55f, zc), new Vector3(1.35f, 1.1f, 5.0f), Quaternion.identity);         // coffre
                    b.Box(4, new Vector3(seatX, floorY + 1.22f, zc), new Vector3(1.45f, 0.26f, 5.0f), Quaternion.identity);        // assise
                    b.Box(4, new Vector3(s * (WallX - 0.18f), floorY + 2.05f, zc), new Vector3(0.32f, 1.5f, 5.0f), Quaternion.Euler(0f, 0f, -s * 6f)); // dossier
                }

                // Barres verticales en laiton, loin de la table pour ne jamais masquer le jeu.
                for (float z = ZStart + 4.5f; z < ZEnd; z += 7.2f)
                    if (Mathf.Abs(z - 0.5f) > 5f) Pole(b, new Vector3(s * (WallX - 1.75f), 0f, z));

            }

            // Pas de plafond : la caméra de jeu est plus haute que le wagon (vue « maquette » ouverte).

            // Fond du wagon : paroi avec porte vitrée, encadrée de bois.
            float zEnd = ZEnd + WallThickness * 0.5f;
            const float doorHalf = 1.25f, doorTop = 3.6f;
            b.Box(0, new Vector3(-(WallX + doorHalf) * 0.5f, (floorY + CeilingY) * 0.5f, zEnd), new Vector3(WallX - doorHalf, CeilingY - floorY, WallThickness), Quaternion.identity);
            b.Box(0, new Vector3((WallX + doorHalf) * 0.5f, (floorY + CeilingY) * 0.5f, zEnd), new Vector3(WallX - doorHalf, CeilingY - floorY, WallThickness), Quaternion.identity);
            b.Box(0, new Vector3(0f, (doorTop + CeilingY) * 0.5f, zEnd), new Vector3(2f * doorHalf, CeilingY - doorTop, WallThickness), Quaternion.identity);
            b.Box(2, new Vector3(0f, (floorY + doorTop) * 0.5f, zEnd + 0.05f), new Vector3(2f * doorHalf, doorTop - floorY, 0.1f), Quaternion.identity);
            b.Box(7, new Vector3(0f, 1.9f, zEnd - 0.02f), new Vector3(1.4f, 2.4f, 0.04f), Quaternion.identity);
            b.Box(3, new Vector3(0.9f, 0.4f, zEnd - 0.08f), new Vector3(0.08f, 0.6f, 0.08f), Quaternion.identity); // poignée
            b.Box(1, new Vector3(0f, (floorY + SillY) * 0.5f, zEnd - 0.13f), new Vector3(2f * WallX, SillY - floorY, 0.02f), Quaternion.identity);

            // Petites appliques ambrées sur le haut des montants (lueur discrète).
            for (int s = -1; s <= 1; s += 2)
                for (float z = ZStart; z < ZEnd; z += 2f * (WindowLength + PillarLength))
                    b.Box(6, new Vector3(s * (WallX - 0.1f), LintelY - 0.3f, z + PillarLength * 0.5f), new Vector3(0.08f, 0.14f, 0.18f), Quaternion.identity);

            Add("Interior", b.ToMesh("Wagon_Interior"), new[] { wall, lower, wood, brass, leather, floor, lamp, glass }, true);
        }

        static void Pole(MeshBuilder b, Vector3 basePos)
        {
            const float r = 0.07f;
            b.Lathe(new List<ProfilePoint>
            {
                new ProfilePoint(0.16f, -2.4f, 0f, 3), new ProfilePoint(0.12f, -2.3f, 0f, 3), new ProfilePoint(r, -2.2f, 0f, 3),
                new ProfilePoint(r, CeilingY - 0.1f, 0f, 3), new ProfilePoint(0.14f, CeilingY, 0f, 3),
            }, 10, basePos);
        }

        // ------------------------------------------------------------------ ville

        void BuildCity()
        {
            var body = NeonMaterials.CreateLitOpaque("City_Body", new Color(0.05f, 0.06f, 0.09f), 0f, 0.2f, new Color(0.006f, 0.008f, 0.016f));
            var warm = NeonMaterials.CreateLitOpaque("City_WindowWarm", Color.black, 0f, 0.2f, new Color(1f, 0.70f, 0.36f) * 1.15f);
            var cold = NeonMaterials.CreateLitOpaque("City_WindowCold", Color.black, 0f, 0.2f, new Color(0.62f, 0.78f, 1f) * 0.85f);
            var sign = NeonMaterials.CreateLitOpaque("City_Sign", Color.black, 0f, 0.2f, new Color(1f, 0.42f, 0.32f) * 1.5f);
            var mats = new[] { body, warm, cold, sign };

            for (int s = -1; s <= 1; s += 2)
            {
                // Plan proche (rapide) et plan lointain (lent, plus haut).
                for (int layer = 0; layer < 2; layer++)
                {
                    float near = layer == 0 ? 9f : 20f;
                    float speed = layer == 0 ? CitySpeed : CitySpeed * 0.45f;
                    for (int k = 0; k < 2; k++)
                    {
                        var mesh = CitySegment(s, near, layer, 4100 + s * 17 + layer * 5 + k * 101);
                        var r = Add($"City {(s < 0 ? "L" : "R")}{layer}{k}", mesh, mats, false);
                        r.transform.localPosition = new Vector3(0f, 0f, ZStart - 6f + k * SegmentLength);
                        segments.Add(r.transform);
                        segmentSpeed.Add(speed);
                    }
                }
            }
        }

        Mesh CitySegment(int side, float near, int layer, int seed)
        {
            var r = new System.Random(seed);
            var b = new MeshBuilder(4);
            float z = 0f;
            float baseY = floorY - 6f;
            while (z < SegmentLength)
            {
                float w = 3f + (float)r.NextDouble() * 5f;        // longueur le long de la voie
                float gap = 0.4f + (float)r.NextDouble() * (layer == 0 ? 2.5f : 1f);
                float depth = 4f + (float)r.NextDouble() * 6f;
                float h = layer == 0 ? 6f + (float)r.NextDouble() * 10f : 12f + (float)r.NextDouble() * 18f;
                float inner = side * (near + (float)r.NextDouble() * 2f);
                float cx = inner + side * depth * 0.5f;
                float topY = baseY + h;
                b.Box(0, new Vector3(cx, (baseY + topY) * 0.5f, z + w * 0.5f), new Vector3(depth, h, w), Quaternion.identity);

                // Fenêtres éclairées sur la façade tournée vers le train.
                float fx = inner - side * 0.02f;
                Vector3 facing = new Vector3(-side, 0f, 0f);
                float ww = layer == 0 ? 0.42f : 0.6f, wh = layer == 0 ? 0.55f : 0.8f;
                float stepZ = ww * 2.1f, stepY = wh * 1.8f;
                for (float y = baseY + 2f; y < topY - wh; y += stepY)
                    for (float wz = z + 0.4f; wz < z + w - ww - 0.2f; wz += stepZ)
                    {
                        double roll = r.NextDouble();
                        if (roll > 0.3) continue;
                        int sub = roll < 0.21 ? 1 : 2;
                        b.QuadFacing(sub, new Vector3(fx, y, wz), new Vector3(fx, y + wh, wz), new Vector3(fx, y + wh, wz + ww), new Vector3(fx, y, wz + ww), facing);
                    }

                // De temps en temps une enseigne lumineuse au pied d'un immeuble proche.
                if (layer == 0 && r.NextDouble() < 0.3)
                {
                    float sy = floorY + 1.2f + (float)r.NextDouble() * 2f, sl = Mathf.Min(w - 0.6f, 2.2f);
                    b.QuadFacing(3, new Vector3(fx - side * 0.01f, sy, z + 0.3f), new Vector3(fx - side * 0.01f, sy + 0.35f, z + 0.3f),
                        new Vector3(fx - side * 0.01f, sy + 0.35f, z + 0.3f + sl), new Vector3(fx - side * 0.01f, sy, z + 0.3f + sl), facing);
                }
                z += w + gap;
            }
            return b.ToMesh("City_Segment");
        }

        // ------------------------------------------------------------------ bandes de lumière

        void BuildBands()
        {
            var tex = new Texture2D(32, 4, TextureFormat.RGBA32, false) { name = "Wagon_Band", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[32 * 4];
            for (int x = 0; x < 32; x++)
            {
                float u = (x + 0.5f) / 32f * 2f - 1f;
                float a = Mathf.Exp(-u * u * 5f);
                for (int y = 0; y < 4; y++) px[y * 32 + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            bandMat = NeonMaterials.CreateAdditive("Wagon_LightBand", tex, new Color(1f, 0.78f, 0.5f) * 0.9f, 5);

            const int count = 6;
            bands = new Transform[count];
            bandSpeed = new float[count];
            bandWait = new float[count];
            for (int i = 0; i < count; i++)
            {
                int s = i % 2 == 0 ? -1 : 1;
                var mesh = BandMesh(s);
                var r = Add("Light Band", mesh, new[] { bandMat }, false);
                bands[i] = r.transform;
                ResetBand(i, true);
            }
        }

        /// <summary>Quad vertical juste derrière la vitre, face à l'intérieur ; u le long de la voie.</summary>
        static Mesh BandMesh(int side)
        {
            float x = side * (WallX + WallThickness + 0.15f);
            const float half = 0.9f;
            var mesh = new Mesh { name = "Wagon_Band" };
            mesh.SetVertices(new List<Vector3> { new Vector3(x, SillY, -half), new Vector3(x, LintelY, -half), new Vector3(x, LintelY, half), new Vector3(x, SillY, half) });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
            mesh.SetTriangles(side < 0 ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void ResetBand(int i, bool initial)
        {
            bandSpeed[i] = 26f + (float)rnd.NextDouble() * 14f;
            bandWait[i] = initial ? (float)rnd.NextDouble() * 4f : 1.5f + (float)rnd.NextDouble() * 5f;
            bands[i].localPosition = new Vector3(0f, 0f, ZEnd + 4f);
            bands[i].gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ animation

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < segments.Count; i++)
            {
                var t = segments[i];
                var p = t.localPosition;
                p.z -= segmentSpeed[i] * dt;
                if (p.z < ZStart - 6f - SegmentLength) p.z += 2f * SegmentLength;
                t.localPosition = p;
            }

            for (int i = 0; i < bands.Length; i++)
            {
                if (bandWait[i] > 0f)
                {
                    bandWait[i] -= dt;
                    if (bandWait[i] <= 0f) bands[i].gameObject.SetActive(true);
                    continue;
                }
                var p = bands[i].localPosition;
                p.z -= bandSpeed[i] * dt;
                bands[i].localPosition = p;
                if (p.z < ZStart - 2f) ResetBand(i, false);
            }
        }

        MeshRenderer Add(string name, Mesh mesh, Material[] mats, bool receiveShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = receiveShadows;
            r.lightProbeUsage = LightProbeUsage.Off;
            return r;
        }
    }
}
