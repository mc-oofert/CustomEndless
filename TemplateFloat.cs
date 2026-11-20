using TMPro;
using UnityEngine;

public class TemplateFloat : MonoBehaviour
{
    [SerializeField] private string settingName = "Float Setting";
    [SerializeField] private string settingsTag = "default_float";
    [SerializeField] private float defaultValue = 0f;

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
            controls.Initialise(inputField, titleText, false, 0.1f);
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
            inputField.contentType = TMP_InputField.ContentType.DecimalNumber;
            inputField.text = defaultValue.ToString();
            inputField.onValueChanged.AddListener(OnInputValueChanged);
            controls.SyncFromInput();
        }

        UpdateSettingState(defaultValue);
    }

    private void OnInputValueChanged(string newValue)
    {
        controls.SyncFromInput();
        if (float.TryParse(newValue, out float parsedValue))
        {
            UpdateSettingState(parsedValue);
        }
        else if (string.IsNullOrEmpty(newValue))
        {
            UpdateSettingState(0f);
        }
    }

    private void UpdateSettingState(float value)
    {
        if (settingsManager != null)
        {
            settingsManager.SetFloat(settingsTag, value);
        }
    }
}
