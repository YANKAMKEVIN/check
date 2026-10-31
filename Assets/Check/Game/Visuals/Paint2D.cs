using System.Collections.Generic;
using Check.Core;
using UnityEngine;

namespace Check.Game.Visuals
{
    /// <summary>
    /// Palette de la direction artistique « afrofuturisme néon » : indigo du ndop, ivoire, or et cuivre,
    /// vermillon des couleurs rouges, accents néon cyan / magenta / ambre.
    /// Valeurs sRGB (comme les textures générées et les couleurs de matériaux).
    /// </summary>
    public static class AfroPalette
    {
        public static readonly Color Ivory = new Color(0.955f, 0.925f, 0.845f);
        public static readonly Color IvoryShade = new Color(0.86f, 0.80f, 0.68f);
        /// <summary>« Noir » des piques et trèfles : encre indigo très sombre.</summary>
        public static readonly Color Ink = new Color(0.07f, 0.07f, 0.15f);
        /// <summary>Rouge des cœurs et carreaux.</summary>
        public static readonly Color Vermilion = new Color(0.80f, 0.08f, 0.10f);
        public static readonly Color Indigo = new Color(0.12f, 0.16f, 0.46f);
        public static readonly Color IndigoDeep = new Color(0.035f, 0.045f, 0.17f);
        public static readonly Color Gold = new Color(0.88f, 0.66f, 0.26f);
        public static readonly Color GoldLight = new Color(1.00f, 0.86f, 0.52f);
        public static readonly Color Copper = new Color(0.74f, 0.40f, 0.22f);
        public static readonly Color Bronze = new Color(0.40f, 0.24f, 0.14f);
        public static readonly Color Terracotta = new Color(0.62f, 0.13f, 0.10f);
        public static readonly Color Crimson = new Color(0.55f, 0.04f, 0.08f);
        public static readonly Color NeonCyan = new Color(0.25f, 0.95f, 1.00f);
        public static readonly Color NeonMagenta = new Color(1.00f, 0.25f, 0.80f);
        public static readonly Color NeonAmber = new Color(1.00f, 0.62f, 0.18f);
        public static readonly Color Tension = new Color(1.00f, 0.10f, 0.12f);

        /// <summary>Couleur d'impression d'une carte (rouge ou encre).</summary>
        public static Color SuitColor(CardColor c) => c == CardColor.Red ? Vermilion : Ink;
    }

    /// <summary>
    /// Petit moteur de dessin 2D en mémoire (CPU) pour générer les textures par code :
    /// formes définies par champs de distance signée (en pixels), anticrénelage par couverture.
    /// Coordonnées « design » : origine en bas à gauche, y vers le haut, décalées de (OX, OY) dans le tampon.
    /// Avec <see cref="Rot180"/>, tout le dessin est tourné de 180° autour du centre du design.
    /// </summary>
    internal sealed class Paint2D
    {
        public readonly int W, H;
        public readonly Color[] Px;
        public float OX, OY;
        /// <summary>Taille du design (pour la rotation de 180°).</summary>
        public float DW, DH;
        public bool Rot180;

        public delegate float Field(float x, float y);
        public delegate Color ColorAt(float x, float y);

        public Paint2D(int w, int h)
        {
            W = w; H = h;
            Px = new Color[w * h];
            DW = w; DH = h;
        }

        public void Fill(Color c)
        {
            for (int i = 0; i < Px.Length; i++) Px[i] = c;
        }

        public void CopyFrom(Paint2D other) => System.Array.Copy(other.Px, Px, Px.Length);

        /// <summary>Remplit une forme (champ négatif à l'intérieur).</summary>
        public void Shape(float x0, float y0, float x1, float y1, Field f, Color c, float soft = 1f)
            => Draw(x0, y0, x1, y1, f, null, c, soft, false);

        /// <summary>Remplit une forme avec une couleur calculée par pixel.</summary>
        public void Shape(float x0, float y0, float x1, float y1, Field f, ColorAt color, float soft = 1f)
            => Draw(x0, y0, x1, y1, f, color, Color.white, soft, false);

        /// <summary>Contour d'une forme : |champ| - demi-épaisseur.</summary>
        public void Stroke(float x0, float y0, float x1, float y1, Field f, float halfWidth, Color c, float soft = 1f)
        {
            float pad = halfWidth + 1f;
            Draw(x0 - pad, y0 - pad, x1 + pad, y1 + pad, (x, y) => Mathf.Abs(f(x, y)) - halfWidth, null, c, soft, false);
        }

