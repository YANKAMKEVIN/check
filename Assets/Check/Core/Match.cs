using System;
using System.Collections.Generic;
using System.Linq;

namespace Check.Core
{
    /// <summary>
    /// Une partie complète : des tours successifs, avec un éliminé par tour, jusqu'au dernier joueur.
    /// Les joueurs sont numérotés de 0 à PlayerCount - 1 dans l'ordre de la table.
    /// </summary>
    public sealed class Match
    {
        public readonly RuleSet Rules;
        public readonly int PlayerCount;
        readonly IRandom random;
        readonly List<int> alive;
        readonly List<int> eliminated = new List<int>();

        public IReadOnlyList<int> Alive => alive;
        /// <summary>Joueurs éliminés, du premier au dernier.</summary>
        public IReadOnlyList<int> Eliminated => eliminated;
        public int Dealer { get; private set; }
        public Round Round { get; private set; }
        public int RoundNumber { get; private set; }
        public int? Winner { get; private set; }
        public bool IsOver => Winner.HasValue;

        public Match(RuleSet rules, int playerCount, IRandom random, int firstDealer = 0)
        {
            if (playerCount < RuleSet.MinPlayers || playerCount > RuleSet.MaxPlayers)
                throw new ArgumentException($"De {RuleSet.MinPlayers} à {RuleSet.MaxPlayers} joueurs");
            Rules = rules;
            PlayerCount = playerCount;
            this.random = random;
            alive = Enumerable.Range(0, playerCount).ToList();
            Dealer = firstDealer;
        }

        /// <summary>Distribue un nouveau tour ; le joueur à gauche du donneur commence.</summary>
        public IReadOnlyList<GameEvent> StartRound()
        {
            if (IsOver) throw new InvalidOperationException("La partie est finie");
            if (Round != null && !Round.IsOver) throw new InvalidOperationException("Le tour en cours n'est pas fini");
            RoundNumber++;
            int dealerSeat = alive.IndexOf(Dealer);
            Round = new Round(Rules, alive, (dealerSeat + 1) % alive.Count, random);
            return Round.InitialEvents;
        }

        public IReadOnlyList<GameEvent> Apply(TurnAction action)
        {
            if (Round == null || Round.IsOver) throw new InvalidOperationException("Aucun tour en cours");
            var events = Round.Apply(action);
            if (!Round.IsOver) return events;

            int loser = Round.PlayerId(Round.LoserSeat);
            alive.Remove(loser);
            eliminated.Add(loser);
            var all = new List<GameEvent>(events);
            if (alive.Count == 1)
            {
                Winner = alive[0];
                all.Add(new GameEvent { Kind = GameEventKind.MatchWon, PlayerId = alive[0], Seat = Round.SeatOf(alive[0]) });
            }
            else
            {
                Dealer = NextAlive(Dealer);
            }
            return all;
        }

        int NextAlive(int playerId)
        {
            for (int i = 1; i <= PlayerCount; i++)
            {
                int p = (playerId + i) % PlayerCount;
                if (alive.Contains(p)) return p;
            }
            return playerId;
        }
    }
}
