using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Check.Core;
using Check.Game.App;
using Check.Game.Audio;
using Check.Game.Match;
using Check.Game.View;
using UnityEngine;
using UnityEngine.UI;

namespace Check.Game.UI
{
    /// <summary>
    /// Interface construite par code (uGUI) : bandeau d'état, journal, badges des joueurs suivant la table 3D,
    /// indicateurs près du tas (couleur demandée, pénalité), boutons d'action du joueur, et les écrans
    /// (accueil, nouvelle partie, règles, pause, Valet, fin de tour, fin de partie) dans <c>HudScreens.cs</c>.
    /// Référence 1920 × 1080 en paysage, zone sûre respectée (encoches).
    /// </summary>
    public sealed partial class Hud : MonoBehaviour
    {
        GameController game;
        CardTable table;
        HumanTurn human;
        SoundBank sfx;

        Canvas canvas;
        CanvasScaler scaler;
        RectTransform root, safeRoot, badgeLayer;
        Rect appliedSafeArea;

        GameObject hudGroup;
        Text status, hint, roundInfo, logText, pileLabel;
        Button menuButton, speedButton;
        public Button PlayButton { get; private set; }
        public Button CheckButton { get; private set; }
        /// <summary>Piocher / Prendre N cartes / Passer, selon la situation.</summary>
        public Button DrawButton { get; private set; }
        Image checkFill;
        Text checkLabel, drawLabel, playLabel;
        RectTransform suitIndicator, penaltyIndicator;
        Text suitText, suitCaption, penaltyText;

        SeatBadge[] badges = Array.Empty<SeatBadge>();
        SeatBadge humanBadge;
        RectTransform humanBubbleAnchor;

        readonly List<string> log = new List<string>();
        string flashText;
        float flashUntil;
        string eventStatus;
        float eventStatusUntil;

        public void Build(GameController controller, CardTable cardTable, HumanTurn humanTurn, SoundBank sounds)
        {
            game = controller;
            table = cardTable;
            human = humanTurn;
            sfx = sounds;

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            root = (RectTransform)transform;

            badgeLayer = UIKit.Rect("Badges", transform);
            UIKit.Stretch(badgeLayer);
            safeRoot = UIKit.Rect("SafeArea", transform);
            UIKit.Stretch(safeRoot);

            BuildHud();
            BuildScreens();
            BuildTips();

            human.UiBlocked = () => IsBlockingOverlayOpen;
            human.AskSuit = AskSuit;
            human.Refused += msg => Flash(msg);
            table.EventShown += OnEventShown;
            game.GameStarted += OnGameStarted;
            game.RoundOver += OnRoundOver;
            game.MatchOver += OnMatchOver;
            UpdateLayout();
        }

        // ---------------------------------------------------------------- Construction du HUD

        void BuildHud()
        {
            hudGroup = UIKit.Rect("Hud", safeRoot).gameObject;
            UIKit.Stretch((RectTransform)hudGroup.transform);
            var hud = hudGroup.transform;

            menuButton = UIKit.Button("Menu", hud, "Menu", () => { sfx.Play(Sfx.Click); ShowPause(); }, UIKit.Gold, 34);
            UIKit.Anchor((RectTransform)menuButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -20), new Vector2(170, 84));

            // Bandeau d'état.
            var banner = UIKit.Panel("StatusBanner", hud, UIKit.GlassStrong, true, UIKit.Gold);
            UIKit.Anchor(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(1060, 112));
            status = UIKit.Label("Status", banner.transform, "", 42, UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(status.rectTransform, 20, 46, 20, 6);
            status.supportRichText = true;
            status.resizeTextForBestFit = true;
            status.resizeTextMinSize = 22;
            status.resizeTextMaxSize = 42;
            hint = UIKit.Label("Hint", banner.transform, "", 28, UIKit.TextDim, TextAnchor.MiddleCenter);
            UIKit.Stretch(hint.rectTransform, 20, 8, 20, 64);
            hint.supportRichText = true;
            hint.resizeTextForBestFit = true;
            hint.resizeTextMinSize = 18;
            hint.resizeTextMaxSize = 28;

            // En haut à droite : tour en cours, vitesse.
            roundInfo = UIKit.Label("RoundInfo", hud, "", 28, UIKit.TextDim, TextAnchor.MiddleRight, FontStyle.Bold);
            UIKit.Anchor(roundInfo.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24, -20), new Vector2(330, 40));
            speedButton = UIKit.Button("Speed", hud, "Vitesse ×1", () =>
            {
                sfx.Play(Sfx.Click);
                game.SetSpeed(game.Settings.Speed == AiSpeed.Fast ? AiSpeed.Normal : AiSpeed.Fast);
                SavePreferredSpeed();
            }, UIKit.Cyan, 28);
            UIKit.Anchor((RectTransform)speedButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24, -66), new Vector2(230, 70));

