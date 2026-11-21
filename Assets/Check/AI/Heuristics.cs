using System.Collections.Generic;
using Check.Core;

namespace Check.AI
{
    /// <summary>
    /// Évaluation rapide d'une action du point de vue de celui qui la joue. N'utilise que ce que ce siège voit :
    /// sa main, le tas, le nombre de cartes des autres, la pénalité et la couleur demandée en cours.
    /// Sert au niveau Moyen et de politique de jeu dans les simulations Monte-Carlo.
    /// </summary>
    public static class Heuristics
    {
        /// <summary>Score d'une action finissant la main : toujours la meilleure.</summary>
        public const double FinishScore = 1000;

        /// <summary>Prochain siège encore en jeu après <paramref name="seat"/>.</summary>
        public static int NextActive(Round r, int seat)
        {
            int n = r.SeatCount;
            for (int i = 1; i <= n; i++)
            {
                int s = (seat + i) % n;
                if (!r.IsFinished(s)) return s;
            }
            return seat;
        }

        /// <summary>Plus petite main parmi les adversaires encore en jeu (nombre de cartes, visible de tous).</summary>
        public static int MinOpponentCount(Round r, int seat)
        {
            int min = int.MaxValue;
            for (int s = 0; s < r.SeatCount; s++)
                if (s != seat && !r.IsFinished(s) && r.Hand(s).Count < min) min = r.Hand(s).Count;
            return min == int.MaxValue ? 0 : min;
        }

        /// <summary>Coût de « gaspiller » une carte spéciale qu'on aurait pu garder pour contrer ou finir.</summary>
        static double KeepValue(Rank rank)
        {
            switch (rank)
            {
                case Rank.Jack: return 9;
                case Rank.Joker: return 6;
                case Rank.Two: return 6;
                case Rank.Seven: return 3.5;
                case Rank.Ace: return 1;
                default: return 0;
            }
        }

        static bool Contains(IReadOnlyList<Card> cards, Card c)
        {
            for (int i = 0; i < cards.Count; i++) if (cards[i] == c) return true;
            return false;
        }

        /// <summary>
        /// Bonus quand le dessus laissé au suivant ne lui laisse, d'après les indices publics
        /// (<see cref="Round.MissingCards"/>), aucune carte jouable parmi celles qu'il avait en piochant.
        /// </summary>
        public const double BlockBonus = 7;

        /// <summary>
        /// Part (0 à 1) de la main de <paramref name="next"/> dont on sait qu'elle ne peut rien poser sur
        /// <paramref name="top"/> (couleur demandée <paramref name="requested"/>) : 1 = il devra piocher.
        /// Les cartes que <paramref name="viewer"/> voit (sa main, le tas) ne peuvent pas être chez le suivant.
        /// </summary>
        public static double BlockedShare(Round r, int viewer, int next, Card top, Suit? requested)
        {
            int held = r.MissingCardsHeld(next), count = r.Hand(next).Count;
            if (held <= 0 || count <= 0) return 0;
            ulong excluded = r.MissingCards(next);
            foreach (var c in r.Hand(viewer)) excluded |= 1UL << c.Id;
            var discard = r.Discard;
            for (int i = 0; i < discard.Count; i++) excluded |= 1UL << discard[i].Id;
            // Cartes cachées (ni chez soi ni au tas) que le suivant pourrait avoir, et celles qui le sauveraient.
            int hidden = 0, saving = 0;
            for (int id = 0; id <= Card.RedJokerId; id++)
            {
                if ((excluded & (1UL << id)) != 0) continue;
                hidden++;
                if (Playable(r.Rules, new Card(id), top, requested)) saving++;
            }
            if (hidden <= 0) return 0;
            // Chance qu'aucune de ses cartes indicées ne soit une carte qui le sauve (tirage approché avec remise).
            double p = 1;
            double miss = 1 - (double)saving / hidden;
            int n = System.Math.Min(held, count);
            for (int i = 0; i < n; i++) p *= miss;
            return p * n / count;
        }

        /// <summary>Carte jouable sur un dessus donné, sans pénalité en cours (mêmes règles que <see cref="Round.CanStartWith"/>).</summary>
        static bool Playable(RuleSet rules, Card c, Card top, Suit? requested)
        {
            if (c.Rank == Rank.Two) return true;
            if (c.Rank == Rank.Jack && rules.JackIsWild) return true;
            CardColor color = requested.HasValue ? Card.ColorOf(requested.Value) : top.Color;
            if (c.IsJoker) return c.Color == color;
            if (requested.HasValue) return c.Suit == requested.Value || (!top.IsJoker && c.Rank == top.Rank);
            if (top.IsJoker) return c.Color == top.Color;
            return c.Suit == top.Suit || c.Rank == top.Rank;
        }

