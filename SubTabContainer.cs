using Celestial.CustomEndless;
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SubTabContainer : MonoBehaviour
{
    public static bool tempCampDis = false;

    [Serializable]
    public class RegionRecord
    {
        public string name;
        public string displayName;
        public string group;
        public string[] subregionKeys;
        public string[] subregionDisplayNames;
        public bool randomizable;
        public bool randomOption;
        public bool balanced;
        public bool useHeight;
        public bool useLoops;
        public int amounts;
        public int bronzeAmount;
        public int silverAmount;
        public int goldAmount;
        public int platinumAmount;
    }

    [Serializable]
    private class SavedLayout
    {
        public List<string> groupOrder = new List<string>();
        public List<RegionRecord> regions = new List<RegionRecord>();
    }

    public struct ResolvedSlot
    {
        public string selection;
        public bool hasBreakroomOverride;
        public bool generateBreakroom;

        public ResolvedSlot(string value, bool overrideBreakroom, bool breakroom)
        {
            selection = value;
            hasBreakroomOverride = overrideBreakroom;
            generateBreakroom = breakroom;
        }
    }

    private sealed class Resolution
    {
        public string selection;
        public List<ResolvedSlot> values;
    }

    private const string LayoutKey = "CustomEndless.SubTabContainer.Layout";
    private static readonly string[] DefaultGroupOrder = { "Regions", "Extra", "Short Regions", "Random", "Custom" };
    private static readonly Dictionary<string, RegionRecord> Catalog = new Dictionary<string, RegionRecord>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, bool> Randomizable = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, Resolution> Resolutions = new Dictionary<int, Resolution>();
    private static readonly Dictionary<string, string> GroupByRegion = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Silos", "Regions" }, { "Pipeworks", "Regions" }, { "Habitation", "Regions" }, { "Abyss", "Regions" }, { "Nest", "Regions" },
        { "Duct", "Regions" },
        { "Body Cavity", "Regions" },
        { "Diver", "Regions" },
        { "Ladder", "Regions" }, { "Chute", "Regions" }, { "Sink", "Regions" }, { "Parasite", "Regions" }, { "Chimney", "Regions" },
        { "Omicron", "Extra" }, { "Campaign", "Extra" }, { "Checkpoint", "Extra" },
        { "Pier", "Short Regions" }, { "Haunted Pier", "Short Regions" }, { "Interlude", "Short Regions" }, { "Advanced Course", "Short Regions" }, { "Shattered Territory", "Short Regions" }, { "Fractured Territory", "Short Regions" }, { "Roach Run", "Short Regions" }, { "Comms Array", "Short Regions" }, { "Boost Course", "Short Regions" },
        { "Random Any", "Random" }, { "Random Region", "Random" }, { "Random Shorts", "Random" }, { "Random Subregion", "Random" }, { "Random Random", "Random" }
    };
    private static readonly HashSet<string> RandomOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Random Any", "Random Region", "Random Shorts", "Random Subregion", "Random Random"
    };
    private static readonly HashSet<string> DefaultExcluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Campaign", "Ladder", "Checkpoint"
    };

    public static SubTabContainer Instance { get; private set; }
    public static IReadOnlyDictionary<string, RegionRecord> CachedRegions => Catalog;

    [SerializeField] private string selectedGroup = "Regions";
    [SerializeField] private bool saveLayout = true;
    private readonly List<string> groupOrder = new List<string>();
    private readonly Dictionary<string, List<TemplateRegion>> views = new Dictionary<string, List<TemplateRegion>>(StringComparer.OrdinalIgnoreCase);
    private readonly List<Button> tabButtons = new List<Button>();
    private TemplateRegion regionTemplate;
    private Transform regionParent;
    private bool built;

    private void Awake()
    {
        Instance = this;
        LoadGroups();
        CacheExistingRegions();
    }

    private void Start()
    {
        Build();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void CacheExistingRegions()
    {
        TemplateRegion[] found = FindObjectsOfType<TemplateRegion>(true);
        for (int i = 0; i < found.Length; i++)
        {
            TemplateRegion region = found[i];
            if (region == null) continue;
            if (IsRemovedOption(region.RegionName))
            {
                region.gameObject.SetActive(false);
                continue;
            }
            if (region.IsTemplate)
            {
                if (regionTemplate == null) regionTemplate = region;
                if (regionParent == null) regionParent = region.transform.parent;
                continue;
            }
            if (string.Equals(region.RegionName, "Random", StringComparison.OrdinalIgnoreCase))
            {
                Catalog.Remove("Random");
                Randomizable.Remove("Random");
                region.Configure("Random Any", "Random Any", region.SubregionKeys, region.SubregionDisplayNames, false);
            }
            RegisterRegion(region);
            if (regionParent == null) regionParent = region.transform.parent;
        }
        EnsureRandomOptions();
    }

    public void CreateRegionGroup(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName) || groupOrder.Contains(groupName)) return;
        groupOrder.Add(groupName);
        if (saveLayout) SaveGroups();
        BuildTabs();
    }

    public TemplateRegion CreateRegion(string regionName, string displayName, string groupName, string[] subregionKeys, string[] subregionDisplayNames, bool randomizable = true)
    {
        if (string.IsNullOrWhiteSpace(regionName)) return null;
        if (IsRemovedOption(regionName)) return null;
        if (regionTemplate == null || regionParent == null) CacheExistingRegions();
        if (regionTemplate == null || regionParent == null) return null;
        GameObject clone = Instantiate(regionTemplate.gameObject, regionParent);
        clone.name = regionName;
        TemplateRegion region = clone.GetComponent<TemplateRegion>();
        if (region == null)
        {
            Destroy(clone);
            return null;
        }
        region.Configure(regionName, displayName, subregionKeys, subregionDisplayNames, false);
        clone.SetActive(true);
        RegisterRegion(region, groupName, randomizable);
        AddView(region, groupName);
        if (saveLayout) SaveGroups();
        ShowGroup(selectedGroup);
        return region;
    }

    public void SaveGroups()
    {
        if (Plugin.DumbItDown) return;
        SavedLayout layout = new SavedLayout();
        layout.groupOrder.AddRange(groupOrder);
        foreach (KeyValuePair<string, RegionRecord> pair in Catalog)
        {
            RegionRecord source = pair.Value;
            layout.regions.Add(new RegionRecord
            {
                name = source.name,
                displayName = source.displayName,
                group = source.group,
                subregionKeys = Clone(source.subregionKeys),
                subregionDisplayNames = Clone(source.subregionDisplayNames),
                randomizable = IsRandomizable(source.name),
                randomOption = source.randomOption,
                balanced = source.balanced,
                useHeight = source.useHeight,
                useLoops = source.useLoops,
                amounts = source.amounts,
                bronzeAmount = source.bronzeAmount,
                silverAmount = source.silverAmount,
                goldAmount = source.goldAmount,
                platinumAmount = source.platinumAmount
            });
        }
        try
        {
            ModDataPaths.EnsureRootDirectory();
            File.WriteAllText(ModDataPaths.RegionLayoutFile, JsonUtility.ToJson(layout));
            PlayerPrefs.DeleteKey(LayoutKey);
            PlayerPrefs.Save();
        }
        catch (Exception exception)
        {
            Plugin.Log?.LogWarning(exception);
        }
    }

    public void RemoveRegion(string name)
    {
        if (name == CustomRegions.TemplateName) return;
        Catalog.Remove(name);
        Randomizable.Remove(name);
        Resolutions.Clear();
        foreach (List<TemplateRegion> list in views.Values)
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i] == null || list[i].RegionName == name)
                {
                    if (list[i] != null) Destroy(list[i].gameObject);
                    list.RemoveAt(i);
                }
        if (saveLayout) SaveGroups();
    }

    public void LoadGroups()
    {
        groupOrder.Clear();
        string json = string.Empty;
        try
        {
            if (File.Exists(ModDataPaths.RegionLayoutFile))
            {
                json = File.ReadAllText(ModDataPaths.RegionLayoutFile);
                PlayerPrefs.DeleteKey(LayoutKey);
                PlayerPrefs.Save();
            }
            else
            {
                json = PlayerPrefs.GetString(LayoutKey, string.Empty);
                if (!string.IsNullOrEmpty(json))
                {
                    ModDataPaths.EnsureRootDirectory();
                    File.WriteAllText(ModDataPaths.RegionLayoutFile, json);
                    PlayerPrefs.DeleteKey(LayoutKey);
                    PlayerPrefs.Save();
                }
            }
        }
        catch (Exception exception)
        {
            Plugin.Log?.LogWarning(exception);
        }
        if (!string.IsNullOrEmpty(json))
        {
            SavedLayout layout = JsonUtility.FromJson<SavedLayout>(json);
            if (layout != null)
            {
                if (layout.groupOrder != null)
                {
                    for (int i = 0; i < layout.groupOrder.Count; i++)
                    {
                        string group = MigrateGroupName(layout.groupOrder[i]);
                        if (!string.IsNullOrEmpty(group) && !groupOrder.Contains(group)) groupOrder.Add(group);
                    }
                }
                if (layout.regions != null)
                {
                    for (int i = 0; i < layout.regions.Count; i++)
                    {
                        RegionRecord record = layout.regions[i];
                        if (record == null || string.IsNullOrEmpty(record.name) || IsRemovedOption(record.name)) continue;
                        if (CustomRegions.IsCustom(record.name) && !CustomRegions.TryGet(record.name, out _)) continue;
                        record.group = MigrateGroupName(record.group);
                        Catalog[record.name] = record;
                        Randomizable[record.name] = record.randomizable;
                    }
                }
            }
        }
        if (!groupOrder.Contains("Extra"))
        {
            int regionsIndex = groupOrder.FindIndex(group => string.Equals(group, "Regions", StringComparison.OrdinalIgnoreCase));
            groupOrder.Insert(regionsIndex >= 0 ? regionsIndex + 1 : 0, "Extra");
        }
        for (int i = 0; i < DefaultGroupOrder.Length; i++)
            if (!groupOrder.Contains(DefaultGroupOrder[i])) groupOrder.Add(DefaultGroupOrder[i]);
        if (Plugin.DumbItDown) groupOrder.RemoveAll(group => string.Equals(group, "Custom", StringComparison.OrdinalIgnoreCase));
        selectedGroup = MigrateGroupName(selectedGroup);
    }

    public void ShowGroup(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return;
        selectedGroup = groupName;
        foreach (KeyValuePair<string, List<TemplateRegion>> pair in views)
        {
            bool active = string.Equals(pair.Key, selectedGroup, StringComparison.OrdinalIgnoreCase);
            List<TemplateRegion> regions = pair.Value;
            for (int i = 0; i < regions.Count; i++)
                regions[i]?.gameObject.SetActive(active && !IsRemovedOption(regions[i].RegionName));
        }
        for (int i = 0; i < tabButtons.Count; i++)
        {
            Button button = tabButtons[i];
            if (button == null) continue;
            string group = button.gameObject.name;
            button.interactable = !string.Equals(group, selectedGroup, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void RegisterRegion(TemplateRegion region)
    {
        if (region == null || region.IsTemplate) return;
        if (IsRemovedOption(region.RegionName))
        {
            region.gameObject.SetActive(false);
            return;
        }
        RegisterRegion(region, GetDefaultGroup(region.RegionName), !DefaultExcluded.Contains(region.RegionName));
    }

    public static bool IsRandomOption(string regionName)
    {
        return !string.IsNullOrEmpty(regionName) && RandomOptions.Contains(regionName);
    }

    public static bool IsRandomizable(string regionName)
    {
        return regionName == CustomRegions.TemplateName
            ? false
            : string.IsNullOrEmpty(regionName)
            ? false
            : Randomizable.TryGetValue(regionName, out bool value) ? value : !DefaultExcluded.Contains(regionName);
    }

    public static void SetRandomizable(string regionName, bool value)
    {
        if (string.IsNullOrEmpty(regionName)) return;
        Randomizable[regionName] = value;
        if (Catalog.TryGetValue(regionName, out RegionRecord record)) record.randomizable = value;
        UISettingsManager manager = UISettingsManager.instance;
        manager?.SetBool(regionName.ToLowerInvariant() + "_randomizable", value);
        TemplateRegion[] regions = FindObjectsOfType<TemplateRegion>(true);
        for (int i = 0; i < regions.Length; i++)
            if (regions[i] != null && string.Equals(regions[i].RegionName, regionName, StringComparison.OrdinalIgnoreCase)) regions[i].RefreshRandomizableVisual();
        Resolutions.Clear();
    }

    public static void EnsureRandomizable(string regionName, bool defaultValue)
    {
        if (string.IsNullOrEmpty(regionName)) return;
        if (!Randomizable.ContainsKey(regionName)) Randomizable[regionName] = defaultValue;
        UISettingsManager manager = UISettingsManager.instance;
        manager?.SetBool(regionName.ToLowerInvariant() + "_randomizable", Randomizable[regionName]);
    }

    public static List<ResolvedSlot> ExpandSlot(int slotIndex, string selection)
    {
        if (IsRemovedOption(selection)) selection = "Random Region";
        if (!IsRandomOption(selection)) return new List<ResolvedSlot> { new ResolvedSlot(selection, false, true) };
        bool reroll = GetSlotBool(slotIndex, "reroll_on_restart");
        if (!reroll && Resolutions.TryGetValue(slotIndex, out Resolution prior) && string.Equals(prior.selection, selection, StringComparison.OrdinalIgnoreCase) &&
            prior.values.TrueForAll(value => IsRandomizable(value.selection) && BalancedMode.CanUse(value.selection) &&
                (!CustomRegions.IsCustom(value.selection) || BalancedMode.CanResolveCustom(value.selection)))) return new List<ResolvedSlot>(prior.values);
        HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<ResolvedSlot> values = Resolve(selection, slotIndex, false, visited);
        if (values.Count == 0) values.Add(new ResolvedSlot("Silos", false, true));
        Resolutions[slotIndex] = new Resolution { selection = selection, values = new List<ResolvedSlot>(values) };
        return values;
    }

    public static string GetRandomSliderSelection()
    {
        List<string> choices = Eligible("Random", null, false);
        choices.RemoveAll(value => string.Equals(value, "Random Any", StringComparison.OrdinalIgnoreCase));
        return choices.Count == 0 ? "Random Region" : choices[UnityEngine.Random.Range(0, choices.Count)];
    }

    private void Build()
    {
        if (built) return;
        built = true;
        CacheExistingRegions();
        CustomRegions.EnsureViews(this);
        RegionFlavorText.RegisterDefaults();
        views.Clear();
        TemplateRegion[] regions = FindObjectsOfType<TemplateRegion>(true);
        HashSet<string> displayedRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < regions.Length; i++)
        {
            TemplateRegion region = regions[i];
            if (region == null || region.IsTemplate) continue;
            if (IsRemovedOption(region.RegionName))
            {
                region.gameObject.SetActive(false);
                continue;
            }
            string name = region.RegionName;
            if (string.Equals(name, "Random", StringComparison.OrdinalIgnoreCase))
            {
                Catalog.Remove("Random");
                Randomizable.Remove("Random");
                region.Configure("Random Any", "Random Any", region.SubregionKeys, region.SubregionDisplayNames, false);
                name = "Random Any";
            }
            if (!displayedRegions.Add(name))
            {
                Destroy(region.gameObject);
                continue;
            }
            string group = GetDefaultGroup(name);
            RegisterRegion(region, group, !DefaultExcluded.Contains(name));
            AddView(region, group);
        }
        EnsureRandomOptions();
        BuildTabs();
        ShowGroup(groupOrder.Contains(selectedGroup) ? selectedGroup : groupOrder[0]);
        if (saveLayout) SaveGroups();
    }

    private void EnsureRandomOptions()
    {
        string[] options = { "Random Any", "Random Region", "Random Shorts", "Random Subregion", "Random Random" };
        for (int i = 0; i < options.Length; i++)
        {
            string option = options[i];
            if (FindRegion(option) != null) continue;
            TemplateRegion created = CreateRegion(option, option, "Random", new string[0], new string[0], !DefaultExcluded.Contains(option));
            if (created == null)
            {
                Catalog[option] = new RegionRecord { name = option, displayName = option, group = "Random", subregionKeys = new string[0], subregionDisplayNames = new string[0], randomizable = !DefaultExcluded.Contains(option), randomOption = true };
                EnsureRandomizable(option, !DefaultExcluded.Contains(option));
            }
        }
    }

    private static TemplateRegion FindRegion(string name)
    {
        TemplateRegion[] regions = FindObjectsOfType<TemplateRegion>(true);
        for (int i = 0; i < regions.Length; i++)
            if (regions[i] != null && !regions[i].IsTemplate && string.Equals(regions[i].RegionName, name, StringComparison.OrdinalIgnoreCase)) return regions[i];
        return null;
    }

    private void BuildTabs()
    {
        tabButtons.Clear();
        List<Button> buttons = new List<Button>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Button button = transform.GetChild(i).GetComponent<Button>();
            if (button != null) buttons.Add(button);
        }
        while (buttons.Count > 0 && buttons.Count < groupOrder.Count)
        {
            Button template = buttons[buttons.Count - 1];
            GameObject clone = Instantiate(template.gameObject, template.transform.parent);
            clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
            buttons.Add(clone.GetComponent<Button>());
        }
        for (int i = 0; i < buttons.Count; i++)
        {
            Button button = buttons[i];
            if (i >= groupOrder.Count)
            {
                button.gameObject.SetActive(false);
                continue;
            }
            string group = groupOrder[i];
            button.gameObject.SetActive(true);
            button.gameObject.name = group;
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = group;
            Text legacy = text == null ? button.GetComponentInChildren<Text>(true) : null;
            if (legacy != null) legacy.text = group;
            button.onClick.RemoveAllListeners();
            string captured = group;
            button.onClick.AddListener(() => ShowGroup(captured));
            tabButtons.Add(button);
        }
    }

    private void AddView(TemplateRegion region, string group)
    {
        if (!views.TryGetValue(group, out List<TemplateRegion> list))
        {
            list = new List<TemplateRegion>();
            views[group] = list;
        }
        if (!list.Contains(region)) list.Add(region);
    }

    private static void RegisterRegion(TemplateRegion region, string group, bool defaultRandomizable)
    {
        if (region == null || string.IsNullOrEmpty(region.RegionName) || IsRemovedOption(region.RegionName)) return;
        string name = region.RegionName;
        RegionRecord record = new RegionRecord
        {
            name = name,
            displayName = region.DisplayName,
            group = string.IsNullOrEmpty(group) ? GetDefaultGroup(name) : group,
            subregionKeys = Clone(region.SubregionKeys),
            subregionDisplayNames = Clone(region.SubregionDisplayNames),
            randomizable = IsRandomizable(name),
            randomOption = IsRandomOption(name),
            balanced = region.Balanced,
            useHeight = region.UseHeight,
            useLoops = region.UseLoops,
            bronzeAmount = region.BronzeAmount,
            silverAmount = region.SilverAmount,
            goldAmount = region.GoldAmount,
            platinumAmount = region.PlatinumAmount
        };
        Catalog[name] = record;
        EnsureRandomizable(name, defaultRandomizable);
        region.RefreshRandomizableVisual();
    }

    private static bool IsRemovedOption(string name)
    {
        return Plugin.DumbItDown && (CustomRegions.IsCustom(name) || name == CustomRegions.TemplateName)
            ? true
            : string.Equals(name, "Random Region/Misc", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Random Misc", StringComparison.OrdinalIgnoreCase) || (tempCampDis && string.Equals(name, "Campaign", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetDefaultGroup(string regionName)
    {
        return regionName == CustomRegions.TemplateName || CustomRegions.IsCustom(regionName)
            ? "Custom"
            : GroupByRegion.TryGetValue(regionName ?? string.Empty, out string group) ? group : "Regions";
    }

    private static string MigrateGroupName(string groupName)
    {
        return groupName;
    }

    private static List<ResolvedSlot> Resolve(string selection, int slotIndex, bool randomParent, HashSet<string> visited)
    {
        if (!IsRandomOption(selection)) return new List<ResolvedSlot> { new ResolvedSlot(selection, false, true) };
        if (!visited.Add(selection)) return new List<ResolvedSlot>();
        List<string> choices;
        if (string.Equals(selection, "Random Region", StringComparison.OrdinalIgnoreCase)) choices = Eligible("Regions", visited, false);
        else if (string.Equals(selection, "Random Shorts", StringComparison.OrdinalIgnoreCase)) return ResolveShorts(slotIndex, randomParent, visited);
        else if (string.Equals(selection, "Random Subregion", StringComparison.OrdinalIgnoreCase)) return ResolveSubregion(slotIndex, visited);
        else if (string.Equals(selection, "Random Random", StringComparison.OrdinalIgnoreCase)) choices = Eligible("Random", visited, true);
        else
        {
            choices = Eligible(null, visited, true);
            choices.RemoveAll(value => string.Equals(value, "Random Any", StringComparison.OrdinalIgnoreCase));
        }
        if (choices.Count == 0) return new List<ResolvedSlot>();
        string chosen = choices[UnityEngine.Random.Range(0, choices.Count)];
        List<ResolvedSlot> resolved = IsRandomOption(chosen) ? Resolve(chosen, slotIndex, true, visited) : new List<ResolvedSlot> { new ResolvedSlot(chosen, false, true) };
        visited.Remove(selection);
        return resolved;
    }

    private static List<ResolvedSlot> ResolveShorts(int slotIndex, bool randomParent, HashSet<string> visited)
    {
        int count = randomParent ? UnityEngine.Random.Range(2, 6) : Mathf.Clamp(GetSlotInt(slotIndex, "short_count", 3), 1, 12);
        List<string> choices = Eligible("Short Regions", visited, false);
        List<ResolvedSlot> result = new List<ResolvedSlot>();
        for (int i = 0; i < count && choices.Count > 0; i++)
        {
            string chosen = choices[UnityEngine.Random.Range(0, choices.Count)];
            result.Add(new ResolvedSlot(chosen, true, i == count - 1));
        }
        return result;
    }

    private static List<ResolvedSlot> ResolveSubregion(int slotIndex, HashSet<string> visited)
    {
        List<string> candidates = new List<string>();
        foreach (KeyValuePair<string, RegionRecord> pair in Catalog)
        {
            RegionRecord record = pair.Value;
            if (record == null || record.randomOption || string.Equals(record.name, "Campaign", StringComparison.OrdinalIgnoreCase) || visited.Contains(record.name)) continue;
            if (!IsRandomizable(record.name) || !BalancedMode.CanUse(record.name) || (!BalancedMode.FullCustom && CustomRegions.IsCustom(record.name)) || record.subregionKeys == null || record.subregionKeys.Length < 2) continue;
            candidates.Add(record.name);
        }
        if (candidates.Count == 0) return new List<ResolvedSlot>();
        string name = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        RegionRecord selected = Catalog[name];
        int active = UnityEngine.Random.Range(0, selected.subregionKeys.Length);
        for (int i = 0; i < selected.subregionKeys.Length; i++)
        {
            string key = selected.subregionKeys[i];
            if (string.IsNullOrEmpty(key)) continue;
            SetSlotBool(slotIndex, "subregion_" + key + "_disabled", i != active);
        }
        return new List<ResolvedSlot> { new ResolvedSlot(name, false, true) };
    }

    private static List<string> Eligible(string group, HashSet<string> visited, bool includeRandom)
    {
        List<string> result = new List<string>();
        foreach (KeyValuePair<string, RegionRecord> pair in Catalog)
        {
            RegionRecord record = pair.Value;
            if (record == null || string.IsNullOrEmpty(record.name) || !IsRandomizable(record.name)) continue;
            if (!BalancedMode.CanUse(record.name)) continue;
            if (CustomRegions.IsCustom(record.name) && !BalancedMode.CanResolveCustom(record.name)) continue;
            if (visited != null && visited.Contains(record.name)) continue;
            if (!string.IsNullOrEmpty(group) && !string.Equals(record.group, group, StringComparison.OrdinalIgnoreCase) &&
                !(group == "Regions" && CustomRegions.IsCustom(record.name))) continue;
            if (!includeRandom && record.randomOption) continue;
            if (includeRandom && string.Equals(group, "Random", StringComparison.OrdinalIgnoreCase) && !record.randomOption) continue;
            result.Add(record.name);
        }
        return result;
    }

    private static bool GetSlotBool(int slot, string key)
    {
        string full = "slot_" + slot + "_" + key;
        UISettingsManager manager = UISettingsManager.instance;
        return manager != null
            ? manager.GetBool(full, EndlessConfiguration.CustomUIManager.GetToggleState(full))
            : EndlessConfiguration.CustomUIManager.GetToggleState(full);
    }

    private static int GetSlotInt(int slot, string key, int defaultValue)
    {
        string full = "slot_" + slot + "_" + key;
        UISettingsManager manager = UISettingsManager.instance;
        return manager != null
            ? manager.GetInt(full, EndlessConfiguration.CustomUIManager.GetCountState(full, defaultValue))
            : EndlessConfiguration.CustomUIManager.GetCountState(full, defaultValue);
    }

    private static void SetSlotBool(int slot, string key, bool value)
    {
        string full = "slot_" + slot + "_" + key;
        UISettingsManager manager = UISettingsManager.instance;
        manager?.SetBool(full, value);
        EndlessConfiguration.CustomUIManager.SettingToggles[full] = value;
    }

    private static string[] Clone(string[] values)
    {
        if (values == null) return new string[0];
        string[] result = new string[values.Length];
        Array.Copy(values, result, values.Length);
        return result;
    }
}
