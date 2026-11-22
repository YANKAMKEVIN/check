using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Check.Core
{
    /// <summary>
    /// « Film » d'une partie sauvegardée : suite de jetons (« R » = nouveau tour, une action par coup) qui,
    /// rejouée sur un <see cref="Match"/> neuf de même graine, redonne exactement la même position.
    /// Logique pure, testée hors Unity ; le stockage (PlayerPrefs) reste dans <c>Game/Match/SavedGame</c>.
    /// </summary>
    public static class SaveReplay
    {
        /// <summary>
        /// Version du film. À incrémenter à chaque changement de <see cref="Round.Apply"/>, du tirage ou des
        /// règles par défaut qui ferait rejouer une ancienne sauvegarde autrement (ou pas du tout).
        /// 0 = sauvegardes d'avant le champ (avant la règle « piocher termine le tour », 21/11/2026) ; 1 = actuelle.
        /// </summary>
        public const int CurrentFormat = 1;

        public const string RoundToken = "R";

        /// <summary>« P:12,40:2:1 » = pose des cartes 12 puis 40, couleur demandée 2, Check annoncé ; « D » pioche ; « S » passe.</summary>
        public static string Encode(TurnAction a)
        {
            switch (a.Kind)
            {
                case ActionKind.Draw: return "D";
                case ActionKind.Pass: return "S";
            }
            var sb = new StringBuilder("P:");
            sb.Append(string.Join(",", a.Cards.Select(c => c.Id.ToString(CultureInfo.InvariantCulture))));
            sb.Append(':').Append(a.RequestedSuit.HasValue ? ((int)a.RequestedSuit.Value).ToString(CultureInfo.InvariantCulture) : "-");
            sb.Append(':').Append(a.DeclaresCheck ? '1' : '0');
            return sb.ToString();
        }

        /// <summary>Lit un jeton d'action ; lève <see cref="FormatException"/> s'il est illisible.</summary>
        public static TurnAction Decode(string s)
        {
            if (s == "D") return TurnAction.Draw;
            if (s == "S") return TurnAction.Pass;
            var parts = (s ?? "").Split(':');
            if (parts.Length != 4 || parts[0] != "P" || parts[1].Length == 0 || (parts[3] != "0" && parts[3] != "1"))
                throw new FormatException("Action inconnue : " + s);
            var cards = new List<Card>();
            foreach (var x in parts[1].Split(','))
            {
                if (!int.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id >= Card.DeckSize)
                    throw new FormatException("Carte inconnue : " + s);
                cards.Add(new Card(id));
            }
            Suit? suit = null;
            if (parts[2] != "-")
            {
                if (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int su) || su > 3)
                    throw new FormatException("Couleur inconnue : " + s);
                suit = (Suit)su;
            }
            return TurnAction.Play(cards, suit, parts[3] == "1");
        }

        /// <summary>
        /// Rejoue le film sur <paramref name="match"/> (neuf, construit avec la graine et le donneur sauvegardés).
        /// Faux, avec une raison lisible, si la version diffère, si un jeton est illisible ou illégal, ou si la
        /// partie est déjà finie ; ne lève pas d'exception. En cas d'échec, <paramref name="match"/> est inutilisable.
        /// </summary>
        public static bool TryReplay(Match match, int format, IReadOnlyList<string> log, out string reason)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (format != CurrentFormat)
            {
                reason = $"sauvegarde d'une autre version (format {format}, attendu {CurrentFormat})";
                return false;
            }
            if (log == null || log.Count == 0 || log[0] != RoundToken)
            {
                reason = "film vide ou sans début de tour";
                return false;
            }
            for (int i = 0; i < log.Count; i++)
            {
                string token = log[i];
                try
                {
                    if (token == RoundToken)
                    {
                        if (match.IsOver || (match.Round != null && !match.Round.IsOver))
                        {
                            reason = $"nouveau tour inattendu au coup {i + 1}";
                            return false;
                        }
                        match.StartRound();
                        continue;
                    }
                    var action = Decode(token);
                    if (match.Round == null || match.Round.IsOver)
                    {
                        reason = $"action hors tour au coup {i + 1}";
                        return false;
                    }
                    if (!match.Round.IsLegal(action, out var why))
                    {
                        reason = $"action illégale au coup {i + 1} ({action}) : {why}";
                        return false;
                    }
                    match.Apply(action);
                }
                catch (Exception e) when (e is FormatException || e is InvalidOperationException || e is ArgumentException)
                {
                    reason = $"coup {i + 1} illisible : {e.Message}";
                    return false;
                }
            }
            if (match.IsOver || match.Round == null)
            {
                reason = "partie déjà finie";
                return false;
            }
            reason = null;
            return true;
        }
    }
}
