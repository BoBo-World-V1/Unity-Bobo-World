using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class RuntimeItemCatalog
{
    public const string FistItemId = "core.fist";
    public const string StonePickaxeItemId = "tool.stone_pickaxe";
    public const string StonePickaxeRecipeId = "recipe.stone_pickaxe";

    private static readonly Dictionary<string, ItemDefinition> itemsById = new();
    private static readonly Dictionary<TileBase, BlockItemDefinition> blocksByTile = new();
    private static readonly Dictionary<string, CraftingRecipeDefinition> recipesById = new();
    private static ItemRegistry itemRegistry;

    public static void Configure(ItemRegistry registry, Sprite fistIcon = null, Sprite stonePickaxeIcon = null)
    {
        itemRegistry = registry;
        if (itemRegistry != null){
            itemRegistry.RebuildCaches();
            itemRegistry.ApplyRuntimeDefaults(fistIcon, stonePickaxeIcon);
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
            registeredFist.UpdateIcon(icon);
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
                blockByTile.UpdateIcon(icon);
                blockByTile.UpdateTile(tile, breakTime);
                return blockByTile;
            }

            if (registry.TryGetItem(itemId, out ItemDefinition registeredItem) && registeredItem is BlockItemDefinition registeredBlock){
                registeredBlock.UpdateIcon(icon);
                registeredBlock.UpdateTile(tile, breakTime);
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

    public static ToolItemDefinition GetOrCreateStonePickaxe(Sprite icon, float breakSpeedMultiplier)
    {
        ItemRegistry registry = GetRegistry();
        if (registry != null && registry.TryGetItem(StonePickaxeItemId, out ItemDefinition registeredItem) && registeredItem is ToolItemDefinition registeredTool){
            registeredTool.UpdateIcon(icon);
            return registeredTool;
        }

        if (!itemsById.TryGetValue(StonePickaxeItemId, out ItemDefinition existingItem)){
            ToolItemDefinition tool = CreateRuntimeDefinition<ToolItemDefinition>();
            tool.InitializeRuntime(StonePickaxeItemId, "Stone Pickaxe", icon, breakSpeedMultiplier, true);
            itemsById[StonePickaxeItemId] = tool;
            return tool;
        }

        ToolItemDefinition existingTool = existingItem as ToolItemDefinition;
        existingTool?.UpdateIcon(icon);
        return existingTool;
    }

    public static CraftingRecipeDefinition GetOrCreateStonePickaxeRecipe(ItemDefinition dirtItem, Sprite icon, int dirtCost, float breakSpeedMultiplier)
    {
        if (dirtItem == null){
            return null;
        }

        ItemRegistry registry = GetRegistry();
        if (registry != null && registry.TryGetRecipe(StonePickaxeRecipeId, out CraftingRecipeDefinition registeredRecipe)){
            GetOrCreateStonePickaxe(icon, breakSpeedMultiplier);
            return registeredRecipe;
        }

        ToolItemDefinition output = GetOrCreateStonePickaxe(icon, breakSpeedMultiplier);

        if (!recipesById.TryGetValue(StonePickaxeRecipeId, out CraftingRecipeDefinition recipe)){
            recipe = CreateRuntimeDefinition<CraftingRecipeDefinition>();
            recipe.InitializeRuntime(
                StonePickaxeRecipeId,
                "Stone Pickaxe",
                output,
                1,
                new CraftingIngredient(dirtItem.ItemId, dirtCost));
            recipesById[StonePickaxeRecipeId] = recipe;
            return recipe;
        }

        recipe.InitializeRuntime(
            StonePickaxeRecipeId,
            "Stone Pickaxe",
            output,
            1,
            new CraftingIngredient(dirtItem.ItemId, dirtCost));
        return recipe;
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
