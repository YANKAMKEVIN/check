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

        // ---------- Carnet du joueur ----------

        [Test]
        public void Stats_CountOnlyTheHumanAndTrackStreaks()
        {
            var st = new PlayerStats();
            Assert.IsFalse(st.Record(new GameEvent { Kind = GameEventKind.CheckForgotten, Count = 2 }, false, 1, 4));
            Assert.IsTrue(st.Record(new GameEvent { Kind = GameEventKind.CheckForgotten, Count = 2 }, true, 1, 4));
            st.Record(new GameEvent { Kind = GameEventKind.CheckDeclared }, true, 1, 4);
            st.Record(new GameEvent { Kind = GameEventKind.PenaltyDrawn, Count = 6 }, true, 1, 4);
            Assert.IsFalse(st.Record(new GameEvent { Kind = GameEventKind.PenaltyDrawn, Count = 2 }, true, 1, 4));
            st.Record(new GameEvent { Kind = GameEventKind.MatchWon }, true, 1, 4);
            st.Record(new GameEvent { Kind = GameEventKind.MatchWon }, true, 2, 6);
            st.Record(new GameEvent { Kind = GameEventKind.Eliminated }, true, 2, 6);
            st.Record(new GameEvent { Kind = GameEventKind.MatchWon }, true, 3, 10);
            Assert.AreEqual(1, st.ChecksForgotten);
            Assert.AreEqual(1, st.ChecksSaid);
            Assert.AreEqual(6, st.BiggestPenalty);
            Assert.AreEqual(4, st.Played);
            Assert.AreEqual(3, st.Won);
            Assert.AreEqual(2, st.PlayedByLevel[2]);
            Assert.AreEqual(1, st.WonByLevel[2]);
            Assert.AreEqual(2, st.PlayedByPlayers[6]);
            Assert.AreEqual(1, st.WonByPlayers[10]);
            Assert.AreEqual(2, st.BestStreak);
            Assert.AreEqual(1, st.CurrentStreak);
        }

        [Test]
        public void Stats_DescribeAndRepairOldData()
        {
            Assert.AreEqual("aucune partie", PlayerStats.Describe(0, 0));
            Assert.AreEqual("1 partie · 0 victoire (0 %)", PlayerStats.Describe(1, 0));
            Assert.AreEqual("3 parties · 2 victoires (67 %)", PlayerStats.Describe(3, 2));
            var old = new PlayerStats { PlayedByLevel = new[] { 2 }, WonByPlayers = null }.Normalized();
            Assert.AreEqual(PlayerStats.Levels, old.PlayedByLevel.Length);
            Assert.AreEqual(2, old.PlayedByLevel[0]);
            Assert.AreEqual(RuleSet.MaxPlayers + 1, old.WonByPlayers.Length);
        }

        // ---------- Appui long : effet d'une carte ----------

        [Test]
        public void CardHelp_DescribesEachKindOfCard()
        {
            StringAssert.StartsWith("As :", CardHelp.Effect(C("AH")));
            StringAssert.Contains("pioche 2", CardHelp.Effect(C("7S")));
            StringAssert.Contains("joker", CardHelp.Effect(C("7S")));
            StringAssert.DoesNotContain("joker", CardHelp.Effect(C("7S"), new RuleSet { JokerCountersSeven = false }));
            StringAssert.Contains("rouge", CardHelp.Effect(Card.RedJoker));
            StringAssert.Contains("pioche 4", CardHelp.Effect(Card.BlackJoker));
            StringAssert.Contains("n'importe quelle", CardHelp.Effect(C("VD")));
            StringAssert.Contains("carreau", CardHelp.Effect(C("VD"), new RuleSet { JackIsWild = false }));
            StringAssert.Contains("sous le tas", CardHelp.Effect(C("2C")));
            Assert.AreEqual("9♥ : carte simple, se pose sur un cœur ou un 9.", CardHelp.Effect(C("9H")));
        }

        // ---------- Historique du tour ----------

        [Test]
        public void PlayLog_MapsEventsToReadableLines()
        {
            var play = PlayLogEntry.From(Played("8H 8S"), 3, 2);
            Assert.AreEqual(LogKind.Play, play.Kind);
            Assert.AreEqual(3, play.Player);
            Assert.AreEqual(2, play.Cards.Count);
            Assert.AreEqual("pose 2 cartes", play.Action);
            Assert.IsFalse(play.IsPenalty);

            var pen = PlayLogEntry.From(new GameEvent { Kind = GameEventKind.PenaltyDrawn, Count = 4, Cards = Cs("3C 4C 5C 6C") }, 1, 2);
            Assert.IsTrue(pen.IsPenalty);
            Assert.AreEqual(0, pen.Cards.Count, "les cartes piochées restent cachées");
            Assert.AreEqual("+4 cartes", pen.Action);

            var round = PlayLogEntry.From(new GameEvent { Kind = GameEventKind.RoundStarted, Seat = 2, Count = 6 }, 1, 3);
            Assert.AreEqual(-1, round.Player);
            Assert.AreEqual("Tour 3", round.Action);

            Assert.IsTrue(PlayLogEntry.From(new GameEvent { Kind = GameEventKind.CheckForgotten, Count = 2 }, 0, 1).IsPenalty);
            Assert.IsNull(PlayLogEntry.From(new GameEvent { Kind = GameEventKind.TurnStarted }, 0, 1));
            Assert.IsNull(PlayLogEntry.From(new GameEvent { Kind = GameEventKind.PenaltyStacked }, 0, 1));
        }

        // ---------- Prénom du joueur ----------

        [Test]
        public void PlayerName_IsCleanedAndShortened()
        {
            Assert.AreEqual("Toi", PlayerIdentity.Sanitize(null));
            Assert.AreEqual("Toi", PlayerIdentity.Sanitize("   "));
            Assert.AreEqual("Kevin", PlayerIdentity.Sanitize("  Kevin "));
            Assert.AreEqual("Tonton Paul", PlayerIdentity.Sanitize("Tonton    Paul"));
            Assert.AreEqual("bKevin/b", PlayerIdentity.Sanitize("<b>Kevin</b>"));
            Assert.AreEqual(PlayerIdentity.MaxLength, PlayerIdentity.Sanitize("Abcdefghijklmnopqrstuvwxyz").Length);
            Assert.AreEqual("Toi", PlayerIdentity.Sanitize("<>"));
        }

        // ---------- Valet : aide au choix de la couleur ----------

        [Test]
        public void SuitAdvice_CountsWhatRemainsAfterThePlay()
        {
            var hand = Cs("VS 3H 9H KD 4C JkR");
            var counts = SuitAdvice.Counts(hand, Cs("VS"));
            CollectionAssert.AreEqual(new[] { 0, 2, 1, 1 }, counts);
            Assert.AreEqual(Suit.Hearts, SuitAdvice.Best(counts));
            Assert.AreEqual(Suit.Spades, SuitAdvice.Best(new[] { 1, 1, 0, 0 }), "égalité : premier dans l'ordre");
            Assert.IsNull(SuitAdvice.Best(SuitAdvice.Counts(Cs("JkN"), null)));
        }

        // ---------- Qui joue ensuite ----------

        [Test]
        public void TurnOrder_SkipsPlayersOutOfTheRound()
        {
            var outs = new bool[6];
            Assert.AreEqual(2, TurnOrder.Next(1, 6, p => outs[p]));
            outs[2] = outs[3] = true;
            Assert.AreEqual(4, TurnOrder.Next(1, 6, p => outs[p]));
            Assert.AreEqual(0, TurnOrder.Next(5, 6, p => outs[p]));
            outs[0] = outs[4] = outs[5] = true;
            Assert.AreEqual(-1, TurnOrder.Next(1, 6, p => outs[p]));
            Assert.AreEqual(-1, TurnOrder.Next(-1, 6, p => false));
        }

        [Test]
        public void TurnOrder_MatchesTheEngineAfterEliminations()
        {
            // Partie à 5 où le joueur 2 est déjà éliminé : le moteur fait jouer 0, 1, 3, 4 dans cet ordre.
            var round = new Round(RuleSet.Default, new[] { 0, 1, 3, 4 }, 0, new SystemRandom(5));
            for (int k = 0; k < 4; k++)
            {
                int player = round.PlayerId(round.Current);
                int expected = TurnOrder.Next(player, 5, p => p == 2);
                round.Apply(TurnAction.Draw);
                if (round.HasDrawn) round.Apply(TurnAction.Pass);
                Assert.AreEqual(expected, round.PlayerId(round.Current));
            }
        }
    }
}
