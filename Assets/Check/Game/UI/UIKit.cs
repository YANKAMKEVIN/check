using System;
using Check.Core;
using Check.Game.Visuals;
using UnityEngine;
using UnityEngine.UI;

namespace Check.Game.UI
{
    /// <summary>Palette et fabriques d'éléments uGUI au style « verre sombre + liseré néon ».</summary>
    public static class UIKit
    {
        // « Dernier Métro » : les noms historiques restent, les teintes suivent le wagon
        // (Cyan = laiton clair des liserés, Magenta = bleu pétrole clair, verre = nuit bleutée).
        public static readonly Color Cyan = new Color(0.93f, 0.74f, 0.48f);
        public static readonly Color Magenta = new Color(0.47f, 0.78f, 0.80f);
        public static readonly Color Glass = new Color(0.06f, 0.10f, 0.13f, 0.80f);
        public static readonly Color GlassStrong = new Color(0.05f, 0.08f, 0.10f, 0.93f);
        public static readonly Color Text = new Color(0.90f, 0.95f, 1f);
        public static readonly Color TextDim = new Color(0.62f, 0.68f, 0.82f);
        public static readonly Color Danger = new Color(1f, 0.32f, 0.38f);
        // Afrofuturisme : indigo profond, or / cuivre, ambre.
        public static readonly Color Gold = new Color(1f, 0.80f, 0.36f);
        public static readonly Color Copper = new Color(0.90f, 0.55f, 0.32f);
        public static readonly Color Amber = new Color(1f, 0.64f, 0.18f);
        public static readonly Color Indigo = new Color(0.12f, 0.28f, 0.32f); // pétrole (liserés des badges)
        public static readonly Color Success = new Color(0.35f, 1f, 0.62f);
        /// <summary>Rouge des cœurs / carreaux, lisible sur fond sombre.</summary>
        public static readonly Color SuitRed = new Color(1f, 0.36f, 0.45f);

        static Font font;
        static Sprite rounded, roundedOutline, glow, circle, triangle;

