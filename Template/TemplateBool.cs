using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TemplateBool : MonoBehaviour
{
    [SerializeField] private string settingName = "Bool Setting";
    [SerializeField] private string settingsTag = "default_bool";
    [SerializeField] private bool defaultValue = false;

    private TMP_Text titleText;
    private Toggle toggleInput;
    private UISettingsManager settingsManager;

    private void Awake()
    {

        settingsManager = UISettingsManager.instance;

        Transform titleTransform = transform.Find("Title");
        if (titleTransform != null)
        {
            titleText = titleTransform.GetComponent<TMP_Text>();
        }

        Transform inputTransform = transform.Find("Input");
        if (inputTransform != null)
        {
            toggleInput = inputTransform.GetComponent<Toggle>();
        }
    }

    private void Start()
    {
        if (titleText != null)
        {
            titleText.text = settingName;
        }

        if (toggleInput != null)
        {
            toggleInput.isOn = defaultValue;
            toggleInput.onValueChanged.AddListener(OnToggleValueChanged);
        }

        UpdateSettingState(defaultValue);
    }

    private void OnToggleValueChanged(bool newValue)
    {
        UpdateSettingState(newValue);
    }

    private void UpdateSettingState(bool value)
    {
        settingsManager?.SetBool(settingsTag, value);
        BalancedMode.SetModifierSetting(settingsTag, value);
        Celestial.CustomEndless.EndlessConfiguration.CustomUIManager.SettingToggles[settingsTag] = value;
    }
}