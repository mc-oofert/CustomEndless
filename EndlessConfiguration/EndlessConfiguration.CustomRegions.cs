using CustomEndless;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace Celestial.CustomEndless
{
    public static partial class EndlessConfiguration
    {
        private sealed class CustomGeneration
        {
            public CustomRegions.RegionData Data;
            public int Slot;
            public List<(CustomRegions.SubregionData Data, M_Subregion Asset)> Subregions = new List<(CustomRegions.SubregionData, M_Subregion)>();
        }

        private static readonly Dictionary<M_Region, CustomGeneration> customGeneration = new Dictionary<M_Region, CustomGeneration>();

        private static void RegisterPlayerRegions()
        {
            if (Plugin.DumbItDown) return;
            foreach (CustomRegions.RegionData data in CustomRegions.All)
                RegionDefinitions[data.id] = new RegionDefinition { RegionAsset = data.id, BreakroomAsset = "" };
        }

        public static IEnumerable<string> GetCustomTransitionChoices()
        {
            RegisterRegions();
            return new[] { "Anywhere", "Campaign" }.Concat(RegionDefinitions.Keys.Where(name => !CustomRegions.IsCustom(name))).Distinct().OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static string GetCustomTransitionIdentity(string name, bool entering)
        {
            if (!CustomRegions.TryGet(name, out CustomRegions.RegionData data)) return name;
            string identity = entering ? data.enterAs : data.exitAs;
            return string.IsNullOrWhiteSpace(identity) || CustomRegions.IsCustom(identity) ? "Anywhere" : identity;
        }

        private static bool IsCustomMassDisabled(M_Level level)
        {
            return level != null && level.region != null && customGeneration.TryGetValue(level.region, out CustomGeneration generation)
                && CustomBool(generation.Data, generation.Data.id + "_disable_mass", generation.Slot, false);
        }

        public static List<M_Region> GetCustomRegionSources(string name)
        {
            RegisterRegions();
            if (name == "Campaign") return GetCampaignGamemode()?.regions?.Where(region => region != null).ToList() ?? new List<M_Region>();
            if (!RegionDefinitions.TryGetValue(name, out RegionDefinition definition)) return new List<M_Region>();
            M_Region source;
            if (!TryGetRegisteredCustomRegion(definition.RegionAsset, out source)) source = GetCachedObject<M_Region>(definition.RegionAsset);
            return source != null ? new List<M_Region> { source } : new List<M_Region>();
        }

        public static string GetCustomSubregionSettingKey(M_Subregion sub)
        {
            foreach (string key in SubregionDefinitions.Keys)
                if (sub.name.ContainsInsensitive(key) || (sub.subregionName ?? "").ContainsInsensitive(key)) return key;
            return sub.subregionName;
        }

        public static M_Region BuildPlayerCustomRegion(CustomRegions.RegionData data, int slot)
        {
            CustomRegions.RefreshCatalog();
            M_Region source = !string.IsNullOrEmpty(data.sourceRegion) ? GetCachedObject<M_Region>(data.sourceRegion) : null;
            if (source == null)
                source = data.subregions.SelectMany(sub => sub.levels).Select(id => CustomRegions.ResolveLevel(id)?.region).FirstOrDefault(asset => asset != null)
                    ?? GetCachedObject<M_Region>("Region_Silos_Endless");
            M_Region region = source != null ? UnityEngine.Object.Instantiate(source) : ScriptableObject.CreateInstance<M_Region>();
            region.name = data.id + "_slot_" + slot + "_RuntimeCopy";
            region.regionName = data.name;
            region.introText = data.name;
            region.useRegionHeight = false;
            region.regionOrder = M_Region.RegionOrder.playlist;
            region.startLevels = new List<M_Level>();
            region.startLevelReferences = new List<M_Level.LevelAssetHolder>();
            region.transitionLevels = new List<M_Region.TransitionLevels>();
            region.subregionGroups = new List<M_Region.SubregionGroup>();
            if (region.sessionEventLists == null) region.sessionEventLists = new List<SessionEventList>();
            if (region.radioStations == null) region.radioStations = new List<ENV_RadioStation>();
            RegisterGeneratedRegion(region);
            WKAssetDatabase database = CL_AssetManager.GetBaseAssetDatabase();
            if (database != null && !database.regionAssets.Contains(region)) database.regionAssets.Add(region);
            foreach (M_Region dead in customGeneration.Keys.Where(asset => asset == null).ToList()) customGeneration.Remove(dead);
            CustomGeneration generation = new CustomGeneration { Data = data, Slot = slot };
            customGeneration[region] = generation;
            foreach (CustomRegions.SubregionData sub in data.subregions)
            {
                if (CustomBool(data, "subregion_" + sub.id + "_disabled", slot, false)) continue;
                M_Subregion template = CustomRegions.GetSourceSubregion(sub.source);
                M_Subregion asset = template != null ? UnityEngine.Object.Instantiate(template) : ScriptableObject.CreateInstance<M_Subregion>();
                asset.name = sub.id;
                asset.subregionName = sub.name;
                asset.introText = sub.name;
                asset.flagWhitelist = new List<string>();
                asset.flagBlacklist = new List<string>();
                asset.startLevelReferences = new List<M_Level.LevelAssetHolder>();
                asset.endLevelReferences = new List<M_Level.LevelAssetHolder>();
                asset.levelGroups = new List<M_Subregion.LevelGroup>();
                if (asset.sessionEventLists == null) asset.sessionEventLists = new List<SessionEventList>();
                if (asset.radioStations == null) asset.radioStations = new List<ENV_RadioStation>();
                asset.difficulty = (template != null ? template.difficulty : 1f) * CustomFloat(data, "subregion_" + sub.id + "_difficulty_mult", slot, 1f);
                asset.massSpeedMult = (template != null ? template.massSpeedMult : 1f) * CustomFloat(data, "subregion_" + sub.id + "_mass_mult", slot, 1f);
                asset.spaceCompression = (template != null ? template.spaceCompression : 1f) * CustomFloat(data, "subregion_" + sub.id + "_space_mult", slot, 1f);

                M_Subregion.LevelGroup newGroup = new M_Subregion.LevelGroup { name = sub.name, length = 0, endSubregion = true };
                foreach (string id in sub.levels)
                {
                    M_Level.LevelAssetHolder holder = CopyCustomHolder(CustomRegions.ResolveLevel(id), region, asset);
                    if (holder != null) newGroup.levelReferences.Add(holder);
                }
                if (newGroup.levelReferences.Count == 0) { UnityEngine.Object.Destroy(asset); continue; }
                asset.levelGroups.Add(newGroup);

                asset.useLevelCount = true;
                asset.subregionMinLength = asset.subregionMaxLength = newGroup.levelReferences.Count;
                RegisterGeneratedSubregion(asset);
                if (database != null && !database.subRegionAssets.Contains(asset)) database.subRegionAssets.Add(asset);
                region.subregionGroups.Add(new M_Region.SubregionGroup { name = sub.name, subregions = new List<M_Subregion> { asset } });
                generation.Subregions.Add((sub, asset));
            }
            return region;
        }

        private static M_Level.LevelAssetHolder CopyCustomHolder(M_Level.LevelAssetHolder source, M_Region region, M_Subregion sub)
        {
            return source == null
                ? null
                : new M_Level.LevelAssetHolder
            {
                id = source.id,
                length = source.length,
                level = source.level,
                levelAssetReference = source.levelAssetReference,
                region = region,
                subregion = sub,
                setCustomRegionInfo = true,
                spawnSettings = source.spawnSettings,
                tags = source.tags,
                pauseGeneration = source.pauseGeneration
            };
        }

        private static string CustomSlotKey(CustomRegions.RegionData data, string key, int slot)
        {
            string prefix = data.id + "_";
            return "slot_" + slot + "_" + (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? key.Substring(prefix.Length) : key);
        }

        private static bool CustomBool(CustomRegions.RegionData data, string key, int slot, bool fallback)
        {
            UISettingsManager manager = UISettingsManager.instance;
            string slotKey = CustomSlotKey(data, key, slot);
            if (slot >= 0 && manager != null && manager.HasBool(slotKey)) return manager.GetBool(slotKey);
            if (slot >= 0 && CustomUIManager.SettingToggles.TryGetValue(slotKey, out bool slotValue)) return slotValue;
            if (manager != null && manager.HasBool(key)) return manager.GetBool(key);
            if (CustomUIManager.SettingToggles.TryGetValue(key, out bool value)) return value;
            int index = data.settings.boolKeys.IndexOf(key);
            return index >= 0 ? data.settings.boolValues[index] : fallback;
        }

        private static float CustomFloat(CustomRegions.RegionData data, string key, int slot, float fallback)
        {
            UISettingsManager manager = UISettingsManager.instance;
            string slotKey = CustomSlotKey(data, key, slot);
            if (slot >= 0 && manager != null && manager.HasFloat(slotKey)) return manager.GetFloat(slotKey);
            if (slot >= 0 && CustomUIManager.SettingFloats.TryGetValue(slotKey, out float slotValue)) return slotValue;
            if (manager != null && manager.HasFloat(key)) return manager.GetFloat(key);
            if (CustomUIManager.SettingFloats.TryGetValue(key, out float value)) return value;
            int index = data.settings.floatKeys.IndexOf(key);
            return index >= 0 ? data.settings.floatValues[index] : fallback;
        }

        private static int CustomCount(CustomRegions.RegionData data, string key, int slot, int fallback)
        {
            UISettingsManager manager = UISettingsManager.instance;
            string slotKey = CustomSlotKey(data, key, slot);
            if (slot >= 0 && manager != null && manager.HasInt(slotKey)) return manager.GetInt(slotKey);
            if (slot >= 0 && CustomUIManager.SettingCounts.TryGetValue(slotKey, out int slotValue)) return slotValue;
            if (manager != null && manager.HasInt(key)) return manager.GetInt(key);
            if (CustomUIManager.SettingCounts.TryGetValue(key, out int value)) return value;
            int index = data.settings.intKeys.IndexOf(key);
            return index >= 0 ? data.settings.intValues[index] : fallback;
        }

        [HarmonyPatch(typeof(M_Region), nameof(M_Region.GetLevels))]
        private static class PlayerCustomRegionGenerationPatch
        {
            private static bool Prefix(M_Region __instance, ref List<M_Level.LevelAssetHolder> __result)
            {
                if (!customGeneration.TryGetValue(__instance, out CustomGeneration generation)) return true;
                List<(CustomRegions.SubregionData Data, M_Subregion Asset)> ordered = new List<(CustomRegions.SubregionData, M_Subregion)>(generation.Subregions);
                __result = new List<M_Level.LevelAssetHolder>();
                foreach (var sub in ordered)
                {
                    List<M_Level.LevelAssetHolder> pool = sub.Asset.GetLevelReferences();
                    bool inOrder = CustomBool(generation.Data, "subregion_" + sub.Data.id + "_ordered", generation.Slot, true);
                    int count = inOrder ? pool.Count : Mathf.Max(0, Mathf.RoundToInt(CustomCount(generation.Data, "subregion_" + sub.Data.id + "_level_count", generation.Slot, pool.Count) * Mathf.Max(0f, CustomFloat(generation.Data, generation.Data.id + "_length_mult", generation.Slot, 1f))));
                    for (int i = 0; i < count; i++)
                        __result.Add(CopyCustomHolder(pool[inOrder ? i : UnityEngine.Random.Range(0, pool.Count)], __instance, sub.Asset));
                }
                return false;
            }
        }
    }
}
