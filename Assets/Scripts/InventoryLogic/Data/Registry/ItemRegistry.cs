using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "ItemRegistry", menuName = "Game/Items/Item Registry")]
public class ItemRegistry : ScriptableObject
{
    [SerializeField] private List<ItemDefinition> items = new();
    [SerializeField] private List<CraftingRecipeDefinition> recipes = new();

    private readonly Dictionary<string, ItemDefinition> itemsById = new();
    private readonly Dictionary<string, CraftingRecipeDefinition> recipesById = new();
    private readonly Dictionary<TileBase, BlockItemDefinition> blocksByTile = new();

    public IReadOnlyList<ItemDefinition> Items => items;
    public IReadOnlyList<CraftingRecipeDefinition> Recipes => recipes;

    public static ItemRegistry LoadDefault()
    {
        return Resources.Load<ItemRegistry>("GameData/ItemRegistry");
    }

    private void OnEnable()
    {
        RebuildCaches();
    }

    public void RebuildCaches()
    {
        itemsById.Clear();
        recipesById.Clear();
        blocksByTile.Clear();

        foreach (ItemDefinition item in items){
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId)){
                continue;
            }

            itemsById[item.ItemId] = item;

            if (item is BlockItemDefinition block && block.PlacementTile != null){
                blocksByTile[block.PlacementTile] = block;
            }
        }

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
}
