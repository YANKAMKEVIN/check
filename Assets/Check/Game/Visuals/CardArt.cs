using System.Collections.Generic;
using Check.Core;
using UnityEngine;

namespace Check.Game.Visuals
{
    /// <summary>
    /// Dessin par code des faces et du dos des cartes (aucune texture importée).
    /// Style « Dernier Métro » (Art déco des années 30, accordé au décor du wagon).
    /// Face : 200 × 280 px de design (+ 2 px de fond perdu), crème, index géants dans deux coins, cadre laiton,
    /// points classiques, figures V / D / R en personnel de gare et jokers en saltimbanques.
    /// Dos : bleu nuit, croisillons laiton, rosace façon panneau de station barrée du mot CHECK.
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

        // Palette « Dernier Métro » (Art déco des années 30) : pétrole, laiton, bordeaux, bleu nuit.
        static readonly Color Petrol = new Color(0.09f, 0.25f, 0.28f);
        static readonly Color PetrolLight = new Color(0.22f, 0.45f, 0.48f);
        static readonly Color Brass = new Color(0.80f, 0.58f, 0.30f);
        static readonly Color BrassLight = new Color(0.96f, 0.80f, 0.50f);
        static readonly Color Burgundy = new Color(0.46f, 0.10f, 0.12f);
        static readonly Color BurgundyLight = new Color(0.66f, 0.22f, 0.22f);
        static readonly Color Night = new Color(0.11f, 0.15f, 0.24f);
        static readonly Color NightDeep = new Color(0.055f, 0.075f, 0.13f);
        static readonly Color Teal = new Color(0.47f, 0.78f, 0.80f);

        static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

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
            // Cadre laiton + filet pétrole.
            p.RoundRectStroke(7f, 7f, DW - 7f, DH - 7f, 9f, 1.05f, Brass);
            p.RoundRectStroke(11f, 11f, DW - 11f, DH - 11f, 6f, 0.5f, A(Petrol, 0.7f));
            // Éventail Art déco dans le coin haut droit, et bas gauche par rotation.
            for (int pass = 0; pass < 2; pass++)
            {
                p.Rot180 = pass == 1;
                float ox = DW - 11f, oy = DH - 11f;
                for (int k = 0; k < 2; k++)
                {
                    float r = k == 0 ? 25f : 17f;
                    Color c = k == 0 ? Brass : A(Petrol, 0.8f);
                    p.Shape(ox - 30f, oy - 30f, ox, oy, (x, y) => Mathf.Max(Mathf.Abs(Sdf.Circle(x - ox, y - oy, r)) - 0.8f, Mathf.Max(x - ox, y - oy)), c);
                }
                for (int i = 1; i < 6; i++)
                {
                    float a = (180f + i * 15f) * Mathf.Deg2Rad;
                    p.Line(ox + Mathf.Cos(a) * 5f, oy + Mathf.Sin(a) * 5f, ox + Mathf.Cos(a) * 15f, oy + Mathf.Sin(a) * 15f, 0.6f, Brass);
                }
                p.Circle(ox - 3f, oy - 3f, 2f, AfroPalette.Vermilion);
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
            // Soleil Art déco : rayons fins alternés, double anneau laiton / pétrole.
            p.Circle(cx, cy, 64f, A(ink, 0.05f), 3f);
            for (int i = 0; i < 36; i++)
            {
                float a = i * Mathf.PI * 2f / 36f;
                float r1 = i % 2 == 0 ? 80f : 70f;
                p.Line(cx + Mathf.Cos(a) * 58f, cy + Mathf.Sin(a) * 58f, cx + Mathf.Cos(a) * r1, cy + Mathf.Sin(a) * r1, 0.55f, A(Brass, 0.9f));
            }
            p.CircleStroke(cx, cy, 56f, 1.4f, Brass);
            p.CircleStroke(cx, cy, 51f, 0.6f, A(Petrol, 0.8f));
            // Gradins (ziggourat) en haut et en bas.
            for (int pass = 0; pass < 2; pass++)
            {
                p.Rot180 = pass == 1;
                for (int k = 0; k < 3; k++)
                {
                    float hw = 22f - k * 7f, y = cy + 92f + k * 5f;
                    p.Line(cx - hw, y, cx + hw, y, 1.0f, k == 1 ? Petrol : Brass);
                }
            }
            p.Rot180 = false;
            Pip(p, suit, cx, cy, 98f, false, ink, true);
        }

        // ------------------------------------------------------------------ figures

