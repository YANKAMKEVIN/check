using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Check.Core;
using Check.Game.View;
using UnityEngine;

namespace Check.Game.Match
{
    /// <summary>
    /// Boucle de jeu : tient la partie (moteur <see cref="Check.Core.Match"/>), fait jouer les adversaires
    /// (cerveau appelé hors du thread principal), reçoit les actions du joueur humain, envoie les événements
    /// à la vue et sauvegarde la partie en cours. Le moteur avance instantanément ; on n'enchaîne que quand
    /// la vue a fini d'animer.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        public const int HumanId = 0;

        public GameSettings Settings { get; private set; } = new GameSettings();
        public Check.Core.Match Match { get; private set; }
        public string[] Names { get; private set; } = Array.Empty<string>();
        /// <summary>L'humain est éliminé et regarde la fin en accéléré.</summary>
        public bool Spectating { get; private set; }
        /// <summary>Recette automatique : tout va très vite.</summary>
        public bool Turbo { get; set; }
        /// <summary>Menu ouvert : l'IA ne joue pas, rien n'avance.</summary>
        public bool Paused { get; set; }
        public bool IsAIThinking => thinking;

        CardTable table;
        IOpponentBrain[] brains = Array.Empty<IOpponentBrain>();
        SavedGame save;
        System.Random rng = new System.Random();
        int generation;
        bool thinking;
        TurnAction aiResult;
        int aiResultGeneration = -1;
        CancellationTokenSource cts;
        bool roundEndRaised;
        bool reconciled;

        public event Action GameStarted;
        /// <summary>Un tour est fini (la vue a tout montré) ; argument : l'humain vient d'être éliminé.</summary>
        public event Action<bool> RoundOver;
        public event Action MatchOver;
        public event Action StateChanged;

        public void Init(CardTable cardTable) => table = cardTable;

        public bool HasGame => Match != null;
        public Round Round => Match?.Round;
        public int HumanSeat => Match?.Round != null ? Match.Round.SeatOf(HumanId) : -1;
        public bool HumanAlive => Match != null && Match.Alive.Contains(HumanId);
        public bool IsHumanTurn => Round != null && !Round.IsOver && HumanSeat >= 0 && Round.Current == HumanSeat;
        public bool CanHumanAct => !Paused && IsHumanTurn && !table.IsBusy;
        public bool AwaitingNextRound => Match != null && !Match.IsOver && Round != null && Round.IsOver && !table.IsBusy;
        public int PlayerCount => Match?.PlayerCount ?? Settings.PlayerCount;

        public string NameOf(int player) =>
            player == HumanId ? "Toi" : player >= 0 && player < Names.Length ? Names[player] : "?";

        // ---------------------------------------------------------------- Partie

        public void NewGame(GameSettings settings)
        {
            CancelAI();
            generation++;
            Settings = settings.Clone();
            int seed = Environment.TickCount ^ (int)(DateTime.Now.Ticks & 0x7FFFFFFF);
            rng = new System.Random(seed);
            int n = Mathf.Clamp(Settings.PlayerCount, RuleSet.MinPlayers, RuleSet.MaxPlayers);
            Settings.PlayerCount = n;
            var names = new string[n];
            names[0] = "Toi";
            var picked = OpponentNames.Pick(n - 1, rng);
            for (int i = 1; i < n; i++) names[i] = picked[i - 1];
            int dealer = rng.Next(n);
            save = new SavedGame { Settings = Settings.Clone(), Names = names, Seed = seed, FirstDealer = dealer };
            Begin(names, seed, dealer);
            var events = Match.StartRound();
            save.Log.Add(SavedGame.RoundToken);
            save.Store();
            table.Enqueue(events, Match.Round, Match);
            GameStarted?.Invoke();
            StateChanged?.Invoke();
        }

        void Begin(string[] names, int seed, int dealer)
        {
            Names = names;
            Match = new Check.Core.Match(Settings.ToRuleSet(), names.Length, new SystemRandom(seed), dealer);
            brains = new IOpponentBrain[names.Length];
            for (int p = 1; p < names.Length; p++) brains[p] = OpponentBrains.Create(Settings.Level, p, seed);
            Spectating = false;
            roundEndRaised = false;
            reconciled = false;
            aiResult = null;
            table.Setup(names.Length, Array.ConvertAll(names, OpponentNames.IsFemale));
            ApplySpeed();
        }

