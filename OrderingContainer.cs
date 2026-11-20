using System.Collections.Generic;
using UnityEngine;

public class OrderingContainer : MonoBehaviour
{
    public static OrderingContainer instance;

    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotParent;

    private List<RemoveOnClick> activeSlots = new List<RemoveOnClick>();

    private void Awake()
    {
        slotPrefab = transform.GetChild(0).gameObject;
        slotParent = transform;
        instance = this;
    }

    public void AddSlot(string regionName, bool isRandom = false)
    {
        if (Celestial.CustomEndless.Plugin.DumbItDown && (CustomRegions.IsCustom(regionName) || regionName == CustomRegions.TemplateName)) return;
        if (UISettingsManager.instance == null || string.IsNullOrEmpty(regionName) || !BalancedMode.CanQueue(regionName)) return;
        isRandom = isRandom || SubTabContainer.IsRandomOption(regionName);
        int newSlotIndex = activeSlots.Count;
        if (newSlotIndex >= 50) return;
        UISettingsManager.instance.add_slot_to_end(regionName);
        Transform parent = slotParent != null ? slotParent : transform;
        GameObject slotObj = Instantiate(slotPrefab, parent);
        RemoveOnClick slotItem = slotObj.GetComponent<RemoveOnClick>();
        if (slotItem == null) slotItem = slotObj.AddComponent<RemoveOnClick>();
        slotItem.gameObject.SetActive(true);
        slotItem.Initialize(newSlotIndex, regionName, this, isRandom);
        activeSlots.Add(slotItem);
    }

    public void ReplaceSlots(IList<string> regions)
    {
        for (int i = activeSlots.Count - 1; i >= 0; i--)
        {
            if (activeSlots[i] != null) Destroy(activeSlots[i].gameObject);
        }
        activeSlots.Clear();
        UISettingsManager.instance?.ClearSlots();
        if (regions == null) return;
        for (int i = 0; i < regions.Count && i < 50; i++)
        {
            if (!string.IsNullOrEmpty(regions[i]) && !string.Equals(regions[i], "None", System.StringComparison.OrdinalIgnoreCase)) AddSlot(regions[i], SubTabContainer.IsRandomOption(regions[i]));
        }
    }

    public void RemoveSlotAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= activeSlots.Count) return;
        UISettingsManager.instance?.remove_slot(slotIndex);
        GameObject objToDestroy = activeSlots[slotIndex].gameObject;
        activeSlots.RemoveAt(slotIndex);
        Destroy(objToDestroy);
        RefreshIndices();
    }

    private void RefreshIndices()
    {
        for (int i = 0; i < activeSlots.Count; i++) activeSlots[i].UpdateSlotIndex(i);
    }
}
