using System;
using System.Collections.Generic;
using System.Linq;

namespace Check.Core
{
    public enum PenaltyKind : byte { None, Seven, Joker }

    /// <summary>
    /// Un tour de jeu (une distribution) : de la donne jusqu'à ce qu'un seul joueur ait encore des cartes.
    /// Les places (« sièges ») vont de 0 à SeatCount - 1 dans l'ordre de jeu ; PlayerId(seat) donne le joueur.
    /// </summary>
    public sealed class Round
    {
        public readonly RuleSet Rules;
        IRandom random;
        readonly int[] playerIds;
        readonly List<Card>[] hands;
        readonly List<Card> drawPile;   // dessus = dernier élément
        readonly List<Card> discard;    // dessous = 0, dessus = dernier élément
        readonly bool[] finished;
        readonly List<int> finishOrder;
        // Indices publics (voir MissingCards) : cartes qu'un siège n'avait pas quand il a pioché faute de jouer,
        // et nombre de cartes de sa main encore concernées.
        readonly ulong[] missingMask;
        readonly int[] missingHeld;

        public int Current { get; private set; }
        /// <summary>Couleur demandée par le dernier Valet posé, tant qu'elle n'a pas été jouée.</summary>
        public Suit? RequestedSuit { get; private set; }
        /// <summary>Cartes que le joueur courant devra piocher s'il ne contre pas.</summary>
        public int PendingDraw { get; private set; }
        public PenaltyKind PendingKind { get; private set; }
        /// <summary>
        /// Le joueur courant a déjà pioché ce tour-ci (il peut poser la carte piochée ou passer). Seulement avec
        /// <see cref="RuleSet.MayPlayDrawnCard"/> : par défaut, piocher passe la main au suivant.
        /// </summary>
        public bool HasDrawn { get; private set; }
        public Card? DrawnCard { get; private set; }
        public bool IsOver { get; private set; }
        public int LoserSeat { get; private set; } = -1;
        public int TurnNumber { get; private set; }

        /// <summary>Événements de la donne (distribution, carte retournée, premier tour).</summary>
        public IReadOnlyList<GameEvent> InitialEvents { get; }

        public int SeatCount => hands.Length;
        public int PlayerId(int seat) => playerIds[seat];
        public int SeatOf(int playerId) => Array.IndexOf(playerIds, playerId);
        public IReadOnlyList<Card> Hand(int seat) => hands[seat];
        public int DrawPileCount => drawPile.Count;
        public IReadOnlyList<Card> Discard => discard;
        public Card Top => discard[discard.Count - 1];
        public bool IsFinished(int seat) => finished[seat];
        public IReadOnlyList<int> FinishOrder => finishOrder;
        public int ActiveCount => finished.Count(f => !f);

        /// <summary>
        /// Indice public sur la main de <paramref name="seat"/> : quand un joueur pioche sans pénalité, on en déduit
        /// qu'il n'avait aucune carte jouable à ce moment-là (bit n° <see cref="Card.Id"/> de chaque carte exclue).
        /// L'indice vaut pour les <see cref="MissingCardsHeld"/> cartes les plus anciennes de sa main ; celles piochées
        /// depuis peuvent être n'importe quoi. Tout le monde voit la pioche et le tas : ce n'est pas de la triche.
        /// Si le joueur pose ensuite une carte exclue (il avait pioché exprès), l'indice est effacé.
        /// </summary>
        public ulong MissingCards(int seat) => missingMask[seat];

        /// <summary>Nombre de cartes de la main de <paramref name="seat"/> concernées par <see cref="MissingCards"/>.</summary>
        public int MissingCardsHeld(int seat) => missingHeld[seat];

        /// <summary>
        /// Vrai si l'on sait que <paramref name="seat"/> n'a aucune carte de cette couleur (♠ ♥ ♦ ♣) : il l'a montré
        /// en piochant et n'a pas pioché depuis.
        /// </summary>
        public bool IsKnownToLack(int seat, Suit suit)
        {
            if (missingHeld[seat] < hands[seat].Count) return false;
            ulong suitMask = ((1UL << 13) - 1) << ((int)suit * 13);
            return (missingMask[seat] & suitMask) == suitMask;
        }

