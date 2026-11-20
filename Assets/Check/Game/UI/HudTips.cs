using Check.Core;
using Check.Game.Match;
using UnityEngine;
using UnityEngine.UI;

namespace Check.Game.UI
{
    /// <summary>
    /// Bulles d'apprentissage de la première partie (<see cref="FirstGameTips"/>) : une bulle courte la première
    /// fois que chaque situation arrive, pointée sur le tas ou sur le bouton CHECK.
    /// </summary>
    public sealed partial class Hud
    {
        const float TipSeconds = 5.5f;

        RectTransform tipPanel, tipPointer;
        Text tipText;
        int tipsMask;
        float tipUntil, tipStart;
        bool tipOnCheck;

        void BuildTips()
        {
            tipsMask = GameSettings.Load().TipsSeen;
            var panel = UIKit.Panel("Tip", hudGroup.transform, new Color(0.10f, 0.07f, 0.03f, 0.96f), true, UIKit.Gold);
            panel.raycastTarget = false;
            tipPanel = panel.rectTransform;
            tipPanel.sizeDelta = new Vector2(560, 150);
            // Pointe (carré tourné à 45°) derrière le panneau, vers ce qui est expliqué.
            var pointer = UIKit.Rect("Pointer", tipPanel);
            var pi = pointer.gameObject.AddComponent<Image>();
            pi.sprite = UIKit.Rounded;
            pi.type = Image.Type.Sliced;
            pi.color = new Color(0.10f, 0.07f, 0.03f, 0.96f);
            pi.raycastTarget = false;
            pointer.sizeDelta = new Vector2(40, 40);
            pointer.localRotation = Quaternion.Euler(0, 0, 45f);
            pointer.SetAsFirstSibling();
            tipPointer = pointer;
            tipText = UIKit.Label("Text", tipPanel, "", 28, UIKit.Text, TextAnchor.MiddleLeft);
            UIKit.Stretch(tipText.rectTransform, 24, 12, 20, 12);
            tipText.supportRichText = true;
            tipText.resizeTextForBestFit = true;
            tipText.resizeTextMinSize = 18;
            tipText.resizeTextMaxSize = 28;
            tipPanel.gameObject.SetActive(false);
        }

        /// <summary>Montre la bulle de la situation si elle n'a jamais été vue (une seule bulle à la fois).</summary>
        void TryTip(TipId id, bool onCheckButton)
        {
            if (tipPanel == null || FirstGameTips.IsSeen(tipsMask, id)) return;
            if (tipPanel.gameObject.activeSelf && Time.unscaledTime < tipUntil) return;
            tipsMask = FirstGameTips.MarkSeen(tipsMask, id);
            pending.TipsSeen = tipsMask;
            var prefs = GameSettings.Load();
            prefs.TipsSeen = tipsMask;
            prefs.Save();
            tipText.text = FirstGameTips.Text(id, game.Match?.Rules);
            tipOnCheck = onCheckButton;
            tipStart = Time.unscaledTime;
            tipUntil = tipStart + TipSeconds;
            tipPanel.gameObject.SetActive(true);
            tipPanel.SetAsLastSibling();
        }

        /// <summary>Remet toutes les bulles à zéro (bouton « Revoir les astuces » des règles).</summary>
        void ResetTips()
        {
            tipsMask = 0;
            pending.TipsSeen = 0;
            var prefs = GameSettings.Load();
            prefs.TipsSeen = 0;
            prefs.Save();
        }

        void RefreshTips()
        {
            if (tipPanel == null) return;
            // Situations liées au tour du joueur (le contre du 7, le moment de dire Check).
            var r = game.Round;
            if (game.IsHumanTurn && !table.IsBusy && !IsBlockingOverlayOpen && r != null)
            {
                if (FirstGameTips.CanCounterSeven(r, game.HumanSeat)) TryTip(TipId.SevenCounter, false);
                else if (CheckButton.gameObject.activeSelf && !human.CheckArmed) TryTip(TipId.SayCheck, true);
            }

            if (!tipPanel.gameObject.activeSelf) return;
            float t = Time.unscaledTime;
            if (t > tipUntil || IsBlockingOverlayOpen) { tipPanel.gameObject.SetActive(false); return; }
            if (tipOnCheck)
            {
                // À gauche des boutons d'action, la pointe vers CHECK.
                tipPanel.anchorMin = tipPanel.anchorMax = new Vector2(1f, 0f);
                tipPanel.pivot = new Vector2(1f, 0.5f);
                tipPanel.anchoredPosition = new Vector2(-370, 300);
                tipPointer.anchorMin = tipPointer.anchorMax = new Vector2(1f, 0.5f);
                tipPointer.anchoredPosition = Vector2.zero;
            }
            else
            {
                // Au-dessus du tas, la pointe vers la carte posée.
                PlaceNear(tipPanel, table.DiscardScreenPoint, new Vector2(0, 250));
                tipPointer.anchorMin = tipPointer.anchorMax = new Vector2(0.5f, 0f);
                tipPointer.anchoredPosition = Vector2.zero;
            }
            float pop = Mathf.Clamp01((t - tipStart) / 0.18f);
            float fade = Mathf.Clamp01((tipUntil - t) / 0.3f);
            tipPanel.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, 1f - (1f - pop) * (1f - pop)) * Mathf.Lerp(0.95f, 1f, fade);
        }
    }
}
