using BepInEx;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Celestial.CustomEndless
{
    public static class ModDataPaths
    {
        public static string RootDirectory => Path.Combine(Paths.ConfigPath, "CustomEndless");
        public static string PresetsDirectory => Path.Combine(RootDirectory, "Presets");
        public static string CustomRegionsDirectory => Path.Combine(RootDirectory, "CustomRegions");
        public static string ProgressFile => Path.Combine(RootDirectory, "CustomEndlessProgress.json");
        public static string RegionLayoutFile => Path.Combine(RootDirectory, "RegionLayout.json");

        public static void EnsureRootDirectory()
        {
            Directory.CreateDirectory(RootDirectory);
        }
    }

    public static class ModSaveData
    {
        [Serializable]
        public sealed class SaveFile
        {
            public List<RegionProgress> regions = new List<RegionProgress>();
            public List<RunScore> scores = new List<RunScore>();
        }

        [Serializable]
        public sealed class RegionProgress
        {
            public string region;
            public float customHeight;
            public int metal;
        }

        [Serializable]
        public sealed class RunScore
        {
            public string run;
            public string region;
            public double points;
            public string completedUtc;
            public bool medalsEnabled;
            public int medal;
            public List<string> challenges = new List<string>();
        }

        private static readonly List<RunScore> scores = new List<RunScore>();

        public static List<RunScore> GetTopScores(string region)
        {
            EnsureLoaded();
            return scores.Where(score => string.Equals(score.region, region, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(score => score.points).ThenByDescending(score => score.completedUtc).Take(10).ToList();
        }

        public static void RecordScore(RunScore score)
        {
            if (score == null || string.IsNullOrEmpty(score.region) || double.IsNaN(score.points) || double.IsInfinity(score.points)) return;
            EnsureLoaded();
            if (scores.Any(existing => existing.run == score.run && existing.region == score.region)) return;
            scores.Add(score);
            List<RunScore> best = GetTopScores(score.region);
            scores.RemoveAll(existing => string.Equals(existing.region, score.region, StringComparison.OrdinalIgnoreCase) && !best.Contains(existing));
            Save();
        }

        private static readonly Dictionary<string, RegionProgress> progress = new Dictionary<string, RegionProgress>(StringComparer.OrdinalIgnoreCase);
        private static bool loaded;
        private const string PlayerPrefsPrefix = "CustomEndless.Progress.";
        private static string FilePath => ModDataPaths.ProgressFile;
        private static string LegacyFilePath => Path.Combine(Path.GetDirectoryName(typeof(ModSaveData).Assembly.Location), "CustomEndlessProgress.json");

        public static void Initialize()
        {
            EnsureLoaded();
            if (!File.Exists(FilePath)) Save();
        }

        public static float GetCustomHeight(string region)
        {
            EnsureLoaded();
            string key = region ?? string.Empty;
            if (progress.TryGetValue(key, out RegionProgress value)) return value.customHeight;
            float legacyValue = PlayerPrefs.GetFloat(PlayerPrefsPrefix + key + ".height", 0f);
            if (legacyValue > 0f)
            {
                GetOrCreate(key).customHeight = legacyValue;
                Save();
            }
            return legacyValue;
        }

        public static int GetMetal(string region)
        {
            EnsureLoaded();
            string key = region ?? string.Empty;
            if (progress.TryGetValue(key, out RegionProgress value)) return value.metal;
            int legacyValue = PlayerPrefs.GetInt(PlayerPrefsPrefix + key + ".metal", 0);
            if (legacyValue > 0)
            {
                GetOrCreate(key).metal = Mathf.Clamp(legacyValue, 0, 4);
                Save();
            }
            return legacyValue;
        }

        public static void RecordHeight(string region, float height)
        {
            if (string.IsNullOrEmpty(region) || float.IsNaN(height) || float.IsInfinity(height)) return;
            EnsureLoaded();
            GetCustomHeight(region);
            RegionProgress value = GetOrCreate(region);
            if (height <= value.customHeight) return;
            value.customHeight = height;
            Save();
        }

        public static void RecordMetal(string region, int metal)
        {
            if (string.IsNullOrEmpty(region)) return;
            EnsureLoaded();
            GetMetal(region);
            RegionProgress value = GetOrCreate(region);
            if (metal <= value.metal) return;
            value.metal = Mathf.Clamp(metal, 0, 4);
            Save();
        }

        private static RegionProgress GetOrCreate(string region)
        {
            if (progress.TryGetValue(region, out RegionProgress value)) return value;
            value = new RegionProgress { region = region, customHeight = 0f, metal = 0 };
            progress[region] = value;
            return value;
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                string loadPath = File.Exists(FilePath) ? FilePath : LegacyFilePath;
                if (!File.Exists(loadPath)) return;
                SaveFile file = JsonConvert.DeserializeObject<SaveFile>(File.ReadAllText(loadPath));
                if (file == null) return;
                if (file.scores != null) scores.AddRange(file.scores.Where(score => score != null && !string.IsNullOrEmpty(score.region)));
                if (file.regions == null) file.regions = new List<RegionProgress>();
                for (int i = 0; i < file.regions.Count; i++)
                {
                    RegionProgress value = file.regions[i];
                    if (value != null && !string.IsNullOrEmpty(value.region)) progress[value.region] = value;
                }
                if (loadPath != FilePath) Save();
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogWarning(exception);
            }
        }

        private static void Save()
        {
            try
            {
                ModDataPaths.EnsureRootDirectory();
                SaveFile file = new SaveFile { scores = new List<RunScore>(scores) };
                foreach (KeyValuePair<string, RegionProgress> pair in progress) file.regions.Add(pair.Value);
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(file, Formatting.Indented));
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogWarning(exception);
            }
        }
    }
}