        /// <summary>Couleur (rouge / noir) qui s'impose au prochain joker.</summary>
        public CardColor EffectiveColor => RequestedSuit.HasValue ? Card.ColorOf(RequestedSuit.Value) : Top.Color;

        /// <summary>Nouvelle donne : mélange, distribue et retourne la première carte.</summary>
        public Round(RuleSet rules, IReadOnlyList<int> playerIds, int firstSeat, IRandom random)
        {
            if (playerIds.Count < RuleSet.MinPlayers || playerIds.Count > RuleSet.MaxPlayers)
                throw new ArgumentException($"De {RuleSet.MinPlayers} à {RuleSet.MaxPlayers} joueurs");
            Rules = rules;
            this.random = random;
            this.playerIds = playerIds.ToArray();
            int n = playerIds.Count;
            hands = new List<Card>[n];
            for (int i = 0; i < n; i++) hands[i] = new List<Card>();
            finished = new bool[n];
            finishOrder = new List<int>();
            missingMask = new ulong[n];
            missingHeld = new int[n];
            discard = new List<Card>();
            drawPile = Card.FullDeck();
            random.Shuffle(drawPile);

            int perPlayer = rules.CardsToDeal(n);
            for (int r = 0; r < perPlayer; r++)
                for (int s = 0; s < n; s++)
                    hands[(firstSeat + s) % n].Add(Pop());

            var events = new List<GameEvent>
            {
                new GameEvent { Kind = GameEventKind.RoundStarted, Seat = firstSeat, Count = perPlayer },
            };
            discard.Add(FlipStartCard());
            events.Add(new GameEvent { Kind = GameEventKind.StartCard, Cards = new[] { Top } });
            Current = firstSeat;
            events.Add(new GameEvent { Kind = GameEventKind.TurnStarted, Seat = Current });
            InitialEvents = events;
        }

        Round(Round o, IRandom random)
        {
            Rules = o.Rules;
            this.random = random;
            playerIds = (int[])o.playerIds.Clone();
            hands = o.hands.Select(h => new List<Card>(h)).ToArray();
            drawPile = new List<Card>(o.drawPile);
            discard = new List<Card>(o.discard);
            finished = (bool[])o.finished.Clone();
            finishOrder = new List<int>(o.finishOrder);
            missingMask = (ulong[])o.missingMask.Clone();
            missingHeld = (int[])o.missingHeld.Clone();
            Current = o.Current;
            RequestedSuit = o.RequestedSuit;
            PendingDraw = o.PendingDraw;
            PendingKind = o.PendingKind;
            HasDrawn = o.HasDrawn;
            DrawnCard = o.DrawnCard;
            IsOver = o.IsOver;
            LoserSeat = o.LoserSeat;
            TurnNumber = o.TurnNumber;
            InitialEvents = Array.Empty<GameEvent>();
        }

        Round(RuleSet rules, List<Card>[] hands, List<Card> discard, List<Card> drawPile, int current, IRandom random)
        {
            Rules = rules;
            this.random = random;
            this.hands = hands;
            this.discard = discard;
            this.drawPile = drawPile;
            playerIds = Enumerable.Range(0, hands.Length).ToArray();
            finished = new bool[hands.Length];
            finishOrder = new List<int>();
            missingMask = new ulong[hands.Length];
            missingHeld = new int[hands.Length];
            Current = current;
            InitialEvents = Array.Empty<GameEvent>();
        }

