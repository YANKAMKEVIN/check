using System.Collections.Generic;
using Check.Core;
using UnityEngine;

namespace Check.Game.Visuals
{
    /// <summary>
    /// Dessin par code des faces et du dos des cartes (aucune texture importée).
    /// Face : 200 × 280 px de design (+ 2 px de fond perdu), ivoire, index géants dans deux coins,
    /// points classiques, figures V / D / R et jokers illustrés de motifs géométriques afrofuturistes.
    /// Dos : ndop indigo et blanc, médaillon spiralé doré et pistes de circuit néon.
    /// </summary>
    internal static class CardArt
    {
        public const int Bleed = 2;
        public const float DW = 200f, DH = 280f;
        public const int CellW = 204, CellH = 284;
        public const int BackW = 256, BackH = 358;

        static Paint2D _base;

        public static Paint2D NewCell() => new Paint2D(CellW, CellH) { OX = Bleed, OY = Bleed, DW = DW, DH = DH };

        // ------------------------------------------------------------------ face

        public static void PaintFace(Paint2D p, Card card)
        {
            p.Rot180 = false;
            if (card.IsJoker)
            {
                PaintJoker(p, card.Id == Card.RedJokerId);
                return;
            }
            if (_base == null)
            {
                _base = NewCell();
                PaintBase(_base);
            }
            p.CopyFrom(_base);

            Rank rank = card.Rank;
            Suit suit = card.Suit;
            Color ink = AfroPalette.SuitColor(card.Color);
            string label = Card.RankLabel(rank);

            if (rank == Rank.Jack || rank == Rank.Queen || rank == Rank.King)
            {
                // Figure réversible : on dessine la moitié haute (et l'index haut gauche) puis on la reflète.
                PaintFigure(p, rank, suit, card.Color);
                PaintIndex(p, label, suit, ink);
                p.MirrorTopToBottom();
                return;
            }

            if (rank == Rank.Ace) PaintAce(p, suit, ink);
            else PaintPips(p, (int)rank, suit, ink);
            PaintIndex(p, label, suit, ink);
            p.Rot180 = true;
            PaintIndex(p, label, suit, ink);
            p.Rot180 = false;
        }

        static void PaintBase(Paint2D p)
        {
            p.Fill(AfroPalette.Ivory);
            // Bords légèrement plus chauds (papier).
            Color shade = AfroPalette.IvoryShade;
            p.Shape(-2f, -2f, DW + 2f, DH + 2f, (x, y) => -1f, (x, y) =>
            {
                float dx = (x - 100f) / 100f, dy = (y - 140f) / 140f;
                float r = Mathf.Clamp01(dx * dx * 0.8f + dy * dy * 0.6f);
                return new Color(shade.r, shade.g, shade.b, 0.35f * r * r);
            });
            // Filigrane en losanges (wax) très discret.
            const float s = 18f;
            Color water = new Color(shade.r * 0.95f, shade.g * 0.92f, shade.b * 0.9f, 0.20f);
            p.Shape(12f, 12f, DW - 12f, DH - 12f, (x, y) =>
            {
                float u = (x + y) * 0.7071f, v = (x - y) * 0.7071f;
                float du = Mathf.Abs(Mathf.Repeat(u, s) - s * 0.5f), dv = Mathf.Abs(Mathf.Repeat(v, s) - s * 0.5f);
                float line = s * 0.5f - Mathf.Max(du, dv);
                return Mathf.Max(line - 0.55f, Sdf.RoundBox(x - 100f, y - 140f, 86f, 126f, 6f));
            }, water);
            // Cadre or + filet indigo.
            p.RoundRectStroke(7f, 7f, DW - 7f, DH - 7f, 9f, 1.05f, AfroPalette.Gold);
            p.RoundRectStroke(11f, 11f, DW - 11f, DH - 11f, 6f, 0.5f, new Color(AfroPalette.Indigo.r, AfroPalette.Indigo.g, AfroPalette.Indigo.b, 0.6f));
            // Ornement de coin (arcs concentriques façon wax) en haut à droite, et en bas à gauche par rotation.
            for (int pass = 0; pass < 2; pass++)
            {
                p.Rot180 = pass == 1;
                for (int k = 0; k < 3; k++)
                {
                    float r = 9f + k * 5.5f;
                    Color c = k == 1 ? new Color(AfroPalette.Indigo.r, AfroPalette.Indigo.g, AfroPalette.Indigo.b, 0.75f) : AfroPalette.Gold;
                    p.Shape(DW - 40f, DH - 40f, DW - 11f, DH - 11f,
                        (x, y) => Mathf.Max(Mathf.Abs(Sdf.Circle(x - (DW - 11f), y - (DH - 11f), r)) - 0.9f, x - (DW - 11.5f)), c);
                }
                p.Circle(DW - 15f, DH - 15f, 2.2f, AfroPalette.Vermilion);
            }
            p.Rot180 = false;
        }

