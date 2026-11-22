using System;
using System.Collections.Generic;
using System.Linq;
using Check.Core;
using NUnit.Framework;

namespace Check.Tests
{
    /// <summary>Film de sauvegarde (Core/SaveReplay) : codage des actions et reprise d'une partie en cours.</summary>
    public class SaveReplayTests
    {
        const int Seed = 1234;
        const int Dealer = 2;

        /// <summary>Joue une partie au hasard et note le film, en s'arrêtant après <paramref name="stopAfter"/> jetons.</summary>
        static (Match match, List<string> log) Record(RuleSet rules, int players, int stopAfter, int seed = Seed)
        {
            var match = new Match(rules, players, new SystemRandom(seed), Dealer);
            var rng = new SystemRandom(seed + 1);
            var log = new List<string>();
            while (!match.IsOver && log.Count < stopAfter)
            {
                if (match.Round == null || match.Round.IsOver)
                {
                    match.StartRound();
                    log.Add(SaveReplay.RoundToken);
                    continue;
                }
                var actions = match.Round.LegalActions();
                var a = actions[rng.Next(actions.Count)];
                match.Apply(a);
                log.Add(SaveReplay.Encode(a));
            }
            return (match, log);
        }

        static void AssertSamePosition(Match expected, Match actual)
        {
            Assert.AreEqual(expected.RoundNumber, actual.RoundNumber);
            CollectionAssert.AreEqual(expected.Alive, actual.Alive);
            var e = expected.Round;
            var a = actual.Round;
            Assert.AreEqual(e.Current, a.Current);
            Assert.AreEqual(e.Top, a.Top);
            Assert.AreEqual(e.PendingDraw, a.PendingDraw);
            Assert.AreEqual(e.RequestedSuit, a.RequestedSuit);
            Assert.AreEqual(e.DrawPileCount, a.DrawPileCount);
            Assert.AreEqual(e.IsOver, a.IsOver);
            for (int s = 0; s < e.SeatCount; s++) CollectionAssert.AreEqual(e.Hand(s), a.Hand(s), $"main du siège {s}");
        }

        [Test]
        public void Encode_DecodeRoundTripsEveryLegalAction([Values(2, 4, 10)] int players)
        {
            var match = new Match(RuleSet.Default, players, new SystemRandom(players), 0);
            var rng = new SystemRandom(9);
            match.StartRound();
            int checkedActions = 0;
            for (int step = 0; step < 300 && !match.Round.IsOver; step++)
            {
                var actions = match.Round.LegalActions();
                foreach (var a in actions.Concat(actions.Where(x => x.Kind == ActionKind.Play).Select(x => x.WithCheck(!x.DeclaresCheck))))
                {
                    Assert.AreEqual(a, SaveReplay.Decode(SaveReplay.Encode(a)), SaveReplay.Encode(a));
                    checkedActions++;
                }
                match.Apply(actions[rng.Next(actions.Count)]);
            }
            Assert.Greater(checkedActions, 50);
        }

        [Test]
        public void Encode_KeepsTheKnownFormat()
        {
            var jack = new Card(Rank.Jack, Suit.Hearts);
            Assert.AreEqual("D", SaveReplay.Encode(TurnAction.Draw));
            Assert.AreEqual("S", SaveReplay.Encode(TurnAction.Pass));
            Assert.AreEqual($"P:{jack.Id}:3:1", SaveReplay.Encode(TurnAction.Play(jack, Suit.Clubs, true)));
            Assert.AreEqual("P:52,53:-:0", SaveReplay.Encode(TurnAction.Play(new[] { Card.BlackJoker, Card.RedJoker })));
        }

        [TestCase("")]
        [TestCase("X")]
        [TestCase("P:12:-")]
        [TestCase("P::-:0")]
        [TestCase("P:54:-:0")]
        [TestCase("P:-1:-:0")]
        [TestCase("P:12:7:0")]
        [TestCase("P:12:-:2")]
        [TestCase("Q:12:-:0")]
        [TestCase(null)]
        public void Decode_RejectsGarbage(string token)
        {
            Assert.Throws<FormatException>(() => SaveReplay.Decode(token));
        }

        [Test]
        public void TryReplay_RebuildsTheSamePosition([Values(2, 4, 7, 10)] int players, [Values(1, 40, 400)] int length)
        {
            var (original, log) = Record(RuleSet.Default, players, length);
            if (original.IsOver) (original, log) = Record(RuleSet.Default, players, log.Count - 1); // coupe juste avant le dernier coup
            Assert.IsFalse(original.IsOver);
            var fresh = new Match(RuleSet.Default, players, new SystemRandom(Seed), Dealer);
            Assert.IsTrue(SaveReplay.TryReplay(fresh, SaveReplay.CurrentFormat, log, out var reason), reason);
            Assert.IsNull(reason);
            AssertSamePosition(original, fresh);
        }

