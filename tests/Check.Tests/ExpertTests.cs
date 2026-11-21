using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Check.AI;
using Check.Core;
using NUnit.Framework;

namespace Check.Tests
{
    /// <summary>
    /// Indices publics sur les mains (un joueur qui pioche faute de jouer n'avait ni la couleur ni la valeur du
    /// dessus) et leur usage par le niveau Expert.
    /// </summary>
    public class ExpertTests
    {
        static Card[] Cs(string s) => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Card.Parse).ToArray();

        static ulong Bit(string card) => 1UL << Card.Parse(card).Id;

        /// <summary>Position à 2 joueurs : mains, tas (du dessous au dessus), pioche (tirée par la fin).</summary>
        static Round Pos(string hand0, string hand1, string discard, string drawPile, int current = 0, RuleSet rules = null)
        {
            var hands = new List<Card[]> { Cs(hand0), Cs(hand1) };
            return Round.FromState(rules ?? RuleSet.Default, hands, Cs(discard), Cs(drawPile), current);
        }

        /// <summary>Variante « pioche, puis pose ou passe » (avant la règle de Kevin du 21/11).</summary>
        static RuleSet PlayDrawnVariant()
        {
            var rules = RuleSet.Default;
            rules.MayPlayDrawnCard = true;
            return rules;
        }

        // ---------- Indices dans le moteur ----------

        [Test]
        public void Clue_DrawWithoutPlayableCard_RecordsWhatWasMissing()
        {
            // Le siège 1 n'a ni cœur, ni 9, ni Valet, ni 2, ni joker rouge : il pioche, ce qui termine son tour. La carte
            // piochée (6D) reste cachée et personne ne sait si elle se posait : l'indice couvre les 3 cartes d'avant.
            var r = Pos("KH KS 8D", "3C 4C 5S", "4H 9H", "10C 6D", current: 1);
            Assert.AreEqual(0, r.MissingCardsHeld(1));
            r.Apply(TurnAction.Draw);
            Assert.AreEqual(0, r.Current);
            Assert.AreEqual(4, r.Hand(1).Count);
            Assert.AreEqual(3, r.MissingCardsHeld(1));
            ulong m = r.MissingCards(1);
            foreach (var c in new[] { "AH", "KH", "9S", "9C", "JS", "2D", "JkR" }) Assert.AreNotEqual(0UL, m & Bit(c), c);
            foreach (var c in new[] { "KS", "8D", "JkN", "3C" }) Assert.AreEqual(0UL, m & Bit(c), c);
            Assert.IsFalse(r.IsKnownToLack(1, Suit.Hearts), "la carte piochée est peut-être un cœur");
            Assert.AreEqual(0, r.MissingCardsHeld(0), "le siège 0 n'a rien montré");
        }

        /// <summary>
        /// Avec la règle de Kevin, l'indice ne dépend jamais de la carte piochée (cachée) : qu'elle se pose ou non,
        /// les autres voient la même chose. Sinon l'IA lirait une information secrète.
        /// </summary>
        [Test]
        public void Clue_DoesNotLeakWhetherTheDrawnCardWasPlayable()
        {
            var playable = Pos("KH KS 8D", "3C 4C 5S", "4H 9H", "10C 5H", current: 1);
            var unplayable = Pos("KH KS 8D", "3C 4C 5S", "4H 9H", "10C 6D", current: 1);
            playable.Apply(TurnAction.Draw);
            unplayable.Apply(TurnAction.Draw);
            Assert.AreEqual(playable.MissingCards(1), unplayable.MissingCards(1));
            Assert.AreEqual(playable.MissingCardsHeld(1), unplayable.MissingCardsHeld(1));
        }

        [Test]
        public void Clue_PlayingAnExcludedCardRevealsTheDrawnCard()
        {
            // Après la pioche, une carte exclue posée ne peut être que la carte piochée : toute la main redevient indicée.
            var r = Pos("JS QD 8D", "3C 4C 5S", "4H 9H", "10C 6D QH", current: 1);
            r.Apply(TurnAction.Draw);                              // pioche QH, tour fini
            Assert.IsFalse(r.IsKnownToLack(1, Suit.Hearts));
            r.Apply(TurnAction.Play(Cs("JS"), Suit.Hearts));
            r.Apply(TurnAction.Play(Cs("QH")));                    // cœur exclu : c'était la carte piochée
            Assert.AreEqual(3, r.MissingCardsHeld(1));
            Assert.AreEqual(3, r.Hand(1).Count);
            Assert.IsTrue(r.IsKnownToLack(1, Suit.Hearts));
        }

