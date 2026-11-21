using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Check.Core;

namespace Check.AI
{
    /// <summary>Bilan de la dernière recherche (diagnostic, réglages).</summary>
    public sealed class SearchStats
    {
        public int Iterations;
        public long ElapsedMs;
        public int Candidates;
        public bool Cancelled;
        /// <summary>Candidats étudiés, valeur moyenne et nombre de simulations de chacun (diagnostic).</summary>
        public string Detail;
    }

    /// <summary>
    /// Monte-Carlo par détermination (PIMC) avec comparaison appariée : à chaque étape, les cartes que le siège
    /// ne voit pas (mains adverses, pioche) sont redistribuées au hasard, puis CHAQUE action candidate encore en
    /// lice est jouée dans ce même monde, avec la même suite de hasard, et la fin du tour est simulée par la
    /// politique heuristique pour tout le monde. Comparer les candidats sur les mêmes mondes élimine l'essentiel
    /// du bruit (le hasard de la donne pèse bien plus que le choix d'une carte) ; les candidats nettement moins
    /// bons sont abandonnés en cours de route pour concentrer le budget sur les autres.
    /// Le résultat d'une simulation mesure l'objectif réel : finir tôt, et surtout ne pas rester le dernier.
    /// </summary>
    internal static class MonteCarloSearch
    {
        public static TurnAction Search(Round root, int seat, List<TurnAction> legal, AISettings settings,
            IRandom rng, CancellationToken ct, SearchStats stats)
        {
            var watch = Stopwatch.StartNew();

            // Candidats : les meilleures actions selon les heuristiques. Pour un Valet, seule la meilleure demande
            // de couleur (la plus présente dans la main) est gardée : les simulations ne savent pas les départager.
            var scored = new List<KeyValuePair<double, TurnAction>>(legal.Count);
            foreach (var a in legal)
            {
                double sc = Heuristics.Score(root, seat, a, settings.UseHandClues);
                int same = a.RequestedSuit.HasValue ? scored.FindIndex(kv => SameCards(kv.Value, a)) : -1;
                if (same < 0) scored.Add(new KeyValuePair<double, TurnAction>(sc, a));
                else if (sc > scored[same].Key) scored[same] = new KeyValuePair<double, TurnAction>(sc, a);
            }
            int n = scored.Count;
            // Tri stable : à score égal, l'ordre de LegalActions est gardé (reproductibilité).
            var sorted = new List<KeyValuePair<double, TurnAction>>(n);
            foreach (var kv in scored)
            {
                int pos = sorted.Count;
                while (pos > 0 && sorted[pos - 1].Key < kv.Key) pos--;
                sorted.Insert(pos, kv);
            }
            int count = settings.MaxCandidates > 0 ? Math.Min(settings.MaxCandidates, n) : n;
            var cands = new TurnAction[count];
            var prior = new double[count];
            double hi = sorted[0].Key, lo = sorted[count - 1].Key;
            for (int i = 0; i < count; i++)
            {
                cands[i] = sorted[i].Value;
                prior[i] = hi > lo ? (sorted[i].Key - lo) / (hi - lo) : 1;
            }
            stats.Candidates = count;
            // Finir la main est imbattable : inutile de simuler.
            if (count == 1 || hi >= Heuristics.FinishScore) return cands[0];

            var values = new List<double>[count];
            for (int i = 0; i < count; i++) values[i] = new List<double>();
            var alive = new List<int>();
            for (int i = 0; i < count; i++) alive.Add(i);
            int rollouts = 0, worlds = 0, nextPrune = 16;
            int maxRollouts = settings.MaxIterations > 0 ? settings.MaxIterations : int.MaxValue;
            long budget = settings.TimeBudgetMs > 0 ? settings.TimeBudgetMs : long.MaxValue;

            while (alive.Count > 1 && rollouts + alive.Count <= maxRollouts)
            {
                if (ct.IsCancellationRequested) { stats.Cancelled = true; break; }
                if (watch.ElapsedMilliseconds >= budget) break;

                int worldSeed = rng.Next(int.MaxValue), playSeed = rng.Next(int.MaxValue);
                var world = root.Clone(new SystemRandom(worldSeed));
                world.RedealHiddenCards(seat, new SystemRandom(worldSeed), settings.UseHandClues);
                bool complete = true;
                foreach (int i in alive)
                {
                    // Une simulation peut être longue : on vérifie le temps entre chaque.
                    if (ct.IsCancellationRequested || watch.ElapsedMilliseconds >= budget) { complete = false; break; }
                    var playRng = new SystemRandom(playSeed);
                    var w = world.Clone(playRng);
                    w.Apply(cands[i]);
                    values[i].Add(Rollout(w, seat, settings, playRng));
                    rollouts++;
                }
                if (!complete)
                {
                    // Monde incomplet : on le retire pour garder des comparaisons appariées.
                    foreach (int i in alive) if (values[i].Count > worlds) values[i].RemoveAt(values[i].Count - 1);
                    break;
                }
                worlds++;
                if (worlds >= nextPrune)
                {
                    Prune(alive, values, worlds, settings.PruneZ);
                    nextPrune = worlds + Math.Max(4, worlds / 4);
                }
            }

            stats.Iterations = rollouts;
            stats.ElapsedMs = watch.ElapsedMilliseconds;
            var detail = new System.Text.StringBuilder();
            for (int i = 0; i < count; i++)
                detail.Append(cands[i]).Append(" = ").Append(values[i].Count > 0 ? Mean(values[i], values[i].Count).ToString("F3") : "-")
                      .Append(" (").Append(values[i].Count).Append(")").Append(alive.Contains(i) ? "" : " abandonné").Append(" ; ");
            stats.Detail = detail.ToString();

            int best = alive[0];
            double bestScore = double.NegativeInfinity;
            foreach (int i in alive)
            {
                double sc = (worlds > 0 ? Mean(values[i], worlds) : 0) + settings.HeuristicBias * prior[i];
                if (sc > bestScore) { bestScore = sc; best = i; }
            }
            return cands[best];
        }

