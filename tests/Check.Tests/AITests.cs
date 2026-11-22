using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Check.AI;
using Check.Core;
using NUnit.Framework;

namespace Check.Tests
{
    public class AITests
    {
        // ---------- Utilitaires ----------

        static Card C(string s) => Card.Parse(s);
        static Card[] Cs(string s) => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Card.Parse).ToArray();

        /// <summary>
        /// Position à 2 ou 3 joueurs. Sans pioche précisée, toutes les autres cartes du paquet forment la pioche
        /// (ordre fixe), comme dans une vraie partie.
        /// </summary>
        static Round Pos(string hand0, string hand1, string hand2, string discard, string drawPile = null,
            int current = 0, Suit? requested = null, int pending = 0, PenaltyKind kind = PenaltyKind.None)
        {
            var hands = new List<Card[]> { Cs(hand0), Cs(hand1) };
            if (hand2 != null) hands.Add(Cs(hand2));
            var used = new HashSet<Card>(hands.SelectMany(h => h).Concat(Cs(discard)));
            var pile = drawPile != null ? Cs(drawPile) : Card.FullDeck().Where(c => !used.Contains(c)).ToArray();
            return Round.FromState(RuleSet.Default, hands, Cs(discard), pile, current, null, requested, pending, kind);
        }

        /// <summary>Réglages rapides pour les tests : simulations comptées, pas de limite de temps (reproductible).</summary>
        static AISettings Fast(AILevel level, int iterations)
        {
            var s = AISettings.For(level);
            if (s.UseSearch) { s.TimeBudgetMs = 0; s.MaxIterations = iterations; }
            return s;
        }

        static readonly AILevel[] AllLevels = { AILevel.Facile, AILevel.Moyen, AILevel.Difficile, AILevel.Expert };

        // ---------- Légalité ----------

        /// <summary>Parties complètes jouées par des IA de tous niveaux, de 2 à 10 joueurs : toujours des actions légales.</summary>
        [Test]
        public void Fuzz_AlwaysLegal([Values(2, 3, 4, 6, 8, 10)] int players, [Values(false, true)] bool mayPlayDrawn)
        {
            var rules = RuleSet.Default;
            rules.MayPlayDrawnCard = mayPlayDrawn;
            int decisions = 0;
            for (int game = 0; game < 3; game++)
            {
                var match = new Match(rules, players, new SystemRandom(1000 + game * 17 + players));
                var ais = Enumerable.Range(0, players)
                    .Select(p => new CheckAI(Fast(AllLevels[(p + game) % 4], 25), new SystemRandom(p * 7 + game)))
                    .ToArray();
                while (!match.IsOver)
                {
                    match.StartRound();
                    int guard = 0;
                    while (!match.Round.IsOver)
                    {
                        var r = match.Round;
                        int seat = r.Current;
                        var a = ais[r.PlayerId(seat)].ChooseAction(r, seat, CancellationToken.None);
                        Assert.IsTrue(r.IsLegal(a, out var why), $"{a} : {why}");
                        if (!mayPlayDrawn) Assert.AreNotEqual(ActionKind.Pass, a.Kind, "piocher termine le tour : rien à passer");
                        match.Apply(a);
                        decisions++;
                        Assert.Less(++guard, 5000, "tour sans fin");
                    }
                }
            }
            // Simple garde-fou (des parties ont bien été jouées) : trois parties à deux font une trentaine de décisions.
            Assert.Greater(decisions, 30);
        }

