using System.Text;
using Check.Core;
using Check.Game.Audio;
using Check.Game.Match;
using UnityEngine;
using UnityEngine.UI;

namespace Check.Game.UI
{
    /// <summary>Écran « Mon carnet » : résultats gardés d'une partie à l'autre (<see cref="PlayerStats"/>).</summary>
    public sealed partial class Hud
    {
        GameObject statsOverlay;
        Text statsLevels, statsPlayers, statsCounters, statsHeadline;

        void BuildStats()
        {
            statsOverlay = Overlay("Stats", out var card, 1300, 860, UIKit.Gold, 0.6f);
            var title = UIKit.Display(UIKit.Label("Title", card, "Mon carnet", 60, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold));
            UIKit.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -26), new Vector2(900, 80));
            statsHeadline = UIKit.Label("Headline", card, "", 32, UIKit.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Anchor(statsHeadline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -108), new Vector2(1100, 50));

            statsLevels = Column(card, 0f, 0.5f, "Par niveau d'adversaires");
            statsPlayers = Column(card, 0.5f, 1f, "Par nombre de joueurs");
            statsCounters = UIKit.Label("Counters", card, "", 28, UIKit.Text, TextAnchor.MiddleCenter);
            statsCounters.supportRichText = true;
            UIKit.Anchor(statsCounters.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 136), new Vector2(1180, 110));

            var close = UIKit.Button("Close", card, "Fermer", () => { sfx.Play(Sfx.Click); ShowHome(); }, UIKit.Gold, 40);
            UIKit.Anchor((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(380, 92));
        }

        static Text Column(RectTransform card, float x0, float x1, string heading)
        {
            var col = UIKit.Panel("Column", card, new Color(0.03f, 0.03f, 0.08f, 0.8f), true, UIKit.Indigo * 2f);
            var rt = col.rectTransform;
            rt.anchorMin = new Vector2(x0, 0f);
            rt.anchorMax = new Vector2(x1, 1f);
            rt.offsetMin = new Vector2(x0 == 0f ? 44 : 12, 262);
            rt.offsetMax = new Vector2(x1 == 1f ? -44 : -12, -170);
            var h = UIKit.Label("Heading", rt, heading, 30, UIKit.Gold, TextAnchor.UpperCenter, FontStyle.Bold);
            UIKit.Stretch(h.rectTransform, 16, 0, 16, 16);
            var body = UIKit.Label("Body", rt, "", 27, UIKit.Text, TextAnchor.UpperLeft);
            body.supportRichText = true;
            body.lineSpacing = 1.12f;
            UIKit.Stretch(body.rectTransform, 28, 16, 20, 70);
            return body;
        }

        void ShowStats()
        {
            var st = game.Stats;
            statsHeadline.text = st.Played == 0 ? "Termine une partie pour remplir ton carnet." : "Toutes parties : " + PlayerStats.Describe(st.Played, st.Won);

            var sb = new StringBuilder();
            for (int i = 0; i < PlayerStats.Levels; i++)
                sb.Append($"<b>{GameSettings.LevelName((OpponentLevel)i)}</b> : {PlayerStats.Describe(st.PlayedByLevel[i], st.WonByLevel[i])}\n");
            statsLevels.text = sb.ToString();

            sb.Clear();
            for (int n = RuleSet.MinPlayers; n <= RuleSet.MaxPlayers; n++)
                if (st.PlayedByPlayers[n] > 0)
                    sb.Append($"<b>{n} joueurs</b> : {PlayerStats.Describe(st.PlayedByPlayers[n], st.WonByPlayers[n])}\n");
            statsPlayers.text = sb.Length > 0 ? sb.ToString() : "<color=#9EADD1>Pas encore de partie terminée.</color>";

            statsCounters.text =
                $"Meilleure série : <b>{st.BestStreak}</b>   ·   Série en cours : <b>{st.CurrentStreak}</b>   ·   « Games » : <b>{st.GamesCalled}</b>\n" +
                $"Checks annoncés : <b>{st.ChecksSaid}</b>   ·   Checks oubliés : <b><color=#FFA22E>{st.ChecksForgotten}</color></b>   ·   " +
                $"Plus grosse pénalité : <b><color=#FFA22E>{(st.BiggestPenalty > 0 ? "+" + st.BiggestPenalty : "—")}</color></b>";
            HideAll();
            Show(statsOverlay, true);
        }
    }
}
