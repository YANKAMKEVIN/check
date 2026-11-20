using System;
using System.Collections;
using System.Linq;
using Check.Core;
using Check.Game.App;
using Check.Game.Audio;
using Check.Game.Match;
using UnityEngine;
using UnityEngine.UI;

namespace Check.Game.UI
{
    /// <summary>Écrans plein cadre du HUD : accueil, nouvelle partie, règles, pause, Valet, fin de tour, fin de partie.</summary>
    public sealed partial class Hud
    {
        GameObject homeOverlay, setupOverlay, rulesOverlay, pauseOverlay, suitOverlay, roundOverlay, matchOverlay;
        bool rulesFromPause;
        Action<Suit?> suitCallback;
        GameSettings pending;

        Button resumeSavedButton, soundButton, hapticsButton, pauseSpeedButton, pauseSoundButton;
        Button[] playerButtons, levelButtons, speedButtons;
        Text setupSummary;
        Button[] ruleButtons;
        Text roundTitle, roundBody;
        GameObject roundNextRow, roundOutRow;
        Text matchTitle, matchBody;
        Coroutine autoNext;

        // Accès pour la recette automatique (mêmes boutons que le toucher).
        public Button HomePlayButton { get; private set; }
        public Button ResumeSavedButton => resumeSavedButton;
        public Button SetupStartButton { get; private set; }
        public Button[] SetupPlayerButtons => playerButtons;
        public Button[] SuitButtons { get; private set; }
        public Button NextRoundButton { get; private set; }
        public Button WatchButton { get; private set; }
        public Button RoundReplayButton { get; private set; }
        public Button MatchReplayButton { get; private set; }
        public Button MatchMenuButton { get; private set; }
        public bool SuitPickerOpen => suitOverlay.activeSelf;
        public bool RoundScreenOpen => roundOverlay.activeSelf;
        public bool MatchScreenOpen => matchOverlay.activeSelf;
        public bool HomeOpen => homeOverlay.activeSelf;
        public bool IsBlockingRulesOpen => rulesOverlay.activeSelf;

        public bool IsBlockingOverlayOpen =>
            homeOverlay.activeSelf || setupOverlay.activeSelf || rulesOverlay.activeSelf || pauseOverlay.activeSelf ||
            suitOverlay.activeSelf || roundOverlay.activeSelf || matchOverlay.activeSelf;

        void BuildScreens()
        {
            pending = GameSettings.Load();
            Haptics.Enabled = pending.Haptics;
            BuildHome();
            BuildSetup();
            BuildRules();
            BuildPause();
            BuildSuitPicker();
            BuildRoundEnd();
            BuildMatchEnd();
        }

        GameObject Overlay(string name, out RectTransform card, float width, float height, Color accent, float dim = 0.62f)
        {
            var rt = UIKit.Rect(name, transform);
            UIKit.Stretch(rt);
            var shade = rt.gameObject.AddComponent<Image>();
            shade.color = new Color(0.02f, 0.01f, 0.06f, dim);
            rt.gameObject.AddComponent<CanvasGroup>();
            var panel = UIKit.Panel("Card", rt, new Color(0.035f, 0.03f, 0.08f, 0.985f), true, accent);
            UIKit.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            // Liseré doré intérieur : touche afrofuturiste (or sur indigo).
            var inner = UIKit.Rect("Inner", panel.transform);
            UIKit.Stretch(inner, 10, 10, 10, 10);
            var innerImg = inner.gameObject.AddComponent<Image>();
            innerImg.sprite = UIKit.RoundedOutline;
            innerImg.type = Image.Type.Sliced;
            innerImg.color = UIKit.Gold * new Color(1, 1, 1, 0.22f);
            innerImg.raycastTarget = false;
            inner.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            card = panel.rectTransform;
            rt.gameObject.SetActive(false);
            return rt.gameObject;
        }

        void Show(GameObject overlay, bool visible)
        {
            if (overlay.activeSelf == visible) return;
            overlay.SetActive(visible);
            if (visible)
            {
                overlay.transform.SetAsLastSibling();
                StartCoroutine(FadeIn(overlay));
            }
        }

