using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// These kinds of files get assigned to the UI elements with that uhh.... unity thingy. The thingy that tells the monobehaviors where to look. That thing.
public class CollapsibleContainer : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private bool startExpanded = true;

    private bool isExpanded;
    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        isExpanded = startExpanded;
        UpdateVisibility();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
        GameObject hit = eventData.pointerCurrentRaycast.gameObject;
        if (hit == null) hit = eventData.pointerPressRaycast.gameObject;
        if (hit == null) return;
        if (hit.GetComponentInParent<CollapsibleContainer>() != this) return;
        Transform header = transform.Find("Title");
        if (header == null && transform.childCount > 0) header = transform.GetChild(0);
        if (header is RectTransform headerRect && !RectTransformUtility.RectangleContainsScreenPoint(headerRect, eventData.position, eventData.pressEventCamera)) return;
        if (hit != gameObject && (header == null || (hit.transform != header && !hit.transform.IsChildOf(header)))) return;
        Toggle();
    }

    private void OnEnable()
    {
        if (rectTransform != null) UpdateVisibility();
    }

    public void SetExpanded(bool expanded)
    {
        startExpanded = expanded;
        isExpanded = expanded;
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        UpdateVisibility();
    }

    public void Toggle()
    {
        SetExpanded(!isExpanded);
        MenuSettingsManager menu = GetComponentInParent<MenuSettingsManager>();
        if (menu != null) menu.RefreshContentLayout();
        else RebuildLayouts(rectTransform);
    }

    private void UpdateVisibility()
    {
        int childCount = transform.childCount;
        if (childCount > 0)
        {
            transform.GetChild(0).gameObject.SetActive(true);
        }

        for (int i = 1; i < childCount; i++)
        {
            transform.GetChild(i).gameObject.SetActive(isExpanded);
        }
    }

    public void RefreshHeight()
    {
        VerticalLayoutGroup group = GetComponent<VerticalLayoutGroup>();
        if (group == null) return;
        group.CalculateLayoutInputHorizontal();
        group.CalculateLayoutInputVertical();
        LayoutElement size = GetComponent<LayoutElement>();
        if (size == null) size = gameObject.AddComponent<LayoutElement>();
        size.minHeight = size.preferredHeight = Mathf.Max(32f, group.preferredHeight);
        size.flexibleHeight = 0f;
    }

    private void RebuildLayouts(RectTransform rect)
    {
        while (rect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            Canvas.ForceUpdateCanvases();
            rect = rect.parent as RectTransform;
        }
    }
}