using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class Play : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private AnimationCurve hoverCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float hoverDuration = 0.18f;
    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private Color hoverColor = Color.red;

    private static readonly FieldInfo ActiveSlotsField = typeof(OrderingContainer).GetField("activeSlots", BindingFlags.NonPublic | BindingFlags.Instance);
    private UnityEngine.UI.Button button;
    private TMP_Text label;
    private MenuSettingsManager menu;
    private Vector3 originalScale;
    private Color originalColor;
    private Coroutine hoverAnimation;

    private void Awake()
    {
        button = GetComponent<UnityEngine.UI.Button>();
        if (button == null)
            button = gameObject.AddComponent<UnityEngine.UI.Button>();

        menu = GetComponentInParent<Canvas>(true)?.GetComponentInChildren<MenuSettingsManager>(true);
        label = GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            originalScale = label.transform.localScale;
            originalColor = label.color;
        }

        button.onClick.AddListener(Press);
        UpdateLabel();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(Press);
    }

    private void Update()
    {
        UpdateLabel();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        AnimateHover(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateHover(false);
    }

    private void Press()
    {
        string region = GetSoloRegion();
        Challenges.PrepareRun();
        RandomContainer random = RandomContainer.Instance ?? FindObjectOfType<RandomContainer>(true);
        if (!Challenges.IsActive && region != null)
        {
            if (!BalancedMode.CanUse(region)) BalancedMode.EnableFullCustom();
            OrderingContainer.instance?.AddSlot(region);
        }
        else if (!Challenges.IsActive && random != null && random.PrimaryValue > 0)
        {
            ClearQueue();
            AddRandomRegions(random.GetRandomCount());
        }
        AnimateHover(false);
        CloseMenu();
    }

    private void UpdateLabel()
    {
        if (label == null)
            return;

        RandomContainer random = RandomContainer.Instance ?? FindObjectOfType<RandomContainer>(true);
        string region = GetSoloRegion();
        string text = region != null ? "Play Endless " + SubTabContainer.CachedRegions[region].displayName :
            !Challenges.IsActive && random != null && random.PrimaryValue > 0 ? "RANDOM" : "CLIMB";
        if (label.text != text)
        {
            label.text = text;
        }
    }

    private string GetSoloRegion()
    {
        if (Challenges.IsActive || menu == null || UISettingsManager.instance == null) return null;
        for (int i = 0; i < 50; i++)
        {
            string queued = UISettingsManager.instance.GetString("slot_" + i + "_contents");
            if (!string.IsNullOrEmpty(queued) && queued != "None") return null;
        }
        string region = menu.ActiveMenuTag;
        if (string.IsNullOrEmpty(region)) return null;
        string[] suffixes = { "-SCORES", "-PROGRESS", "-BALANCED" };
        foreach (string suffix in suffixes)
            if (region.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase)) region = region.Substring(0, region.Length - suffix.Length);
        if (region == CustomRegions.TemplateName || SubTabContainer.IsRandomOption(region)) return null;
        return SubTabContainer.CachedRegions.ContainsKey(region) ? region : null;
    }

    private void AnimateHover(bool entering)
    {
        if (label == null)
            return;

        if (hoverAnimation != null)
            StopCoroutine(hoverAnimation);

        hoverAnimation = StartCoroutine(AnimateHoverRoutine(entering));
    }

    private IEnumerator AnimateHoverRoutine(bool entering)
    {
        Vector3 startScale = label.transform.localScale;
        Color startColor = label.color;
        Vector3 endScale = entering ? originalScale * hoverScale : originalScale;
        Color endColor = entering ? hoverColor : originalColor;
        float elapsed = 0f;

        while (elapsed < hoverDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = hoverCurve.Evaluate(Mathf.Clamp01(elapsed / hoverDuration));
            label.color = Color.Lerp(startColor, endColor, t);
            yield return null;
        }

        label.color = endColor;
    }

    private static void ClearQueue()
    {
        OrderingContainer container = OrderingContainer.instance;
        if (container == null || ActiveSlotsField == null)
            return;

        IList slots = ActiveSlotsField.GetValue(container) as IList;
        while (slots != null && slots.Count > 0)
            container.RemoveSlotAt(0);
    }

    private static void AddRandomRegions(int count)
    {
        OrderingContainer container = OrderingContainer.instance;
        if (container == null || count <= 0) return;
        for (int i = 0; i < count; i++) container.AddSlot(SubTabContainer.GetRandomSliderSelection(), true);
    }

    private void CloseMenu()
    {
        UISettingsManager manager = GetComponentInParent<UISettingsManager>();
        if (manager == null)
            manager = FindObjectOfType<UISettingsManager>(true);

        if (manager == null)
            return;

        UnityEngine.UI.Button background = manager.transform.Find("MenuBackgroundOverlay")?.GetComponent<UnityEngine.UI.Button>();
        if (background != null)
            background.onClick.Invoke();
        else
            manager.gameObject.SetActive(false);
    }
}