        // Index : grand rang (police géométrique) + petite couleur dessous, sur une plaque ivoire.
        static void PaintIndex(Paint2D p, string label, Suit suit, Color ink)
        {
            const float h = 47f, weight = 0.088f, left = 10f, top = 269f;
            float hw = h * weight;
            bool ten = label.Length > 1;
            float ws = ten ? 0.55f : 1f, spacing = ten ? 0.06f : 0.14f;
            float width = StrokeFont.Measure(label, h, ws, spacing);
            float x = left + hw, baseline = top - hw - h;
            float cx = x + width * 0.5f;
            p.RoundRect(4f, 168f, 49f, 275f, 7f, AfroPalette.Ivory, 2f);
            StrokeFont.Draw(p, label, x, baseline, h, ws, weight, ink, spacing);
            Pip(p, suit, cx, baseline - hw - 21f, 30f, false, ink, false);
        }

        static void Pip(Paint2D p, Suit suit, float cx, float cy, float size, bool flip, Color col, bool inlay)
        {
            Paint2D.Field f = (x, y) =>
            {
                float u = (x - cx) / size, v = (y - cy) / size;
                if (flip) { u = -u; v = -v; }
                return Sdf.Suit(suit, u, v) * size;
            };
            float e = size * 0.56f;
            p.Shape(cx - e, cy - e, cx + e, cy + e, f, col);
            if (inlay)
            {
                Color line = new Color(AfroPalette.GoldLight.r, AfroPalette.GoldLight.g, AfroPalette.GoldLight.b, 0.9f);
                p.Shape(cx - e, cy - e, cx + e, cy + e, (x, y) => Mathf.Abs(f(x, y) + size * 0.075f) - size * 0.011f, line);
            }
        }

        // ------------------------------------------------------------------ cartes numérotées

        static readonly float ColL = 72f, ColC = 100f, ColR = 128f;

        static void PaintPips(Paint2D p, int n, Suit suit, Color ink)
        {
            var pips = new List<Vector2>(); // (colonne x, fraction de hauteur depuis le haut)
            void Add(float x, float f) => pips.Add(new Vector2(x, f));
            void Pair(float f) { Add(ColL, f); Add(ColR, f); }
            switch (n)
            {
                case 2: Add(ColC, 0f); Add(ColC, 1f); break;
                case 3: Add(ColC, 0f); Add(ColC, 0.5f); Add(ColC, 1f); break;
                case 4: Pair(0f); Pair(1f); break;
                case 5: Pair(0f); Pair(1f); Add(ColC, 0.5f); break;
                case 6: Pair(0f); Pair(0.5f); Pair(1f); break;
                case 7: Pair(0f); Pair(0.5f); Pair(1f); Add(ColC, 0.25f); break;
                case 8: Pair(0f); Pair(0.5f); Pair(1f); Add(ColC, 0.25f); Add(ColC, 0.75f); break;
                case 9: Pair(0f); Pair(1f / 3f); Pair(2f / 3f); Pair(1f); Add(ColC, 0.5f); break;
                default: Pair(0f); Pair(1f / 3f); Pair(2f / 3f); Pair(1f); Add(ColC, 1f / 6f); Add(ColC, 5f / 6f); break;
            }
            float size = n >= 9 ? 35f : 38f;
            const float top = 245f, bottom = 35f;
            foreach (var pip in pips)
            {
                float y = Mathf.Lerp(top, bottom, pip.y);
                Pip(p, suit, pip.x, y, size, pip.y > 0.5f, ink, false);
            }
        }

        // ------------------------------------------------------------------ as

        static void PaintAce(Paint2D p, Suit suit, Color ink)
        {
            const float cx = 100f, cy = 140f;
            Color gold = AfroPalette.Gold;
            // Halo teinté puis soleil adinkra : anneau, perles et rayons triangulaires.
            p.Circle(cx, cy, 64f, new Color(ink.r, ink.g, ink.b, 0.06f), 3f);
            p.CircleStroke(cx, cy, 64f, 1.4f, gold);
            p.CircleStroke(cx, cy, 58f, 0.6f, new Color(AfroPalette.Indigo.r, AfroPalette.Indigo.g, AfroPalette.Indigo.b, 0.7f));
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2f / 24f;
                p.Circle(cx + Mathf.Cos(a) * 70f, cy + Mathf.Sin(a) * 70f, 1.9f, i % 2 == 0 ? gold : AfroPalette.Vermilion);
            }
            for (int i = 0; i < 8; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / 8f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var nrm = new Vector2(-dir.y, dir.x);
                var c = new Vector2(cx, cy);
                p.Polygon(new[] { c + dir * 75f + nrm * 4f, c + dir * 86f, c + dir * 75f - nrm * 4f }, gold);
            }
            // Pistes de circuit vers les coins libres (haut droite, bas gauche).
            for (int pass = 0; pass < 2; pass++)
            {
                p.Rot180 = pass == 1;
                var trace = new[] { new Vector2(cx + 45f, cy + 45f), new Vector2(150f, 205f), new Vector2(150f, 236f), new Vector2(166f, 252f) };
                p.Polyline(trace, 1.0f, new Color(AfroPalette.Indigo.r, AfroPalette.Indigo.g, AfroPalette.Indigo.b, 0.85f));
                p.CircleStroke(166f, 252f, 3.2f, 1.0f, gold);
                p.Circle(150f, 220f, 1.8f, AfroPalette.Vermilion);
            }
            p.Rot180 = false;
            Pip(p, suit, cx, cy, 98f, false, ink, true);
        }

