using System;
using System.Collections.Generic;
using System.Linq;
using Check.Core;
using Check.Game.Audio;
using Check.Game.View;
using Check.Game.Visuals;

namespace Check.Game.Match
{
    /// <summary>
    /// Ce que le joueur humain prépare pendant son tour : sélection de cartes (ordre de sélection = ordre de pose,
    /// la dernière finit au-dessus), annonce « Check », puis envoi au contrôleur. Toutes les entrées
    /// (toucher, double-tap, glisser, boutons, recette automatique) passent par ici.
    /// </summary>
    public sealed class HumanTurn
    {
        readonly GameController game;
        readonly CardTable table;
        readonly SoundBank sfx;
        readonly List<Card> selection = new List<Card>();

        /// <summary>Vrai quand une fenêtre de l'interface bloque le jeu.</summary>
        public Func<bool> UiBlocked = () => false;
        /// <summary>Ouvre le sélecteur de couleur du Valet ; rappel avec null si annulé.</summary>
        public Action<Action<Suit?>> AskSuit;
        /// <summary>Message d'explication quand une action est refusée.</summary>
        public event Action<string> Refused;
        /// <summary>Annonce « Check » touchée (pour l'effet visuel / haptique).</summary>
        public event Action CheckArmedChanged;

        public bool CheckArmed { get; private set; }
        public IReadOnlyList<Card> Selection => selection;
        bool suitPending;

        public HumanTurn(GameController game, CardTable table, SoundBank sfx)
        {
            this.game = game;
            this.table = table;
            this.sfx = sfx;
            table.HumanHighlight = Highlight;
            table.IsSelected = c => selection.Contains(c);
        }

        Round R => game.Round;
        IReadOnlyList<Card> Hand => game.HumanSeat >= 0 ? R.Hand(game.HumanSeat) : Array.Empty<Card>();

        /// <summary>Le joueur peut agir : son tour, file d'animations vide, aucune fenêtre ouverte.</summary>
        public bool CanAct => game.CanHumanAct && !UiBlocked() && !suitPending;

        /// <summary>À appeler à chaque image : vide la sélection quand ce n'est plus le tour du joueur.</summary>
        public void Tick()
        {
            if (!game.IsHumanTurn)
            {
                if (selection.Count > 0 || CheckArmed)
                {
                    selection.Clear();
                    CheckArmed = false;
                }
                return;
            }
            var hand = Hand;
            selection.RemoveAll(c => !hand.Contains(c));
        }

        // ---------------------------------------------------------------- Lecture de l'état (interface)

        public CardHighlight Highlight(Card c)
        {
            if (selection.Contains(c)) return CardHighlight.Selected;
            if (!game.IsHumanTurn || table.IsBusy || R == null) return CardHighlight.None;
            return CanSelect(c) ? CardHighlight.Playable : CardHighlight.Dimmed;
        }

        bool CanStart(Card c)
        {
            var r = R;
            if (r == null) return false;
            if (r.HasDrawn) return r.DrawnCard.HasValue && r.DrawnCard.Value == c && r.CanStartWith(c);
            return r.CanStartWith(c);
        }

        bool CanJoin(Card c) =>
            selection.Count > 0 && R != null && R.Rules.AllowMultiples && c.Rank != Rank.Joker && c.Rank == selection[0].Rank;

        bool CanSelect(Card c) => CanStart(c) || CanJoin(c);

        /// <summary>Les cartes sélectionnées, remises dans un ordre légal si besoin (la première doit être jouable).</summary>
        List<Card> Ordered(IReadOnlyList<Card> cards)
        {
            var order = new List<Card>(cards);
            if (order.Count > 1 && !CanStart(order[0]) && R != null)
            {
                // Une carte jouable passe en premier, en gardant si possible la dernière choisie au-dessus.
                int j = order.FindIndex(0, order.Count - 1, c => R.CanStartWith(c));
                if (j < 0) j = order.FindIndex(c => R.CanStartWith(c));
                if (j > 0)
                {
                    var first = order[j];
                    order.RemoveAt(j);
                    order.Insert(0, first);
                }
            }
            return order;
        }

        bool IsPlayable(IReadOnlyList<Card> cards)
        {
            if (R == null || cards.Count == 0) return false;
            var order = Ordered(cards);
            Suit? suit = order[0].Rank == Rank.Jack ? Suit.Spades : (Suit?)null;
            return R.IsLegal(TurnAction.Play(order, suit));
        }

        /// <summary>La sélection actuelle peut être posée.</summary>
        public bool SelectionPlayable => CanAct && IsPlayable(selection);

        /// <summary>Le bouton « Check ! » a du sens : la pose laissera une seule carte.</summary>
        public bool CheckAvailable
        {
            get
            {
                if (!game.IsHumanTurn || table.IsBusy || R == null) return false;
                int n = Hand.Count;
                if (n < 2) return false;
                if (selection.Count > 0) return n - selection.Count == 1;
                return n == 2 && Hand.Any(CanStart);
            }
        }

