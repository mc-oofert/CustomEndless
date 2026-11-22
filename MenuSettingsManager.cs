using Celestial.CustomEndless;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum SettingType
{
    Integer,
    Float,
    Bool
}

public class MenuSettingsManager : MonoBehaviour
{
    private GameObject templateFloat;
    private GameObject templateInteger;
    private GameObject templateBool;
    private GameObject templateDropdown;

    private UISettingsManager settingsManager;
    private Transform contentContainer;

    private class SettingData
    {
        public string menuTag;
        public string displayName;
        public string settingTag;
        public SettingType settingType;
        public string defaultValue;
        public string dropdownName;
    }

    private List<SettingData> registeredSettings = new List<SettingData>();
    private List<GameObject> activeInstances = new List<GameObject>();
    private string activeMenuTag;
    public bool IsChallengeMenu => string.Equals(activeMenuTag, "Challenges", StringComparison.OrdinalIgnoreCase);
    public Transform Content => contentContainer;
    public string ActiveMenuTag => activeMenuTag;
    private Transform controls;
    private RectTransform regionViewport;
    private Coroutine pendingLayoutRefresh;
    private static readonly Dictionary<string, bool> boolDefaults = new Dictionary<string, bool>();
    private static readonly Dictionary<string, int> intDefaults = new Dictionary<string, int>();
    private static readonly Dictionary<string, float> floatDefaults = new Dictionary<string, float>();

    private void Awake()
    {
        settingsManager = transform.parent?.GetComponent<UISettingsManager>();
        templateFloat = transform.Find("TemplateFloat")?.gameObject;
        templateInteger = transform.Find("TemplateInteger")?.gameObject;
        templateBool = transform.Find("TemplateBool")?.gameObject;
        templateDropdown = transform.Find("TemplateDropdown")?.gameObject;

        SetupContentContainer();
    }

    private void SetupContentContainer()
    {
        Transform existingContent = transform.Find("Content");
        if (existingContent != null)
        {
            contentContainer = existingContent;
        }
        else
        {
            GameObject contentObj = new GameObject("Content", typeof(RectTransform));
            contentContainer = contentObj.transform;
            contentContainer.SetParent(transform, false);

            RectTransform rt = contentContainer.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0, rt.sizeDelta.y);

            VerticalLayoutGroup parentLayout = GetComponent<VerticalLayoutGroup>();
            if (parentLayout != null)
            {
                VerticalLayoutGroup childLayout = contentObj.AddComponent<VerticalLayoutGroup>();
                childLayout.padding = parentLayout.padding;
                childLayout.spacing = parentLayout.spacing;
                childLayout.childAlignment = parentLayout.childAlignment;
                childLayout.childControlWidth = parentLayout.childControlWidth;
                childLayout.childControlHeight = parentLayout.childControlHeight;
                childLayout.childScaleWidth = parentLayout.childScaleWidth;
                childLayout.childScaleHeight = parentLayout.childScaleHeight;
                childLayout.childForceExpandWidth = parentLayout.childForceExpandWidth;
                childLayout.childForceExpandHeight = parentLayout.childForceExpandHeight;
                childLayout.reverseArrangement = parentLayout.reverseArrangement;

                ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        RectTransform contentRect = contentContainer as RectTransform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, contentRect.sizeDelta.y);
        contentRect.anchoredPosition = Vector2.zero;
        VerticalLayoutGroup contentGroup = contentContainer.GetComponent<VerticalLayoutGroup>();
        if (contentGroup == null) contentGroup = contentContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        contentGroup.childAlignment = TextAnchor.UpperLeft;
        contentGroup.childControlWidth = contentGroup.childControlHeight = true;
        contentGroup.childForceExpandWidth = true;
        contentGroup.childForceExpandHeight = false;
        ContentSizeFitter contentFitter = contentContainer.GetComponent<ContentSizeFitter>();
        if (contentFitter == null) contentFitter = contentContainer.gameObject.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (TryGetComponent(out ScrollRect scrollRect))
        {
            LayoutElement contentLayout = contentContainer.GetComponent<LayoutElement>() ?? contentContainer.gameObject.AddComponent<LayoutElement>();
            contentLayout.ignoreLayout = true;
            scrollRect.content = contentContainer.GetComponent<RectTransform>();
            scrollRect.viewport = GetComponent<RectTransform>();
        }

        if (GetComponent<RectMask2D>() == null && GetComponent<Mask>() == null)
        {
            gameObject.AddComponent<RectMask2D>();
        }
    }