            // Journal des derniers coups.
            var logPanel = UIKit.Panel("Log", hud, new Color(0.03f, 0.03f, 0.08f, 0.85f), true, UIKit.Indigo * 2f);
            UIKit.Anchor(logPanel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -120), new Vector2(420, 210));
            logPanel.raycastTarget = false;
            logText = UIKit.Label("Lines", logPanel.transform, "", 25, UIKit.TextDim, TextAnchor.UpperLeft);
            UIKit.Stretch(logText.rectTransform, 18, 10, 14, 12);
            logText.supportRichText = true;
            logText.horizontalOverflow = HorizontalWrapMode.Overflow;
            logText.verticalOverflow = VerticalWrapMode.Truncate;

            // Indicateurs près du tas (placés à chaque image sur la projection du tas).
            suitIndicator = UIKit.Panel("RequestedSuit", hud, UIKit.GlassStrong, true, UIKit.Gold).rectTransform;
            suitIndicator.sizeDelta = new Vector2(170, 190);
            suitText = UIKit.Label("Suit", suitIndicator, "♥", 120, UIKit.SuitRed, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(suitText.rectTransform, 0, 40, 0, 0);
            suitCaption = UIKit.Label("Caption", suitIndicator, "demandé", 26, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(suitCaption.rectTransform, 0, 8, 0, 140);
            suitIndicator.gameObject.SetActive(false);

            penaltyIndicator = UIKit.Panel("Penalty", hud, new Color(0.22f, 0.08f, 0.02f, 0.92f), true, UIKit.Amber).rectTransform;
            penaltyIndicator.sizeDelta = new Vector2(170, 150);
            penaltyText = UIKit.Label("Count", penaltyIndicator, "+2", 72, UIKit.Amber, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(penaltyText.rectTransform, 0, 40, 0, 4);
            UIKit.Stretch(UIKit.Label("Caption", penaltyIndicator, "à piocher", 26, UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform, 0, 8, 0, 100);
            penaltyIndicator.gameObject.SetActive(false);

            pileLabel = UIKit.Label("PileCount", hud, "", 24, UIKit.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
            pileLabel.rectTransform.sizeDelta = new Vector2(220, 34);
            pileLabel.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.9f);

            // Boutons d'action du joueur, en bas à droite (les boutons cachés ne prennent pas de place).
            var actions = UIKit.Rect("Actions", hud);
            UIKit.Anchor(actions, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24, 24), new Vector2(310, 420));
            var v = UIKit.VStack(actions, 16);
            v.childAlignment = TextAnchor.LowerCenter;

            CheckButton = UIKit.Button("Check", actions, "CHECK !", () => { human.PressCheck(); if (human.CheckArmed) Haptics.Medium(); }, UIKit.Magenta, 52);
            UIKit.Size(CheckButton, 132);
            checkFill = UIKit.Panel("Fill", CheckButton.transform, UIKit.Magenta * new Color(1, 1, 1, 0.55f), false);
            UIKit.Stretch(checkFill.rectTransform, 4, 4, 4, 4);
            checkFill.raycastTarget = false;
            checkFill.transform.SetSiblingIndex(1);
            checkLabel = CheckButton.GetComponentInChildren<Text>();
            checkLabel.transform.SetAsLastSibling();

            PlayButton = UIKit.Button("Play", actions, "Poser", () => { human.PressPlay(); Haptics.Light(); }, UIKit.Cyan, 46);
            UIKit.Size(PlayButton, 112);
            playLabel = PlayButton.GetComponentInChildren<Text>();
            DrawButton = UIKit.Button("Draw", actions, "Piocher", OnDrawButton, UIKit.Gold, 38);
            UIKit.Size(DrawButton, 100);
            drawLabel = DrawButton.GetComponentInChildren<Text>();

            // Le joueur humain : badge en bas à gauche, bulle au-dessus de sa main.
            humanBadge = new SeatBadge(hud, true);
            UIKit.Anchor(humanBadge.Root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24, 24), new Vector2(270, 104));
            humanBubbleAnchor = UIKit.Rect("HumanBubble", hud);
            UIKit.Anchor(humanBubbleAnchor, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(214, 236), new Vector2(10, 10));
        }

        void OnDrawButton()
        {
            Haptics.Light();
            if (human.HasDrawn && human.PendingDraw == 0) human.Pass();
            else human.Draw();
        }

        void SavePreferredSpeed()
        {
            var prefs = GameSettings.Load();
            prefs.Speed = game.Settings.Speed;
            prefs.Save();
        }

        // ---------------------------------------------------------------- Badges des joueurs

        sealed class SeatBadge
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Ring, Avatar, MarkPanel;
            public Text Initial, Name, Count, Mark;
            public RectTransform Bubble;
            public Text BubbleText;
            public Image BubblePanel;
            public float BubbleUntil;
            public float BubbleStart;

            public SeatBadge(Transform parent, bool human)
            {
                var panel = UIKit.Panel(human ? "HumanBadge" : "Badge", parent, new Color(0.05f, 0.05f, 0.12f, 0.8f), true, UIKit.Indigo * 2.2f);
                panel.raycastTarget = false;
                Root = panel.rectTransform;
                Root.sizeDelta = new Vector2(250, 96);
                Group = Root.gameObject.AddComponent<CanvasGroup>();
                Group.blocksRaycasts = false;

                Ring = new GameObject("Ring", typeof(RectTransform)).AddComponent<Image>();
                Ring.transform.SetParent(Root, false);
                Ring.sprite = UIKit.Glow;
                Ring.color = UIKit.Cyan;
                Ring.raycastTarget = false;
                UIKit.Anchor(Ring.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(50, 0), new Vector2(150, 150));

                Avatar = new GameObject("Avatar", typeof(RectTransform)).AddComponent<Image>();
                Avatar.transform.SetParent(Root, false);
                Avatar.sprite = UIKit.Circle;
                Avatar.color = UIKit.Indigo;
                Avatar.raycastTarget = false;
                UIKit.Anchor(Avatar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(50, 0), new Vector2(76, 76));
                var avatarRing = new GameObject("AvatarRing", typeof(RectTransform)).AddComponent<Image>();
                avatarRing.transform.SetParent(Avatar.transform, false);
                avatarRing.sprite = UIKit.RoundedOutline;
                avatarRing.type = Image.Type.Sliced;
                avatarRing.color = UIKit.Gold;
                avatarRing.raycastTarget = false;
                UIKit.Stretch(avatarRing.rectTransform, -3, -3, -3, -3);
                avatarRing.pixelsPerUnitMultiplier = 0.55f;

                Initial = UIKit.Label("Initial", Avatar.transform, "?", 38, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(Initial.rectTransform);

                Name = UIKit.Label("Name", Root, "", 28, UIKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
                UIKit.Stretch(Name.rectTransform, 100, 46, 10, 6);
                Name.horizontalOverflow = HorizontalWrapMode.Overflow;
                Name.resizeTextForBestFit = true;
                Name.resizeTextMinSize = 16;
                Name.resizeTextMaxSize = 28;
                Name.horizontalOverflow = HorizontalWrapMode.Wrap;
                Count = UIKit.Label("Count", Root, "", 24, UIKit.Cyan, TextAnchor.MiddleLeft, FontStyle.Bold);
                UIKit.Stretch(Count.rectTransform, 100, 8, 10, 50);

                MarkPanel = UIKit.Panel("Mark", Root, new Color(0.05f, 0.2f, 0.12f, 0.95f), true, UIKit.Success);
                // Badge du joueur (en bas de l'écran) : la marque passe au-dessus.
                if (human) UIKit.Anchor(MarkPanel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(190, 44));
                else UIKit.Anchor(MarkPanel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, 8), new Vector2(190, 44));
                MarkPanel.raycastTarget = false;
                Mark = UIKit.Label("Text", MarkPanel.transform, "", 24, UIKit.Success, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(Mark.rectTransform);
                MarkPanel.gameObject.SetActive(false);

                BubblePanel = UIKit.Panel("Bubble", Root, new Color(0.06f, 0.05f, 0.14f, 0.96f), true, UIKit.Cyan);
                Bubble = BubblePanel.rectTransform;
                BubblePanel.raycastTarget = false;
                UIKit.Anchor(Bubble, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(240, 66));
                BubbleText = UIKit.Label("Text", Bubble, "", 34, UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(BubbleText.rectTransform, 10, 2, 10, 2);
                BubbleText.supportRichText = true;
                BubbleText.resizeTextForBestFit = true;
                BubbleText.resizeTextMinSize = 18;
                BubbleText.resizeTextMaxSize = 36;
                Bubble.gameObject.SetActive(false);
            }

            bool bubbleBelow;

            public void SetBubbleBelow(bool below)
            {
                if (below == bubbleBelow) return;
                bubbleBelow = below;
                if (below) UIKit.Anchor(Bubble, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(240, 66));
                else UIKit.Anchor(Bubble, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(240, 66));
            }

            public void Say(string text, Color color, float seconds)
            {
                BubbleText.text = text;
                BubbleText.color = color;
                var outline = BubblePanel.transform.Find("Outline")?.GetComponent<Image>();
                if (outline != null) outline.color = color * new Color(1, 1, 1, 0.8f);
                Bubble.gameObject.SetActive(true);
                BubbleStart = Time.unscaledTime;
                BubbleUntil = Time.unscaledTime + seconds;
            }

            public void Tick()
            {
                if (!Bubble.gameObject.activeSelf) return;
                float t = Time.unscaledTime;
                if (t > BubbleUntil) { Bubble.gameObject.SetActive(false); return; }
                float pop = Mathf.Clamp01((t - BubbleStart) / 0.16f);
                float s = pop < 1f ? Mathf.Lerp(0.5f, 1.08f, pop) : Mathf.Lerp(1.08f, 1f, Mathf.Clamp01((t - BubbleStart - 0.16f) / 0.1f));
                Bubble.localScale = Vector3.one * s;
            }
        }

        void OnGameStarted()
        {
            for (int p = 1; p < badges.Length; p++)
                if (badges[p] != null) Destroy(badges[p].Root.gameObject);
            int n = game.PlayerCount;
            badges = new SeatBadge[n];
            for (int p = 1; p < n; p++)
            {
                var b = new SeatBadge(badgeLayer, false);
                // Beaucoup de joueurs : badges plus compacts pour ne pas se chevaucher.
                b.Root.localScale = Vector3.one * (n <= 5 ? 1f : n <= 7 ? 0.86f : 0.74f);
                b.Name.text = game.NameOf(p);
                b.Initial.text = Initial(game.NameOf(p));
                b.Avatar.color = AvatarColor(p);
                badges[p] = b;
            }
            badges[0] = humanBadge;
            humanBadge.Name.text = "Toi";
            humanBadge.Initial.text = "★";
            humanBadge.Avatar.color = new Color(0.30f, 0.14f, 0.05f);
            log.Clear();
            logText.text = "";
            flashUntil = 0f;
            eventStatusUntil = 0f;
            HideGameScreens();
        }

        static string Initial(string name)
        {
            var words = name.Split(' ');
            var w = words[words.Length - 1];
            return w.Length > 0 ? char.ToUpperInvariant(w[0]).ToString() : "?";
        }

        static Color AvatarColor(int p)
        {
            Color[] palette =
            {
                new Color(0.20f, 0.10f, 0.45f), new Color(0.05f, 0.30f, 0.38f), new Color(0.40f, 0.12f, 0.30f),
                new Color(0.35f, 0.22f, 0.05f), new Color(0.10f, 0.28f, 0.14f), new Color(0.38f, 0.10f, 0.10f),
            };
            return palette[p % palette.Length];
        }

        // ---------------------------------------------------------------- Événements animés → bulles, état, journal

        void OnEventShown(ViewEvent ev)
        {
            var e = ev.E;
            int p = ev.Player;
            string who = p >= 0 ? game.NameOf(p) : "";
            bool me = p == GameController.HumanId;
            switch (e.Kind)
            {
                case GameEventKind.RoundStarted:
                    log.Clear();
                    SetEventStatus($"Tour {ev.RoundNumber} : distribution de {e.Count} cartes", 1.5f);
                    AddLog($"<color=#FFCC5C>— Tour {ev.RoundNumber} —</color>");
                    break;
                case GameEventKind.TurnStarted:
                    eventStatusUntil = 0f;
                    break;
                case GameEventKind.Played:
                    AddLog($"{who} : {string.Join(" ", e.Cards.Select(CardText))}");
                    if (e.Cards.Count > 1) Say(p, e.Cards.Count == 2 ? "Doublé !" : e.Cards.Count == 3 ? "Triplé !" : "Carré !", UIKit.Cyan, 1.2f);
                    if (e.Cards[0].Rank == Rank.Ace)
                        SetEventStatus(me ? "As : le suivant passe son tour" : $"{who} pose un As", 1.4f);
                    if (me) Haptics.Light();
                    break;
                case GameEventKind.PlayedUnder:
                    AddLog($"{who} : {string.Join(" ", e.Cards.Select(CardText))} (dessous)");
                    Say(p, "Passe-partout", UIKit.Cyan, 1.2f);
                    SetEventStatus(me ? "Ton 2 se glisse sous le tas" : $"{who} glisse un 2 sous le tas", 1.4f);
                    break;
                case GameEventKind.Drew:
                    AddLog($"{who} : pioche");
                    Say(p, "Pioche", UIKit.TextDim, 0.9f);
                    break;
                case GameEventKind.PenaltyDrawn:
                    AddLog($"{who} : <color=#FFA22E>+{e.Count}</color>");
                    Say(p, $"+{e.Count}", UIKit.Amber, 1.6f);
                    SetEventStatus(me ? $"Tu pioches {e.Count} cartes !" : $"{who} pioche {e.Count} cartes !", 1.6f);
                    if (me) Haptics.Medium();
                    break;
                case GameEventKind.PenaltyStacked:
                    SetEventStatus($"Pénalité : {e.Count} cartes à piocher !", 1.4f);
                    break;
                case GameEventKind.Skipped:
                    AddLog($"{who} : saute son tour");
                    Say(p, "Saute !", UIKit.Magenta, 1.4f);
                    SetEventStatus(me ? "Tu passes ton tour !" : $"{who} passe son tour !", 1.3f);
                    break;
                case GameEventKind.SuitRequested:
                    if (e.Suit.HasValue)
                    {
                        Say(p, SuitText(e.Suit.Value) + " !", UIKit.Gold, 1.6f);
                        SetEventStatus($"Couleur demandée : {SuitText(e.Suit.Value)}", 1.8f);
                    }
                    break;
                case GameEventKind.Passed:
                    AddLog($"{who} : passe");
                    Say(p, "Passe", UIKit.TextDim, 0.9f);
                    break;
                case GameEventKind.CheckDeclared:
                    AddLog($"{who} : <color=#4DEBFF>Check !</color>");
                    Say(p, "Check !", UIKit.Cyan, 1.8f);
                    SetEventStatus(me ? "Check ! Plus qu'une carte" : $"{who} : Check ! Plus qu'une carte", 1.6f);
                    break;
                case GameEventKind.CheckForgotten:
                    AddLog($"{who} : Check oublié <color=#FFA22E>+{e.Count}</color>");
                    Say(p, $"Check oublié ! +{e.Count}", UIKit.Amber, 2f);
                    SetEventStatus(me ? $"Tu as oublié le Check : +{e.Count} cartes" : $"{who} a oublié le Check : +{e.Count}", 2f);
                    if (me) Haptics.Medium();
                    break;
                case GameEventKind.Finished:
                    AddLog($"<color=#FFCC5C>{who} : Games !</color>");
                    Say(p, "Games !", UIKit.Gold, 2.2f);
                    SetEventStatus(me ? "Games ! Tu es qualifié pour le tour suivant" : $"{who} : Games ! Qualifié", 2f);
                    break;
                case GameEventKind.Eliminated:
                    AddLog($"<color=#FF5A6E>{who} : éliminé</color>");
                    SetEventStatus(me ? "Tu es éliminé…" : $"{who} est éliminé !", 2.5f);
                    break;
                case GameEventKind.Reshuffled:
                    SetEventStatus("La pioche est refaite avec le tas", 1.2f);
                    break;
                case GameEventKind.MatchWon:
                    SetEventStatus(me ? "Tu gagnes la partie !" : $"{who} gagne la partie !", 5f);
                    break;
            }
            var tip = FirstGameTips.ForEvent(e);
            if (tip.HasValue) TryTip(tip.Value, false);
        }

        void Say(int player, string text, Color color, float seconds)
        {
            if (player < 0 || player >= badges.Length || badges[player] == null) return;
            badges[player].Say(text, color, seconds);
        }

        void AddLog(string line)
        {
            log.Add(line);
            while (log.Count > 6) log.RemoveAt(0);
            logText.text = string.Join("\n", log);
            var panel = (RectTransform)logText.transform.parent;
            // Hauteur réelle du texte : la police système d'Android a des lignes plus hautes que celle du Mac.
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, 24f + Mathf.Max(31f * log.Count, logText.preferredHeight));
        }

        void SetEventStatus(string text, float seconds)
        {
            eventStatus = text;
            eventStatusUntil = Time.unscaledTime + seconds;
        }

        /// <summary>Message d'explication temporaire sous le bandeau (action refusée…).</summary>
        public void Flash(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            flashText = text;
            flashUntil = Time.unscaledTime + 2.6f;
        }

        public static string SuitText(Suit s) =>
            Card.ColorOf(s) == CardColor.Red ? $"<color=#FF5C73>{Card.SuitSymbol(s)}</color>" : Card.SuitSymbol(s);

        public static string CardText(Card c)
        {
            if (c.IsJoker) return c.Color == CardColor.Red ? "<color=#FF5C73>Joker</color>" : "Joker";
            string label = Card.RankLabel(c.Rank) + Card.SuitSymbol(c.Suit);
            return c.Color == CardColor.Red ? $"<color=#FF5C73>{label}</color>" : label;
        }

        // ---------------------------------------------------------------- Mise à jour

        void Update()
        {
            UpdateLayout();
            bool menuLike = homeOverlay.activeSelf || setupOverlay.activeSelf || pauseOverlay.activeSelf ||
                            (rulesOverlay.activeSelf && !game.HasGame) || (rulesOverlay.activeSelf && rulesFromPause);
            game.Paused = menuLike;
            AnimatePodium();
            Time.timeScale = menuLike ? 0f : game.Turbo ? 3f : 1f;
            human.Tick();

            bool inGame = game.HasGame && !homeOverlay.activeSelf && !setupOverlay.activeSelf;
            hudGroup.SetActive(inGame);
            badgeLayer.gameObject.SetActive(inGame);
            sfx.SetTension(inGame ? RumbleSynth.Tension(ActiveHandCounts()) : 0f);
            if (!inGame) return;

            RefreshStatus();
            RefreshBadges();
            RefreshPileIndicators();
            RefreshActions();
            RefreshTips();
            roundInfo.text = game.Match != null ? $"Tour {game.Match.RoundNumber} · {game.Match.Alive.Count} en jeu" : "";
            UIKit.SetButtonText(speedButton, game.Settings.Speed == AiSpeed.Fast ? "Vitesse ×2" : "Vitesse ×1");
        }

        /// <summary>Cartes en main des joueurs encore en course dans le tour affiché.</summary>
        IEnumerable<int> ActiveHandCounts()
        {
            for (int p = 0; p < table.PlayerCount; p++)
            {
                bool outOfRound = (p < table.Eliminated.Length && table.Eliminated[p]) || (p < table.Qualified.Length && table.Qualified[p]);
                int n = table.HandCount(p);
                if (!outOfRound && n > 0) yield return n;
            }
        }

        void RefreshStatus()
        {
            string text;
            Color color = UIKit.Text;
            string sub = "";
            var r = game.Round;
            if (Time.unscaledTime < eventStatusUntil) text = eventStatus;
            else if (r == null) text = "";
            else if (r.IsOver && !table.IsBusy) text = game.Match.IsOver ? $"{game.NameOf(game.Match.Winner ?? 0)} gagne la partie" : "Fin du tour";
            else if (table.ActivePlayer == GameController.HumanId && game.IsHumanTurn && !table.IsBusy)
            {
                text = "À toi de jouer";
                color = UIKit.Gold;
                if (r.PendingDraw > 0)
                {
                    text = r.PendingKind == PenaltyKind.Joker ? $"Joker ! Prends {r.PendingDraw} cartes" : $"Contre avec un 7 ou prends {r.PendingDraw} cartes";
                    color = UIKit.Amber;
                }
                else if (r.HasDrawn) sub = "Pose la carte piochée ou passe";
                else if (r.RequestedSuit.HasValue) sub = $"Couleur demandée : {SuitText(r.RequestedSuit.Value)}  (ou un Valet)";
                else sub = r.Top.IsJoker
                    ? $"Après le joker : une carte {(r.Top.Color == CardColor.Black ? "noire ♠ ♣" : "rouge <color=#FF5C73>♥ ♦</color>")}"
                    : $"Même couleur ou même valeur que {CardText(r.Top)} · double-tap pour poser";
            }
            else if (table.ActivePlayer > 0)
            {
                text = game.IsAIThinking ? $"{game.NameOf(table.ActivePlayer)} réfléchit…" : $"{game.NameOf(table.ActivePlayer)} joue…";
                if (!game.HumanAlive) sub = "Tu regardes la fin de la partie";
                else if (game.HumanSeat >= 0 && r.IsFinished(game.HumanSeat)) sub = "Tu es qualifié : les autres finissent le tour";
            }
            else text = "";

            status.text = text;
            status.color = color;
            if (Time.unscaledTime < flashUntil)
            {
                hint.text = flashText;
                hint.color = UIKit.Amber;
            }
            else
            {
                hint.text = sub;
                hint.color = UIKit.TextDim;
            }
            logText.transform.parent.gameObject.SetActive(log.Count > 0);
        }

        void RefreshBadges()
        {
            float t = Time.unscaledTime;
            var canvasRect = root.rect;
            for (int p = 0; p < badges.Length; p++)
            {
                var b = badges[p];
                if (b == null) continue;
                bool active = table.ActivePlayer == p;
                bool eliminated = p < table.Eliminated.Length && table.Eliminated[p];
                bool qualified = p < table.Qualified.Length && table.Qualified[p];
                int count = table.HandCount(p);
                b.Count.text = eliminated ? "" : count == 0 ? (qualified ? "" : "—") : count == 1 ? "1 carte" : $"{count} cartes";
                b.Count.color = count == 1 ? UIKit.Magenta : UIKit.Cyan;
                b.Ring.enabled = active;
                if (active)
                {
                    float pulse = 0.75f + 0.25f * Mathf.Sin(t * 5f);
                    b.Ring.color = (p == 0 ? UIKit.Gold : UIKit.Cyan) * new Color(1, 1, 1, pulse);
                    b.Ring.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(t * 5f));
                }
                b.Group.alpha = eliminated ? 0.45f : 1f;
                if (eliminated || qualified)
                {
                    b.MarkPanel.gameObject.SetActive(true);
                    b.Mark.text = eliminated ? "ÉLIMINÉ" : "QUALIFIÉ";
                    b.Mark.color = eliminated ? UIKit.Danger : UIKit.Success;
                    b.MarkPanel.color = eliminated ? new Color(0.25f, 0.04f, 0.06f, 0.95f) : new Color(0.04f, 0.2f, 0.1f, 0.95f);
                }
                else b.MarkPanel.gameObject.SetActive(false);
                b.Tick();

                if (p == 0)
                {
                    // Bulle du joueur : au-dessus de sa main, au centre.
                    var bubble = b.Bubble;
                    if (bubble.parent != humanBubbleAnchor) bubble.SetParent(humanBubbleAnchor, false);
                    bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 0.5f);
                    bubble.pivot = new Vector2(0.5f, 0.5f);
                    bubble.anchoredPosition = Vector2.zero;
                    bubble.sizeDelta = new Vector2(380, 76);
                    continue;
                }
                // Badge adverse : au-delà du bord extérieur de son éventail (projection), gardé dans l'écran.
                if (!table.SeatScreenAnchors(p, out var centerSp, out var edgeSp)) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, centerSp, null, out var centerLocal);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, edgeSp, null, out var local);
                var size = b.Root.sizeDelta * b.Root.localScale.x;
                var dir = local - centerLocal;
                dir = dir.sqrMagnitude > 1f ? dir.normalized : Vector2.up;
                local += dir * (Mathf.Abs(dir.x) * size.x * 0.5f + Mathf.Abs(dir.y) * size.y * 0.5f + 6f);
                float halfW = canvasRect.width * 0.5f - size.x * 0.5f - 12f;
                float halfH = canvasRect.height * 0.5f - size.y * 0.5f - 12f;
                local.x = Mathf.Clamp(local.x, -halfW, halfW);
                local.y = Mathf.Clamp(local.y, -halfH, halfH - 70f);
                b.Root.anchorMin = b.Root.anchorMax = new Vector2(0.5f, 0.5f);
                b.Root.pivot = new Vector2(0.5f, 0.5f);
                b.Root.anchoredPosition = local;
                b.SetBubbleBelow(local.y > canvasRect.height * 0.12f);
            }
        }

        void RefreshPileIndicators()
        {
            // Couleur demandée, en grand à droite du tas.
            bool showSuit = table.RequestedSuit.HasValue && game.Round != null && !game.Round.IsOver;
            suitIndicator.gameObject.SetActive(showSuit);
            if (showSuit)
            {
                var s = table.RequestedSuit.Value;
                suitText.text = Card.SuitSymbol(s);
                suitText.color = Card.ColorOf(s) == CardColor.Red ? UIKit.SuitRed : UIKit.Text;
                PlaceNear(suitIndicator, table.DiscardScreenPoint, new Vector2(190, 10));
                float pulse = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 4f);
                suitIndicator.localScale = Vector3.one * pulse;
            }

            // Pénalité cumulée, à gauche de la pioche.
            bool showPenalty = table.PendingDraw > 0;
            penaltyIndicator.gameObject.SetActive(showPenalty);
            if (showPenalty)
            {
                penaltyText.text = "+" + table.PendingDraw;
                PlaceNear(penaltyIndicator, table.DrawPileScreenPoint, new Vector2(-190, 10));
                penaltyIndicator.localScale = Vector3.one * (1f + 0.06f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)));
            }

            pileLabel.text = table.PileCount > 0 ? $"Pioche · {table.PileCount}" : "Pioche vide";
            PlaceNear(pileLabel.rectTransform, table.DrawPileScreenPoint, new Vector2(0, 92));
        }

        void PlaceNear(RectTransform rt, Vector3 screen, Vector2 offset)
        {
            // Le parent est centré (pivot 0,5) : les coordonnées locales servent directement d'ancrage au centre.
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)hudGroup.transform, screen, null, out var local);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = local + offset;
        }

        void RefreshActions()
        {
            bool myTurn = game.IsHumanTurn && !table.IsBusy && !IsBlockingOverlayOpen;
            var r = game.Round;

            bool showPlay = myTurn && human.Selection.Count > 0;
            PlayButton.gameObject.SetActive(showPlay);
            if (showPlay)
            {
                PlayButton.interactable = human.SelectionPlayable;
                playLabel.text = human.Selection.Count > 1 ? $"Poser ({human.Selection.Count})" : "Poser";
            }

            bool showCheck = myTurn && human.CheckAvailable;
            CheckButton.gameObject.SetActive(showCheck);
            if (showCheck)
            {
                float t = Time.unscaledTime;
                bool armed = human.CheckArmed;
                checkFill.enabled = armed;
                checkFill.color = UIKit.Magenta * new Color(1, 1, 1, 0.6f + 0.2f * Mathf.Sin(t * 8f));
                checkLabel.text = armed ? "Check annoncé" : "CHECK !";
                checkLabel.color = armed ? Color.white : UIKit.Magenta;
                // Le bouton respire tant qu'il n'est pas touché : il faut le voir.
                CheckButton.transform.localScale = Vector3.one * (armed ? 1.04f : 1f + 0.07f * Mathf.Abs(Mathf.Sin(t * 4.5f)));
            }

            bool showDraw = myTurn && r != null;
            DrawButton.gameObject.SetActive(showDraw);
            if (showDraw)
            {
                if (r.PendingDraw > 0) drawLabel.text = $"Prendre {r.PendingDraw} cartes";
                else if (r.HasDrawn) drawLabel.text = "Passer";
                else drawLabel.text = "Piocher";
                drawLabel.color = r.PendingDraw > 0 ? UIKit.Amber : UIKit.Text;
            }
        }

        void UpdateLayout()
        {
            scaler.matchWidthOrHeight = Screen.width >= Screen.height ? 1f : 0f;
            var safe = Screen.safeArea;
            if (safe != appliedSafeArea && Screen.width > 0 && Screen.height > 0)
            {
                appliedSafeArea = safe;
                safeRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
                safeRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
                safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
            }
            // L'éventail du joueur laisse la place au badge (gauche) et aux boutons (droite).
            float width = root.rect.width;
            if (width > 1f)
            {
                float reserve = 340f + (1f - safeRoot.anchorMax.x + safeRoot.anchorMin.x) * width * 0.5f;
                table.HandMaxWidth = Mathf.Clamp(1f - 2f * reserve / width, 0.36f, 0.66f);
            }
        }
    }
}