        /// <summary>
        /// Figures « personnel du Dernier Métro » : le Roi est le chef de gare (képi, boutons de laiton, moustache),
        /// la Dame une voyageuse des années 30 (chapeau cloche, col de fourrure, perles), le Valet le contrôleur
        /// (casquette, lanterne). Panneau pétrole (cartes noires) ou bordeaux (rouges), soleil Art déco derrière la tête.
        /// </summary>
        static void PaintFigure(Paint2D p, Rank rank, Suit suit, CardColor color)
        {
            bool red = color == CardColor.Red;
            Color panel = red ? Burgundy : Petrol;
            Color panelLight = red ? BurgundyLight : PetrolLight;
            Color ink = AfroPalette.Ink, ivory = AfroPalette.Ivory;
            const float px0 = 50f, px1 = 150f, mid = 140f;

            // Panneau : fond, fines rayures verticales, soleil derrière la tête.
            p.Shape(px0, mid - 2f, px1, 268f, (x, y) => Sdf.RoundBox(x - 100f, y - 140f, 50f, 128f, 8f), panel);
            p.Shape(px0, mid, px1, 268f, (x, y) =>
            {
                float d = Mathf.Abs(Mathf.Repeat(x - 100f, 7f) - 3.5f) - 0.5f;
                return Mathf.Max(d, Sdf.RoundBox(x - 100f, y - 140f, 47f, 125f, 6f));
            }, A(panelLight, 0.35f));
            for (int i = 0; i < 15; i++)
            {
                float a = Mathf.Lerp(10f, 170f, i / 14f) * Mathf.Deg2Rad;
                p.Line(100f + Mathf.Cos(a) * 30f, 204f + Mathf.Sin(a) * 30f, 100f + Mathf.Cos(a) * 46f, 204f + Mathf.Sin(a) * 46f, i % 2 == 0 ? 1.1f : 0.6f, A(BrassLight, 0.75f));
            }

            // Buste : manteau d'uniforme ou de voyage.
            Color coat, lapel;
            switch (rank)
            {
                case Rank.King: coat = Night; lapel = Brass; break;
                case Rank.Queen: coat = red ? BurgundyLight : PetrolLight; lapel = ivory; break;
                default: coat = red ? new Color(0.30f, 0.16f, 0.12f) : new Color(0.16f, 0.20f, 0.26f); lapel = Brass; break;
            }
            Paint2D.Field body = (x, y) => Sdf.Ellipse(x - 100f, y - 140f, 45f, 34f);
            p.Shape(54f, mid, 146f, 178f, body, coat);
            p.Stroke(54f, mid, 146f, 178f, body, 0.7f, ink);
            // Revers en V et chemise.
            p.Polygon(new[] { new Vector2(88f, 172f), new Vector2(100f, mid + 2f), new Vector2(112f, 172f) }, ivory);
            p.Polyline(new[] { new Vector2(84f, 173f), new Vector2(100f, mid + 2f), new Vector2(116f, 173f) }, 1.0f, lapel);

            // Cou.
            Color skin = SkinTone(suit);
            p.RoundRect(93f, 166f, 107f, 184f, 3f, skin);

            switch (rank)
            {
                case Rank.King:
                    // Boutons de laiton et cravate.
                    for (int i = 0; i < 3; i++) { p.Circle(84f, 165f - i * 9f, 2.2f, Brass); p.Circle(116f, 165f - i * 9f, 2.2f, Brass); }
                    p.Polygon(new[] { new Vector2(97f, 170f), new Vector2(103f, 170f), new Vector2(101.5f, 150f), new Vector2(98.5f, 150f) }, AfroPalette.Vermilion);
                    break;
                case Rank.Queen:
                    // Col de fourrure festonné et rang de perles.
                    for (int i = 0; i < 9; i++)
                    {
                        float a = Mathf.Lerp(200f, 340f, i / 8f) * Mathf.Deg2Rad;
                        p.Circle(100f + Mathf.Cos(a) * 27f, 176f + Mathf.Sin(a) * 14f, 5.5f, ivory);
                    }
                    for (int i = 0; i < 9; i++)
                    {
                        float a = Mathf.Lerp(205f, 335f, i / 8f) * Mathf.Deg2Rad;
                        p.Circle(100f + Mathf.Cos(a) * 14f, 183f + Mathf.Sin(a) * 9f, 2.1f, BrassLight);
                    }
                    break;
                default:
                    // Sacoche en bandoulière.
                    p.Line(112f, 174f, 80f, 146f, 1.4f, new Color(0.45f, 0.28f, 0.15f));
                    p.RoundRect(72f, 142f, 86f, 152f, 2f, new Color(0.45f, 0.28f, 0.15f));
                    p.Circle(79f, 147f, 1.4f, Brass);
                    break;
            }

            // Tête.
            p.Ellipse(100f, 199f, 15.5f, 19f, ink);
            p.Ellipse(100f, 199f, 14.5f, 18f, skin);
            p.Ellipse(94f, 202f, 3.6f, 1.8f, ivory);
            p.Ellipse(106f, 202f, 3.6f, 1.8f, ivory);
            p.Circle(94f, 202f, 1.3f, ink);
            p.Circle(106f, 202f, 1.3f, ink);
            p.Line(90.5f, 206.5f, 97f, 207f, 0.6f, ink);
            p.Line(103f, 207f, 109.5f, 206.5f, 0.6f, ink);
            p.Line(100f, 200f, 100f, 194f, 0.8f, Darker(skin, 0.6f));
            if (rank == Rank.King)
            {
                // Moustache en guidon.
                p.Polyline(new[] { new Vector2(90f, 192f), new Vector2(95f, 190f), new Vector2(100f, 191.5f), new Vector2(105f, 190f), new Vector2(110f, 192f) }, 1.3f, ink);
            }
            else p.Ellipse(100f, 188.5f, 4f, 1.4f, rank == Rank.Queen ? new Color(0.70f, 0.12f, 0.16f) : new Color(0.45f, 0.10f, 0.10f));

            switch (rank)
            {
                case Rank.King: PaintKepi(p); break;
                case Rank.Queen: PaintCloche(p, red); break;
                default: PaintCap(p); PaintLantern(p); break;
            }

            // Petite couleur dans le coin du panneau.
            Pip(p, suit, 62f, 254f, 17f, false, ivory, false);

            // Bordure laiton du panneau et bande centrale à gradins.
            p.Stroke(px0, mid - 2f, px1, 268f, (x, y) => Sdf.RoundBox(x - 100f, y - 140f, 50f, 128f, 8f), 1.3f, Brass);
            p.Shape(px0, mid - 1f, px1, mid + 5f, (x, y) => Mathf.Max(Mathf.Abs(y - (mid + 2f)) - 3f, Mathf.Abs(x - 100f) - 49f), Brass);
            for (int i = 0; i < 12; i++)
            {
                float x0 = 52f + i * 8f;
                p.RoundRect(x0 + 1.5f, mid + 0.8f, x0 + 6.5f, mid + 3.2f, 0.5f, i % 2 == 0 ? ink : panel);
            }
        }

