using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUISlot : MonoBehaviour
{
    public int slotIndex;
    public Image iconImage;
    public TextMeshProUGUI countText;
    public Image highlightImage;
    public Image lockOverlay;            // Optional: dim overlay for fist slot

    private Inventory inventory;
    private bool isFistSlot;

    public void Initialize(int index, Inventory inv)
    {
        slotIndex = index;
        inventory = inv;

        Button btn = GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(OnSlotClicked);

        ClearSlot();
    }

    public void SetFistSlot(bool locked)
    {
        isFistSlot = locked;

        // Show lock tint on fist slot so player knows it's permanent
        if (lockOverlay != null)
            lockOverlay.enabled = locked;
    }

    void OnSlotClicked()
    {
        inventory.SelectSlot(slotIndex);
    }

    public void UpdateSlot(InventorySlot slot)
    {
        if (slot != null && slot.count > 0)
        {
            iconImage.sprite = slot.icon;
            iconImage.enabled = true;

            // Hide count for fist slot
            if (!isFistSlot)
            {
                countText.text = slot.count.ToString();
                countText.enabled = true;
            }
            else
            {
                countText.enabled = false;
            }
        }
        else
        {
            ClearSlot();
        }
    }

    public void ClearSlot()
    {
        // Never clear fist slot visually
        if (isFistSlot) return;

        iconImage.enabled = false;
        countText.enabled = false;
    }

    public void SetHighlight(bool highlighted)
    {
        if (highlightImage != null)
            highlightImage.enabled = highlighted;
    }
}