        /// <summary>
        /// Position construite à la main (tests, scénarios). <paramref name="discard"/> va du dessous au dessus ;
        /// la pioche se tire par la fin de <paramref name="drawPile"/>.
        /// </summary>
        public static Round FromState(RuleSet rules, IEnumerable<IEnumerable<Card>> hands, IEnumerable<Card> discard,
            IEnumerable<Card> drawPile, int current = 0, IRandom random = null, Suit? requestedSuit = null,
            int pendingDraw = 0, PenaltyKind pendingKind = PenaltyKind.None)
        {
            var round = new Round(rules, hands.Select(h => h.ToList()).ToArray(), discard.ToList(), drawPile.ToList(),
                current, random ?? new SystemRandom(0))
            {
                RequestedSuit = requestedSuit,
                PendingDraw = pendingDraw,
                PendingKind = pendingDraw > 0 ? pendingKind : PenaltyKind.None,
            };
            var all = round.hands.SelectMany(h => h).Concat(round.discard).Concat(round.drawPile).ToList();
            if (all.Count != all.Distinct().Count()) throw new ArgumentException("Carte en double dans la position");
            if (round.discard.Count == 0) throw new ArgumentException("Le tas ne peut pas être vide");
            return round;
        }

        public Round Clone(IRandom random = null) => new Round(this, random ?? this.random);

        /// <summary>
        /// Pour l'IA : redistribue au hasard tout ce que <paramref name="viewerSeat"/> ne voit pas
        /// (mains adverses et pioche), en gardant le nombre de cartes de chacun. Avec <paramref name="useClues"/>,
        /// les mains tirées respectent les indices publics (<see cref="MissingCards"/>) quand c'est possible.
        /// </summary>
        public void RedealHiddenCards(int viewerSeat, IRandom rng, bool useClues = false)
        {
            if (useClues && RedealWithClues(viewerSeat, rng)) return;
            var pool = new List<Card>(drawPile);
            for (int s = 0; s < hands.Length; s++)
                if (s != viewerSeat) pool.AddRange(hands[s]);
            // Ordre canonique avant le mélange : à graine égale, le résultat ne doit pas dépendre de la place
            // réelle des cartes cachées (sinon le choix de l'IA en dépendrait).
            pool.Sort((a, b) => a.Id.CompareTo(b.Id));
            rng.Shuffle(pool);
            int k = 0;
            for (int s = 0; s < hands.Length; s++)
            {
                if (s == viewerSeat) continue;
                for (int i = 0; i < hands[s].Count; i++) hands[s][i] = pool[k++];
            }
            for (int i = 0; i < drawPile.Count; i++) drawPile[i] = pool[k++];
            if (DrawnCard.HasValue && Current != viewerSeat) DrawnCard = hands[Current][hands[Current].Count - 1];
        }

        /// <summary>
        /// Redistribution qui donne d'abord à chaque siège indicé ses cartes « anciennes » hors des cartes exclues,
        /// puis complète au hasard. Renvoie faux (rien n'est modifié) si aucun siège n'a d'indice. Un indice
        /// impossible à respecter (trop peu de cartes cachées compatibles) est ignoré.
        /// </summary>
        bool RedealWithClues(int viewerSeat, IRandom rng)
        {
            bool any = false;
            for (int s = 0; s < hands.Length; s++)
                if (s != viewerSeat && missingHeld[s] > 0 && missingMask[s] != 0) { any = true; break; }
            if (!any) return false;

            var pool = new List<Card>(drawPile);
            for (int s = 0; s < hands.Length; s++)
                if (s != viewerSeat) pool.AddRange(hands[s]);
            pool.Sort((a, b) => a.Id.CompareTo(b.Id));
            rng.Shuffle(pool);
            var taken = new bool[pool.Count];
            var dealt = new List<Card>[hands.Length];

            // Sièges indicés, du plus contraint (le plus de cartes exclues) au moins contraint ; ordre stable.
            var order = new List<int>();
            for (int s = 0; s < hands.Length; s++)
                if (s != viewerSeat && missingHeld[s] > 0 && missingMask[s] != 0) order.Add(s);
            order.Sort((a, b) =>
            {
                int c = PopCount(missingMask[b]).CompareTo(PopCount(missingMask[a]));
                return c != 0 ? c : a.CompareTo(b);
            });
            foreach (int s in order)
            {
                int need = Math.Min(missingHeld[s], hands[s].Count);
                var picked = new List<int>(need);
                for (int i = 0; i < pool.Count && picked.Count < need; i++)
                    if (!taken[i] && (missingMask[s] & (1UL << pool[i].Id)) == 0) picked.Add(i);
                if (picked.Count < need) continue; // indice intenable : main tirée sans contrainte
                dealt[s] = new List<Card>(hands[s].Count);
                foreach (int i in picked) { taken[i] = true; dealt[s].Add(pool[i]); }
            }

            int k = 0;
            Card NextFree()
            {
                while (taken[k]) k++;
                taken[k] = true;
                return pool[k];
            }
            for (int s = 0; s < hands.Length; s++)
            {
                if (s == viewerSeat) continue;
                var h = dealt[s] ?? new List<Card>(hands[s].Count);
                // Les cartes libres (piochées depuis l'indice) vont en fin de main, comme la carte qu'on vient de piocher.
                while (h.Count < hands[s].Count) h.Add(NextFree());
                for (int i = 0; i < h.Count; i++) hands[s][i] = h[i];
            }
            for (int i = 0; i < drawPile.Count; i++) drawPile[i] = NextFree();
            if (DrawnCard.HasValue && Current != viewerSeat) DrawnCard = hands[Current][hands[Current].Count - 1];
            return true;
        }