        /// <summary>Ajout lumineux (pour les cartes d'émission) au lieu d'un mélange.</summary>
        public void AddShape(float x0, float y0, float x1, float y1, Field f, Color c, float soft = 1f)
            => Draw(x0, y0, x1, y1, f, null, c, soft, true);

        void Draw(float x0, float y0, float x1, float y1, Field f, ColorAt colorAt, Color c, float soft, bool additive)
        {
            if (Rot180)
            {
                float nx0 = DW - x1, nx1 = DW - x0, ny0 = DH - y1, ny1 = DH - y0;
                x0 = nx0; x1 = nx1; y0 = ny0; y1 = ny1;
            }
            int ix0 = Mathf.Max(0, Mathf.FloorToInt(x0 + OX - 1f));
            int iy0 = Mathf.Max(0, Mathf.FloorToInt(y0 + OY - 1f));
            int ix1 = Mathf.Min(W - 1, Mathf.CeilToInt(x1 + OX + 1f));
            int iy1 = Mathf.Min(H - 1, Mathf.CeilToInt(y1 + OY + 1f));
            float inv = 1f / Mathf.Max(0.05f, soft);
            for (int y = iy0; y <= iy1; y++)
            {
                float dy = y + 0.5f - OY;
                for (int x = ix0; x <= ix1; x++)
                {
                    float dx = x + 0.5f - OX;
                    float fx = dx, fy = dy;
                    if (Rot180) { fx = DW - dx; fy = DH - dy; }
                    float d = f(fx, fy);
                    float a = 0.5f - d * inv;
                    if (a <= 0f) continue;
                    if (a > 1f) a = 1f;
                    Color col = colorAt != null ? colorAt(fx, fy) : c;
                    int i = y * W + x;
                    if (additive)
                    {
                        float k = a * col.a;
                        Color o = Px[i];
                        Px[i] = new Color(o.r + col.r * k, o.g + col.g * k, o.b + col.b * k, 1f);
                    }
                    else Blend(i, col, a);
                }
            }
        }

        void Blend(int i, Color c, float a)
        {
            float k = a * c.a;
            Color d = Px[i];
            Px[i] = new Color(d.r + (c.r - d.r) * k, d.g + (c.g - d.g) * k, d.b + (c.b - d.b) * k, 1f);
        }

        /// <summary>Copie la moitié haute du tampon, tournée de 180°, dans la moitié basse (cartes réversibles).</summary>
        public void MirrorTopToBottom()
        {
            for (int y = 0; y < H / 2; y++)
            for (int x = 0; x < W; x++)
                Px[y * W + x] = Px[(H - 1 - y) * W + (W - 1 - x)];
        }

        // ------------------------------------------------------------------ raccourcis

        public void Circle(float cx, float cy, float r, Color c, float soft = 1f)
            => Shape(cx - r, cy - r, cx + r, cy + r, (x, y) => Sdf.Circle(x - cx, y - cy, r), c, soft);

        public void CircleStroke(float cx, float cy, float r, float hw, Color c, float soft = 1f)
            => Stroke(cx - r, cy - r, cx + r, cy + r, (x, y) => Sdf.Circle(x - cx, y - cy, r), hw, c, soft);

        public void Ellipse(float cx, float cy, float rx, float ry, Color c, float soft = 1f)
            => Shape(cx - rx, cy - ry, cx + rx, cy + ry, (x, y) => Sdf.Ellipse(x - cx, y - cy, rx, ry), c, soft);

        public void RoundRect(float x0, float y0, float x1, float y1, float r, Color c, float soft = 1f)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f, hx = (x1 - x0) * 0.5f, hy = (y1 - y0) * 0.5f;
            Shape(x0, y0, x1, y1, (x, y) => Sdf.RoundBox(x - cx, y - cy, hx, hy, r), c, soft);
        }