    public void Register(string menuTag, string displayName, string settingTag, SettingType settingType, string defaultValue, string dropdownName = "")
    {
        registeredSettings.RemoveAll(data => data.menuTag == menuTag && data.settingTag == settingTag);
        registeredSettings.Add(new SettingData
        {
            menuTag = menuTag,
            displayName = displayName,
            settingTag = settingTag,
            settingType = settingType,
            defaultValue = defaultValue,
            dropdownName = dropdownName
        });

        if (settingType == SettingType.Bool && bool.TryParse(defaultValue, out bool boolDefault)) boolDefaults[settingTag] = boolDefault;
        if (settingType == SettingType.Integer && int.TryParse(defaultValue, out int intDefault)) intDefaults[settingTag] = intDefault;
        if (settingType == SettingType.Float && float.TryParse(defaultValue, out float floatDefault)) floatDefaults[settingTag] = floatDefault;

        UISettingsManager manager = settingsManager ?? UISettingsManager.instance;
        if (manager == null) return;
        if (settingType == SettingType.Integer && int.TryParse(defaultValue, out int intVal) && !manager.HasInt(settingTag)) manager.SetInt(settingTag, EndlessConfiguration.CustomUIManager.SettingCounts.TryGetValue(settingTag, out int savedInt) ? savedInt : intVal);
        if (settingType == SettingType.Float && float.TryParse(defaultValue, out float floatVal) && !manager.HasFloat(settingTag)) manager.SetFloat(settingTag, EndlessConfiguration.CustomUIManager.SettingFloats.TryGetValue(settingTag, out float savedFloat) ? savedFloat : floatVal);
        if (settingType == SettingType.Bool && bool.TryParse(defaultValue, out bool boolVal) && !manager.HasBool(settingTag)) manager.SetBool(settingTag, EndlessConfiguration.CustomUIManager.SettingToggles.TryGetValue(settingTag, out bool savedBool) ? savedBool : boolVal);
    }

    public void ResetConfiguredSettings(string excludedTag)
    {
        UISettingsManager manager = settingsManager ?? UISettingsManager.instance;
        if (manager == null) return;
        for (int i = 0; i < registeredSettings.Count; i++)
        {
            SettingData data = registeredSettings[i];
            if (string.Equals(data.settingTag, excludedTag, StringComparison.OrdinalIgnoreCase)) continue;
            if (data.settingType == SettingType.Bool && bool.TryParse(data.defaultValue, out bool boolValue))
            {
                manager.SetBool(data.settingTag, boolValue);
                EndlessConfiguration.CustomUIManager.SettingToggles[data.settingTag] = boolValue;
            }
            if (data.settingType == SettingType.Integer && int.TryParse(data.defaultValue, out int intValue))
            {
                manager.SetInt(data.settingTag, intValue);
                EndlessConfiguration.CustomUIManager.SettingCounts[data.settingTag] = intValue;
            }
            if (data.settingType == SettingType.Float && float.TryParse(data.defaultValue, out float floatValue))
            {
                manager.SetFloat(data.settingTag, floatValue);
                EndlessConfiguration.CustomUIManager.SettingFloats[data.settingTag] = floatValue;
            }
        }
    }

    public Transform BeginCustomMenu(string menuTag)
    {
        if (menuTag == "Presets") BalancedMode.EnableFullCustom();
        if (contentContainer == null) SetupContentContainer();
        if (string.Equals(activeMenuTag, menuTag, StringComparison.OrdinalIgnoreCase))
        {
            ClearMenu();
            return null;
        }
        ReleaseRegionFoot();
        DestroyMenuInstances();
        RefreshContentLayout(true);
        activeMenuTag = menuTag;
        Transform source = GetControls();
        source?.gameObject.SetActive(false);
        return contentContainer;
    }