        // ------------------------------------------------------------------ figures

        static void PaintFigure(Paint2D p, Rank rank, Suit suit, CardColor color)
        {
            bool red = color == CardColor.Red;
            Color panel = red ? AfroPalette.Terracotta : AfroPalette.Indigo;
            Color panelLight = red ? new Color(0.85f, 0.38f, 0.26f) : new Color(0.42f, 0.50f, 0.95f);
            Color neon = red ? AfroPalette.NeonAmber : AfroPalette.NeonCyan;
            Color gold = AfroPalette.Gold, ink = AfroPalette.Ink, ivory = AfroPalette.Ivory;
            const float px0 = 50f, px1 = 150f, mid = 140f;

            // Panneau (moitié haute) : fond, motif ndop en losanges, bordure or.
            p.Shape(px0, mid - 2f, px1, 268f, (x, y) => Sdf.RoundBox(x - 100f, y - 140f, 50f, 128f, 8f), panel);
            const float s = 13f;
            p.Shape(px0, mid, px1, 268f, (x, y) =>
            {
                float u = (x - 100f + y) * 0.7071f, v = (x - 100f - y) * 0.7071f;
                float du = Mathf.Abs(Mathf.Repeat(u, s) - s * 0.5f), dv = Mathf.Abs(Mathf.Repeat(v, s) - s * 0.5f);
                float d = Mathf.Abs(du + dv - s * 0.32f) - 0.7f;
                return Mathf.Max(d, Sdf.RoundBox(x - 100f, y - 140f, 47f, 125f, 6f));
            }, new Color(panelLight.r, panelLight.g, panelLight.b, 0.28f));

            // Halo solaire derrière la tête.
            p.Circle(100f, 202f, 36f, new Color(neon.r, neon.g, neon.b, 0.22f), 2f);
            p.CircleStroke(100f, 202f, 36f, 0.9f, new Color(neon.r, neon.g, neon.b, 0.9f));
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f;
                if (Mathf.Sin(a) < -0.2f) continue;
                p.Line(100f + Mathf.Cos(a) * 39f, 202f + Mathf.Sin(a) * 39f, 100f + Mathf.Cos(a) * 43f, 202f + Mathf.Sin(a) * 43f, 0.7f, gold);
            }

            // Pistes de circuit néon.
            for (int side = -1; side <= 1; side += 2)
            {
                float sx = side;
                var tr = new[] { new Vector2(100f + sx * 47f, 232f), new Vector2(100f + sx * 41f, 226f), new Vector2(100f + sx * 41f, 190f), new Vector2(100f + sx * 33f, 182f) };
                p.Polyline(tr, 0.8f, neon);
                p.Circle(100f + sx * 33f, 182f, 2f, neon);
                p.CircleStroke(100f + sx * 41f, 208f, 2.2f, 0.7f, gold);
            }

