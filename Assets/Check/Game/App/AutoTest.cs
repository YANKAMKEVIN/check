using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Check.Core;
using Check.Game.Match;
using Check.Game.UI;
using Check.Game.View;
using UnityEngine;
using UnityEngine.UI;

namespace Check.Game.App
{
    /// <summary>
    /// Recette automatique, lancée avec « -check-autotest [dossier] » : partie à 4 joueurs où le joueur humain
    /// est joué par les mêmes chemins que le toucher (toucher des cartes à l'écran, bouton Check, Poser, Valet,
    /// pioche, Passer), captures aux moments clés, retour Android, sauvegarde/reprise, rafale de touchers pendant
    /// les animations, puis une partie à 10 joueurs en accéléré jusqu'au bout. Code de sortie ≠ 0 en cas d'échec.
    /// </summary>
    public sealed class AutoTest : MonoBehaviour
    {
        const string Arg = "-check-autotest";
        const float Watchdog = 1200f;

        CheckApp app;
        GameController game;
        HumanTurn human;
        Hud hud;
        CardTable cards;
        string folder;
        int shot;
        int failures;
        int errors;
        readonly HashSet<string> captured = new HashSet<string>();
        readonly System.Random rng = new System.Random(1234);
        int humanActions, pickMisses, quickPlays, checksPressed, jacksPlayed, draws, passes, penaltiesTaken;

