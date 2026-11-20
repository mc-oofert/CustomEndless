using Celestial.CustomEndless;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;


//Challenge related stuff
public sealed class Challenges : MonoBehaviour
{
    private sealed class LockedGroup
    {
        public CanvasGroup Group;
        public bool Interactable;
        public bool BlocksRaycasts;
    }

    private readonly Dictionary<GameObject, LockedGroup> lockedGroups = new Dictionary<GameObject, LockedGroup>();
    private static Presets.PresetData selected;
    private static Presets.PresetData previous;
    private static string runChallenge;
    private static bool applying;
    private MenuSettingsManager menu;
    private bool showingChallenges;
    private Transform challengeContent;
    public static bool IsActive => selected != null;
    public static string RunName => runChallenge;
    public static bool IsLocked => IsActive && !applying;
    public static string DirectoryPath => Path.Combine(Path.GetDirectoryName(typeof(Challenges).Assembly.Location), "Challenges");

    private void Start()
    {
        InstallPackagedChallenges();
        menu = GetComponentInChildren<MenuSettingsManager>(true);
        Presets tab = GetComponentInChildren<Presets>(true);
        if (menu == null || tab == null) return;
        GameObject clone = Instantiate(tab.gameObject, tab.transform.parent);
        clone.name = "Challenges";
        clone.transform.SetSiblingIndex(tab.transform.GetSiblingIndex() + 1);
        Presets presetTab = clone.GetComponent<Presets>();
        if (presetTab != null) DestroyImmediate(presetTab);
        foreach (TMP_Text text in clone.GetComponentsInChildren<TMP_Text>(true)) text.text = "Challenges";
        foreach (UnityEngine.UI.Text text in clone.GetComponentsInChildren<UnityEngine.UI.Text>(true)) text.text = "Challenges";
        UnityEngine.UI.Button button = clone.GetComponent<UnityEngine.UI.Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OpenMenu);
        }
        else clone.AddComponent<ChallengeTab>().Owner = this;
        clone.SetActive(true);
        if (selected != null)
        {
            applying = true;
            try { Presets.Apply(selected); }
            finally { applying = false; }
        }
    }

    private static void InstallPackagedChallenges()
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            Assembly assembly = typeof(Challenges).Assembly;
            const string prefix = "CustomEndless.Challenges.";
            foreach (string resource in assembly.GetManifestResourceNames())
            {
                if (!resource.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string path = Path.Combine(DirectoryPath, resource.Substring(prefix.Length));
                if (File.Exists(path)) continue;
                using (Stream source = assembly.GetManifestResourceStream(resource))
                using (FileStream destination = File.Create(path)) source.CopyTo(destination);
                File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
            }
        }
        catch (Exception exception) { Plugin.Log?.LogWarning(exception); }
    }

    public void OpenMenu()
    {
        challengeContent = menu?.BeginCustomMenu("Challenges");
        showingChallenges = challengeContent != null;
        if (challengeContent == null) return;
        Unlock(challengeContent.gameObject);
        Dictionary<string, Presets.PresetData> presets = new Dictionary<string, Presets.PresetData>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(DirectoryPath))
        {
            string[] files = Directory.GetFiles(DirectoryPath, "*.preset.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                try
                {
                    Presets.PresetData data = JsonUtility.FromJson<Presets.PresetData>(File.ReadAllText(file));
                    if (data == null) continue;
                    if (string.IsNullOrWhiteSpace(data.name)) data.name = Path.GetFileName(file).Replace(".preset.json", "");
                    presets[data.name] = data;
                    presets[Path.GetFileName(file).Replace(".preset.json", "")] = data;
                }
                catch (Exception exception)
                {
                    Plugin.Log?.LogWarning(exception);
                }
            }
        }
        foreach (string name in ChallengeGoals.ByName.Keys)
        {
            presets.TryGetValue(name, out Presets.PresetData data);
            if (data != null) data.name = name;
            CreateRow(name, data);
        }
        if (ChallengeGoals.ByName.Count == 0) CreateRow("No challenges configured", null);
    }

    private void CreateRow(string name, Presets.PresetData data)
    {
        GameObject instance = menu.CreateIntegerInstance(challengeContent);
        if (instance == null) return;
        instance.SetActive(false);
        foreach (MonoBehaviour behaviour in instance.GetComponents<MonoBehaviour>())
            if (behaviour is TemplateInteger || behaviour is NumericInputControls) DestroyImmediate(behaviour);
        TMP_Text source = instance.GetComponentInChildren<TMP_Text>(true);
        TMP_FontAsset font = source != null ? source.font : TMP_Settings.defaultFontAsset;
        for (int i = instance.transform.childCount - 1; i >= 0; i--) DestroyImmediate(instance.transform.GetChild(i).gameObject);
        instance.name = "Challenge " + name;
        UnityEngine.UI.Button button = instance.GetComponent<UnityEngine.UI.Button>() ?? instance.AddComponent<UnityEngine.UI.Button>();
        UnityEngine.UI.Image image = instance.GetComponent<UnityEngine.UI.Image>() ?? instance.AddComponent<UnityEngine.UI.Image>();
        menu.StylePanel(image);
        button.targetGraphic = image;
        button.interactable = data != null;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            Select(data);
            showingChallenges = false;
            menu.BeginCustomMenu("Challenges");
            OpenMenu();
        });
        GameObject label = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(instance.transform, false);
        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 4f);
        rect.offsetMax = new Vector2(-8f, -4f);
        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = 14f;
        text.color = Color.white;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        int medal = ModSaveData.GetMetal("challenge|" + name);
        text.text = (selected != null && string.Equals(selected.name, name, StringComparison.OrdinalIgnoreCase) ? "Enabled: " : "") + name;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(8f, -27f);
        rect.offsetMax = new Vector2(-32f, -4f);
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        if (medal > 0)
        {
            UnityEngine.UI.Image earned = CreateMedalIcon(instance.transform, medal, 20f);
            RectTransform iconRect = earned.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = Vector2.one;
            iconRect.anchoredPosition = new Vector2(-8f, -5f);
        }
        if (ChallengeGoals.ByName.TryGetValue(name, out ChallengeGoals.HeightGoals goals))
        {
            GameObject targets = new GameObject("Medal Goals", typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup));
            targets.transform.SetParent(instance.transform, false);
            RectTransform targetsRect = targets.GetComponent<RectTransform>();
            targetsRect.anchorMin = Vector2.zero;
            targetsRect.anchorMax = new Vector2(1f, 0f);
            targetsRect.pivot = new Vector2(0.5f, 0f);
            targetsRect.anchoredPosition = new Vector2(0f, 5f);
            targetsRect.sizeDelta = new Vector2(-12f, 20f);
            UnityEngine.UI.HorizontalLayoutGroup targetLayout = targets.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            targetLayout.spacing = 3f;
            targetLayout.childControlWidth = targetLayout.childControlHeight = true;
            targetLayout.childForceExpandWidth = targetLayout.childForceExpandHeight = false;
            targetLayout.childAlignment = TextAnchor.MiddleLeft;
            float[] heights = { goals.Bronze, goals.Silver, goals.Gold, goals.Platinum };
            for (int i = 0; i < heights.Length; i++)
            {
                CreateMedalIcon(targets.transform, i + 1, 13f);
                GameObject goal = new GameObject("Height", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(UnityEngine.UI.LayoutElement));
                goal.transform.SetParent(targets.transform, false);
                TextMeshProUGUI value = goal.GetComponent<TextMeshProUGUI>();
                value.font = font;
                value.fontSize = 8f;
                value.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                value.raycastTarget = false;
                value.alignment = TextAlignmentOptions.MidlineLeft;
                value.enableWordWrapping = false;
                value.text = heights[i] >= 1000f ? (heights[i] / 1000f).ToString("0.#") + "k" : heights[i].ToString("0") + "m";
                UnityEngine.UI.LayoutElement goalLayout = goal.GetComponent<UnityEngine.UI.LayoutElement>();
                goalLayout.minWidth = goalLayout.preferredWidth = 23f;
                goalLayout.minHeight = goalLayout.preferredHeight = 20f;
            }
        }
        UnityEngine.UI.LayoutElement layout = instance.GetComponent<UnityEngine.UI.LayoutElement>() ?? instance.AddComponent<UnityEngine.UI.LayoutElement>();
        layout.minHeight = layout.preferredHeight = 56f;
        instance.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 56f);
        instance.SetActive(true);
    }

    private static UnityEngine.UI.Image CreateMedalIcon(Transform parent, int medal, float size)
    {
        string[] names = { "Bronze", "Silver", "Gold", "Platinum" };
        GameObject icon = new GameObject("Medal", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
        icon.transform.SetParent(parent, false);
        icon.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);
        UnityEngine.UI.Image image = icon.GetComponent<UnityEngine.UI.Image>();
        image.sprite = BalancedMode.FindMetalSprite(names[Mathf.Clamp(medal - 1, 0, 3)]);
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = image.sprite != null;
        UnityEngine.UI.LayoutElement layout = icon.GetComponent<UnityEngine.UI.LayoutElement>();
        layout.minWidth = layout.preferredWidth = size;
        layout.minHeight = layout.preferredHeight = size;
        return image;
    }

    private static void Select(Presets.PresetData data)
    {
        if (data == null) return;
        runChallenge = null;
        applying = true;
        try
        {
            if (selected != null && string.Equals(selected.name, data.name, StringComparison.OrdinalIgnoreCase))
            {
                selected = null;
                if (previous != null) Presets.Apply(previous);
                previous = null;
            }
            else
            {
                if (selected == null) previous = Presets.Capture("Before challenge");
                selected = data;
                Presets.Apply(data);
            }
        }
        finally { applying = false; }
    }

    public static void PrepareRun()
    {
        if (selected == null) { runChallenge = null; return; }
        applying = true;
        try { Presets.Apply(selected); }
        finally { applying = false; }
        runChallenge = null;
    }

    public static void BeginRun()
    {
        runChallenge = CL_GameManager.GetCurrentGamemode() == EndlessConfiguration.EndlessMode ? selected?.name : null;
    }

    public static void UpdateProgress()
    {
        if (runChallenge == null || CL_GameManager.gMan == null || CL_GameManager.IsLoading() ||
            CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode ||
            !ChallengeGoals.ByName.TryGetValue(runChallenge, out ChallengeGoals.HeightGoals goals)) return;
        float height = Mathf.Max(0f, CL_GameManager.gMan.GetPlayerBestTravelDistance());
        int medal = height >= goals.Platinum ? 4 : height >= goals.Gold ? 3 : height >= goals.Silver ? 2 : height >= goals.Bronze ? 1 : 0;
        ModSaveData.RecordMetal("challenge|" + runChallenge, medal);
    }

    private void LateUpdate()
    {
        if (menu == null) return;
        if (!menu.IsChallengeMenu) showingChallenges = false;
        if (!IsActive) { Unlock(); return; }
        foreach (TemplateRegion region in GetComponentsInChildren<TemplateRegion>(true)) Lock(region.gameObject);
        foreach (OrderingContainer order in GetComponentsInChildren<OrderingContainer>(true)) Lock(order.gameObject);
        foreach (RandomContainer random in GetComponentsInChildren<RandomContainer>(true)) Lock(random.gameObject);
        if (menu.Content != null)
        {
            if (showingChallenges && menu.IsChallengeMenu) Unlock(menu.Content.gameObject);
            else Lock(menu.Content.gameObject);
        }
    }

    private void Lock(GameObject target)
    {
        if (!lockedGroups.TryGetValue(target, out LockedGroup state))
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>() ?? target.AddComponent<CanvasGroup>();
            state = new LockedGroup { Group = group, Interactable = group.interactable, BlocksRaycasts = group.blocksRaycasts };
            lockedGroups[target] = state;
        }
        state.Group.interactable = false;
        state.Group.blocksRaycasts = true;
    }

    private void Unlock(GameObject target)
    {
        if (!lockedGroups.TryGetValue(target, out LockedGroup state)) return;
        if (state.Group != null)
        {
            state.Group.interactable = state.Interactable;
            state.Group.blocksRaycasts = state.BlocksRaycasts;
        }
        lockedGroups.Remove(target);
    }

    private void Unlock()
    {
        foreach (LockedGroup state in lockedGroups.Values)
            if (state.Group != null)
            {
                state.Group.interactable = state.Interactable;
                state.Group.blocksRaycasts = state.BlocksRaycasts;
            }
        lockedGroups.Clear();
    }

    private void OnDisable() { Unlock(); }
    private void OnDestroy() { Unlock(); }

    private sealed class ChallengeTab : MonoBehaviour, IPointerClickHandler
    {
        public Challenges Owner;
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Owner?.OpenMenu();
        }
    }
}

