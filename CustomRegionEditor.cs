using Celestial.CustomEndless;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

// It is here that you start to see my refusal to make a langauge file, and now you must pay for my actions as I have.

public static class CustomRegionEditor
{
    private static readonly HashSet<string> collapsed = new HashSet<string>();
    public static void MakeIt(MenuSettingsManager menu, string tag, int slot)
    {
        if (Plugin.DumbItDown) return;
        if (tag.EndsWith("-BALANCED", StringComparison.OrdinalIgnoreCase)) return;
        string region = tag.EndsWith("-BALANCED", StringComparison.OrdinalIgnoreCase) ? tag.Substring(0, tag.Length - 9) : tag;
        if (!SubTabContainer.CachedRegions.ContainsKey(region) || region == CustomRegions.TemplateName) return;
        AddCopyButton(menu, region, slot);
        if (!CustomRegions.TryGet(region, out CustomRegions.RegionData data))
        {
            Row(menu, "Foot Space");
            return;
        }
        if (slot >= 0)
        {
            foreach (CustomRegions.SubregionData sub in data.subregions) AddModeButton(menu, sub, slot);
            Row(menu, "Foot Space");
            return;
        }
        CustomRegions.RefreshCatalog();
        AddInfo(menu, "Editor Help", "Name your region, choose its transitions, then add subregions and levels. Save stores your region and makes the current settings its defaults.");
        GameObject nameRow = Row(menu, "Region Name");
        AddLabel(nameRow.transform, "Name", 40f);
        TMP_InputField regionName = Input(nameRow.transform, data.name, "Region name");
        regionName.onEndEdit.AddListener(value =>
        {
            if (Challenges.IsLocked || string.IsNullOrWhiteSpace(value)) { regionName.SetTextWithoutNotify(data.name); return; }
            if (data.name == value.Trim()) return;
            data.name = value.Trim();
            Refresh(menu, data);
        });
        GameObject entering = Row(menu, "Entrance Region");
        AddLabel(entering.transform, "Entrance Transition", 0f);
        Autocomplete(menu, "Entrance region", EndlessConfiguration.GetCustomTransitionChoices, id => id, value =>
        {
            if (!Challenges.IsLocked && EndlessConfiguration.GetCustomTransitionChoices().Contains(value)) data.enterAs = value;
        }, "Set Entrance", data.enterAs, "Set");
        GameObject exiting = Row(menu, "Exit Region");
        AddLabel(exiting.transform, "Exit Transition", 0f);
        Autocomplete(menu, "Exit region", EndlessConfiguration.GetCustomTransitionChoices, id => id, value =>
        {
            if (!Challenges.IsLocked && EndlessConfiguration.GetCustomTransitionChoices().Contains(value)) data.exitAs = value;
        }, "Set Exit", data.exitAs, "Set");
        GameObject actions = Row(menu, "Custom Region Actions");
        TMP_Text status = null;
        Button(actions.transform, "Save", () =>
        {
            bool saved = CustomRegions.Save(data, menu);
            if (status != null) status.text = saved ? "Saved" : "Couldn't Save";
        });
        bool confirmDelete = false;
        UnityEngine.UI.Button delete = null;
        delete = Button(actions.transform, "Delete", () =>
        {
            if (Challenges.IsLocked) return;
            if (!confirmDelete)
            {
                confirmDelete = true;
                delete.GetComponentInChildren<TMP_Text>().text = "Confirm Delete";
                return;
            }
            CustomRegions.Delete(data, menu);
            menu.BeginCustomMenu(data.id);
        });
        GameObject folder = Row(menu, "Custom Region Folder");
        Button(folder.transform, "Copy Folder Path", () =>
        {
            GUIUtility.systemCopyBuffer = ModDataPaths.CustomRegionsDirectory;
            if (status != null) status.text = "Folder path copied";
        });
        GameObject message = Row(menu, "Custom Region Status");
        status = AddLabel(message.transform, "", 0f);
        if (data.subregions.Count == 0) AddInfo(menu, "Empty Region", "This region has no subregions yet. Add a blank subregion or copy one from the game in the menu below.");
        foreach (CustomRegions.SubregionData sub in data.subregions)
        {
            HashSet<Transform> previousRows = new HashSet<Transform>();
            foreach (Transform child in menu.Content) previousRows.Add(child);
            GameObject heading = Row(menu, "Subregion " + sub.id);
            TMP_InputField subName = Input(heading.transform, sub.name, "Subregion name");
            subName.onEndEdit.AddListener(value =>
            {
                if (Challenges.IsLocked || string.IsNullOrWhiteSpace(value)) { subName.SetTextWithoutNotify(sub.name); return; }
                if (sub.name == value.Trim()) return;
                sub.name = value.Trim();
                Refresh(menu, data);
            });
            GameObject subActions = Row(menu, "Subregion Actions");
            Button(subActions.transform, "Up", () => Move(data.subregions, sub, -1, menu, data), 35f).interactable = data.subregions.IndexOf(sub) > 0;
            Button(subActions.transform, "Down", () => Move(data.subregions, sub, 1, menu, data), 40f).interactable = data.subregions.IndexOf(sub) < data.subregions.Count - 1;
            Button(subActions.transform, "Remove", () =>
            {
                if (Challenges.IsLocked) return;
                data.subregions.Remove(sub);
                Refresh(menu, data);
            }, 60f);
            AddModeButton(menu, sub, -1);
            AddInfo(menu, "Level List Info", sub.levels.Count == 0 ? "No levels yet. Type a level ID below or choose a search result." : sub.levels.Count + "levels. Ordered uses every level in this list; random chooses the requested count.");
            for (int i = 0; i < sub.levels.Count; i++)
            {
                int index = i;
                GameObject levelRow = Row(menu, "Level " + i);
                TMP_InputField levelId = Input(levelRow.transform, sub.levels[i], "Level ID");
                levelId.readOnly = true;
                Button(levelRow.transform, "^", () => MoveLevel(sub, index, -1, menu, data), 22f);
                Button(levelRow.transform, "v", () => MoveLevel(sub, index, 1, menu, data), 22f);
                Button(levelRow.transform, "X", () =>
                {
                    if (Challenges.IsLocked) return;
                    sub.levels.RemoveAt(index);
                    Refresh(menu, data);
                }, 22f);
            }
            Autocomplete(menu, "Level ID", () => CustomRegions.LevelIds, id => id, id =>
            {
                if (Challenges.IsLocked) return;
                if (CustomRegions.ResolveLevel(id) == null) { status.text = "Level ID not found: " + id; return; }
                sub.levels.Add(id);
                Refresh(menu, data);
            }, "Add Level", "", "Add");
            GroupSubregion(menu, sub, heading, previousRows);
        }
        Autocomplete(menu, "Existing subregion", () => CustomRegions.SubregionIds, CustomRegions.SubregionTitle, id =>
        {
            if (Challenges.IsLocked) return;
            if (CustomRegions.GetSourceSubregion(id) == null) { status.text = "Subregion not found: " + id; return; }
            CustomRegions.AddSubregionData(data, id);
            Refresh(menu, data);
        }, "Add Subregion");
        GameObject blank = Row(menu, "Blank Subregion");
        Button(blank.transform, "Add Blank Subregion", () =>
        {
            if (Challenges.IsLocked) return;
            CustomRegions.AddSubregionData(data, null);
            Refresh(menu, data);
        });
        Row(menu, "Foot Space");
    }