        /// <param name="useClues">Tenir compte des indices publics sur la main du suivant (niveau Expert).</param>
        public static double Score(Round r, int seat, TurnAction a, bool useClues = false)
        {
            var hand = r.Hand(seat);
            int next = NextActive(r, seat);
            int nextCount = r.Hand(next).Count;

            if (a.Kind == ActionKind.Draw)
            {
                if (r.PendingDraw > 0) return -4 * r.PendingDraw;
                // Piocher volontairement : rarement bon, sauf pour garder une main faite de cartes précieuses.
                return -9;
            }
            if (a.Kind == ActionKind.Pass) return -6;

            var cards = a.Cards;
            int k = cards.Count;
            int left = hand.Count - k;
            if (left == 0) return FinishScore;

            Rank rank = cards[0].Rank;
            double s = 10 * k;

            bool countering = r.PendingDraw > 0;
            if (countering)
            {
                // Contrer évite de piocher tout le cumul et le renvoie au suivant.
                s += 4 * r.PendingDraw;
                if (rank == Rank.Joker && nextCount <= 2) s += 6;
            }
            else
            {
                for (int i = 0; i < k; i++) s -= KeepValue(rank);
            }

            // Attaque : le suivant est près de finir (ou a annoncé « Check »).
            if (rank == Rank.Seven || rank == Rank.Joker || rank == Rank.Ace)
            {
                double pressure = nextCount <= 1 ? 22 : nextCount == 2 ? 12 : nextCount == 3 ? 4 : 0;
                s += rank == Rank.Ace ? pressure * 0.8 * k : pressure * (rank == Rank.Seven ? k : 1);
                // Garder un 7 / joker contre un adversaire qui n'est PAS le suivant mais va bientôt finir.
                if (pressure == 0 && MinOpponentCount(r, seat) <= 1 && !countering) s -= 3;
            }

            // Ce que devient le dessus : un 2 ne le change pas.
            Card top;
            Suit? requested;
            if (rank == Rank.Two) { top = r.Top; requested = r.RequestedSuit; }
            else { top = a.TopCard; requested = a.RequestedSuit; }
            bool hasSuit = requested.HasValue || !top.IsJoker;
            Suit effSuit = requested ?? (top.IsJoker ? Suit.Spades : top.Suit);

            // Souplesse de la main restante : cartes qui resteront jouables si personne ne change le dessus.
            int requestedCount = 0;
            Card last = default;
            foreach (var c in hand)
            {
                if (Contains(cards, c)) continue;
                last = c;
                if (c.Rank == Rank.Jack || c.Rank == Rank.Two) { s += 1.5; continue; }
                if (c.IsJoker) { if (c.Color == (top.IsJoker ? top.Color : Card.ColorOf(effSuit))) s += 1; continue; }
                if (hasSuit && c.Suit == effSuit) { s += 2; requestedCount++; }
                else if (!hasSuit && c.Color == top.Color) s += 2;
                else if (!top.IsJoker && c.Rank == top.Rank && rank != Rank.Two) s += 2;
            }

            // Indices : le suivant a montré qu'il n'avait pas cette couleur ; la lui laisser le force à piocher.
            if (useClues && next != seat && rank != Rank.Seven && rank != Rank.Joker && rank != Rank.Ace)
                s += BlockBonus * BlockedShare(r, seat, next, top, requested);

            if (rank == Rank.Jack)
            {
                // Demander la couleur la plus présente dans sa main (la souplesse ci-dessus compte déjà ces cartes).
                s += requestedCount > 0 ? 1 : -4;
            }

            if (left == 1)
            {
                // Dernière carte : un Valet ou un 2 se pose presque toujours.
                if (last.Rank == Rank.Jack || last.Rank == Rank.Two) s += 6;
                else if (last.IsJoker) s -= 4;
                else if (last.Rank == Rank.Seven) s -= 1;
            }
            return s;
        }

        /// <summary>Meilleure action selon <see cref="Score"/> ; égalités départagées au hasard.</summary>
        public static TurnAction Best(Round r, int seat, List<TurnAction> actions, IRandom rng, bool useClues = false)
        {
            TurnAction best = actions[0];
            double bestScore = double.NegativeInfinity;
            int ties = 0;
            foreach (var a in actions)
            {
                double sc = Score(r, seat, a, useClues);
                if (sc > bestScore + 1e-9) { best = a; bestScore = sc; ties = 1; }
                else if (sc > bestScore - 1e-9 && rng.Next(++ties) == 0) best = a;
            }
            return best;
        }

        /// <summary>
        /// Niveau naïf : première carte jouable de la main (une seule à la fois), parfois une au hasard ;
        /// couleur du Valet au hasard. Pioche s'il ne peut rien poser.
        /// </summary>
        public static TurnAction Naive(Round r, List<TurnAction> actions, IRandom rng, double randomChance)
        {
            var singles = new List<TurnAction>();
            foreach (var a in actions)
                if (a.Kind == ActionKind.Play && a.Cards.Count == 1) singles.Add(a);
            if (singles.Count == 0)
            {
                foreach (var a in actions) if (a.Kind != ActionKind.Play) return a;
                return actions[0];
            }
            TurnAction pick = rng.Next(1000) < randomChance * 1000 ? singles[rng.Next(singles.Count)] : singles[0];
            if (pick.Cards[0].Rank == Rank.Jack)
                pick = TurnAction.Play(pick.Cards, (Suit)rng.Next(4), pick.DeclaresCheck);
            return pick;
        }
    }
}
