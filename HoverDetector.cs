using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class HoverDetector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float fadeDuration = 0.15f;
    [SerializeField] private float toggleFadeDuration = 0.1f;
    [SerializeField] private Color enabledColor = Color.green;
    [SerializeField] private float rotationDuration = 0.16f;
    [SerializeField] private AnimationCurve rotationCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.65f, 1.15f), new Keyframe(1f, 1f));

    public bool toggled;

    private UnityEngine.UI.Image targetImage;
    private UnityEngine.UI.Image buttonImage;
    private Color disabledColor;
    private Quaternion originalImageRotation;

    private Coroutine currentFadeCoroutine;
    private Coroutine rotationCoroutine;
    private bool imageHovered;

    private UISettingsManager settingsManager;

    public string settingsTag = "default_bool";

    private void Awake()
    {
        if (TryGetComponent(out buttonImage))
        {
            disabledColor = buttonImage.color;
        }

        Transform childTransform = transform.Find("Image");
        if (childTransform != null && childTransform.TryGetComponent(out targetImage))
        {
            originalImageRotation = targetImage.transform.localRotation;
            SetImageAlpha(0f);
        }

        settingsManager = UISettingsManager.instance;

    }

    private void UpdateSettingState(bool isEnabled)
    {
        settingsManager?.SetBool(settingsTag, isEnabled);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        StartFade(1f);

        StartRotation(true);

    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StartFade(0f);

        StartRotation(false);

    }

    private void StartRotation(bool hovering)
    {
        if (targetImage == null)
            return;

        if (rotationCoroutine != null)
            StopCoroutine(rotationCoroutine);

        rotationCoroutine = StartCoroutine(RotateImage(hovering));
    }

    private IEnumerator RotateImage(bool hovering)
    {
        Quaternion startRotation = targetImage.transform.localRotation;
        Quaternion endRotation = hovering ? originalImageRotation * Quaternion.Euler(0f, 0f, 45f) : originalImageRotation;
        float elapsed = 0f;

        while (elapsed < rotationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = rotationCurve.Evaluate(Mathf.Clamp01(elapsed / rotationDuration));
            targetImage.transform.localRotation = Quaternion.SlerpUnclamped(startRotation, endRotation, t);
            yield return null;
        }

        targetImage.transform.localRotation = endRotation;
    }

    private void StartFade(float targetAlpha)
    {
        if (targetImage == null) return;

        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
        }

        currentFadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha));
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        float startAlpha = targetImage.color.a;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetImageAlpha(Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration));
            yield return null;
        }

        SetImageAlpha(targetAlpha);
    }

    private IEnumerator ToggleFadeRoutine(Color targetColor)
    {
        if (buttonImage == null) yield break;

        Color startColor = buttonImage.color;
        float elapsed = 0f;

        while (elapsed < toggleFadeDuration)
        {
            elapsed += Time.deltaTime;
            buttonImage.color = Color.Lerp(startColor, targetColor, elapsed / toggleFadeDuration);
            yield return null;
        }

        buttonImage.color = targetColor;
    }

    private void SetImageAlpha(float alpha)
    {
        Color color = targetImage.color;
        color.a = alpha;
        targetImage.color = color;
    }
}
