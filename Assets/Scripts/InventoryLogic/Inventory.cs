using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using TMPro;

public class Inventory : MonoBehaviour
{
    [Header("Inventory Settings")]
    public int hotbarSlots = 4;
    public int mainGridSlots = 32;
    public int selectedSlot = 0;

    [Header("Fist Slot")]
    public Sprite fistIcon;              // Drag fist sprite here

    [Header("UI References")]
    public Transform hotbarParent;
    public Transform mainGridParent;
    public GameObject slotPrefab;

    [Header("Action Buttons")]
    public Button dropButton;
    public Button infoButton;
    public Button storeButton;
    public Button recycleButton;

    // ── internals ──────────────────────────────────────────────────────────────
    private List<InventorySlot> slots = new List<InventorySlot>();
    private List<InventoryUISlot> uiSlots = new List<InventoryUISlot>();
    private int totalSlots => hotbarSlots + mainGridSlots;

    public bool IsFistSelected => selectedSlot == 0;

    void Start()
    {
        for (int i = 0; i < totalSlots; i++)
            slots.Add(new InventorySlot { blockName = "", count = 0 });

        // Slot 0 is always fist — permanent, never cleared
        slots[0].blockName = "Fist";
        slots[0].icon = fistIcon;
        slots[0].count = 1;

        CreateHotbarUI();
        CreateMainGridUI();
        SetupActionButtons();
        SelectSlot(0);
    }

    void Update()
    {
        for (int i = 0; i < hotbarSlots; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                SelectSlot(i);
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0)
            SelectSlot((selectedSlot - 1 + hotbarSlots) % hotbarSlots);
        else if (scroll < 0)
            SelectSlot((selectedSlot + 1) % hotbarSlots);
    }

    void CreateHotbarUI()
    {
        foreach (Transform child in hotbarParent)
            Destroy(child.gameObject);

        for (int i = 0; i < hotbarSlots; i++)
        {
            GameObject slotGO = Instantiate(slotPrefab, hotbarParent);
            InventoryUISlot uiSlot = slotGO.GetComponent<InventoryUISlot>();
            uiSlot.Initialize(i, this);

            // Mark slot 0 as fist slot (locked)
            if (i == 0) uiSlot.SetFistSlot(true);

            uiSlots.Add(uiSlot);
        }

        // Update fist slot UI immediately
        UpdateUISlot(0);
    }

    void CreateMainGridUI()
    {
        foreach (Transform child in mainGridParent)
            Destroy(child.gameObject);

        for (int i = 0; i < mainGridSlots; i++)
        {
            int slotIndex = hotbarSlots + i;
            GameObject slotGO = Instantiate(slotPrefab, mainGridParent);
            InventoryUISlot uiSlot = slotGO.GetComponent<InventoryUISlot>();
            uiSlot.Initialize(slotIndex, this);
            uiSlots.Add(uiSlot);
        }
    }

    void SetupActionButtons()
    {
        if (dropButton != null) dropButton.onClick.AddListener(OnDropClicked);
        if (infoButton != null) infoButton.onClick.AddListener(OnInfoClicked);
        if (storeButton != null) storeButton.onClick.AddListener(OnStoreClicked);
        if (recycleButton != null) recycleButton.onClick.AddListener(OnRecycleClicked);
    }

    public void SelectSlot(int slotIndex)
    {
        selectedSlot = slotIndex;
        UpdateSlotHighlights();
    }

    void UpdateSlotHighlights()
    {
        foreach (InventoryUISlot uiSlot in uiSlots)
            uiSlot.SetHighlight(uiSlot.slotIndex == selectedSlot);
    }

    public bool AddBlock(string blockName, TileBase blockTile, Sprite icon, int amount = 1)
    {
        // Never add to fist slot (slot 0)
        for (int i = 1; i < slots.Count; i++)
        {
            if (slots[i].blockName == blockName && slots[i].count < 999)
            {
                slots[i].count += amount;
                UpdateUISlot(i);
                return true;
            }
        }

        for (int i = 1; i < slots.Count; i++)
        {
            if (slots[i].count == 0)
            {
                slots[i].blockName = blockName;
                slots[i].blockTile = blockTile;
                slots[i].icon = icon;
                slots[i].count = amount;
                UpdateUISlot(i);
                return true;
            }
        }

        Debug.Log("Inventory full!");
        return false;
    }

    public bool RemoveBlock(int slotIndex, int amount = 1)
    {
        // Never remove from fist slot
        if (slotIndex == 0) return false;

        if (slotIndex >= 0 && slotIndex < slots.Count && slots[slotIndex].count >= amount)
        {
            slots[slotIndex].count -= amount;

            if (slots[slotIndex].count <= 0)
            {
                slots[slotIndex].blockName = "";
                slots[slotIndex].blockTile = null;
                slots[slotIndex].icon = null;
            }

            UpdateUISlot(slotIndex);
            return true;
        }
        return false;
    }

    public InventorySlot GetSelectedSlot()
    {
        return slots[selectedSlot];
    }

    void UpdateUISlot(int index)
    {
        InventoryUISlot uiSlot = uiSlots.Find(s => s.slotIndex == index);
        if (uiSlot != null)
            uiSlot.UpdateSlot(slots[index]);
    }

    void OnDropClicked()
    {
        if (selectedSlot == 0) return;
        InventorySlot selected = GetSelectedSlot();
        if (selected.count > 0)
        {
            Debug.Log($"Drop: {selected.blockName}");
            RemoveBlock(selectedSlot, 1);
        }
    }

    void OnInfoClicked()
    {
        InventorySlot selected = GetSelectedSlot();
        Debug.Log($"Info: {selected.blockName}");
    }

    void OnStoreClicked()
    {
        Debug.Log("Store clicked");
    }

    void OnRecycleClicked()
    {
        if (selectedSlot == 0) return;
        InventorySlot selected = GetSelectedSlot();
        if (selected.count > 0)
        {
            Debug.Log($"Recycle: {selected.blockName}");
            RemoveBlock(selectedSlot, 1);
        }
    }
}