        /// <summary>
        /// Positions aléatoires en cours de partie (sous pénalité, après un Valet ; une sur deux avec la variante
        /// « pioche, puis pose ou passe », pour couvrir aussi les positions après une pioche).
        /// </summary>
        [Test]
        public void Fuzz_RandomPositionsAllLevels()
        {
            var rng = new SystemRandom(99);
            int checkedPositions = 0;
            for (int i = 0; i < 300; i++)
            {
                int players = 2 + rng.Next(9);
                var rules = RuleSet.Default;
                rules.MayPlayDrawnCard = (i & 1) == 1;
                var r = new Round(rules, Enumerable.Range(0, players).ToList(), rng.Next(players), new SystemRandom(i));
                int moves = rng.Next(40);
                for (int m = 0; m < moves && !r.IsOver; m++)
                {
                    var acts = r.LegalActions();
                    r.Apply(acts[rng.Next(acts.Count)]);
                }
                if (r.IsOver) continue;
                var level = AllLevels[i % 4];
                var ai = new CheckAI(Fast(level, 30), new SystemRandom(i));
                var a = ai.ChooseAction(r, r.Current, CancellationToken.None);
                Assert.IsTrue(r.IsLegal(a, out var why), $"{level} {a} : {why}");
                checkedPositions++;
            }
            Assert.Greater(checkedPositions, 200);
        }

        [Test]
        public void ChooseAction_DoesNotModifyTheRound()
        {
            var r = new Round(RuleSet.Default, new[] { 0, 1, 2, 3 }, 0, new SystemRandom(4));
            var before = Enumerable.Range(0, 4).Select(s => string.Join(" ", r.Hand(s))).ToList();
            int pile = r.DrawPileCount;
            new CheckAI(Fast(AILevel.Expert, 200), new SystemRandom(1)).ChooseAction(r, 0, CancellationToken.None);
            CollectionAssert.AreEqual(before, Enumerable.Range(0, 4).Select(s => string.Join(" ", r.Hand(s))).ToList());
            Assert.AreEqual(pile, r.DrawPileCount);
            Assert.AreEqual(0, r.Current);
        }

        [Test]
        public void ChooseAction_RejectsWrongSeat()
        {
            var r = new Round(RuleSet.Default, new[] { 0, 1, 2 }, 0, new SystemRandom(4));
            Assert.Throws<InvalidOperationException>(() => new CheckAI(AILevel.Moyen).ChooseAction(r, 1, CancellationToken.None));
        }

        // ---------- Temps ----------

        [TestCase(AILevel.Difficile), TestCase(AILevel.Expert)]
        public void Search_RespectsTimeBudget(AILevel level)
        {
            var settings = AISettings.For(level);
            settings.MaxIterations = 0; // seul le temps arrête la recherche
            var ai = new CheckAI(settings, new SystemRandom(3));
            long worst = 0;
            for (int i = 0; i < 6; i++)
            {
                var r = new Round(RuleSet.Default, Enumerable.Range(0, 2 + i).ToList(), 0, new SystemRandom(i));
                var watch = Stopwatch.StartNew();
                var a = ai.ChooseAction(r, 0, CancellationToken.None);
                worst = Math.Max(worst, watch.ElapsedMilliseconds);
                Assert.IsTrue(r.IsLegal(a));
                if (ai.LastStats != null)
                    TestContext.WriteLine($"{2 + i} joueurs : {ai.LastStats.Iterations} simulations, {ai.LastStats.Candidates} candidats, {ai.LastStats.ElapsedMs} ms");
            }
            TestContext.WriteLine($"{level} : pire décision {worst} ms (budget {settings.TimeBudgetMs} ms)");
            // Marge large : sur une machine chargée (CI à 2 cœurs, autres builds), le fil peut être suspendu
            // quelques centaines de ms (453 ms vus pour 350 ms de budget). Une recherche qui ignorerait le temps
            // ne s'arrêterait jamais (MaxIterations = 0) : la marge garde donc tout son sens.
            Assert.LessOrEqual(worst, settings.TimeBudgetMs + Math.Max(60, settings.TimeBudgetMs / 2));
        }

