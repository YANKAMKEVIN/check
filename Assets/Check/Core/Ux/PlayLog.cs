using System;
using System.Collections.Generic;

namespace Check.Core
{
    public enum LogKind : byte { Round, Play, PlayUnder, Draw, Penalty, Skip, Pass, Check, CheckForgotten, Games, Eliminated }

    /// <summary>Une ligne de l'historique du tour : qui, quoi, avec quelles cartes.</summary>
    public sealed class PlayLogEntry
    {
        public LogKind Kind;
        /// <summary>Joueur (identifiant de partie), -1 pour une ligne de tour.</summary>
        public int Player = -1;
        public IReadOnlyList<Card> Cards = Array.Empty<Card>();
        public int Count;
        public int RoundNumber;

        /// <summary>Pénalité (cartes piochées en punition) : affichée en ambre.</summary>
        public bool IsPenalty => Kind == LogKind.Penalty || Kind == LogKind.CheckForgotten;

        /// <summary>Ligne d'historique d'un événement du moteur, null s'il n'en mérite pas.</summary>
        public static PlayLogEntry From(GameEvent e, int player, int roundNumber)
        {
            if (e == null) return null;
            LogKind kind;
            switch (e.Kind)
            {
                case GameEventKind.RoundStarted: kind = LogKind.Round; player = -1; break;
                case GameEventKind.Played: kind = LogKind.Play; break;
                case GameEventKind.PlayedUnder: kind = LogKind.PlayUnder; break;
                case GameEventKind.Drew: kind = LogKind.Draw; break;
                case GameEventKind.PenaltyDrawn: kind = LogKind.Penalty; break;
                case GameEventKind.Skipped: kind = LogKind.Skip; break;
                case GameEventKind.Passed: kind = LogKind.Pass; break;
                case GameEventKind.CheckDeclared: kind = LogKind.Check; break;
                case GameEventKind.CheckForgotten: kind = LogKind.CheckForgotten; break;
                case GameEventKind.Finished: kind = LogKind.Games; break;
                case GameEventKind.Eliminated: kind = LogKind.Eliminated; break;
                default: return null;
            }
            bool showCards = kind == LogKind.Play || kind == LogKind.PlayUnder;
            return new PlayLogEntry
            {
                Kind = kind,
                Player = player,
                Cards = showCards ? e.Cards : Array.Empty<Card>(),
                Count = e.Count,
                RoundNumber = roundNumber,
            };
        }

        /// <summary>Ce qui suit le nom (sans les cartes) : « pioche », « +4 », « Check oublié +2 »…</summary>
        public string Action => Kind switch
        {
            LogKind.Round => $"Tour {RoundNumber}",
            LogKind.Play => Cards.Count > 1 ? $"pose {Cards.Count} cartes" : "pose",
            LogKind.PlayUnder => "glisse sous le tas",
            LogKind.Draw => "pioche",
            LogKind.Penalty => $"+{Count} cartes",
            LogKind.Skip => "saute son tour",
            LogKind.Pass => "passe",
            LogKind.Check => "Check !",
            LogKind.CheckForgotten => $"Check oublié : +{Count}",
            LogKind.Games => "Games !",
            LogKind.Eliminated => "éliminé",
            _ => "",
        };
    }
}
