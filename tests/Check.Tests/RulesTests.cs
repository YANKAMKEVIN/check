using System;
using System.Collections.Generic;
using System.Linq;
using Check.Core;
using NUnit.Framework;

namespace Check.Tests
{
    public class RulesTests
    {
        // ---------- Utilitaires ----------

        static Card C(string s) => Card.Parse(s);
        static Card[] Cs(string s) => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Card.Parse).ToArray();

        /// <summary>Position à 3 joueurs : mains, tas (du dessous au dessus), pioche (tirée par la fin).</summary>
        static Round Pos(string hand0, string hand1, string hand2, string discard, string drawPile = "10C 10D 10H 10S",
            int current = 0, Suit? requested = null, int pending = 0, PenaltyKind kind = PenaltyKind.None, RuleSet rules = null)
        {
            var hands = new List<Card[]> { Cs(hand0), Cs(hand1) };
            if (hand2 != null) hands.Add(Cs(hand2));
            return Round.FromState(rules ?? RuleSet.Default, hands, Cs(discard), Cs(drawPile), current, null, requested, pending, kind);
        }

        static IReadOnlyList<GameEvent> Play(Round r, string cards, Suit? suit = null, bool check = false) =>
            r.Apply(TurnAction.Play(Cs(cards), suit, check));

        static bool CanPlay(Round r, string cards, Suit? suit = null) =>
            r.IsLegal(TurnAction.Play(Cs(cards), suit));

        // ---------- Cartes ----------

        [Test]
        public void Deck_Has54UniqueCardsWithTwoJokers()
        {
            var deck = Card.FullDeck();
            Assert.AreEqual(54, deck.Count);
            Assert.AreEqual(54, deck.Distinct().Count());
            Assert.AreEqual(2, deck.Count(c => c.IsJoker));
            Assert.AreEqual(CardColor.Black, Card.BlackJoker.Color);
            Assert.AreEqual(CardColor.Red, Card.RedJoker.Color);
            Assert.AreEqual(4, deck.Count(c => c.Rank == Rank.Seven));
        }

        [Test]
        public void Parse_RoundTripsEveryCard()
        {
            foreach (var c in Card.FullDeck()) Assert.AreEqual(c, Card.Parse(c.ToString()));
            Assert.AreEqual(new Card(Rank.Jack, Suit.Spades), C("JS"));
            Assert.AreEqual(new Card(Rank.Ten, Suit.Hearts), C("10H"));
        }

        [TestCase(2, 7), TestCase(3, 7), TestCase(4, 6), TestCase(5, 6), TestCase(6, 5), TestCase(7, 5), TestCase(8, 4), TestCase(10, 4)]
        public void Deal_CardCountDependsOnPlayers(int players, int expected)
        {
            var round = new Round(RuleSet.Default, Enumerable.Range(0, players).ToList(), 0, new SystemRandom(players));
            for (int s = 0; s < players; s++) Assert.AreEqual(expected, round.Hand(s).Count);
            Assert.AreEqual(54 - players * expected - 1, round.DrawPileCount);
            Assert.AreEqual(1, round.Discard.Count);
        }