        static IEnumerator FadeIn(GameObject overlay)
        {
            var group = overlay.GetComponent<CanvasGroup>();
            var card = overlay.transform.Find("Card");
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.18f)
            {
                if (overlay == null) yield break;
                float e = 1f - (1f - t) * (1f - t);
                group.alpha = e;
                if (card != null) card.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, e);
                yield return null;
            }
            group.alpha = 1f;
            if (card != null) card.localScale = Vector3.one;
        }

        static Text Title(RectTransform parent, string text, int size, Color color, float height)
        {
            var t = UIKit.Label("Title", parent, text, size, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Size(t, height);
            return t;
        }

        static RectTransform Row(RectTransform parent, float height, float spacing = 16f)
        {
            var row = UIKit.Rect("Row", parent);
            UIKit.HStack(row, spacing);
            UIKit.Size(row.gameObject.AddComponent<LayoutElement>(), height);
            return row;
        }

        static void Mark(Button b, bool selected, Color accent)
        {
            var img = b.GetComponent<Image>();
            img.color = selected ? Color.Lerp(new Color(0.08f, 0.10f, 0.20f, 0.95f), accent, 0.45f) : new Color(0.08f, 0.10f, 0.20f, 0.85f);
            var label = b.GetComponentInChildren<Text>();
            if (label != null) label.color = selected ? Color.white : UIKit.TextDim;
        }

        // ---------------------------------------------------------------- Accueil

        void BuildHome()
        {
            homeOverlay = Overlay("Home", out var card, 820, 960, UIKit.Gold, 0.45f);
            UIKit.VStack(card, 20, 48);

            var title = Title(card, "CHECK", 190, UIKit.Gold, 210);
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = UIKit.Magenta * new Color(1, 1, 1, 0.7f);
            outline.effectDistance = new Vector2(5, -5);
            var glow = title.gameObject.AddComponent<Shadow>();
            glow.effectColor = UIKit.Cyan * new Color(1, 1, 1, 0.5f);
            glow.effectDistance = new Vector2(-4, 4);
            UIKit.Size(UIKit.Label("Subtitle", card, "Le jeu de cartes du Cameroun", 36, UIKit.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold), 50);
            UIKit.Size(UIKit.Label("Pitch", card, "Débarrasse-toi de tes cartes… et n'oublie pas de dire « Check » !", 26, UIKit.TextDim), 44);

            HomePlayButton = UIKit.Button("Play", card, "Jouer", () => { sfx.Play(Sfx.Click); ShowSetup(); }, UIKit.Gold, 56);
            UIKit.Size(HomePlayButton, 124);
            resumeSavedButton = UIKit.Button("Resume", card, "Reprendre la partie", () =>
            {
                sfx.Play(Sfx.Click);
                if (game.Resume()) Show(homeOverlay, false);
                else { resumeSavedButton.gameObject.SetActive(false); Flash("Aucune partie à reprendre"); }
            }, UIKit.Cyan, 40);
            UIKit.Size(resumeSavedButton, 104);
            UIKit.Size(UIKit.Button("Rules", card, "Règles", () => { sfx.Play(Sfx.Click); ShowRules(false); }, UIKit.Cyan, 40), 100);

            var row = Row(card, 96);
            soundButton = UIKit.Button("Sound", row, "", () =>
            {
                sfx.Muted = !sfx.Muted;
                sfx.Play(Sfx.Click);
                RefreshToggles();
            }, UIKit.Cyan, 32);
            hapticsButton = UIKit.Button("Haptics", row, "", () =>
            {
                pending.Haptics = !pending.Haptics;
                Haptics.Enabled = pending.Haptics;
                pending.Save();
                sfx.Play(Sfx.Click);
                Haptics.Light();
                RefreshToggles();
            }, UIKit.Cyan, 32);

            if (Application.platform != RuntimePlatform.IPhonePlayer)
                UIKit.Size(UIKit.Button("Quit", card, "Quitter", () => { game.Persist(); Application.Quit(); }, UIKit.Magenta, 34), 84);
        }

        void RefreshToggles()
        {
            UIKit.SetButtonText(soundButton, sfx.Muted ? "Son : non" : "Son : oui");
            UIKit.SetButtonText(hapticsButton, pending.Haptics ? "Vibrations : oui" : "Vibrations : non");
            UIKit.SetButtonText(pauseSoundButton, sfx.Muted ? "Son : non" : "Son : oui");
            UIKit.SetButtonText(pauseSpeedButton, game.Settings.Speed == AiSpeed.Fast ? "Adversaires : rapides" : "Adversaires : normaux");
        }

        public void ShowHome()
        {
            HideAll();
            resumeSavedButton.gameObject.SetActive(SavedGame.Exists);
            RefreshToggles();
            Show(homeOverlay, true);
        }

        void HideAll()
        {
            foreach (var o in new[] { homeOverlay, setupOverlay, rulesOverlay, pauseOverlay, suitOverlay, roundOverlay, matchOverlay })
                o.SetActive(false);
            if (suitCallback != null) { var cb = suitCallback; suitCallback = null; cb(null); }
            if (autoNext != null) { StopCoroutine(autoNext); autoNext = null; }
        }

        void HideGameScreens()
        {
            HideAll();
        }

        // ---------------------------------------------------------------- Nouvelle partie

        void BuildSetup()
        {
            setupOverlay = Overlay("Setup", out var card, 1240, 940, UIKit.Cyan, 0.5f);
            UIKit.VStack(card, 14, 44);
            Title(card, "Nouvelle partie", 64, UIKit.Gold, 84);

            UIKit.Size(UIKit.Label("PlayersLabel", card, "Nombre de joueurs (toi compris)", 32, UIKit.TextDim), 46);
            var players = Row(card, 100, 10);
            playerButtons = new Button[RuleSet.MaxPlayers - RuleSet.MinPlayers + 1];
            for (int n = RuleSet.MinPlayers; n <= RuleSet.MaxPlayers; n++)
            {
                int count = n;
                playerButtons[n - RuleSet.MinPlayers] = UIKit.Button("P" + n, players, n.ToString(), () =>
                {
                    sfx.Play(Sfx.Click);
                    pending.PlayerCount = count;
                    RefreshSetup();
                }, UIKit.Cyan, 44);
            }

            UIKit.Size(UIKit.Label("LevelLabel", card, "Niveau des adversaires", 32, UIKit.TextDim), 46);
            var levels = Row(card, 96);
            levelButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                var level = (OpponentLevel)i;
                levelButtons[i] = UIKit.Button("L" + i, levels, GameSettings.LevelName(level), () =>
                {
                    sfx.Play(Sfx.Click);
                    pending.Level = level;
                    RefreshSetup();
                }, UIKit.Magenta, 36);
            }

            UIKit.Size(UIKit.Label("SpeedLabel", card, "Rythme des adversaires", 32, UIKit.TextDim), 46);
            var speeds = Row(card, 90);
            speedButtons = new Button[2];
            speedButtons[0] = UIKit.Button("Normal", speeds, "Normal", () => { sfx.Play(Sfx.Click); pending.Speed = AiSpeed.Normal; RefreshSetup(); }, UIKit.Gold, 34);
            speedButtons[1] = UIKit.Button("Fast", speeds, "Rapide", () => { sfx.Play(Sfx.Click); pending.Speed = AiSpeed.Fast; RefreshSetup(); }, UIKit.Gold, 34);

            setupSummary = UIKit.Label("Summary", card, "", 28, UIKit.Cyan);
            UIKit.Size(setupSummary, 52);

            var actions = Row(card, 116);
            UIKit.Button("Back", actions, "Retour", () =>
            {
                sfx.Play(Sfx.Click);
                Show(setupOverlay, false);
                if (game.HasGame && !game.Match.IsOver) ShowPause(); else ShowHome();
            }, UIKit.TextDim, 36);
            UIKit.Button("Rules", actions, "Règles", () => { sfx.Play(Sfx.Click); ShowRules(false); }, UIKit.Cyan, 36);
            SetupStartButton = UIKit.Button("Start", actions, "Jouer !", StartFromSetup, UIKit.Gold, 50);
            UIKit.Size(SetupStartButton, -1, -1, 1.6f);
        }

        void StartFromSetup()
        {
            sfx.Play(Sfx.Click);
            pending.Save();
            HideAll();
            Time.timeScale = 1f;
            game.NewGame(pending);
        }

        public void ShowSetup()
        {
            HideAll();
            pending = GameSettings.Load();
            RefreshSetup();
            Show(setupOverlay, true);
        }

        void RefreshSetup()
        {
            for (int i = 0; i < playerButtons.Length; i++) Mark(playerButtons[i], pending.PlayerCount == i + RuleSet.MinPlayers, UIKit.Cyan);
            for (int i = 0; i < levelButtons.Length; i++) Mark(levelButtons[i], (int)pending.Level == i, UIKit.Magenta);
            for (int i = 0; i < speedButtons.Length; i++) Mark(speedButtons[i], (int)pending.Speed == i, UIKit.Gold);
            int n = pending.PlayerCount;
            setupSummary.text = $"Toi + {n - 1} adversaire{(n > 2 ? "s" : "")} · {RuleSet.Default.CardsToDeal(n)} cartes chacun au premier tour";
        }

        // ---------------------------------------------------------------- Règles

        Text rulesText;
        RectTransform rulesCards;
        ScrollRect rulesScroll;
        Button[] rulesTabs;
        int rulesSheet;

        void BuildRules()
        {
            rulesOverlay = Overlay("Rules", out var card, 1380, 950, UIKit.Gold, 0.7f);
            var title = UIKit.Label("Title", card, "Règles du Check", 56, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(900, 76));

            // Fiches à gauche : un onglet par fiche, puis la fiche (cartes réelles + texte), défilante si besoin.
            var tabs = UIKit.Rect("Tabs", card);
            tabs.anchorMin = new Vector2(0f, 1f);
            tabs.anchorMax = new Vector2(0.6f, 1f);
            tabs.pivot = new Vector2(0.5f, 1f);
            tabs.offsetMin = new Vector2(44, -184);
            tabs.offsetMax = new Vector2(-16, -110);
            UIKit.HStack(tabs, 8);
            rulesTabs = new Button[RulesSheets.All.Count];
            for (int i = 0; i < rulesTabs.Length; i++)
            {
                int index = i;
                rulesTabs[i] = UIKit.Button("Tab" + i, tabs, RulesSheets.All[i].Title, () => { sfx.Play(Sfx.Click); ShowRulesSheet(index); }, UIKit.Gold, 24);
            }

            var scrollRt = UIKit.Rect("Scroll", card);
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(0.6f, 1f);
            scrollRt.offsetMin = new Vector2(44, 130);
            scrollRt.offsetMax = new Vector2(-16, -196);
            var bg = scrollRt.gameObject.AddComponent<Image>();
            bg.sprite = UIKit.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.03f, 0.03f, 0.08f, 0.8f);
            scrollRt.gameObject.AddComponent<RectMask2D>();
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            var content = UIKit.Rect("Content", scrollRt);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(24, 0);
            content.offsetMax = new Vector2(-24, 0);
            rulesCards = UIKit.Rect("Cards", content);
            var cardsRow = UIKit.HStack(rulesCards, 14);
            cardsRow.childAlignment = TextAnchor.MiddleLeft;
            cardsRow.childForceExpandWidth = false;
            UIKit.Size(rulesCards.gameObject.AddComponent<LayoutElement>(), 170);
            foreach (var c in RulesSheets.All[2].Cards)
                UIKit.Size(UIKit.MiniCard("Card", rulesCards, c), 170, 122);
            var text = UIKit.Label("Text", content, "", 28, UIKit.Text, TextAnchor.UpperLeft);
            text.supportRichText = true;
            text.lineSpacing = 1.08f;
            UIKit.Stretch(text.rectTransform, 0, 0, 0, 20);
            rulesText = text;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var cv = content.gameObject.AddComponent<VerticalLayoutGroup>();
            cv.padding = new RectOffset(0, 0, 20, 24);
            cv.spacing = 18;
            cv.childControlHeight = true;
            cv.childControlWidth = true;
            cv.childForceExpandHeight = false;
            scroll.content = content;
            scroll.viewport = scrollRt;
            rulesScroll = scroll;

            // Réglages à droite.
            var side = UIKit.Rect("Settings", card);
            side.anchorMin = new Vector2(0.6f, 0f);
            side.anchorMax = new Vector2(1f, 1f);
            side.offsetMin = new Vector2(16, 130);
            side.offsetMax = new Vector2(-44, -110);
            var sv = UIKit.VStack(side, 14);
            sv.childAlignment = TextAnchor.UpperCenter;
            UIKit.Size(UIKit.Label("SettingsTitle", side, "Règles réglables", 34, UIKit.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold), 50);
            ruleButtons = new Button[3];
            ruleButtons[0] = UIKit.Button("ForgotCheck", side, "", () => { pending.ForgotCheckPenalty = !pending.ForgotCheckPenalty; OnRuleChanged(); }, UIKit.Magenta, 28);
            ruleButtons[1] = UIKit.Button("JackWild", side, "", () => { pending.JackIsWild = !pending.JackIsWild; OnRuleChanged(); }, UIKit.Magenta, 28);
            ruleButtons[2] = UIKit.Button("JokerSeven", side, "", () => { pending.JokerCountersSeven = !pending.JokerCountersSeven; OnRuleChanged(); }, UIKit.Magenta, 28);
            foreach (var b in ruleButtons) UIKit.Size(b, 118);
            var note = UIKit.Label("Note", side, "Les changements s'appliquent à la prochaine partie.", 24, UIKit.TextDim);
            UIKit.Size(note, 70);
            UIKit.Size(UIKit.Button("Tips", side, "Revoir les astuces de la première partie", () =>
            {
                sfx.Play(Sfx.Click);
                ResetTips();
                Flash("Les astuces reviendront pendant la partie");
            }, UIKit.Cyan, 26), 84);

            var close = UIKit.Button("Close", card, "Fermer", () =>
            {
                sfx.Play(Sfx.Click);
                Show(rulesOverlay, false);
                if (rulesFromPause) ShowPause();
            }, UIKit.Gold, 40);
            UIKit.Anchor((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 26), new Vector2(380, 92));
        }

        void OnRuleChanged()
        {
            sfx.Play(Sfx.Click);
            pending.Save();
            RefreshRules();
        }

        void RefreshRules()
        {
            UIKit.SetButtonText(ruleButtons[0], "Check oublié : " + (pending.ForgotCheckPenalty ? "2 cartes de pénalité" : "pas de pénalité"));
            UIKit.SetButtonText(ruleButtons[1], "Valet : " + (pending.JackIsWild ? "se pose sur tout" : "couleur ou valeur"));
            UIKit.SetButtonText(ruleButtons[2], "Joker contre le 7 : " + (pending.JokerCountersSeven ? "oui" : "non"));
            Mark(ruleButtons[0], pending.ForgotCheckPenalty, UIKit.Magenta);
            Mark(ruleButtons[1], pending.JackIsWild, UIKit.Magenta);
            Mark(ruleButtons[2], pending.JokerCountersSeven, UIKit.Magenta);
        }

        void ShowRulesSheet(int index)
        {
            rulesSheet = Mathf.Clamp(index, 0, RulesSheets.All.Count - 1);
            var sheet = RulesSheets.All[rulesSheet];
            rulesText.text = sheet.Body;
            int k = 0;
            foreach (Transform child in rulesCards)
            {
                var img = child.GetComponent<RawImage>();
                bool on = k < sheet.Cards.Count;
                child.gameObject.SetActive(on);
                if (on && img != null) UIKit.SetMiniCard(img, sheet.Cards[k]);
                k++;
            }
            rulesCards.gameObject.SetActive(sheet.Cards.Count > 0);
            for (int i = 0; i < rulesTabs.Length; i++) Mark(rulesTabs[i], i == rulesSheet, UIKit.Gold);
            rulesScroll.verticalNormalizedPosition = 1f;
        }

        public void ShowRules(bool fromPause)
        {
            rulesFromPause = fromPause;
            pending = GameSettings.Load();
            RefreshRules();
            ShowRulesSheet(rulesSheet);
            if (fromPause) Show(pauseOverlay, false);
            Show(rulesOverlay, true);
        }

        // ---------------------------------------------------------------- Pause

        void BuildPause()
        {
            pauseOverlay = Overlay("Pause", out var card, 760, 940, UIKit.Cyan);
            UIKit.VStack(card, 16, 44);
            Title(card, "Pause", 70, UIKit.Gold, 96);
            UIKit.Size(UIKit.Button("Resume", card, "Reprendre", () => { sfx.Play(Sfx.Click); Show(pauseOverlay, false); }, UIKit.Gold, 46), 112);
            UIKit.Size(UIKit.Button("New", card, "Nouvelle partie", () => { sfx.Play(Sfx.Click); ShowSetup(); }, UIKit.Cyan, 38), 98);
            UIKit.Size(UIKit.Button("Rules", card, "Règles", () => { sfx.Play(Sfx.Click); ShowRules(true); }, UIKit.Cyan, 38), 98);
            pauseSpeedButton = UIKit.Button("Speed", card, "", () =>
            {
                sfx.Play(Sfx.Click);
                game.SetSpeed(game.Settings.Speed == AiSpeed.Fast ? AiSpeed.Normal : AiSpeed.Fast);
                SavePreferredSpeed();
                RefreshToggles();
            }, UIKit.Cyan, 34);
            UIKit.Size(pauseSpeedButton, 92);
            pauseSoundButton = UIKit.Button("Sound", card, "", () => { sfx.Muted = !sfx.Muted; sfx.Play(Sfx.Click); RefreshToggles(); }, UIKit.Cyan, 34);
            UIKit.Size(pauseSoundButton, 92);
            UIKit.Size(UIKit.Button("Quit", card, "Quitter la partie", () =>
            {
                sfx.Play(Sfx.Click);
                game.Persist();
                ShowHome();
            }, UIKit.Magenta, 36), 98);
            UIKit.Size(UIKit.Label("Saved", card, "La partie en cours est sauvegardée.", 24, UIKit.TextDim), 40);
        }

        public void ShowPause()
        {
            if (!game.HasGame) { ShowHome(); return; }
            RefreshToggles();
            Show(pauseOverlay, true);
        }

        // ---------------------------------------------------------------- Valet : choix de la couleur

        void BuildSuitPicker()
        {
            suitOverlay = Overlay("SuitPicker", out var card, 1200, 600, UIKit.Gold, 0.5f);
            UIKit.VStack(card, 20, 40);
            Title(card, "Valet : quelle couleur demandes-tu ?", 50, UIKit.Gold, 74);
            var row = Row(card, 280, 24);
            SuitButtons = new Button[4];
            string[] names = { "Pique", "Cœur", "Carreau", "Trèfle" };
            Suit[] order = { Suit.Spades, Suit.Hearts, Suit.Diamonds, Suit.Clubs };
            for (int i = 0; i < 4; i++)
            {
                var suit = order[i];
                bool red = Card.ColorOf(suit) == CardColor.Red;
                var b = UIKit.Button("Suit" + suit, row, "", () => PickSuit(suit), red ? UIKit.SuitRed : UIKit.Cyan, 40);
                var label = b.GetComponentInChildren<Text>();
                label.text = Card.SuitSymbol(suit);
                label.fontSize = 170;
                label.resizeTextMaxSize = 170;
                label.color = red ? UIKit.SuitRed : UIKit.Text;
                UIKit.Stretch(label.rectTransform, 6, 50, 6, 4);
                var caption = UIKit.Label("Caption", b.transform, names[i], 30, UIKit.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(caption.rectTransform, 6, 10, 6, 220);
                SuitButtons[(int)suit] = b;
            }
            UIKit.Size(UIKit.Button("Cancel", card, "Annuler", () => PickSuit(null), UIKit.TextDim, 34), 90);
        }

        void AskSuit(Action<Suit?> callback)
        {
            if (suitCallback != null) { var old = suitCallback; suitCallback = null; old(null); }
            suitCallback = callback;
            Show(suitOverlay, true);
        }

        void PickSuit(Suit? suit)
        {
            sfx.Play(suit.HasValue ? Sfx.Suit : Sfx.Click);
            Show(suitOverlay, false);
            var cb = suitCallback;
            suitCallback = null;
            cb?.Invoke(suit);
        }

        // ---------------------------------------------------------------- Fin de tour

        void BuildRoundEnd()
        {
            roundOverlay = Overlay("RoundEnd", out var card, 1100, 720, UIKit.Magenta, 0.45f);
            UIKit.VStack(card, 18, 46);
            roundTitle = Title(card, "", 66, UIKit.Gold, 90);
            roundBody = UIKit.Label("Body", card, "", 32, UIKit.Text);
            roundBody.supportRichText = true;
            UIKit.Size(roundBody, 300);

            var next = Row(card, 120);
            roundNextRow = next.gameObject;
            NextRoundButton = UIKit.Button("Next", next, "Tour suivant", () =>
            {
                sfx.Play(Sfx.Click);
                Show(roundOverlay, false);
                game.NextRound();
            }, UIKit.Gold, 48);

            var out_ = Row(card, 120);
            roundOutRow = out_.gameObject;
            WatchButton = UIKit.Button("Watch", out_, "Regarder la fin (accéléré)", () =>
            {
                sfx.Play(Sfx.Click);
                Show(roundOverlay, false);
                game.WatchToEnd();
                game.NextRound();
            }, UIKit.Cyan, 34);
            UIKit.Size(WatchButton, -1, -1, 1.6f);
            RoundReplayButton = UIKit.Button("Replay", out_, "Rejouer", () => { sfx.Play(Sfx.Click); Replay(); }, UIKit.Gold, 38);
            UIKit.Button("Menu", out_, "Menu", () => { sfx.Play(Sfx.Click); game.Abandon(); ShowHome(); }, UIKit.Magenta, 38);
        }

        void OnRoundOver(bool humanJustEliminated)
        {
            var m = game.Match;
            int loser = m.Eliminated[m.Eliminated.Count - 1];
            var alive = m.Alive.Select(game.NameOf).ToList();
            roundTitle.text = humanJustEliminated ? "Tu es éliminé !" : $"Fin du tour {m.RoundNumber}";
            roundTitle.color = humanJustEliminated ? UIKit.Danger : UIKit.Gold;
            string lostLine = humanJustEliminated ? "Tu es le dernier à avoir encore des cartes." : $"<color=#FF5A6E><b>{game.NameOf(loser)}</b></color> est éliminé.";
            string aliveLine = $"Encore en course ({alive.Count}) : <color=#4DEBFF>{string.Join(", ", alive)}</color>";
            string extra = humanJustEliminated ? "\nTu peux regarder la fin de la partie en accéléré." :
                game.HumanAlive ? $"\nTour {m.RoundNumber + 1} : {m.Rules.CardsToDeal(alive.Count)} cartes chacun." : "";
            roundBody.text = lostLine + "\n\n" + aliveLine + extra;
            roundNextRow.SetActive(!humanJustEliminated);
            roundOutRow.SetActive(humanJustEliminated);
            if (humanJustEliminated) sfx.Play(Sfx.Defeat, 0.7f);
            StartCoroutine(ShowDelayed(roundOverlay, 0.35f));
            // L'humain regarde la fin : on enchaîne tout seul.
            if (!humanJustEliminated && !game.HumanAlive) autoNext = StartCoroutine(AutoNextRound());
        }

        IEnumerator AutoNextRound()
        {
            yield return new WaitForSecondsRealtime(game.Turbo ? 0.4f : 1.6f);
            autoNext = null;
            if (roundOverlay.activeSelf && game.AwaitingNextRound)
            {
                Show(roundOverlay, false);
                game.NextRound();
            }
        }

        IEnumerator ShowDelayed(GameObject overlay, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (game.HasGame) Show(overlay, true);
        }

        // ---------------------------------------------------------------- Fin de partie

        sealed class PodiumStep
        {
            public RectTransform Root;
            public Medallion Face;
            public Image Glow;
            public Text Name;
        }

        PodiumStep[] podium;
        Text matchRest;
        float podiumShownAt = -1f;

        void BuildMatchEnd()
        {
            matchOverlay = Overlay("MatchEnd", out var card, 1200, 900, UIKit.Gold, 0.5f);
            UIKit.VStack(card, 12, 40);
            matchTitle = Title(card, "", 90, UIKit.Gold, 110);
            var o = matchTitle.gameObject.AddComponent<Outline>();
            o.effectColor = UIKit.Magenta * new Color(1, 1, 1, 0.6f);
            o.effectDistance = new Vector2(4, -4);
            matchBody = UIKit.Label("Body", card, "", 30, UIKit.Text);
            matchBody.supportRichText = true;
            UIKit.Size(matchBody, 84);

            // Podium : 2e à gauche, 1er au centre (plus haut), 3e à droite ; portraits peints sur les marches.
            var stage = UIKit.Rect("Podium", card);
            UIKit.Size(stage.gameObject.AddComponent<LayoutElement>(), 400);
            podium = new PodiumStep[3];
            float[] heights = { 110f, 160f, 74f };
            float[] xs = { -300f, 0f, 300f };
            string[] ranks = { "2", "1", "3" };
            for (int i = 0; i < 3; i++)
            {
                var step = new PodiumStep();
                step.Root = UIKit.Rect("Step" + ranks[i], stage);
                UIKit.Anchor(step.Root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(xs[i], 0f), new Vector2(270f, 400f));
                var block = UIKit.Panel("Block", step.Root, new Color(0.10f, 0.08f, 0.05f, 0.95f), true, i == 1 ? UIKit.Gold : UIKit.Copper);
                UIKit.Anchor(block.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(250f, heights[i]));
                var num = UIKit.Label("Rank", block.transform, ranks[i], i == 1 ? 72 : 56, i == 1 ? UIKit.Gold : UIKit.Copper, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(num.rectTransform);
                float faceSize = i == 1 ? 170f : 130f;
                step.Glow = new GameObject("Glow", typeof(RectTransform)).AddComponent<Image>();
                step.Glow.transform.SetParent(step.Root, false);
                step.Glow.sprite = UIKit.Glow;
                step.Glow.color = UIKit.Gold;
                step.Glow.raycastTarget = false;
                UIKit.Anchor(step.Glow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, heights[i] + 56f + faceSize * 0.5f), new Vector2(faceSize * 2.1f, faceSize * 2.1f));
                step.Glow.gameObject.SetActive(i == 1);
                step.Face = BuildMedallion(step.Root, faceSize, i == 1 ? UIKit.Gold : UIKit.Copper);
                UIKit.Anchor(step.Face.Root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, heights[i] + 56f), new Vector2(faceSize, faceSize));
                step.Name = UIKit.Label("Name", step.Root, "", i == 1 ? 34 : 28, i == 1 ? UIKit.Gold : UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Anchor(step.Name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, heights[i] + 8f), new Vector2(270f, 44f));
                step.Name.resizeTextForBestFit = true;
                step.Name.resizeTextMinSize = 18;
                step.Name.resizeTextMaxSize = i == 1 ? 34 : 28;
                podium[i] = step;
            }

            matchRest = UIKit.Label("Rest", card, "", 26, UIKit.TextDim);
            matchRest.supportRichText = true;
            UIKit.Size(matchRest, 70);
            var row = Row(card, 110);
            MatchReplayButton = UIKit.Button("Replay", row, "Rejouer", () => { sfx.Play(Sfx.Click); Replay(); }, UIKit.Gold, 46);
            MatchMenuButton = UIKit.Button("Menu", row, "Menu", () => { sfx.Play(Sfx.Click); game.Abandon(); ShowHome(); }, UIKit.Magenta, 40);
        }

        void OnMatchOver()
        {
            var m = game.Match;
            int winner = m.Winner ?? 0;
            bool won = winner == GameController.HumanId;
            matchTitle.text = won ? "VICTOIRE !" : "Partie finie";
            matchTitle.color = won ? UIKit.Gold : UIKit.Cyan;
            string head = won ? "Tu remportes la partie de Check !" : $"<b>{game.NameOf(winner)}</b> gagne la partie.";
            matchBody.text = head + $"\n<size=26>{m.RoundNumber} tour{(m.RoundNumber > 1 ? "s" : "")} joué{(m.RoundNumber > 1 ? "s" : "")}</size>";

            var order = Standings.Of(m);
            for (int i = 0; i < podium.Length; i++) podium[i].Root.gameObject.SetActive(false);
            for (int rank = 1; rank <= Mathf.Min(3, order.Count); rank++)
            {
                var step = podium[Standings.PodiumSlot(rank)];
                step.Root.gameObject.SetActive(true);
                step.Name.text = game.NameOf(order[rank - 1]);
                SetMedallion(step.Face, order[rank - 1]);
            }
            var rest = new System.Text.StringBuilder();
            for (int rank = 4; rank <= order.Count; rank++)
                rest.Append(rank > 4 ? "   " : "").Append($"{rank}. {game.NameOf(order[rank - 1])}");
            matchRest.text = rest.ToString();
            matchRest.gameObject.SetActive(order.Count > 3);

            table.Celebrate(winner);
            sfx.Play(won ? Sfx.Victory : Sfx.Defeat);
            if (won) Haptics.Medium();
            Show(roundOverlay, false);
            StartCoroutine(ShowDelayed(matchOverlay, 0.6f));
            podiumShownAt = Time.unscaledTime + 0.6f;
        }

        /// <summary>Le vainqueur est éclairé : halo doré qui respire, portrait qui monte sur sa marche.</summary>
        void AnimatePodium()
        {
            if (podium == null || !matchOverlay.activeSelf) return;
            float t = Time.unscaledTime - podiumShownAt;
            var win = podium[1];
            float pulse = 0.65f + 0.35f * Mathf.Sin(t * 3.2f);
            win.Glow.color = UIKit.Gold * new Color(1, 1, 1, 0.55f * pulse);
            win.Glow.rectTransform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * 3.2f));
            for (int i = 0; i < podium.Length; i++)
            {
                // Entrée échelonnée : 3e, puis 2e, puis 1er.
                float delay = i == 2 ? 0f : i == 0 ? 0.15f : 0.3f;
                float e = Mathf.Clamp01((t - delay) / 0.35f);
                e = 1f - (1f - e) * (1f - e);
                podium[i].Face.Root.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, e);
            }
        }

        void Replay()
        {
            HideAll();
            var s = game.Settings.Clone();
            game.NewGame(s);
        }

        // ---------------------------------------------------------------- Retour Android / Échap

        /// <summary>Ferme l'écran du dessus ; en jeu, ouvre le menu.</summary>
        public void HandleBack()
        {
            if (suitOverlay.activeSelf) { PickSuit(null); return; }
            if (rulesOverlay.activeSelf)
            {
                Show(rulesOverlay, false);
                if (rulesFromPause) ShowPause();
                return;
            }
            if (setupOverlay.activeSelf)
            {
                Show(setupOverlay, false);
                if (game.HasGame && !game.Match.IsOver) ShowPause(); else ShowHome();
                return;
            }
            if (pauseOverlay.activeSelf) { Show(pauseOverlay, false); return; }
            if (homeOverlay.activeSelf)
            {
                if (game.HasGame && !game.Match.IsOver) Show(homeOverlay, false);
                return;
            }
            if (roundOverlay.activeSelf || matchOverlay.activeSelf) return;
            if (game.HasGame) ShowPause();
        }
    }
}
