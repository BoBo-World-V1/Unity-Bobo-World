Legend:
✅ completed or basically working
🛠️ partially done / works but should be refactored or expanded
⬜ not done yet / not visible in current code

Core Gameplay
✅ Player movement
✅ Jumping/basic grounded movement
✅ Block breaking
✅ Block placement
✅ Reach distance checks
🛠️ Block placement safety rules
🛠️ Input system consistency
⬜ Spawn point system
⬜ Death/respawn system
⬜ Damage/health system
⬜ Enemy/NPC gameplay
⬜ Tutorial/onboarding flow

World System
✅ Tilemap-based world interaction
✅ Basic block hardness support
✅ World item drops from broken blocks
🛠️ World saving/loading
🛠️ Block data structure
🛠️ World rules/validation
⬜ Block registry / ScriptableObject block database
⬜ Background/foreground block layers
⬜ World generation or world templates
⬜ Chunk/region loading
⬜ World ownership/permissions
⬜ Lighting/day-night/weather

Inventory & Items
✅ Inventory slots
✅ Hotbar selection
✅ Inventory UI slots
✅ Stacking
✅ Pickup into inventory
✅ Selected-slot placement flow
🛠️ Inventory drag/open-close UX
🛠️ Drop/recycle/info buttons
🛠️ Item data model
⬜ Drag and drop between slots
⬜ Drop item from inventory into world
⬜ Item descriptions/stats panel
⬜ Consumables/material/tool item categories
⬜ Inventory persistence synced with save/server

Tools / Weapons / Combat
✅ Fist weapon behavior
✅ Aim + basic attack animation
✅ Attack event hook
🛠️ Weapon system structure
🛠️ Tool/weapon expansion path
⬜ Multiple tools/weapons
⬜ Tool switching/equipment manager
⬜ Tool speed modifiers
⬜ Durability
⬜ Combat hit detection
⬜ Enemy/player damage interactions
⬜ Cooldown balancing

Player Systems
🛠️ Animation hookups
🛠️ Mobile input entry points
⬜ Player stats
⬜ Level/XP
⬜ Currency/gems
⬜ Player profile data
⬜ Cosmetics/customization

UI / UX
✅ Inventory UI foundation
✅ Hotbar highlight/selection
🛠️ Action buttons UI
🛠️ Blocking gameplay input over UI
⬜ Main menu
⬜ Pause/settings menu
⬜ HUD for health/stats/currency
⬜ World loading UI
⬜ Connection status UI
⬜ Controller navigation polish
⬜ Accessibility options

Audio / Juice
🛠️ Visual break crack feedback
🛠️ Fist hover/pop animation feel
⬜ Block break sounds
⬜ Block place sounds
⬜ Pickup sounds
⬜ Footsteps/jump/land sounds
⬜ Particles
⬜ Camera shake
⬜ Music/ambience

Codebase / Architecture
✅ Scripts split by gameplay domain
✅ Prototype is compile-clean
🛠️ Client gameplay logic separation
🛠️ Refactor notes already identified in scripts
🛠️ Preparation for server authority
⬜ Shared data contracts
⬜ Dedicated world state layer
⬜ Formal event/message architecture
⬜ Testing 

Java Backend Foundation
⬜ Java version finalized
⬜ Server project created
⬜ Networking library chosen/finalized
⬜ Build tool chosen
⬜ Dedicated server loop
⬜ Server config system
⬜ Logging/monitoring
⬜ Dev/staging/prod setup

Networking Design
🛠️ High-level direction exists
⬜ Protocol finalized
⬜ Packet format finalized
⬜ Movement packets
⬜ Place/break packets
⬜ Pickup packets
⬜ Inventory sync packets
⬜ Join/leave/world sync flow
⬜ Latency handling
⬜ Reconnect logic
⬜ Version compatibility plan

Server Authority / Multiplayer
⬜ Authoritative movement validation
⬜ Authoritative block break validation
⬜ Authoritative block place validation
⬜ Authoritative pickup validation
⬜ Other player replication
⬜ Other player interpolation
⬜ Client prediction/reconciliation
⬜ Multiplayer world state sync

Persistence / Database
🛠️ Local world persistence exists in some form
⬜ Database choice finalized
⬜ Player save schema
⬜ World save schema
⬜ Inventory persistence schema
⬜ Account persistence
⬜ Backup/recovery strategy

Authentication / Accounts
⬜ Login system
⬜ Registration flow
⬜ Session/token handling
⬜ Password hashing
⬜ Guest login decision
⬜ Ban/moderation account tools

Online Social Features
⬜ Chat
⬜ Player names/nameplates
⬜ Friends
⬜ Private messages
⬜ World permissions
⬜ Admin/moderation commands

Security / Anti-Cheat
⬜ Range validation on server
⬜ Movement anti-speedhack checks
⬜ Inventory anti-duplication checks
⬜ Rate limiting
⬜ Packet validation
⬜ Unauthorized edit prevention

Testing / Release Readiness
✅ C# project builds cleanly
🛠️ Manual gameplay testing
⬜ Unit tests
⬜ Integration tests
⬜ Multiclient multiplayer tests
⬜ Mobile device test pass
⬜ Controller full test pass
⬜ Performance profiling
⬜ Release pipeline