        /// <summary>Reprend la partie sauvegardée (rejoue le film des actions). Faux si rien à reprendre.</summary>
        public bool Resume()
        {
            var saved = SavedGame.Load();
            if (saved == null) return false;
            CancelAI();
            generation++;
            try
            {
                Settings = saved.Settings.Clone();
                rng = new System.Random(saved.Seed ^ saved.Log.Count);
                Begin(saved.Names, saved.Seed, saved.FirstDealer);
                foreach (var token in saved.Log)
                {
                    if (token == SavedGame.RoundToken) Match.StartRound();
                    else Match.Apply(SavedGame.Decode(token));
                }
                if (Match.IsOver || Match.Round == null) throw new InvalidOperationException("partie déjà finie");
                save = saved;
                Spectating = !HumanAlive;
                ApplySpeed();
                table.ResetTo(Match);
                GameStarted?.Invoke();
                StateChanged?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Check] Reprise impossible, sauvegarde effacée : " + e.Message);
                SavedGame.Clear();
                Match = null;
                table.Setup(2);
                return false;
            }
        }

        /// <summary>Distribue le tour suivant (après l'écran de fin de tour).</summary>
        public void NextRound()
        {
            if (!AwaitingNextRound) return;
            var events = Match.StartRound();
            save?.Log.Add(SavedGame.RoundToken);
            save?.Store();
            roundEndRaised = false;
            reconciled = false;
            table.Enqueue(events, Match.Round, Match);
            StateChanged?.Invoke();
        }

        /// <summary>L'humain éliminé regarde la fin de la partie en accéléré.</summary>
        public void WatchToEnd()
        {
            Spectating = true;
            ApplySpeed();
        }

        /// <summary>Abandonne la partie en cours (retour à l'accueil sans sauvegarde).</summary>
        public void Abandon()
        {
            CancelAI();
            generation++;
            Match = null;
            SavedGame.Clear();
            table.Setup(2);
        }

        public void SetSpeed(AiSpeed speed)
        {
            Settings.Speed = speed;
            if (save != null) save.Settings.Speed = speed;
            ApplySpeed();
        }

        void ApplySpeed()
        {
            if (table == null) return;
            table.Speed = Turbo ? 0.1f : Spectating ? 0.3f : Settings.Speed == AiSpeed.Fast ? 0.6f : 1f;
        }

        float ThinkDelay()
        {
            if (Turbo) return 0f;
            if (Spectating) return 0.08f;
            float baseDelay = Settings.Speed == AiSpeed.Fast ? 0.3f : 0.75f;
            return baseDelay + (float)rng.NextDouble() * baseDelay * 0.5f;
        }

        // ---------------------------------------------------------------- Actions

        /// <summary>Action du joueur humain (passée par l'interface). Faux + raison si refusée.</summary>
        public bool TryHumanAction(TurnAction action, out string reason)
        {
            reason = null;
            if (!CanHumanAct) { reason = "Attends ton tour"; return false; }
            if (!Round.IsLegal(action, out reason)) return false;
            Apply(action);
            return true;
        }

        void Apply(TurnAction action)
        {
            var round = Match.Round;
            var events = Match.Apply(action);
            reconciled = false;
            if (Match.IsOver) SavedGame.Clear();
            else if (save != null)
            {
                save.Log.Add(SavedGame.Encode(action));
                save.Store();
            }
            table.Enqueue(events, round, Match);
            StateChanged?.Invoke();
        }

        void Update()
        {
            if (Match == null || Paused || table == null || table.IsBusy) return;
            var round = Match.Round;
            if (round == null) return;
            if (!reconciled)
            {
                reconciled = true;
                table.Reconcile(Match);
            }

            if (round.IsOver)
            {
                if (roundEndRaised) return;
                roundEndRaised = true;
                SavedGame.Flush();
                if (Match.IsOver) MatchOver?.Invoke();
                else RoundOver?.Invoke(Match.Eliminated.Count > 0 && Match.Eliminated[Match.Eliminated.Count - 1] == HumanId);
                return;
            }
            if (IsHumanTurn) return;

            if (aiResult != null && aiResultGeneration == generation)
            {
                var a = aiResult;
                aiResult = null;
                if (!round.IsLegal(a, out var why))
                {
                    Debug.LogWarning($"[Check] Action IA refusée ({a}) : {why} ; action de secours");
                    a = Fallback(round);
                }
                Apply(a);
                return;
            }
            if (!thinking) StartThinking(round);
        }

        TurnAction Fallback(Round round)
        {
            var legal = round.LegalActions();
            var plays = legal.Where(x => x.Kind == ActionKind.Play).ToList();
            if (plays.Count > 0) return plays[rng.Next(plays.Count)];
            return legal[rng.Next(legal.Count)];
        }

        async void StartThinking(Round round)
        {
            int gen = generation;
            int seat = round.Current;
            int player = round.PlayerId(seat);
            var brain = player > 0 && player < brains.Length ? brains[player] : null;
            var copy = round.Clone(new SystemRandom(rng.Next()));
            var source = new CancellationTokenSource();
            cts = source;
            var token = source.Token;
            float delay = ThinkDelay();
            float started = Time.realtimeSinceStartup;
            thinking = true;
            TurnAction result = null;
            try
            {
                if (brain != null) result = await Task.Run(() => brain.Choose(copy, seat, token), token);
                float wait = delay - (Time.realtimeSinceStartup - started);
                if (wait > 0.01f) await Task.Delay(TimeSpan.FromSeconds(wait), token);
            }
            catch (OperationCanceledException) { result = null; }
            catch (Exception e)
            {
                Debug.LogException(e);
                result = null;
            }
            finally
            {
                if (cts == source) cts = null;
                source.Dispose();
            }

            if (this == null || gen != generation) return;
            thinking = false;
            if (Match?.Round == null || Match.Round.IsOver) return;
            aiResult = result ?? Fallback(Match.Round);
            aiResultGeneration = gen;
        }

        void CancelAI()
        {
            thinking = false;
            aiResult = null;
            if (cts == null) return;
            try { cts.Cancel(); } catch (ObjectDisposedException) { }
            cts = null;
        }

        /// <summary>Écrit la sauvegarde sur disque (mise en pause de l'app, fermeture).</summary>
        public void Persist()
        {
            if (Match != null && !Match.IsOver && save != null)
            {
                save.Store();
                SavedGame.Flush();
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Persist();
        }

        void OnApplicationQuit() => Persist();

        void OnDestroy() => CancelAI();
    }
}