        static int PopCount(ulong v)
        {
            int c = 0;
            while (v != 0) { v &= v - 1; c++; }
            return c;
        }

        /// <summary>Met à jour l'indice public quand <paramref name="seat"/> pioche sans pénalité (avant la pioche).</summary>
        void NoteVoluntaryDraw(int seat)
        {
            ulong mask = 0;
            for (int id = 0; id <= Card.RedJokerId; id++)
                if (CanStartWith(new Card(id))) mask |= 1UL << id;
            int count = hands[seat].Count;
            // Toute la main était déjà indicée : les deux indices se cumulent. Sinon on garde le plus récent.
            if (missingHeld[seat] >= count && missingHeld[seat] > 0) missingMask[seat] |= mask;
            else missingMask[seat] = mask;
            missingHeld[seat] = count;
            if (count == 0) missingMask[seat] = 0;
        }

        /// <summary>Met à jour l'indice public quand <paramref name="seat"/> pose <paramref name="c"/> (avant de la retirer).</summary>
        void NotePlayedCard(int seat, Card c)
        {
            if (missingHeld[seat] == 0) return;
            if ((missingMask[seat] & (1UL << c.Id)) != 0)
            {
                // Carte exclue : elle vient forcément des cartes piochées depuis. S'il n'y en a pas, le joueur avait
                // pioché exprès : l'indice était faux, on l'oublie.
                if (hands[seat].Count - missingHeld[seat] <= 0) { missingMask[seat] = 0; missingHeld[seat] = 0; }
                return;
            }
            // Carte permise : on ne sait pas d'où elle vient ; supposer qu'elle sort des cartes indicées garde un
            // indice toujours vrai (au pire plus faible que la réalité).
            missingHeld[seat]--;
            if (missingHeld[seat] == 0) missingMask[seat] = 0;
        }

        Card Pop()
        {
            var c = drawPile[drawPile.Count - 1];
            drawPile.RemoveAt(drawPile.Count - 1);
            return c;
        }

        Card FlipStartCard()
        {
            for (int tries = 0; tries < 200; tries++)
            {
                var c = Pop();
                if (!Rules.RedrawSpecialStartCard || !RuleSet.IsSpecial(c.Rank) || tries == 199) return c;
                drawPile.Insert(random.Next(drawPile.Count), c);
            }
            throw new InvalidOperationException("inatteignable");
        }

        // ---------------------------------------------------------------- Légalité

