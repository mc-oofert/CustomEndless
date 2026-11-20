using TMPro;
using UnityEngine;

public class TemplateInteger : MonoBehaviour
{
    [SerializeField] private string settingName = "Integer Setting";
    [SerializeField] private string settingsTag = "default_int";
    [SerializeField] private int defaultValue = 0;

    private TMP_Text titleText;
    private TMP_InputField inputField;
    private UISettingsManager settingsManager;
    private NumericInputControls controls;

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
            inputField = inputTransform.GetComponent<TMP_InputField>();
        }

        controls = gameObject.AddComponent<NumericInputControls>();
        if (inputField != null && titleText != null)
        {
            controls.Initialise(inputField, titleText, true, 1f);
        }
    }

    private void Start()
    {
        if (titleText != null)
        {
            titleText.text = settingName;
        }

        if (inputField != null)
        {
            inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
            inputField.text = defaultValue.ToString();
            inputField.onValueChanged.AddListener(OnInputValueChanged);
            controls.SyncFromInput();
        }

        UpdateSettingState(defaultValue);
    }

    private void OnInputValueChanged(string newValue)
    {
        controls.SyncFromInput();
        if (int.TryParse(newValue, out int parsedValue))
        {
            UpdateSettingState(parsedValue);
        }
        else if (string.IsNullOrEmpty(newValue))
        {
            UpdateSettingState(0);
        }
    }

    private void UpdateSettingState(int value)
    {
        if (settingsManager != null)
        {
            settingsManager.SetInt(settingsTag, value);
        }
    }
}
