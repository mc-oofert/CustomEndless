using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class NumericInputControls : MonoBehaviour
{
    private const float PanelWidth = 200f;
    private const float PanelHeight = 50f;
    private const float RowHeight = 25f;

    private TMP_InputField input;
    private Slider slider;
    private bool isInteger;
    private float increment;
    private string lastInputText;
    private float maximumRange;

    public void Initialise(TMP_InputField sourceInput, TMP_Text title, bool integer, float step)
    {
        input = sourceInput;
        isInteger = integer;
        increment = step;
        maximumRange = isInteger ? 10f : 1f;

        RectTransform root = (RectTransform)transform;
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, PanelWidth);
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, PanelHeight);

        HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
        if (layout != null) layout.enabled = false;

        Position((RectTransform)title.transform, 100f, 12.5f, PanelWidth, RowHeight);
        Position((RectTransform)input.transform, 50f, -12.5f, 50f, RowHeight);
        StretchInputText(input.transform);

        CreateStepper(root, title);
        slider = CreateSlider(root);
        slider.onValueChanged.AddListener(OnSliderChanged);
        SyncFromInput();
    }

    private void Update()
    {
        if (input != null && input.text != lastInputText) SyncFromInput();
    }

    public void SyncFromInput()
    {
        if (input == null || slider == null) return;

        lastInputText = input.text;
        if (!TryRead(input.text, out float value)) value = 0f;

        maximumRange = Mathf.Max(maximumRange, RangeFor(value));
        slider.minValue = -maximumRange;
        slider.maxValue = maximumRange;
        slider.wholeNumbers = isInteger;
        slider.SetValueWithoutNotify(Mathf.Clamp(value, -maximumRange, maximumRange));
    }

    private void CreateStepper(RectTransform root, TMP_Text title)
    {
        GameObject stepper = new GameObject("Stepper", typeof(RectTransform));
        RectTransform rect = stepper.GetComponent<RectTransform>();
        rect.SetParent(root, false);
        Position(rect, 15f, -12.5f, 20f, RowHeight);
        CreateButton(rect, "Increase", "+", 5f, title, () => Adjust(increment));
        CreateButton(rect, "Decrease", "-", -5f, title, () => Adjust(-increment));
    }

    private void Adjust(float amount)
    {
        TryRead(input.text, out float value);
        value += amount;
        if (float.IsNaN(value) || float.IsInfinity(value)) return;
        if (isInteger) value = Mathf.Round(value);
        input.text = Format(value);
    }

    private static void Position(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private Button CreateButton(RectTransform parent, string name, string label, float y, TMP_Text title, UnityAction action)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(20f, 10f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.38f, 0.38f, 0.38f, 0.3f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
        labelText.font = title.font;
        labelText.fontSize = 18f;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.text = label;
        labelText.raycastTarget = false;
        return button;
    }

    private Slider CreateSlider(RectTransform root)
    {
        GameObject sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.SetParent(root, false);
        Position(sliderRect, 137.5f, -12.5f, 125f, RowHeight);
        Slider result = sliderObject.GetComponent<Slider>();

        RectTransform background = CreateImage("Background", sliderRect, new Color(0.18f, 0.18f, 0.18f, 1f));
        SetTrackRect(background);
        GameObject fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
        RectTransform fillArea = fillAreaObject.GetComponent<RectTransform>();
        fillArea.SetParent(sliderRect, false);
        SetTrackRect(fillArea);
        RectTransform fill = CreateImage("Fill", fillArea, new Color(0.18f, 0.18f, 0.18f, 1f));
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.sizeDelta = Vector2.zero;

        GameObject handleAreaObject = new GameObject("Handle Slide Area", typeof(RectTransform));
        RectTransform handleArea = handleAreaObject.GetComponent<RectTransform>();
        handleArea.SetParent(sliderRect, false);
        handleArea.anchorMin = new Vector2(0f, 0.5f);
        handleArea.anchorMax = new Vector2(1f, 0.5f);
        handleArea.offsetMin = new Vector2(8f, -12f);
        handleArea.offsetMax = new Vector2(-8f, 12f);
        RectTransform handle = CreateImage("Handle", handleArea, Color.black);
        handle.anchorMin = handle.anchorMax = new Vector2(0.5f, 0.5f);
        handle.anchoredPosition = Vector2.zero;
        handle.sizeDelta = new Vector2(10f, 25f);


        result.fillRect = fill;
        result.handleRect = handle;
        result.handleRect.localScale = new Vector2(1, 0.2f);
        result.targetGraphic = handle.GetComponent<Image>();
        return result;
    }

    private static RectTransform CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.GetComponent<Image>().color = color;
        return rect;
    }

    private static void SetTrackRect(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.offsetMin = new Vector2(8f, -4f);
        rect.offsetMax = new Vector2(-8f, 4f);
    }

    private static void StretchInputText(Transform inputTransform)
    {
        RectTransform text = inputTransform.childCount > 0 ? inputTransform.GetChild(0) as RectTransform : null;
        if (text == null) return;
        text.anchorMin = Vector2.zero;
        text.anchorMax = Vector2.one;
        text.offsetMin = new Vector2(4f, 4f);
        text.offsetMax = new Vector2(-4f, -4f);
    }

    private static bool TryRead(string text, out float value)
    {
        bool parsed = float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            || float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        if (parsed && !float.IsNaN(value) && !float.IsInfinity(value)) return true;
        value = 0f;
        return false;
    }

    private static float RangeFor(float value)
    {
        float magnitude = Mathf.Abs(value);
        return magnitude < 1f ? 1f : Mathf.Min(float.MaxValue * 0.25f, Mathf.Pow(10f, Mathf.Ceil(Mathf.Log10(magnitude))));
    }

    private string Format(float value)
    {
        return isInteger
            ? Mathf.RoundToInt(value).ToString(CultureInfo.CurrentCulture)
            : value.ToString("0.###", CultureInfo.CurrentCulture);
    }

    private void OnSliderChanged(float value)
    {
        input.text = Format(value);
    }
}