        /// <summary>La carte peut-elle ouvrir une pose sur le dessus actuel ?</summary>
        public bool CanStartWith(Card c)
        {
            if (PendingDraw > 0)
            {
                if (PendingKind != PenaltyKind.Seven) return false;
                return c.Rank == Rank.Seven || (Rules.JokerCountersSeven && c.IsJoker && c.Color == Top.Color);
            }
            if (c.Rank == Rank.Two) return true;
            if (c.Rank == Rank.Jack && Rules.JackIsWild) return true;
            if (c.IsJoker) return c.Color == EffectiveColor;
            if (RequestedSuit.HasValue) return c.Suit == RequestedSuit.Value || c.Rank == Top.Rank;
            if (Top.IsJoker) return c.Color == Top.Color;
            return c.Suit == Top.Suit || c.Rank == Top.Rank;
        }

        public bool IsLegal(TurnAction action) => IsLegal(action, out _);

        public bool IsLegal(TurnAction action, out string reason)
        {
            reason = null;
            if (IsOver) { reason = "Le tour est fini"; return false; }
            switch (action.Kind)
            {
                case ActionKind.Draw:
                    if (PendingDraw > 0) return true;
                    if (HasDrawn) { reason = "Déjà pioché ce tour-ci"; return false; }
                    if (!Rules.MayDrawVoluntarily && HasAnyPlay()) { reason = "Une carte est jouable"; return false; }
                    return true;

                case ActionKind.Pass:
                    if (!HasDrawn || PendingDraw > 0) { reason = "Il faut piocher avant de passer"; return false; }
                    return true;
            }

            var cards = action.Cards;
            var hand = hands[Current];
            if (cards.Count == 0) { reason = "Aucune carte"; return false; }
            if (cards.Distinct().Count() != cards.Count) { reason = "Carte en double"; return false; }
            if (cards.Any(c => !hand.Contains(c))) { reason = "Carte absente de la main"; return false; }
            var rank = cards[0].Rank;
            if (cards.Any(c => c.Rank != rank)) { reason = "Les cartes posées ensemble doivent avoir la même valeur"; return false; }
            if (cards.Count > 1 && (!Rules.AllowMultiples || rank == Rank.Joker)) { reason = "Pose multiple interdite"; return false; }
            if (HasDrawn)
            {
                if (!Rules.MayPlayDrawnCard || !DrawnCard.HasValue || !cards.Contains(DrawnCard.Value))
                { reason = "Après avoir pioché, seule la carte piochée peut être posée"; return false; }
            }
            if (!CanStartWith(cards[0])) { reason = "Carte non jouable sur " + Top; return false; }
            bool jackOnTop = rank == Rank.Jack;
            if (jackOnTop && !action.RequestedSuit.HasValue) { reason = "Le Valet doit demander une couleur"; return false; }
            if (!jackOnTop && action.RequestedSuit.HasValue) { reason = "Seul un Valet demande une couleur"; return false; }
            return true;
        }

        public bool HasAnyPlay() => hands[Current].Any(CanStartWith);

        /// <summary>
        /// Toutes les actions légales du joueur courant. Les poses annoncent « Check » quand il ne restera
        /// qu'une carte. Une pose multiple apparaît pour chaque carte possible sur le dessus.
        /// </summary>
        public List<TurnAction> LegalActions()
        {
            var actions = new List<TurnAction>();
            if (IsOver) return actions;
            var hand = hands[Current];
            foreach (var group in hand.GroupBy(c => c.Rank))
            {
                var g = group.ToList();
                int k = g.Count;
                bool multi = Rules.AllowMultiples && group.Key != Rank.Joker;
                for (int mask = 1; mask < (1 << k); mask++)
                {
                    if (!multi && (mask & (mask - 1)) != 0) continue;
                    var subset = new List<Card>();
                    for (int i = 0; i < k; i++) if ((mask & (1 << i)) != 0) subset.Add(g[i]);
                    if (HasDrawn && (!DrawnCard.HasValue || !subset.Contains(DrawnCard.Value))) continue;
                    AddOrderings(subset, hand.Count, actions);
                }
            }
            if (HasDrawn && PendingDraw == 0)
            {
                if (!Rules.MayPlayDrawnCard) actions.Clear();
                actions.Add(TurnAction.Pass);
            }
            else if (PendingDraw > 0 || Rules.MayDrawVoluntarily || actions.Count == 0)
            {
                actions.Add(TurnAction.Draw);
            }
            return actions;
        }

