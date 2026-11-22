using System;
using System.Collections.Generic;
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
        public const string RoundToken = SaveReplay.RoundToken;

        /// <summary>Version du film (<see cref="SaveReplay.CurrentFormat"/>) ; 0 pour les sauvegardes d'avant ce champ.</summary>
        public int Format;
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

        /// <summary>Voir <see cref="SaveReplay.Encode"/>.</summary>
        public static string Encode(TurnAction a) => SaveReplay.Encode(a);

        /// <summary>Voir <see cref="SaveReplay.Decode"/>.</summary>
        public static TurnAction Decode(string s) => SaveReplay.Decode(s);
    }
}
