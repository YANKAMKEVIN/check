using Check.Game.Match;
using UnityEngine;
using UnityEngine.UI;

namespace Check.Game.UI
{
    /// <summary>Médaillons ronds avec le visage peint d'un joueur (ou son initiale s'il n'a pas de portrait).</summary>
    public sealed partial class Hud
    {
        sealed class Medallion
        {
            public RectTransform Root;
            public Image Disc, Ring;
            public RawImage Face;
            public Text Initial;
        }

        static Medallion BuildMedallion(Transform parent, float size, Color ring)
        {
            var m = new Medallion();
            m.Root = UIKit.Rect("Medallion", parent);
            m.Root.sizeDelta = new Vector2(size, size);
            // Disque masquant : le visage est découpé en rond.
            var disc = UIKit.Rect("Disc", m.Root);
            UIKit.Stretch(disc);
            m.Disc = disc.gameObject.AddComponent<Image>();
            m.Disc.sprite = UIKit.Circle;
            m.Disc.color = UIKit.Indigo;
            m.Disc.raycastTarget = false;
            disc.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var face = UIKit.Rect("Face", disc);
            UIKit.Stretch(face);
            m.Face = face.gameObject.AddComponent<RawImage>();
            m.Face.raycastTarget = false;
            m.Initial = UIKit.Label("Initial", disc, "?", Mathf.RoundToInt(size * 0.5f), UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(m.Initial.rectTransform);
            // Liseré laiton par-dessus, hors du masque (coins du 9-slice réglés pour faire un cercle).
            var ringRt = UIKit.Rect("Ring", m.Root);
            UIKit.Stretch(ringRt, -3, -3, -3, -3);
            m.Ring = ringRt.gameObject.AddComponent<Image>();
            m.Ring.sprite = UIKit.RoundedOutline;
            m.Ring.type = Image.Type.Sliced;
            m.Ring.pixelsPerUnitMultiplier = 44f / (size + 6f);
            m.Ring.color = ring;
            m.Ring.raycastTarget = false;
            return m;
        }

        /// <summary>Visage du joueur s'il en a un, sinon son initiale sur un disque coloré.</summary>
        void SetMedallion(Medallion m, int player)
        {
            if (TryGetPortrait(player, out var tex, out var uv))
            {
                m.Face.texture = tex;
                m.Face.uvRect = uv;
                m.Face.enabled = true;
                m.Initial.enabled = false;
                m.Disc.color = new Color(0.08f, 0.10f, 0.14f);
            }
            else
            {
                m.Face.enabled = false;
                m.Initial.enabled = true;
                m.Initial.text = player == GameController.HumanId ? "★" : Initial(game.NameOf(player));
                m.Disc.color = player == GameController.HumanId ? new Color(0.30f, 0.14f, 0.05f) : AvatarColor(player);
            }
        }

        bool TryGetPortrait(int player, out Texture2D texture, out Rect face) => table.TryGetPortrait(player, out texture, out face);
    }
}
