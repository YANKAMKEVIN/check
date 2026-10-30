using System;
using System.Collections.Generic;

namespace Check.Core
{
    public enum Suit : byte { Spades, Hearts, Diamonds, Clubs }

    public enum CardColor : byte { Black, Red }

    public enum Rank : byte
    {
        Two = 2, Three, Four, Five, Six, Seven, Eight, Nine, Ten,
        Jack, Queen, King, Ace, Joker
    }

    /// <summary>
    /// Une carte du jeu de 54 : identifiants 0 à 51 pour les cartes classiques (couleur × 13 + rang),
    /// 52 pour le joker noir, 53 pour le joker rouge. Chaque carte est unique dans le paquet.
    /// </summary>
    public readonly struct Card : IEquatable<Card>
    {
        public const int DeckSize = 54;
        public const int BlackJokerId = 52;
        public const int RedJokerId = 53;

        public readonly byte Id;

        public Card(int id)
        {
            if (id < 0 || id >= DeckSize) throw new ArgumentOutOfRangeException(nameof(id));
            Id = (byte)id;
        }

        public Card(Rank rank, Suit suit)
        {
            if (rank == Rank.Joker) throw new ArgumentException("Utiliser Card.BlackJoker / Card.RedJoker");
            Id = (byte)((int)suit * 13 + ((int)rank - 2));
        }

        public static Card BlackJoker => new Card(BlackJokerId);
        public static Card RedJoker => new Card(RedJokerId);

        public bool IsJoker => Id >= BlackJokerId;
        public Rank Rank => IsJoker ? Rank.Joker : (Rank)(Id % 13 + 2);

        /// <summary>Couleur (♠ ♥ ♦ ♣). Sans objet pour un joker : lire <see cref="Color"/>.</summary>
        public Suit Suit => IsJoker ? throw new InvalidOperationException("Un joker n'a pas de couleur de carte") : (Suit)(Id / 13);

        public CardColor Color => Id switch
        {
            BlackJokerId => CardColor.Black,
            RedJokerId => CardColor.Red,
            _ => ColorOf(Suit),
        };

        public static CardColor ColorOf(Suit suit) =>
            suit == Suit.Hearts || suit == Suit.Diamonds ? CardColor.Red : CardColor.Black;

        public static List<Card> FullDeck()
        {
            var deck = new List<Card>(DeckSize);
            for (int i = 0; i < DeckSize; i++) deck.Add(new Card(i));
            return deck;
        }

        public bool Equals(Card other) => Id == other.Id;
        public override bool Equals(object obj) => obj is Card c && c.Id == Id;
        public override int GetHashCode() => Id;
        public static bool operator ==(Card a, Card b) => a.Id == b.Id;
        public static bool operator !=(Card a, Card b) => a.Id != b.Id;

        /// <summary>Notation courte : « 7♥ », « V♠ », « 10♦ », « JkN », « JkR ».</summary>
        public override string ToString()
        {
            if (Id == BlackJokerId) return "JkN";
            if (Id == RedJokerId) return "JkR";
            return RankLabel(Rank) + SuitSymbol(Suit);
        }

        public static string RankLabel(Rank rank) => rank switch
        {
            Rank.Jack => "V",
            Rank.Queen => "D",
            Rank.King => "R",
            Rank.Ace => "A",
            Rank.Joker => "Jk",
            _ => ((int)rank).ToString(),
        };

        public static string SuitSymbol(Suit suit) => suit switch
        {
            Suit.Spades => "♠",
            Suit.Hearts => "♥",
            Suit.Diamonds => "♦",
            _ => "♣",
        };

        /// <summary>Lit la notation de <see cref="ToString"/> (accepte aussi S/H/D/C pour les couleurs).</summary>
        public static Card Parse(string text)
        {
            text = text.Trim();
            if (text == "JkN") return BlackJoker;
            if (text == "JkR") return RedJoker;
            string r = text.Substring(0, text.Length - 1);
            char s = text[text.Length - 1];
            Suit suit = s switch
            {
                '♠' or 'S' => Suit.Spades,
                '♥' or 'H' => Suit.Hearts,
                '♦' or 'D' => Suit.Diamonds,
                '♣' or 'C' => Suit.Clubs,
                _ => throw new FormatException("Couleur inconnue : " + text),
            };
            Rank rank = r switch
            {
                "V" or "J" => Rank.Jack,
                "D" or "Q" => Rank.Queen,
                "R" or "K" => Rank.King,
                "A" => Rank.Ace,
                _ => (Rank)int.Parse(r),
            };
            return new Card(rank, suit);
        }
    }
}