        public int PendingDraw => R?.PendingDraw ?? 0;
        public bool HasDrawn => R?.HasDrawn ?? false;

        // ---------------------------------------------------------------- Entrées

        public void TapCard(Card c)
        {
            if (!CanAct)
            {
                if (!game.IsHumanTurn && !table.IsBusy && game.Round != null && !game.Round.IsOver && !UiBlocked())
                    Refused?.Invoke("Ce n'est pas ton tour");
                return;
            }
            if (selection.Contains(c))
            {
                selection.Remove(c);
                sfx.Play(Sfx.Deselect, 0.7f);
                return;
            }
            if (CanJoin(c))
            {
                selection.Add(c);
                sfx.Play(Sfx.Select);
                return;
            }
            if (CanStart(c))
            {
                selection.Clear();
                selection.Add(c);
                sfx.Play(Sfx.Select);
                return;
            }
            sfx.Play(Sfx.Illegal, 0.6f);
            Refused?.Invoke(WhyNot(c));
        }

        /// <summary>Double-tap (ou glisser vers le tas) : pose directement la carte, avec celles déjà choisies.</summary>
        public void QuickPlay(Card c)
        {
            if (!CanAct) return;
            var cards = selection.Where(x => x != c).ToList();
            if (cards.Count > 0 && cards[0].Rank != c.Rank) cards.Clear();
            cards.Add(c);
            if (!IsPlayable(cards))
            {
                sfx.Play(Sfx.Illegal, 0.6f);
                Refused?.Invoke(WhyNot(c));
                return;
            }
            selection.Clear();
            selection.AddRange(cards);
            PressPlay();
        }

        public void PressPlay()
        {
            if (!CanAct || selection.Count == 0) return;
            var cards = Ordered(selection);
            if (cards[0].Rank == Rank.Jack)
            {
                if (AskSuit == null) { Submit(cards, Suit.Spades); return; }
                suitPending = true;
                AskSuit(suit =>
                {
                    suitPending = false;
                    if (suit.HasValue) Submit(cards, suit);
                });
                return;
            }
            Submit(cards, null);
        }

        void Submit(List<Card> cards, Suit? suit)
        {
            var action = TurnAction.Play(cards, suit, CheckArmed);
            if (game.TryHumanAction(action, out var reason))
            {
                selection.Clear();
                CheckArmed = false;
            }
            else
            {
                sfx.Play(Sfx.Illegal, 0.6f);
                Refused?.Invoke(reason);
            }
        }

        public void PressCheck()
        {
            if (!CanAct || !CheckAvailable) return;
            CheckArmed = !CheckArmed;
            if (CheckArmed) sfx.Play(Sfx.Check);
            else sfx.Play(Sfx.Deselect, 0.6f);
            CheckArmedChanged?.Invoke();
        }

        /// <summary>Piocher (ou prendre la pénalité) : toucher la pioche ou le bouton.</summary>
        public void Draw()
        {
            if (!CanAct) return;
            if (!R.IsLegal(TurnAction.Draw, out var reason))
            {
                sfx.Play(Sfx.Illegal, 0.6f);
                Refused?.Invoke(R.HasDrawn ? "Tu as déjà pioché : pose la carte piochée ou passe" : reason);
                return;
            }
            selection.Clear();
            if (!game.TryHumanAction(TurnAction.Draw, out reason)) Refused?.Invoke(reason);
        }

        public void Pass()
        {
            if (!CanAct) return;
            if (!game.TryHumanAction(TurnAction.Pass, out var reason))
            {
                sfx.Play(Sfx.Illegal, 0.6f);
                Refused?.Invoke(reason);
                return;
            }
            selection.Clear();
            CheckArmed = false;
        }

        public void ClearSelection() => selection.Clear();

        string WhyNot(Card c)
        {
            var r = R;
            if (r == null) return "";
            if (r.PendingDraw > 0)
                return r.PendingKind == PenaltyKind.Joker
                    ? $"Un joker ne se contre pas : prends {r.PendingDraw} cartes"
                    : $"Contre avec un 7{(r.Rules.JokerCountersSeven ? " (ou le joker de la couleur)" : "")}, sinon prends {r.PendingDraw} cartes";
            if (r.HasDrawn) return "Tu ne peux poser que la carte piochée (ou passer)";
            if (r.RequestedSuit.HasValue) return $"Couleur demandée : {Card.SuitSymbol(r.RequestedSuit.Value)} (ou un Valet)";
            if (c.IsJoker) return c.Color == CardColor.Black ? "Le joker noir se pose sur une carte noire" : "Le joker rouge se pose sur une carte rouge";
            if (r.Top.IsJoker) return "Après un joker : une carte de sa couleur (" + (r.Top.Color == CardColor.Black ? "♠ ♣" : "♥ ♦") + ")";
            return $"Même couleur ou même valeur que {r.Top}";
        }
    }
}
