using System;
using System.Threading;
using System.Threading.Tasks;
using Check.Core;

namespace Check.AI
{
    /// <summary>Un joueur (IA ou autre) qui choisit son action quand c'est son tour.</summary>
    public interface ICheckPlayer
    {
        /// <summary>
        /// Choisit une action légale pour <paramref name="seat"/>, qui doit être le joueur courant de
        /// <paramref name="round"/> (non modifié). Si <paramref name="ct"/> est annulé pendant la réflexion,
        /// renvoie tout de suite la meilleure action trouvée jusque-là (toujours légale).
        /// </summary>
        TurnAction ChooseAction(Round round, int seat, CancellationToken ct);
    }

    /// <summary>
    /// IA des adversaires. Ne lit que ce que son siège voit : sa main, le tas, le nombre de cartes des autres,
    /// la taille de la pioche, la pénalité et la couleur demandée. Toute simulation passe par
    /// <see cref="Round.Clone"/> + <see cref="Round.RedealHiddenCards"/> : les mains adverses réelles ne sont jamais lues.
    /// Une instance n'est pas prévue pour des appels simultanés (ils sont sérialisés) ; en prendre une par siège.
    /// </summary>
    public sealed class CheckAI : ICheckPlayer
    {
        readonly IRandom random;
        readonly object gate = new object();

        public AISettings Settings { get; }
        public AILevel Level => Settings.Level;

        /// <summary>Bilan de la dernière recherche Monte-Carlo (null si le niveau ne cherche pas).</summary>
        public SearchStats LastStats { get; private set; }

        /// <param name="random">Hasard injectable (tests reproductibles) ; null = aléatoire.</param>
        public CheckAI(AILevel level, IRandom random = null) : this(AISettings.For(level), random) { }

        public CheckAI(AISettings settings, IRandom random = null)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.random = random ?? new SystemRandom();
        }

        public TurnAction ChooseAction(Round round, int seat, CancellationToken ct = default)
        {
            if (round == null) throw new ArgumentNullException(nameof(round));
            if (round.IsOver) throw new InvalidOperationException("Le tour est fini");
            if (round.Current != seat) throw new InvalidOperationException($"Ce n'est pas au siège {seat} de jouer");

            lock (gate)
            {
                var legal = round.LegalActions();
                TurnAction choice;
                if (legal.Count == 1) choice = legal[0];
                else if (Settings.Naive) choice = Heuristics.Naive(round, legal, random, Settings.NaiveRandomChance);
                else if (!Settings.UseSearch) choice = Heuristics.Best(round, seat, legal, random);
                else
                {
                    var stats = new SearchStats();
                    choice = MonteCarloSearch.Search(round, seat, legal, Settings, random, ct, stats);
                    LastStats = stats;
                }

                // Distraction volontaire des niveaux faciles : oublier d'annoncer « Check ».
                if (choice.DeclaresCheck && Settings.ForgetCheckChance > 0 &&
                    random.Next(10000) < Settings.ForgetCheckChance * 10000)
                    choice = choice.WithCheck(false);
                return choice;
            }
        }

        /// <summary>
        /// Variante en tâche de fond pour le jeu. La position est copiée avant de rendre la main :
        /// l'appelant peut continuer à lire (sans la modifier) le tour pendant la réflexion.
        /// </summary>
        public Task<TurnAction> ChooseActionAsync(Round round, int seat, CancellationToken ct = default)
        {
            int seed;
            lock (gate) seed = random.Next(int.MaxValue);
            var copy = round.Clone(new SystemRandom(seed));
            return Task.Run(() => ChooseAction(copy, seat, ct));
        }
    }
}
