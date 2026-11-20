using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UISettingsManager : MonoBehaviour
{
    private Dictionary<string, int> intSettings = new Dictionary<string, int>();
    private Dictionary<string, float> floatSettings = new Dictionary<string, float>();
    private Dictionary<string, string> stringSettings = new Dictionary<string, string>();
    private Dictionary<string, bool> boolSettings = new Dictionary<string, bool>();

    private int activeSlotCount;
    private GameObject bgOverlay;

    public static UISettingsManager instance;

    private void Awake()
    {
        instance = this;
    }

    private void OnEnable()
    {
        instance = this;
        SetupBackgroundOverlay();
    }

    private void SetupBackgroundOverlay()
    {
        if (bgOverlay != null) Destroy(bgOverlay);
        bgOverlay = new GameObject("MenuBackgroundOverlay");
        bgOverlay.transform.SetParent(transform, false);
        bgOverlay.transform.SetAsFirstSibling();
        RectTransform rectTransform = bgOverlay.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        Image bgImage = bgOverlay.AddComponent<Image>();
        bgImage.color = new Color(0.05f, 0.05f, 0.05f, 0.99f);
        Button closeButton = bgOverlay.AddComponent<Button>();
        closeButton.transition = Selectable.Transition.None;
        closeButton.onClick.AddListener(() =>
        {
            closeButton.interactable = false;
            bgOverlay.transform.SetParent(transform.parent, false);
            Destroy(bgOverlay, 0.1f);
            bgOverlay = null;
            gameObject.SetActive(false);
        });
    }

    public void SetInt(string key, int value)
    {
        if (!string.IsNullOrEmpty(key)) intSettings[key] = value;
    }

    public bool HasInt(string key) => intSettings.ContainsKey(key);
    public bool HasFloat(string key) => floatSettings.ContainsKey(key);
    public bool HasBool(string key) => boolSettings.ContainsKey(key);

    public int GetInt(string key, int defaultValue = 0) => intSettings.TryGetValue(key, out int value) ? value : defaultValue;

    public void SetFloat(string key, float value)
    {
        if (!string.IsNullOrEmpty(key) && !float.IsNaN(value) && !float.IsInfinity(value)) floatSettings[key] = value;
    }

    public float GetFloat(string key, float defaultValue = 0f) => floatSettings.TryGetValue(key, out float value) ? value : defaultValue;

    public void SetString(string key, string value)
    {
        if (!string.IsNullOrEmpty(key)) stringSettings[key] = value ?? string.Empty;
    }

    public string GetString(string key, string defaultValue = "") => stringSettings.TryGetValue(key, out string value) ? value : defaultValue;

    public void SetBool(string key, bool value)
    {
        if (!string.IsNullOrEmpty(key)) boolSettings[key] = value;
    }

    public bool GetBool(string key, bool defaultValue = false) => boolSettings.TryGetValue(key, out bool value) ? value : defaultValue;

    public Dictionary<string, int> CopyInts() => new Dictionary<string, int>(intSettings);
    public Dictionary<string, float> CopyFloats() => new Dictionary<string, float>(floatSettings);
    public Dictionary<string, string> CopyStrings() => new Dictionary<string, string>(stringSettings);
    public Dictionary<string, bool> CopyBools() => new Dictionary<string, bool>(boolSettings);

    public void RestoreSettings(Dictionary<string, int> ints, Dictionary<string, float> floats, Dictionary<string, string> strings, Dictionary<string, bool> bools)
    {
        intSettings.Clear();
        floatSettings.Clear();
        stringSettings.Clear();
        boolSettings.Clear();
        CopyInto(intSettings, ints);
        CopyInto(floatSettings, floats);
        CopyInto(stringSettings, strings);
        CopyInto(boolSettings, bools);
    }

    public void ClearSlots()
    {
        RemoveSlotKeys(intSettings);
        RemoveSlotKeys(floatSettings);
        RemoveSlotKeys(stringSettings);
        RemoveSlotKeys(boolSettings);
        activeSlotCount = 0;
    }

    public void SetActiveSlotCount(int count)
    {
        activeSlotCount = Mathf.Clamp(count, 0, 50);
    }

    public void add_slot_to_end(string logical_name)
    {
        if (activeSlotCount >= 50) return;
        SetString($"slot_{activeSlotCount}_contents", logical_name);
        SetInt($"slot_{activeSlotCount}_loops", 1);
        activeSlotCount++;
    }

    public void remove_slot(int slot_index)
    {
        if (slot_index < 0 || slot_index >= activeSlotCount) return;
        for (int i = slot_index; i < activeSlotCount - 1; i++)
        {
            SetString($"slot_{i}_contents", GetString($"slot_{i + 1}_contents"));
            SetInt($"slot_{i}_loops", GetInt($"slot_{i + 1}_loops"));
        }
        SetString($"slot_{activeSlotCount - 1}_contents", "None");
        SetInt($"slot_{activeSlotCount - 1}_loops", 0);
        activeSlotCount--;
    }

    private static void CopyInto<T>(Dictionary<string, T> destination, Dictionary<string, T> source)
    {
        if (source == null) return;
        foreach (KeyValuePair<string, T> pair in source) destination[pair.Key] = pair.Value;
    }

    private static void RemoveSlotKeys<T>(Dictionary<string, T> values)
    {
        List<string> keys = new List<string>();
        foreach (string key in values.Keys) if (key.StartsWith("slot_", StringComparison.OrdinalIgnoreCase)) keys.Add(key);
        for (int i = 0; i < keys.Count; i++) values.Remove(keys[i]);
    }
}