        [Test]
        public void Search_StopsWhenCancelled()
        {
            var settings = AISettings.For(AILevel.Expert);
            settings.MaxIterations = 0;
            settings.TimeBudgetMs = 10000;
            var ai = new CheckAI(settings, new SystemRandom(3));
            var r = new Round(RuleSet.Default, new[] { 0, 1, 2, 3 }, 0, new SystemRandom(8));
            using (var cts = new CancellationTokenSource(50))
            {
                var watch = Stopwatch.StartNew();
                var a = ai.ChooseAction(r, 0, cts.Token);
                Assert.Less(watch.ElapsedMilliseconds, 500);
                Assert.IsTrue(r.IsLegal(a));
            }
        }

        [Test]
        public void ChooseActionAsync_ReturnsLegalAction()
        {
            var r = new Round(RuleSet.Default, new[] { 0, 1, 2 }, 0, new SystemRandom(8));
            var a = new CheckAI(Fast(AILevel.Difficile, 100), new SystemRandom(2)).ChooseActionAsync(r, 0).Result;
            Assert.IsTrue(r.IsLegal(a));
        }

        // ---------- Information cachée ----------

        /// <summary>
        /// Deux positions identiques pour le siège 0, mais avec des mains adverses et une pioche différentes :
        /// à graine égale, chaque niveau doit faire exactement le même choix (il ne lit pas les cartes cachées).
        /// </summary>
        [Test]
        public void HiddenCards_DoNotInfluenceTheChoice([Values] AILevel level)
        {
            var a = Pos("9H 9S KD 7C JS", "6C 6D 4S", "8C 8D 3H", "5H", "10C 10D 10H 10S QS QH");
            var b = Pos("9H 9S KD 7C JS", "QS 10H 8C", "10S 6D QH", "5H", "3H 4S 10C 8D 6C 10D");
            for (int seed = 0; seed < 8; seed++)
            {
                var ca = new CheckAI(Fast(level, 120), new SystemRandom(seed)).ChooseAction(a, 0, CancellationToken.None);
                var cb = new CheckAI(Fast(level, 120), new SystemRandom(seed)).ChooseAction(b, 0, CancellationToken.None);
                Assert.AreEqual(ca, cb, $"graine {seed}");
            }
        }

        // ---------- Tactique ----------

        [TestCase(AILevel.Moyen), TestCase(AILevel.Difficile), TestCase(AILevel.Expert)]
        public void CountersSevenWhenPossible(AILevel level)
        {
            // Un 7 attend le siège 0, qui a un 7 : contrer plutôt que piocher 2.
            var r = Pos("7S KD 4C 9D", "6C 6D 4S", "8C 8D 3H", "5H 7H", pending: 2, kind: PenaltyKind.Seven);
            for (int seed = 0; seed < 5; seed++)
            {
                var a = new CheckAI(Fast(level, 300), new SystemRandom(seed)).ChooseAction(r, 0, CancellationToken.None);
                Assert.AreEqual(ActionKind.Play, a.Kind, a.ToString());
                Assert.AreEqual(C("7S"), a.Cards[0]);
            }
        }

        [TestCase(AILevel.Facile), TestCase(AILevel.Moyen), TestCase(AILevel.Difficile), TestCase(AILevel.Expert)]
        public void JackRequestsASuitItHolds(AILevel level)
        {
            // Seul le Valet est jouable ; la main restante est surtout en ♦.
            var r = Pos("JS 4D 9D KD 3C", "6C 6S 4S", "8C 8S 3S", "5H");
            int jacks = 0;
            for (int seed = 0; seed < 6; seed++)
            {
                var a = new CheckAI(Fast(level, 400), new SystemRandom(seed)).ChooseAction(r, 0, CancellationToken.None);
                if (a.Kind != ActionKind.Play) continue; // piocher pour garder son Valet reste permis
                jacks++;
                Assert.AreEqual(Rank.Jack, a.TopCard.Rank, a.ToString());
                if (level == AILevel.Facile) continue; // le niveau naïf demande au hasard
                Assert.AreEqual(Suit.Diamonds, a.RequestedSuit, a.ToString());
            }
            if (level == AILevel.Moyen) Assert.AreEqual(6, jacks);
        }

