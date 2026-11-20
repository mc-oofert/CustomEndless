using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class RandomContainer : MonoBehaviour
{
    [SerializeField] private float textFadeDuration = 0.18f;
    [SerializeField] private int secondHandleOffset = 5;

    private UnityEngine.UI.Slider slider;
    private UnityEngine.UI.Slider secondarySlider;
    private RectTransform primaryHandle;
    private RectTransform secondaryHandle;
    private TMP_Text primaryNumber;
    private TMP_Text secondaryNumber;
    private readonly Dictionary<TMP_Text, Color> originalTextColors = new Dictionary<TMP_Text, Color>();
    private Coroutine textFade;

    public static RandomContainer Instance { get; private set; }
    public int PrimaryValue => slider == null ? 0 : Mathf.RoundToInt(slider.value);
    public bool HasSecondHandle => secondarySlider != null;

    private void Awake()
    {
        Instance = this;
        slider = GetComponentInChildren<UnityEngine.UI.Slider>(true);
        if (slider == null)
            return;

        slider.wholeNumbers = true;
        primaryHandle = slider.handleRect;
        primaryNumber = FindNumber(primaryHandle);
        AddHandleInput(primaryHandle);
        slider.onValueChanged.AddListener(OnSliderValueChanged);
        CacheTextColors();
        UpdateNumbers();
    }

    private void OnDestroy()
    {
        if (slider != null)
            slider.onValueChanged.RemoveListener(OnSliderValueChanged);

        if (secondarySlider != null)
            secondarySlider.onValueChanged.RemoveListener(OnSecondarySliderValueChanged);

        if (Instance == this)
            Instance = null;
    }

    public int GetRandomCount()
    {
        if (!HasSecondHandle)
            return PrimaryValue;

        int secondaryValue = Mathf.RoundToInt(secondarySlider.value);
        return Random.Range(Mathf.Min(PrimaryValue, secondaryValue), Mathf.Max(PrimaryValue, secondaryValue) + 1);
    }

    private void ToggleSecondHandle()
    {
        if (slider == null || primaryHandle == null)
            return;

        if (secondarySlider == null)
            CreateSecondSlider();
        else
            RemoveSecondSlider();

        StartTextFade(secondarySlider != null ? Color.red : Color.white);
    }

    private void CreateSecondSlider()
    {
        GameObject clone = Instantiate(slider.gameObject, slider.transform.parent);
        clone.name = "Secondary Slider";
        clone.transform.SetAsLastSibling();
        secondarySlider = clone.GetComponent<UnityEngine.UI.Slider>();
        secondarySlider.wholeNumbers = true;
        secondarySlider.minValue = slider.minValue;
        secondarySlider.maxValue = slider.maxValue;
        secondarySlider.value = Mathf.Clamp(PrimaryValue + secondHandleOffset, Mathf.RoundToInt(slider.minValue), Mathf.RoundToInt(slider.maxValue));
        secondaryHandle = secondarySlider.handleRect;
        secondaryNumber = FindNumber(secondaryHandle);
        HideSecondaryBar();
        AddHandleInput(secondaryHandle);
        secondarySlider.onValueChanged.AddListener(OnSecondarySliderValueChanged);
        CacheTextColors();
        UpdateNumbers();
    }

    private void RemoveSecondSlider()
    {
        secondarySlider.onValueChanged.RemoveListener(OnSecondarySliderValueChanged);
        Destroy(secondarySlider.gameObject);
        secondarySlider = null;
        secondaryHandle = null;
        secondaryNumber = null;
    }

    private void HideSecondaryBar()
    {
        foreach (UnityEngine.UI.Graphic graphic in secondarySlider.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
        {
            bool isHandleGraphic = graphic.transform == secondaryHandle || graphic.transform.IsChildOf(secondaryHandle);
            if (isHandleGraphic)
                continue;

            Color color = graphic.color;
            color.a = 0f;
            graphic.color = color;
            graphic.raycastTarget = false;
        }
    }

    private void AddHandleInput(RectTransform handle)
    {
        if (handle == null)
            return;

        HandleInput input = handle.GetComponent<HandleInput>();
        if (input == null)
            input = handle.gameObject.AddComponent<HandleInput>();

        input.Initialize(this);
    }

    private void OnSliderValueChanged(float value)
    {
        UpdateNumbers();
    }

    private void OnSecondarySliderValueChanged(float value)
    {
        UpdateNumbers();
    }

    private void UpdateNumbers()
    {
        SetNumber(primaryNumber, PrimaryValue);
        if (secondarySlider != null)
            SetNumber(secondaryNumber, Mathf.RoundToInt(secondarySlider.value));
    }

    private void CacheTextColors()
    {
        CacheTextColor(primaryNumber);
        CacheTextColor(secondaryNumber);
    }

    private void CacheTextColor(TMP_Text text)
    {
        if (text != null && !originalTextColors.ContainsKey(text))
            originalTextColors.Add(text, text.color);
    }

    private void StartTextFade(Color targetColor)
    {
        if (textFade != null)
            StopCoroutine(textFade);

        textFade = StartCoroutine(FadeTextColors(targetColor));
    }

    private IEnumerator FadeTextColors(Color targetColor)
    {
        CacheTextColors();
        Dictionary<TMP_Text, Color> startColors = new Dictionary<TMP_Text, Color>();
        foreach (KeyValuePair<TMP_Text, Color> pair in originalTextColors)
            if (pair.Key != null)
                startColors[pair.Key] = pair.Key.color;

        float elapsed = 0f;
        while (elapsed < textFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / textFadeDuration);
            foreach (KeyValuePair<TMP_Text, Color> pair in startColors)
            {
                if (pair.Key == null)
                    continue;

                Color destination = targetColor == Color.white ? originalTextColors[pair.Key] : new Color(targetColor.r, targetColor.g, targetColor.b, originalTextColors[pair.Key].a);
                pair.Key.color = Color.Lerp(pair.Value, destination, t);
            }
            yield return null;
        }
    }

    private static TMP_Text FindNumber(Transform handle)
    {
        return handle == null ? null : handle.Find("Number")?.GetComponent<TMP_Text>();
    }

    private static void SetNumber(TMP_Text text, int value)
    {
        if (text != null)
            text.text = value.ToString();
    }

    private class HandleInput : MonoBehaviour, IPointerDownHandler
    {
        private RandomContainer owner;

        public void Initialize(RandomContainer container)
        {
            owner = container;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Middle || Input.GetMouseButtonDown(2))
                owner?.ToggleSecondHandle();
        }
    }
}
