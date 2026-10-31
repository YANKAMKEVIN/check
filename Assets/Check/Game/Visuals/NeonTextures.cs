using UnityEngine;

namespace Check.Game.Visuals
{
    /// <summary>
    /// Petites textures générées par code (halo, anneau, ciel de crépuscule).
    /// Toutes sont mises en cache et créées à la première demande.
    /// </summary>
    public static class NeonTextures
    {
        static Texture2D _radial, _ring;
        static Cubemap _sky;

        /// <summary>Halo radial doux (blanc, alpha dégressif).</summary>
        public static Texture2D RadialGlow => _radial != null ? _radial : (_radial = Make("Check_RadialGlow", 64, (u, v) =>
        {
            float d = Mathf.Clamp01(Dist(u, v) * 2f);
            float a = (1f - d);
            return a * a * (0.65f + 0.35f * a);
        }));

        /// <summary>Anneau fin lumineux avec un halo léger.</summary>
        public static Texture2D Ring => _ring != null ? _ring : (_ring = Make("Check_Ring", 128, (u, v) =>
        {
            float d = Dist(u, v) * 2f; // 0 au centre, 1 au bord
            float core = Mathf.Exp(-Sq((d - 0.74f) / 0.06f));
            float halo = Mathf.Exp(-Sq((d - 0.74f) / 0.18f)) * 0.35f;
            return Mathf.Clamp01(core + halo) * (d < 1f ? 1f : 0f);
        }));

        /// <summary>
        /// Ciel de crépuscule afrofuturiste (cubemap) : zénith indigo, horizon magenta / ambre, soleil cerclé,
        /// volutes et étoiles. Sert de skybox et de réflexion d'environnement (verre, cuivre).
        /// </summary>
        public static Cubemap NebulaSky
        {
            get
            {
                if (_sky != null) return _sky;
                const int size = 128;
                var cube = new Cubemap(size, TextureFormat.RGBA32, true) { name = "Check_NebulaSky", filterMode = FilterMode.Trilinear };
                var px = new Color[size * size];
                for (int f = 0; f < 6; f++)
                {
                    var face = (CubemapFace)f;
                    for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float sc = (x + 0.5f) / size * 2f - 1f;
                        float tc = (y + 0.5f) / size * 2f - 1f; // ligne 0 = haut de la face
                        Vector3 d = FaceDirection(face, sc, tc).normalized;
                        px[y * size + x] = SkyColor(d, f * 7919 + y * size + x);
                    }
                    cube.SetPixels(px, face);
                }
                cube.Apply(true, false);
                _sky = cube;
                return cube;
            }
        }

        static Vector3 FaceDirection(CubemapFace face, float sc, float tc)
        {
            switch (face)
            {
                case CubemapFace.PositiveX: return new Vector3(1f, -tc, -sc);
                case CubemapFace.NegativeX: return new Vector3(-1f, -tc, sc);
                case CubemapFace.PositiveY: return new Vector3(sc, 1f, tc);
                case CubemapFace.NegativeY: return new Vector3(sc, -1f, -tc);
                case CubemapFace.PositiveZ: return new Vector3(sc, -tc, 1f);
                default: return new Vector3(-sc, -tc, -1f);
            }
        }

        static readonly Color SkyLow = new Color(0.006f, 0.004f, 0.014f);
        static readonly Color SkyHorizon = new Color(0.16f, 0.045f, 0.14f);
        static readonly Color SkyGlow = new Color(0.55f, 0.22f, 0.08f);
        static readonly Color SkyHigh = new Color(0.008f, 0.012f, 0.050f);
        static readonly Color NebulaCyan = new Color(0.04f, 0.22f, 0.36f);
        static readonly Color NebulaMagenta = new Color(0.34f, 0.05f, 0.30f);
        static readonly Vector3 SunDir = new Vector3(0.18f, 0.16f, 1f).normalized;

