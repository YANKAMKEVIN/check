namespace Check.AI
{
    public enum AILevel { Facile = 0, Moyen = 1, Difficile = 2, Expert = 3 }

    /// <summary>
    /// Réglages d'un niveau. Les valeurs par défaut viennent de <see cref="For"/> ; les champs peuvent être
    /// modifiés (tests, équilibrage) avant de passer l'objet à <see cref="CheckAI"/>.
    /// </summary>
    public sealed class AISettings
    {
        public AILevel Level;
        public string DisplayName;

        /// <summary>Probabilité d'oublier d'annoncer « Check » en posant l'avant-dernière carte (0 = jamais).</summary>
        public double ForgetCheckChance;

        /// <summary>Choix naïf (première carte jouable) au lieu des heuristiques.</summary>
        public bool Naive;

        /// <summary>Probabilité, pour le niveau naïf, de poser une carte jouable au hasard plutôt que la première.</summary>
        public double NaiveRandomChance;

        /// <summary>Recherche Monte-Carlo par détermination (sinon : heuristiques seules).</summary>
        public bool UseSearch;

        /// <summary>Temps max d'une décision avec recherche, en millisecondes (0 = pas de limite de temps).</summary>
        public int TimeBudgetMs;

        /// <summary>Nombre max de simulations (fins de tour jouées) par décision (0 = pas de limite ; il faut alors un temps max).</summary>
        public int MaxIterations;

        /// <summary>Nombre max d'actions candidates étudiées à la racine (les meilleures selon les heuristiques).</summary>
        public int MaxCandidates;

        /// <summary>Probabilité, dans une simulation, qu'un joueur joue au hasard au lieu de suivre les heuristiques.</summary>
        public double RolloutRandomness;

        /// <summary>
        /// Bonus (en valeur de simulation, 0 à 1) donné à l'action préférée des heuristiques au moment du choix final :
        /// départage les actions que les simulations ne distinguent pas.
        /// </summary>
        public double HeuristicBias;

        /// <summary>Écart (en erreurs types de la différence appariée) au-delà duquel un candidat est abandonné.</summary>
        public double PruneZ = 2.5;

        /// <summary>Coups max simulés avant d'évaluer la position (garde-fou contre les tours interminables).</summary>
        public int MaxRolloutTurns = 300;

        public AISettings Clone() => (AISettings)MemberwiseClone();

        public static AISettings For(AILevel level)
        {
            switch (level)
            {
                case AILevel.Facile:
                    return new AISettings
                    {
                        Level = level, DisplayName = "Facile", Naive = true, NaiveRandomChance = 0.25,
                        ForgetCheckChance = 0.25,
                    };
                case AILevel.Moyen:
                    return new AISettings { Level = level, DisplayName = "Moyen", ForgetCheckChance = 0.05 };
                case AILevel.Difficile:
                    return new AISettings
                    {
                        Level = level, DisplayName = "Difficile", UseSearch = true, TimeBudgetMs = 150,
                        MaxIterations = 4000, MaxCandidates = 6, RolloutRandomness = 0.1, HeuristicBias = 0.02,
                    };
                default:
                    return new AISettings
                    {
                        Level = AILevel.Expert, DisplayName = "Expert", UseSearch = true, TimeBudgetMs = 350,
                        MaxIterations = 12000, MaxCandidates = 10, RolloutRandomness = 0.03, HeuristicBias = 0.02,
                    };
            }
        }
    }
}
