using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUISlot : MonoBehaviour
{
    public int slotIndex;
    public Image iconImage;
    public TextMeshProUGUI countText;
    public Image highlightImage;
    public Image lockOverlay;

    private Inventory inventory;
    private bool isFistSlot;

    public void Initialize(int index, Inventory inv)
    {
        slotIndex = index;
        inventory = inv;

        ConfigureRaycastTargets();

        Button button = GetComponent<Button>();
        if (button != null){
            button.onClick.AddListener(OnSlotClicked);
        }

        ClearSlot();
    }

    public void SetFistSlot(bool locked)
    {
        isFistSlot = locked;

        if (lockOverlay != null){
            lockOverlay.enabled = locked;
            lockOverlay.raycastTarget = false;
        }
    }

    public void UpdateSlot(InventorySlot slot)
    {
        if (slot != null && !slot.IsEmpty){
            SetIcon(slot.Icon);
            bool showCount = !isFistSlot && slot.CanStack;
            SetCount(showCount ? slot.count.ToString() : string.Empty, showCount);
            return;
        }

        ClearSlot();
    }

    public void ClearSlot()
    {
        if (isFistSlot){
            SetCount(string.Empty, false);
            return;
        }

        SetIcon(null, false);
        SetCount(string.Empty, false);
    }

    public void SetHighlight(bool highlighted)
    {
        if (highlightImage != null){
            highlightImage.enabled = highlighted;
        }
    }

    private void OnSlotClicked()
    {
        if (inventory != null){
            inventory.SelectSlot(slotIndex);
        }
    }

    private void SetIcon(Sprite sprite, bool enabled = true)
    {
        if (iconImage == null){
            return;
        }

        iconImage.sprite = sprite;
        iconImage.enabled = enabled;
    }

    private void SetCount(string text, bool enabled)
    {
        if (countText == null){
            return;
        }

        countText.text = text;
        countText.enabled = enabled;
    }

    private void ConfigureRaycastTargets()
    {
        if (iconImage != null) iconImage.raycastTarget = false;
        if (highlightImage != null) highlightImage.raycastTarget = false;
        if (lockOverlay != null) lockOverlay.raycastTarget = false;
    }
}
