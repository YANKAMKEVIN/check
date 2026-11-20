namespace Check.Core
{
    /// <summary>Rappel en une ligne de ce que fait une carte (appui long sur une carte de la main ou sur le tas).</summary>
    public static class CardHelp
    {
        public static string SuitName(Suit s) => s switch
        {
            Suit.Spades => "pique",
            Suit.Hearts => "cœur",
            Suit.Diamonds => "carreau",
            _ => "trèfle",
        };

        public static string Effect(Card card, RuleSet rules = null)
        {
            rules ??= RuleSet.Default;
            if (card.IsJoker)
            {
                string color = card.Color == CardColor.Red ? "rouge" : "noir";
                string on = card.Color == CardColor.Red ? "rouge (♥ ♦)" : "noire (♠ ♣)";
                return $"Joker {color} : se pose sur une carte {on} ; le suivant pioche {rules.JokerDraw} cartes, sans contre possible.";
            }
            string label = Card.RankLabel(card.Rank);
            switch (card.Rank)
            {
                case Rank.Ace: return "As : le joueur suivant passe son tour (deux As = deux joueurs sautés).";
                case Rank.Seven:
                    return $"7 : le suivant pioche {rules.SevenDraw} cartes, sauf s'il contre avec un 7" +
                           (rules.JokerCountersSeven ? " ou le joker de sa couleur" : "") + " (le cumul passe au suivant).";
                case Rank.Jack:
                    return rules.JackIsWild
                        ? "Valet : se pose sur n'importe quelle carte et demande une couleur au suivant."
                        : $"Valet : se pose sur un {SuitName(card.Suit)} ou un Valet, puis demande une couleur au suivant.";
                case Rank.Two: return "2 : passe-partout, se glisse sous le tas sans changer le dessus. Il ne contre pas une pénalité.";
            }
            return $"{label}{Card.SuitSymbol(card.Suit)} : carte simple, se pose sur un {SuitName(card.Suit)} ou un {label}.";
        }
    }
}