        public static bool Requested(out string folder)
        {
            folder = null;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != Arg) continue;
                folder = i + 1 < args.Length && !args[i + 1].StartsWith("-")
                    ? args[i + 1]
                    : Path.Combine(Application.persistentDataPath, "autotest");
                return true;
            }
            return false;
        }

        public void Run(CheckApp checkApp, string outputFolder)
        {
            app = checkApp;
            game = app.Game;
            human = app.Human;
            hud = app.Hud;
            cards = app.Cards;
            folder = outputFolder;
            Directory.CreateDirectory(folder);
            Application.runInBackground = true; // la recette tourne même sans le focus
            Application.logMessageReceived += OnLog;
            StartCoroutine(Script());
            StartCoroutine(WatchdogRoutine());
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (condition.StartsWith("[AutoTest]")) return;
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            {
                errors++;
                Debug.Log("[AutoTest] ERREUR JOURNAL : " + condition);
            }
            else if (type == LogType.Warning && condition.StartsWith("[Check] Vue resynchronisée"))
            {
                errors++;
                Debug.Log("[AutoTest] ÉCART VUE/MOTEUR : " + condition);
            }
        }

        IEnumerator WatchdogRoutine()
        {
            yield return new WaitForSecondsRealtime(Watchdog);
            Fail("délai global dépassé (" + Watchdog + " s)");
            Finish();
        }

        IEnumerator Script()
        {
            Log("début, dossier " + folder);
            // Préférences connues pour la recette (les vraies sont restaurées à la fin).
            string savedPrefs = PlayerPrefs.GetString("check.settings", "");
            SavedGame.Clear();
            new GameSettings { PlayerCount = 4, Speed = AiSpeed.Fast }.Save();

            yield return new WaitForSecondsRealtime(2f);
            yield return Capture("menu");
            Check(hud.HomeOpen, "écran d'accueil affiché");
            hud.ShowRules(false);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Capture("regles");
            hud.HandleBack();
            yield return new WaitForSecondsRealtime(0.3f);
            Check(hud.HomeOpen && !hud.IsBlockingRulesOpen, "règles refermées par le bouton retour");

            // ---------------------------------------------------------------- Partie à 4 par l'interface
            Click(hud.HomePlayButton, "Jouer");
            yield return new WaitForSecondsRealtime(0.4f);
            Click(hud.SetupPlayerButtons[4 - RuleSet.MinPlayers], "4 joueurs");
            yield return Capture("nouvelle_partie");
            Click(hud.SetupStartButton, "Lancer");
            yield return new WaitForSecondsRealtime(1.1f);
            Check(game.HasGame && game.PlayerCount == 4, "partie à 4 lancée");
            yield return Capture("distribution");

            yield return PlayMatch(4, 360f, fast: false);
            Check(game.Match != null && game.Match.IsOver, "partie à 4 terminée");
            for (float w = 0f; w < 8f && !hud.MatchScreenOpen; w += Time.unscaledDeltaTime) yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            Check(hud.MatchScreenOpen, "écran de fin de partie");
            yield return Capture(game.Match != null && game.Match.Winner == GameController.HumanId ? "victoire" : "fin_de_partie");

            // ---------------------------------------------------------------- Partie à 10 en accéléré
            Click(hud.MatchMenuButton, "Menu");
            yield return new WaitForSecondsRealtime(0.4f);
            Click(hud.HomePlayButton, "Jouer");
            yield return new WaitForSecondsRealtime(0.3f);
            Click(hud.SetupPlayerButtons[10 - RuleSet.MinPlayers], "10 joueurs");
            Click(hud.SetupStartButton, "Lancer");
            yield return new WaitForSecondsRealtime(0.8f);
            Check(game.PlayerCount == 10, "partie à 10 lancée");
            yield return Capture("10_joueurs_distribution");
            game.Turbo = true;
            yield return PlayMatch(10, 900f, fast: true);
            game.Turbo = false;
            Check(game.Match != null && game.Match.IsOver, "partie à 10 terminée (" + (game.Match?.RoundNumber ?? 0) + " tours)");
            for (float w = 0f; w < 8f && !hud.MatchScreenOpen; w += Time.unscaledDeltaTime) yield return null;
            Check(hud.MatchScreenOpen, "écran de fin de partie (10 joueurs)");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("10_joueurs_fin");

            Log($"bilan humain : {humanActions} actions, {checksPressed} Check, {jacksPlayed} Valets, {quickPlays} double-taps, " +
                $"{draws} pioches, {passes} passes, {penaltiesTaken} pénalités prises, {pickMisses} touchers manqués");
            Check(pickMisses == 0, "tous les touchers de cartes ont touché la bonne carte");
            Check(cards.ReconcileFixes == 0, "aucun écart entre la vue et le moteur");
            Check(errors == 0, "aucune erreur ni exception au journal (" + errors + ")");

            if (!string.IsNullOrEmpty(savedPrefs)) PlayerPrefs.SetString("check.settings", savedPrefs);
            SavedGame.Clear();
            Finish();
        }

        void Finish()
        {
            Log(failures == 0 ? "TERMINÉ : OK" : "TERMINÉ : " + failures + " échec(s)");
            StopAllCoroutines();
            Application.Quit(failures == 0 ? 0 : 1);
        }

        // ---------------------------------------------------------------- Boucle d'une partie

        IEnumerator PlayMatch(int players, float timeout, bool fast)
        {
            float start = Time.realtimeSinceStartup;
            bool savedTested = fast, backTested = fast, spamTested = fast, ratioTested = fast;
            int lastRound = 0;
            while (game.Match != null && !game.Match.IsOver)
            {
                if (Time.realtimeSinceStartup - start > timeout)
                {
                    Fail($"partie à {players} trop longue ({timeout} s)");
                    yield break;
                }
                if (game.Match.RoundNumber != lastRound && fast)
                {
                    lastRound = game.Match.RoundNumber;
                    if (lastRound == 3) { yield return null; yield return Capture("10_joueurs_tour3"); }
                }

                // Écran de fin de tour.
                if (hud.RoundScreenOpen)
                {
                    if (!fast && Once("fin_de_tour"))
                    {
                        yield return new WaitForSecondsRealtime(0.6f);
                        yield return Capture("fin_de_tour");
                    }
                    if (hud.WatchButton.gameObject.activeInHierarchy)
                    {
                        if (!fast && Once("elimine")) yield return Capture("elimine");
                        Click(hud.WatchButton, "Regarder la fin");
                        game.Turbo = true;
                    }
                    else if (hud.NextRoundButton.gameObject.activeInHierarchy && game.AwaitingNextRound)
                    {
                        Click(hud.NextRoundButton, "Tour suivant");
                        game.Turbo = true; // la suite de la partie en accéléré
                    }
                    yield return new WaitForSecondsRealtime(0.3f);
                    continue;
                }

                // Pendant les animations des adversaires : rafale de touchers (rien ne doit bouger ni casser).
                if (!spamTested && cards.IsBusy && !game.IsHumanTurn && game.Round != null && !game.Round.IsOver)
                {
                    spamTested = true;
                    SpamWhileBusy();
                }

                if (human.CanAct)
                {
                    if (!backTested)
                    {
                        backTested = true;
                        yield return TestBackButton();
                    }
                    if (!ratioTested)
                    {
                        ratioTested = true;
                        yield return TestAspectRatios();
                    }
                    if (!savedTested && game.Round.TurnNumber >= 4)
                    {
                        savedTested = true;
                        yield return TestSaveResume();
                        continue;
                    }
                    yield return HumanTurnRoutine(fast);
                    continue;
                }
                yield return null;
            }
        }

        bool Once(string key) => captured.Add(key);

        // ---------------------------------------------------------------- Le joueur humain, par l'interface

        IEnumerator HumanTurnRoutine(bool fast)
        {
            var round = game.Round;
            int turn = round.TurnNumber;
            var legal = round.LegalActions();
            var plays = legal.Where(a => a.Kind == ActionKind.Play).ToList();

            if (!fast && Once("ton_tour")) yield return Capture("ton_tour");
            if (round.PendingDraw > 0 && !fast && Once("penalite")) yield return Capture("penalite");

            TurnAction choice;
            var jack = plays.FirstOrDefault(a => a.Cards[0].Rank == Rank.Jack);
            var multi = plays.FirstOrDefault(a => a.Cards.Count > 1);
            if (round.PendingDraw > 0 && (plays.Count == 0 || rng.NextDouble() < 0.4)) choice = TurnAction.Draw;
            else if (round.HasDrawn) choice = plays.Count > 0 && rng.NextDouble() < 0.8 ? plays[0] : TurnAction.Pass;
            else if (jack != null && (!captured.Contains("valet") || rng.NextDouble() < 0.3)) choice = jack;
            else if (multi != null && rng.NextDouble() < 0.7) choice = multi;
            else if (plays.Count > 0 && rng.NextDouble() < 0.85) choice = plays[rng.Next(plays.Count)];
            else choice = TurnAction.Draw;
            if (!round.IsLegal(choice)) choice = legal[rng.Next(legal.Count)];

            humanActions++;
            switch (choice.Kind)
            {
                case ActionKind.Draw:
                    if (round.PendingDraw > 0)
                    {
                        penaltiesTaken++;
                        Click(hud.DrawButton, "Prendre la pénalité");
                    }
                    else
                    {
                        draws++;
                        app.OnTap(cards.DrawPileScreenPoint); // toucher la pioche
                    }
                    break;
                case ActionKind.Pass:
                    passes++;
                    Click(hud.DrawButton, "Passer");
                    break;
                default:
                    yield return PlayThroughUi(choice, fast);
                    break;
            }

            // On attend que le tour soit parti (ou que le joueur puisse rejouer après une pioche).
            float t = 0f;
            while (t < 20f && game.Round != null && !game.Round.IsOver && game.Round.TurnNumber == turn && !human.CanAct &&
                   !hud.SuitPickerOpen)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (game.Round != null && !game.Round.IsOver && game.Round.TurnNumber == turn && game.IsHumanTurn && !round.HasDrawn &&
                choice.Kind != ActionKind.Draw && human.CanAct)
                Fail("l'action humaine n'a pas été jouée : " + choice);
            if (hud.SuitPickerOpen) Fail("sélecteur de Valet resté ouvert");
            while (cards.IsBusy && t < 30f) { t += Time.unscaledDeltaTime; yield return null; }
        }

        IEnumerator PlayThroughUi(TurnAction action, bool fast)
        {
            human.ClearSelection();
            bool single = action.Cards.Count == 1;
            bool useDoubleTap = single && !action.DeclaresCheck && action.Cards[0].Rank != Rank.Jack && rng.NextDouble() < 0.35;
            if (useDoubleTap)
            {
                // Double-tap tel que le produit PointerInput : toucher, toucher, double.
                if (!cards.TryGetHumanCardScreenPoint(action.Cards[0], out var p)) { Fail("carte introuvable dans la main : " + action.Cards[0]); yield break; }
                app.OnTap(p);
                app.OnTap(p);
                quickPlays++;
                SendDoubleTap(p);
                yield break;
            }

            foreach (var c in action.Cards)
            {
                if (!cards.TryGetHumanCardScreenPoint(c, out var p)) { Fail("carte introuvable dans la main : " + c); yield break; }
                app.OnTap(p);
                if (!human.Selection.Contains(c))
                {
                    pickMisses++;
                    Debug.Log($"[AutoTest] toucher manqué sur {c} en {p}, sélection = {string.Join(" ", human.Selection)}");
                    human.TapCard(c);
                }
                yield return null;
            }
            if (!human.Selection.SequenceEqual(action.Cards))
                Log($"sélection {string.Join(" ", human.Selection)} ≠ action {action}");

            if (action.DeclaresCheck)
            {
                yield return null;
                if (!fast && Once("check")) yield return Capture("bouton_check");
                if (hud.CheckButton.gameObject.activeInHierarchy)
                {
                    Click(hud.CheckButton, "Check !");
                    checksPressed++;
                    Check(human.CheckArmed, "Check annoncé", quiet: true);
                }
                else Fail("bouton Check absent alors qu'il ne restera qu'une carte");
            }

            if (!fast && Once("selection") && action.Cards.Count > 1) yield return Capture("selection_multiple");
            Click(hud.PlayButton, "Poser");

            if (action.Cards[0].Rank == Rank.Jack)
            {
                yield return null;
                Check(hud.SuitPickerOpen, "sélecteur de couleur ouvert pour le Valet", quiet: true);
                if (!fast && Once("valet"))
                {
                    yield return new WaitForSecondsRealtime(0.35f);
                    yield return Capture("valet");
                }
                jacksPlayed++;
                var suit = action.RequestedSuit ?? Suit.Hearts;
                Click(hud.SuitButtons[(int)suit], "couleur " + Card.SuitSymbol(suit));
                yield return null;
                if (!fast && Once("couleur_demandee"))
                {
                    yield return new WaitForSecondsRealtime(1.2f);
                    yield return Capture("couleur_demandee");
                }
            }
        }

        /// <summary>Même chemin que PointerInput.DoubleTapped.</summary>
        void SendDoubleTap(Vector2 p) => app.OnDoubleTap(p);

        // ---------------------------------------------------------------- Tests ciblés

        void SpamWhileBusy()
        {
            var round = game.Round;
            int turn = round.TurnNumber, current = round.Current;
            int before = cards.HumanCards.Count();
            for (int i = 0; i < 40; i++)
            {
                var hand = cards.HumanCards.ToList();
                if (hand.Count > 0 && cards.TryGetHumanCardScreenPoint(hand[rng.Next(hand.Count)], out var p))
                {
                    app.OnTap(p);
                    SendDoubleTap(p);
                }
                app.OnTap(cards.DrawPileScreenPoint);
                app.OnTap(cards.DiscardScreenPoint);
                human.PressPlay();
                human.PressCheck();
                human.Draw();
                human.Pass();
            }
            Check(round.TurnNumber == turn && round.Current == current && cards.HumanCards.Count() == before,
                "rafale de touchers pendant les animations sans effet");
        }

        IEnumerator TestBackButton()
        {
            hud.HandleBack();
            yield return new WaitForSecondsRealtime(0.4f);
            Check(game.Paused, "retour Android : menu pause ouvert, jeu en pause");
            yield return Capture("pause");
            hud.HandleBack();
            yield return new WaitForSecondsRealtime(0.3f);
            Check(!game.Paused && !hud.IsBlockingOverlayOpen, "retour Android : menu refermé");
        }

        /// <summary>En plein tour, à la main du joueur : quitter vers l'accueil puis « Reprendre la partie ».</summary>
        IEnumerator TestSaveResume()
        {
            var r = game.Round;
            int round = game.Match.RoundNumber, turn = r.TurnNumber, current = r.Current;
            var hand = r.Hand(game.HumanSeat).OrderBy(c => c.Id).ToList();
            var top = r.Top;
            game.Persist();
            hud.ShowHome();
            yield return new WaitForSecondsRealtime(0.5f);
            Check(SavedGame.Exists, "partie sauvegardée");
            Click(hud.ResumeSavedButton, "Reprendre la partie");
            yield return new WaitForSecondsRealtime(0.8f);
            var r2 = game.Round;
            Check(r2 != null && game.Match.RoundNumber == round && r2.TurnNumber == turn && r2.Current == current && r2.Top == top &&
                  game.HumanSeat >= 0 && r2.Hand(game.HumanSeat).OrderBy(c => c.Id).SequenceEqual(hand),
                "reprise : même tour, même main, même tas");
            Check(cards.HumanCards.OrderBy(c => c.Id).SequenceEqual(hand), "reprise : la main affichée est la bonne");
            yield return Capture("reprise");
            float t = 0f;
            while (!human.CanAct && t < 5f) { t += Time.unscaledDeltaTime; yield return null; }
            Check(human.CanAct, "reprise : le joueur peut rejouer");
        }

        IEnumerator TestAspectRatios()
        {
            int w = Screen.width, h = Screen.height;
            var sizes = new[] { (1950, 900, "19_5x9"), (1600, 900, "16x9"), (1200, 900, "4x3") };
            foreach (var (sw, sh, name) in sizes)
            {
                Screen.SetResolution(sw, sh, FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Capture("format_" + name);
            }
            Screen.SetResolution(w, h, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(0.6f);
        }

        // ---------------------------------------------------------------- Outils

        void Click(Button b, string what)
        {
            if (b == null || !b.gameObject.activeInHierarchy || !b.interactable)
            {
                Fail("bouton indisponible : " + what);
                return;
            }
            b.onClick.Invoke();
        }

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(folder, $"{++shot:00}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            Log("capture " + path);
            yield return new WaitForSecondsRealtime(0.3f);
        }

        void Check(bool ok, string what, bool quiet = false)
        {
            if (ok) { if (!quiet) Log("OK " + what); }
            else Fail(what);
        }

        void Fail(string what)
        {
            failures++;
            Debug.Log("[AutoTest] ÉCHEC " + what);
        }

        static void Log(string msg) => Debug.Log("[AutoTest] " + msg);
    }
}
