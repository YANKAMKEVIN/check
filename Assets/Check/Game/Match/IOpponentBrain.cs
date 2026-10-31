using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Check.Core;

namespace Check.Game.Match
{
    /// <summary>
    /// Cerveau d'un adversaire. SEUL point de branchement de l'IA réelle (Check.AI) : voir <see cref="OpponentBrains.Create"/>.
    /// <para>
    /// <see cref="Choose"/> est appelée HORS du thread principal (Task.Run), sur une COPIE du tour
    /// (<see cref="Round.Clone"/> avec son propre hasard) : le cerveau peut la modifier librement (simulations).
    /// Il doit renvoyer une action légale pour <paramref name="seat"/> (= round.Current), et surveiller
    /// <paramref name="ct"/> pour s'arrêter vite (nouvelle partie, fermeture). Une action illégale ou une exception
    /// est remplacée par une action légale tirée au hasard : la partie ne se bloque jamais.
    /// Le délai minimal de « réflexion » (rythme) est géré par le contrôleur, pas par le cerveau.
    /// </para>
    /// </summary>
    public interface IOpponentBrain
    {
        TurnAction Choose(Round round, int seat, CancellationToken ct);
    }

    public static class OpponentBrains
    {
        /// <summary>
        /// Fabrique un cerveau par adversaire (il peut garder une mémoire de la partie).
        /// À remplacer par l'IA de Check.AI quand elle sera disponible.
        /// </summary>
        public static IOpponentBrain Create(OpponentLevel level, int playerId, int seed) =>
            new RandomBrain(level, seed * 7919 + playerId);
    }

    /// <summary>
    /// Cerveau provisoire : action légale au hasard, en préférant poser plutôt que piocher,
    /// et en demandant au Valet la couleur la plus fournie de sa main. En Facile, oublie parfois le « Check ».
    /// </summary>
    public sealed class RandomBrain : IOpponentBrain
    {
        readonly OpponentLevel level;
        readonly Random rng;

        public RandomBrain(OpponentLevel level, int seed)
        {
            this.level = level;
            rng = new Random(seed);
        }

        public TurnAction Choose(Round round, int seat, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var legal = round.LegalActions();
            if (legal.Count == 0) return TurnAction.Draw;
            var plays = legal.Where(a => a.Kind == ActionKind.Play).ToList();
            var others = legal.Where(a => a.Kind != ActionKind.Play).ToList();

            double playChance = level == OpponentLevel.Easy ? 0.8 : 0.95;
            TurnAction choice;
            if (plays.Count > 0 && (others.Count == 0 || rng.NextDouble() < playChance))
            {
                // Valet : on garde la couleur la plus présente dans ce qui restera en main.
                var hand = round.Hand(seat);
                choice = plays[rng.Next(plays.Count)];
                if (choice.Cards[0].Rank == Rank.Jack)
                {
                    var rest = hand.Where(c => !choice.Cards.Contains(c) && !c.IsJoker).ToList();
                    Suit best = rest.Count == 0 ? (Suit)rng.Next(4)
                        : rest.GroupBy(c => c.Suit).OrderByDescending(g => g.Count()).First().Key;
                    choice = plays.FirstOrDefault(a => a.Cards.SequenceEqual(choice.Cards) && a.RequestedSuit == best) ?? choice;
                }
            }
            else
            {
                choice = others.Count > 0 ? others[rng.Next(others.Count)] : plays[rng.Next(plays.Count)];
            }

            if (choice.DeclaresCheck && level == OpponentLevel.Easy && rng.NextDouble() < 0.25)
                choice = choice.WithCheck(false);
            return choice;
        }
    }
}
