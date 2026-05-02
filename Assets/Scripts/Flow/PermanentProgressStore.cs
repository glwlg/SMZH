using System;
using System.Collections.Generic;
using UnityEngine;

namespace XTD.Flow
{
    [Serializable]
    public sealed class PermanentProgressData
    {
        public int totalRuns;
        public int completedRuns;
        public int totalHeroExperience;
        public List<string> permanentArtifactIds = new();

        public PermanentProgressData Clone()
        {
            return new PermanentProgressData
            {
                totalRuns = totalRuns,
                completedRuns = completedRuns,
                totalHeroExperience = totalHeroExperience,
                permanentArtifactIds = new List<string>(permanentArtifactIds)
            };
        }
    }

    public static class PermanentProgressStore
    {
        private const string PlayerPrefsKey = "XTD.PermanentProgress.v1";

        public static PermanentProgressData Load()
        {
            var json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new PermanentProgressData();
            }

            try
            {
                return JsonUtility.FromJson<PermanentProgressData>(json) ?? new PermanentProgressData();
            }
            catch (ArgumentException)
            {
                return new PermanentProgressData();
            }
        }

        public static void Save(PermanentProgressData data)
        {
            data ??= new PermanentProgressData();
            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }
}
