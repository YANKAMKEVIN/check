using Check.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Check.Game.UI
{
    /// <summary>Appui long sur une carte (main ou dessus du tas) : la carte en grand et son effet en une ligne.</summary>
    public sealed partial class Hud
    {
        RectTransform cardHelp;
        RawImage cardHelpFace;
        Text cardHelpText;
        float cardHelpStart;

        void BuildCardHelp()
        {
            var panel = UIKit.Panel("CardHelp", hudGroup.transform, UIKit.GlassStrong, true, UIKit.Gold);
            panel.raycastTarget = false;
            cardHelp = panel.rectTransform;
            UIKit.Anchor(cardHelp, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(980, 340));
            cardHelpFace = UIKit.MiniCard("Card", cardHelp, Card.Parse("7H"));
            UIKit.Anchor(cardHelpFace.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28, 0), new Vector2(210, 292));
            cardHelpText = UIKit.Label("Effect", cardHelp, "", 36, UIKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Stretch(cardHelpText.rectTransform, 270, 24, 32, 24);
            cardHelpText.resizeTextForBestFit = true;
            cardHelpText.resizeTextMinSize = 22;
            cardHelpText.resizeTextMaxSize = 36;
            cardHelp.gameObject.SetActive(false);
        }

        /// <summary>Montre la carte en grand avec son effet, jusqu'à la relâche du doigt.</summary>
        public void ShowCardHelp(Card card)
        {
            if (cardHelp == null || IsBlockingOverlayOpen) return;
            UIKit.SetMiniCard(cardHelpFace, card);
            cardHelpText.text = CardHelp.Effect(card, game.Match?.Rules);
            cardHelpStart = Time.unscaledTime;
            cardHelp.gameObject.SetActive(true);
            cardHelp.SetAsLastSibling();
        }

        public void HideCardHelp()
        {
            if (cardHelp != null) cardHelp.gameObject.SetActive(false);
        }

        void RefreshCardHelp()
        {
            if (cardHelp == null || !cardHelp.gameObject.activeSelf) return;
            if (IsBlockingOverlayOpen) { HideCardHelp(); return; }
            float e = Mathf.Clamp01((Time.unscaledTime - cardHelpStart) / 0.15f);
            cardHelp.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, 1f - (1f - e) * (1f - e));
        }
    }
}