        [TestCase(AILevel.Difficile), TestCase(AILevel.Expert)]
        public void SearchRequestsASuitItHoldsWhenFinishingIsNear(AILevel level)
        {
            // Deux joueurs, l'adversaire n'a plus que 2 cartes : jouer le Valet en demandant ♦ (deux cartes en main).
            var r = Pos("JS 4D 9D", "6C 6S", null, "5H");
            for (int seed = 0; seed < 4; seed++)
            {
                var a = new CheckAI(Fast(level, 600), new SystemRandom(seed)).ChooseAction(r, 0, CancellationToken.None);
                Assert.AreEqual(ActionKind.Play, a.Kind, a.ToString());
                Assert.AreEqual(Suit.Diamonds, a.RequestedSuit, a.ToString());
            }
        }

        [Test]
        public void MediumAttacksPlayerWhoDeclaredCheck()
        {
            // Le suivant n'a plus qu'une carte : poser le 7 (même couleur que le dessus) plutôt qu'un 9.
            // (Les niveaux qui simulent peuvent préférer garder le 7 : à trois, laisser finir le suivant ne coûte rien.)
            var r = Pos("7H 9H KC QC 4C", "6C", "8C 8D 3H 3S", "5H");
            var a = new CheckAI(AILevel.Moyen, new SystemRandom(1)).ChooseAction(r, 0, CancellationToken.None);
            Assert.AreEqual(Rank.Seven, a.TopCard.Rank, a.ToString());
        }

        [TestCase(AILevel.Moyen), TestCase(AILevel.Difficile), TestCase(AILevel.Expert)]
        public void AttacksHeadsUpOpponentOnLastCard(AILevel level)
        {
            // À deux, l'adversaire qui finit gagne : le 7 s'impose plutôt que le 9.
            var r = Pos("7H 9H KC QC 4C", "6C", null, "5H");
            for (int seed = 0; seed < 4; seed++)
            {
                var a = new CheckAI(Fast(level, 600), new SystemRandom(seed)).ChooseAction(r, 0, CancellationToken.None);
                Assert.AreEqual(ActionKind.Play, a.Kind, a.ToString());
                Assert.AreEqual(Rank.Seven, a.TopCard.Rank, a.ToString());
            }
        }

        [TestCase(AILevel.Moyen), TestCase(AILevel.Difficile), TestCase(AILevel.Expert)]
        public void FinishesWhenPossible(AILevel level)
        {
            var r = Pos("9H 9S", "6C 6D 4S", "8C 8D 3H", "5H");
            var a = new CheckAI(Fast(level, 100), new SystemRandom(1)).ChooseAction(r, 0, CancellationToken.None);
            Assert.AreEqual(2, a.Cards.Count, a.ToString());
        }

        [Test]
        public void MediumSavesJackWhenANormalCardFits()
        {
            var r = Pos("JS 4H KD QC 3C", "6C 6S 4S", "8C 8S 3S", "5H");
            var a = new CheckAI(AILevel.Moyen, new SystemRandom(1)).ChooseAction(r, 0, CancellationToken.None);
            Assert.AreEqual(C("4H"), a.TopCard, a.ToString());
        }

        [Test]
        public void ForgottenCheck_RateDependsOnLevel()
        {
            // Pose de l'avant-dernière carte : 9H, il restera KD.
            var r = Pos("9H KD", "6C 6D 4S", "8C 8D 3H", "5H");
            int Forgotten(AILevel level)
            {
                var ai = new CheckAI(level, new SystemRandom(5));
                int n = 0;
                for (int i = 0; i < 400; i++)
                {
                    var a = ai.ChooseAction(r, 0, CancellationToken.None);
                    if (a.Kind == ActionKind.Play && a.Cards.Count == 1 && !a.DeclaresCheck) n++;
                }
                return n;
            }
            int easy = Forgotten(AILevel.Facile), medium = Forgotten(AILevel.Moyen);
            TestContext.WriteLine($"Check oublié sur 400 : Facile {easy}, Moyen {medium}");
            Assert.That(easy, Is.InRange(60, 140));
            Assert.That(medium, Is.InRange(5, 40));
            var hard = new CheckAI(Fast(AILevel.Difficile, 300), new SystemRandom(5));
            for (int i = 0; i < 20; i++) Assert.IsTrue(hard.ChooseAction(r, 0, CancellationToken.None).DeclaresCheck);
        }