        static bool SameCards(TurnAction x, TurnAction y)
        {
            if (x.Kind != y.Kind || x.Cards.Count != y.Cards.Count) return false;
            for (int i = 0; i < x.Cards.Count; i++) if (x.Cards[i] != y.Cards[i]) return false;
            return true;
        }

        static double Mean(List<double> v, int count)
        {
            double sum = 0;
            for (int d = 0; d < count; d++) sum += v[d];
            return sum / count;
        }

        /// <summary>Abandonne les candidats dont l'écart apparié avec le meilleur dépasse z erreurs types.</summary>
        static void Prune(List<int> alive, List<double>[] values, int worlds, double z)
        {
            int best = alive[0];
            foreach (int i in alive) if (Mean(values[i], worlds) > Mean(values[best], worlds)) best = i;
            for (int k = alive.Count - 1; k >= 0; k--)
            {
                int i = alive[k];
                if (i == best) continue;
                double sum = 0, sq = 0;
                for (int d = 0; d < worlds; d++)
                {
                    double diff = values[best][d] - values[i][d];
                    sum += diff;
                    sq += diff * diff;
                }
                double mean = sum / worlds;
                double var = Math.Max(0, sq / worlds - mean * mean);
                double se = Math.Sqrt(var / worlds) + 1e-3;
                if (mean > z * se) alive.RemoveAt(k);
            }
        }

        /// <summary>Joue la fin du tour (politique heuristique bruitée) et renvoie la valeur pour <paramref name="seat"/>.</summary>
        static double Rollout(Round w, int seat, AISettings settings, IRandom rng)
        {
            int startTurn = w.TurnNumber;
            int randomPermil = (int)(settings.RolloutRandomness * 1000);
            int guard = 0;
            var r = w.Rules;
            bool fast = r.AllowMultiples && r.MayPlayDrawnCard && r.MayDrawVoluntarily;
            var buffer = new List<TurnAction>(16);
            while (!w.IsOver && !w.IsFinished(seat) && w.TurnNumber - startTurn < settings.MaxRolloutTurns && guard++ < 4 * settings.MaxRolloutTurns)
            {
                List<TurnAction> actions;
                if (fast) { RolloutActions(w, buffer); actions = buffer; }
                else actions = w.LegalActions();
                TurnAction a;
                if (actions.Count == 1) a = actions[0];
                else if (randomPermil > 0 && rng.Next(1000) < randomPermil) a = actions[rng.Next(actions.Count)];
                else a = Heuristics.Best(w, w.Current, actions, rng, settings.UseHandClues);
                w.Apply(a);
            }
            return Outcome(w, seat);
        }

