Legend:

✅ good as-is for now
🛠️ works but should be refactored soon
⬜ missing / not future-ready yet
Current Runtime Files

✅ PlayerController.cs
✅ DroppedBlock.cs
✅ BlockDropSpawner.cs
🛠️ BlockInteraction.cs
🛠️ WeaponSystem.cs
🛠️ Inventory.cs
✅ InventorySlot.cs
✅ InventoryUISlot.cs
🛠️ InventoryDragHandle.cs

Item/Data Architecture
✅ ItemCategory.cs
✅ ItemDefinition.cs
✅ BlockItemDefinition.cs
✅ ToolItemDefinition.cs
🛠️ WeaponItemDefinition.cs
✅ CraftingRecipeDefinition.cs
✅ ItemRegistry.cs
🛠️ RuntimeItemCatalog.cs
🛠️ GeneratedItemIcons.cs

Unity Asset Setup
✅ ItemRegistry.asset
✅ DirtItem.asset
✅ FistItem.asset
✅ StonePickaxeItem.asset
✅ StonePickaxeRecipe.asset
⬜ More real item assets for future content
⬜ More recipe assets
⬜ Weapon assets
⬜ Consumable assets

What’s Lagging Behind
🛠️ Inventory still owns too much logic
🛠️ Crafting is still too tied to Inventory
🛠️ Weapon flow is not fully data-driven yet
🛠️ Block interaction still has some hardcoded world logic
🛠️ Runtime fallback still exists instead of pure asset-driven flow
⬜ Save/load by itemId
⬜ World persistence by stable IDs
⬜ Player persistence
⬜ Multiplayer-ready item sync
⬜ Server-authoritative action flow

Good As-Is For Current Prototype
✅ Basic inventory structure
✅ Basic item definitions
✅ Basic recipe definitions
✅ Asset-backed item setup
✅ Pickaxe progression loop
✅ Block place/break loop
✅ Dropped item pickup loop

Needs Refactor Soon
🛠️ RuntimeItemCatalog fallback behavior
🛠️ WeaponSystem using true weapon stats
🛠️ Crafting logic placement
🛠️ Inventory class size/responsibility
🛠️ BlockInteraction responsibility split

Can Wait Until Later
⬜ Full weapon system
⬜ Consumables
⬜ Advanced crafting UI
⬜ Database
⬜ Java networking
⬜ Authentication
⬜ Trading/social systems

Best Next Refactor Priorities
🛠️ Make ItemRegistry the real source of truth
🛠️ Reduce RuntimeItemCatalog to a temporary helper or remove it later
🛠️ Move crafting logic into its own system
🛠️ Make WeaponSystem fully item-data-driven
⬜ Implement save/load using itemId
⬜ Prepare item/world sync for Java backend