        // ---------- Tournois ----------

        /// <summary>
        /// Tours à 4 joueurs (un éliminé par tour). Avec <paramref name="soloA"/>, un seul siège A face à trois B
        /// (siège tournant) ; sinon A et B en alternance (A B A B). Donneur tournant, graines fixes : reproductible.
        /// Renvoie le taux d'élimination et le rang moyen (0 = premier à finir, 3 = éliminé) de chaque niveau.
        /// </summary>
        static (double elimA, double elimB, double rankA, double rankB) Tournament(AISettings a, AISettings b, int rounds,
            int seed, bool soloA = false)
        {
            var elim = new int[rounds, 2];
            var rank = new int[rounds, 2];
            Parallel.For(0, rounds, i =>
            {
                bool aFirst = (i & 1) == 0;
                var levels = soloA
                    ? Enumerable.Range(0, 4).Select(s => s == i % 4 ? a : b).ToArray()
                    : new[] { aFirst ? a : b, aFirst ? b : a, aFirst ? a : b, aFirst ? b : a };
                var ais = levels.Select((s, p) => new CheckAI(s, new SystemRandom(seed + i * 13 + p))).ToArray();
                var r = new Round(RuleSet.Default, new[] { 0, 1, 2, 3 }, (i / 2) % 4, new SystemRandom(seed * 7 + i));
                int guard = 0;
                while (!r.IsOver && guard++ < 3000)
                {
                    int seat = r.Current;
                    r.Apply(ais[seat].ChooseAction(r, seat, CancellationToken.None));
                }
                for (int s = 0; s < 4; s++)
                {
                    int team = (levels[s] == a) ? 0 : 1;
                    int pos = r.FinishOrder.ToList().IndexOf(s);
                    if (pos < 0) pos = 3;
                    rank[i, team] += pos;
                    if (r.IsOver && r.LoserSeat == s) elim[i, team]++;
                }
            });
            double ea = 0, eb = 0, ra = 0, rb = 0;
            for (int i = 0; i < rounds; i++) { ea += elim[i, 0]; eb += elim[i, 1]; ra += rank[i, 0]; rb += rank[i, 1]; }
            int seatsA = soloA ? rounds : rounds * 2, seatsB = rounds * 4 - seatsA;
            return (ea / seatsA, eb / seatsB, ra / seatsA, rb / seatsB);
        }

        [Test]
        public void Tournament_MediumBeatsEasy()
        {
            var t = Tournament(AISettings.For(AILevel.Moyen), AISettings.For(AILevel.Facile), 600, 11);
            TestContext.WriteLine($"Moyen : élimination {t.elimA:P1}, rang {t.rankA:F2} | Facile : élimination {t.elimB:P1}, rang {t.rankB:F2}");
            Assert.Less(t.elimA, t.elimB * 0.75);
            Assert.Less(t.rankA, t.rankB - 0.15);
        }

        [Test]
        public void Tournament_HardBeatsMedium()
        {
            // Un Difficile (400 simulations par décision, sans limite de temps : reproductible) contre trois Moyen.
            var t = Tournament(Fast(AILevel.Difficile, 400), AISettings.For(AILevel.Moyen), 240, 23, soloA: true);
            TestContext.WriteLine($"Difficile : élimination {t.elimA:P1}, rang {t.rankA:F2} | Moyen : élimination {t.elimB:P1}, rang {t.rankB:F2}");
            Assert.Less(t.elimA, t.elimB * 0.8);
            Assert.Less(t.rankA, t.rankB);
        }
    }
}