        [Test]
        public void TryReplay_AcceptsAPauseBetweenRounds()
        {
            // Sauvegarde prise sur l'écran de fin de tour : le tour est fini, le suivant pas encore distribué.
            var match = new Match(RuleSet.Default, 4, new SystemRandom(Seed), Dealer);
            var rng = new SystemRandom(5);
            var log = new List<string>();
            match.StartRound();
            log.Add(SaveReplay.RoundToken);
            while (!match.Round.IsOver)
            {
                var actions = match.Round.LegalActions();
                var a = actions[rng.Next(actions.Count)];
                match.Apply(a);
                log.Add(SaveReplay.Encode(a));
            }
            var fresh = new Match(RuleSet.Default, 4, new SystemRandom(Seed), Dealer);
            Assert.IsTrue(SaveReplay.TryReplay(fresh, SaveReplay.CurrentFormat, log, out var reason), reason);
            Assert.IsTrue(fresh.Round.IsOver);
            Assert.AreEqual(3, fresh.Alive.Count);
        }

        [Test]
        public void TryReplay_RefusesAnotherFormat([Values(0, SaveReplay.CurrentFormat + 1)] int format)
        {
            var (_, log) = Record(RuleSet.Default, 4, 30);
            var fresh = new Match(RuleSet.Default, 4, new SystemRandom(Seed), Dealer);
            Assert.IsFalse(SaveReplay.TryReplay(fresh, format, log, out var reason));
            StringAssert.Contains("format " + format, reason);
        }

        [Test]
        public void TryReplay_RefusesAnIllegalMoveWithItsPosition()
        {
            var (_, log) = Record(RuleSet.Default, 4, 30);
            // Une passe sans avoir pioché est illégale avec la règle par défaut (piocher termine le tour).
            log.Insert(5, SaveReplay.Encode(TurnAction.Pass));
            var fresh = new Match(RuleSet.Default, 4, new SystemRandom(Seed), Dealer);
            Assert.IsFalse(SaveReplay.TryReplay(fresh, SaveReplay.CurrentFormat, log, out var reason));
            StringAssert.Contains("coup 6", reason);
        }

        [Test]
        public void TryReplay_OldRuleFilmDoesNotReplayUnderTheNewRule()
        {
            // Film joué avec l'ancienne variante « pioche, puis pose ou passe » (sauvegardes d'avant le 21/11),
            // rejoué avec la règle actuelle : il doit échouer proprement, sans exception.
            var old = RuleSet.Default;
            old.MayPlayDrawnCard = true;
            var (_, log) = Record(old, 4, 200);
            Assume.That(log.Contains("S"), "le film contient une passe après une pioche");
            var fresh = new Match(RuleSet.Default, 4, new SystemRandom(Seed), Dealer);
            Assert.IsFalse(SaveReplay.TryReplay(fresh, SaveReplay.CurrentFormat, log, out var reason));
            Assert.IsNotNull(reason);
        }

        [Test]
        public void TryReplay_RefusesCorruptOrEmptyFilms()
        {
            var (_, log) = Record(RuleSet.Default, 3, 20);
            Match Fresh() => new Match(RuleSet.Default, 3, new SystemRandom(Seed), Dealer);

            Assert.IsFalse(SaveReplay.TryReplay(Fresh(), SaveReplay.CurrentFormat, new List<string>(), out var r1));
            Assert.IsFalse(SaveReplay.TryReplay(Fresh(), SaveReplay.CurrentFormat, null, out _));
            Assert.IsFalse(SaveReplay.TryReplay(Fresh(), SaveReplay.CurrentFormat, log.Skip(1).ToList(), out var r2), "film sans « R » initial");

            var garbled = log.ToList();
            garbled[4] = "P:99:-:0";
            Assert.IsFalse(SaveReplay.TryReplay(Fresh(), SaveReplay.CurrentFormat, garbled, out var r3));
            StringAssert.Contains("coup 5", r3);

            var doubleRound = log.ToList();
            doubleRound.Insert(3, SaveReplay.RoundToken);
            Assert.IsFalse(SaveReplay.TryReplay(Fresh(), SaveReplay.CurrentFormat, doubleRound, out var r4));
            StringAssert.Contains("coup 4", r4);
            Assert.IsNotNull(r1);
            Assert.IsNotNull(r2);
        }

        [Test]
        public void TryReplay_RefusesAFinishedMatch()
        {
            var (original, log) = Record(RuleSet.Default, 2, int.MaxValue);
            Assert.IsTrue(original.IsOver);
            var fresh = new Match(RuleSet.Default, 2, new SystemRandom(Seed), Dealer);
            Assert.IsFalse(SaveReplay.TryReplay(fresh, SaveReplay.CurrentFormat, log, out var reason));
            StringAssert.Contains("finie", reason);
        }
    }
}
