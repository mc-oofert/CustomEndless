using Celestial.CustomEndless;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class CustomRegions
{
    public const string TemplateName = "Custom Region";

    [Serializable]
    public sealed class RegionData
    {
        public string id;
        public string name;
        public string sourceRegion;
        public string enterAs = "Anywhere";
        public string exitAs = "Anywhere";
        public List<SubregionData> subregions = new List<SubregionData>();
        public Presets.PresetData settings = new Presets.PresetData();
    }

    [Serializable]
    public sealed class SubregionData
    {
        public string id;
        public string name;
        public string source;
        public List<string> levels = new List<string>();
    }

    private static readonly Dictionary<string, RegionData> regions = new Dictionary<string, RegionData>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, M_Level.LevelAssetHolder> levels = new Dictionary<string, M_Level.LevelAssetHolder>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, M_Subregion> subregions = new Dictionary<string, M_Subregion>(StringComparer.OrdinalIgnoreCase);
    private static bool loaded;
    public static IEnumerable<RegionData> All { get { Load(); return Plugin.DumbItDown ? Enumerable.Empty<RegionData>() : regions.Values; } }
    public static bool IsCustom(string id) => id != null && id.StartsWith("customregion_", StringComparison.OrdinalIgnoreCase);
    public static bool TryGet(string id, out RegionData data) { data = null; if (Plugin.DumbItDown) return false; Load(); return regions.TryGetValue(id ?? "", out data); }

    public static void Load()
    {
        if (Plugin.DumbItDown || loaded) return;
        loaded = true;
        try
        {
            Directory.CreateDirectory(ModDataPaths.CustomRegionsDirectory);
            foreach (string path in Directory.GetFiles(ModDataPaths.CustomRegionsDirectory, "*.region.json"))
            {
                try
                {
                    RegionData data = JsonConvert.DeserializeObject<RegionData>(File.ReadAllText(path));
                    if (data == null || !IsCustom(data.id) || data.id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) continue;
                    if (string.IsNullOrWhiteSpace(data.name)) data.name = "Custom Region";
                    if (string.IsNullOrWhiteSpace(data.enterAs)) data.enterAs = "Anywhere";
                    if (string.IsNullOrWhiteSpace(data.exitAs)) data.exitAs = "Anywhere";
                    if (data.subregions == null) data.subregions = new List<SubregionData>();
                    if (data.settings == null) data.settings = new Presets.PresetData();
                    data.subregions.RemoveAll(sub => sub == null || string.IsNullOrEmpty(sub.id));
                    foreach (SubregionData sub in data.subregions) if (sub.levels == null) sub.levels = new List<string>();
                    regions[data.id] = data;
                    ApplyValues(data.settings, false);
                }
                catch (Exception exception) { Plugin.Log?.LogWarning(exception); }
            }
        }
        catch (Exception exception) { Plugin.Log?.LogWarning(exception); }
    }

    public static void RefreshCatalog()
    {
        if (Plugin.DumbItDown) return;
        levels.Clear();
        subregions.Clear();
        WKAssetDatabase database = CL_AssetManager.GetFullCombinedAssetDatabase() ?? CL_AssetManager.GetBaseAssetDatabase();
        if (database != null)
        {
            if (database.levelAssets != null) foreach (M_Level.LevelAssetHolder holder in database.levelAssets) AddLevel(holder);
            if (database.subRegionAssets != null)
                foreach (M_Subregion sub in database.subRegionAssets) AddSubregion(sub);
            if (database.regionAssets != null)
                foreach (M_Region region in database.regionAssets)
                {
                    if (region == null || IsCustom(EndlessConfiguration.CleanRegionName(region.name))) continue;
                    AddLevels(region.startLevelReferences);
                    if (region.startLevels != null) foreach (M_Level level in region.startLevels) AddLevel(level);
                    if (region.subregionGroups != null)
                        foreach (M_Region.SubregionGroup group in region.subregionGroups)
                            if (group?.subregions != null) foreach (M_Subregion sub in group.subregions) AddSubregion(sub);
                    if (region.transitionLevels != null)
                        foreach (M_Region.TransitionLevels transition in region.transitionLevels)
                        {
                            if (transition == null) continue;
                            AddLevels(transition.levelReferences);
                            if (transition.levels != null) foreach (M_Level level in transition.levels) AddLevel(level);
                        }
                }
        }
        foreach (KeyValuePair<string, M_Level> pair in CustomLevelRegistry.All)
        {
            if (pair.Value == null) continue;
            AddLevel(pair.Value);
            levels[pair.Key] = M_Level.LevelAssetHolder.GetNewHolderFromLevel(pair.Value);
        }
        foreach (M_Level level in CustomLevelLoader.GetCustomLevels()) AddLevel(level);
    }

    private static void AddLevel(M_Level level)
    {
        if (level != null) AddLevel(M_Level.LevelAssetHolder.GetNewHolderFromLevel(level));
    }

    private static void AddLevel(M_Level.LevelAssetHolder holder)
    {
        if (holder == null || string.IsNullOrEmpty(holder.id)) return;
        if (!levels.ContainsKey(holder.id) || levels[holder.id].level == null) levels[holder.id] = holder;
    }

    private static void AddLevels(IEnumerable<M_Level.LevelAssetHolder> holders)
    {
        if (holders != null) foreach (M_Level.LevelAssetHolder holder in holders) AddLevel(holder);
    }

    private static void AddSubregion(M_Subregion sub)
    {
        if (sub == null || sub.name.Contains("_RuntimeCopy") || sub.name.StartsWith("customsubregion_", StringComparison.OrdinalIgnoreCase)) return;
        subregions[sub.name] = sub;
        AddLevels(sub.startLevelReferences);
        AddLevels(sub.levelReferences);
        AddLevels(sub.endLevelReferences);
        if (sub.levelGroups != null) foreach (M_Subregion.LevelGroup group in sub.levelGroups) AddLevels(group?.levelReferences);
    }

    public static IEnumerable<string> LevelIds => levels.Keys.OrderBy(id => id, StringComparer.OrdinalIgnoreCase);
    public static IEnumerable<string> SubregionIds => subregions.Keys.OrderBy(id => id, StringComparer.OrdinalIgnoreCase);
    public static string SubregionTitle(string id) => subregions.TryGetValue(id, out M_Subregion sub) ? sub.subregionName + " [" + id + "]" : id;
    public static M_Subregion GetSourceSubregion(string id) => !string.IsNullOrEmpty(id) && subregions.TryGetValue(id, out M_Subregion sub) ? sub : null;

    public static M_Level.LevelAssetHolder ResolveLevel(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (levels.TryGetValue(id, out M_Level.LevelAssetHolder holder)) return holder;
        M_Level level = CustomLevelRegistry.Load(id) ?? EndlessConfiguration.GetCachedObject<M_Level>(id);
        if (level == null) return null;
        AddLevel(level);
        return levels.TryGetValue(id, out holder) ? holder : null;
    }

    public static RegionData CreateBlank(MenuSettingsManager menu)
    {
        if (Plugin.DumbItDown || Challenges.IsLocked) return null;
        RegionData data = new RegionData { id = "customregion_" + Guid.NewGuid().ToString("N"), name = UniqueName("Custom Region") };
        regions[data.id] = data;
        EnsureView(data, menu);
        Save(data, menu);
        return data;
    }

    private static string UniqueName(string name)
    {
        string result = name;
        int number = 2;
        while (regions.Values.Any(region => string.Equals(region.name, result, StringComparison.OrdinalIgnoreCase))) result = name + " " + number++;
        return result;
    }

    public static RegionData Copy(string sourceName, MenuSettingsManager menu, int slot = -1)
    {
        if (Plugin.DumbItDown) return null;
        if (Challenges.IsLocked) return null;
        RefreshCatalog();
        RegionData data;
        if (TryGet(sourceName, out RegionData existing))
        {
            data = JsonConvert.DeserializeObject<RegionData>(JsonConvert.SerializeObject(existing));
            data.id = "customregion_" + Guid.NewGuid().ToString("N");
            data.name = UniqueName(existing.name + " Copy");
            data.settings = Remap(menu.CaptureMenuSettings(existing.id, slot), existing.id, data.id);
            foreach (SubregionData sub in data.subregions)
            {
                string old = sub.id;
                sub.id = "customsubregion_" + Guid.NewGuid().ToString("N");
                RemapSubregion(data.settings, old, sub.id);
            }
        }
        else
        {
            string title = SubTabContainer.CachedRegions.TryGetValue(sourceName, out SubTabContainer.RegionRecord record) ? record.displayName : sourceName;
            data = new RegionData { id = "customregion_" + Guid.NewGuid().ToString("N"), name = UniqueName(title + " Copy") };
            data.settings = Remap(menu.CaptureMenuSettings(sourceName, slot), sourceName, data.id);
            List<M_Region> sources = EndlessConfiguration.GetCustomRegionSources(sourceName);
            data.sourceRegion = sources.FirstOrDefault()?.name;
            if (sources.Count > 0) data.enterAs = data.exitAs = sourceName;
            foreach (M_Region source in sources)
            {
                if (source?.subregionGroups == null) continue;
                foreach (M_Region.SubregionGroup group in source.subregionGroups)
                    if (group?.subregions != null)
                        foreach (M_Subregion sub in group.subregions)
                        {
                            if (sub == null) continue;
                            SubregionData added = AddSubregionData(data, sub.name);
                            string oldKey = EndlessConfiguration.GetCustomSubregionSettingKey(sub);
                            CopySubregionValues(menu.CaptureMenuSettings(sourceName, slot), oldKey, added.id, data.settings);
                        }
            }
        }
        regions[data.id] = data;
        ApplyValues(data.settings, true);
        EnsureView(data, menu);
        Save(data, menu);
        SubTabContainer.Instance?.ShowGroup("Custom");
        return data;
    }

    private static Presets.PresetData Remap(Presets.PresetData source, string oldRegion, string newRegion)
    {
        string prefix = oldRegion.ToLowerInvariant() + "_";
        foreach (List<string> keys in new[] { source.intKeys, source.floatKeys, source.boolKeys, source.stringKeys })
            for (int i = 0; i < keys.Count; i++)
                if (keys[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) keys[i] = newRegion + "_" + keys[i].Substring(prefix.Length);
        source.name = newRegion;
        return source;
    }

    private static void RemapSubregion(Presets.PresetData values, string oldId, string newId)
    {
        foreach (List<string> keys in new[] { values.intKeys, values.floatKeys, values.boolKeys })
            for (int i = 0; i < keys.Count; i++) keys[i] = keys[i].Replace("subregion_" + oldId + "_", "subregion_" + newId + "_");
    }

    private static void CopySubregionValues(Presets.PresetData source, string oldId, string newId, Presets.PresetData target)
    {
        string prefix = "subregion_" + oldId + "_";
        for (int i = 0; i < source.boolKeys.Count; i++)
            if (source.boolKeys[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            { target.boolKeys.Add("subregion_" + newId + "_" + source.boolKeys[i].Substring(prefix.Length)); target.boolValues.Add(source.boolValues[i]); }
        for (int i = 0; i < source.floatKeys.Count; i++)
            if (source.floatKeys[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            { target.floatKeys.Add("subregion_" + newId + "_" + source.floatKeys[i].Substring(prefix.Length)); target.floatValues.Add(source.floatValues[i]); }
    }

    public static SubregionData AddSubregionData(RegionData data, string sourceId)
    {
        M_Subregion source = GetSourceSubregion(sourceId);
        SubregionData sub = new SubregionData
        {
            id = "customsubregion_" + Guid.NewGuid().ToString("N"),
            name = source != null ? string.IsNullOrWhiteSpace(source.subregionName) ? source.name : source.subregionName : "Subregion " + (data.subregions.Count + 1),
            source = source != null ? source.name : ""
        };
        if (source != null)
        {
            Action<IEnumerable<M_Level.LevelAssetHolder>> append = holders =>
            {
                if (holders != null) foreach (M_Level.LevelAssetHolder holder in holders)
                        if (holder != null && !string.IsNullOrEmpty(holder.id)) sub.levels.Add(holder.id);
            };
            append(source.startLevelReferences);
            if (source.levelGroups != null && source.levelGroups.Count > 0)
                foreach (M_Subregion.LevelGroup group in source.levelGroups) append(group?.levelReferences);
            else append(source.levelReferences);
            append(source.endLevelReferences);
            string key = "subregion_" + sub.id;
            data.settings.boolKeys.Add(key + "_ordered");
            data.settings.boolValues.Add(source.subregionOrder == M_Subregion.SubregionOrder.playlist);
            float averageLength = sub.levels.Select(id => ResolveLevel(id)?.length ?? 1f).DefaultIfEmpty(1f).Average();
            int count = source.useLevelCount ? Mathf.Max(1, (source.subregionMinLength + source.subregionMaxLength) / 2) : Mathf.Max(1, Mathf.CeilToInt(source.subregionHeight / Mathf.Max(1f, averageLength)));
            if (source.subregionOrder == M_Subregion.SubregionOrder.single) count = 1;
            data.settings.intKeys.Add(key + "_level_count");
            data.settings.intValues.Add(count);
            ApplyValues(data.settings, false);
        }
        data.subregions.Add(sub);
        return sub;
    }

    public static void EnsureViews(SubTabContainer container)
    {
        if (Plugin.DumbItDown) return;
        Load();
        MenuSettingsManager menu = container.GetComponentInParent<Canvas>(true)?.GetComponentInChildren<MenuSettingsManager>(true);
        container.CreateRegion(TemplateName, TemplateName, "Custom", new string[0], new string[0], false)?.ConfigureCustom();
        foreach (RegionData data in regions.Values) EnsureView(data, menu);
    }

    public static void EnsureView(RegionData data, MenuSettingsManager menu)
    {
        if (Plugin.DumbItDown) return;
        TemplateRegion view = UnityEngine.Object.FindObjectsOfType<TemplateRegion>(true).FirstOrDefault(region => region.RegionName == data.id);
        string[] keys = data.subregions.Select(sub => sub.id).ToArray();
        string[] names = data.subregions.Select(sub => sub.name).ToArray();
        if (view == null) view = SubTabContainer.Instance?.CreateRegion(data.id, data.name, "Custom", keys, names);
        if (view != null)
        {
            menu?.UnregisterMenu(data.id);
            view.Configure(data.id, data.name, keys, names, false);
            view.ConfigureCustom();
            view.RegisterRegionSettings();
            SubTabContainer.RegisterRegion(view);
        }
        if (menu != null)
        {
            menu.Register(data.id, "Disable Mass", data.id + "_disable_mass", SettingType.Bool, "false", data.name.ToUpperInvariant() + " SETTINGS");
            foreach (SubregionData sub in data.subregions)
            {
                menu.Register(data.id, "Ordered Levels", "subregion_" + sub.id + "_ordered", SettingType.Bool, "true", sub.name.ToUpperInvariant());
                menu.Register(data.id, "Level Count", "subregion_" + sub.id + "_level_count", SettingType.Integer, Mathf.Max(1, sub.levels.Count).ToString(), sub.name.ToUpperInvariant());
            }
            menu.SetMenuDefaults(data.id, data.settings);
        }
    }

    public static bool Save(RegionData data, MenuSettingsManager menu)
    {
        if (Plugin.DumbItDown) return false;
        if (data == null || Challenges.IsLocked) return false;
        try
        {
            data.settings = menu.CaptureMenuSettings(data.id);
            Directory.CreateDirectory(ModDataPaths.CustomRegionsDirectory);
            File.WriteAllText(Path.Combine(ModDataPaths.CustomRegionsDirectory, data.id + ".region.json"), JsonConvert.SerializeObject(data, Formatting.Indented));
            menu.SetMenuDefaults(data.id, data.settings);
            SubTabContainer.Instance?.SaveGroups();
            return true;
        }
        catch (Exception exception) { Plugin.Log?.LogWarning(exception); return false; }
    }

    public static void Delete(RegionData data, MenuSettingsManager menu)
    {
        if (Plugin.DumbItDown) return;
        if (data == null || Challenges.IsLocked) return;
        try
        {
            string path = Path.Combine(ModDataPaths.CustomRegionsDirectory, data.id + ".region.json");
            if (File.Exists(path)) File.Delete(path);
            regions.Remove(data.id);
            menu.UnregisterMenu(data.id);
            SubTabContainer.Instance?.RemoveRegion(data.id);
            UISettingsManager manager = UISettingsManager.instance;
            if (manager != null && OrderingContainer.instance != null)
                for (int i = 49; i >= 0; i--)
                    if (manager.GetString("slot_" + i + "_contents") == data.id) OrderingContainer.instance.RemoveSlotAt(i);
        }
        catch (Exception exception) { Plugin.Log?.LogWarning(exception); }
    }

    private static void ApplyValues(Presets.PresetData values, bool overwrite)
    {
        UISettingsManager manager = UISettingsManager.instance;
        for (int i = 0; i < values.intKeys.Count; i++)
            if (overwrite || !EndlessConfiguration.CustomUIManager.SettingCounts.ContainsKey(values.intKeys[i]))
            { manager?.SetInt(values.intKeys[i], values.intValues[i]); EndlessConfiguration.CustomUIManager.SettingCounts[values.intKeys[i]] = values.intValues[i]; }
        for (int i = 0; i < values.floatKeys.Count; i++)
            if (overwrite || !EndlessConfiguration.CustomUIManager.SettingFloats.ContainsKey(values.floatKeys[i]))
            { manager?.SetFloat(values.floatKeys[i], values.floatValues[i]); EndlessConfiguration.CustomUIManager.SettingFloats[values.floatKeys[i]] = values.floatValues[i]; }
        for (int i = 0; i < values.boolKeys.Count; i++)
            if (overwrite || !EndlessConfiguration.CustomUIManager.SettingToggles.ContainsKey(values.boolKeys[i]))
            { manager?.SetBool(values.boolKeys[i], values.boolValues[i]); EndlessConfiguration.CustomUIManager.SettingToggles[values.boolKeys[i]] = values.boolValues[i]; }
    }
}