        void AddOrderings(List<Card> subset, int handCount, List<TurnAction> actions)
        {
            bool check = handCount - subset.Count == 1;
            var legalFirsts = subset.Where(CanStartWith).ToList();
            if (legalFirsts.Count == 0) return;
            var rank = subset[0].Rank;
            // Les 2 passent sous le tas : l'ordre ne change rien.
            var tops = rank == Rank.Two ? new List<Card> { subset[subset.Count - 1] } : subset;
            foreach (var top in tops)
            {
                Card first;
                if (subset.Count == 1) first = top;
                else
                {
                    var f = legalFirsts.Where(c => c != top).Take(1).ToList();
                    if (f.Count == 0) continue;
                    first = f[0];
                }
                var order = new List<Card> { first };
                order.AddRange(subset.Where(c => c != first && c != top));
                if (top != first) order.Add(top);
                if (rank == Rank.Jack)
                    for (int s = 0; s < 4; s++) actions.Add(TurnAction.Play(order, (Suit)s, check));
                else
                    actions.Add(TurnAction.Play(order, null, check));
            }
        }

        // ---------------------------------------------------------------- Jeu

        public IReadOnlyList<GameEvent> Apply(TurnAction action)
        {
            if (!IsLegal(action, out var reason)) throw new InvalidOperationException($"Action illégale ({action}) : {reason}");
            var events = new List<GameEvent>();
            switch (action.Kind)
            {
                case ActionKind.Play: ApplyPlay(action, events); break;
                case ActionKind.Draw: ApplyDraw(events); break;
                default:
                    events.Add(new GameEvent { Kind = GameEventKind.Passed, Seat = Current });
                    Advance(Current, 0, events);
                    break;
            }
            return events;
        }

        void ApplyPlay(TurnAction action, List<GameEvent> events)
        {
            int seat = Current;
            var cards = action.Cards;
            foreach (var c in cards) { NotePlayedCard(seat, c); hands[seat].Remove(c); }
            int skip = 0;

            if (cards[0].Rank == Rank.Two)
            {
                discard.InsertRange(0, cards);
                events.Add(new GameEvent { Kind = GameEventKind.PlayedUnder, Seat = seat, Cards = cards });
            }
            else
            {
                discard.AddRange(cards);
                events.Add(new GameEvent { Kind = GameEventKind.Played, Seat = seat, Cards = cards });
                RequestedSuit = null;
                switch (cards[0].Rank)
                {
                    case Rank.Seven:
                        PendingDraw += Rules.SevenDraw * cards.Count;
                        PendingKind = PenaltyKind.Seven;
                        events.Add(new GameEvent { Kind = GameEventKind.PenaltyStacked, Seat = seat, Count = PendingDraw });
                        break;
                    case Rank.Joker:
                        PendingDraw += Rules.JokerDraw;
                        PendingKind = PenaltyKind.Joker;
                        events.Add(new GameEvent { Kind = GameEventKind.PenaltyStacked, Seat = seat, Count = PendingDraw });
                        break;
                    case Rank.Ace:
                        // On ne saute jamais le poseur lui-même : à deux, un ou deux As = on rejoue.
                        skip = Math.Min(cards.Count, ActiveCount - 1);
                        break;
                    case Rank.Jack:
                        RequestedSuit = action.RequestedSuit;
                        events.Add(new GameEvent { Kind = GameEventKind.SuitRequested, Seat = seat, Suit = RequestedSuit });
                        break;
                }
            }

            int left = hands[seat].Count;
            if (left == 1)
            {
                if (action.DeclaresCheck)
                    events.Add(new GameEvent { Kind = GameEventKind.CheckDeclared, Seat = seat });
                else if (Rules.ForgotCheckPenalty > 0)
                {
                    var drawn = DrawCards(seat, Rules.ForgotCheckPenalty, events);
                    events.Add(new GameEvent { Kind = GameEventKind.CheckForgotten, Seat = seat, Cards = drawn, Count = drawn.Count });
                }
            }
            else if (left == 0)
            {
                finished[seat] = true;
                finishOrder.Add(seat);
                events.Add(new GameEvent { Kind = GameEventKind.Finished, Seat = seat });
                if (ActiveCount == 1)
                {
                    IsOver = true;
                    LoserSeat = Array.IndexOf(finished, false);
                    events.Add(new GameEvent { Kind = GameEventKind.Eliminated, Seat = LoserSeat, PlayerId = playerIds[LoserSeat] });
                    return;
                }
            }
            Advance(seat, skip, events);
        }

