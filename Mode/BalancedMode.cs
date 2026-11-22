using Celestial.CustomEndless;
using HarmonyLib;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

/*
Balanced mode is when you don't modify anything and you can earn metals. 
If you want to change that mode, look here; usually the code will be here.
*/
public static class BalancedMode
{
    private static readonly HashSet<string> VanillaRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Silos", "Pipeworks", "Habitation", "Abyss", "Nest", "Chimney" };
    private static readonly Dictionary<string, float> sessionHeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> sessionLoops = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, HashSet<string>> activeModifiers = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Dictionary<string, bool>> configuredModifiers = new Dictionary<string, Dictionary<string, bool>>(StringComparer.OrdinalIgnoreCase);
    private static string currentRegion;
    private static float currentSegmentHeight;
    private static float lastSampleHeight;
    private static float currentSpaceCompression = 1f;
    private static bool hasShort;
    private static GameObject hoverMetal;

    public static bool FullCustom
    {
        get
        {
            if (Challenges.IsActive) return true;
            UISettingsManager manager = UISettingsManager.instance;
            return manager != null && manager.HasBool("full_custom")
                ? manager.GetBool("full_custom", false)
                : EndlessConfiguration.CustomUIManager.SettingToggles.TryGetValue("full_custom", out bool value) && value;
        }
    }

    public static void RegisterRegion(MenuSettingsManager menu, TemplateRegion region)
    {
        if (menu == null || region == null || !region.Balanced || SubTabContainer.IsRandomOption(region.RegionName)) return;
        string name = region.RegionName + "-BALANCED";
        menu.Register(name, "Long Haul", region.RegionName.ToLowerInvariant() + "_balanced_long_haul", SettingType.Bool, "false");
        menu.Register(name, "Mass Slapper", region.RegionName.ToLowerInvariant() + "_balanced_mass_slapper", SettingType.Bool, "false");
        menu.Register(name, "Abyss Mode", region.RegionName.ToLowerInvariant() + "_balanced_abyss_mode", SettingType.Bool, "false");
    }

    public static void EnableFullCustom()
    {
        if (Challenges.IsLocked || FullCustom) return;
        UISettingsManager.instance?.SetBool("full_custom", true);
        EndlessConfiguration.CustomUIManager.SettingToggles["full_custom"] = true;
    }

