using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class RuntimeItemCatalog
{
    public const string FistItemId = "core.fist";

    private static readonly Dictionary<string, ItemDefinition> itemsById = new();
    private static readonly Dictionary<TileBase, BlockItemDefinition> blocksByTile = new();
    private static readonly Dictionary<string, CraftingRecipeDefinition> recipesById = new();
    private static ItemRegistry itemRegistry;

    public static void Configure(ItemRegistry registry)
    {
        itemRegistry = registry;
        if (itemRegistry != null){
            itemRegistry.RebuildCaches();
        }
    }

    public static ItemRegistry GetRegistry()
    {
        if (itemRegistry == null){
            itemRegistry = ItemRegistry.LoadDefault();
        }

        return itemRegistry;
    }

    public static string GetBlockItemId(string legacyBlockName)
    {
        return $"block.{NormalizeId(legacyBlockName)}";
    }

    public static FistItemDefinition GetOrCreateFist(Sprite icon)
    {
        ItemRegistry registry = GetRegistry();
        if (registry != null && registry.TryGetItem(FistItemId, out ItemDefinition registeredItem) && registeredItem is FistItemDefinition registeredFist){
            return registeredFist;
        }

        if (!itemsById.TryGetValue(FistItemId, out ItemDefinition existingItem)){
            FistItemDefinition fist = CreateRuntimeDefinition<FistItemDefinition>();
            fist.InitializeRuntime(icon);
            itemsById[FistItemId] = fist;
            return fist;
        }

        FistItemDefinition existingFist = existingItem as FistItemDefinition;
        existingFist?.UpdateIcon(icon);
        return existingFist;
    }

    public static BlockItemDefinition GetOrCreateBlock(string legacyBlockName, TileBase tile, Sprite icon, float breakTime)
    {
        string itemId = GetBlockItemId(legacyBlockName);
        string displayName = string.IsNullOrWhiteSpace(legacyBlockName) ? "Block" : legacyBlockName;

        ItemRegistry registry = GetRegistry();
        if (registry != null){
            if (tile != null && registry.TryGetBlockByTile(tile, out BlockItemDefinition blockByTile)){
                return blockByTile;
            }

            if (registry.TryGetItem(itemId, out ItemDefinition registeredItem) && registeredItem is BlockItemDefinition registeredBlock){
                return registeredBlock;
            }
        }

        if (!itemsById.TryGetValue(itemId, out ItemDefinition existingItem)){
            BlockItemDefinition block = CreateRuntimeDefinition<BlockItemDefinition>();
            block.InitializeRuntime(itemId, displayName, icon, tile, breakTime);
            itemsById[itemId] = block;

            if (tile != null){
                blocksByTile[tile] = block;
            }

            return block;
        }

        BlockItemDefinition existingBlock = existingItem as BlockItemDefinition;
        existingBlock?.UpdateIcon(icon);
        existingBlock?.UpdateTile(tile, breakTime);

        if (tile != null && existingBlock != null){
            blocksByTile[tile] = existingBlock;
        }

        return existingBlock;
    }

    public static bool TryGetBlock(TileBase tile, out BlockItemDefinition block)
    {
        ItemRegistry registry = GetRegistry();
        if (registry != null && registry.TryGetBlockByTile(tile, out block)){
            return true;
        }

        if (tile != null && blocksByTile.TryGetValue(tile, out BlockItemDefinition existingBlock)){
            block = existingBlock;
            return true;
        }

        block = null;
        return false;
    }

    public static bool TryGetRecipe(string recipeId, out CraftingRecipeDefinition recipe)
    {
        ItemRegistry registry = GetRegistry();
        if (registry != null && registry.TryGetRecipe(recipeId, out recipe)){
            return true;
        }

        return recipesById.TryGetValue(recipeId, out recipe);
    }

    public static bool TryGetItem(string itemId, out ItemDefinition item)
    {
        ItemRegistry registry = GetRegistry();
        if (registry != null && registry.TryGetItem(itemId, out item)){
            return true;
        }

        return itemsById.TryGetValue(itemId, out item);
    }

    private static T CreateRuntimeDefinition<T>() where T : ScriptableObject
    {
        T definition = ScriptableObject.CreateInstance<T>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        return definition;
    }

    private static string NormalizeId(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)){
            return "item";
        }

        return rawValue.Trim().ToLowerInvariant().Replace(" ", "_");
    }
}