        // Crépuscule afrofuturiste : zénith indigo, bande d'horizon magenta / ambre,
        // soleil cerclé d'anneaux (inventé) au fond de la scène, volutes et étoiles.
        static Color SkyColor(Vector3 d, int seed)
        {
            float h = d.y;
            Color c = h > 0f
                ? Color.Lerp(SkyHorizon, SkyHigh, Mathf.Pow(Mathf.Clamp01(h * 2.2f), 0.55f))
                : Color.Lerp(SkyHorizon, SkyLow, Mathf.Sqrt(Mathf.Clamp01(-h * 4f)));
            // Lueur ambrée rasante, plus forte du côté du soleil.
            float toward = Mathf.Clamp01(Vector3.Dot(new Vector3(d.x, 0f, d.z).normalized, new Vector3(SunDir.x, 0f, SunDir.z).normalized) * 0.5f + 0.5f);
            c += SkyGlow * (Mathf.Exp(-Mathf.Abs(h) * 14f) * (0.25f + 0.75f * toward * toward));

            float n1 = Fbm(d * 2.2f + new Vector3(3.1f, 0.7f, 5.3f));
            float n2 = Fbm(d * 3.1f + new Vector3(-4.2f, 2.9f, 1.1f));
            float upper = Mathf.Clamp01(h * 3f + 0.2f);
            c += NebulaCyan * (Mathf.Pow(Mathf.Clamp01(n1 * 1.6f - 0.45f), 2.2f) * upper);
            c += NebulaMagenta * (Mathf.Pow(Mathf.Clamp01(n2 * 1.6f - 0.45f), 2.2f) * upper);

            // Soleil cerclé : disque ambré, halo, deux anneaux fins.
            float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, SunDir), -1f, 1f));
            c += new Color(1f, 0.62f, 0.25f) * (Mathf.Exp(-ang * ang / 0.0016f) * 1.4f + Mathf.Exp(-ang / 0.09f) * 0.22f);
            c += new Color(1f, 0.45f, 0.75f) * (Mathf.Exp(-Sq((ang - 0.085f) / 0.006f)) * 0.55f);
            c += new Color(0.35f, 0.8f, 1f) * (Mathf.Exp(-Sq((ang - 0.12f) / 0.005f)) * 0.35f);

            uint hash = Hash((uint)seed);
            float r = (hash & 0xFFFF) / 65535f;
            if (h > 0.05f && r > 0.9965f)
            {
                float b = ((hash >> 16) & 0xFF) / 255f;
                c += Color.Lerp(new Color(0.6f, 0.75f, 1f), new Color(1f, 0.85f, 0.7f), b) * (0.35f + 0.65f * b) * Mathf.Clamp01(h * 4f);
            }
            c.a = 1f;
            return c;
        }

        static float Fbm(Vector3 p)
        {
            float sum = 0f, amp = 0.5f;
            for (int i = 0; i < 4; i++)
            {
                sum += amp * Noise3(p);
                p = p * 2.03f + new Vector3(1.7f, 9.2f, 3.4f);
                amp *= 0.5f;
            }
            return sum / 0.9375f;
        }

        // Bruit 3D approximatif à partir de trois bruits de Perlin 2D (suffisant pour un ciel).
        static float Noise3(Vector3 p)
        {
            float a = Mathf.PerlinNoise(p.x + 17.3f, p.y + 4.1f);
            float b = Mathf.PerlinNoise(p.y + 31.7f, p.z + 8.9f);
            float c = Mathf.PerlinNoise(p.z + 2.3f, p.x + 13.7f);
            return (a + b + c) / 3f;
        }

        static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352d;
            x ^= x >> 15; x *= 0x846ca68b;
            x ^= x >> 16;
            return x;
        }

        // ------------------------------------------------------------------ utilitaires

        delegate float AlphaFunc(float u, float v);

        static Texture2D Make(string name, int size, AlphaFunc alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = Mathf.Clamp01(alpha((x + 0.5f) / size, (y + 0.5f) / size));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(true, true); // non lisible ensuite : économise la mémoire CPU
            return tex;
        }

        static float Dist(float u, float v) => Mathf.Sqrt(Sq(u - 0.5f) + Sq(v - 0.5f));
        static float Sq(float x) => x * x;
    }
}
