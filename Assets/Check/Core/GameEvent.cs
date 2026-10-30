using System;
using System.Collections.Generic;

namespace Check.Core
{
    public enum GameEventKind : byte
    {
        /// <summary>Nouveau tour de jeu : distribution faite (Count = cartes par joueur).</summary>
        RoundStarted,
        /// <summary>Carte retournée pour lancer le tas.</summary>
        StartCard,
        TurnStarted,
        /// <summary>Cartes posées sur le tas (la dernière est le nouveau dessus).</summary>
        Played,
        /// <summary>2 passe-partout glissés sous le tas.</summary>
        PlayedUnder,
        /// <summary>Pioche volontaire ou faute de jouer (Count = 1 en général).</summary>
        Drew,
        /// <summary>Pénalité de 7 / joker piochée (Count = nombre de cartes).</summary>
        PenaltyDrawn,
        /// <summary>Le cumul de pénalité augmente (Count = total en attente pour le joueur suivant).</summary>
        PenaltyStacked,
        Skipped,
        SuitRequested,
        Passed,
        CheckDeclared,
        /// <summary>« Check » oublié : pénalité piochée (Count).</summary>
        CheckForgotten,
        /// <summary>« Games » : le joueur a posé sa dernière carte.</summary>
        Finished,
        /// <summary>Dernier joueur avec des cartes : éliminé, le tour est fini.</summary>
        Eliminated,
        /// <summary>La pioche est refaite avec le tas (Count = cartes dans la nouvelle pioche).</summary>
        Reshuffled,
        MatchWon,
    }

    /// <summary>
    /// Ce qui vient de se passer, dans l'ordre, pour que la vue l'anime. Seat = place dans le tour en cours
    /// (sauf MatchWon / Eliminated qui donnent aussi PlayerId).
    /// </summary>
    public sealed class GameEvent
    {
        public GameEventKind Kind;
        public int Seat = -1;
        public int PlayerId = -1;
        public IReadOnlyList<Card> Cards = Array.Empty<Card>();
        public int Count;
        public Suit? Suit;

        public override string ToString() =>
            $"{Kind} siège={Seat}{(Cards.Count > 0 ? " " + string.Join(" ", Cards) : "")}" +
            $"{(Count != 0 ? " n=" + Count : "")}{(Suit.HasValue ? " " + Core.Card.SuitSymbol(Suit.Value) : "")}";
    }
}
