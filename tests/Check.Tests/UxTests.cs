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
    }
}