[HarmonyPatch]
public static class ChallengeInteractionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        Type[] settings = { typeof(TemplateBool), typeof(TemplateFloat), typeof(TemplateInteger) };
        foreach (Type type in settings) yield return AccessTools.Method(type, "UpdateSettingState");
        yield return AccessTools.Method(typeof(TemplateRegion), "OnPointerClick");
        yield return AccessTools.Method(typeof(RemoveOnClick), "OnPointerClick");
        yield return AccessTools.Method(typeof(OrderingContainer), "AddSlot");
        yield return AccessTools.Method(typeof(OrderingContainer), "RemoveSlotAt");
        yield return AccessTools.Method(typeof(OrderingContainer), "ReplaceSlots");
        yield return AccessTools.Method(typeof(SubTabContainer), "SetRandomizable");
        yield return AccessTools.Method(typeof(Presets), "Apply");
        yield return AccessTools.Method(typeof(MenuSettingsManager), "ResetConfiguredSettings");
        string[] methods = { "SetBool", "SetInt", "SetFloat", "SetString", "RestoreSettings", "ClearSlots", "add_slot_to_end", "remove_slot", "SetActiveSlotCount" };
        foreach (string method in methods) yield return AccessTools.Method(typeof(UISettingsManager), method);
    }

    private static bool Prefix() { return !Challenges.IsLocked; }
}

[HarmonyPatch(typeof(WorldLoader), nameof(WorldLoader.Initialize))]
public static class ChallengeRunStartPatch
{
    private static void Postfix() { Challenges.BeginRun(); }
}

[HarmonyPatch(typeof(CL_GameManager), "Update")]
public static class ChallengeProgressPatch
{
    private static void Postfix() { Challenges.UpdateProgress(); }
}
