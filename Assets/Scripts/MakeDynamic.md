Legend:

✅ already dynamic enough for now
🛠️ partially dynamic, should be refactored
⬜ still missing
Items

✅ ItemDefinition asset system
✅ categorized ItemRegistry.asset
✅ itemId-based item identity
🛠️ RuntimeItemCatalog.cs still has item-specific helper methods like GetOrCreateStonePickaxe
🛠️ Inventory.cs still knows about DirtTile and StoneTile
⬜ generic item database flow with no item-specific code branches
Blocks

✅ block items can store placementTile
✅ block items can store breakTime
🛠️ BlockInteraction.cs still has a fallback hardnessTable
🛠️ Inventory.cs still has ResolveDefaultBreakTime()
⬜ all block stats fully driven by BlockItemDefinition
⬜ optional per-block sounds/effects driven by data
Crafting

✅ recipe asset exists
✅ recipe ingredients are data-driven
🛠️ Inventory.cs still has a special OnStoreClicked() for only Stone Pickaxe
🛠️ RuntimeItemCatalog.cs still has a dedicated StonePickaxeRecipeId
⬜ generic recipe selection UI
⬜ generic craft-any-recipe button flow
⬜ crafting manager separate from inventory
Tools

✅ tool break speed is data-driven
✅ selected item controls mining speed
🛠️ current tool flow is still basically “stone pickaxe special path”
⬜ multiple tool tiers driven only by assets
⬜ tool-specific rules like canBreakStone, canBreakWood, etc.
⬜ durability if desired
Weapons

✅ WeaponItemDefinition exists
🛠️ WeaponSystem.cs does not yet fully read weapon asset stats
🛠️ current attack logic is still fist-oriented
⬜ damage/range/cooldown driven directly from weapon assets
⬜ multiple weapon types
⬜ equipped-weapon system
Inventory

✅ inventory stores item definitions instead of loose raw fields
✅ stacking is mostly generic
🛠️ Inventory.cs still owns too many systems
🛠️ action buttons are still fixed-purpose
⬜ generic item use / equip / craft routing
⬜ inventory persistence by itemId
Audio

✅ central GameAudio.cs
🛠️ sounds are still event-based, not data-driven per item/block
⬜ per-block break/place audio from data
⬜ per-tool/weapon audio from data
Save / Backend Readiness

✅ itemId system exists
🛠️ some legacy tile-name assumptions still exist
⬜ save/load by itemId
⬜ world data by stable ids
⬜ multiplayer sync by ids and actions
⬜ server-authoritative crafting/inventory flow
Best Dynamic Refactor Priorities

🛠️ remove item-specific logic from Inventory.cs
🛠️ replace stone-pickaxe-specific crafting flow with generic recipe crafting
🛠️ remove fallback block break-time tables from code
🛠️ make RuntimeItemCatalog.cs generic instead of item-specific
🛠️ make WeaponSystem.cs fully read weapon asset data
⬜ add save/load using itemId
⬜ prepare item/crafting sync for Java server