        [Test]
        public void Clue_Variant_UnplayableDrawnCardIsCovered()
        {
            // Variante : la passe automatique montre que la carte piochée ne se posait pas non plus.
            var r = Pos("KH KS 8D", "3C 4C 5S", "4H 9H", "10C 6D", current: 1, rules: PlayDrawnVariant());
            r.Apply(TurnAction.Draw);
            Assert.AreEqual(4, r.MissingCardsHeld(1));
            Assert.IsTrue(r.IsKnownToLack(1, Suit.Hearts));
            Assert.IsFalse(r.IsKnownToLack(1, Suit.Spades));
        }

        [Test]
        public void Clue_Variant_PlayableDrawnCard_IsNotCovered()
        {
            // La carte piochée (5H) se pose : le joueur passe de lui-même, elle reste libre.
            var r = Pos("KH KS 8D", "3C 4C 5S", "4H 9H", "10C 5H", current: 1, rules: PlayDrawnVariant());
            r.Apply(TurnAction.Draw);
            Assert.IsTrue(r.HasDrawn);
            r.Apply(TurnAction.Pass);
            Assert.AreEqual(3, r.MissingCardsHeld(1));
            Assert.AreEqual(4, r.Hand(1).Count);
            Assert.IsFalse(r.IsKnownToLack(1, Suit.Hearts), "il a peut-être un cœur : la carte piochée");
        }

        [Test]
        public void Clue_PenaltyCardsAreFreeAndPlaysShrinkTheClue()
        {
            var r = Pos("KH 7H 5H", "3C 4C 5S", "4H 9H", "10C 10D QS QD 6D", current: 1);
            r.Apply(TurnAction.Draw);                              // pioche 6D, tour fini
            Assert.AreEqual(3, r.MissingCardsHeld(1));
            r.Apply(TurnAction.Play(Cs("7H")));                    // le siège 0 lui envoie +2
            r.Apply(TurnAction.Draw);                              // pénalité : deux cartes libres (QD, QS)
            Assert.AreEqual(6, r.Hand(1).Count);
            Assert.AreEqual(3, r.MissingCardsHeld(1));
            Assert.IsFalse(r.IsKnownToLack(1, Suit.Hearts));
            r.Apply(TurnAction.Play(Cs("5H")));
            r.Apply(TurnAction.Play(Cs("5S")));                    // carte permise : on ne sait pas d'où elle vient
            Assert.AreEqual(2, r.MissingCardsHeld(1), "l'indice couvre une carte de moins");
        }

        [Test]
        public void Clue_PlayingAnExcludedCard_ClearsAWrongClue()
        {
            // Le siège 1 a des cœurs mais pioche exprès (permis) : l'indice est faux. Le premier cœur posé passe pour la
            // carte piochée ; le second ne peut plus l'être, l'indice s'efface.
            var r = Pos("JS JC KD 8D", "3H 4H 5S", "4D 9H", "10C 6D", current: 1);
            r.Apply(TurnAction.Draw);                              // pioche 6D, tour fini
            Assert.AreEqual(3, r.MissingCardsHeld(1));
            r.Apply(TurnAction.Play(Cs("JS"), Suit.Hearts));
            r.Apply(TurnAction.Play(Cs("3H")));
            Assert.IsTrue(r.IsKnownToLack(1, Suit.Hearts), "indice faux mais cohérent avec ce qu'on a vu");
            r.Apply(TurnAction.Play(Cs("JC"), Suit.Hearts));
            r.Apply(TurnAction.Play(Cs("4H")));                    // carte exclue alors que toute la main était indicée
            Assert.AreEqual(0, r.MissingCardsHeld(1));
            Assert.AreEqual(0UL, r.MissingCards(1));
        }