        public void RoundRectStroke(float x0, float y0, float x1, float y1, float r, float hw, Color c, float soft = 1f)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f, hx = (x1 - x0) * 0.5f, hy = (y1 - y0) * 0.5f;
            Stroke(x0, y0, x1, y1, (x, y) => Sdf.RoundBox(x - cx, y - cy, hx, hy, r), hw, c, soft);
        }

        /// <summary>Trait épais à bouts ronds (segment).</summary>
        public void Line(float ax, float ay, float bx, float by, float hw, Color c, float soft = 1f)
            => Shape(Mathf.Min(ax, bx) - hw, Mathf.Min(ay, by) - hw, Mathf.Max(ax, bx) + hw, Mathf.Max(ay, by) + hw,
                (x, y) => Sdf.Segment(x, y, ax, ay, bx, by) - hw, c, soft);

        /// <summary>Ligne brisée à bouts ronds.</summary>
        public void Polyline(IList<Vector2> pts, float hw, Color c, float soft = 1f, bool additive = false)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var p in pts)
            {
                x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y);
            }
            Field f = (x, y) =>
            {
                float d = float.MaxValue;
                for (int i = 0; i < pts.Count - 1; i++)
                    d = Mathf.Min(d, Sdf.Segment(x, y, pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y));
                return d - hw;
            };
            if (additive) AddShape(x0 - hw, y0 - hw, x1 + hw, y1 + hw, f, c, soft);
            else Shape(x0 - hw, y0 - hw, x1 + hw, y1 + hw, f, c, soft);
        }

        /// <summary>Polygone plein (convexe ou non).</summary>
        public void Polygon(Vector2[] pts, Color c, float soft = 1f)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var p in pts)
            {
                x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y);
            }
            Shape(x0, y0, x1, y1, (x, y) => Sdf.Polygon(pts, x, y), c, soft);
        }

        /// <summary>Losange plein centré.</summary>
        public void Rhombus(float cx, float cy, float hx, float hy, Color c, float soft = 1f)
            => Shape(cx - hx, cy - hy, cx + hx, cy + hy, (x, y) => Sdf.Rhombus(x - cx, y - cy, hx, hy), c, soft);

        public void RhombusStroke(float cx, float cy, float hx, float hy, float hw, Color c, float soft = 1f)
            => Stroke(cx - hx, cy - hy, cx + hx, cy + hy, (x, y) => Sdf.Rhombus(x - cx, y - cy, hx, hy), hw, c, soft);

        /// <summary>Halo radial doux ajouté (lueur).</summary>
        public void Glow(float cx, float cy, float r, Color c)
            => AddShape(cx - r, cy - r, cx + r, cy + r, (x, y) =>
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                return d >= 1f ? 1f : 0.5f - (1f - d) * (1f - d); // couverture = (1-d)² (voir Draw : a = 0.5 - champ)
            }, c, 1f);
    }

    /// <summary>Fonctions de distance signée 2D (négatives à l'intérieur).</summary>
    internal static class Sdf
    {
        public static float Circle(float x, float y, float r) => Mathf.Sqrt(x * x + y * y) - r;

        /// <summary>Ellipse (approximation suffisante pour du dessin).</summary>
        public static float Ellipse(float x, float y, float rx, float ry)
        {
            float k0 = Mathf.Sqrt(x * x / (rx * rx) + y * y / (ry * ry));
            if (k0 < 1e-5f) return -Mathf.Min(rx, ry);
            float k1 = Mathf.Sqrt(x * x / (rx * rx * rx * rx) + y * y / (ry * ry * ry * ry));
            return k0 * (k0 - 1f) / k1;
        }

        public static float RoundBox(float x, float y, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x) - hx + r, qy = Mathf.Abs(y) - hy + r;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        public static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float l = bax * bax + bay * bay;
            float h = l > 1e-9f ? Mathf.Clamp01((pax * bax + pay * bay) / l) : 0f;
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public static float Rhombus(float x, float y, float bx, float by)
        {
            x = Mathf.Abs(x); y = Mathf.Abs(y);
            float ndot = bx * (bx - 2f * x) - by * (by - 2f * y);
            float h = Mathf.Clamp(ndot / (bx * bx + by * by), -1f, 1f);
            float dx = x - 0.5f * bx * (1f - h), dy = y - 0.5f * by * (1f + h);
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return d * Mathf.Sign(x * by + y * bx - bx * by);
        }

        public static float Polygon(Vector2[] v, float px, float py)
        {
            float d = (px - v[0].x) * (px - v[0].x) + (py - v[0].y) * (py - v[0].y);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                float ex = v[j].x - v[i].x, ey = v[j].y - v[i].y;
                float wx = px - v[i].x, wy = py - v[i].y;
                float h = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                float bx = wx - ex * h, by = wy - ey * h;
                d = Mathf.Min(d, bx * bx + by * by);
                bool c1 = py >= v[i].y, c2 = py < v[j].y, c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        public static float SmoothMin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        // ------------------------------------------------------------------ couleurs de cartes
        // Espace unité : centré, y vers le haut, la forme tient dans [-0.5, 0.5]².

        static readonly Vector2[] HeartPoly =
        {
            new Vector2(0f, -0.47f), new Vector2(0.404f, -0.004f), new Vector2(0.215f, 0.16f),
            new Vector2(0f, 0.16f), new Vector2(-0.215f, 0.16f), new Vector2(-0.404f, -0.004f),
        };
        static readonly Vector2[] SpadeStem = { new Vector2(0f, 0.02f), new Vector2(0.15f, -0.5f), new Vector2(-0.15f, -0.5f) };
        static readonly Vector2[] ClubStem = { new Vector2(0f, 0.04f), new Vector2(0.14f, -0.5f), new Vector2(-0.14f, -0.5f) };

        static float Heart(float x, float y)
        {
            float c = Mathf.Min(Circle(x - 0.215f, y - 0.16f, 0.25f), Circle(x + 0.215f, y - 0.16f, 0.25f));
            return SmoothMin(c, Polygon(HeartPoly, x, y), 0.012f);
        }

        public static float Suit(Suit suit, float x, float y)
        {
            switch (suit)
            {
                case Core.Suit.Hearts:
                    return Heart(x, y - 0.03f);
                case Core.Suit.Diamonds:
                    return Rhombus(x, y, 0.37f, 0.5f);
                case Core.Suit.Spades:
                {
                    const float s = 0.86f;
                    float body = Heart(x / s, -(y - 0.1f) / s) * s;
                    return SmoothMin(body, Polygon(SpadeStem, x, y), 0.03f);
                }
                default:
                {
                    float c = Mathf.Min(Circle(x, y - 0.26f, 0.205f),
                        Mathf.Min(Circle(x - 0.225f, y + 0.04f, 0.205f), Circle(x + 0.225f, y + 0.04f, 0.205f)));
                    c = Mathf.Min(c, Circle(x, y + 0.02f, 0.12f));
                    return SmoothMin(c, Polygon(ClubStem, x, y), 0.03f);
                }
            }
        }
    }

    /// <summary>
    /// Police géométrique dessinée par code (traits épais à bouts ronds), pour les index des cartes.
    /// Glyphe dans une boîte de 0.6 × 1 (y vers le haut).
    /// </summary>
    internal static class StrokeFont
    {
        public const float GlyphWidth = 0.6f;
        static readonly Dictionary<char, Vector2[][]> Glyphs = Build();

        public static bool Has(char c) => Glyphs.ContainsKey(c);

        static Vector2[] Arc(float cx, float cy, float rx, float ry, float deg0, float deg1, int n = 14)
        {
            var pts = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(deg0, deg1, i / (float)n) * Mathf.Deg2Rad;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
            }
            return pts;
        }

        static Vector2[] Join(params Vector2[][] parts)
        {
            var list = new List<Vector2>();
            foreach (var p in parts) list.AddRange(p);
            return list.ToArray();
        }

        static Vector2[] P(params float[] xy)
        {
            var pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return pts;
        }

        static Dictionary<char, Vector2[][]> Build()
        {
            var g = new Dictionary<char, Vector2[][]>();
            // Chiffres : géométriques, ronds, très gras.
            g['0'] = new[] { Join(Arc(0.3f, 0.70f, 0.27f, 0.30f, 0f, 180f), Arc(0.3f, 0.30f, 0.27f, 0.30f, 180f, 360f), P(0.57f, 0.70f)) };
            g['1'] = new[] { P(0.10f, 0.80f, 0.36f, 1.0f, 0.36f, 0.0f) };
            g['2'] = new[] { Join(Arc(0.3f, 0.72f, 0.26f, 0.27f, 165f, -38f), P(0.04f, 0.0f, 0.58f, 0.0f)) };
            g['3'] = new[] { Join(P(0.06f, 1.0f, 0.52f, 1.0f, 0.26f, 0.60f), Arc(0.29f, 0.31f, 0.28f, 0.30f, 75f, -160f)) };
            g['4'] = new[] { P(0.46f, 0.0f, 0.46f, 1.0f, 0.02f, 0.30f, 0.60f, 0.30f) };
            g['5'] = new[] { Join(P(0.55f, 1.0f, 0.10f, 1.0f, 0.07f, 0.53f), Arc(0.29f, 0.31f, 0.29f, 0.31f, 135f, -150f)) };
            g['6'] = new[] { Arc(0.3f, 0.30f, 0.27f, 0.30f, 0f, 360f, 20), P(0.48f, 1.0f, 0.06f, 0.36f) };
            g['7'] = new[] { P(0.02f, 1.0f, 0.58f, 1.0f, 0.18f, 0.0f) };
            g['8'] = new[] { Arc(0.3f, 0.77f, 0.22f, 0.22f, 0f, 360f, 18), Arc(0.3f, 0.28f, 0.27f, 0.28f, 0f, 360f, 20) };
            g['9'] = new[] { Arc(0.3f, 0.70f, 0.27f, 0.30f, 0f, 360f, 20), P(0.12f, 0.0f, 0.54f, 0.64f) };
            // Lettres des figures (V D R), de l'as, du joker et du dos (CHECK).
            g['A'] = new[] { P(0.02f, 0.0f, 0.3f, 1.0f, 0.58f, 0.0f), P(0.13f, 0.34f, 0.47f, 0.34f) };
            g['V'] = new[] { P(0.02f, 1.0f, 0.3f, 0.0f, 0.58f, 1.0f) };
            g['D'] = new[] { Join(P(0.21f, 0.0f, 0.07f, 0.0f, 0.07f, 1.0f, 0.21f, 1.0f), Arc(0.21f, 0.5f, 0.36f, 0.5f, 90f, -90f, 18)) };
            g['R'] = new[] { Join(P(0.07f, 0.0f, 0.07f, 1.0f, 0.30f, 1.0f), Arc(0.30f, 0.75f, 0.26f, 0.25f, 90f, -90f), P(0.07f, 0.5f)), P(0.30f, 0.5f, 0.58f, 0.0f) };
            g['J'] = new[] { Join(P(0.28f, 1.0f, 0.56f, 1.0f, 0.56f, 0.30f), Arc(0.31f, 0.30f, 0.25f, 0.30f, 0f, -180f), P(0.06f, 0.36f)) };
            g['O'] = new[] { Arc(0.3f, 0.5f, 0.27f, 0.5f, 0f, 360f, 24) };
            g['K'] = new[] { P(0.07f, 0.0f, 0.07f, 1.0f), P(0.56f, 1.0f, 0.07f, 0.38f), P(0.25f, 0.60f, 0.58f, 0.0f) };
            g['E'] = new[] { P(0.55f, 1.0f, 0.07f, 1.0f, 0.07f, 0.0f, 0.55f, 0.0f), P(0.07f, 0.5f, 0.45f, 0.5f) };
            g['C'] = new[] { Arc(0.33f, 0.5f, 0.29f, 0.5f, 48f, 312f, 22) };
            g['H'] = new[] { P(0.07f, 0.0f, 0.07f, 1.0f), P(0.53f, 0.0f, 0.53f, 1.0f), P(0.07f, 0.5f, 0.53f, 0.5f) };
            return g;
        }

        /// <summary>Largeur d'un texte (en pixels) pour une hauteur donnée.</summary>
        public static float Measure(string text, float height, float widthScale, float spacing = 0.14f)
        {
            float adv = (GlyphWidth + spacing) * height * widthScale;
            return text.Length * adv - spacing * height * widthScale;
        }

        /// <summary>Dessine un texte : (x, baseline) = coin bas gauche de la boîte du premier glyphe.</summary>
        public static void Draw(Paint2D p, string text, float x, float baseline, float height, float widthScale, float weight, Color c, float spacing = 0.14f)
        {
            float hw = weight * height;
            var segs = new List<Vector4>();
            float cursor = x;
            foreach (char ch in text)
            {
                if (Glyphs.TryGetValue(ch, out var strokes))
                    foreach (var s in strokes)
                        for (int i = 0; i < s.Length - 1; i++)
                            segs.Add(new Vector4(cursor + s[i].x * height * widthScale, baseline + s[i].y * height,
                                cursor + s[i + 1].x * height * widthScale, baseline + s[i + 1].y * height));
                cursor += (GlyphWidth + spacing) * height * widthScale;
            }
            if (segs.Count == 0) return;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var s in segs)
            {
                x0 = Mathf.Min(x0, Mathf.Min(s.x, s.z)); x1 = Mathf.Max(x1, Mathf.Max(s.x, s.z));
                y0 = Mathf.Min(y0, Mathf.Min(s.y, s.w)); y1 = Mathf.Max(y1, Mathf.Max(s.y, s.w));
            }
            var arr = segs.ToArray();
            p.Shape(x0 - hw, y0 - hw, x1 + hw, y1 + hw, (px, py) =>
            {
                float d = float.MaxValue;
                for (int i = 0; i < arr.Length; i++)
                {
                    var s = arr[i];
                    float dd = Sdf.Segment(px, py, s.x, s.y, s.z, s.w);
                    if (dd < d) d = dd;
                }
                return d - hw;
            }, c);
        }
    }
}