        void ApplyDraw(List<GameEvent> events)
        {
            int seat = Current;
            if (PendingDraw > 0)
            {
                var penalty = DrawCards(seat, PendingDraw, events);
                PendingDraw = 0;
                PendingKind = PenaltyKind.None;
                events.Add(new GameEvent { Kind = GameEventKind.PenaltyDrawn, Seat = seat, Cards = penalty, Count = penalty.Count });
                Advance(seat, 0, events);
                return;
            }

            NoteVoluntaryDraw(seat);
            var drawn = DrawCards(seat, 1, events);
            events.Add(new GameEvent { Kind = GameEventKind.Drew, Seat = seat, Cards = drawn, Count = drawn.Count });
            HasDrawn = true;
            DrawnCard = drawn.Count > 0 ? drawn[0] : (Card?)null;
            if (!Rules.MayPlayDrawnCard)
            {
                // Règle de Kevin : piocher termine le tour. La carte piochée reste cachée et personne ne sait si
                // elle se posait : l'indice ne couvre que les cartes d'avant la pioche (pas de « passe »).
                Advance(seat, 0, events);
                return;
            }
            // Variante : carte piochée injouable, on passe tout de suite.
            if (!DrawnCard.HasValue || !CanStartWith(DrawnCard.Value))
            {
                // Passe automatique : tout le monde voit que la carte piochée ne se posait pas non plus, l'indice
                // couvre donc toute la main.
                if (DrawnCard.HasValue && missingMask[seat] != 0)
                    missingHeld[seat] = hands[seat].Count;
                events.Add(new GameEvent { Kind = GameEventKind.Passed, Seat = seat });
                Advance(seat, 0, events);
            }
        }

        List<Card> DrawCards(int seat, int count, List<GameEvent> events)
        {
            var drawn = new List<Card>(count);
            for (int i = 0; i < count; i++)
            {
                if (drawPile.Count == 0 && !Reshuffle(events)) break;
                var c = Pop();
                hands[seat].Add(c);
                drawn.Add(c);
            }
            return drawn;
        }

        bool Reshuffle(List<GameEvent> events)
        {
            if (discard.Count <= 1) return false;
            var top = Top;
            drawPile.AddRange(discard.Take(discard.Count - 1));
            discard.Clear();
            discard.Add(top);
            random.Shuffle(drawPile);
            events.Add(new GameEvent { Kind = GameEventKind.Reshuffled, Count = drawPile.Count });
            return true;
        }

        int NextActive(int seat)
        {
            int n = hands.Length;
            for (int i = 1; i <= n; i++)
            {
                int s = (seat + i) % n;
                if (!finished[s]) return s;
            }
            return seat;
        }

        void Advance(int fromSeat, int skip, List<GameEvent> events)
        {
            int next = NextActive(fromSeat);
            for (int i = 0; i < skip; i++)
            {
                events.Add(new GameEvent { Kind = GameEventKind.Skipped, Seat = next });
                next = NextActive(next);
            }
            Current = next;
            HasDrawn = false;
            DrawnCard = null;
            TurnNumber++;
            events.Add(new GameEvent { Kind = GameEventKind.TurnStarted, Seat = Current });
        }
    }
}