        [Test]
        public void Clue_IsCopiedByClone()
        {
            var r = Pos("KH KS 8D", "3C 4C 5S", "4H 9H", "10C 6D", current: 1);
            r.Apply(TurnAction.Draw);
            var c = r.Clone();
            Assert.AreEqual(r.MissingCards(1), c.MissingCards(1));
            Assert.AreEqual(r.MissingCardsHeld(1), c.MissingCardsHeld(1));
        }

        [Test]
        public void RedealWithClues_NeverGivesExcludedCardsAndKeepsTheDeck()
        {
            // À 3 : le siège 1 a montré qu'il n'a pas de cœur ; le siège 0 (l'IA) tire des mondes.
            var hands = new List<Card[]> { Cs("KS 8D"), Cs("3C 4C 5S"), Cs("6H 7H 8H QC") };
            var used = new HashSet<Card>(hands.SelectMany(h => h).Concat(Cs("4H 9H")));
            var pile = Card.FullDeck().Where(c => !used.Contains(c) && c != Card.Parse("6D")).Append(Card.Parse("6D")).ToArray();
            var r = Round.FromState(RuleSet.Default, hands, Cs("4H 9H"), pile, 1);
            r.Apply(TurnAction.Draw);
            Assert.AreEqual(3, r.MissingCardsHeld(1), "la carte piochée (cachée) est libre");
            int held = r.MissingCardsHeld(1);
            ulong mask = r.MissingCards(1);
            int violationsWithout = 0;
            for (int seed = 0; seed < 300; seed++)
            {
                var w = r.Clone(new SystemRandom(seed));
                w.RedealHiddenCards(2, new SystemRandom(seed), useClues: true);
                Assert.IsTrue(w.Hand(1).Take(held).All(c => (mask & (1UL << c.Id)) == 0), $"graine {seed}");
                CollectionAssert.AreEqual(r.Hand(2), w.Hand(2), "la main du joueur qui regarde ne bouge pas");
                Assert.AreEqual(r.Hand(1).Count, w.Hand(1).Count);
                Assert.AreEqual(54, w.Hand(0).Concat(w.Hand(1)).Concat(w.Hand(2)).Concat(w.Discard).Distinct().Count() + w.DrawPileCount);

                var u = r.Clone(new SystemRandom(seed));
                u.RedealHiddenCards(2, new SystemRandom(seed));
                if (u.Hand(1).Take(held).Any(c => (mask & (1UL << c.Id)) != 0)) violationsWithout++;
            }
            Assert.Greater(violationsWithout, 100, "sans indices, la main tirée ignore ce qu'on sait");
        }

        // ---------- Usage par l'IA ----------

        /// <summary>Le suivant a montré qu'il n'a pas de cœur : l'Expert garde le cœur au dessus, le Difficile n'en sait rien.</summary>
        [Test]
        public void Expert_KeepsTheSuitTheNextPlayerLacks()
        {
            // Le siège 1 pioche sur 9♥ (tour fini), puis pose la carte piochée, un cœur exclu : on sait qu'il n'en a plus.
            var r = Pos("JS QH KS 8D 10D", "3C 4C 5S", "4H 9H", "8C KH", current: 1);
            r.Apply(TurnAction.Draw);
            r.Apply(TurnAction.Play(Cs("JS"), Suit.Hearts));
            r.Apply(TurnAction.Play(Cs("KH")));
            Assert.AreEqual(0, r.Current);
            Assert.IsTrue(r.IsKnownToLack(1, Suit.Hearts));
            var qh = TurnAction.Play(Cs("QH"));
            var ks = TurnAction.Play(Cs("KS"));
            Assert.AreEqual(Heuristics.Score(r, 0, qh), Heuristics.Score(r, 0, ks), "sans indices, rien ne les distingue");
            Assert.Greater(Heuristics.Score(r, 0, qh, useClues: true), Heuristics.Score(r, 0, ks, useClues: true));
            double blockQh = Heuristics.BlockedShare(r, 0, 1, Card.Parse("QH"), null);
            double blockKs = Heuristics.BlockedShare(r, 0, 1, Card.Parse("KS"), null);
            Assert.Greater(blockQh, 0.5);
            Assert.Less(blockKs, blockQh / 2);

            var expert = AISettings.For(AILevel.Expert);
            expert.TimeBudgetMs = 0;
            expert.MaxIterations = 600;
            for (int seed = 0; seed < 4; seed++)
            {
                var choice = new CheckAI(expert, new SystemRandom(seed)).ChooseAction(r, 0, CancellationToken.None);
                // La recherche peut juger la pioche aussi bonne ; jamais elle ne rend la main au pique.
                Assert.AreNotEqual(ks, choice, $"graine {seed}");
            }
        }

