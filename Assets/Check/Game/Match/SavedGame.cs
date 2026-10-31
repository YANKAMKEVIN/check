using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Check.Core;
using UnityEngine;

namespace Check.Game.Match
{
    /// <summary>
    /// Partie en cours sauvegardée sous forme de « film » : graine du hasard, donneur, et suite des actions
    /// (« R » = nouveau tour). Le moteur étant déterministe pour un hasard donné, rejouer le film
    /// redonne exactement la même position. Stocké dans PlayerPrefs.
    /// </summary>
    [Serializable]
    public sealed class SavedGame
    {
        const string Key = "check.savedgame";
        public const string RoundToken = "R";

        public GameSettings Settings;
        public string[] Names;
        public int Seed;
        public int FirstDealer;
        public List<string> Log = new List<string>();

        public static bool Exists => PlayerPrefs.HasKey(Key);

        public void Store()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
        }

        public static void Flush() => PlayerPrefs.Save();

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        public static SavedGame Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(Key, "");
                if (string.IsNullOrEmpty(json)) return null;
                var g = JsonUtility.FromJson<SavedGame>(json);
                if (g == null || g.Settings == null || g.Names == null || g.Names.Length != g.Settings.PlayerCount) return null;
                return g;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Check] Sauvegarde illisible : " + e.Message);
                return null;
            }
        }

        /// <summary>« P:12,40:2:1 » = pose des cartes 12 puis 40, couleur demandée 2, Check annoncé ; « D » pioche ; « S » passe.</summary>
        public static string Encode(TurnAction a)
        {
            switch (a.Kind)
            {
                case ActionKind.Draw: return "D";
                case ActionKind.Pass: return "S";
            }
            var sb = new StringBuilder("P:");
            sb.Append(string.Join(",", a.Cards.Select(c => c.Id.ToString())));
            sb.Append(':').Append(a.RequestedSuit.HasValue ? ((int)a.RequestedSuit.Value).ToString() : "-");
            sb.Append(':').Append(a.DeclaresCheck ? '1' : '0');
            return sb.ToString();
        }

        public static TurnAction Decode(string s)
        {
            if (s == "D") return TurnAction.Draw;
            if (s == "S") return TurnAction.Pass;
            var parts = s.Split(':');
            if (parts.Length != 4 || parts[0] != "P") throw new FormatException("Action inconnue : " + s);
            var cards = parts[1].Split(',').Select(x => new Card(int.Parse(x))).ToList();
            Suit? suit = parts[2] == "-" ? (Suit?)null : (Suit)int.Parse(parts[2]);
            return TurnAction.Play(cards, suit, parts[3] == "1");
        }
    }
}