    private static void Refresh(MenuSettingsManager menu, CustomRegions.RegionData data)
    {
        RectTransform content = menu.Content as RectTransform;
        Vector2 position = content != null ? content.anchoredPosition : Vector2.zero;
        CustomRegions.EnsureView(data, menu);
        menu.BeginCustomMenu(data.id);
        menu.LoadMenu(data.id);
        if (content != null)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            content.anchoredPosition = position;
        }
    }

    internal static void AddInfo(MenuSettingsManager menu, string name, string value)
    {
        GameObject row = Row(menu, name);
        TMP_Text text = AddLabel(row.transform, value, 0f);
        text.fontSize = 11f;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = new Color(0.72f, 0.72f, 0.72f, 1f);
        float width = menu.Content is RectTransform rect ? Mathf.Max(100f, rect.rect.width - 20f) : 200f;
        float height = Mathf.Max(32f, text.GetPreferredValues(value, width, float.PositiveInfinity).y + 12f);
        UnityEngine.UI.LayoutElement layout = row.GetComponent<UnityEngine.UI.LayoutElement>();
        layout.minHeight = layout.preferredHeight = height;
        row.AddComponent<CustomInfoLayout>().Text = text;
    }

    private static void GroupSubregion(MenuSettingsManager menu, CustomRegions.SubregionData sub, GameObject heading, HashSet<Transform> previousRows)
    {
        List<Transform> rows = new List<Transform>();
        foreach (Transform child in menu.Content) if (!previousRows.Contains(child)) rows.Add(child);
        GameObject card = new GameObject("Subregion Card", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.VerticalLayoutGroup));
        card.transform.SetParent(menu.Content, false);
        card.GetComponent<UnityEngine.UI.Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.8f);
        card.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        UnityEngine.UI.VerticalLayoutGroup layout = card.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 3f;
        layout.padding = new RectOffset(4, 4, 5, 5);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        foreach (Transform row in rows) row.SetParent(card.transform, false);
        Transform settings = menu.Content.Find(sub.name.ToUpperInvariant());
        settings?.SetParent(card.transform, false);
        menu.TrackInstance(card);
        Dictionary<GameObject, bool> visible = new Dictionary<GameObject, bool>();
        UnityEngine.UI.Button toggle = null;
        Action apply = () =>
        {
            bool hide = collapsed.Contains(sub.id);
            foreach (Transform row in card.transform)
            {
                if (row == heading.transform) continue;
                if (hide)
                {
                    if (!visible.ContainsKey(row.gameObject)) visible[row.gameObject] = row.gameObject.activeSelf;
                    row.gameObject.SetActive(false);
                }
                else if (visible.TryGetValue(row.gameObject, out bool active)) row.gameObject.SetActive(active);
            }
            if (toggle != null) toggle.GetComponentInChildren<TMP_Text>().text = hide ? "Show" : "Hide";
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(menu.Content as RectTransform);
        };
        toggle = Button(heading.transform, "Hide", () =>
        {
            if (!collapsed.Add(sub.id)) collapsed.Remove(sub.id);
            apply();
        }, 40f);
        apply();
    }

    private static void AddModeButton(MenuSettingsManager menu, CustomRegions.SubregionData sub, int slot)
    {
        string key = (slot >= 0 ? "slot_" + slot + "_" : "") + "subregion_" + sub.id + "_ordered";
        GameObject row = Row(menu, "Generation " + sub.id);
        AddLabel(row.transform, slot >= 0 ? sub.name : "Level order", 0f);
        UnityEngine.UI.Button button = null;
        Action update = () =>
        {
            bool ordered = UISettingsManager.instance != null ? UISettingsManager.instance.GetBool(key, true) : true;
            if (button != null) button.GetComponentInChildren<TMP_Text>().text = ordered ? "Ordered" : "Random";
        };
        button = Button(row.transform, "Ordered", () =>
        {
            if (Challenges.IsLocked || UISettingsManager.instance == null) return;
            bool ordered = UISettingsManager.instance.GetBool(key, true);
            UISettingsManager.instance.SetBool(key, !ordered);
            EndlessConfiguration.CustomUIManager.SettingToggles[key] = !ordered;
            update();
        }, 75f);
        update();
        foreach (CustomLevelCountLock count in menu.Content.GetComponentsInChildren<CustomLevelCountLock>(true))
        {
            if (count.OrderedKey != key) continue;
            count.transform.SetParent(row.transform.parent, false);
            count.transform.SetSiblingIndex(row.transform.GetSiblingIndex() + 1);
            count.gameObject.SetActive(true);
            break;
        }
    }

    private static void Move<T>(List<T> list, T item, int direction, MenuSettingsManager menu, CustomRegions.RegionData data)
    {
        if (Challenges.IsLocked) return;
        int index = list.IndexOf(item);
        int target = index + direction;
        if (index < 0 || target < 0 || target >= list.Count) return;
        list.RemoveAt(index);
        list.Insert(target, item);
        Refresh(menu, data);
    }

    private static void MoveLevel(CustomRegions.SubregionData sub, int index, int direction, MenuSettingsManager menu, CustomRegions.RegionData data)
    {
        if (Challenges.IsLocked) return;
        int target = index + direction;
        if (target < 0 || target >= sub.levels.Count) return;
        string swap = sub.levels[index]; sub.levels[index] = sub.levels[target]; sub.levels[target] = swap;
        Refresh(menu, data);
    }

    private static void AddCopyButton(MenuSettingsManager menu, string region, int slot)
    {
        GameObject Foot = new GameObject("Copy As Custom", typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement));
        Foot.transform.SetParent(menu.transform, false);
        Foot.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
        RectTransform rect = Foot.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-8f, 8f);
        rect.sizeDelta = new Vector2(100f, 30f);
        UnityEngine.UI.Button button = Button(Foot.transform, "Copy As Custom", () =>
        {
            CustomRegions.RegionData copy = CustomRegions.Copy(region, menu, slot);
            if (copy != null) menu.LoadMenu(copy.id);
        });
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = Vector2.zero;
        buttonRect.anchorMax = Vector2.one;
        buttonRect.offsetMin = buttonRect.offsetMax = Vector2.zero;
        button.interactable = !Challenges.IsLocked;
        menu.TrackInstance(Foot);
    }

    internal static GameObject Row(MenuSettingsManager menu, string name)
    {
        GameObject row = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement), typeof(UnityEngine.UI.HorizontalLayoutGroup));
        row.transform.SetParent(menu.Content, false);
        row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 32f);
        UnityEngine.UI.LayoutElement element = row.GetComponent<UnityEngine.UI.LayoutElement>();
        element.minHeight = element.preferredHeight = 32f;
        element.flexibleHeight = 0f;
        UnityEngine.UI.HorizontalLayoutGroup layout = row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(5, 5, 3, 3);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        menu.TrackInstance(row);
        return row;
    }

    private static TMP_FontAsset Font(Transform parent)
    {
        Canvas canvas = parent.GetComponentInParent<Canvas>(true);
        TMP_Text source = canvas?.GetComponentInChildren<TMP_Text>(true);
        return source != null ? source.font : TMP_Settings.defaultFontAsset;
    }

    internal static TMP_Text AddLabel(Transform parent, string value, float width)
    {
        MenuSettingsManager menu = parent.GetComponentInParent<Canvas>(true)?.GetComponentInChildren<MenuSettingsManager>(true);
        TMP_Text source = menu?.GetTextTemplate(parent.GetComponent<UnityEngine.UI.Button>() != null);
        GameObject label = source != null ? UnityEngine.Object.Instantiate(source.gameObject, parent, false) :
            new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.name = "Label";
        label.transform.SetParent(parent, false);
        TMP_Text text = label.GetComponent<TMP_Text>();
        if (source == null) text.font = Font(parent);
        text.enableAutoSizing = false;
        text.fontSize = 12f;
        text.text = value;
        text.raycastTarget = false;
        if (label.GetComponent<UnityEngine.UI.LayoutElement>() == null) label.AddComponent<UnityEngine.UI.LayoutElement>();
        UnityEngine.UI.LayoutElement element = label.GetComponent<UnityEngine.UI.LayoutElement>();
        if (width > 0f) element.minWidth = element.preferredWidth = width;
        else { element.minWidth = 25f; element.flexibleWidth = 1f; }
        return text;
    }

    internal static UnityEngine.UI.Button Button(Transform parent, string title, Action callback, float width = 0f)
    {
        GameObject obj = new GameObject(title, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.LayoutElement));
        obj.transform.SetParent(parent, false);
        parent.GetComponentInParent<Canvas>(true)?.GetComponentInChildren<MenuSettingsManager>(true)?.StylePanel(obj.GetComponent<UnityEngine.UI.Image>());
        UnityEngine.UI.LayoutElement element = obj.GetComponent<UnityEngine.UI.LayoutElement>();
        element.minWidth = width > 0f ? width : 60f;
        if (width > 0f) element.preferredWidth = width;
        else element.flexibleWidth = 1f;
        TMP_Text label = AddLabel(obj.transform, title, 0f);
        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(3f, 0f);
        rect.offsetMax = new Vector2(-3f, 0f);
        UnityEngine.UI.Button button = obj.GetComponent<UnityEngine.UI.Button>();
        button.targetGraphic = obj.GetComponent<UnityEngine.UI.Image>();
        button.onClick.AddListener(() => callback());
        return button;
    }

    private static TMP_InputField Input(Transform parent, string value, string placeholder)
    {
        GameObject obj = new GameObject("Input", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
        obj.SetActive(false);
        obj.AddComponent<TMP_InputField>();
        obj.transform.SetParent(parent, false);
        obj.GetComponent<UnityEngine.UI.LayoutElement>().minWidth = 55f;
        obj.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
        obj.GetComponent<UnityEngine.UI.Image>().color = new Color(0.1f, 0.1f, 0.1f, 1f);
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D));
        viewport.transform.SetParent(obj.transform, false);
        RectTransform rect = viewport.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(6f, 2f);
        rect.offsetMax = new Vector2(-6f, -2f);
        TMP_Text text = AddLabel(viewport.transform, "", 0f);
        TMP_Text hint = AddLabel(viewport.transform, placeholder, 0f);
        hint.color = new Color(0.65f, 0.65f, 0.65f, 1f);
        foreach (TMP_Text label in new[] { text, hint })
        {
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
        }
        TMP_InputField input = obj.GetComponent<TMP_InputField>();
        input.textViewport = rect;
        input.textComponent = text;
        input.placeholder = hint;
        input.targetGraphic = obj.GetComponent<UnityEngine.UI.Image>();
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.customCaretColor = true;
        input.caretColor = Color.white;
        input.caretWidth = 2;
        input.caretBlinkRate = 0.8f;
        input.selectionColor = new Color(0.65f, 0.65f, 0.65f, 0.4f);
        input.text = value;
        obj.SetActive(true);
        return input;
    }

    private static void Autocomplete(MenuSettingsManager menu, string hint, Func<IEnumerable<string>> choices, Func<string, string> title, Action<string> accept, string action, string initialValue = "", string buttonText = "+")
    {
        int acceptedFrame = -1;
        Action<string> acceptValue = accept;
        accept = value =>
        {
            if (Time.frameCount == acceptedFrame) return;
            acceptedFrame = Time.frameCount;
            acceptValue(value);
        };
        GameObject row = Row(menu, action);
        TMP_InputField input = Input(row.transform, initialValue, hint);
        GameObject suggestions = new GameObject("Suggestions", typeof(RectTransform), typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(UnityEngine.UI.LayoutElement));
        suggestions.transform.SetParent(menu.Content, false);
        UnityEngine.UI.VerticalLayoutGroup layout = suggestions.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 2f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        menu.TrackInstance(suggestions);
        suggestions.SetActive(false);
        Action<string> update = query =>
        {
            RectTransform content = menu.Content as RectTransform;
            Vector2 position = content != null ? content.anchoredPosition : Vector2.zero;
            UnityEngine.UI.ScrollRect scroll = menu.GetComponent<UnityEngine.UI.ScrollRect>();
            scroll?.StopMovement();
            CustomRegions.RefreshCatalog();
            for (int i = suggestions.transform.childCount - 1; i >= 0; i--)
            {
                GameObject old = suggestions.transform.GetChild(i).gameObject;
                old.SetActive(false);
                UnityEngine.Object.Destroy(old);
            }
            List<string> matches = choices().Where(id => string.IsNullOrEmpty(query) || title(id).ContainsInsensitive(query)).Take(8).ToList();
            suggestions.SetActive(matches.Count > 0);
            suggestions.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = matches.Count * 28f;
            suggestions.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, matches.Count * 28f);
            foreach (string id in matches)
            {
                string captured = id;
                UnityEngine.UI.Button button = Button(suggestions.transform, title(id), () =>
                {
                    Vector2 selectedPosition = content != null ? content.anchoredPosition : Vector2.zero;
                    input.SetTextWithoutNotify(captured);
                    if (buttonText == "Set") accept(captured);
                    suggestions.SetActive(false);
                    if (content != null)
                    {
                        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                        content.anchoredPosition = selectedPosition;
                    }
                });
                button.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 26f;
                button.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 26f);
            }
            if (content != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                content.anchoredPosition = position;
            }
        };
        input.onValueChanged.AddListener(query => update(query));
        input.onSelect.AddListener(query => update(query));
        if (buttonText == "Set") input.onEndEdit.AddListener(value => { if (!string.IsNullOrWhiteSpace(value)) accept(value.Trim()); });
        input.onSubmit.AddListener(value => { if (!string.IsNullOrWhiteSpace(value)) accept(value.Trim()); });
        Button(row.transform, buttonText, () => { if (!string.IsNullOrWhiteSpace(input.text)) accept(input.text.Trim()); }, 32f);
    }
}

