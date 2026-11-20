using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TemplateRegion : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string regionName = "Silos";
    [SerializeField] private string displayName = "";
    [SerializeField] private int defaultLoops = 1;
    [SerializeField] private float defaultLengthMult = 1f;
    [SerializeField] private bool balanced = true;
    [SerializeField] private bool useHeight = true;
    [SerializeField] private bool useLoops;
    [SerializeField] private int amounts;
    [SerializeField] private int bronzeAmount = 2000;
    [SerializeField] private int silverAmount = 5000;
    [SerializeField] private int goldAmount = 10000;
    [SerializeField] private int platinumAmount = 15000;
    [SerializeField] private string[] subregionKeys = { };
    [SerializeField] private string[] subregionDisplayNames = { };
    [SerializeField] private float hoverDuration = 0.16f;
    [SerializeField] private float hoverScale = 1.12f;
    [SerializeField] private AnimationCurve hoverCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private TMP_Text titleTMP;
    private Text titleText;
    private Transform titleTransform;
    private MenuSettingsManager menuSettingsManager;
    private Coroutine hoverCoroutine;
    private Vector3 titleScale;
    private bool registered;
    private readonly Dictionary<string, string> campaignDropdowns = new Dictionary<string, string>();

    public string RegionName => regionName;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? regionName : displayName;
    public string[] SubregionKeys => Copy(subregionKeys);
    public string[] SubregionDisplayNames => Copy(subregionDisplayNames);
    public bool Balanced => balanced;
    public bool UseHeight => useHeight;
    public bool UseLoops => useLoops;
    public int BronzeAmount => bronzeAmount;
    public int SilverAmount => silverAmount;
    public int GoldAmount => goldAmount;
    public int PlatinumAmount => platinumAmount;
    public bool IsTemplate => string.Equals(regionName, "Template", System.StringComparison.OrdinalIgnoreCase) || !gameObject.activeSelf && transform.parent != null && transform.parent.Find("TemplateRegion") == transform;

    private void Awake()
    {
        menuSettingsManager = GetComponentInParent<Canvas>(true)?.GetComponentInChildren<MenuSettingsManager>(true);
        titleTransform = transform.Find("Title") ?? transform.Find("Text");
        if (titleTransform != null)
        {
            if (!titleTransform.TryGetComponent(out titleTMP)) titleTransform.TryGetComponent(out titleText);
            titleScale = titleTransform.localScale;
        }
        SubTabContainer.RegisterRegion(this);
    }

    private void Start()
    {
        UpdateTitle();
        RegisterRegionSettings();
        SubTabContainer.RegisterRegion(this);
        RefreshRandomizableVisual();
    }

    public void Configure(string name, string title, string[] keys, string[] names, bool template)
    {
        regionName = template ? "Template" : name;
        displayName = title;
        subregionKeys = Copy(keys);
        subregionDisplayNames = Copy(names);
        registered = false;
        UpdateTitle();
    }

    public void RefreshRandomizableVisual()
    {
        Graphic graphic = titleTMP != null ? (Graphic)titleTMP : titleText;
        if (graphic == null || IsTemplate) return;
        graphic.color = SubTabContainer.IsRandomizable(regionName) ? Color.white : Color.red;
    }

    private void UpdateTitle()
    {
        if (titleTMP != null) titleTMP.text = DisplayName;
        else if (titleText != null) titleText.text = DisplayName;
    }

    public void ConfigureCustom()
    {
        balanced = true;
    }

    public void RegisterRegionSettings()
    {
        if (registered || IsTemplate || regionName == CustomRegions.TemplateName || menuSettingsManager == null) return;
        registered = true;
        if (string.Equals(regionName, "Campaign", System.StringComparison.OrdinalIgnoreCase)) AddCampaignSubregions();
        string key = regionName.ToLowerInvariant();
        string dropdown = DisplayName.ToUpperInvariant() + " SETTINGS";
        menuSettingsManager.Register(regionName, "Loops", key + "_loops", SettingType.Integer, Mathf.Max(1, defaultLoops).ToString(), dropdown);
        menuSettingsManager.Register(regionName, "Length Multiplier", key + "_length_mult", SettingType.Float, defaultLengthMult.ToString(), dropdown);
        menuSettingsManager.Register(regionName, "Breakroom Amount", key + "_breakroom_amount", SettingType.Integer, "1", dropdown);
        menuSettingsManager.Register(regionName, "Faces", key + "_faces", SettingType.Integer, "0", dropdown);
        if (!string.Equals(regionName, "Duct", System.StringComparison.OrdinalIgnoreCase))
            menuSettingsManager.Register(regionName, "Remains", key + "_remains", SettingType.Integer, "0", dropdown);
        menuSettingsManager.Register(regionName, "Unseen Tuocs", key + "_unseen", SettingType.Integer, "0", dropdown);
        menuSettingsManager.Register(regionName, "Unseen Cimim", key + "_cimim", SettingType.Integer, "0", dropdown);
        if (string.Equals(key, "checkpoint", System.StringComparison.OrdinalIgnoreCase))
        {
            menuSettingsManager.Register(regionName, "Save", key + "_save", SettingType.Bool, "false", dropdown);
            menuSettingsManager.Register(regionName, "Experimental", key + "_experimental", SettingType.Bool, "false", dropdown);
        }
        if (SubTabContainer.IsRandomOption(regionName))
        {
            menuSettingsManager.Register(regionName, "Reroll on Restart", "reroll_on_restart", SettingType.Bool, "false", dropdown);
            if (string.Equals(regionName, "Random Shorts", System.StringComparison.OrdinalIgnoreCase)) menuSettingsManager.Register(regionName, "Short Count", "short_count", SettingType.Integer, "3", dropdown);
        }
        SubTabContainer.EnsureRandomizable(regionName, !IsDefaultExcluded());
        BalancedMode.RegisterRegion(menuSettingsManager, this);
        List<string> keys = new List<string>(subregionKeys ?? new string[0]);
        List<string> names = new List<string>(subregionDisplayNames ?? new string[0]);
        if (keys.Count == 0 && !CustomRegions.IsCustom(regionName) && !SubTabContainer.IsRandomOption(regionName))
        {
            keys.Add(regionName);
            names.Add(DisplayName);
        }
        int count = Mathf.Min(keys.Count, names.Count);
        for (int i = 0; i < count; i++)
        {
            if (string.IsNullOrEmpty(keys[i])) continue;
            string subDropdown = campaignDropdowns.TryGetValue(keys[i], out string group) ? group + "/" + names[i].ToUpperInvariant() : names[i].ToUpperInvariant();
            menuSettingsManager.Register(regionName, "Exclude", "subregion_" + keys[i] + "_disabled", SettingType.Bool, "false", subDropdown);
            menuSettingsManager.Register(regionName, "Difficulty", "subregion_" + keys[i] + "_difficulty_mult", SettingType.Float, "1", subDropdown);
            menuSettingsManager.Register(regionName, "Mass Speed", "subregion_" + keys[i] + "_mass_mult", SettingType.Float, "1", subDropdown);
            menuSettingsManager.Register(regionName, "Space Compression", "subregion_" + keys[i] + "_space_mult", SettingType.Float, "1", subDropdown);
        }
    }

    private void AddCampaignSubregions()
    {
        List<string> keys = new List<string>();
        List<string> names = new List<string>();
        campaignDropdowns.Clear();
        foreach (var entry in Celestial.CustomEndless.EndlessConfiguration.GetCampaignSubregions())
        {
            string key = Celestial.CustomEndless.EndlessConfiguration.GetCampaignSubregionKey(entry.Subregion);
            keys.Add(key);
            names.Add(string.IsNullOrWhiteSpace(entry.Subregion.subregionName) ? entry.Subregion.name : entry.Subregion.subregionName);
            campaignDropdowns[key] = entry.Region.ToUpperInvariant();
        }
        subregionKeys = keys.ToArray();
        subregionDisplayNames = names.ToArray();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);

    private bool rawRightHeld;
    private bool rawRightStartedHere;
    private int rightClickFrame = -10;
    private PointerEventData.InputButton? physicalButton;
    private readonly List<RaycastResult> mouseHits = new List<RaycastResult>();

    private static bool RightHeld()
    {
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed) return true;
        try { if (Input.GetMouseButton(1)) return true; }
        catch (System.InvalidOperationException) { }
        if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
            return (GetAsyncKeyState(0x02) & 0x8000) != 0;
        return false;
    }

    private bool MouseIsOverRegion()
    {
        if (EventSystem.current == null) return false;
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        Vector2 position = mouse != null ? mouse.position.ReadValue() : (Vector2)Input.mousePosition;
        PointerEventData pointer = new PointerEventData(EventSystem.current) { position = position };
        mouseHits.Clear();
        EventSystem.current.RaycastAll(pointer, mouseHits);
        return mouseHits.Count > 0 && mouseHits[0].gameObject.GetComponentInParent<TemplateRegion>() == this;
    }

    private void Update()
    {
        bool held = RightHeld();
        if (held && !rawRightHeld) rawRightStartedHere = MouseIsOverRegion();
        if (!held && rawRightHeld && rawRightStartedHere)
        {
            rawRightStartedHere = false;
            if (MouseIsOverRegion()) HandleRightClick();
        }
        rawRightHeld = held;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        physicalButton = RightHeld() || eventData.button == PointerEventData.InputButton.Right
            ? PointerEventData.InputButton.Right : eventData.button;
    }

    private void HandleRightClick()
    {
        if (Time.frameCount - rightClickFrame <= 1) return;
        rightClickFrame = Time.frameCount;
        if (Challenges.IsLocked || regionName == CustomRegions.TemplateName || !BalancedMode.CanUse(regionName)) return;
        OrderingContainer.instance?.AddSlot(regionName, SubTabContainer.IsRandomOption(regionName));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PointerEventData.InputButton button = physicalButton ?? (RightHeld() ? PointerEventData.InputButton.Right : eventData.button);
        physicalButton = null;
        if (button == PointerEventData.InputButton.Right)
        {
            HandleRightClick();
            return;
        }
        if (Time.frameCount - rightClickFrame <= 1 || Challenges.IsLocked) return;
        if (button == PointerEventData.InputButton.Middle)
        {
            SubTabContainer.SetRandomizable(regionName, !SubTabContainer.IsRandomizable(regionName));
            return;
        }
        if (button == PointerEventData.InputButton.Left)
        {
            GameObject target = eventData.pointerCurrentRaycast.gameObject;
            if (Celestial.CustomEndless.Plugin.DumbItDown && target != null && target != gameObject)
            {
                menuSettingsManager?.LoadMenu(regionName);
                return;
            }
            OpenMenu();
            return;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        AnimateTitle(true);
        BalancedMode.ShowMetal(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateTitle(false);
        BalancedMode.HideMetal();
    }

    private void AnimateTitle(bool entering)
    {
        if (titleTransform == null) return;
        if (hoverCoroutine != null) StopCoroutine(hoverCoroutine);
        hoverCoroutine = StartCoroutine(AnimateTitleRoutine(entering));
    }

    private IEnumerator AnimateTitleRoutine(bool entering)
    {
        Vector3 start = titleTransform.localScale;
        Vector3 end = entering ? titleScale * hoverScale : titleScale;
        float elapsed = 0f;
        while (elapsed < hoverDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = hoverCurve.Evaluate(Mathf.Clamp01(elapsed / hoverDuration));
            titleTransform.localScale = Vector3.LerpUnclamped(start, end, t);
            yield return null;
        }
        titleTransform.localScale = end;
    }

    public void OpenMenu()
    {
        if (Celestial.CustomEndless.Plugin.DumbItDown)
        {
            HandleRightClick();
            return;
        }
        if (regionName == CustomRegions.TemplateName)
        {
            BalancedMode.EnableFullCustom();
            CustomRegions.RegionData created = CustomRegions.CreateBlank(menuSettingsManager);
            if (created != null) menuSettingsManager.LoadMenu(created.id);
            return;
        }
        if (menuSettingsManager != null) BalancedMode.OpenRegionMenu(menuSettingsManager, this);
    }

    private bool IsDefaultExcluded()
    {
        return string.Equals(regionName, "Campaign", System.StringComparison.OrdinalIgnoreCase) || string.Equals(regionName, "Ladder", System.StringComparison.OrdinalIgnoreCase) || string.Equals(regionName, "Checkpoint", System.StringComparison.OrdinalIgnoreCase) || string.Equals(regionName, "Random Any", System.StringComparison.OrdinalIgnoreCase);
    }

    private static string[] Copy(string[] values)
    {
        if (values == null) return new string[0];
        string[] result = new string[values.Length];
        System.Array.Copy(values, result, values.Length);
        return result;
    }
}
