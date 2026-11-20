using System;
using System.Collections.Generic;
using Check.Core;
using UnityEngine;

namespace Check.Game.Match
{
    /// <summary>Niveau des adversaires, transmis tel quel au cerveau (Check.AI).</summary>
    public enum OpponentLevel { Easy, Medium, Hard, Expert }

    /// <summary>Rythme des adversaires (délai de réflexion et vitesse des animations).</summary>
    public enum AiSpeed { Normal, Fast }

    /// <summary>
    /// Préférences d'une partie : nombre de joueurs, niveau, vitesse et règles réglables.
    /// Sauvegardées dans PlayerPrefs (JSON).
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        const string Key = "check.settings";

        public int PlayerCount = 4;
        public OpponentLevel Level = OpponentLevel.Medium;
        public AiSpeed Speed = AiSpeed.Normal;
        /// <summary>« Check » oublié : 2 cartes de pénalité (sinon aucune).</summary>
        public bool ForgotCheckPenalty = true;
        /// <summary>Le Valet se pose sur n'importe quelle carte.</summary>
        public bool JackIsWild = true;
        /// <summary>Le joker de la couleur du 7 contre (et alourdit) un cumul de 7.</summary>
        public bool JokerCountersSeven = true;
        public bool Haptics = true;
        /// <summary>Bulles d'apprentissage déjà vues (masque de <see cref="TipId"/>).</summary>
        public int TipsSeen;

        public RuleSet ToRuleSet()
        {
            var r = RuleSet.Default;
            r.ForgotCheckPenalty = ForgotCheckPenalty ? 2 : 0;
            r.JackIsWild = JackIsWild;
            r.JokerCountersSeven = JokerCountersSeven;
            return r;
        }

        public GameSettings Clone() => (GameSettings)MemberwiseClone();

        public static GameSettings Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var s = JsonUtility.FromJson<GameSettings>(json);
                    if (s != null)
                    {
                        s.PlayerCount = Mathf.Clamp(s.PlayerCount, RuleSet.MinPlayers, RuleSet.MaxPlayers);
                        return s;
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[Check] Préférences illisibles : " + e.Message); }
            return new GameSettings();
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public static string LevelName(OpponentLevel level) => level switch
        {
            OpponentLevel.Easy => "Facile",
            OpponentLevel.Medium => "Moyen",
            OpponentLevel.Hard => "Difficile",
            _ => "Expert",
        };
    }

    /// <summary>Prénoms et surnoms camerounais pour les adversaires.</summary>
    public static class OpponentNames
    {
        public static readonly string[] All =
        {
            "Mamadou", "Tonton Roger", "Mami Ngono", "Petit Jojo", "Kamga", "Fotso", "Essomba", "Mbarga",
            "Aïssatou", "Tata Odile", "Bouba", "Chantal", "Njoya", "Abena", "Le Mbenguiste", "Ebénézer",
            "Nadège", "Grand Frère", "Manga", "Fadimatou", "Ndzana", "Mama Thérèse", "Atangana", "Yaya",
            "Ngo Bilong", "Djibril", "Tchoupo", "Mola Bertrand",
        };

        static readonly HashSet<string> Female = new HashSet<string>
        {
            "Mami Ngono", "Aïssatou", "Tata Odile", "Chantal", "Abena", "Nadège", "Fadimatou", "Mama Thérèse", "Ngo Bilong",
        };

        /// <summary>Prénom féminin : l'adversaire est alors représenté par une voyageuse.</summary>
        public static bool IsFemale(string name) => name != null && Female.Contains(name);

        /// <summary>Tire <paramref name="count"/> noms distincts.</summary>
        public static string[] Pick(int count, System.Random rng)
        {
            var pool = new List<string>(All);
            var result = new string[count];
            for (int i = 0; i < count; i++)
            {
                int k = rng.Next(pool.Count);
                result[i] = pool[k];
                pool.RemoveAt(k);
            }
            return result;
        }
    }
}