        static Color SkinTone(Suit suit) => suit switch
        {
            Suit.Spades => new Color(0.40f, 0.24f, 0.14f),
            Suit.Hearts => new Color(0.86f, 0.67f, 0.53f),
            Suit.Clubs => new Color(0.63f, 0.43f, 0.29f),
            _ => new Color(0.30f, 0.18f, 0.11f),
        };

        static Color Darker(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        /// <summary>Képi de chef de gare : calot plat, bandeau laiton, insigne, visière noire.</summary>
        static void PaintKepi(Paint2D p)
        {
            Color ink = AfroPalette.Ink;
            var crown = new[] { new Vector2(83f, 214f), new Vector2(117f, 214f), new Vector2(121f, 238f), new Vector2(79f, 238f) };
            p.Shape(70f, 206f, 130f, 246f, (x, y) => Sdf.Polygon(crown, x, y) - 0.8f, ink);
            p.Polygon(crown, Night);
            p.Shape(70f, 206f, 130f, 246f, (x, y) => Mathf.Max(Mathf.Abs(y - 220f) - 3.2f, Sdf.Polygon(crown, x, y)), Brass);
            p.Ellipse(100f, 238.5f, 21f, 3.5f, ink);
            p.Ellipse(100f, 238.5f, 20f, 2.6f, PetrolLight);
            // Insigne : petite roue ailée.
            p.CircleStroke(100f, 229f, 4f, 0.9f, BrassLight);
            p.Circle(100f, 229f, 1.3f, BrassLight);
            p.Line(92f, 230f, 96f, 229f, 0.8f, BrassLight);
            p.Line(104f, 229f, 108f, 230f, 0.8f, BrassLight);
            // Visière.
            p.Shape(76f, 206f, 124f, 216f, (x, y) => Sdf.Ellipse(x - 100f, y - 212f, 22f, 3.6f), ink);
        }

        /// <summary>Chapeau cloche des années 30 : dôme, ruban, petite plume.</summary>
        static void PaintCloche(Paint2D p, bool red)
        {
            Color ink = AfroPalette.Ink;
            Color hat = red ? Burgundy : Night, band = red ? BrassLight : Teal;
            Paint2D.Field dome = (x, y) => Mathf.Min(Sdf.Ellipse(x - 100f, y - 222f, 23f, 18f), Sdf.Ellipse(x - 100f, y - 212f, 27f, 5f));
            p.Shape(66f, 200f, 134f, 246f, (x, y) => dome(x, y) - 0.9f, ink);
            p.Shape(66f, 200f, 134f, 246f, dome, hat);
            p.Shape(66f, 200f, 134f, 246f, (x, y) => Mathf.Max(Mathf.Abs(y - 216f) - 2.4f, dome(x, y)), band);
            // Plume.
            p.Polyline(new[] { new Vector2(118f, 217f), new Vector2(126f, 230f), new Vector2(128f, 244f) }, 1.4f, A(AfroPalette.Ivory, 0.95f));
            for (int k = 0; k < 4; k++) p.Line(124f + k * 1f, 226f + k * 4f, 130f + k * 0.6f, 228f + k * 4f, 0.5f, AfroPalette.Ivory);
            // Boucles d'oreilles.
            p.Circle(85f, 192f, 2.4f, BrassLight);
            p.Circle(115f, 192f, 2.4f, BrassLight);
        }

        /// <summary>Casquette de contrôleur : calotte souple, galon laiton, visière.</summary>
        static void PaintCap(Paint2D p)
        {
            Color ink = AfroPalette.Ink;
            Paint2D.Field cap = (x, y) => Mathf.Max(Sdf.Ellipse(x - 100f, y - 216f, 21f, 15f), 214f - y);
            p.Shape(72f, 208f, 128f, 236f, (x, y) => cap(x, y) - 0.9f, ink);
            p.Shape(72f, 208f, 128f, 236f, cap, new Color(0.16f, 0.20f, 0.26f));
            p.Line(81f, 218f, 119f, 218f, 1.3f, Brass);
            p.Circle(100f, 224f, 1.8f, BrassLight);
            p.Shape(74f, 206f, 126f, 216f, (x, y) => Sdf.Ellipse(x - 104f, y - 212f, 21f, 3.4f), ink);
        }

        /// <summary>Lanterne de quai tenue à gauche (verre ambré).</summary>
        static void PaintLantern(Paint2D p)
        {
            Color ink = AfroPalette.Ink;
            p.Line(64f, 210f, 64f, 200f, 0.8f, ink);
            p.CircleStroke(64f, 212f, 3f, 0.8f, ink);
            p.RoundRect(56f, 176f, 72f, 200f, 2f, ink);
            p.RoundRect(58f, 179f, 70f, 197f, 1.5f, new Color(1f, 0.74f, 0.32f));
            p.Circle(64f, 188f, 3.4f, new Color(1f, 0.93f, 0.70f));
            p.Line(56f, 174f, 72f, 174f, 1.4f, Brass);
            p.Line(56f, 202f, 72f, 202f, 1.4f, Brass);
        }

        // ------------------------------------------------------------------ jokers

        /// <summary>
        /// Joker « saltimbanque du métro » : chapeau à trois pointes et grelots, collerette, soleil Art déco.
        /// Rouge : fond bordeaux, laiton et crème. Noir : fond bleu nuit, bleu pétrole clair et crème.
        /// </summary>
        static void PaintJoker(Paint2D p, bool red)
        {
            Color bg = red ? Burgundy : NightDeep;
            Color bgCenter = red ? BurgundyLight : Night;
            Color main = red ? BrassLight : Teal;
            Color accent = AfroPalette.Ivory;
            Color dark = red ? new Color(0.25f, 0.04f, 0.06f) : new Color(0.03f, 0.04f, 0.08f);
            p.Fill(bg);
            p.Shape(-2f, -2f, DW + 2f, DH + 2f, (x, y) => -1f, (x, y) =>
            {
                float dx = (x - 100f) / 110f, dy = (y - 140f) / 150f;
                float k = Mathf.Clamp01(1f - (dx * dx + dy * dy));
                return A(bgCenter, k * k);
            });
            p.RoundRectStroke(7f, 7f, DW - 7f, DH - 7f, 9f, 1.1f, main);
            p.RoundRectStroke(11f, 11f, DW - 11f, DH - 11f, 6f, 0.5f, A(accent, 0.6f));

            const float cx = 100f, cy = 128f;
            // Soleil Art déco derrière la tête.
            for (int i = 0; i < 28; i++)
            {
                float a = i * Mathf.PI * 2f / 28f;
                float r1 = i % 2 == 0 ? 88f : 74f;
                p.Line(cx + Mathf.Cos(a) * 52f, cy + Mathf.Sin(a) * 52f, cx + Mathf.Cos(a) * r1, cy + Mathf.Sin(a) * r1, i % 2 == 0 ? 1.0f : 0.6f, A(main, 0.55f));
            }

            // Collerette festonnée.
            for (int i = 0; i < 11; i++)
            {
                float a = Mathf.Lerp(195f, 345f, i / 10f) * Mathf.Deg2Rad;
                Vector2 c = new Vector2(cx + Mathf.Cos(a) * 34f, cy - 30f + Mathf.Sin(a) * 12f);
                p.Circle(c.x, c.y, 9f, dark);
                p.Circle(c.x, c.y, 8f, i % 2 == 0 ? main : accent);
            }

            // Chapeau à trois pointes, chacune terminée par un grelot.
            var tips = new[] { new Vector2(cx - 46f, cy + 52f), new Vector2(cx, cy + 82f), new Vector2(cx + 46f, cy + 52f) };
            for (int i = 0; i < 3; i++)
            {
                var tri = new[] { new Vector2(cx - 26f + i * 16f, cy + 22f), tips[i], new Vector2(cx - 10f + i * 16f, cy + 22f) };
                Color c = i == 1 ? accent : main;
                p.Shape(cx - 70f, cy + 10f, cx + 70f, cy + 100f, (x, y) => Sdf.Polygon(tri, x, y) - 1.2f, dark);
                p.Polygon(tri, c);
                p.Circle(tips[i].x, tips[i].y, 6.5f, dark);
                p.Circle(tips[i].x, tips[i].y, 5.5f, BrassLight);
                p.Line(tips[i].x - 3f, tips[i].y - 1f, tips[i].x + 3f, tips[i].y - 1f, 0.6f, dark);
            }
            p.Shape(cx - 34f, cy + 14f, cx + 34f, cy + 30f, (x, y) => Sdf.RoundBox(x - cx, y - (cy + 22f), 30f, 5f, 2f), BrassLight);

            // Visage : masque crème, yeux en losange, grand sourire.
            p.Ellipse(cx, cy - 4f, 25f, 28f, dark);
            p.Ellipse(cx, cy - 4f, 23.5f, 26.5f, accent);
            p.Rhombus(cx - 9f, cy + 2f, 5f, 7f, dark);
            p.Rhombus(cx + 9f, cy + 2f, 5f, 7f, dark);
            p.Rhombus(cx - 9f, cy + 2f, 2f, 3f, main);
            p.Rhombus(cx + 9f, cy + 2f, 2f, 3f, main);
            p.Circle(cx, cy - 6f, 3.4f, AfroPalette.Vermilion);
            var smile = new Vector2[13];
            for (int i = 0; i < 13; i++)
            {
                float a = Mathf.Lerp(200f, 340f, i / 12f) * Mathf.Deg2Rad;
                smile[i] = new Vector2(cx + Mathf.Cos(a) * 13f, cy - 9f + Mathf.Sin(a) * 9f);
            }
            p.Polyline(smile, 1.3f, dark);
            p.Circle(cx - 15f, cy - 12f, 3f, A(AfroPalette.Vermilion, 0.35f), 2f);
            p.Circle(cx + 15f, cy - 12f, 3f, A(AfroPalette.Vermilion, 0.35f), 2f);

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

        /// <summary>
        /// Dessine le dos (couleur) et sa carte de lueur (émission) : bleu nuit Art déco, croisillons laiton,
        /// rosace centrale façon panneau de station barrée du mot CHECK, éventails à gradins en haut et en bas.
        /// </summary>
        public static void PaintBack(Paint2D p, Paint2D glow)
        {
            const float W = BackW, H = BackH;
            const float cx = W * 0.5f, cy = H * 0.5f;
            p.Fill(NightDeep);
            glow.Fill(Color.black);
            p.Shape(0f, 0f, W, H, (x, y) => -1f, (x, y) =>
            {
                float dx = (x - cx) / cx, dy = (y - cy) / cy;
                float k = Mathf.Clamp01(1f - (dx * dx + dy * dy) * 0.7f);
                return A(Night, k * 0.9f);
            });

            // Croisillons laiton très fins (losanges) avec un point à chaque nœud.
            const float fx0 = 20f, fy0 = 20f, fx1 = W - 20f, fy1 = H - 20f;
            const float s = 22f;
            p.Shape(fx0, fy0, fx1, fy1, (x, y) =>
            {
                float u = (x - cx + y - cy) * 0.7071f, v = (x - cx - y + cy) * 0.7071f;
                float du = Mathf.Abs(Mathf.Repeat(u, s) - s * 0.5f), dv = Mathf.Abs(Mathf.Repeat(v, s) - s * 0.5f);
                return Mathf.Min(du, dv) - 0.45f;
            }, A(Brass, 0.28f));

            // Éventails à gradins (haut, et bas par symétrie).
            for (int pass = 0; pass < 2; pass++)
            {
                float dir = pass == 0 ? 1f : -1f;
                float by = cy + dir * 112f;
                for (int i = 0; i < 9; i++)
                {
                    float a = Mathf.Lerp(30f, 150f, i / 8f) * Mathf.Deg2Rad;
                    float len = i % 2 == 0 ? 44f : 32f;
                    p.Line(cx + Mathf.Cos(a) * 10f, by + dir * Mathf.Sin(a) * 10f, cx + Mathf.Cos(a) * len, by + dir * Mathf.Sin(a) * len, i % 2 == 0 ? 1.2f : 0.7f, Brass);
                }
                for (int k = 0; k < 3; k++)
                {
                    float hw = 30f - k * 9f, y = by - dir * (6f + k * 6f);
                    p.Line(cx - hw, y, cx + hw, y, 1.3f, k == 1 ? Teal : Brass);
                }
            }

            // Rosace centrale : disque pétrole, double anneau laiton, rayons, bandeau « CHECK ».
            const float R = 58f;
            p.Circle(cx, cy, R + 6f, NightDeep, 1.5f);
            p.Circle(cx, cy, R, Petrol);
            for (int i = 0; i < 32; i++)
            {
                float a = i * Mathf.PI * 2f / 32f;
                p.Line(cx + Mathf.Cos(a) * 22f, cy + Mathf.Sin(a) * 22f, cx + Mathf.Cos(a) * (R - 8f), cy + Mathf.Sin(a) * (R - 8f), 0.7f, A(BrassLight, 0.45f));
            }
            p.CircleStroke(cx, cy, R, 2.4f, Brass);
            p.CircleStroke(cx, cy, R + 5f, 0.8f, A(Brass, 0.8f));
            p.CircleStroke(cx, cy, R - 6f, 0.7f, A(BrassLight, 0.8f));
            glow.CircleStroke(cx, cy, R, 2.6f, AfroPalette.NeonAmber * 0.18f);

            const float barH = 15f;
            p.RoundRect(cx - R - 14f, cy - barH, cx + R + 14f, cy + barH, 3f, NightDeep);
            p.RoundRectStroke(cx - R - 14f, cy - barH, cx + R + 14f, cy + barH, 3f, 1.4f, Brass);
            const float th = 18f, tw = 0.62f, weight = 0.12f;
            float width = StrokeFont.Measure("CHECK", th, tw, 0.22f);
            StrokeFont.Draw(p, "CHECK", cx - width * 0.5f, cy - th * 0.5f, th, tw, weight, AfroPalette.Ivory, 0.22f);
            StrokeFont.Draw(glow, "CHECK", cx - width * 0.5f, cy - th * 0.5f, th, tw, weight, AfroPalette.NeonAmber * 0.22f, 0.22f);

            // Bordure : tranche laiton, filets, losanges de coin.
            p.Stroke(0f, 0f, W, H, (x, y) => -Sdf.RoundBox(x - cx, y - cy, cx, cy, 2f), 7f, Brass, 1f);
            p.RoundRectStroke(11f, 11f, W - 11f, H - 11f, 8f, 1.3f, Brass);
            p.RoundRectStroke(15.5f, 15.5f, W - 15.5f, H - 15.5f, 5f, 0.6f, A(Teal, 0.7f));
            for (int k = 0; k < 4; k++)
            {
                float x = k % 2 == 0 ? 11f : W - 11f, y = k < 2 ? 11f : H - 11f;
                p.Rhombus(x, y, 4f, 4f, Brass);
                p.Rhombus(x, y, 1.6f, 1.6f, Teal);
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
