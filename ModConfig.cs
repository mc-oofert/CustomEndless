using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ModConfig : MonoBehaviour, IPointerClickHandler
{
    private MenuSettingsManager menu;
    private bool initialized;
    private bool lastFullCustom;

    private void Awake()
    {
        menu = GetComponentInParent<Canvas>(true)?.GetComponentInChildren<MenuSettingsManager>(true);
        menu?.Register("Settings", "Full Custom", "full_custom", SettingType.Bool, "false");
    }

    private void Update()
    {
        if (Challenges.IsActive) return;
        bool fullCustom = BalancedMode.FullCustom;
        if (!initialized)
        {
            initialized = true;
            lastFullCustom = fullCustom;
            SetModifierAccess(fullCustom);
            return;
        }
        if (lastFullCustom == fullCustom) return;
        lastFullCustom = fullCustom;
        if (!fullCustom)
        {
            menu?.ResetConfiguredSettings("full_custom");
            BalancedMode.NormalizeMedalQueue();
            UISettingsManager.instance?.SetBool("full_custom", false);
            Celestial.CustomEndless.EndlessConfiguration.CustomUIManager.SettingToggles["full_custom"] = false;
        }
        SetModifierAccess(fullCustom);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) OpenMenu();
    }

    public void OpenMenu()
    {
        menu?.LoadMenu("Settings");
    }

    private void SetModifierAccess(bool enabled)
    {
        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null) return;
        Transform[] values = canvas.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < values.Length; i++)
        {
            if (!string.Equals(values[i].name, "Modifiers", System.StringComparison.OrdinalIgnoreCase)) continue;
            Button button = values[i].GetComponent<Button>();
            if (button != null) button.interactable = true;
        }
    }
}
