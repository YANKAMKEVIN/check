using System;

namespace Check.Core
{
    /// <summary>
    /// Carnet du joueur : parties jouées et gagnées par niveau d'IA et par nombre de joueurs, et quelques
    /// compteurs propres au Check. Champs publics simples pour la sérialisation JSON (PlayerPrefs côté jeu).
    /// </summary>
    [Serializable]
    public sealed class PlayerStats
    {
        public const int Levels = 4;

        public int[] PlayedByLevel = new int[Levels];
        public int[] WonByLevel = new int[Levels];
        /// <summary>Index = nombre de joueurs à la table (2 à 10).</summary>
        public int[] PlayedByPlayers = new int[RuleSet.MaxPlayers + 1];
        public int[] WonByPlayers = new int[RuleSet.MaxPlayers + 1];
        public int ChecksSaid;
        public int ChecksForgotten;
        public int BiggestPenalty;
        public int GamesCalled;
        public int CurrentStreak;
        public int BestStreak;

        public int Played => Sum(PlayedByLevel);
        public int Won => Sum(WonByLevel);

        static int Sum(int[] a)
        {
            int s = 0;
            if (a != null) foreach (var v in a) s += v;
            return s;
        }

        /// <summary>Remet les tableaux à la bonne taille (fichier ancien ou abîmé).</summary>
        public PlayerStats Normalized()
        {
            PlayedByLevel = Fit(PlayedByLevel, Levels);
            WonByLevel = Fit(WonByLevel, Levels);
            PlayedByPlayers = Fit(PlayedByPlayers, RuleSet.MaxPlayers + 1);
            WonByPlayers = Fit(WonByPlayers, RuleSet.MaxPlayers + 1);
            return this;
        }

        static int[] Fit(int[] a, int n)
        {
            if (a != null && a.Length == n) return a;
            var r = new int[n];
            if (a != null) Array.Copy(a, r, Math.Min(n, a.Length));
            return r;
        }

        /// <summary>
        /// Tient compte d'un événement de la partie. <paramref name="mine"/> : il concerne le joueur humain.
        /// La partie compte comme perdue dès que le joueur est éliminé, gagnée quand il remporte la partie.
        /// Renvoie vrai si le carnet a changé.
        /// </summary>
        public bool Record(GameEvent e, bool mine, int level, int players)
        {
            if (e == null || !mine) return false;
            switch (e.Kind)
            {
                case GameEventKind.CheckDeclared: ChecksSaid++; return true;
                case GameEventKind.CheckForgotten: ChecksForgotten++; return true;
                case GameEventKind.Finished: GamesCalled++; return true;
                case GameEventKind.PenaltyDrawn:
                    if (e.Count <= BiggestPenalty) return false;
                    BiggestPenalty = e.Count;
                    return true;
                case GameEventKind.Eliminated: RecordMatch(level, players, false); return true;
                case GameEventKind.MatchWon: RecordMatch(level, players, true); return true;
            }
            return false;
        }

        public void RecordMatch(int level, int players, bool won)
        {
            Normalized();
            level = Math.Max(0, Math.Min(Levels - 1, level));
            players = Math.Max(RuleSet.MinPlayers, Math.Min(RuleSet.MaxPlayers, players));
            PlayedByLevel[level]++;
            PlayedByPlayers[players]++;
            if (won)
            {
                WonByLevel[level]++;
                WonByPlayers[players]++;
                CurrentStreak++;
                BestStreak = Math.Max(BestStreak, CurrentStreak);
            }
            else CurrentStreak = 0;
        }

        /// <summary>« 3 parties · 1 victoire (33 %) », « aucune partie ».</summary>
        public static string Describe(int played, int won)
        {
            if (played <= 0) return "aucune partie";
            int pct = (int)Math.Round(100.0 * won / played);
            return $"{played} partie{(played > 1 ? "s" : "")} · {won} victoire{(won > 1 ? "s" : "")} ({pct} %)";
        }
    }
}