        [Test]
        public void Deal_StartCardIsNeverSpecial()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                var round = new Round(RuleSet.Default, new[] { 0, 1, 2, 3 }, 0, new SystemRandom(seed));
                Assert.IsFalse(RuleSet.IsSpecial(round.Top.Rank), $"graine {seed} : {round.Top}");
            }
        }

        // ---------- Pose simple ----------

        [Test]
        public void Play_SameSuitOrSameRank()
        {
            var r = Pos("9H 9S KC 3D", "6C 6D", "8C 8D", "5H");
            Assert.IsTrue(CanPlay(r, "9H"), "même couleur");
            Assert.IsFalse(CanPlay(r, "9S"), "ni couleur ni valeur");
            Assert.IsFalse(CanPlay(r, "KC"));
            r = Pos("9H 9S KC 3D", "6C 6D", "8C 8D", "9C");
            Assert.IsTrue(CanPlay(r, "9S"), "même valeur");
        }

        [Test]
        public void Play_RemovesCardsAndPassesTurn()
        {
            var r = Pos("9H 9S KC 3D", "6C 6D", "8C 8D", "5H");
            Play(r, "9H");
            Assert.AreEqual(C("9H"), r.Top);
            Assert.AreEqual(3, r.Hand(0).Count);
            Assert.AreEqual(1, r.Current);
        }

        [Test]
        public void Play_CardNotInHandIsIllegal()
        {
            var r = Pos("9H 3D", "6C 6D", "8C 8D", "5H");
            Assert.IsFalse(CanPlay(r, "6H"));
            Assert.Throws<InvalidOperationException>(() => Play(r, "6H"));
        }

        // ---------- Poses multiples ----------

        [Test]
        public void Multiple_SameRankAllowed_LastCardIsTop()
        {
            var r = Pos("9H 9S 9C KD", "6C 6D", "8C 8D", "5H");
            Assert.IsTrue(CanPlay(r, "9H 9C 9S"));
            Play(r, "9H 9C 9S", check: true);
            Assert.AreEqual(C("9S"), r.Top);
            Assert.AreEqual(1, r.Hand(0).Count);
        }

        [Test]
        public void Multiple_FirstCardMustBeLegal()
        {
            var r = Pos("9H 9S 9C KD", "6C 6D", "8C 8D", "5H");
            Assert.IsFalse(CanPlay(r, "9S 9H"), "le 9♠ ne va pas sur le 5♥");
        }

        [Test]
        public void Multiple_DifferentRanksIllegal()
        {
            var r = Pos("9H 8H KD", "6C 6D", "8C 8D", "5H");
            Assert.IsFalse(CanPlay(r, "9H 8H"));
        }

        [Test]
        public void Multiple_CanBeDisabled()
        {
            var rules = RuleSet.Default;
            rules.AllowMultiples = false;
            var r = Pos("9H 9S KD", "6C 6D", "8C 8D", "5H", rules: rules);
            Assert.IsFalse(CanPlay(r, "9H 9S"));
        }

        // ---------- As ----------

        [Test]
        public void Ace_SkipsNextPlayer()
        {
            var r = Pos("AH KD", "6C 6D", "8C 8D", "5H");
            var ev = Play(r, "AH");
            Assert.AreEqual(2, r.Current);
            Assert.IsTrue(ev.Any(e => e.Kind == GameEventKind.Skipped && e.Seat == 1));
        }

        [Test]
        public void Ace_TwoAcesSkipTwoPlayers()
        {
            var r = Pos("AH AS KD", "6C 6D", "8C 8D", "5H");
            Play(r, "AH AS");
            Assert.AreEqual(0, r.Current, "à 3 joueurs, deux As reviennent au poseur");
        }

        [Test]
        public void Ace_HeadsUpReplays()
        {
            var r = Pos("AH KD", "6C 6D", null, "5H");
            Play(r, "AH");
            Assert.AreEqual(0, r.Current);
        }

        [Test]
        public void Ace_HeadsUpTwoAcesStillReplay()
        {
            var r = Pos("AH AS KD", "6C 6D", null, "5H");
            Play(r, "AH AS");
            Assert.AreEqual(0, r.Current, "à deux, les sauts ne font jamais sauter le poseur");
        }

        // ---------- 7 ----------

        [Test]
        public void Seven_NextPlayerMustDrawTwo()
        {
            var r = Pos("7H KD", "6C 6D", "8C 8D", "5H");
            Play(r, "7H");
            Assert.AreEqual(1, r.Current);
            Assert.AreEqual(2, r.PendingDraw);
            Assert.IsFalse(CanPlay(r, "6C"), "on ne joue pas normalement sous une pénalité");
            var actions = r.LegalActions();
            Assert.AreEqual(1, actions.Count);
            Assert.AreEqual(ActionKind.Draw, actions[0].Kind);

            r.Apply(TurnAction.Draw);
            Assert.AreEqual(4, r.Hand(1).Count);
            Assert.AreEqual(0, r.PendingDraw);
            Assert.AreEqual(2, r.Current, "piocher la pénalité termine le tour");
        }

        [Test]
        public void Seven_StacksWithAnySeven()
        {
            var r = Pos("7H KD", "7C 6D", "8C 8D", "5H");
            Play(r, "7H", check: true);
            Assert.IsTrue(CanPlay(r, "7C"), "un 7 de n'importe quelle couleur contre");
            Play(r, "7C", check: true);
            Assert.AreEqual(4, r.PendingDraw);
            Assert.AreEqual(2, r.Current);
            r.Apply(TurnAction.Draw);
            Assert.AreEqual(6, r.Hand(2).Count);
        }

        [Test]
        public void Seven_TwoSevensAtOnceGiveFour()
        {
            var r = Pos("7H 7S KD", "6C 6D", "8C 8D", "5H");
            Play(r, "7H 7S");
            Assert.AreEqual(4, r.PendingDraw);
        }

        [Test]
        public void Seven_CounteredBySameColorJoker()
        {
            var r = Pos("7H KD", "JkR JkN 6D", "8C 8D", "5H");
            Play(r, "7H");
            Assert.IsTrue(CanPlay(r, "JkR"), "joker rouge sur 7 rouge");
            Assert.IsFalse(CanPlay(r, "JkN"), "joker noir sur 7 rouge");
            Play(r, "JkR");
            Assert.AreEqual(6, r.PendingDraw, "2 + 4");
            Assert.AreEqual(PenaltyKind.Joker, r.PendingKind);
        }

        [Test]
        public void Seven_JokerCounterCanBeDisabled()
        {
            var rules = RuleSet.Default;
            rules.JokerCountersSeven = false;
            var r = Pos("7H KD", "JkR 6D", "8C 8D", "5H", rules: rules);
            Play(r, "7H");
            Assert.IsFalse(CanPlay(r, "JkR"));
        }

        [Test]
        public void Seven_TwoCannotCounter()
        {
            var r = Pos("7H KD", "2C 6D", "8C 8D", "5H");
            Play(r, "7H");
            Assert.IsFalse(CanPlay(r, "2C"));
        }

        [Test]
        public void Seven_AfterPenaltyNextPlaysOnTheSevenNormally()
        {
            var r = Pos("7H KD", "6C 6D", "9H 8D", "5H");
            Play(r, "7H");
            r.Apply(TurnAction.Draw);
            Assert.AreEqual(0, r.PendingDraw);
            Assert.IsTrue(CanPlay(r, "9H"), "cœur sur le 7♥");
        }

        // ---------- Joker ----------

        [Test]
        public void Joker_ColorRestriction()
        {
            var r = Pos("JkN JkR KD", "6C 6D", "8C 8D", "5S");
            Assert.IsTrue(CanPlay(r, "JkN"));
            Assert.IsFalse(CanPlay(r, "JkR"));
            r = Pos("JkN JkR KD", "6C 6D", "8C 8D", "5D");
            Assert.IsFalse(CanPlay(r, "JkN"));
            Assert.IsTrue(CanPlay(r, "JkR"));
        }

        [Test]
        public void Joker_NextDrawsFour_NoCounter()
        {
            var r = Pos("JkN KD", "7S 7C 6D", "8C 8D", "5S");
            Play(r, "JkN", check: true);
            Assert.AreEqual(4, r.PendingDraw);
            Assert.IsFalse(CanPlay(r, "7S"), "un 7 ne contre pas un joker");
            Assert.AreEqual(1, r.LegalActions().Count);
            r.Apply(TurnAction.Draw);
            Assert.AreEqual(7, r.Hand(1).Count);
        }

        [Test]
        public void Joker_AfterPenaltyAnyCardOfItsColor()
        {
            var r = Pos("JkN KD", "6C 6D", "9C 9H", "5S");
            Play(r, "JkN");
            r.Apply(TurnAction.Draw);
            Assert.IsTrue(CanPlay(r, "9C"), "trèfle (noir) sur joker noir");
            Assert.IsFalse(CanPlay(r, "9H"), "cœur (rouge) sur joker noir");
        }

        [Test]
        public void Joker_CannotPlaySeveralAtOnce()
        {
            var r = Pos("JkN JkR KD", "6C 6D", "8C 8D", "5S");
            Assert.IsFalse(CanPlay(r, "JkN JkR"));
        }

        // ---------- Valet ----------

        [Test]
        public void Jack_RequestsSuit()
        {
            var r = Pos("JS KD", "6C 6D KH", "8C 8D", "5H");
            Assert.IsTrue(CanPlay(r, "JS", Suit.Diamonds), "Valet sur n'importe quoi");
            Assert.IsFalse(CanPlay(r, "JS"), "il faut demander une couleur");
            Play(r, "JS", Suit.Diamonds);
            Assert.AreEqual(Suit.Diamonds, r.RequestedSuit);
            Assert.IsTrue(CanPlay(r, "6D"));
            Assert.IsFalse(CanPlay(r, "6C"), "pique sur Valet de pique mais carreau demandé");
            Assert.IsFalse(CanPlay(r, "KH"));
            Play(r, "6D");
            Assert.IsNull(r.RequestedSuit);
        }

        [Test]
        public void Jack_OnRequestedSuitAnotherJackOrRedJokerForRedSuit()
        {
            var r = Pos("JS KD", "JC JkR JkN", "8C 8D", "5H");
            Play(r, "JS", Suit.Hearts);
            Assert.IsTrue(CanPlay(r, "JC", Suit.Clubs));
            Assert.IsTrue(CanPlay(r, "JkR"), "cœur demandé = rouge");
            Assert.IsFalse(CanPlay(r, "JkN"));
        }

        [Test]
        public void Jack_NotWildWhenDisabled()
        {
            var rules = RuleSet.Default;
            rules.JackIsWild = false;
            var r = Pos("JS JH KD", "6C 6D", "8C 8D", "5H", rules: rules);
            Assert.IsFalse(CanPlay(r, "JS", Suit.Clubs));
            Assert.IsTrue(CanPlay(r, "JH", Suit.Clubs));
        }

        [Test]
        public void Jack_NonJackCannotRequestSuit()
        {
            var r = Pos("5S KD", "6C 6D", "8C 8D", "5H");
            Assert.IsFalse(CanPlay(r, "5S", Suit.Clubs));
        }

        // ---------- 2 passe-partout ----------

        [Test]
        public void Two_GoesUnderPile_TopUnchanged()
        {
            var r = Pos("2C KD", "6C 6D", "8C 8D", "4S 5H");
            Assert.IsTrue(CanPlay(r, "2C"), "passe-partout");
            var ev = Play(r, "2C");
            Assert.AreEqual(C("5H"), r.Top, "le dessus ne change pas");
            Assert.AreEqual(C("2C"), r.Discard[0], "le 2 est sous le tas");
            Assert.AreEqual(1, r.Current);
            Assert.IsTrue(ev.Any(e => e.Kind == GameEventKind.PlayedUnder));
        }

        [Test]
        public void Two_KeepsRequestedSuit()
        {
            var r = Pos("2C KD", "6C 6D", "8C 8D", "JS", requested: Suit.Diamonds);
            Play(r, "2C");
            Assert.AreEqual(Suit.Diamonds, r.RequestedSuit);
        }

        [Test]
        public void Two_SeveralAtOnce()
        {
            var r = Pos("2C 2D KD", "6C 6D", "8C 8D", "5H");
            Play(r, "2C 2D", check: true);
            Assert.AreEqual(C("5H"), r.Top);
            Assert.AreEqual(1, r.Hand(0).Count);
        }

        // ---------- Pioche ----------

        [Test]
        public void Draw_PlayableDrawnCardMayBePlayed()
        {
            var r = Pos("KD 3C", "6C 6D", "8C 8D", "5H", drawPile: "4C 9H");
            r.Apply(TurnAction.Draw);
            Assert.AreEqual(0, r.Current, "9♥ jouable : le joueur garde la main");
            Assert.IsTrue(r.HasDrawn);
            Assert.IsFalse(CanPlay(r, "3C"), "seule la carte piochée peut être posée");
            Assert.IsTrue(CanPlay(r, "9H"));
            Assert.IsTrue(r.IsLegal(TurnAction.Pass));
            Assert.IsFalse(r.IsLegal(TurnAction.Draw), "une seule pioche par tour");
        }

        [Test]
        public void Draw_UnplayableDrawnCardPassesAutomatically()
        {
            var r = Pos("KD 3C", "6C 6D", "8C 8D", "5H", drawPile: "4C 9S");
            var ev = r.Apply(TurnAction.Draw);
            Assert.AreEqual(1, r.Current);
            Assert.IsTrue(ev.Any(e => e.Kind == GameEventKind.Passed));
        }

        [Test]
        public void Draw_VoluntaryDrawCanBeForbidden()
        {
            var rules = RuleSet.Default;
            rules.MayDrawVoluntarily = false;
            var r = Pos("KH 3C", "6C 6D", "8C 8D", "5H", rules: rules);
            Assert.IsFalse(r.IsLegal(TurnAction.Draw));
            r = Pos("KD 3C", "6C 6D", "8C 8D", "5H", rules: rules);
            Assert.IsTrue(r.IsLegal(TurnAction.Draw), "aucune carte jouable");
        }

        [Test]
        public void Draw_ReshufflesDiscardKeepingTop()
        {
            var r = Pos("KD 3C", "6C 6D", "8C 8D", "4S 4H 9C 5H", drawPile: "");
            var ev = r.Apply(TurnAction.Draw);
            Assert.IsTrue(ev.Any(e => e.Kind == GameEventKind.Reshuffled));
            Assert.AreEqual(C("5H"), r.Top);
            Assert.AreEqual(1, r.Discard.Count);
            Assert.AreEqual(3, r.Hand(0).Count);
        }

        [Test]
        public void Draw_PenaltyLimitedByAvailableCards()
        {
            var r = Pos("JkN KD", "6C 6D", "8C 8D", "4S 5S", drawPile: "9D");
            Play(r, "JkN", check: true);
            r.Apply(TurnAction.Draw);
            Assert.AreEqual(5, r.Hand(1).Count, "9♦ + 4♠ + 5♠, le joker reste au-dessus");
            Assert.AreEqual(1, r.Discard.Count);
            Assert.AreEqual(2, r.Current);
        }

        // ---------- Check et Games ----------

        [Test]
        public void Check_DeclaredWhenOneCardLeft()
        {
            var r = Pos("9H KD", "6C 6D", "8C 8D", "5H");
            var ev = Play(r, "9H", check: true);
            Assert.IsTrue(ev.Any(e => e.Kind == GameEventKind.CheckDeclared && e.Seat == 0));
            Assert.AreEqual(1, r.Hand(0).Count);
        }

        [Test]
        public void Check_ForgottenDrawsPenalty()
        {
            var r = Pos("9H KD", "6C 6D", "8C 8D", "5H");
            var ev = Play(r, "9H", check: false);
            Assert.IsTrue(ev.Any(e => e.Kind == GameEventKind.CheckForgotten && e.Count == 2));
            Assert.AreEqual(3, r.Hand(0).Count);
            Assert.AreEqual(1, r.Current);
        }

        [Test]
        public void Check_PenaltyCanBeDisabled()
        {
            var rules = RuleSet.Default;
            rules.ForgotCheckPenalty = 0;
            var r = Pos("9H KD", "6C 6D", "8C 8D", "5H", rules: rules);
            Play(r, "9H");
            Assert.AreEqual(1, r.Hand(0).Count);
        }

        [Test]
        public void Check_LegalActionsDeclareCheckAutomatically()
        {
            var r = Pos("9H KD", "6C 6D", "8C 8D", "5H");
            var play = r.LegalActions().Single(a => a.Kind == ActionKind.Play);
            Assert.IsTrue(play.DeclaresCheck);
        }

        [Test]
        public void Games_FinishingPlayerLeavesTheRound()
        {
            var r = Pos("9H", "6C 6D", "8C 8D", "5H");
            var ev = Play(r, "9H");
            Assert.IsTrue(ev.Any(e => e.Kind == GameEventKind.Finished && e.Seat == 0));
            Assert.IsTrue(r.IsFinished(0));
            Assert.IsFalse(r.IsOver, "deux joueurs ont encore des cartes");
            Assert.AreEqual(1, r.Current);
        }

        [Test]
        public void Games_FinishedPlayersAreSkippedInTurnOrder()
        {
            var r = Pos("9H", "9D 6D", "8C 8D", "5H");
            Play(r, "9H");
            Play(r, "9D", check: true);
            Assert.AreEqual(2, r.Current);
            r.Apply(TurnAction.Draw); // pioche 5S injouable → passe
            Assert.AreEqual(1, r.Current, "le siège 0 a fini, on revient au siège 1");
        }

        [Test]
        public void Games_LastPlayerWithCardsIsEliminated()
        {
            var r = Pos("9H", "6C 6D", null, "5H");
            var ev = Play(r, "9H");
            Assert.IsTrue(r.IsOver);
            Assert.AreEqual(1, r.LoserSeat);
            Assert.IsTrue(ev.Any(e => e.Kind == GameEventKind.Eliminated && e.Seat == 1));
            Assert.IsEmpty(r.LegalActions());
        }

        [Test]
        public void Games_FinishingOnSevenStillPenalizesNext()
        {
            var r = Pos("7H", "6C 6D", "8C 8D", "5H");
            Play(r, "7H");
            Assert.IsTrue(r.IsFinished(0));
            Assert.AreEqual(2, r.PendingDraw);
            Assert.AreEqual(1, r.Current);
        }

        [Test]
        public void Games_FinishingWithTwo()
        {
            var r = Pos("2S", "6C 6D", "8C 8D", "5H");
            Play(r, "2S");
            Assert.IsTrue(r.IsFinished(0));
        }

        // ---------- Actions légales ----------

        [Test]
        public void LegalActions_AllAreLegalAndCoverMultiplesAndJackSuits()
        {
            var r = Pos("9H 9S 9C JD KD", "6C 6D", "8C 8D", "5H");
            var actions = r.LegalActions();
            foreach (var a in actions) Assert.IsTrue(r.IsLegal(a), a.ToString());
            Assert.IsTrue(actions.Any(a => a.Kind == ActionKind.Play && a.Cards.Count == 3));
            Assert.AreEqual(4, actions.Count(a => a.Kind == ActionKind.Play && a.TopCard.Rank == Rank.Jack));
            Assert.IsTrue(actions.Contains(TurnAction.Draw));
            Assert.IsFalse(actions.Any(a => a.Kind == ActionKind.Play && a.Cards[0] == C("9S")), "9♠ ne peut pas ouvrir");
        }

        // ---------- Partie complète ----------

        [Test]
        public void Match_EliminatesOnePerRoundUntilWinner()
        {
            var match = new Match(RuleSet.Default, 5, new SystemRandom(42));
            var rng = new SystemRandom(7);
            int rounds = 0;
            while (!match.IsOver)
            {
                match.StartRound();
                rounds++;
                int aliveBefore = match.Alive.Count;
                int guard = 0;
                while (!match.Round.IsOver)
                {
                    var actions = match.Round.LegalActions();
                    match.Apply(actions[rng.Next(actions.Count)]);
                    Assert.Less(++guard, 20000, "partie sans fin");
                }
                Assert.AreEqual(aliveBefore - 1, match.Alive.Count);
            }
            Assert.AreEqual(4, rounds);
            Assert.AreEqual(4, match.Eliminated.Count);
            Assert.IsFalse(match.Eliminated.Contains(match.Winner.Value));
        }

        /// <summary>Parties aléatoires : conservation des 54 cartes, actions légales cohérentes, fin garantie.</summary>
        [Test]
        public void Fuzz_RandomGamesKeepInvariants([Values(2, 3, 4, 6, 8, 10)] int players)
        {
            for (int seed = 0; seed < 40; seed++)
            {
                var match = new Match(RuleSet.Default, players, new SystemRandom(seed * 31 + players));
                var rng = new SystemRandom(seed);
                int steps = 0;
                while (!match.IsOver)
                {
                    match.StartRound();
                    while (!match.Round.IsOver)
                    {
                        var r = match.Round;
                        var actions = r.LegalActions();
                        Assert.IsNotEmpty(actions);
                        var a = actions[rng.Next(actions.Count)];
                        match.Apply(a);
                        int total = r.DrawPileCount + r.Discard.Count;
                        for (int s = 0; s < r.SeatCount; s++) total += r.Hand(s).Count;
                        Assert.AreEqual(54, total, $"graine {seed}, après {a}");
                        Assert.Less(++steps, 100000);
                    }
                }
            }
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var r = Pos("9H KD", "6C 6D", "8C 8D", "5H");
            var copy = r.Clone();
            Play(copy, "9H", check: true);
            Assert.AreEqual(2, r.Hand(0).Count);
            Assert.AreEqual(C("5H"), r.Top);
            Assert.AreEqual(0, r.Current);
        }

        [Test]
        public void RedealHiddenCards_KeepsViewerHandAndCounts()
        {
            var r = new Round(RuleSet.Default, new[] { 0, 1, 2, 3 }, 0, new SystemRandom(3));
            var mine = r.Hand(0).ToList();
            var counts = Enumerable.Range(0, 4).Select(s => r.Hand(s).Count).ToList();
            var copy = r.Clone();
            copy.RedealHiddenCards(0, new SystemRandom(9));
            CollectionAssert.AreEqual(mine, copy.Hand(0));
            for (int s = 0; s < 4; s++) Assert.AreEqual(counts[s], copy.Hand(s).Count);
            Assert.AreEqual(r.DrawPileCount, copy.DrawPileCount);
            Assert.AreEqual(r.Top, copy.Top);
        }

        /// <summary>
        /// Régression : deux positions identiques pour le siège 0 (mains adverses et pioche permutées)
        /// doivent donner la même redistribution à graine égale.
        /// </summary>
        [Test]
        public void RedealHiddenCards_DoesNotDependOnHiddenPlacement()
        {
            var a = Pos("9H KD", "6C 6D", "8C 8D", "5H", "10C 10D 10H 10S");
            var b = Pos("9H KD", "10S 8D", "10C 6C", "5H", "8C 6D 10D 10H");
            a.RedealHiddenCards(0, new SystemRandom(5));
            b.RedealHiddenCards(0, new SystemRandom(5));
            for (int s = 0; s < 3; s++) CollectionAssert.AreEqual(a.Hand(s), b.Hand(s));
        }
    }
}