        [Test]
        public void OnlyExpertUsesClues()
        {
            Assert.IsTrue(AISettings.For(AILevel.Expert).UseHandClues);
            Assert.IsFalse(AISettings.For(AILevel.Difficile).UseHandClues);
            Assert.IsFalse(AISettings.For(AILevel.Moyen).UseHandClues);
            Assert.IsFalse(AISettings.For(AILevel.Facile).UseHandClues);
        }

        // ---------- Mesure : Expert contre Difficile ----------

        /// <summary>
        /// Parties à deux, chaque donne jouée deux fois en échangeant les places (le hasard de la donne s'annule en
        /// grande partie). Simulations comptées, sans limite de temps : reproductible. Renvoie le taux de victoire de A.
        /// </summary>
        internal static double HeadsUp(AISettings a, AISettings b, int deals, int seed)
        {
            var win = new int[deals * 2];
            Parallel.For(0, deals * 2, k =>
            {
                int i = k / 2;
                bool aFirstSeat = (k & 1) == 0;
                var ais = new[]
                {
                    new CheckAI(aFirstSeat ? a : b, new SystemRandom(seed + i * 13)),
                    new CheckAI(aFirstSeat ? b : a, new SystemRandom(seed + i * 13 + 1)),
                };
                var r = new Round(RuleSet.Default, new[] { 0, 1 }, (i / 2) & 1, new SystemRandom(seed * 7 + i));
                int guard = 0;
                while (!r.IsOver && guard++ < 3000) r.Apply(ais[r.Current].ChooseAction(r, r.Current, CancellationToken.None));
                int aSeat = aFirstSeat ? 0 : 1;
                win[k] = r.IsOver && r.LoserSeat != aSeat ? 1 : 0;
            });
            return win.Average();
        }

        /// <summary>
        /// Simulations par décision dans le rapport des réglages du jeu (Expert : 350 ms, Difficile : 150 ms, soit
        /// environ trois fois plus de simulations pour l'Expert), comptées pour rester reproductibles.
        /// </summary>
        static AISettings Counted(AILevel level, int iterations)
        {
            var s = AISettings.For(level);
            s.TimeBudgetMs = 0;
            s.MaxIterations = iterations;
            return s;
        }

        [Test]
        public void Tournament_ExpertBeatsHard()
        {
            const int deals = 200;
            double rate = HeadsUp(Counted(AILevel.Expert, 300), Counted(AILevel.Difficile, 100), deals, 31);
            TestContext.WriteLine($"Expert contre Difficile, {deals * 2} parties à deux : {rate:P1} de victoires");
            Assert.Greater(rate, 0.54);
        }

        /// <summary>
        /// Mesure longue (hors CI, à lancer à la main) : 1 000 parties, avec et sans les indices.
        /// <c>dotnet test --filter Name=Measure_ExpertAgainstHard</c>
        /// </summary>
        [Test, Explicit("Mesure longue (plusieurs minutes)")]
        public void Measure_ExpertAgainstHard()
        {
            const int deals = 500;
            var withClues = Counted(AILevel.Expert, 600);
            var withoutClues = Counted(AILevel.Expert, 600);
            withoutClues.UseHandClues = false;
            var hard = Counted(AILevel.Difficile, 200);
            TestContext.WriteLine($"Expert : {HeadsUp(withClues, hard, deals, 5):P1} de victoires sur {deals * 2} parties");
            TestContext.WriteLine($"Expert sans indices : {HeadsUp(withoutClues, hard, deals, 5):P1}");
        }
    }
}
