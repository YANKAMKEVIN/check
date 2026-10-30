using System;
using System.Collections.Generic;
using System.Linq;

namespace Check.Core
{
    public enum ActionKind : byte { Play, Draw, Pass }

    /// <summary>
    /// Action d'un joueur à son tour. Pour <see cref="ActionKind.Play"/>, les cartes sont dans l'ordre de pose :
    /// la dernière devient la carte du dessus (sauf les 2, glissés sous le tas).
    /// </summary>
    public sealed class TurnAction : IEquatable<TurnAction>
    {
        public readonly ActionKind Kind;
        public readonly IReadOnlyList<Card> Cards;
        /// <summary>Couleur demandée quand un Valet finit sur le dessus.</summary>
        public readonly Suit? RequestedSuit;
        /// <summary>Le joueur annonce « Check » en posant son avant-dernière carte.</summary>
        public readonly bool DeclaresCheck;

        TurnAction(ActionKind kind, IReadOnlyList<Card> cards, Suit? requested, bool check)
        {
            Kind = kind;
            Cards = cards;
            RequestedSuit = requested;
            DeclaresCheck = check;
        }

        public static readonly TurnAction Draw = new TurnAction(ActionKind.Draw, Array.Empty<Card>(), null, false);
        public static readonly TurnAction Pass = new TurnAction(ActionKind.Pass, Array.Empty<Card>(), null, false);

        public static TurnAction Play(IEnumerable<Card> cards, Suit? requestedSuit = null, bool declaresCheck = false) =>
            new TurnAction(ActionKind.Play, cards.ToArray(), requestedSuit, declaresCheck);

        public static TurnAction Play(Card card, Suit? requestedSuit = null, bool declaresCheck = false) =>
            new TurnAction(ActionKind.Play, new[] { card }, requestedSuit, declaresCheck);

        public TurnAction WithCheck(bool declares) => new TurnAction(Kind, Cards, RequestedSuit, declares);

        public Card TopCard => Cards[Cards.Count - 1];

        public bool Equals(TurnAction other) =>
            other != null && other.Kind == Kind && other.RequestedSuit == RequestedSuit &&
            other.DeclaresCheck == DeclaresCheck && other.Cards.SequenceEqual(Cards);

        public override bool Equals(object obj) => Equals(obj as TurnAction);

        public override int GetHashCode()
        {
            int h = (int)Kind * 31 + (RequestedSuit.HasValue ? (int)RequestedSuit.Value + 1 : 0);
            foreach (var c in Cards) h = h * 59 + c.Id;
            return h * 2 + (DeclaresCheck ? 1 : 0);
        }

        public override string ToString() => Kind switch
        {
            ActionKind.Play => string.Join(" ", Cards) +
                               (RequestedSuit.HasValue ? " → " + Card.SuitSymbol(RequestedSuit.Value) : "") +
                               (DeclaresCheck ? " (Check)" : ""),
            ActionKind.Draw => "pioche",
            _ => "passe",
        };
    }
}
