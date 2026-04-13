using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "ItemRegistry", menuName = "Game/Items/Item Registry")]
public class ItemRegistry : ScriptableObject
{
    [Header("Items")]
    [SerializeField] private List<FistItemDefinition> coreItems = new();
    [SerializeField] private List<BlockItemDefinition> blockItems = new();
    [SerializeField] private List<ToolItemDefinition> toolItems = new();
    [SerializeField] private List<WeaponItemDefinition> weaponItems = new();
    [SerializeField] private List<ItemDefinition> consumableItems = new();

    [FormerlySerializedAs("items")]
    [SerializeField, HideInInspector] private List<ItemDefinition> legacyItems = new();

    [Header("Crafting")]
    [SerializeField] private List<CraftingRecipeDefinition> recipes = new();

    private readonly Dictionary<string, ItemDefinition> itemsById = new();
    private readonly Dictionary<string, CraftingRecipeDefinition> recipesById = new();
    private readonly Dictionary<TileBase, BlockItemDefinition> blocksByTile = new();
    private readonly List<ItemDefinition> allItems = new();

    public IReadOnlyList<ItemDefinition> Items => allItems;
    public IReadOnlyList<FistItemDefinition> CoreItems => coreItems;
    public IReadOnlyList<BlockItemDefinition> BlockItems => blockItems;
    public IReadOnlyList<ToolItemDefinition> ToolItems => toolItems;
    public IReadOnlyList<WeaponItemDefinition> WeaponItems => weaponItems;
    public IReadOnlyList<ItemDefinition> ConsumableItems => consumableItems;
    public IReadOnlyList<CraftingRecipeDefinition> Recipes => recipes;

    public static ItemRegistry LoadDefault()
    {
        return Resources.Load<ItemRegistry>("GameData/ItemRegistry");
    }

    private void OnEnable()
    {
        RebuildCaches();
    }

    private void OnValidate()
    {
        SortLegacyItemsIntoCategories();
    }

    [ContextMenu("Sort Items Into Category Lists")]
    public void SortItemsIntoCategoryLists()
    {
        SortLegacyItemsIntoCategories();
    }

    public void RebuildCaches()
    {
        SortLegacyItemsIntoCategories();

        itemsById.Clear();
        recipesById.Clear();
        blocksByTile.Clear();
        allItems.Clear();

        AddItemsToCaches(coreItems);
        AddItemsToCaches(blockItems);
        AddItemsToCaches(toolItems);
        AddItemsToCaches(weaponItems);
        AddItemsToCaches(consumableItems);

        foreach (CraftingRecipeDefinition recipe in recipes){
            if (recipe == null || string.IsNullOrWhiteSpace(recipe.RecipeId)){
                continue;
            }

            recipesById[recipe.RecipeId] = recipe;
        }
    }

    public void ApplyRuntimeDefaults(Sprite fistIcon, Sprite stonePickaxeIcon)
    {
        if (TryGetItem(RuntimeItemCatalog.FistItemId, out ItemDefinition fistItem)){
            fistItem.UpdateIcon(fistIcon);
        }

        if (TryGetItem(RuntimeItemCatalog.StonePickaxeItemId, out ItemDefinition stonePickaxeItem) && stonePickaxeIcon != null){
            stonePickaxeItem.UpdateIcon(stonePickaxeIcon);
        }
    }

    public bool TryGetItem(string itemId, out ItemDefinition item)
    {
        if (itemsById.Count == 0){
            RebuildCaches();
        }

        return itemsById.TryGetValue(itemId, out item);
    }

    public bool TryGetRecipe(string recipeId, out CraftingRecipeDefinition recipe)
    {
        if (recipesById.Count == 0){
            RebuildCaches();
        }

        return recipesById.TryGetValue(recipeId, out recipe);
    }

    public bool TryGetBlockByTile(TileBase tile, out BlockItemDefinition block)
    {
        if (blocksByTile.Count == 0){
            RebuildCaches();
        }

        if (tile != null && blocksByTile.TryGetValue(tile, out block)){
            return true;
        }

        block = null;
        return false;
    }

    private void AddItemsToCaches<T>(IEnumerable<T> source) where T : ItemDefinition
    {
        foreach (T item in source){
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId)){
                continue;
            }

            if (!itemsById.ContainsKey(item.ItemId)){
                itemsById[item.ItemId] = item;
                allItems.Add(item);
            }

            if (item is BlockItemDefinition block && block.PlacementTile != null){
                blocksByTile[block.PlacementTile] = block;
            }
        }
    }

    private void SortLegacyItemsIntoCategories()
    {
        MoveItemsIntoCategories(legacyItems);
        legacyItems.Clear();

        SortList(coreItems);
        SortList(blockItems);
        SortList(toolItems);
        SortList(weaponItems);
        SortList(consumableItems);
    }

    private void MoveItemsIntoCategories(IEnumerable<ItemDefinition> source)
    {
        foreach (ItemDefinition item in source){
            if (item == null){
                continue;
            }

            switch (item){
                case FistItemDefinition fist:
                    AddUnique(coreItems, fist);
                    break;
                case BlockItemDefinition block:
                    AddUnique(blockItems, block);
                    break;
                case ToolItemDefinition tool:
                    AddUnique(toolItems, tool);
                    break;
                case WeaponItemDefinition weapon:
                    AddUnique(weaponItems, weapon);
                    break;
                default:
                    AddUnique(consumableItems, item);
                    break;
            }
        }
    }

    private void AddUnique<T>(List<T> list, T item) where T : Object
    {
        if (item != null && !list.Contains(item)){
            list.Add(item);
        }
    }

    private void SortList<T>(List<T> list) where T : ItemDefinition
    {
        list.Sort((left, right) => string.Compare(
            left != null ? left.DisplayName : string.Empty,
            right != null ? right.DisplayName : string.Empty,
            System.StringComparison.OrdinalIgnoreCase));
    }
}
