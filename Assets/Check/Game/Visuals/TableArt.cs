using System.Collections.Generic;
using UnityEngine;

namespace Check.Game.Visuals
{
    /// <summary>
    /// Textures générées par code pour la table et le décor : motifs lumineux du plateau (wax / ndop /
    /// circuits), emplacements de pioche et de tas, tube néon de la bordure, sol, vignette de tension.
    /// Chaque texture est créée une fois puis rendue non lisible (pas de copie CPU).
    /// </summary>
    internal static class TableArt
    {
        /// <summary>Exposant de la super-ellipse du plateau (2 = ellipse, plus grand = plus carré).</summary>
        public const float Squareness = 3.2f;
        /// <summary>Pixels par unité monde de la texture du plateau.</summary>
        const float SurfacePpu = 100f;

        public static Vector2 SuperEllipse(float t, float a, float b)
        {
            float c = Mathf.Cos(t), s = Mathf.Sin(t);
            float e = 2f / Squareness;
            return new Vector2(a * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e), b * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e));
        }

        /// <summary>« Rayon » normalisé d'un point (1 = sur le bord de la super-ellipse).</summary>
        public static float Rho(float x, float z, float a, float b)
            => Mathf.Pow(Mathf.Pow(Mathf.Abs(x / a), Squareness) + Mathf.Pow(Mathf.Abs(z / b), Squareness), 1f / Squareness);

        /// <summary>Angle paramétrique approché d'un point (pour placer une lueur sur le bord).</summary>
        public static float Param(float x, float z, float a, float b)
        {
            float u = x / a, v = z / b;
            // Inverse de la paramétrisation : |cos t|^(2/n) = |u| → cos t ≈ sign(u)|u|^(n/2).
            float cu = Mathf.Sign(u) * Mathf.Pow(Mathf.Abs(u), Squareness * 0.5f);
            float sv = Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), Squareness * 0.5f);
            return Mathf.Atan2(sv, cu);
        }

        // ------------------------------------------------------------------ plateau

        /// <summary>
        /// Carte d'émission du plateau (couleurs) : treillis wax discret, médaillon central autour de
        /// la pioche et du tas, pistes de circuit vers chaque place, bande en dents de scie au bord,
        /// symboles inventés façon adinkra.
        /// </summary>
        public static Texture2D Surface(float a, float b, Vector3 drawPile, Vector3 discard)
            => Finish(SurfacePaint(a, b, drawPile, discard), "Check_TableSurface", TextureWrapMode.Clamp);

        public static Paint2D SurfacePaint(float a, float b, Vector3 drawPile, Vector3 discard)
        {
            int w = Mathf.RoundToInt(2f * a * SurfacePpu), h = Mathf.RoundToInt(2f * b * SurfacePpu);
            var p = new Paint2D(w, h);
            p.Fill(Color.black);
            // Conversions monde → pixels.
            float PX(float x) => (x + a) * SurfacePpu;
            float PZ(float z) => (z + b) * SurfacePpu;
            float S(float d) => d * SurfacePpu;

            Color lattice = new Color(0.20f, 0.26f, 0.78f);
            Color gold = new Color(1.0f, 0.62f, 0.20f);
            Color cyan = new Color(0.20f, 0.85f, 1.0f);
            Color magenta = new Color(1.0f, 0.25f, 0.75f);
            var mid = new Vector2(PX((drawPile.x + discard.x) * 0.5f), PZ((drawPile.z + discard.z) * 0.5f));

            // Treillis de losanges (wax), qui s'efface au centre et près du bord.
            float cell = S(0.62f);
            p.Shape(0f, 0f, w, h, (x, y) =>
            {
                float u = (x + y) * 0.7071f, v = (x - y) * 0.7071f;
                float du = Mathf.Abs(Mathf.Repeat(u, cell) - cell * 0.5f), dv = Mathf.Abs(Mathf.Repeat(v, cell) - cell * 0.5f);
                float line = cell * 0.5f - Mathf.Max(du, dv) - 0.9f;
                // Petit losange plein au centre de chaque maille.
                float dot = du + dv - S(0.035f);
                return Mathf.Min(line, dot);
            }, (x, y) =>
            {
                float wx = x / SurfacePpu - a, wz = y / SurfacePpu - b;
                float rho = Rho(wx, wz, a, b);
                float dc = Vector2.Distance(new Vector2(x, y), mid) / SurfacePpu;
                float k = Mathf.Clamp01((dc - 1.9f) / 0.8f) * Mathf.Clamp01((0.86f - rho) / 0.12f);
                return new Color(lattice.r, lattice.g, lattice.b, 0.42f * k);
            });

            // Médaillon central (soleil adinkra) autour de la pioche et du tas.
            p.CircleStroke(mid.x, mid.y, S(1.30f), 1.0f, new Color(cyan.r, cyan.g, cyan.b, 0.55f));
            p.CircleStroke(mid.x, mid.y, S(1.45f), 1.6f, gold);
            p.CircleStroke(mid.x, mid.y, S(1.53f), 0.9f, new Color(gold.r, gold.g, gold.b, 0.7f));
            for (int i = 0; i < 36; i++)
            {
                float ang = i * Mathf.PI * 2f / 36f;
                var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                var n = new Vector2(-d.y, d.x);
                Vector2 c0 = mid + d * S(1.60f), c1 = mid + d * S(1.78f);
                p.Polygon(new[] { c0 + n * S(0.07f), c1, c0 - n * S(0.07f) }, new Color(gold.r, gold.g, gold.b, i % 2 == 0 ? 0.85f : 0.45f));
                Vector2 t0 = mid + d * S(1.36f), t1 = mid + d * S(1.41f);
                p.Line(t0.x, t0.y, t1.x, t1.y, 0.9f, new Color(cyan.r, cyan.g, cyan.b, 0.7f));
            }

            // Pistes de circuit vers les quatre côtés (places des joueurs) et les diagonales.
            var rnd = new System.Random(7);
            for (int k = 0; k < 8; k++)
            {
                float ang = k * Mathf.PI * 0.25f + (k % 2 == 1 ? 0.18f : 0f);
                var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                var pts = new List<Vector2> { mid + d * S(1.84f) };
                Vector2 cur = pts[0];
                float len = k % 2 == 0 ? 0.9f : 0.6f;
                // Segments à 45° façon circuit imprimé.
                for (int s = 0; s < 3; s++)
                {
                    float turn = (s % 2 == 1 ? 1 : 0) * (rnd.Next(2) == 0 ? 0.785f : -0.785f);
                    var dir = new Vector2(Mathf.Cos(ang + turn), Mathf.Sin(ang + turn));
                    cur += dir * S(len * (0.5f + 0.25f * s));
                    pts.Add(cur);
                }
                float ex = cur.x / SurfacePpu - a, ez = cur.y / SurfacePpu - b;
                if (Rho(ex, ez, a, b) > 0.86f) continue;
                p.Polyline(pts, 1.3f, new Color(cyan.r, cyan.g, cyan.b, 0.85f));
                p.CircleStroke(cur.x, cur.y, S(0.05f), 1.3f, k % 3 == 0 ? magenta : cyan);
                p.Circle(cur.x, cur.y, S(0.018f), gold);
            }

            // Symboles inventés (losange aux cornes spiralées) sur les diagonales.
            for (int k = 0; k < 4; k++)
            {
                float ang = (k + 0.5f) * Mathf.PI * 0.5f;
                Vector2 e = SuperEllipse(ang, a, b) * 0.70f;
                Glyph(p, new Vector2(PX(e.x), PZ(e.y)), S(0.22f), gold, magenta);
            }

            // Frise de losanges (ndop) qui suit le bord, espacée régulièrement en longueur d'arc.
            {
                const int samples = 2048;
                var pts = new Vector2[samples + 1];
                for (int i = 0; i <= samples; i++) pts[i] = SuperEllipse(i * Mathf.PI * 2f / samples, a, b) * 0.925f;
                float acc = 0f, step = 0.16f;
                int k = 0;
                for (int i = 1; i <= samples; i++)
                {
                    acc += Vector2.Distance(pts[i], pts[i - 1]);
                    if (acc < step) continue;
                    acc -= step;
                    Vector2 c = pts[i], t = pts[i] - pts[i - 1];
                    float len = Mathf.Sqrt(t.x * t.x + t.y * t.y);
                    t = t * (1f / Mathf.Max(1e-5f, len));
                    var n = new Vector2(t.y, -t.x);
                    float along = k % 2 == 0 ? 0.065f : 0.04f, across = k % 2 == 0 ? 0.045f : 0.028f;
                    var poly = new[] { c + t * along, c + n * across, c - t * along, c - n * across };
                    for (int q = 0; q < 4; q++) poly[q] = new Vector2(PX(poly[q].x), PZ(poly[q].y));
                    p.Polygon(poly, new Color(gold.r, gold.g, gold.b, k % 2 == 0 ? 0.75f : 0.45f));
                    k++;
                }
            }
            p.Shape(0f, 0f, w, h, (x, y) =>
            {
                float wx = x / SurfacePpu - a, wz = y / SurfacePpu - b;
                float rho = Rho(wx, wz, a, b);
                return Mathf.Abs(rho - 0.962f) * a * SurfacePpu - 0.9f;
            }, new Color(cyan.r, cyan.g, cyan.b, 0.8f));

            return p;
        }

        static void Glyph(Paint2D p, Vector2 c, float r, Color gold, Color accent)
        {
            p.RhombusStroke(c.x, c.y, r * 0.35f, r * 0.5f, 1.3f, gold);
            p.Rhombus(c.x, c.y, r * 0.12f, r * 0.18f, accent);
            for (int k = 0; k < 4; k++)
            {
                float a0 = k * Mathf.PI * 0.5f + Mathf.PI * 0.5f;
                var dir = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                float vr = k % 2 == 0 ? r * 0.5f : r * 0.35f;
                Vector2 vtx = c + dir * vr, sc = c + dir * (vr + r * 0.3f);
                var pts = new List<Vector2> { vtx };
                float startA = Mathf.Atan2(-dir.y, -dir.x);
                for (int t = 0; t <= 20; t++)
                {
                    float u = t / 20f;
                    float ang = startA - u * Mathf.PI * 3f;
                    pts.Add(sc + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (r * 0.28f * (1f - u * 0.8f)));
                }
                p.Polyline(pts, 1.2f, gold);
            }
            p.CircleStroke(c.x, c.y, r * 1.25f, 0.8f, new Color(gold.r, gold.g, gold.b, 0.5f));
        }

        // ------------------------------------------------------------------ décalques additifs (alpha)

        /// <summary>Emplacement de carte : contour arrondi, crochets d'angle, remplissage léger, graduations.</summary>
        public static Texture2D Slot(float width, float height, float radius, float margin)
        {
            const int tw = 128;
            int th = Mathf.RoundToInt(tw * (height + 2f * margin) / (width + 2f * margin));
            float qw = width + 2f * margin, qh = height + 2f * margin;
            return MakeAlpha("Check_Slot", tw, th, (u, v) =>
            {
                float x = (u - 0.5f) * qw, z = (v - 0.5f) * qh;
                float d = Sdf.RoundBox(x, z, width * 0.5f + 0.02f, height * 0.5f + 0.02f, radius + 0.02f);
                float line = Mathf.Exp(-d * d / (0.006f * 0.006f)) * 0.75f;
                float halo = Mathf.Exp(-Mathf.Abs(d) / 0.03f) * 0.25f;
                // Crochets d'angle plus épais.
                float ax = Mathf.Abs(x) - width * 0.5f, az = Mathf.Abs(z) - height * 0.5f;
                bool corner = ax > -0.12f && az > -0.12f;
                float bracket = corner ? Mathf.Exp(-(d + 0.035f) * (d + 0.035f) / (0.010f * 0.010f)) : 0f;
                float fill = d < 0f ? 0.07f : 0f;
                // Graduations sur les grands côtés.
                float tick = 0f;
                if (Mathf.Abs(x) > width * 0.5f + 0.045f && Mathf.Abs(x) < width * 0.5f + 0.075f)
                {
                    float m = Mathf.Abs(Mathf.Repeat(z / 0.1f, 1f) - 0.5f);
                    tick = m < 0.1f && Mathf.Abs(z) < height * 0.4f ? 0.6f : 0f;
                }
                return Mathf.Clamp01(line + halo + bracket + fill + tick);
            });
        }

        /// <summary>Profil transversal du tube néon (u le long du tube, v en travers).</summary>
        public static Texture2D NeonBand() => MakeAlpha("Check_NeonBand", 8, 64, (u, v) =>
        {
            float d = v - 0.5f;
            return Mathf.Clamp01(Mathf.Exp(-d * d / 0.0035f) + Mathf.Exp(-d * d / 0.03f) * 0.35f);
        }, TextureWrapMode.Repeat);

        /// <summary>Vignette de tension : rouge surtout vers le bord du plateau.</summary>
        public static Texture2D TensionVignette() => MakeAlpha("Check_TensionVignette", 128, 128, (u, v) =>
        {
            float x = (u - 0.5f) * 2f, z = (v - 0.5f) * 2f;
            float r = Mathf.Pow(Mathf.Pow(Mathf.Abs(x), Squareness) + Mathf.Pow(Mathf.Abs(z), Squareness), 1f / Squareness);
            if (r > 1f) return 0f;
            return Mathf.Clamp01(Mathf.Pow(r, 3f) * 0.9f + 0.08f) * Mathf.Clamp01((1f - r) * 25f);
        });

        // ------------------------------------------------------------------ sol

        /// <summary>Sol lointain : anneaux concentriques, rayons et bande de losanges, très sombres.</summary>
        public static Texture2D Floor(float radius)
        {
            const int size = 512;
            var p = new Paint2D(size, size);
            p.Fill(Color.black);
            float ppu = size / (2f * radius);
            float c = size * 0.5f;
            Color indigo = new Color(0.22f, 0.20f, 0.70f);
            Color amber = new Color(0.9f, 0.45f, 0.15f);
            Color cyan = new Color(0.15f, 0.6f, 0.9f);
            for (int i = 1; i < 12; i++)
            {
                float r = (3.5f + i * 2.6f) * ppu;
                p.CircleStroke(c, c, r, i % 3 == 0 ? 1.4f : 0.8f, i % 3 == 0 ? amber * 0.8f : indigo * 0.7f);
            }
            for (int k = 0; k < 24; k++)
            {
                float ang = k * Mathf.PI / 12f;
                float r0 = 6f * ppu, r1 = radius * 0.95f * ppu;
                p.Line(c + Mathf.Cos(ang) * r0, c + Mathf.Sin(ang) * r0, c + Mathf.Cos(ang) * r1, c + Mathf.Sin(ang) * r1, 0.7f, indigo * 0.5f);
            }
            // Bande de losanges (ndop) sur un anneau.
            float rb = 14.5f * ppu;
            for (int k = 0; k < 64; k++)
            {
                float ang = k * Mathf.PI * 2f / 64f;
                p.RhombusStroke(c + Mathf.Cos(ang) * rb, c + Mathf.Sin(ang) * rb, 0.45f * ppu, 0.45f * ppu, 0.8f, k % 2 == 0 ? cyan * 0.8f : amber * 0.6f);
            }
            // Atténuation radiale (le brouillard fait le reste).
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float k = Mathf.Clamp01(1.15f - d);
                int i = y * size + x;
                p.Px[i] = p.Px[i] * k;
            }
            return Finish(p, "Check_Floor", TextureWrapMode.Clamp);
        }

        // ------------------------------------------------------------------ utilitaires

        static Texture2D Finish(Paint2D p, string name, TextureWrapMode wrap)
        {
            var tex = new Texture2D(p.W, p.H, TextureFormat.RGBA32, true, false)
            {
                name = name, wrapMode = wrap, filterMode = FilterMode.Trilinear, anisoLevel = 8,
            };
            var px = new Color32[p.W * p.H];
            for (int i = 0; i < px.Length; i++)
            {
                Color c = p.Px[i];
                c.a = 1f;
                px[i] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        delegate float AlphaFunc(float u, float v);

        static Texture2D MakeAlpha(string name, int w, int h, AlphaFunc f, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, wrapMode = wrap, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = Mathf.Clamp01(f((x + 0.5f) / w, (y + 0.5f) / h));
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}
