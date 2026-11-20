using Celestial.CustomEndless;
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Presets : MonoBehaviour, IPointerClickHandler
{
    [Serializable]
    public sealed class PresetData
    {
        public string name;
        public List<string> stringKeys = new List<string>();
        public List<string> stringValues = new List<string>();
        public List<string> intKeys = new List<string>();
        public List<int> intValues = new List<int>();
        public List<string> floatKeys = new List<string>();
        public List<float> floatValues = new List<float>();
        public List<string> boolKeys = new List<string>();
        public List<bool> boolValues = new List<bool>();
        public List<string> slots = new List<string>();
    }

    private sealed class PresetRow
    {
        public GameObject gameObject;
        public TMP_InputField title;
        public string filePath;
        public string savedName;
        public bool removed;
    }

    private sealed class PresetFile
    {
        public PresetData Data;
        public string Path;
    }

    private const string Extension = ".preset.json";
    private readonly List<PresetRow> rows = new List<PresetRow>();
    private MenuSettingsManager menu;
    private Transform content;

    private static string PresetDirectory => ModDataPaths.PresetsDirectory;

    private void Awake()
    {
        menu = GetComponentInParent<Canvas>(true)?.GetComponentInChildren<MenuSettingsManager>(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) OpenMenu();
    }

    public void OpenMenu()
    {
        content = menu?.BeginCustomMenu("Presets");
        if (content != null) Refresh();
    }

    private void Refresh()
    {
        if (content == null || menu == null) return;
        ClearRows();
        CreateNewButton();
        List<PresetFile> presets = ReadPresets();
        for (int i = 0; i < presets.Count; i++) CreateRow(presets[i].Data, presets[i].Path);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content as RectTransform);
    }
    private List<PresetFile> ReadPresets()
    {
        List<PresetFile> values = new List<PresetFile>();
        try
        {
            if (!Directory.Exists(PresetDirectory)) return values;
            string[] files = Directory.GetFiles(PresetDirectory, "*" + Extension, SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    PresetData preset = JsonUtility.FromJson<PresetData>(File.ReadAllText(files[i]));
                    if (preset == null) continue;
                    preset.name = Path.GetFileName(files[i]).Substring(0, Path.GetFileName(files[i]).Length - Extension.Length);
                    values.Add(new PresetFile { Data = preset, Path = files[i] });
                }
                catch (Exception exception)
                {
                    Plugin.Log?.LogWarning(exception);
                }
            }
        }
        catch (Exception exception)
        {
            Plugin.Log?.LogWarning(exception);
        }
        values.Sort((left, right) => string.Compare(left.Data.name, right.Data.name, StringComparison.OrdinalIgnoreCase));
        return values;
    }

    private void CreateNewButton()
    {
        GameObject instance = new GameObject("New Preset", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        instance.transform.SetParent(content, false);
        instance.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 30f);
        menu.StylePanel(instance.GetComponent<Image>());
        LayoutElement layout = instance.GetComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = 30f;
        GameObject title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        title.transform.SetParent(instance.transform, false);
        RectTransform rect = title.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = title.GetComponent<TextMeshProUGUI>();
        TMP_Text source = menu.GetComponentInParent<Canvas>(true)?.GetComponentInChildren<TMP_Text>(true);
        text.font = source != null ? source.font : TMP_Settings.defaultFontAsset;
        text.fontSize = 12f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = "New Preset";
        instance.GetComponent<Button>().onClick.AddListener(() =>
        {
            if (Challenges.IsLocked) return;
            PresetData data = Capture(GetUniqueName("New Preset"));
            string path = GetPath(data.name);
            if (WritePreset(data, path)) CreateRow(data, path);
        });
        menu.TrackInstance(instance);
    }

    private void ClearRows()
    {
        for (int i = 0; i < rows.Count; i++) if (rows[i].gameObject != null) Destroy(rows[i].gameObject);
        rows.Clear();
    }

    private void CreateRow(PresetData data, string filePath)
    {
        GameObject instance = menu.CreateIntegerInstance(content);
        if (instance == null) return;
        instance.name = "Preset " + data.name;
        instance.SetActive(false);
        TemplateInteger integer = instance.GetComponent<TemplateInteger>();
        if (integer != null) DestroyImmediate(integer);
        NumericInputControls controls = instance.GetComponent<NumericInputControls>();
        if (controls != null) DestroyImmediate(controls);
        BuildActions(instance);

        PresetRow row = new PresetRow
        {
            gameObject = instance,
            filePath = filePath,
            savedName = data.name
        };
        row.title = GetTitleInput(instance);
        SetTitle(row, data.name);
        if (row.title != null) row.title.onEndEdit.AddListener(value => Rename(row, value));
        Bind(instance, "Save", () => Save(row));
        Bind(instance, "Load", () => Load(row));
        Bind(instance, "Copy", () => Copy(row));
        Bind(instance, "Remove", () => Remove(row));
        PresetLocationOpener opener = instance.GetComponent<PresetLocationOpener>();
        if (opener == null) opener = instance.AddComponent<PresetLocationOpener>();
        opener.Configure(filePath);
        instance.SetActive(true);
        rows.Add(row);
    }

    private TMP_InputField GetTitleInput(GameObject row)
    {
        Transform titleTransform = FindNamed(row.transform, "Title");
        if (titleTransform == null) return row.GetComponentInChildren<TMP_InputField>(true);
        TMP_InputField field = titleTransform.GetComponent<TMP_InputField>();
        if (field == null) field = titleTransform.gameObject.AddComponent<TMP_InputField>();
        TMP_Text text = titleTransform.GetComponent<TMP_Text>();
        if (text == null) text = titleTransform.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            field.textComponent = text;
            field.targetGraphic = text;
            field.transition = Selectable.Transition.None;
        }
        return field;
    }

    private void BuildActions(GameObject row)
    {
        LayoutElement rowSize = row.GetComponent<LayoutElement>() ?? row.AddComponent<LayoutElement>();
        rowSize.minHeight = rowSize.preferredHeight = 64f;
        rowSize.flexibleHeight = 0f;
        Transform titleTransform = FindNamed(row.transform, "Title");
        if (titleTransform is RectTransform titleRect)
        {
            LayoutElement titleSize = titleTransform.GetComponent<LayoutElement>() ?? titleTransform.gameObject.AddComponent<LayoutElement>();
            titleSize.ignoreLayout = true;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(6f, -31f);
            titleRect.offsetMax = new Vector2(-6f, -4f);
        }
        Transform input = FindNamed(row.transform, "Input");
        if (input != null) input.gameObject.SetActive(false);
        GameObject actions = new GameObject("Actions", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        actions.transform.SetParent(row.transform, false);
        actions.GetComponent<LayoutElement>().ignoreLayout = true;
        RectTransform rect = actions.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 3f);
        rect.sizeDelta = new Vector2(-8f, 23f);
        HorizontalLayoutGroup layout = actions.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 3f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        TMP_Text title = row.GetComponentInChildren<TMP_Text>(true);
        Image source = row.GetComponent<Image>();
        CreateAction(actions.transform, "Save", title, source);
        CreateAction(actions.transform, "Load", title, source);
        CreateAction(actions.transform, "Copy", title, source);
        CreateAction(actions.transform, "Remove", title, source);
    }

    private static void CreateAction(Transform parent, string label, TMP_Text sourceText, Image sourceImage)
    {
        GameObject action = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        action.transform.SetParent(parent, false);
        Image image = action.GetComponent<Image>();
        if (sourceImage != null)
        {
            image.sprite = sourceImage.sprite;
            image.type = sourceImage.type;
        }
        image.color = new Color(0.22f, 0.22f, 0.22f, 1f);
        LayoutElement element = action.GetComponent<LayoutElement>();
        element.minWidth = 38f;
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(action.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (sourceText != null)
        {
            text.font = sourceText.font;
            text.fontSharedMaterial = sourceText.fontSharedMaterial;
            text.fontSize = Mathf.Min(sourceText.fontSize, 12f);
        }
        text.alignment = TextAlignmentOptions.Center;
        text.text = label;
        text.raycastTarget = false;
    }
    private void Bind(GameObject row, string action, Action callback)
    {
        Transform transform = FindNamed(row.transform, action);
        Button button = transform != null ? transform.GetComponent<Button>() : null;
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => callback());
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = action;
    }

    private void Save(PresetRow row)
    {
        if (row.removed || Challenges.IsLocked) return;
        if (!Rename(row, GetTitle(row))) return;
        PresetData data = Capture(row.savedName);
        WritePreset(data, row.filePath);
    }

    private void Load(PresetRow row)
    {
        try
        {
            if (!File.Exists(row.filePath)) return;
            PresetData data = JsonUtility.FromJson<PresetData>(File.ReadAllText(row.filePath));
            if (data == null) return;
            Apply(data);
        }
        catch (Exception exception)
        {
            Plugin.Log?.LogWarning(exception);
        }
    }

    private void Copy(PresetRow row)
    {
        if (row.removed || Challenges.IsLocked) return;
        try
        {
            if (!File.Exists(row.filePath)) return;
            PresetData data = JsonUtility.FromJson<PresetData>(File.ReadAllText(row.filePath));
            if (data == null) return;
            data.name = GetUniqueName(row.savedName + " Copy");
            string filePath = GetPath(data.name);
            if (WritePreset(data, filePath)) CreateRow(data, filePath);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content as RectTransform);
        }
        catch (Exception exception)
        {
            Plugin.Log?.LogWarning(exception);
        }
    }

    private void Remove(PresetRow row)
    {
        if (row.removed || Challenges.IsLocked) return;
        try
        {
            if (File.Exists(row.filePath)) File.Delete(row.filePath);
        }
        catch (Exception exception)
        {
            Plugin.Log?.LogWarning(exception);
            return;
        }
        row.removed = true;
        if (row.title != null) row.title.onEndEdit.RemoveAllListeners();
        rows.Remove(row);
        if (row.gameObject != null)
        {
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }
    }

    private bool Rename(PresetRow row, string requestedName)
    {
        if (row.removed || Challenges.IsLocked) return false;
        string name = string.IsNullOrWhiteSpace(requestedName) ? row.savedName : requestedName.Trim();
        if (string.Equals(name, row.savedName, StringComparison.Ordinal))
        {
            SetTitle(row, row.savedName);
            return true;
        }
        string newPath = GetPath(name);
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            SetTitle(row, row.savedName);
            return false;
        }
        bool samePath = string.Equals(Path.GetFullPath(row.filePath), Path.GetFullPath(newPath), StringComparison.OrdinalIgnoreCase);
        if ((!samePath && File.Exists(newPath)) || rows.Exists(other => other != row && string.Equals(other.savedName, name, StringComparison.OrdinalIgnoreCase)))
        {
            SetTitle(row, row.savedName);
            return false;
        }
        try
        {
            if (!File.Exists(row.filePath)) { SetTitle(row, row.savedName); return false; }
            PresetData data = JsonUtility.FromJson<PresetData>(File.ReadAllText(row.filePath));
            if (data == null) { SetTitle(row, row.savedName); return false; }
            data.name = name;
            if (!WritePreset(data, samePath ? row.filePath : newPath)) { SetTitle(row, row.savedName); return false; }
            if (!samePath)
            {
                try { File.Delete(row.filePath); }
                catch { File.Delete(newPath); throw; }
            }
            else if (!string.Equals(row.filePath, newPath, StringComparison.Ordinal))
            {
                string temporary = Path.Combine(PresetDirectory, Guid.NewGuid().ToString("N") + ".rename");
                File.Move(row.filePath, temporary);
                try { File.Move(temporary, newPath); }
                catch { File.Move(temporary, row.filePath); throw; }
            }
            else newPath = row.filePath;
            row.filePath = newPath;
            row.savedName = name;
            row.gameObject.name = "Preset " + name;
            row.gameObject.GetComponent<PresetLocationOpener>()?.Configure(newPath);
            SetTitle(row, name);
            return true;
        }
        catch (Exception exception)
        {
            Plugin.Log?.LogWarning(exception);
            SetTitle(row, row.savedName);
            return false;
        }
    }

    private bool ContainsName(string name)
    {
        for (int i = 0; i < rows.Count; i++) if (string.Equals(rows[i].savedName, name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private string GetUniqueName(string baseName)
    {
        string name = baseName;
        int suffix = 2;
        while (ContainsName(name) || File.Exists(GetPath(name))) name = baseName + " " + suffix++;
        return name;
    }

    private static bool WritePreset(PresetData data, string filePath)
    {
        try
        {
            ModDataPaths.EnsureRootDirectory();
            Directory.CreateDirectory(PresetDirectory);
            File.WriteAllText(filePath, JsonUtility.ToJson(data, true));
            return true;
        }
        catch (Exception exception)
        {
            Plugin.Log?.LogWarning(exception);
            return false;
        }
    }

    public static PresetData Capture(string name)
    {
        Dictionary<string, int> ints = new Dictionary<string, int>(EndlessConfiguration.CustomUIManager.SettingCounts);
        Dictionary<string, float> floats = new Dictionary<string, float>(EndlessConfiguration.CustomUIManager.SettingFloats);
        Dictionary<string, string> strings = new Dictionary<string, string>(EndlessConfiguration.CustomUIManager.SettingStrings);
        Dictionary<string, bool> bools = new Dictionary<string, bool>(EndlessConfiguration.CustomUIManager.SettingToggles);
        UISettingsManager manager = UISettingsManager.instance;
        if (manager != null)
        {
            Merge(ints, manager.CopyInts());
            Merge(floats, manager.CopyFloats());
            Merge(strings, manager.CopyStrings());
            Merge(bools, manager.CopyBools());
        }
        PresetData data = new PresetData { name = name };
        Add(data.intKeys, data.intValues, ints);
        Add(data.floatKeys, data.floatValues, floats);
        Add(data.stringKeys, data.stringValues, strings);
        Add(data.boolKeys, data.boolValues, bools);
        for (int i = 0; i < 50; i++)
        {
            if (!strings.TryGetValue("slot_" + i + "_contents", out string slot) || string.IsNullOrEmpty(slot) || string.Equals(slot, "None", StringComparison.OrdinalIgnoreCase)) break;
            data.slots.Add(slot);
        }
        return data;
    }

    public static void Apply(PresetData data)
    {
        Dictionary<string, int> ints = Read(data.intKeys, data.intValues);
        Dictionary<string, float> floats = Read(data.floatKeys, data.floatValues);
        Dictionary<string, string> strings = Read(data.stringKeys, data.stringValues);
        Dictionary<string, bool> bools = Read(data.boolKeys, data.boolValues);
        RestoreSlotState(data, ints, strings);
        UISettingsManager manager = UISettingsManager.instance;
        if (manager != null) manager.RestoreSettings(ints, floats, strings, bools);
        Replace(EndlessConfiguration.CustomUIManager.SettingCounts, ints);
        Replace(EndlessConfiguration.CustomUIManager.SettingFloats, floats);
        Replace(EndlessConfiguration.CustomUIManager.SettingStrings, strings);
        Replace(EndlessConfiguration.CustomUIManager.SettingToggles, bools);
        OrderingContainer.instance?.ReplaceSlots(data.slots);
        if (manager != null)
        {
            manager.RestoreSettings(ints, floats, strings, bools);
            manager.SetActiveSlotCount(data.slots == null ? 0 : data.slots.Count);
        }
        foreach (KeyValuePair<string, bool> pair in bools)
        {
            const string suffix = "_randomizable";
            if (pair.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) SubTabContainer.SetRandomizable(pair.Key.Substring(0, pair.Key.Length - suffix.Length), pair.Value);
        }
        Replace(EndlessConfiguration.CustomUIManager.SettingCounts, ints);
        Replace(EndlessConfiguration.CustomUIManager.SettingStrings, strings);
        EndlessConfiguration.UpdateOrderedRegions();
        Canvas.ForceUpdateCanvases();
    }

    private static void RestoreSlotState(PresetData data, Dictionary<string, int> ints, Dictionary<string, string> strings)
    {
        if (data.slots == null) return;
        for (int i = 0; i < data.slots.Count && i < 50; i++)
        {
            string slot = data.slots[i];
            if (string.IsNullOrEmpty(slot) || string.Equals(slot, "None", StringComparison.OrdinalIgnoreCase)) continue;
            strings["slot_" + i + "_contents"] = slot;
            string loopKey = "slot_" + i + "_loops";
            if (!ints.TryGetValue(loopKey, out int loops) || loops < 1) ints[loopKey] = 1;
        }
    }
    private static void Merge<T>(Dictionary<string, T> target, Dictionary<string, T> source)
    {
        if (source == null) return;
        foreach (KeyValuePair<string, T> pair in source) target[pair.Key] = pair.Value;
    }

    private static void Replace<T>(Dictionary<string, T> target, Dictionary<string, T> values)
    {
        target.Clear();
        Merge(target, values);
    }

    private static void Add<T>(List<string> keys, List<T> values, Dictionary<string, T> source)
    {
        foreach (KeyValuePair<string, T> pair in source)
        {
            keys.Add(pair.Key);
            values.Add(pair.Value);
        }
    }

    private static Dictionary<string, T> Read<T>(List<string> keys, List<T> values)
    {
        Dictionary<string, T> result = new Dictionary<string, T>();
        if (keys == null || values == null) return result;
        int count = Mathf.Min(keys.Count, values.Count);
        for (int i = 0; i < count; i++) if (!string.IsNullOrEmpty(keys[i])) result[keys[i]] = values[i];
        return result;
    }

    private static Transform FindNamed(Transform root, string name)
    {
        Transform[] values = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < values.Length; i++) if (string.Equals(values[i].name, name, StringComparison.OrdinalIgnoreCase)) return values[i];
        return null;
    }

    private static string GetTitle(PresetRow row) => row.title == null ? row.savedName : row.title.text;

    private static void SetTitle(PresetRow row, string title)
    {
        if (row.title != null) row.title.SetTextWithoutNotify(title);
    }

    private static string GetPath(string name)
    {
        string value = string.IsNullOrWhiteSpace(name) ? "Current" : name.Trim();
        char[] invalid = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalid.Length; i++) value = value.Replace(invalid[i], '_');
        return Path.Combine(PresetDirectory, value + Extension);
    }
}

public sealed class PresetLocationOpener : MonoBehaviour, IPointerClickHandler
{
    private string filePath;

    public void Configure(string path)
    {
        filePath = path;
    }

    public void OnPointerClick(PointerEventData eventData)
    {

    }
}




