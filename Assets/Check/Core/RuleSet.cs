namespace Check.Core
{
    /// <summary>
    /// Réglages des règles. Les valeurs par défaut suivent la version de Kevin (docs/regles.md) ;
    /// celles marquées « à valider » dans la fiche sont des choix faits faute de précision.
    /// </summary>
    public sealed class RuleSet
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 10;

        /// <summary>Cartes piochées par 7 posé.</summary>
        public int SevenDraw = 2;

        /// <summary>Cartes piochées par joker posé.</summary>
        public int JokerDraw = 4;

        /// <summary>Un joker de la couleur du 7 du dessus contre (et alourdit) un cumul de 7.</summary>
        public bool JokerCountersSeven = true;

        /// <summary>Le Valet se pose sur n'importe quelle carte (sinon il suit la règle couleur / valeur).</summary>
        public bool JackIsWild = true;

        /// <summary>Poser plusieurs cartes de même valeur en une fois.</summary>
        public bool AllowMultiples = true;

        /// <summary>Après avoir pioché, on peut poser la carte piochée si elle est jouable.</summary>
        public bool MayPlayDrawnCard = true;

        /// <summary>On peut piocher même si on a une carte jouable.</summary>
        public bool MayDrawVoluntarily = true;

        /// <summary>Cartes de pénalité pour un « Check » oublié (0 = pas de pénalité).</summary>
        public int ForgotCheckPenalty = 2;

        /// <summary>Une carte spéciale retournée en début de tour est remise dans la pioche.</summary>
        public bool RedrawSpecialStartCard = true;

        public static RuleSet Default => new RuleSet();

        /// <summary>Cartes distribuées selon le nombre de joueurs encore en jeu.</summary>
        public int CardsToDeal(int players) => players switch
        {
            <= 3 => 7,
            <= 5 => 6,
            <= 7 => 5,
            _ => 4,
        };

        public static bool IsSpecial(Rank rank) =>
            rank == Rank.Two || rank == Rank.Seven || rank == Rank.Jack || rank == Rank.Ace || rank == Rank.Joker;

        public RuleSet Clone() => (RuleSet)MemberwiseClone();
    }
}