public sealed class CustomLevelCountLock : MonoBehaviour
{
    public string OrderedKey;
    public string CountKey;
    public CustomRegions.SubregionData Subregion;
    private bool wasOrdered;

    private void LateUpdate()
    {
        bool ordered = UISettingsManager.instance != null ? UISettingsManager.instance.GetBool(OrderedKey, true) : true;
        TMP_InputField input = GetComponentInChildren<TMP_InputField>(true);
        if (input != null)
        {
            if (ordered && Subregion != null) input.SetTextWithoutNotify(Subregion.levels.Count.ToString());
            else if (wasOrdered && UISettingsManager.instance != null) input.SetTextWithoutNotify(UISettingsManager.instance.GetInt(CountKey, 1).ToString());
        }
        wasOrdered = ordered;
        foreach (UnityEngine.UI.Selectable selectable in GetComponentsInChildren<UnityEngine.UI.Selectable>(true)) selectable.interactable = !ordered;
        CanvasGroup group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        group.alpha = ordered ? 0.4f : 1f;
        group.interactable = !ordered;
    }
}

public sealed class CustomInfoLayout : MonoBehaviour, UnityEngine.UI.ILayoutElement
{
    public TMP_Text Text;
    private float height = 32f;
    public float minWidth => -1f;
    public float preferredWidth => -1f;
    public float flexibleWidth => -1f;
    public float minHeight => height;
    public float preferredHeight => height;
    public float flexibleHeight => 0f;
    public int layoutPriority => 2;

    public void CalculateLayoutInputHorizontal() { }

    public void CalculateLayoutInputVertical()
    {
        if (Text == null) return;
        float width = Mathf.Max(1f, Text.rectTransform.rect.width);
        height = Mathf.Max(32f, Text.GetPreferredValues(Text.text, width, float.PositiveInfinity).y + 12f);
    }
}