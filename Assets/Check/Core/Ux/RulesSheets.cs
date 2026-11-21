using System.Collections.Generic;

namespace Check.Core
{
    /// <summary>Une fiche de l'écran Règles : titre d'onglet, texte (enrichi uGUI) et cartes qui l'illustrent.</summary>
    public sealed class RulesSheet
    {
        public readonly string Title;
        public readonly string Body;
        public readonly IReadOnlyList<Card> Cards;

        public RulesSheet(string title, string body, params Card[] cards)
        {
            Title = title;
            Body = body;
            Cards = cards;
        }
    }

    /// <summary>Les règles découpées en fiches courtes (au lieu d'un seul bloc défilant).</summary>
    public static class RulesSheets
    {
        public static readonly IReadOnlyList<RulesSheet> All = new[]
        {
            new RulesSheet("But",
                "<b><color=#FFCC5C>But</color></b>\nÊtre le premier à se débarrasser de toutes ses cartes. 54 cartes (52 + 2 jokers), de 2 à 10 joueurs ; " +
                "7 cartes chacun à 2-3 joueurs, 6 à 4-5, 5 à 6-7, 4 à 8-10.",
                Card.Parse("RS"), Card.Parse("DH"), Card.Parse("10D"), Card.Parse("5C")),
            new RulesSheet("À ton tour",
                "<b><color=#FFCC5C>À ton tour</color></b>\nPose une carte de la <b>même couleur</b> (♠ ♣ <color=#FF5C73>♥ ♦</color>) ou de la <b>même valeur</b> que la carte du dessus. " +
                "Tu peux poser d'un coup plusieurs cartes de même valeur (doubler, tripler) : la dernière posée devient le dessus. " +
                "Sinon, pioche une carte : c'est alors au joueur suivant (la carte piochée se pose à ton prochain tour).",
                Card.Parse("9H"), Card.Parse("4H"), Card.Parse("4S")),
            new RulesSheet("Cartes spéciales",
                "<b><color=#FFCC5C>Cartes spéciales</color></b>\n" +
                "<b>As</b> : le suivant passe son tour (deux As = deux joueurs sautés).\n" +
                "<b>7</b> : le suivant pioche 2 cartes, sauf s'il contre avec un 7 (le cumul passe au suivant, +2 par 7).\n" +
                "<b>Joker</b> : le suivant pioche 4 cartes. Le noir se pose sur du noir, le rouge sur du rouge ; il ne se contre pas.\n" +
                "<b>Valet</b> : tu demandes une couleur ; le suivant doit la jouer (ou poser un Valet).\n" +
                "<b>2</b> : passe-partout, se glisse sous le tas ; le dessus ne change pas. Il ne contre pas une pénalité.",
                Card.Parse("AS"), Card.Parse("7H"), Card.RedJoker, Card.Parse("VC"), Card.Parse("2D")),
            new RulesSheet("Check et Games",
                "<b><color=#FFCC5C>« Check ! » et « Games ! »</color></b>\nQuand ta pose te laisse une seule carte, touche le bouton <b>Check !</b> avant de poser. " +
                "Oublié ? Tu pioches des cartes de pénalité. Ta dernière carte posée : « Games ! », tu es qualifié.",
                Card.Parse("8C")),
            new RulesSheet("Éliminations",
                "<b><color=#FFCC5C>Éliminations</color></b>\nLe dernier joueur à avoir encore des cartes est éliminé. On redistribue entre les autres, " +
                "jusqu'à ce qu'il ne reste qu'un joueur : le vainqueur. À deux, le premier qui finit gagne."),
        };
    }
}
