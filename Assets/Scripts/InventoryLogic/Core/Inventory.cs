using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class Inventory : MonoBehaviour
{
    private const string DirtLegacyName = "DirtTile";
    private const int StonePickaxeCraftCost = 10;
    private const float StonePickaxeBreakMultiplier = 3f;

    [Header("Inventory Settings")]
    public int hotbarSlots = 4;
    public int mainGridSlots = 32;
    public int selectedSlot = 0;

    [Header("Core Item Icons")]
    public Sprite fistIcon;
    public Sprite stonePickaxeIcon;

    [Header("Data")]
    public ItemRegistry itemRegistry;

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
    private string DirtItemId => RuntimeItemCatalog.GetBlockItemId(DirtLegacyName);

    public ItemDefinition SelectedItemDefinition => GetSelectedSlot()?.Item;
    public bool IsFistSelected => SelectedItemDefinition != null && SelectedItemDefinition.Category == ItemCategory.Fist;
    public bool IsSelectedPlaceableBlock => GetSelectedSlot()?.IsPlaceableBlock == true;
    public bool CanUseSelectedItemForBreaking => GetSelectedSlot()?.CanBreakBlocks == true;
    public bool CanUseSelectedItemAsWeapon => GetSelectedSlot()?.CanUseAttackAnimation == true;
    public float SelectedBreakSpeedMultiplier => GetSelectedSlot()?.BreakSpeedMultiplier ?? 1f;

    private void Start()
    {
        InitializeItemData();
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

        slots[0].SetItem(RuntimeItemCatalog.GetOrCreateFist(fistIcon), 1);
    }

    private void InitializeItemData()
    {
        if (itemRegistry == null){
            itemRegistry = ItemRegistry.LoadDefault();
        }

        RuntimeItemCatalog.Configure(itemRegistry, fistIcon, GetStonePickaxeIcon());
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
        if (hotbarParent == null || slotPrefab == null){
            return;
        }

        uiSlots.Clear();
        uiSlotLookup.Clear();
        ClearUIContainer(hotbarParent);

        for (int i = 0; i < hotbarSlots; i++){
            InventoryUISlot uiSlot = CreateUISlot(i, hotbarParent);
            if (uiSlot == null){
                continue;
            }

            if (i == 0){
                uiSlot.SetFistSlot(true);
            }

            uiSlots.Add(uiSlot);
            uiSlotLookup[i] = uiSlot;
        }
    }

    private void CreateMainGridUI()
    {
        if (mainGridParent == null || slotPrefab == null){
            return;
        }

        ClearUIContainer(mainGridParent);

        for (int i = 0; i < mainGridSlots; i++){
            int slotIndex = hotbarSlots + i;
            InventoryUISlot uiSlot = CreateUISlot(slotIndex, mainGridParent);
            if (uiSlot == null){
                continue;
            }

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

        if (storeButton != null){
            storeButton.onClick.AddListener(OnStoreClicked);
            SetButtonLabel(storeButton, "Craft Pickaxe");
        }

        if (recycleButton != null) recycleButton.onClick.AddListener(OnRecycleClicked);
    }

    private void SetButtonLabel(Button button, string label)
    {
        if (button == null){
            return;
        }

        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        if (text != null){
            text.text = label;
        }
    }

    public void SelectSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex)){
            return;
        }

        selectedSlot = slotIndex;
        UpdateSlotHighlights();
    }

    public bool AddBlock(string legacyBlockName, TileBase blockTile, Sprite icon, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(legacyBlockName) || blockTile == null || amount <= 0){
            return false;
        }

        BlockItemDefinition blockDefinition = RuntimeItemCatalog.GetOrCreateBlock(
            legacyBlockName,
            blockTile,
            icon,
            ResolveDefaultBreakTime(legacyBlockName));
        return AddItem(blockDefinition, amount);
    }

    public bool AddItem(ItemDefinition item, int amount = 1)
    {
        return TryAddItem(item, amount, out _);
    }

    public bool RemoveBlock(int slotIndex, int amount = 1)
    {
        if (slotIndex == 0 || amount <= 0 || !IsValidSlotIndex(slotIndex)){
            return false;
        }

        InventorySlot slot = slots[slotIndex];
        if (slot.IsEmpty || slot.count < amount){
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

    private void UpdateSlotHighlights()
    {
        foreach (InventoryUISlot uiSlot in uiSlots){
            uiSlot.SetHighlight(uiSlot.slotIndex == selectedSlot);
        }
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

    private bool TryAddItem(ItemDefinition item, int amount, out int firstFilledSlot)
    {
        firstFilledSlot = -1;

        if (item == null || amount <= 0){
            return false;
        }

        if (!HasCapacityFor(item, amount)){
            Debug.Log("Inventory full!");
            return false;
        }

        int remaining = amount;

        if (item.CanStack){
            remaining = FillExistingStacks(item, remaining, ref firstFilledSlot);
        }

        remaining = FillEmptySlots(item, remaining, ref firstFilledSlot);
        return remaining == 0;
    }

    private bool HasCapacityFor(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0){
            return false;
        }

        if (!item.CanStack){
            return CountEmptySlots() >= amount;
        }

        int capacity = 0;

        for (int i = 1; i < slots.Count; i++){
            InventorySlot slot = slots[i];
            if (slot.CanStackWith(item)){
                capacity += item.MaxStackSize - slot.count;
            }
            else if (slot.IsEmpty){
                capacity += item.MaxStackSize;
            }

            if (capacity >= amount){
                return true;
            }
        }

        return false;
    }

    private int FillExistingStacks(ItemDefinition item, int remaining, ref int firstFilledSlot)
    {
        for (int i = 1; i < slots.Count && remaining > 0; i++){
            InventorySlot slot = slots[i];
            if (!slot.CanStackWith(item) || slot.count >= item.MaxStackSize){
                continue;
            }

            int addAmount = Mathf.Min(item.MaxStackSize - slot.count, remaining);
            slot.count += addAmount;
            remaining -= addAmount;

            if (firstFilledSlot < 0){
                firstFilledSlot = i;
            }

            UpdateUISlot(i);
        }

        return remaining;
    }

    private int FillEmptySlots(ItemDefinition item, int remaining, ref int firstFilledSlot)
    {
        for (int i = 1; i < slots.Count && remaining > 0; i++){
            InventorySlot slot = slots[i];
            if (!slot.IsEmpty){
                continue;
            }

            int addAmount = item.CanStack ? Mathf.Min(item.MaxStackSize, remaining) : 1;
            slot.SetItem(item, addAmount);
            remaining -= addAmount;

            if (firstFilledSlot < 0){
                firstFilledSlot = i;
            }

            UpdateUISlot(i);
        }

        return remaining;
    }

    private int CountEmptySlots()
    {
        int count = 0;

        for (int i = 1; i < slots.Count; i++){
            if (slots[i].IsEmpty){
                count++;
            }
        }

        return count;
    }

    private int CountItem(string itemId)
    {
        int count = 0;

        for (int i = 1; i < slots.Count; i++){
            InventorySlot slot = slots[i];
            if (slot.ItemId == itemId){
                count += slot.count;
            }
        }

        return count;
    }

    private bool HasItem(string itemId)
    {
        return CountItem(itemId) > 0;
    }

    private bool TryGetItemDefinition(string itemId, out ItemDefinition item)
    {
        if (RuntimeItemCatalog.TryGetItem(itemId, out item)){
            return true;
        }

        foreach (InventorySlot slot in slots){
            if (slot.ItemId == itemId && slot.Item != null){
                item = slot.Item;
                return true;
            }
        }

        item = null;
        return false;
    }

    private bool ConsumeItem(string itemId, int amount)
    {
        if (amount <= 0 || CountItem(itemId) < amount){
            return false;
        }

        int remaining = amount;

        for (int i = 1; i < slots.Count && remaining > 0; i++){
            InventorySlot slot = slots[i];
            if (slot.ItemId != itemId || slot.count <= 0){
                continue;
            }

            int removeAmount = Mathf.Min(slot.count, remaining);
            slot.count -= removeAmount;
            remaining -= removeAmount;

            if (slot.count <= 0){
                slot.Clear();
            }

            UpdateUISlot(i);
        }

        return remaining == 0;
    }

    private bool TryCraftRecipe(CraftingRecipeDefinition recipe, out int craftedSlotIndex)
    {
        craftedSlotIndex = -1;

        if (recipe == null || recipe.Output == null){
            return false;
        }

        foreach (CraftingIngredient ingredient in recipe.Ingredients){
            if (CountItem(ingredient.itemId) < ingredient.amount){
                return false;
            }
        }

        foreach (CraftingIngredient ingredient in recipe.Ingredients){
            if (!ConsumeItem(ingredient.itemId, ingredient.amount)){
                return false;
            }
        }

        if (TryAddItem(recipe.Output, recipe.OutputCount, out craftedSlotIndex)){
            return true;
        }

        foreach (CraftingIngredient ingredient in recipe.Ingredients){
            if (TryGetItemDefinition(ingredient.itemId, out ItemDefinition ingredientItem)){
                AddItem(ingredientItem, ingredient.amount);
            }
        }

        return false;
    }

    private float ResolveDefaultBreakTime(string legacyBlockName)
    {
        return legacyBlockName switch
        {
            "StoneTile" => 1.5f,
            _ => 0.5f,
        };
    }

    private Sprite GetStonePickaxeIcon()
    {
        return stonePickaxeIcon != null ? stonePickaxeIcon : GeneratedItemIcons.GetStonePickaxeIcon();
    }

    private void OnDropClicked()
    {
        if (selectedSlot == 0){
            return;
        }

        InventorySlot selected = GetSelectedSlot();
        if (selected != null && !selected.IsEmpty){
            Debug.Log($"Drop: {selected.DisplayName}");
            RemoveBlock(selectedSlot, 1);
        }
    }

    private void OnInfoClicked()
    {
        InventorySlot selected = GetSelectedSlot();
        if (selected == null || selected.IsEmpty){
            Debug.Log("Info: empty slot");
            return;
        }

        if (selected.IsTool){
            Debug.Log($"Info: {selected.DisplayName} ({selected.Category}) break x{selected.BreakSpeedMultiplier:0.0}");
            return;
        }

        Debug.Log($"Info: {selected.DisplayName} ({selected.Category}) x{selected.count}");
    }

    private void OnStoreClicked()
    {
        if (HasItem(RuntimeItemCatalog.StonePickaxeItemId)){
            Debug.Log("Stone Pickaxe already crafted.");
            return;
        }

        if (CountItem(DirtItemId) < StonePickaxeCraftCost){
            Debug.Log($"Need {StonePickaxeCraftCost} Dirt blocks to craft Stone Pickaxe.");
            return;
        }

        if (!TryGetItemDefinition(DirtItemId, out ItemDefinition dirtItem)){
            Debug.Log("Missing Dirt item definition for crafting.");
            return;
        }

        CraftingRecipeDefinition recipe = RuntimeItemCatalog.GetOrCreateStonePickaxeRecipe(
            dirtItem,
            GetStonePickaxeIcon(),
            StonePickaxeCraftCost,
            StonePickaxeBreakMultiplier);

        if (!TryCraftRecipe(recipe, out int craftedSlotIndex)){
            Debug.Log("Unable to craft Stone Pickaxe.");
            return;
        }

        SelectSlot(craftedSlotIndex);
        Debug.Log($"Crafted {recipe.DisplayName}.");
    }

    private void OnRecycleClicked()
    {
        if (selectedSlot == 0){
            return;
        }

        InventorySlot selected = GetSelectedSlot();
        if (selected != null && !selected.IsEmpty){
            Debug.Log($"Recycle: {selected.DisplayName}");
            RemoveBlock(selectedSlot, 1);
        }
    }
}
