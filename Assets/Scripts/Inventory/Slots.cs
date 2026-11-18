using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Slots : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public bool hoveringOverSlot;
    private ItemScriptableObject heldItem;
    private int itemAmount;
    private Image itemIcon;
    private TextMeshProUGUI itemCountText;

    private void Awake()
    {
        itemIcon = transform.GetChild(0).GetComponent<Image>();
        itemCountText = transform.GetChild(1).GetComponent<TextMeshProUGUI>();
    }

    public ItemScriptableObject GetItem()
    {
        return heldItem;
    }

    public int GetItemCount()
    {
        return itemAmount;
    }

    public void SetItem(ItemScriptableObject item, int amount = 1)
    {
        heldItem = item;
        itemAmount = amount;
        UpdateSlotUI();
    }

    public void UpdateSlotUI()
    {
        if (itemIcon == null)
        {
            itemIcon = transform.GetChild(0).GetComponent<Image>();
            itemCountText = transform.GetChild(1).GetComponent<TextMeshProUGUI>();
        }

        if (heldItem != null)
        {
            itemIcon.enabled = true;
            itemIcon.sprite = heldItem.itemIcon;
            itemCountText.text = itemAmount.ToString();
        }
        else
        {
            itemIcon.enabled = false;
            itemCountText.text = "";
        }
    }

    public int AddAmount(int amountToAdd)
    {
        itemAmount += amountToAdd;
        UpdateSlotUI();
        return itemAmount;
    }

    public int RemoveAmount(int amountToRemove)
    {
        itemAmount -= amountToRemove;
        if (itemAmount <= 0)
        {
            ClearSlot();
            return 0;
        }
        UpdateSlotUI();
        return itemAmount;
    }

    public void ClearSlot()
    {
        heldItem = null;
        itemAmount = 0;
        UpdateSlotUI();
    }

    public bool HasItem()
    {
        return heldItem != null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hoveringOverSlot = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hoveringOverSlot = false;
    }
}
