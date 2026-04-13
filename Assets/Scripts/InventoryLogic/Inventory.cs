using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class Inventory : MonoBehaviour
{
    private const int MaxStackSize = 999;

    [Header("Inventory Settings")]
    public int hotbarSlots = 4;
    public int mainGridSlots = 32;
    public int selectedSlot = 0;

    [Header("Fist Slot")]
    public Sprite fistIcon;

    [Header("UI References")]
    public Transform hotbarParent;
    public Transform mainGridParent;
    public GameObject slotPrefab;

    [Header("Action Buttons")]
    public Button dropButton;
    public Button infoButton;
    public Button storeButton;
    public Button recycleButton;

    private readonly List<InventorySlot> slots = new();
    private readonly List<InventoryUISlot> uiSlots = new();
    private readonly Dictionary<int, InventoryUISlot> uiSlotLookup = new();

    private int TotalSlots => hotbarSlots + mainGridSlots;

    public bool IsFistSelected => selectedSlot == 0;

    private void Start()
    {
        InitializeSlots();
        CreateHotbarUI();
        CreateMainGridUI();
        SetupActionButtons();
        RefreshAllUISlots();
        SelectSlot(0);
    }

    private void Update()
    {
        HandleHotbarInput();
        HandleScrollInput();
    }

    private void OnDestroy()
    {
        if (dropButton != null) dropButton.onClick.RemoveListener(OnDropClicked);
        if (infoButton != null) infoButton.onClick.RemoveListener(OnInfoClicked);
        if (storeButton != null) storeButton.onClick.RemoveListener(OnStoreClicked);
        if (recycleButton != null) recycleButton.onClick.RemoveListener(OnRecycleClicked);
    }

    private void InitializeSlots()
    {
        slots.Clear();

        for (int i = 0; i < TotalSlots; i++){
            slots.Add(new InventorySlot());
        }

        slots[0].SetItem("Fist", null, fistIcon, 1);
    }

    private void HandleHotbarInput()
    {
        for (int i = 0; i < hotbarSlots; i++){
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)){
                SelectSlot(i);
            }
        }
    }

    private void HandleScrollInput()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f){
            SelectSlot((selectedSlot - 1 + hotbarSlots) % hotbarSlots);
        }
        else if (scroll < 0f){
            SelectSlot((selectedSlot + 1) % hotbarSlots);
        }
    }

    private void CreateHotbarUI()
    {
        if (hotbarParent == null || slotPrefab == null) return;

        uiSlots.Clear();
        uiSlotLookup.Clear();
        ClearUIContainer(hotbarParent);

        for (int i = 0; i < hotbarSlots; i++){
            InventoryUISlot uiSlot = CreateUISlot(i, hotbarParent);
            if (uiSlot == null) continue;

            if (i == 0){
                uiSlot.SetFistSlot(true);
            }

            uiSlots.Add(uiSlot);
            uiSlotLookup[i] = uiSlot;
        }
    }

    private void CreateMainGridUI()
    {
        if (mainGridParent == null || slotPrefab == null) return;

        ClearUIContainer(mainGridParent);

        for (int i = 0; i < mainGridSlots; i++){
            int slotIndex = hotbarSlots + i;
            InventoryUISlot uiSlot = CreateUISlot(slotIndex, mainGridParent);
            if (uiSlot == null) continue;

            uiSlots.Add(uiSlot);
            uiSlotLookup[slotIndex] = uiSlot;
        }
    }

    private void ClearUIContainer(Transform parent)
    {
        foreach (Transform child in parent){
            Destroy(child.gameObject);
        }
    }

    private InventoryUISlot CreateUISlot(int slotIndex, Transform parent)
    {
        GameObject slotGameObject = Instantiate(slotPrefab, parent);
        InventoryUISlot uiSlot = slotGameObject.GetComponent<InventoryUISlot>();
        if (uiSlot == null){
            return null;
        }

        uiSlot.Initialize(slotIndex, this);
        return uiSlot;
    }

    private void SetupActionButtons()
    {
        if (dropButton != null) dropButton.onClick.AddListener(OnDropClicked);
        if (infoButton != null) infoButton.onClick.AddListener(OnInfoClicked);
        if (storeButton != null) storeButton.onClick.AddListener(OnStoreClicked);
        if (recycleButton != null) recycleButton.onClick.AddListener(OnRecycleClicked);
    }

    public void SelectSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex)){
            return;
        }

        selectedSlot = slotIndex;
        UpdateSlotHighlights();
    }

    private void UpdateSlotHighlights()
    {
        foreach (InventoryUISlot uiSlot in uiSlots){
            uiSlot.SetHighlight(uiSlot.slotIndex == selectedSlot);
        }
    }

    public bool AddBlock(string blockName, TileBase blockTile, Sprite icon, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(blockName) || amount <= 0){
            return false;
        }

        if (!HasCapacityFor(blockName, blockTile, amount)){
            Debug.Log("Inventory full!");
            return false;
        }

        int remaining = amount;
        remaining = FillExistingStacks(blockName, blockTile, remaining);
        remaining = FillEmptySlots(blockName, blockTile, icon, remaining);
        return remaining == 0;
    }

    public bool RemoveBlock(int slotIndex, int amount = 1)
    {
        if (slotIndex == 0 || amount <= 0 || !IsValidSlotIndex(slotIndex)){
            return false;
        }

        InventorySlot slot = slots[slotIndex];
        if (slot.count < amount){
            return false;
        }

        slot.count -= amount;
        if (slot.count <= 0){
            slot.Clear();
        }

        UpdateUISlot(slotIndex);
        return true;
    }

    public InventorySlot GetSelectedSlot()
    {
        return IsValidSlotIndex(selectedSlot) ? slots[selectedSlot] : null;
    }

    private bool IsValidSlotIndex(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < slots.Count;
    }

    private void UpdateUISlot(int index)
    {
        if (!IsValidSlotIndex(index)){
            return;
        }

        if (uiSlotLookup.TryGetValue(index, out InventoryUISlot uiSlot)){
            uiSlot.UpdateSlot(slots[index]);
        }
    }

    private void RefreshAllUISlots()
    {
        for (int i = 0; i < slots.Count; i++){
            UpdateUISlot(i);
        }
    }

    private bool HasCapacityFor(string blockName, TileBase blockTile, int amount)
    {
        int capacity = 0;

        for (int i = 1; i < slots.Count; i++){
            InventorySlot slot = slots[i];
            if (slot.CanStackWith(blockName, blockTile)){
                capacity += MaxStackSize - slot.count;
            }
            else if (slot.IsEmpty){
                capacity += MaxStackSize;
            }

            if (capacity >= amount){
                return true;
            }
        }

        return false;
    }

    private int FillExistingStacks(string blockName, TileBase blockTile, int remaining)
    {
        for (int i = 1; i < slots.Count && remaining > 0; i++){
            InventorySlot slot = slots[i];
            if (!slot.CanStackWith(blockName, blockTile) || slot.count >= MaxStackSize){
                continue;
            }

            int addAmount = Mathf.Min(MaxStackSize - slot.count, remaining);
            slot.count += addAmount;
            remaining -= addAmount;
            UpdateUISlot(i);
        }

        return remaining;
    }

    private int FillEmptySlots(string blockName, TileBase blockTile, Sprite icon, int remaining)
    {
        for (int i = 1; i < slots.Count && remaining > 0; i++){
            InventorySlot slot = slots[i];
            if (!slot.IsEmpty){
                continue;
            }

            int addAmount = Mathf.Min(MaxStackSize, remaining);
            slot.SetItem(blockName, blockTile, icon, addAmount);
            remaining -= addAmount;
            UpdateUISlot(i);
        }

        return remaining;
    }

    private void OnDropClicked()
    {
        if (selectedSlot == 0){
            return;
        }

        InventorySlot selected = GetSelectedSlot();
        if (selected != null && selected.count > 0){
            Debug.Log($"Drop: {selected.blockName}");
            RemoveBlock(selectedSlot, 1);
        }
    }

    private void OnInfoClicked()
    {
        InventorySlot selected = GetSelectedSlot();
        Debug.Log(selected == null ? "Info: empty slot" : $"Info: {selected.blockName}");
    }

    private void OnStoreClicked()
    {
        Debug.Log("Store clicked");
    }

    private void OnRecycleClicked()
    {
        if (selectedSlot == 0){
            return;
        }

        InventorySlot selected = GetSelectedSlot();
        if (selected != null && selected.count > 0){
            Debug.Log($"Recycle: {selected.blockName}");
            RemoveBlock(selectedSlot, 1);
        }
    }
}