        /// <summary>
        /// Générateur réduit pour les simulations (beaucoup moins coûteux que <see cref="Round.LegalActions"/>) :
        /// chaque carte jouable seule, chaque pose de toutes les cartes d'une même valeur (une par carte du dessus
        /// possible), les quatre demandes du Valet, et la pioche ou le passe. Toutes ces actions sont légales ;
        /// les poses multiples partielles (deux 9 sur trois) sont laissées de côté.
        /// </summary>
        static void RolloutActions(Round w, List<TurnAction> list)
        {
            list.Clear();
            var hand = w.Hand(w.Current);
            int n = hand.Count;
            if (w.HasDrawn)
            {
                if (w.DrawnCard.HasValue && w.CanStartWith(w.DrawnCard.Value))
                    AddPlay(list, new[] { w.DrawnCard.Value }, n);
                list.Add(TurnAction.Pass);
                return;
            }
            for (int i = 0; i < n; i++)
            {
                var c = hand[i];
                if (!w.CanStartWith(c)) continue;
                AddPlay(list, new[] { c }, n);
                if (c.IsJoker) continue;
                int same = 0;
                for (int j = 0; j < n; j++) if (hand[j].Rank == c.Rank) same++;
                if (same < 2) continue;
                var group = new Card[same];
                for (int top = 0; top < n; top++)
                {
                    if (top == i || hand[top].Rank != c.Rank) continue;
                    if (c.Rank == Rank.Two && top != LastOfRank(hand, c.Rank, i)) continue; // sous le tas : l'ordre est sans effet
                    int k = 0;
                    group[k++] = c;
                    for (int j = 0; j < n; j++) if (j != i && j != top && hand[j].Rank == c.Rank) group[k++] = hand[j];
                    group[k] = hand[top];
                    AddPlay(list, (Card[])group.Clone(), n);
                }
            }
            list.Add(TurnAction.Draw);
        }

        static int LastOfRank(IReadOnlyList<Card> hand, Rank rank, int except)
        {
            for (int j = hand.Count - 1; j >= 0; j--) if (j != except && hand[j].Rank == rank) return j;
            return -1;
        }

        static void AddPlay(List<TurnAction> list, Card[] cards, int handCount)
        {
            bool check = handCount - cards.Length == 1;
            if (cards[0].Rank == Rank.Jack)
                for (int s = 0; s < 4; s++) list.Add(TurnAction.Play(cards, (Suit)s, check));
            else
                list.Add(TurnAction.Play(cards, null, check));
        }

        /// <summary>
        /// Valeur d'une fin de simulation pour <paramref name="seat"/>, entre 0 et 1 : 1 pour le premier à finir,
        /// 0,65 pour le dernier qualifié, 0 pour l'éliminé. Sans fin (garde-fou), estimation selon les mains.
        /// </summary>
        public static double Outcome(Round w, int seat)
        {
            int n = w.SeatCount;
            var order = w.FinishOrder;
            for (int i = 0; i < order.Count; i++)
                if (order[i] == seat) return n <= 2 ? 1 : 1 - 0.35 * i / (n - 2);
            if (w.IsOver) return 0;

            int mine = w.Hand(seat).Count, more = 0, equal = 0, opp = 0;
            for (int s = 0; s < n; s++)
            {
                if (s == seat || w.IsFinished(s)) continue;
                opp++;
                int c = w.Hand(s).Count;
                if (c > mine) more++;
                else if (c == mine) equal++;
            }
            return opp == 0 ? 0 : 0.1 + 0.5 * (more + 0.5 * equal) / opp;
        }
    }
}
