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

    public void Initialize(int index, Inventory inv){
        slotIndex = index;
        inventory = inv;

        ConfigureRaycastTargets();

        Button btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(OnSlotClicked);

        ClearSlot();
    }

    public void SetFistSlot(bool locked){
        isFistSlot = locked;

        // Show lock tint on fist slot so player knows it's permanent
        if (lockOverlay != null){
            lockOverlay.enabled = locked;
            lockOverlay.raycastTarget = false;
        }
        
    }

    void OnSlotClicked() { inventory.SelectSlot(slotIndex); }

    public void UpdateSlot(InventorySlot slot){
        if (slot != null && slot.count > 0){
            SetIcon(slot.icon);

            // Hide count for fist slot
            if (!isFistSlot) SetCount(slot.count.ToString(), true);
            
            else SetCount(string.Empty, false);
            
        }
        else ClearSlot();
        
    }

    public void ClearSlot(){
        // Never clear fist slot visually
        if (isFistSlot) return;

        SetIcon(null, false);
        SetCount(string.Empty, false);
    }

    public void SetHighlight(bool highlighted){
        if (highlightImage != null) highlightImage.enabled = highlighted;
        
    }

    private void SetIcon(Sprite sprite, bool enabled = true){
        if (iconImage == null) return;

        iconImage.sprite = sprite;
        iconImage.enabled = enabled;
    }

    private void SetCount(string text, bool enabled){
        if (countText == null) return;

        countText.text = text;
        countText.enabled = enabled;
    }

    private void ConfigureRaycastTargets(){
        if (iconImage != null) iconImage.raycastTarget = false;
        if (highlightImage != null) highlightImage.raycastTarget = false;
        if (lockOverlay != null) lockOverlay.raycastTarget = false;
    }
}