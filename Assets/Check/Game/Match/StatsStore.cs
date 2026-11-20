using System;
using Check.Core;
using UnityEngine;

namespace Check.Game.Match
{
    /// <summary>Carnet du joueur (<see cref="PlayerStats"/>) gardé dans PlayerPrefs (JSON).</summary>
    public static class StatsStore
    {
        const string Key = "check.stats";

        public static PlayerStats Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var s = JsonUtility.FromJson<PlayerStats>(json);
                    if (s != null) return s.Normalized();
                }
            }
            catch (Exception e) { Debug.LogWarning("[Check] Carnet illisible : " + e.Message); }
            return new PlayerStats();
        }

        public static void Save(PlayerStats stats)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(stats));
            PlayerPrefs.Save();
        }
    }
}
