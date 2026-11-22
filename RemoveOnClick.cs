using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RemoveOnClick : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private int slotIndex;
    [SerializeField] private string regionName;

    private OrderingContainer container;
    private TMP_Text titleTMP;
    private Text titleText;
    private bool random;

    public void Initialize(int index, string region, OrderingContainer parentContainer, bool isRandom)
    {
        slotIndex = index;
        regionName = region;
        container = parentContainer;
        random = isRandom;

        UpdateVisuals();
    }

    public void UpdateSlotIndex(int newIndex)
    {
        slotIndex = newIndex;
    }

    private void UpdateVisuals()
    {
        string displayText = "?";
        if (!random)
        {
            string text = CustomRegions.TryGet(regionName, out CustomRegions.RegionData custom) ? custom.name : regionName ?? "";
            displayText = (text.Length >= 3 ? text.Substring(0, 3) : text).ToUpper();
        }

        Transform titleTransform = transform.Find("Title") ?? transform.Find("Text");
        if (titleTransform != null)
        {
            if (titleTransform.TryGetComponent(out titleTMP))
            {
                titleTMP.text = displayText;
            }
            else if (titleTransform.TryGetComponent(out titleText))
            {
                titleText.text = displayText;
            }
        }
        else
        {
            if (TryGetComponent(out titleTMP)) titleTMP.text = displayText;
            else if (TryGetComponent(out titleText)) titleText.text = displayText;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool isMiddleClick = eventData.button == PointerEventData.InputButton.Middle;
        bool isLeftClick = eventData.button == PointerEventData.InputButton.Left;

        if (isMiddleClick || (isLeftClick && isShiftPressed))
        {
            if (container != null)
            {
                container.RemoveSlotAt(slotIndex);
            }
            else if (UISettingsManager.instance != null)
            {
                UISettingsManager.instance.remove_slot(slotIndex);
                Destroy(gameObject);
            }
            return;
        }

        if (isLeftClick)
        {
            MenuSettingsManager menuManager = FindObjectOfType<MenuSettingsManager>();
            menuManager?.LoadMenu(regionName, slotIndex);
        }
    }
}
