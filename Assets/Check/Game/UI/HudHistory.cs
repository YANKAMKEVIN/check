using System.Collections.Generic;
using Check.Core;
using Check.Game.Audio;
using Check.Game.View;
using UnityEngine;
using UnityEngine.UI;

namespace Check.Game.UI
{
    /// <summary>
    /// Historique complet du tour, ouvert en touchant le journal : une ligne par coup, avec les cartes posées
    /// dessinées en petit (vraies faces) et les pénalités en ambre.
    /// </summary>
    public sealed partial class Hud
    {
        readonly List<PlayLogEntry> history = new List<PlayLogEntry>();
        GameObject historyOverlay;
        RectTransform historyContent;
        ScrollRect historyScroll;

        void BuildHistory()
        {
            historyOverlay = Overlay("History", out var card, 1100, 920, UIKit.Indigo * 2.5f, 0.55f);
            var title = UIKit.Display(UIKit.Label("Title", card, "Historique du tour", 54, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold));
            UIKit.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(900, 72));

            var scrollRt = UIKit.Rect("Scroll", card);
            UIKit.Stretch(scrollRt, 40, 140, 40, 108);
            var bg = scrollRt.gameObject.AddComponent<Image>();
            bg.sprite = UIKit.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.03f, 0.03f, 0.08f, 0.8f);
            scrollRt.gameObject.AddComponent<RectMask2D>();
            historyScroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            historyScroll.horizontal = false;
            historyScroll.movementType = ScrollRect.MovementType.Clamped;
            historyScroll.scrollSensitivity = 30f;
            historyContent = UIKit.Rect("Content", scrollRt);
            historyContent.anchorMin = new Vector2(0f, 1f);
            historyContent.anchorMax = new Vector2(1f, 1f);
            historyContent.pivot = new Vector2(0.5f, 1f);
            historyContent.offsetMin = new Vector2(20, 0);
            historyContent.offsetMax = new Vector2(-20, 0);
            var v = historyContent.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(0, 0, 14, 14);
            v.spacing = 6;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            historyContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            historyScroll.content = historyContent;
            historyScroll.viewport = scrollRt;

            var close = UIKit.Button("Close", card, "Reprendre", () => { sfx.Play(Sfx.Click); Show(historyOverlay, false); }, UIKit.Gold, 40);
            UIKit.Anchor((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 28), new Vector2(380, 92));
        }

        void RecordHistory(ViewEvent ev)
        {
            var entry = PlayLogEntry.From(ev.E, ev.Player, ev.RoundNumber);
            if (entry == null) return;
            if (entry.Kind == LogKind.Round) history.Clear();
            history.Add(entry);
        }

        void ShowHistory()
        {
            if (!game.HasGame || IsBlockingOverlayOpen) return;
            foreach (Transform child in historyContent)
            {
                child.gameObject.SetActive(false); // hors de la mise en page dès maintenant (Destroy attend la fin de l'image)
                Destroy(child.gameObject);
            }
            foreach (var e in history) HistoryRow(e);
            Show(historyOverlay, true);
            Canvas.ForceUpdateCanvases();
            historyScroll.verticalNormalizedPosition = 0f; // les derniers coups en bas, visibles
        }

        void HistoryRow(PlayLogEntry e)
        {
            var row = UIKit.Rect("Row", historyContent);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            bool hasCards = e.Cards.Count > 0;
            UIKit.Size(row.gameObject.AddComponent<LayoutElement>(), hasCards ? 82 : 48);

            if (e.Kind == LogKind.Round)
            {
                var t = UIKit.Label("Round", row, $"— {e.Action} —", 30, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Size(t, 48, -1, 1f);
                return;
            }
            var name = UIKit.Label("Name", row, game.NameOf(e.Player), 28, UIKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Size(name, 44, 250);
            Color color = e.IsPenalty ? UIKit.Amber
                : e.Kind == LogKind.Eliminated ? UIKit.Danger
                : e.Kind == LogKind.Games || e.Kind == LogKind.Check ? UIKit.Gold
                : UIKit.TextDim;
            var action = UIKit.Label("Action", row, e.Action, 26, color, TextAnchor.MiddleLeft, e.IsPenalty ? FontStyle.Bold : FontStyle.Normal);
            UIKit.Size(action, 44, 230);
            foreach (var c in e.Cards) UIKit.Size(UIKit.MiniCard("Card", row, c), 76, 54);
        }
    }
}
