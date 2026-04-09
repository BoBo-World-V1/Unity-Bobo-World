// ──────────────────────────────────────────────────────────────────────────────
// FUTURE REFACTOR NOTES — BlockInteraction.cs
// ──────────────────────────────────────────────────────────────────────────────
//
// STEP 1 — Create BlockData.cs (ScriptableObject)
//   - Move hardnessTable data out of this script
//   - Each block gets its own BlockData asset in the project
//   - Fields: breakTime, dropItem, dropAmount, crackSprites, breakSound
//
// STEP 2 — Create BlockRegistry.cs
//   - A lookup table that maps TileBase → BlockData
//   - Replace hardnessTable dictionary with BlockRegistry.GetBlockData(tile)
//   - Lives on a manager GameObject in the scene
//
// STEP 3 — Java Backend Integration
//   - BlockInteraction should NOT directly call groundTilemap.SetTile(cellPos, null)
//   - Instead: send a break request to Java via PacketSender.cs
//   - Java validates the break (ownership, permissions, range check server-side)
//   - Java sends back confirmation → only then remove tile from tilemap
//   - This prevents cheating and keeps world state in sync across all players
//
// STEP 4 — Multiplayer sync
//   - Other players breaking blocks comes through PacketReceiver.cs
//   - PacketReceiver calls a public method here like ApplyBlockBreak(cellPos)
//   - Show crack overlay progress for other players breaking blocks too
//
// ──────────────────────────────────────────────────────────────────────────────
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class BlockInteraction : MonoBehaviour
{
    private const float HealSecondsPerCrackStage = 5f;

    [Header("References")]
    public Tilemap groundTilemap;
    public Transform fistTransform;
    public Inventory inventory;          // Drag Player inventory here
    public WeaponSystem weaponSystem;        // Drag Player WeaponSystem here

    [Header("Settings")]
    public float reachDistance = 3f;
    public float defaultBreakTime = 0.5f;
    public float placeHoldTime = 0.3f;   // How long to hold to place

    [Header("Break Progress Visual")]
    public SpriteRenderer breakOverlay;
    public Sprite[] crackSprites;

    // Hardness table
    private Dictionary<string, float> hardnessTable = new Dictionary<string, float>()
    {
        { "DirtTile", 0.5f },
        // { "StoneTile", 1.5f },
        // { "WoodTile",  0.8f },
        // { "LavaTile",  3.0f },
    };

    // ── internals ──────────────────────────────────────────────────────────────
    private Camera mainCamera;

    // Break state
    private Vector3Int currentCell;
    private Vector3Int lastCell;
    private float breakProgress;
    private bool isBreaking;
    private float currentBreakTime;
    private float crackHealTimer;
    private int crackHealStage = -1;

    // Place state
    private Vector3Int placeCell;
    private Vector3Int lastPlaceCell;

    private void Awake(){
        mainCamera = Camera.main;

        // Listen to weapon system attack events to trigger block interactions
        if (weaponSystem != null) weaponSystem.onAttack += OnAttack;
    }
    private void OnDestroy(){
        if (weaponSystem != null) weaponSystem.onAttack -= OnAttack;
    }

    private void OnAttack(object sender, WeaponSystem.AttackEventArgs e) {
        if (inventory != null && inventory.IsFistSelected) TryBreakBlock();
    }

    private void Update(){
        // Left click — fist selected = break, block selected = place
        if (Input.GetMouseButton(0)){
            if (inventory != null && inventory.IsFistSelected) TryBreakBlock();
            else TryPlaceBlock();
        }

        if (Input.GetMouseButtonUp(0)){
            StopBreak();
            ResetPlace();
        }

        UpdateBreakHealing();
    }

    // ── BREAKING ──────────────────────────────────────────────────────────────

    private void TryBreakBlock(){
        if (groundTilemap == null) return;

        // Always use fist world position — it's already been set by WeaponSystem
        if (fistTransform == null) return;
        Vector3 fistPos = fistTransform.position;

        float dist = Vector2.Distance(transform.position, fistPos);
        if (dist > reachDistance){
            StopBreak();
            return;
        }

        currentCell = groundTilemap.WorldToCell(fistPos);

        if (currentCell != lastCell){
            ResetBreak(false);
            lastCell = currentCell;
        }

        TileBase tile = groundTilemap.GetTile(currentCell);
        if (tile == null){
            ResetBreak();
            return;
        }

        if (!isBreaking){
            isBreaking = true;
            currentBreakTime = GetBreakTime(tile);
            crackHealTimer = 0f;
            crackHealStage = -1;
        }

        breakProgress += Time.deltaTime / currentBreakTime;
        breakProgress = Mathf.Clamp01(breakProgress);

        UpdateCrackOverlay(currentCell);

        if (breakProgress >= 1f) BreakBlock(currentCell, tile);
    }

    private void BreakBlock(Vector3Int cellPos, TileBase tile){
        Vector3 worldPos = groundTilemap.GetCellCenterWorld(cellPos);
        groundTilemap.SetTile(cellPos, null);
        Debug.Log($"Broke {tile.name} at {cellPos}");

        Sprite tileSprite = GetTileSprite(tile);
        BlockDropSpawner.Instance?.SpawnDrop(tile.name, tile, tileSprite, 1, worldPos);

        ResetBreak();
    }

    // ── PLACING ───────────────────────────────────────────────────────────────

    private void TryPlaceBlock(){
        if (inventory == null || groundTilemap == null || mainCamera == null) return;

        // Get selected block from inventory
        InventorySlot selected = inventory.GetSelectedSlot();
        if (selected == null || selected.count <= 0 || selected.blockTile == null) return;

        // Get mouse world position
        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;

        // Range check
        float dist = Vector2.Distance(transform.position, mousePos);
        if (dist > reachDistance){
            ResetPlace();
            return;
        }

        placeCell = groundTilemap.WorldToCell(mousePos);

        // Reset progress if moved to different cell
        if (placeCell != lastPlaceCell){
            ResetPlace(false);
            lastPlaceCell = placeCell;
        }

        // Check cell is empty
        if (groundTilemap.GetTile(placeCell) != null){
            ResetPlace();
            return;
        }

        PlaceBlock(placeCell, selected);
    }

    private void PlaceBlock(Vector3Int cellPos, InventorySlot slot){
        groundTilemap.SetTile(cellPos, slot.blockTile);
        Debug.Log($"Placed {slot.blockName} at {cellPos}");

        // Remove 1 from inventory
        inventory.RemoveBlock(inventory.selectedSlot, 1);

        ResetPlace();
    }

    // ── HELPERS ───────────────────────────────────────────────────────────────

    private Sprite GetTileSprite(TileBase tile){
        if (tile is Tile t) return t.sprite;
        return null;
    }

    private void UpdateCrackOverlay(Vector3Int cellPos){
        if (breakOverlay == null || crackSprites == null || crackSprites.Length == 0) return;

        breakOverlay.gameObject.SetActive(true);
        breakOverlay.transform.position = groundTilemap.GetCellCenterWorld(cellPos);

        int stage = Mathf.FloorToInt(breakProgress * crackSprites.Length);
        stage = Mathf.Clamp(stage, 0, crackSprites.Length - 1);
        breakOverlay.sprite = crackSprites[stage];

        Color c = breakOverlay.color;
        c.a = Mathf.Lerp(0.3f, 1f, breakProgress);
        breakOverlay.color = c;
    }

    private void ResetBreak(bool clearCell = true){
        isBreaking = false;
        breakProgress = 0f;
        currentBreakTime = 0f;
        crackHealTimer = 0f;
        crackHealStage = -1;

        if (clearCell){
            currentCell = Vector3Int.zero;
            lastCell = Vector3Int.zero;
        }

        if (breakOverlay != null) breakOverlay.gameObject.SetActive(false);
    }

    private void StopBreak(){
        isBreaking = false;
        currentBreakTime = 0f;
    }

    private void UpdateBreakHealing(){
        if (isBreaking || breakProgress <= 0f) return;

        int stage = GetCurrentCrackStage();
        if (stage < 0) return;

        if (stage != crackHealStage){
            crackHealStage = stage;
            crackHealTimer = 0f;
        }

        float healTime = GetHealTimeForCrackStage(stage);
        if (healTime <= 0f) return;

        crackHealTimer += Time.deltaTime;
        if (crackHealTimer < healTime) return;

        crackHealTimer = 0f;

        int nextStage = stage - 1;
        if (nextStage >= 0){
            breakProgress = GetProgressForCrackStage(nextStage);
            crackHealStage = nextStage;
            UpdateCrackOverlay(currentCell);
            return;
        }

        breakProgress = 0f;
        crackHealStage = -1;

        if (breakOverlay != null) breakOverlay.gameObject.SetActive(false);

        currentCell = Vector3Int.zero;
        lastCell = Vector3Int.zero;
    }

    private int GetCurrentCrackStage(){
        int stageCount = GetCrackStageCount();
        if (stageCount <= 0) return -1;

        int stage = Mathf.FloorToInt(breakProgress * stageCount);
        return Mathf.Clamp(stage, 0, stageCount - 1);
    }

    private float GetHealTimeForCrackStage(int stage){ return (stage + 1) * HealSecondsPerCrackStage; }

    private float GetProgressForCrackStage(int stage){
        int stageCount = GetCrackStageCount();
        if (stageCount <= 0) return 0f;

        float step = 1f / stageCount;
        float progress = (stage + 0.5f) * step;
        return Mathf.Clamp(progress, 0.001f, 0.999f);
    }

    private int GetCrackStageCount(){
        if (crackSprites == null || crackSprites.Length == 0) return 0;
        return crackSprites.Length;
    }

    private void ResetPlace(bool clearCell = true){
        if (clearCell){
            placeCell = Vector3Int.zero;
            lastPlaceCell = Vector3Int.zero;
        }
    }

    private float GetBreakTime(TileBase tile){
        if (hardnessTable.TryGetValue(tile.name, out float time)) return time;
        return defaultBreakTime;
    }

    private void OnDrawGizmosSelected(){
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, reachDistance);
    }
}