using System;
using System.Collections.Generic;
using System.Linq;
using Check.Core;
using NUnit.Framework;

namespace Check.Tests
{
    /// <summary>Logique d'interface tenue dans Core (bulles d'apprentissage, fiches, statistiques…).</summary>
    public class UxTests
    {
        static Card C(string s) => Card.Parse(s);
        static Card[] Cs(string s) => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Card.Parse).ToArray();

        static GameEvent Played(string cards) => new GameEvent { Kind = GameEventKind.Played, Cards = Cs(cards) };

        // ---------- Bulles de la première partie ----------

        [Test]
        public void Tips_EventsTriggerTheRightSituation()
        {
            Assert.AreEqual(TipId.Ace, FirstGameTips.ForEvent(Played("AS")));
            Assert.AreEqual(TipId.Seven, FirstGameTips.ForEvent(Played("7H 7C")));
            Assert.AreEqual(TipId.Joker, FirstGameTips.ForEvent(Played("JkR")));
            Assert.AreEqual(TipId.Jack, FirstGameTips.ForEvent(new GameEvent { Kind = GameEventKind.SuitRequested, Suit = Suit.Hearts }));
            Assert.AreEqual(TipId.Two, FirstGameTips.ForEvent(new GameEvent { Kind = GameEventKind.PlayedUnder, Cards = Cs("2D") }));
            Assert.IsNull(FirstGameTips.ForEvent(Played("9H")));
            Assert.IsNull(FirstGameTips.ForEvent(new GameEvent { Kind = GameEventKind.Drew }));
        }

        [Test]
        public void Tips_MaskTracksEachSituationOnce()
        {
            int mask = 0;
            Assert.IsFalse(FirstGameTips.IsSeen(mask, TipId.Joker));
            mask = FirstGameTips.MarkSeen(mask, TipId.Joker);
            Assert.IsTrue(FirstGameTips.IsSeen(mask, TipId.Joker));
            Assert.IsFalse(FirstGameTips.IsSeen(mask, TipId.Ace));
            Assert.IsFalse(FirstGameTips.AllSeen(mask));
            foreach (var t in FirstGameTips.All) mask = FirstGameTips.MarkSeen(mask, t);
            Assert.IsTrue(FirstGameTips.AllSeen(mask));
            foreach (var t in FirstGameTips.All) Assert.IsNotEmpty(FirstGameTips.Text(t, RuleSet.Default));
        }

        [Test]
        public void Tips_CounterOnlyWhenTheHandCanAnswerASeven()
        {
            var withSeven = Round.FromState(RuleSet.Default, new[] { Cs("7C 9D"), Cs("KH KS") }, Cs("7H"), Cs("10C 10D"),
                0, null, null, 2, PenaltyKind.Seven);
            Assert.IsTrue(FirstGameTips.CanCounterSeven(withSeven, 0));
            Assert.IsFalse(FirstGameTips.CanCounterSeven(withSeven, 1), "pas son tour");
            var without = Round.FromState(RuleSet.Default, new[] { Cs("8C 9D"), Cs("KH KS") }, Cs("7H"), Cs("10C 10D"),
                0, null, null, 2, PenaltyKind.Seven);
            Assert.IsFalse(FirstGameTips.CanCounterSeven(without, 0));
            var joker = Round.FromState(RuleSet.Default, new[] { Cs("JkN 9D"), Cs("KH KS") }, Cs("7H"), Cs("10C 10D"),
                0, null, null, 4, PenaltyKind.Joker);
            Assert.IsFalse(FirstGameTips.CanCounterSeven(joker, 0), "un joker ne se contre pas");
        }

        // ---------- Fiches de règles ----------

        [Test]
        public void RulesSheets_CoverEverySpecialCard()
        {
            Assert.GreaterOrEqual(RulesSheets.All.Count, 4);
            foreach (var s in RulesSheets.All)
            {
                Assert.IsNotEmpty(s.Title);
                Assert.IsNotEmpty(s.Body);
            }
            var shown = RulesSheets.All.SelectMany(s => s.Cards).Select(c => c.Rank).ToList();
            foreach (var r in new[] { Rank.Ace, Rank.Seven, Rank.Joker, Rank.Jack, Rank.Two }) CollectionAssert.Contains(shown, r);
        }

        // ---------- Classement final ----------

        [Test]
        public void Standings_WinnerFirstThenLastEliminatedFirst()
        {
            CollectionAssert.AreEqual(new[] { 2, 0, 3, 1 }, Standings.Order(2, new[] { 1, 3, 0 }));
            CollectionAssert.AreEqual(new[] { 1, 0 }, Standings.Order(null, new[] { 0, 1 }));
            Assert.AreEqual(1, Standings.PodiumSlot(1));
            Assert.AreEqual(0, Standings.PodiumSlot(2));
            Assert.AreEqual(2, Standings.PodiumSlot(3));
            Assert.AreEqual(-1, Standings.PodiumSlot(4));
        }

        [Test]
        public void Standings_OfAFullMatchListsEveryPlayerOnce()
        {
            var rng = new SystemRandom(11);
            var match = new Match(RuleSet.Default, 5, rng);
            var pick = new System.Random(3);
            match.StartRound();
            for (int guard = 0; guard < 20000 && !match.IsOver; guard++)
            {
                if (match.Round.IsOver) { match.StartRound(); continue; }
                var legal = match.Round.LegalActions();
                match.Apply(legal[pick.Next(legal.Count)]);
            }
            Assert.IsTrue(match.IsOver);
            var order = Standings.Of(match);
            Assert.AreEqual(5, order.Count);
            Assert.AreEqual(match.Winner, order[0]);
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 5), order);
        }

        // ---------- Ambiance du wagon ----------

        [Test]
        public void Rumble_LoopsWithoutClickAndStaysInRange()
        {
            const int rate = 22050;
            var data = RumbleSynth.Render(rate);
            Assert.AreEqual((int)Math.Round(RumbleSynth.LoopSeconds * rate), data.Length);
            float peak = data.Max(Math.Abs);
            Assert.That(peak, Is.InRange(0.5f, 0.81f));
            // Raccord : l'écart entre la fin et le début ne dépasse pas l'écart typique entre deux échantillons voisins.
            float typical = 0f;
            for (int i = 1; i < data.Length; i++) typical = Math.Max(typical, Math.Abs(data[i] - data[i - 1]));
            Assert.LessOrEqual(Math.Abs(data[0] - data[data.Length - 1]), typical);
            var hits = RumbleSynth.WheelHits();
            Assert.AreEqual(RumbleSynth.JointsPerLoop * 4, hits.Count);
            Assert.IsTrue(hits.All(h => h > 0f && h < RumbleSynth.LoopSeconds));
        }

        [Test]
        public void Rumble_TensionRisesWithTheLastCards()
        {
            Assert.AreEqual(0f, RumbleSynth.Tension(new[] { 5, 4, 6 }));
            Assert.AreEqual(0.4f, RumbleSynth.Tension(new[] { 5, 2, 6 }));
            Assert.AreEqual(1f, RumbleSynth.Tension(new[] { 2, 1, 6 }));
            Assert.AreEqual(0f, RumbleSynth.Tension(new int[0]));
        }
    }
}