    public Transform CreateRegionFoot()
    {
        GameObject viewport = new GameObject("Region Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(LayoutElement));
        viewport.transform.SetParent(transform, false);
        viewport.GetComponent<LayoutElement>().ignoreLayout = true;
        regionViewport = viewport.GetComponent<RectTransform>();
        regionViewport.anchorMin = Vector2.zero;
        regionViewport.anchorMax = Vector2.one;
        regionViewport.offsetMin = new Vector2(0f, 82f);
        regionViewport.offsetMax = Vector2.zero;
        contentContainer.SetParent(regionViewport, false);
        RectTransform content = contentContainer as RectTransform;
        if (content != null) content.anchoredPosition = Vector2.zero;
        if (TryGetComponent(out ScrollRect scroll))
        {
            scroll.StopMovement();
            scroll.viewport = regionViewport;
        }
        TrackInstance(viewport);
        GameObject Foot = new GameObject("Region Foot", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        Foot.transform.SetParent(transform, false);
        Foot.GetComponent<LayoutElement>().ignoreLayout = true;
        RectTransform rect = Foot.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 6f);
        rect.sizeDelta = new Vector2(0f, 70f);
        VerticalLayoutGroup layout = Foot.GetComponent<VerticalLayoutGroup>();
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 4f;
        TrackInstance(Foot);
        return Foot.transform;
    }

    private void ReleaseRegionFoot()
    {
        if (regionViewport == null) return;
        contentContainer.SetParent(transform, false);
        if (contentContainer is RectTransform content) content.anchoredPosition = Vector2.zero;
        if (TryGetComponent(out ScrollRect scroll))
        {
            scroll.StopMovement();
            scroll.viewport = GetComponent<RectTransform>();
        }
        regionViewport = null;
    }

    public GameObject CreateIntegerInstance(Transform parent)
    {
        if (templateInteger == null || parent == null) return null;
        GameObject instance = Instantiate(templateInteger, parent);
        SetSettingRowHeight(instance);
        activeInstances.Add(instance);
        return instance;
    }

    private static void SetSettingRowHeight(GameObject instance)
    {
        LayoutElement size = instance.GetComponent<LayoutElement>() ?? instance.AddComponent<LayoutElement>();
        size.minHeight = size.preferredHeight = 32f;
        size.flexibleHeight = 0f;
    }

    public TMP_Text GetTextTemplate(bool button)
    {
        GameObject template = button ? templateDropdown : templateInteger;
        return template?.transform.Find("Title")?.GetComponent<TMP_Text>();
    }

    public void StylePanel(Image image)
    {
        Image source = templateDropdown?.GetComponent<Image>();
        if (source == null && templateInteger != null) source = templateInteger.GetComponent<Image>();
        if (source == null) return;
        image.sprite = source.sprite;
        image.type = source.type;
        image.material = source.material;
        image.color = source.color;
        image.fillCenter = source.fillCenter;
        image.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        image.preserveAspect = source.preserveAspect;
    }

    public void TrackInstance(GameObject instance)
    {
        if (instance != null) activeInstances.Add(instance);
    }

    public Presets.PresetData CaptureMenuSettings(string menuTag, int slot = -1)
    {
        Presets.PresetData result = new Presets.PresetData { name = menuTag };
        UISettingsManager manager = settingsManager ?? UISettingsManager.instance;
        foreach (SettingData data in registeredSettings)
        {
            if (data.menuTag != menuTag) continue;
            string key = data.settingTag;
            if (slot >= 0)
            {
                string prefix = menuTag.ToLowerInvariant() + "_";
                key = "slot_" + slot + "_" + (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? key.Substring(prefix.Length) : key);
            }
            if (data.settingType == SettingType.Integer)
            {
                int.TryParse(data.defaultValue, out int fallback);
                result.intKeys.Add(data.settingTag);
                result.intValues.Add(manager != null ? manager.GetInt(key, fallback) : EndlessConfiguration.CustomUIManager.GetCountState(key, fallback));
            }
            else if (data.settingType == SettingType.Float)
            {
                float.TryParse(data.defaultValue, out float fallback);
                result.floatKeys.Add(data.settingTag);
                result.floatValues.Add(manager != null ? manager.GetFloat(key, fallback) : EndlessConfiguration.CustomUIManager.SettingFloats.TryGetValue(key, out float value) ? value : fallback);
            }
            else
            {
                bool.TryParse(data.defaultValue, out bool fallback);
                result.boolKeys.Add(data.settingTag);
                result.boolValues.Add(manager != null ? manager.GetBool(key, fallback) : EndlessConfiguration.CustomUIManager.GetToggleState(key));
            }
        }
        return result;
    }

    public void SetMenuDefaults(string menuTag, Presets.PresetData values)
    {
        foreach (SettingData data in registeredSettings)
        {
            if (data.menuTag != menuTag) continue;
            int index;
            if (data.settingType == SettingType.Integer && (index = values.intKeys.IndexOf(data.settingTag)) >= 0)
            {
                intDefaults[data.settingTag] = values.intValues[index];
                data.defaultValue = values.intValues[index].ToString();
            }
            else if (data.settingType == SettingType.Float && (index = values.floatKeys.IndexOf(data.settingTag)) >= 0)
            {
                floatDefaults[data.settingTag] = values.floatValues[index];
                data.defaultValue = values.floatValues[index].ToString();
            }
            else if (data.settingType == SettingType.Bool && (index = values.boolKeys.IndexOf(data.settingTag)) >= 0)
            {
                boolDefaults[data.settingTag] = values.boolValues[index];
                data.defaultValue = values.boolValues[index].ToString();
            }
        }
    }

    public void UnregisterMenu(string menuTag)
    {
        registeredSettings.RemoveAll(data => data.menuTag == menuTag);
    }
    public void LoadBalancedMenu(string menuTag, string requirements)
    {
        if (string.Equals(activeMenuTag, menuTag, StringComparison.OrdinalIgnoreCase))
        {
            ClearMenu();
            return;
        }
        LoadMenu(menuTag);
        if (contentContainer == null) return;
        Transform source = GetControls();
        if (source == null) return;
        GameObject instance = Instantiate(source.gameObject, contentContainer);
        instance.name = "Requirements";
        float height = 50f;
        TMP_Text[] tmps = instance.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmps.Length; i++)
        {
            tmps[i].text = requirements;
            tmps[i].overflowMode = TextOverflowModes.Overflow;
            height = Mathf.Max(height, tmps[i].preferredHeight + 12f);
        }
        Text[] texts = instance.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            texts[i].text = requirements;
            texts[i].horizontalOverflow = HorizontalWrapMode.Overflow;
            texts[i].verticalOverflow = VerticalWrapMode.Overflow;
            height = Mathf.Max(height, texts[i].preferredHeight + 12f);
        }
        LayoutElement element = instance.GetComponent<LayoutElement>();
        if (element == null) element = instance.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
        instance.SetActive(true);
        instance.transform.SetAsFirstSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer as RectTransform);
        activeInstances.Add(instance);
    }

    private Transform GetControls()
    {
        if (controls != null) return controls;
        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null) return null;
        Transform[] transforms = canvas.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++) if (string.Equals(transforms[i].name, "Controls", StringComparison.OrdinalIgnoreCase)) return controls = transforms[i];
        return null;
    }

    private void ClearMenu()
    {
        ReleaseRegionFoot();
        DestroyMenuInstances();
        RefreshContentLayout(true);
        activeMenuTag = null;
        Transform source = GetControls();
        source?.gameObject.SetActive(true);
    }

    public static bool IsDefaultBool(string settingTag, bool value)
    {
        return boolDefaults.TryGetValue(settingTag, out bool defaultValue) && value == defaultValue;
    }

    public static bool IsDefaultInt(string settingTag, int value)
    {
        return intDefaults.TryGetValue(settingTag, out int defaultValue) && value == defaultValue;
    }

    public static bool IsDefaultFloat(string settingTag, float value)
    {
        return floatDefaults.TryGetValue(settingTag, out float defaultValue) && value == defaultValue;
    }

    public void LoadMenu(string menuTag, int slot = -1)
    {
        if (Celestial.CustomEndless.Plugin.DumbItDown && (CustomRegions.IsCustom(menuTag) || menuTag == CustomRegions.TemplateName)) return;
        if (!menuTag.EndsWith("-BALANCED", StringComparison.OrdinalIgnoreCase)) BalancedMode.EnableFullCustom();
        if (contentContainer == null) SetupContentContainer();
        if (string.Equals(activeMenuTag, menuTag, StringComparison.OrdinalIgnoreCase))
        {
            ClearMenu();
            return;
        }
        ReleaseRegionFoot();
        DestroyMenuInstances();
        RefreshContentLayout(true);
        activeMenuTag = menuTag;
        Transform source = GetControls();
        source?.gameObject.SetActive(false);

        RegionFlavorText.Show(this, menuTag);
        Dictionary<string, Transform> activeDropdowns = new Dictionary<string, Transform>();

        foreach (SettingData data in registeredSettings)
        {
            if (data.menuTag != menuTag) continue;
            var startsEnabled = true;
            if (CustomRegions.IsCustom(menuTag) && data.settingTag.EndsWith("_ordered", StringComparison.Ordinal)) continue;
            Transform parentTransform = contentContainer;

            if (!string.IsNullOrEmpty(data.dropdownName) && templateDropdown != null)
            {
                startsEnabled = false;
                string path = string.Empty;
                foreach (string part in data.dropdownName.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    path = string.IsNullOrEmpty(path) ? part : path + "/" + part;
                    if (!activeDropdowns.TryGetValue(path, out Transform dropdown))
                    {
                        GameObject instance = Instantiate(templateDropdown, parentTransform);
                        instance.name = part;
                        VerticalLayoutGroup group = instance.GetComponent<VerticalLayoutGroup>() ?? instance.AddComponent<VerticalLayoutGroup>();
                        group.childControlWidth = group.childControlHeight = true;
                        group.childForceExpandWidth = true;
                        group.childForceExpandHeight = false;
                        LayoutElement dropdownSize = instance.GetComponent<LayoutElement>() ?? instance.AddComponent<LayoutElement>();
                        dropdownSize.minHeight = 32f;
                        dropdownSize.preferredHeight = -1f;
                        dropdownSize.flexibleHeight = 0f;
                        Transform title = instance.transform.Find("Title");
                        if (title != null)
                        {
                            LayoutElement titleSize = title.GetComponent<LayoutElement>() ?? title.gameObject.AddComponent<LayoutElement>();
                            titleSize.ignoreLayout = false;
                            titleSize.minHeight = titleSize.preferredHeight = 32f;
                            titleSize.flexibleHeight = 0f;
                            if (title.TryGetComponent(out TMP_Text text)) text.text = part;
                            else if (title.TryGetComponent(out Text legacy)) legacy.text = part;
                        }
                        instance.SetActive(parentTransform == contentContainer);
                        dropdown = instance.transform;
                        activeDropdowns[path] = dropdown;
                        if (parentTransform == contentContainer) activeInstances.Add(instance);
                    }
                    parentTransform = dropdown;
                }
            }

            GameObject templateToUse = null;
            switch (data.settingType)
            {
                case SettingType.Integer: templateToUse = templateInteger; break;
                case SettingType.Float: templateToUse = templateFloat; break;
                case SettingType.Bool: templateToUse = templateBool; break;
            }

            if (templateToUse != null)
            {
                GameObject settingInstance = Instantiate(templateToUse, parentTransform);
                SetSettingRowHeight(settingInstance);

                MonoBehaviour comp = null;
                object parsedDefault = null;

                string currentSettingTag = data.settingTag;
                if (slot != -1)
                {
                    string tagWithoutRegion = currentSettingTag;

                    string regionKey = data.menuTag.ToLower() + "_";
                    if (tagWithoutRegion.StartsWith(regionKey, StringComparison.OrdinalIgnoreCase))
                    {
                        tagWithoutRegion = tagWithoutRegion.Substring(regionKey.Length);
                    }

                    currentSettingTag = $"slot_{slot}_{tagWithoutRegion}";
                }

                if (data.settingType == SettingType.Integer)
                {
                    comp = settingInstance.GetComponent<TemplateInteger>();
                    int fallback = int.TryParse(data.defaultValue, out int defInt) ? defInt : 0;
                    parsedDefault = settingsManager != null ? settingsManager.GetInt(currentSettingTag, fallback) : fallback;
                }
                else if (data.settingType == SettingType.Float)
                {
                    comp = settingInstance.GetComponent<TemplateFloat>();
                    float fallback = float.TryParse(data.defaultValue, out float defFloat) ? defFloat : 0f;
                    parsedDefault = settingsManager != null ? settingsManager.GetFloat(currentSettingTag, fallback) : fallback;
                }
                else if (data.settingType == SettingType.Bool)
                {
                    comp = settingInstance.GetComponent<TemplateBool>();
                    bool fallback = bool.TryParse(data.defaultValue, out bool defBool) ? defBool : false;
                    parsedDefault = settingsManager != null ? settingsManager.GetBool(currentSettingTag, fallback) : fallback;
                }

                if (comp != null)
                {
                    SetPrivateField(comp, "settingName", data.displayName);
                    SetPrivateField(comp, "settingsTag", currentSettingTag);
                    SetPrivateField(comp, "defaultValue", parsedDefault);
                }

                if (CustomRegions.IsCustom(menuTag) && currentSettingTag.EndsWith("_level_count", StringComparison.Ordinal))
                {
                    CustomLevelCountLock countLock = settingInstance.AddComponent<CustomLevelCountLock>();
                    countLock.OrderedKey = currentSettingTag.Substring(0, currentSettingTag.Length - "_level_count".Length) + "_ordered";
                    countLock.CountKey = currentSettingTag;
                    if (CustomRegions.TryGet(menuTag, out CustomRegions.RegionData custom))
                        countLock.Subregion = custom.subregions.Find(sub => data.settingTag == "subregion_" + sub.id + "_level_count");
                }

                settingInstance.SetActive(startsEnabled);
                if (data.settingType == SettingType.Bool && data.menuTag.EndsWith("-BALANCED", StringComparison.OrdinalIgnoreCase))
                {
                    string region = data.menuTag.Substring(0, data.menuTag.Length - "-BALANCED".Length);
                    BalancedMode.AddToggleMetal(settingInstance.transform, region, data.settingTag);
                }

                if (string.IsNullOrEmpty(data.dropdownName))
                {
                    activeInstances.Add(settingInstance);
                }
            }
        }
        foreach (Transform dropdown in activeDropdowns.Values)
        {
            CollapsibleContainer collapsible = dropdown.GetComponent<CollapsibleContainer>();
            if (collapsible == null) collapsible = dropdown.gameObject.AddComponent<CollapsibleContainer>();
            collapsible.SetExpanded(false);
        }
        CustomRegionEditor.MakeIt(this, menuTag, slot);
        RefreshContentLayout(true);
        if (pendingLayoutRefresh != null) StopCoroutine(pendingLayoutRefresh);
        if (isActiveAndEnabled) pendingLayoutRefresh = StartCoroutine(FinishMenuLayout());
    }

    private void DestroyMenuInstances()
    {
        if (pendingLayoutRefresh != null)
        {
            StopCoroutine(pendingLayoutRefresh);
            pendingLayoutRefresh = null;
        }
        foreach (GameObject instance in activeInstances)
        {
            if (instance == null) continue;
            instance.SetActive(false);
            Destroy(instance);
        }
        activeInstances.Clear();
    }

    private IEnumerator FinishMenuLayout()
    {
        yield return null;
        pendingLayoutRefresh = null;
        RefreshContentLayout(true);
    }

    public void RefreshContentLayout(bool resetScroll = false)
    {
        RectTransform content = contentContainer as RectTransform;
        if (content == null) return;
        Vector2 previousPosition = content.anchoredPosition;
        RectTransform[] children = content.GetComponentsInChildren<RectTransform>(false);
        for (int i = children.Length - 1; i >= 0; i--)
        {
            RectTransform child = children[i];
            CollapsibleContainer dropdown = child.GetComponent<CollapsibleContainer>();
            dropdown?.RefreshHeight();
            if (child.GetComponent<LayoutGroup>() != null) LayoutRebuilder.ForceRebuildLayoutImmediate(child);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();
        ScrollRect scroll = GetComponent<ScrollRect>();
        if (scroll == null) return;
        scroll.StopMovement();
        RectTransform viewport = scroll.viewport ?? transform as RectTransform;
        float maximum = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        Vector2 position = previousPosition;
        position.x = 0f;
        position.y = resetScroll ? 0f : Mathf.Clamp(position.y, 0f, maximum);
        content.anchoredPosition = position;
    }

    private void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(target, value);
    }
}