            // Buste : robe en wax (chevrons).
            Color robeA, robeB;
            switch (rank)
            {
                case Rank.King: robeA = gold; robeB = red ? AfroPalette.Crimson : ink; break;
                case Rank.Queen: robeA = red ? AfroPalette.Vermilion : new Color(0.20f, 0.26f, 0.62f); robeB = ivory; break;
                default: robeA = AfroPalette.Copper; robeB = ivory; break;
            }
            p.Shape(54f, mid, 146f, 178f, (x, y) => Sdf.Ellipse(x - 100f, y - 140f, 45f, 34f), (x, y) =>
            {
                float band = Mathf.Repeat(Mathf.Abs(x - 100f) * 0.8f + y, 10f);
                return band < 5f ? robeA : robeB;
            });
            p.Stroke(54f, mid, 146f, 178f, (x, y) => Sdf.Ellipse(x - 100f, y - 140f, 45f, 34f), 0.7f, ink);
            // Cou, collier de perles.
            p.RoundRect(93f, 164f, 107f, 184f, 3f, AfroPalette.Bronze);
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(200f, 340f, i / 8f) * Mathf.Deg2Rad;
                float bx = 100f + Mathf.Cos(a) * 21f, by = 182f + Mathf.Sin(a) * 15f;
                p.Circle(bx, by, 3.4f, ink);
                p.Circle(bx, by, 2.6f, i % 2 == 0 ? gold : ivory);
            }
            // Tête.
            Color skin = AfroPalette.Bronze;
            p.Ellipse(100f, 199f, 15.5f, 19f, ink);
            p.Ellipse(100f, 199f, 14.5f, 18f, skin);
            p.Ellipse(94f, 202f, 3.8f, 1.8f, ivory);
            p.Ellipse(106f, 202f, 3.8f, 1.8f, ivory);
            p.Circle(94f, 202f, 1.3f, ink);
            p.Circle(106f, 202f, 1.3f, ink);
            p.Line(90.5f, 206f, 97f, 206.5f, 0.6f, ink);
            p.Line(103f, 206.5f, 109.5f, 206f, 0.6f, ink);
            p.Line(100f, 200f, 100f, 194f, 0.8f, new Color(0.25f, 0.13f, 0.08f));
            p.Ellipse(100f, 188.5f, 4.2f, 1.5f, new Color(0.45f, 0.08f, 0.08f));
            for (int k = 0; k < 3; k++)
            {
                // Scarifications stylisées dorées.
                p.Line(88f, 197f - k * 2.6f, 91.5f, 197f - k * 2.6f, 0.55f, gold);
                p.Line(108.5f, 197f - k * 2.6f, 112f, 197f - k * 2.6f, 0.55f, gold);
            }
            p.Rhombus(100f, 211f, 1.8f, 2.6f, gold);

            switch (rank)
            {
                case Rank.King: PaintCrown(p, neon); break;
                case Rank.Queen: PaintHeadwrap(p, red); break;
                default: PaintValet(p, neon); break;
            }

            // Petite couleur dans le coin du panneau.
            Color pipCol = ivory;
            Pip(p, suit, 62f, 254f, 17f, false, pipCol, false);

            // Bordure or du panneau et bande centrale à dents de scie (ndop).
            p.Stroke(px0, mid - 2f, px1, 268f, (x, y) => Sdf.RoundBox(x - 100f, y - 140f, 50f, 128f, 8f), 1.3f, gold);
            p.Shape(px0, mid - 1f, px1, mid + 5f, (x, y) => Mathf.Max(Mathf.Abs(y - (mid + 2f)) - 3f, Mathf.Abs(x - 100f) - 49f), gold);
            for (int i = 0; i < 12; i++)
            {
                float x0 = 52f + i * 8f;
                p.Polygon(new[] { new Vector2(x0, mid + 0.6f), new Vector2(x0 + 4f, mid + 4.4f), new Vector2(x0 + 8f, mid + 0.6f) }, i % 2 == 0 ? ink : panel);
            }
        }

        static void PaintCrown(Paint2D p, Color neon)
        {
            Color gold = AfroPalette.Gold, ink = AfroPalette.Ink;
            var band = new[] { new Vector2(83f, 213f), new Vector2(117f, 213f), new Vector2(121f, 228f), new Vector2(79f, 228f) };
            var spikes = new List<Vector2[]>();
            float[] tips = { 248f, 253f, 261f, 253f, 248f };
            for (int i = 0; i < 5; i++)
            {
                float x = 80f + i * 10f;
                spikes.Add(new[] { new Vector2(x - 5.5f, 227f), new Vector2(x, tips[i]), new Vector2(x + 5.5f, 227f) });
            }
            foreach (var sp in spikes)
            {
                p.Shape(70f, 220f, 130f, 266f, (x, y) => Sdf.Polygon(sp, x, y) - 0.8f, ink);
                p.Polygon(sp, gold);
            }
            p.Shape(70f, 208f, 130f, 232f, (x, y) => Sdf.Polygon(band, x, y) - 0.8f, ink);
            p.Polygon(band, gold);
            for (int i = 0; i < 5; i++)
            {
                p.Circle(80f + i * 10f, tips[i] + 1.5f, 2.4f, AfroPalette.Ivory);
                if (i < 4) p.Rhombus(85f + i * 10f, 220.5f, 2.6f, 3.6f, i % 2 == 0 ? AfroPalette.Indigo : AfroPalette.Vermilion);
            }
            p.Rhombus(100f, 236f, 3.5f, 5.5f, neon);
            p.RhombusStroke(100f, 236f, 3.5f, 5.5f, 0.5f, ink);
        }

        static void PaintHeadwrap(Paint2D p, bool red)
        {
            Color a = red ? AfroPalette.Vermilion : new Color(0.20f, 0.26f, 0.62f);
            Color b = AfroPalette.Gold, ink = AfroPalette.Ink;
            Paint2D.Field f = (x, y) => Mathf.Min(Sdf.Ellipse(x - 100f, y - 224f, 27f, 13f),
                Mathf.Min(Sdf.Ellipse(x - 100f, y - 237f, 21f, 13f), Sdf.Ellipse(x - 100f, y - 249f, 12f, 9f)));
            p.Shape(66f, 205f, 134f, 264f, (x, y) => f(x, y) - 0.9f, ink);
            p.Shape(66f, 205f, 134f, 264f, f, (x, y) =>
            {
                float r = Mathf.Sqrt((x - 100f) * (x - 100f) + (y - 206f) * (y - 206f) * 1.4f);
                return Mathf.Repeat(r, 9f) < 4.5f ? a : b;
            });
            p.Circle(100f, 252f, 3f, AfroPalette.Ivory);
            // Grandes créoles.
            p.CircleStroke(84f, 190f, 4.8f, 1.2f, b);
            p.CircleStroke(116f, 190f, 4.8f, 1.2f, b);
        }

        static void PaintValet(Paint2D p, Color neon)
        {
            Color gold = AfroPalette.Gold, ink = AfroPalette.Ink;
            // Coupe haute à motifs de circuit.
            p.RoundRect(84f, 205f, 116f, 236f, 5f, ink);
            for (int k = 0; k < 3; k++)
            {
                float y = 214f + k * 7f;
                p.Line(88f, y, 112f, y, 0.6f, gold);
                p.Circle(88f + k * 12f, y, 1.5f, neon);
            }
            // Lance (droite) et bouclier (gauche).
            p.Line(136f, 146f, 136f, 246f, 1.5f, gold);
            p.Rhombus(136f, 253f, 4.5f, 9f, gold);
            p.RhombusStroke(136f, 253f, 4.5f, 9f, 0.5f, ink);
            p.Line(132.5f, 234f, 139.5f, 234f, 1.2f, AfroPalette.Ivory);
            p.Ellipse(64f, 186f, 10.5f, 18f, ink);
            p.Ellipse(64f, 186f, 9.5f, 17f, AfroPalette.Copper);
            p.Rhombus(64f, 186f, 5f, 10f, AfroPalette.Ivory);
            p.Rhombus(64f, 186f, 2.2f, 4.5f, ink);
            p.Line(64f, 170f, 64f, 176f, 0.6f, AfroPalette.Ivory);
            p.Line(64f, 196f, 64f, 202f, 0.6f, AfroPalette.Ivory);
        }

        // ------------------------------------------------------------------ jokers

        static void PaintJoker(Paint2D p, bool red)
        {
            Color bg = red ? AfroPalette.Crimson : new Color(0.05f, 0.05f, 0.11f);
            Color bgCenter = red ? new Color(0.78f, 0.13f, 0.10f) : new Color(0.13f, 0.11f, 0.32f);
            Color main = red ? new Color(1.0f, 0.80f, 0.32f) : new Color(0.35f, 0.95f, 1.0f);
            Color accent = red ? AfroPalette.Ivory : AfroPalette.NeonMagenta;
            Color dark = red ? new Color(0.30f, 0.02f, 0.04f) : new Color(0.03f, 0.03f, 0.07f);
            p.Fill(bg);
            p.Shape(-2f, -2f, DW + 2f, DH + 2f, (x, y) => -1f, (x, y) =>
            {
                float dx = (x - 100f) / 110f, dy = (y - 140f) / 150f;
                float k = Mathf.Clamp01(1f - (dx * dx + dy * dy));
                return new Color(bgCenter.r, bgCenter.g, bgCenter.b, k * k);
            });
            // Motif ndop discret en fond.
            const float s = 20f;
            p.Shape(10f, 10f, DW - 10f, DH - 10f, (x, y) =>
            {
                float u = (x - 100f + y) * 0.7071f, v = (x - 100f - y) * 0.7071f;
                float du = Mathf.Abs(Mathf.Repeat(u, s) - s * 0.5f), dv = Mathf.Abs(Mathf.Repeat(v, s) - s * 0.5f);
                return Mathf.Abs(du + dv - s * 0.3f) - 0.6f;
            }, new Color(main.r, main.g, main.b, 0.10f));
            p.RoundRectStroke(7f, 7f, DW - 7f, DH - 7f, 9f, 1.1f, main);
            p.RoundRectStroke(11f, 11f, DW - 11f, DH - 11f, 6f, 0.5f, new Color(accent.r, accent.g, accent.b, 0.6f));

            const float cx = 100f, cy = 136f;
            // Crête de rayons (au-dessus du masque).
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(40f, 140f, i / 8f) * Mathf.Deg2Rad;
                float len = i % 2 == 0 ? 40f : 30f;
                float r0 = 54f;
                Vector2 o = new Vector2(cx, cy + 6f);
                Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                p.Line(o.x + d.x * r0, o.y + d.y * r0, o.x + d.x * (r0 + len), o.y + d.y * (r0 + len), 2.2f, i % 2 == 0 ? main : accent);
                p.Circle(o.x + d.x * (r0 + len + 5f), o.y + d.y * (r0 + len + 5f), 3f, main);
            }
            // Oreilles rondes (masque éléphant bamiléké stylisé) avec rayons.
            for (int side = -1; side <= 1; side += 2)
            {
                float ex = cx + side * 44f, ey = cy + 14f;
                p.Circle(ex, ey, 20f, dark);
                p.CircleStroke(ex, ey, 19f, 2.4f, main);
                p.CircleStroke(ex, ey, 10f, 1.2f, accent);
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    p.Line(ex + Mathf.Cos(a) * 12f, ey + Mathf.Sin(a) * 12f, ex + Mathf.Cos(a) * 16f, ey + Mathf.Sin(a) * 16f, 0.9f, main);
                }
                p.Circle(ex, ey, 3.4f, main);
                // Piste de circuit vers le bas (côté gauche) ou le haut (côté droit), hors des index.
                var tr = side < 0
                    ? new[] { new Vector2(ex - 4f, ey - 19f), new Vector2(ex - 4f, 58f), new Vector2(ex + 6f, 48f) }
                    : new[] { new Vector2(ex + 4f, ey + 19f), new Vector2(ex + 4f, 222f), new Vector2(ex - 6f, 232f) };
                p.Polyline(tr, 1.1f, new Color(main.r, main.g, main.b, 0.8f));
                Vector2 end = tr[2];
                p.CircleStroke(end.x, end.y, 3f, 1.1f, accent);
            }
            // Visage du masque.
            p.Ellipse(cx, cy, 31f, 50f, dark);
            p.Stroke(cx - 34f, cy - 54f, cx + 34f, cy + 54f, (x, y) => Sdf.Ellipse(x - cx, y - cy, 31f, 50f), 2.8f, main);
            p.Stroke(cx - 30f, cy - 50f, cx + 30f, cy + 50f, (x, y) => Sdf.Ellipse(x - cx, y - cy, 25f, 44f), 0.8f, accent);
            p.Rhombus(cx - 12f, cy + 12f, 8f, 4.5f, main);
            p.Rhombus(cx + 12f, cy + 12f, 8f, 4.5f, main);
            p.Circle(cx - 12f, cy + 12f, 1.8f, dark);
            p.Circle(cx + 12f, cy + 12f, 1.8f, dark);
            p.Line(cx, cy + 4f, cx, cy - 16f, 2f, main);
            p.Line(cx - 6f, cy - 16f, cx + 6f, cy - 16f, 2f, main);
            p.RoundRect(cx - 11f, cy - 31f, cx + 11f, cy - 25f, 3f, accent);
            p.Circle(cx, cy + 34f, 5.5f, main);
            p.CircleStroke(cx, cy + 34f, 9f, 0.9f, accent);
            for (int k = 0; k < 3; k++)
            {
                p.Line(cx - 22f, cy - 4f - k * 5f, cx - 16f, cy - 4f - k * 5f, 0.9f, accent);
                p.Line(cx + 16f, cy - 4f - k * 5f, cx + 22f, cy - 4f - k * 5f, 0.9f, accent);
            }
            // Barbe en dents de scie (ndop).
            for (int i = 0; i < 6; i++)
            {
                float x0 = cx - 21f + i * 7f;
                p.Polygon(new[] { new Vector2(x0, cy - 52f), new Vector2(x0 + 3.5f, cy - 63f), new Vector2(x0 + 7f, cy - 52f) }, i % 2 == 0 ? main : accent);
            }

            // Index « JOKER » vertical + étoile, dans deux coins.
            for (int pass = 0; pass < 2; pass++)
            {
                p.Rot180 = pass == 1;
                const float h = 15.5f, weight = 0.11f;
                string word = "JOKER";
                for (int i = 0; i < word.Length; i++)
                {
                    float baseline = 268f - (i + 1) * h - i * 4.5f;
                    float w = StrokeFont.Measure(word[i].ToString(), h, 1f);
                    StrokeFont.Draw(p, word[i].ToString(), 21f - w * 0.5f, baseline, h, 1f, weight, main);
                }
                Star(p, 21f, 166f, 8f, accent);
            }
            p.Rot180 = false;
        }

        static void Star(Paint2D p, float cx, float cy, float r, Color c)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float rr = i % 2 == 0 ? r : r * 0.42f;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * rr, cy + Mathf.Sin(a) * rr);
            }
            p.Polygon(pts, c);
        }

        // ------------------------------------------------------------------ dos

        /// <summary>Dessine le dos (couleur) et sa carte de lueur (émission néon).</summary>
        public static void PaintBack(Paint2D p, Paint2D glow)
        {
            const float W = BackW, H = BackH;
            const float cx = W * 0.5f, cy = H * 0.5f;
            Color indigo = AfroPalette.IndigoDeep, gold = AfroPalette.Gold;
            Color ndop = new Color(0.80f, 0.83f, 0.96f, 0.78f);
            p.Fill(indigo);
            glow.Fill(Color.black);
            // Léger éclaircissement au centre (teinture irrégulière).
            p.Shape(0f, 0f, W, H, (x, y) => -1f, (x, y) =>
            {
                float dx = (x - cx) / cx, dy = (y - cy) / cy;
                float k = Mathf.Clamp01(1f - (dx * dx + dy * dy) * 0.7f);
                return new Color(0.08f, 0.11f, 0.33f, k * 0.8f);
            });

            // Grille de motifs ndop (symétrique par rotation de 180°).
            const float fx0 = 20f, fy0 = 20f, fx1 = W - 20f, fy1 = H - 20f;
            const int cols = 9, rows = 13;
            float cw = (fx1 - fx0) / cols, ch = (fy1 - fy0) / rows;
            for (int i = 1; i < cols; i++)
                p.Line(fx0 + i * cw, fy0, fx0 + i * cw, fy1, 0.45f, new Color(ndop.r, ndop.g, ndop.b, 0.22f));
            for (int j = 1; j < rows; j++)
                p.Line(fx0, fy0 + j * ch, fx1, fy0 + j * ch, 0.45f, new Color(ndop.r, ndop.g, ndop.b, 0.22f));
            for (int j = 0; j < rows; j++)
            for (int i = 0; i < cols; i++)
            {
                float mx = fx0 + (i + 0.5f) * cw, my = fy0 + (j + 0.5f) * ch;
                int ring = Mathf.RoundToInt(Mathf.Abs(i - (cols - 1) * 0.5f) + Mathf.Abs(j - (rows - 1) * 0.5f));
                NdopMotif(p, ring % 5, mx, my, Mathf.Min(cw, ch) * 0.40f, ndop);
            }

            // Médaillon central.
            const float R = 60f;
            p.Circle(cx, cy, R + 4f, indigo, 1.5f);
            p.Circle(cx, cy, R, new Color(0.06f, 0.08f, 0.26f));
            p.CircleStroke(cx, cy, R, 2.2f, gold);
            p.CircleStroke(cx, cy, R + 5f, 0.8f, new Color(gold.r, gold.g, gold.b, 0.7f));
            for (int i = 0; i < 28; i++)
            {
                float a = i * Mathf.PI * 2f / 28f;
                p.Circle(cx + Mathf.Cos(a) * (R - 7f), cy + Mathf.Sin(a) * (R - 7f), 1.6f, gold);
            }
            p.CircleStroke(cx, cy, R - 13f, 1.0f, AfroPalette.NeonCyan);
            glow.CircleStroke(cx, cy, R - 13f, 1.6f, AfroPalette.NeonCyan * 0.8f);
            glow.CircleStroke(cx, cy, R, 2.6f, AfroPalette.NeonAmber * 0.35f);
            // Symbole inventé : losange à quatre cornes spiralées.
            p.RhombusStroke(cx, cy, 13f, 19f, 2.4f, gold);
            p.Rhombus(cx, cy, 5f, 7.5f, AfroPalette.NeonMagenta);
            glow.Rhombus(cx, cy, 5f, 7.5f, AfroPalette.NeonMagenta);
            for (int k = 0; k < 4; k++)
            {
                float a0 = k * Mathf.PI * 0.5f + Mathf.PI * 0.5f;
                var dir = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                float vr = k % 2 == 0 ? 19f : 13f;
                Vector2 vtx = new Vector2(cx, cy) + dir * vr;
                Vector2 c = new Vector2(cx, cy) + dir * (vr + 12f);
                var pts = new List<Vector2> { vtx };
                const int n = 30;
                float startA = Mathf.Atan2(-dir.y, -dir.x);
                for (int t = 0; t <= n; t++)
                {
                    float u = t / (float)n;
                    float ang = startA - u * Mathf.PI * 3.2f;
                    float rr = 11f * (1f - u * 0.82f);
                    pts.Add(c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rr);
                }
                p.Polyline(pts, 1.7f, gold);
            }

            // Pistes de circuit néon partant du médaillon.
            float[] angles = { 30f, 90f, 150f, 210f, 270f, 330f };
            foreach (float deg in angles)
            {
                float a = deg * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 s0 = new Vector2(cx, cy) + d * (R + 6f);
                Vector2 s1 = new Vector2(cx, cy) + d * (R + 22f);
                Vector2 s2;
                if (Mathf.Abs(d.x) < 0.1f) s2 = new Vector2(s1.x, d.y > 0 ? fy1 - 8f : fy0 + 8f);
                else s2 = new Vector2(d.x > 0 ? fx1 - 8f : fx0 + 8f, s1.y + Mathf.Sign(d.y) * 12f);
                var path = Mathf.Abs(d.x) < 0.1f ? new[] { s0, s2 } : new[] { s0, s1, new Vector2(s1.x + (s2.x - s1.x) * 0.5f, s2.y), s2 };
                p.Polyline(path, 1.1f, AfroPalette.NeonCyan);
                glow.Polyline(path, 1.6f, AfroPalette.NeonCyan * 0.9f, 1.2f, true);
                Vector2 end = path[path.Length - 1];
                p.CircleStroke(end.x, end.y, 3.4f, 1.1f, AfroPalette.NeonMagenta);
                glow.CircleStroke(end.x, end.y, 3.6f, 1.6f, AfroPalette.NeonMagenta);
            }

            // Bordure : tranche dorée, filets or, losanges de coin.
            p.Stroke(0f, 0f, W, H, (x, y) => -Sdf.RoundBox(x - cx, y - cy, cx, cy, 2f), 7f, gold, 1f);
            p.RoundRectStroke(11f, 11f, W - 11f, H - 11f, 8f, 1.3f, gold);
            p.RoundRectStroke(15.5f, 15.5f, W - 15.5f, H - 15.5f, 5f, 0.6f, new Color(gold.r, gold.g, gold.b, 0.7f));
            for (int k = 0; k < 4; k++)
            {
                float x = k % 2 == 0 ? 11f : W - 11f, y = k < 2 ? 11f : H - 11f;
                p.Rhombus(x, y, 4f, 4f, gold);
                p.Rhombus(x, y, 1.6f, 1.6f, AfroPalette.NeonMagenta);
            }
        }

        static void NdopMotif(Paint2D p, int kind, float x, float y, float h, Color c)
        {
            const float soft = 1.5f; // bords « teinture à réserve » légèrement flous
            switch (kind)
            {
                case 0:
                    p.RhombusStroke(x, y, h * 0.8f, h, 1.1f, c, soft);
                    p.Circle(x, y, 1.8f, c, soft);
                    break;
                case 1:
                    p.RoundRectStroke(x - h * 0.85f, y - h * 0.85f, x + h * 0.85f, y + h * 0.85f, 1.5f, 1.0f, c, soft);
                    p.RoundRectStroke(x - h * 0.4f, y - h * 0.4f, x + h * 0.4f, y + h * 0.4f, 1f, 1.0f, c, soft);
                    break;
                case 2:
                    p.Line(x - h * 0.7f, y - h * 0.7f, x + h * 0.7f, y + h * 0.7f, 1.0f, c, soft);
                    p.Line(x - h * 0.7f, y + h * 0.7f, x + h * 0.7f, y - h * 0.7f, 1.0f, c, soft);
                    p.Circle(x, y + h * 0.9f, 1.4f, c, soft);
                    p.Circle(x, y - h * 0.9f, 1.4f, c, soft);
                    break;
                case 3:
                    for (int r = -1; r <= 1; r += 2)
                    {
                        var z = new Vector2[5];
                        for (int k = 0; k < 5; k++) z[k] = new Vector2(x - h + k * h * 0.5f, y + r * h * 0.4f + (k % 2 == 0 ? -1 : 1) * h * 0.25f);
                        p.Polyline(z, 0.95f, c, soft);
                    }
                    break;
                default:
                    p.CircleStroke(x, y, h * 0.62f, 1.1f, c, soft);
                    for (int k = 0; k < 4; k++)
                    {
                        float a = k * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                        p.Circle(x + Mathf.Cos(a) * h, y + Mathf.Sin(a) * h, 1.3f, c, soft);
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ halo de surbrillance

        /// <summary>
        /// Texture alpha du liseré lumineux : trait fin au bord de la carte + halo extérieur.
        /// Couvre la carte plus une marge <paramref name="margin"/> (unités monde) de chaque côté.
        /// </summary>
        public static Texture2D HighlightTexture(float width, float height, float radius, float margin)
        {
            const int tw = 128;
            int th = Mathf.RoundToInt(tw * (height + 2f * margin) / (width + 2f * margin));
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, true) { name = "Check_CardHighlight", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[tw * th];
            float qw = width + 2f * margin, qh = height + 2f * margin;
            for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
            {
                float wx = ((x + 0.5f) / tw - 0.5f) * qw, wz = ((y + 0.5f) / th - 0.5f) * qh;
                float d = Sdf.RoundBox(wx, wz, width * 0.5f, height * 0.5f, radius);
                float line = Mathf.Exp(-(d - 0.004f) * (d - 0.004f) / (0.0075f * 0.0075f));
                float halo = d > 0f ? Mathf.Exp(-d / (margin * 0.32f)) * 0.55f * Mathf.Clamp01((margin - d) / (margin * 0.25f)) : 0f;
                float a = Mathf.Clamp01(line + halo);
                px[y * tw + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}
