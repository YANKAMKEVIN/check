using System;

namespace Check.Core
{
    /// <summary>Situation qui mérite une explication la première fois qu'elle arrive en partie.</summary>
    public enum TipId { Ace, Seven, SevenCounter, Joker, Jack, Two, SayCheck }

    /// <summary>
    /// Bulles d'apprentissage de la première partie : chaque situation (As, 7 et son contre, Joker, Valet,
    /// 2 passe-partout, moment de dire « Check ») est expliquée une seule fois, au moment où elle arrive.
    /// Les situations déjà vues sont gardées dans un masque de bits (sauvegardé avec les préférences).
    /// </summary>
    public static class FirstGameTips
    {
        public static readonly TipId[] All = (TipId[])Enum.GetValues(typeof(TipId));

        /// <summary>Masque où toutes les situations sont vues.</summary>
        public static int AllSeenMask => (1 << All.Length) - 1;

        public static bool IsSeen(int mask, TipId id) => (mask & (1 << (int)id)) != 0;

        public static int MarkSeen(int mask, TipId id) => mask | (1 << (int)id);

        public static bool AllSeen(int mask) => (mask & AllSeenMask) == AllSeenMask;

        /// <summary>Situation déclenchée par une carte posée (ou glissée sous le tas), null sinon.</summary>
        public static TipId? ForEvent(GameEvent e)
        {
            if (e == null) return null;
            switch (e.Kind)
            {
                case GameEventKind.PlayedUnder: return TipId.Two;
                case GameEventKind.SuitRequested: return TipId.Jack;
                case GameEventKind.Played:
                    if (e.Cards.Count == 0) return null;
                    switch (e.Cards[0].Rank)
                    {
                        case Rank.Ace: return TipId.Ace;
                        case Rank.Seven: return TipId.Seven;
                        case Rank.Joker: return TipId.Joker;
                    }
                    return null;
            }
            return null;
        }

        /// <summary>
        /// Le joueur fait face à un cumul de 7 et tient de quoi le contrer (un 7, ou le joker de la couleur
        /// du 7 si la règle l'autorise) : c'est le moment d'expliquer le contre.
        /// </summary>
        public static bool CanCounterSeven(Round round, int seat)
        {
            if (round == null || seat < 0 || round.IsOver || round.Current != seat) return false;
            if (round.PendingDraw <= 0 || round.PendingKind != PenaltyKind.Seven) return false;
            foreach (var c in round.Hand(seat))
                if (round.CanStartWith(c)) return true;
            return false;
        }

        /// <summary>Texte court de la bulle (texte enrichi uGUI).</summary>
        public static string Text(TipId id, RuleSet rules)
        {
            rules ??= RuleSet.Default;
            switch (id)
            {
                case TipId.Ace: return "<b>As</b> : le joueur suivant passe son tour.";
                case TipId.Seven: return $"<b>7</b> : le suivant pioche {rules.SevenDraw} cartes, sauf s'il contre avec un autre 7.";
                case TipId.SevenCounter:
                    return rules.JokerCountersSeven
                        ? "Tu peux <b>contrer</b> : pose un 7 (ou le joker de sa couleur) et le cumul passe au suivant."
                        : "Tu peux <b>contrer</b> : pose un 7 et le cumul passe au suivant.";
                case TipId.Joker: return $"<b>Joker</b> : le suivant pioche {rules.JokerDraw} cartes. Il ne se pose que sur sa couleur (noir ou rouge).";
                case TipId.Jack: return "<b>Valet</b> : il demande une couleur ; le suivant doit la jouer (ou poser un Valet).";
                case TipId.Two: return "<b>2</b> : passe-partout, il se glisse sous le tas. Le dessus ne change pas.";
                case TipId.SayCheck: return "Il te restera une carte : touche <b>CHECK !</b> avant de poser, sinon pénalité.";
            }
            return "";
        }
    }
}