        public static Font Font
        {
            get
            {
                if (font == null) font = OsFontWithSuits() ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        static Font displayFont;
        static bool displayLoaded;

        /// <summary>
        /// Police d'affichage Art déco (Limelight, OFL, Resources/Limelight-Regular.ttf) pour les titres, le bandeau
        /// et les écrans de fin ; les textes longs gardent <see cref="Font"/>. Repli sur celle-ci si absente.
        /// Elle n'a pas les symboles ♠ ♥ ♦ ♣ : ne pas l'utiliser pour un texte qui en contient.
        /// </summary>
        public static Font DisplayFont
        {
            get
            {
                if (!displayLoaded)
                {
                    displayLoaded = true;
                    displayFont = Resources.Load<Font>("Limelight-Regular");
                }
                return displayFont != null ? displayFont : Font;
            }
        }

        /// <summary>Passe un texte en police d'affichage (titres).</summary>
        public static Text Display(Text t)
        {
            t.font = DisplayFont;
            t.fontStyle = FontStyle.Normal; // la police est déjà grasse : pas de faux gras
            return t;
        }

        public static bool HasSuitSymbol(string text) =>
            text != null && text.IndexOfAny(new[] { '♠', '♥', '♦', '♣' }) >= 0;

        /// <summary>
        /// Sur mobile, la police intégrée n'a pas les symboles ♠ ♥ ♦ ♣ (et les polices système varient d'un
        /// téléphone à l'autre) : on embarque Noto Sans Symbols 2, qui ne contient que des symboles, et
        /// les lettres passent par son repli sur la police système (Roboto, Helvetica…), réglé à l'import.
        /// </summary>
        static Font OsFontWithSuits()
        {
            if (!Application.isMobilePlatform) return null;
            return Resources.Load<Font>("CheckSuits");
        }

        /// <summary>Rectangle arrondi blanc (9-slice), teinté par Image.color.</summary>
        public static Sprite Rounded => rounded != null ? rounded : (rounded = MakeRounded(64, 22, false));
        public static Sprite RoundedOutline => roundedOutline != null ? roundedOutline : (roundedOutline = MakeRounded(64, 22, true));
        public static Sprite Glow => glow != null ? glow : (glow = MakeGlow(64));
        /// <summary>Disque plein antialiasé (avatars, pastilles).</summary>
        public static Sprite Circle => circle != null ? circle : (circle = MakeRounded(128, 64, false));

        /// <summary>Triangle plein pointé vers la droite (pictogramme « lecture » / vitesse).</summary>
        public static Sprite Triangle => triangle != null ? triangle : (triangle = MakeTriangle(64));

        static Sprite MakeTriangle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float x0 = size * 0.18f, x1 = size * 0.86f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float half = (x1 - fx) / (x1 - x0) * size * 0.38f; // demi-hauteur du triangle à cette abscisse
                    float d = Mathf.Min(fx - x0, half - Mathf.Abs(fy - size * 0.5f));
                    float a = Mathf.Clamp01(d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeRounded(int size, int radius, bool outlineOnly)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0f);
                    float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - radius; // < 0 à l'intérieur
                    float a = Mathf.Clamp01(0.5f - d);
                    if (outlineOnly) a *= Mathf.Clamp01(d + 3.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }

        static Sprite MakeGlow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color, bool outline = true, Color? outlineColor = null)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            if (outline)
            {
                var ol = Rect("Outline", rt);
                Stretch(ol);
                var oi = ol.gameObject.AddComponent<Image>();
                oi.sprite = RoundedOutline;
                oi.type = Image.Type.Sliced;
                oi.color = (outlineColor ?? Cyan) * new Color(1, 1, 1, 0.55f);
                oi.raycastTarget = false;
                // Le liseré ne doit pas compter comme un élément des layout groups du panneau.
                ol.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button Button(string name, Transform parent, string text, Action onClick, Color? accent = null, int fontSize = 40)
        {
            var color = accent ?? Cyan;
            var img = Panel(name, parent, new Color(0.08f, 0.10f, 0.20f, 0.85f), true, color);
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.3f);
            colors.pressedColor = new Color(0.7f, 0.85f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.targetGraphic = img;
            var label = Label("Label", img.transform, text, fontSize, Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label.rectTransform, 12, 4, 12, 4);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 16;
            label.resizeTextMaxSize = fontSize;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>Petite carte dessinée avec la vraie face de l'atlas des cartes.</summary>
        public static RawImage MiniCard(string name, Transform parent, Card card)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = CardAtlas.Atlas;
            img.uvRect = CardAtlas.FaceUv(card);
            img.raycastTarget = false;
            return img;
        }

        public static void SetMiniCard(RawImage img, Card card)
        {
            img.texture = CardAtlas.Atlas;
            img.uvRect = CardAtlas.FaceUv(card);
        }

        /// <summary>
        /// Réglage oui / non : un libellé fixe et un interrupteur laiton (glissière + bouton rond) qui dit l'état.
        /// Tout le bandeau se touche ; l'état s'affiche avec <see cref="SetSwitch"/>.
        /// </summary>
        public static Button Switch(string name, Transform parent, string text, Action onClick, int fontSize = 32)
        {
            var b = Button(name, parent, text, onClick, Cyan, fontSize);
            var label = b.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            Stretch(label.rectTransform, 24, 4, 128, 4);
            var track = Panel("Track", b.transform, new Color(0.12f, 0.12f, 0.16f, 1f), true, Cyan);
            track.raycastTarget = false;
            Anchor(track.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20, 0), new Vector2(92, 46));
            var knob = new GameObject("Knob", typeof(RectTransform)).AddComponent<Image>();
            knob.transform.SetParent(track.transform, false);
            knob.sprite = Circle;
            knob.raycastTarget = false;
            Anchor(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38, 38));
            var sw = b.gameObject.AddComponent<BrassSwitch>();
            sw.Track = track;
            sw.Knob = knob;
            sw.Set(false, true);
            return b;
        }

        public static void SetSwitch(Button b, bool on)
        {
            if (b != null && b.TryGetComponent<BrassSwitch>(out var sw)) sw.Set(on, false);
        }

        /// <summary>Remplace le texte d'un bouton par un pictogramme (barres du menu, triangles de vitesse).</summary>
        public static RectTransform Pictogram(Button b)
        {
            var label = b.GetComponentInChildren<Text>();
            if (label != null) label.enabled = false;
            var holder = Rect("Pictogram", b.transform);
            Stretch(holder);
            return holder;
        }

        public static Image PictoPart(RectTransform holder, Sprite sprite, Vector2 pos, Vector2 size, Color color)
        {
            var img = new GameObject("Part", typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(holder, false);
            img.sprite = sprite;
            img.type = sprite == Rounded ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            Anchor(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            return img;
        }

        public static void SetButtonText(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static VerticalLayoutGroup VStack(RectTransform rt, float spacing, int padding = 0)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HStack(RectTransform rt, float spacing, int padding = 0)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(padding, padding, padding, padding);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            return h;
        }

        public static LayoutElement Size(Component c, float preferredHeight = -1, float preferredWidth = -1, float flexibleWidth = -1)
        {
            // Pas de « ?? » sur un objet Unity : GetComponent peut renvoyer un faux null dans l'éditeur.
            if (!c.TryGetComponent<LayoutElement>(out var le)) le = c.gameObject.AddComponent<LayoutElement>();
            if (preferredHeight >= 0) { le.preferredHeight = preferredHeight; le.minHeight = preferredHeight; }
            if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            return le;
        }
    }
}