    public static bool CanQueue(string region)
    {
        if (FullCustom) return true;
        if (!CanUse(region)) return false;
        UISettingsManager manager = UISettingsManager.instance;
        if (manager == null) return true;
        for (int i = 0; i < 50; i++)
        {
            string queued = manager.GetString("slot_" + i + "_contents");
            if (string.IsNullOrEmpty(queued) || queued == "None") continue;
            if ((CustomRegions.IsCustom(region) || CustomRegions.IsCustom(queued)) &&
                !string.Equals(region, queued, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    public static bool CanResolveCustom(string region)
    {
        if (FullCustom) return true;
        UISettingsManager manager = UISettingsManager.instance;
        if (manager == null) return true;
        int count = 0;
        bool matches = true;
        for (int i = 0; i < 50; i++)
        {
            string queued = manager.GetString("slot_" + i + "_contents");
            if (string.IsNullOrEmpty(queued) || queued == "None") continue;
            count++;
            if (!string.Equals(region, queued, StringComparison.OrdinalIgnoreCase)) matches = false;
        }
        return count <= 1 || matches;
    }

    public static void NormalizeMedalQueue()
    {
        UISettingsManager manager = UISettingsManager.instance;
        if (manager == null || OrderingContainer.instance == null) return;
        string first = null;
        for (int i = 0; i < 50; i++)
        {
            string queued = manager.GetString("slot_" + i + "_contents");
            if (string.IsNullOrEmpty(queued) || queued == "None") continue;
            first = queued;
            break;
        }
        if (first == null) return;
        for (int i = 49; i >= 0; i--)
        {
            string queued = manager.GetString("slot_" + i + "_contents");
            if (string.IsNullOrEmpty(queued) || queued == "None") continue;
            if (CustomRegions.IsCustom(first) ? !string.Equals(first, queued, StringComparison.OrdinalIgnoreCase) : CustomRegions.IsCustom(queued))
                OrderingContainer.instance.RemoveSlotAt(i);
        }
    }

    public static void OpenRegionMenu(MenuSettingsManager menu, TemplateRegion region, bool showScores = false)
    {
        if (Plugin.DumbItDown || menu == null || region == null) return;
        if (menu.BeginCustomMenu(region.RegionName + (showScores ? "-SCORES" : "-PROGRESS")) == null) return;
        GameObject titleRow = CustomRegionEditor.Row(menu, "Region Title");
        Image titleFrame = titleRow.AddComponent<Image>();
        menu.StylePanel(titleFrame);
        titleFrame.raycastTarget = false;
        TMPro.TMP_Text title = CustomRegionEditor.AddLabel(titleRow.transform, region.DisplayName, 0f);
        title.fontSize = 18f;
        title.alignment = TMPro.TextAlignmentOptions.Center;
        UnityEngine.UI.LayoutElement titleHeight = titleRow.GetComponent<UnityEngine.UI.LayoutElement>();
        titleHeight.minHeight = titleHeight.preferredHeight = 46f;
        RegionFlavorText.Show(menu, region.RegionName);
        GameObject tabs = CustomRegionEditor.Row(menu, "Region Tabs");
        tabs.GetComponent<LayoutElement>().minHeight = tabs.GetComponent<LayoutElement>().preferredHeight = 40f;
        Button progressTab = CustomRegionEditor.Button(tabs.transform, GoalLabel(region), () => OpenRegionMenu(menu, region));
        progressTab.interactable = showScores;
        Button scoresTab = CustomRegionEditor.Button(tabs.transform, "Scores", () => OpenRegionMenu(menu, region, true));
        scoresTab.interactable = !showScores;
        if (!showScores)
        {
            GameObject best = CustomRegionEditor.Row(menu, "Best Goal Progress");
            Image bestFrame = best.AddComponent<Image>();
            menu.StylePanel(bestFrame);
            bestFrame.raycastTarget = false;
            best.GetComponent<LayoutElement>().minHeight = best.GetComponent<LayoutElement>().preferredHeight = 56f;
            TMPro.TMP_Text value = CustomRegionEditor.AddLabel(best.transform, GetGoalBest(region).ToString("N0"), 0f);
            value.fontSize = 24f;
            CustomRegionEditor.AddInfo(menu, "Goal Progress Info", "Personal best with medals enabled");
        }
        bool medals = region.Balanced;
        string unit = GoalLabel(region) == "Roaches collected" ? " roaches" : GoalLabel(region) == "Rounds won" ? " rounds" : "m";
        if (showScores)
        {
            List<ModSaveData.RunScore> scores = ModSaveData.GetTopScores(region.RegionName);
            scores.Sort((left, right) => right.points.CompareTo(left.points));
            HashSet<Transform> existing = new HashSet<Transform>();
            foreach (Transform child in menu.Content) existing.Add(child);
            if (scores.Count == 0) CustomRegionEditor.AddLabel(CustomRegionEditor.Row(menu, "No Scores").transform, "Complete a run to record a score", 0f);
            for (int i = 0; i < scores.Count; i++)
            {
                ModSaveData.RunScore score = scores[i];
                GameObject row = CustomRegionEditor.Row(menu, "Score " + (i + 1));
                TMPro.TMP_Text text = CustomRegionEditor.AddLabel(row.transform, (i + 1) + ". " + score.points.ToString("N0") + " points", 0f);
                float emphasis = i == 0 ? 1.35f : i == 1 ? 1.2f : i == 2 ? 1.1f : 1f;
                text.transform.localScale *= emphasis;
                row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childScaleHeight = true;
                row.GetComponent<UnityEngine.UI.LayoutElement>().minHeight = row.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 32f * emphasis;
                if (score.medalsEnabled && score.medal > 0) AddScoreMedal(row.transform, score.medal);
                if (score.challenges != null && score.challenges.Count > 0)
                    CustomRegionEditor.AddInfo(menu, "Run Challenges", string.Join(", ", score.challenges));
            }
            GameObject panel = new GameObject("Score List", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.VerticalLayoutGroup));
            List<Transform> scoreRows = new List<Transform>();
            foreach (Transform child in menu.Content) if (!existing.Contains(child)) scoreRows.Add(child);
            panel.transform.SetParent(menu.Content, false);
            menu.StylePanel(panel.GetComponent<UnityEngine.UI.Image>());
            panel.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            UnityEngine.UI.VerticalLayoutGroup layout = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.reverseArrangement = false;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 3f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            for (int i = 0; i < scoreRows.Count; i++)
            {
                scoreRows[i].SetParent(panel.transform, false);
                scoreRows[i].SetSiblingIndex(i);
            }
            menu.TrackInstance(panel);
        }
        if (!showScores)
        {
            if (medals)
            {
                string[] names = { "Bronze", "Silver", "Gold", "Platinum" };
                int[] goals = { region.BronzeAmount, region.SilverAmount, region.GoldAmount, region.PlatinumAmount };
                for (int i = 0; i < names.Length; i++)
                {
                    GameObject row = CustomRegionEditor.Row(menu, names[i] + " Goal");
                    AddScoreMedal(row.transform, i + 1, 16f);
                    string requirement = goals[i] >= 1000 ? (goals[i] / 1000f).ToString("0.#") + "k" : goals[i].ToString();
                    TMPro.TMP_Text text = CustomRegionEditor.AddLabel(row.transform, requirement + unit, 0f);
                    text.color = new Color(0.68f, 0.68f, 0.68f, 1f);
                    text.transform.localScale *= 0.85f;
                    row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childScaleHeight = true;
                    row.GetComponent<UnityEngine.UI.LayoutElement>().minHeight = row.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 24f;
                }
                if (IsShort(region.RegionName) && hasShort)
                    CustomRegionEditor.AddLabel(CustomRegionEditor.Row(menu, "Medal Info").transform, "Non-short regions are capped at gold when a short region is queued", 0f);
            }
            else CustomRegionEditor.AddLabel(CustomRegionEditor.Row(menu, "Medal Info").transform, "Medals unavailable for this region", 0f);
            if (CustomRegions.IsCustom(region.RegionName) && !FullCustom)
                CustomRegionEditor.AddLabel(CustomRegionEditor.Row(menu, "Queue Info").transform, "Custom regions earn medals when queued on their own", 0f);
        }
        Transform Foot = menu.CreateRegionFoot();
        GameObject queue = CustomRegionEditor.Row(menu, "Add To Queue");
        queue.transform.SetParent(Foot, false);
        UnityEngine.UI.Button add = CustomRegionEditor.Button(queue.transform, "Add to Queue", () => OrderingContainer.instance?.AddSlot(region.RegionName, SubTabContainer.IsRandomOption(region.RegionName)));
        add.interactable = !Challenges.IsLocked && CanQueue(region.RegionName);
        GameObject actions = CustomRegionEditor.Row(menu, "Region Actions");
        actions.transform.SetParent(Foot, false);
        CustomRegionEditor.Button(actions.transform, "Challenges", () =>
        {
            menu.LoadBalancedMenu(region.RegionName + "-BALANCED", BuildRequirements(region, string.Empty));
        }).interactable = medals && !Challenges.IsLocked;
        CustomRegionEditor.Button(actions.transform, "Config", () => menu.LoadMenu(region.RegionName)).interactable = !Challenges.IsLocked;
    }

    private static readonly Dictionary<string, int> goalRounds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> goalRoaches = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<M_Level> completedGoalLevels = new HashSet<M_Level>();

    private static string GoalLabel(TemplateRegion region)
    {
        return string.Equals(NormalizeRegion(region.RegionName), "roachrun", StringComparison.OrdinalIgnoreCase)
            ? "Roaches collected"
            : region.UseLoops || !region.UseHeight ? "Rounds won" : "Meters climbed";
    }

    private static float GetGoalBest(TemplateRegion region)
    {
        string label = GoalLabel(region);
        return label == "Meters climbed" && TryGetRecord(region.RegionName, out SubTabContainer.RegionRecord record)
            ? Mathf.Max(ModSaveData.GetCustomHeight(region.RegionName + "|score"), GetEvaluationValue(region.RegionName, record))
            : ModSaveData.GetCustomHeight(region.RegionName + "|goal");
    }

    private static string CurrentGoalRegion(out M_Level level)
    {
        level = WorldLoader.instance?.GetCurrentLevel()?.GetLevel();
        if (FullCustom || CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode || level == null) return null;
        string region = EndlessConfiguration.GetLogicalRegionName(level);
        return TryGetRecord(region, out SubTabContainer.RegionRecord record) && record.balanced ? record.name : null;
    }

    [HarmonyPatch(typeof(CL_GameManager), "Win")]
    private static class GoalRoundPatch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix()
        {
            string region = CurrentGoalRegion(out M_Level level);
            if (region == null || !TryGetRecord(region, out SubTabContainer.RegionRecord record) ||
                record.useHeight && !record.useLoops || NormalizeRegion(region) == "roachrun" || !completedGoalLevels.Add(level)) return;
            int count = goalRounds.TryGetValue(region, out int previous) ? previous + 1 : 1;
            goalRounds[region] = count;
            ModSaveData.RecordHeight(region + "|goal", count);
        }
    }

    [HarmonyPatch(typeof(CL_GameManager), "AddRoaches")]
    private static class GoalRoachPatch
    {
        private static void Postfix(int i, bool global)
        {
            if (i <= 0 || global) return;
            string region = CurrentGoalRegion(out M_Level level);
            if (region == null || NormalizeRegion(region) != "roachrun") return;
            int count = goalRoaches.TryGetValue(region, out int previous) ? previous + i : i;
            goalRoaches[region] = count;
            ModSaveData.RecordHeight(region + "|goal", count);
        }
    }

    private static void AddScoreMedal(Transform parent, int medal, float iconSize = 24f)
    {
        GameObject icon = new GameObject("Medal", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
        icon.transform.SetParent(parent, false);
        UnityEngine.UI.Image image = icon.GetComponent<UnityEngine.UI.Image>();
        image.sprite = FindMetalSprite(NameForMetal(medal));
        image.preserveAspect = true;
        image.raycastTarget = false;
        UnityEngine.UI.LayoutElement size = icon.GetComponent<UnityEngine.UI.LayoutElement>();
        size.minWidth = size.preferredWidth = iconSize;
        size.minHeight = size.preferredHeight = iconSize;
    }

    public static bool CanUse(string region)
    {
        return FullCustom || CustomRegions.IsCustom(region) || SubTabContainer.IsRandomOption(region)
            ? true
            : SubTabContainer.CachedRegions.TryGetValue(region ?? string.Empty, out SubTabContainer.RegionRecord record) && record.balanced;
    }

    public static void BeginBuild()
    {
        hasShort = false;
        sessionHeights.Clear();
        sessionLoops.Clear();
        goalRounds.Clear();
        goalRoaches.Clear();
        completedGoalLevels.Clear();
        activeModifiers.Clear();
        currentRegion = null;
        currentSegmentHeight = 0f;
        lastSampleHeight = 0f;
        currentSpaceCompression = 1f;
    }

    public static void NotifyResolved(string region, string source)
    {
        if (IsShort(region) && !string.Equals(source, "Random Shorts", StringComparison.OrdinalIgnoreCase)) hasShort = true;
    }

    public static void ApplySlot(string region, int slot)
    {
        if (FullCustom || !TryGetRecord(region, out SubTabContainer.RegionRecord record)) return;
        string[] keys = record.subregionKeys ?? new string[0];
        if (!CustomRegions.IsCustom(region))
        {
            SetInt(slot, "loops", 1);
            SetFloat(slot, "length_mult", 1f);
            SetInt(slot, "breakroom_amount", 1);
            SetInt(slot, "faces", 0);
            for (int i = 0; i < keys.Length; i++)
            {
                if (string.IsNullOrEmpty(keys[i])) continue;
                SetBool(slot, "subregion_" + keys[i] + "_disabled", false);
                SetFloat(slot, "subregion_" + keys[i] + "_difficulty_mult", 1f);
                SetFloat(slot, "subregion_" + keys[i] + "_mass_mult", 1f);
                SetFloat(slot, "subregion_" + keys[i] + "_space_mult", 1f);
            }
        }
        if (CustomRegions.TryGet(region, out CustomRegions.RegionData custom))
        {
            string prefix = region.ToLowerInvariant() + "_";
            string SlotKey(string key) => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? key.Substring(prefix.Length) : key;
            for (int i = 0; i < custom.settings.intKeys.Count; i++) SetInt(slot, SlotKey(custom.settings.intKeys[i]), custom.settings.intValues[i]);
            for (int i = 0; i < custom.settings.floatKeys.Count; i++) SetFloat(slot, SlotKey(custom.settings.floatKeys[i]), custom.settings.floatValues[i]);
            for (int i = 0; i < custom.settings.boolKeys.Count; i++) SetBool(slot, SlotKey(custom.settings.boolKeys[i]), custom.settings.boolValues[i]);
        }
        bool longHaul = GetModifierBool(region, "balanced_long_haul");
        bool mass = GetModifierBool(region, "balanced_mass_slapper");
        bool abyss = GetModifierBool(region, "balanced_abyss_mode");
        if (mass && abyss)
        {
            abyss = false;
            SetRegionBool(region, "balanced_abyss_mode", false);
            SetModifierSetting(region.ToLowerInvariant() + "_balanced_abyss_mode", false);
        }
        CaptureModifiers(region, longHaul, mass, abyss);
        if (longHaul) SetFloat(slot, "length_mult", 3f);
        if (mass)
        {
            SetInt(slot, "faces", 1);
            for (int i = 0; i < keys.Length; i++) if (!string.IsNullOrEmpty(keys[i])) SetFloat(slot, "subregion_" + keys[i] + "_mass_mult", 2f);
        }
        if (abyss) SetInt(slot, "faces", 3);
    }

    public static bool IsAbyssMode(string region)
    {
        return !FullCustom && IsModifierActive(region, "balanced_abyss_mode");
    }

    public static void OnLevelEnter(M_Level level)
    {
        if (FullCustom || level == null || CL_GameManager.gMan == null) return;
        string region = EndlessConfiguration.GetLogicalRegionName(level);
        if (!TryGetRecord(region, out SubTabContainer.RegionRecord record) || !CanUse(record.name)) return;
        region = record.name;
        float height = CL_GameManager.gMan.GetPlayerCorrectedHeight();
        if (!string.IsNullOrEmpty(currentRegion) && !string.Equals(currentRegion, region, StringComparison.OrdinalIgnoreCase)) FinalizeRegion(height);
        if (!string.Equals(currentRegion, region, StringComparison.OrdinalIgnoreCase))
        {
            currentRegion = region;
            currentSegmentHeight = 0f;
            lastSampleHeight = height;
            currentSpaceCompression = GetSpaceCompression(level);
            if (!sessionHeights.ContainsKey(region)) sessionHeights[region] = 0f;
            sessionLoops[region] = sessionLoops.TryGetValue(region, out int count) ? count + 1 : 1;
            UpdateMetal(region);
        }
        else
        {
            AccumulateHeight(height);
            currentSpaceCompression = GetSpaceCompression(level);
        }
    }

    public static void UpdateHeight()
    {
        if (FullCustom || string.IsNullOrEmpty(currentRegion) || CL_GameManager.gMan == null) return;
        AccumulateHeight(CL_GameManager.gMan.GetPlayerCorrectedHeight());
    }

    public static List<string> GetRunChallenges(string region)
    {
        List<string> result = new List<string>();
        if (IsModifierActive(region, "balanced_long_haul")) result.Add("Long Haul");
        if (IsModifierActive(region, "balanced_mass_slapper")) result.Add("Mass Slapper");
        if (IsModifierActive(region, "balanced_abyss_mode")) result.Add("Abyss Mode");
        return result;
    }

    public static int GetRunMedal(string region)
    {
        if (!TryGetRecord(region, out SubTabContainer.RegionRecord record) || !record.balanced) return 0;
        float height = sessionHeights.TryGetValue(region, out float value) ? value : 0f;
        if (string.Equals(region, currentRegion, StringComparison.OrdinalIgnoreCase)) height += currentSegmentHeight;
        if (record.useLoops) height = sessionLoops.TryGetValue(region, out int loops) ? loops : 0;
        return CalculateMetal(height, record, region);
    }

    public static void ShowMetal(TemplateRegion region)
    {
        if (FullCustom || region == null || !region.Balanced) return;
        int metal = GetMetal(region.RegionName);
        bool placeholder = metal <= 0;
        if (placeholder) metal = 2;
        if (hoverMetal == null)
        {
            hoverMetal = new GameObject("Metal", typeof(RectTransform), typeof(Image));
            RectTransform rect = hoverMetal.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(22f, 22f);
            hoverMetal.GetComponent<Image>().raycastTarget = false;
        }
        if (hoverMetal.transform.parent != region.transform) hoverMetal.transform.SetParent(region.transform, false);
        Image image = hoverMetal.GetComponent<Image>();
        image.sprite = FindMetalSprite(NameForMetal(metal));
        Color color = image.sprite == null ? ColorForMetal(metal) : Color.white;
        color.a = placeholder ? 0.3f : 1f;
        image.color = color;
        hoverMetal.SetActive(true);
    }

    public static void HideMetal()
    {
        hoverMetal?.SetActive(false);
    }

    internal static Sprite FindMetalSprite(string name)
    {
        string target = "Challenge_Coin_Icon_" + name;
        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < sprites.Length; i++) if (sprites[i] != null && string.Equals(sprites[i].name, target, StringComparison.OrdinalIgnoreCase)) return sprites[i];
        return Resources.Load<Sprite>(target);
    }

    private static void FinalizeRegion(float exitHeight)
    {
        if (string.IsNullOrEmpty(currentRegion)) return;
        AccumulateHeight(exitHeight);
        sessionHeights[currentRegion] = (sessionHeights.TryGetValue(currentRegion, out float value) ? value : 0f) + currentSegmentHeight;
        currentSegmentHeight = 0f;
        ModSaveData.RecordHeight(currentRegion, sessionHeights[currentRegion]);
        UpdateMetal(currentRegion);
        currentRegion = null;
        currentSegmentHeight = 0f;
        lastSampleHeight = exitHeight;
        currentSpaceCompression = 1f;
    }

    private static void AccumulateHeight(float height)
    {
        float delta = Mathf.Max(0f, height - lastSampleHeight);
        if (delta > 0f)
        {
            currentSegmentHeight += delta * currentSpaceCompression;
            UpdateMetal(currentRegion);
        }
        lastSampleHeight = Mathf.Max(lastSampleHeight, height);
    }

    private static float GetSpaceCompression(M_Level level)
    {
        float compression = level != null && level.subRegion != null
            ? level.subRegion.spaceCompression
            : 1f;
        return float.IsNaN(compression) || float.IsInfinity(compression)
            ? 1f
            : Mathf.Max(0f, compression);
    }

    private static void UpdateMetal(string region)
    {
        if (FullCustom || !TryGetRecord(region, out SubTabContainer.RegionRecord record) || !record.balanced) return;
        region = record.name;
        RecordMetal(region, record, region);
        if (IsModifierActive(region, "balanced_long_haul")) RecordMetal(region, record, ProgressKey(region, "balanced_long_haul"));
        if (IsModifierActive(region, "balanced_mass_slapper")) RecordMetal(region, record, ProgressKey(region, "balanced_mass_slapper"));
        if (IsModifierActive(region, "balanced_abyss_mode")) RecordMetal(region, record, ProgressKey(region, "balanced_abyss_mode"));
    }

    public static void AddToggleMetal(Transform parent, string region, string settingTag)
    {
        if (parent == null || FullCustom) return;
        int metal = GetToggleMetal(region, settingTag);
        bool placeholder = metal <= 0;
        if (placeholder) metal = 2;
        GameObject icon = new GameObject("Metal", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(parent, false);
        RectTransform rect = icon.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-6f, 0f);
        rect.sizeDelta = new Vector2(18f, 18f);
        Image image = icon.GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = FindMetalSprite(NameForMetal(metal));
        Color color = image.sprite == null ? ColorForMetal(metal) : Color.white;
        color.a = placeholder ? 0.3f : 1f;
        image.color = color;
    }

    private static int GetToggleMetal(string region, string settingTag)
    {
        if (string.IsNullOrEmpty(region) || string.IsNullOrEmpty(settingTag)) return 0;
        int marker = settingTag.IndexOf("_balanced_", StringComparison.OrdinalIgnoreCase);
        string key = marker >= 0 ? settingTag.Substring(marker + 1) : settingTag;
        if (TryGetRecord(region, out SubTabContainer.RegionRecord record)) region = record.name;
        return ModSaveData.GetMetal(ProgressKey(region, key));
    }

    private static void RecordMetal(string region, SubTabContainer.RegionRecord record, string key)
    {
        int metal = CalculateMetal(GetEvaluationValue(region, record), record, region);
        ModSaveData.RecordMetal(key, metal);
        ModSaveData.RecordHeight(key + "|score", GetEvaluationValue(region, record));
    }

    private static int CalculateMetal(float value, SubTabContainer.RegionRecord record, string region)
    {
        int metal = 0;
        if (value >= record.bronzeAmount) metal = 1;
        if (value >= record.silverAmount) metal = 2;
        if (value >= record.goldAmount) metal = 3;
        if (value >= record.platinumAmount && (!hasShort || IsShort(region))) metal = 4;
        return metal;
    }

    private static string ProgressKey(string region, string settingTag)
    {
        return region + "|" + settingTag;
    }

    private static float GetEvaluationValue(string region, SubTabContainer.RegionRecord record)
    {
        if (record.useLoops) return sessionLoops.TryGetValue(region, out int loops) ? loops : 0f;
        if (!record.useHeight) return 0f;
        float custom = ModSaveData.GetCustomHeight(region);
        if (sessionHeights.TryGetValue(region, out float session)) custom = Mathf.Max(custom, session + (string.Equals(region, currentRegion, StringComparison.OrdinalIgnoreCase) ? currentSegmentHeight : 0f));
        return VanillaRegions.Contains(region) ? Mathf.Max(custom, GetVanillaHeight(region)) : custom;
    }

    private static float GetVanillaHeight(string region)
    {
        try
        {
            M_Gamemode mode = CL_AssetManager.GetBaseAssetDatabase()?.gamemodeAssets?.Find(value => value != null && value.name.ContainsInsensitive(region) && value.name.ContainsInsensitive("endless"));
            return mode == null || StatManager.saveData == null
                ? 0f
                : (float)StatManager.saveData.GetGameMode(mode.gamemodeName).stats.GetStatistic("highest-ascent").GetValue();
        }
        catch
        {
            return 0f;
        }
    }

    private static int GetMetal(string region)
    {
        if (!TryGetRecord(region, out SubTabContainer.RegionRecord record)) return 0;
        region = record.name;
        float value = GetEvaluationValue(region, record);
        int metal = ModSaveData.GetMetal(region);
        if (value >= record.bronzeAmount) metal = Mathf.Max(metal, 1);
        if (value >= record.silverAmount) metal = Mathf.Max(metal, 2);
        if (value >= record.goldAmount) metal = Mathf.Max(metal, 3);
        if (value >= record.platinumAmount && (!hasShort || IsShort(region))) metal = Mathf.Max(metal, 4);
        ModSaveData.RecordMetal(region, metal);
        return metal;
    }

    private static string BuildRequirements(TemplateRegion region, string warning)
    {
        string unit = GoalLabel(region) == "Roaches collected" ? " roaches" : GoalLabel(region) == "Rounds won" ? " rounds" : "m";
        string text = warning;
        if (!string.IsNullOrEmpty(text)) text += "\n";
        return text + "Bronze " + region.BronzeAmount + unit + "\nSilver " + region.SilverAmount + unit + "\nGold " + region.GoldAmount + unit + "\nPlatinum " + region.PlatinumAmount + unit;
    }

    private static bool TryGetRecord(string region, out SubTabContainer.RegionRecord record)
    {
        if (SubTabContainer.CachedRegions.TryGetValue(region ?? string.Empty, out record)) return true;
        string normalized = NormalizeRegion(region);
        foreach (KeyValuePair<string, SubTabContainer.RegionRecord> pair in SubTabContainer.CachedRegions)
        {
            if (string.Equals(NormalizeRegion(pair.Key), normalized, StringComparison.OrdinalIgnoreCase))
            {
                record = pair.Value;
                return true;
            }
        }
        record = null;
        return false;
    }

    private static string NormalizeRegion(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        char[] buffer = new char[value.Length];
        int count = 0;
        for (int i = 0; i < value.Length; i++) if (char.IsLetterOrDigit(value[i])) buffer[count++] = char.ToLowerInvariant(value[i]);
        return new string(buffer, 0, count);
    }

    private static bool IsShort(string region)
    {
        return TryGetRecord(region, out SubTabContainer.RegionRecord record) && string.Equals(record.group, "Short Regions", StringComparison.OrdinalIgnoreCase);
    }

    public static void SetModifierSetting(string settingTag, bool value)
    {
        if (string.IsNullOrEmpty(settingTag)) return;
        const string marker = "_balanced_";
        int index = settingTag.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index <= 0) return;
        string regionKey = settingTag.Substring(0, index);
        foreach (KeyValuePair<string, SubTabContainer.RegionRecord> pair in SubTabContainer.CachedRegions)
        {
            if (!string.Equals(pair.Key.ToLowerInvariant(), regionKey, StringComparison.OrdinalIgnoreCase)) continue;
            if (!configuredModifiers.TryGetValue(pair.Key, out Dictionary<string, bool> settings))
            {
                settings = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                configuredModifiers[pair.Key] = settings;
            }
            settings[settingTag.Substring(index + 1)] = value;
            return;
        }
    }

    private static bool GetModifierBool(string region, string key)
    {
        return configuredModifiers.TryGetValue(region ?? string.Empty, out Dictionary<string, bool> settings) && settings.TryGetValue(key, out bool value)
            ? value
            : GetBool(region, key);
    }

    private static void CaptureModifiers(string region, bool longHaul, bool mass, bool abyss)
    {
        HashSet<string> values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (longHaul) values.Add("balanced_long_haul");
        if (mass) values.Add("balanced_mass_slapper");
        if (abyss) values.Add("balanced_abyss_mode");
        activeModifiers[region] = values;
    }

    private static bool IsModifierActive(string region, string key)
    {
        if (activeModifiers.TryGetValue(region ?? string.Empty, out HashSet<string> values) && values.Contains(key)) return true;
        foreach (KeyValuePair<string, HashSet<string>> pair in activeModifiers) if (string.Equals(NormalizeRegion(pair.Key), NormalizeRegion(region), StringComparison.OrdinalIgnoreCase) && pair.Value.Contains(key)) return true;
        return false;
    }

    private static bool GetBool(string region, string key)
    {
        string full = region.ToLowerInvariant() + "_" + key;
        UISettingsManager manager = UISettingsManager.instance;
        return manager != null && manager.HasBool(full)
            ? manager.GetBool(full, false)
            : EndlessConfiguration.CustomUIManager.GetToggleState(full);
    }

    private static void SetRegionBool(string region, string key, bool value)
    {
        string full = region.ToLowerInvariant() + "_" + key;
        UISettingsManager manager = UISettingsManager.instance;
        manager?.SetBool(full, value);
        EndlessConfiguration.CustomUIManager.SettingToggles[full] = value;
    }

    private static void SetBool(int slot, string key, bool value)
    {
        string full = "slot_" + slot + "_" + key;
        UISettingsManager manager = UISettingsManager.instance;
        manager?.SetBool(full, value);
        EndlessConfiguration.CustomUIManager.SettingToggles[full] = value;
    }

    private static void SetInt(int slot, string key, int value)
    {
        string full = "slot_" + slot + "_" + key;
        UISettingsManager manager = UISettingsManager.instance;
        manager?.SetInt(full, value);
        EndlessConfiguration.CustomUIManager.SettingCounts[full] = value;
    }

    private static void SetFloat(int slot, string key, float value)
    {
        string full = "slot_" + slot + "_" + key;
        UISettingsManager manager = UISettingsManager.instance;
        manager?.SetFloat(full, value);
        EndlessConfiguration.CustomUIManager.SettingFloats[full] = value;
    }

    private static string NameForMetal(int metal)
    {
        return metal == 4 ? "Platinum" : metal == 3 ? "Gold" : metal == 2 ? "Silver" : "Bronze";
    }

    private static Color ColorForMetal(int metal)
    {
        return metal == 4 ? new Color(0.8f, 0.9f, 1f) : metal == 3 ? new Color(1f, 0.8f, 0.1f) : metal == 2 ? new Color(0.8f, 0.8f, 0.85f) : new Color(0.8f, 0.45f, 0.2f);
    }
}

[HarmonyPatch(typeof(CL_GameManager), "Update")]
public static class BalancedModeUpdatePatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        BalancedMode.UpdateHeight();
    }
}
