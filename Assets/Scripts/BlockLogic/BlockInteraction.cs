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

    // Place state
    private float placeHoldProgress;
    private Vector3Int placeCell;
    private Vector3Int lastPlaceCell;
    private bool isPlacing;

    private void Awake()
    {
        mainCamera = Camera.main;

        // Listen to weapon system attack events to trigger block interactions
        if (weaponSystem != null)
            weaponSystem.onAttack += OnAttack;
    }
    private void OnDestroy()
    {
        if (weaponSystem != null)
            weaponSystem.onAttack -= OnAttack;
    }

    private void OnAttack(object sender, WeaponSystem.AttackEventArgs e)
    {
        if (inventory.IsFistSelected)
            TryBreakBlock();
    }

    private void Update()
    {
        // Left click — fist selected = break, block selected = place
        if (Input.GetMouseButton(0))
        {
            if (inventory != null && inventory.IsFistSelected)
                TryBreakBlock();
            else
                TryPlaceBlock();
        }

        if (Input.GetMouseButtonUp(0))
        {
            ResetBreak();
            ResetPlace();
        }
    }

    // ── BREAKING ──────────────────────────────────────────────────────────────

    private void TryBreakBlock()
    {
        // Always use fist world position — it's already been set by WeaponSystem
        if (fistTransform == null) return;
        Vector3 fistPos = fistTransform != null
            ? fistTransform.position
            : mainCamera.ScreenToWorldPoint(Input.mousePosition);

        float dist = Vector2.Distance(transform.position, fistPos);
        if (dist > reachDistance)
        {
            ResetBreak();
            return;
        }

        currentCell = groundTilemap.WorldToCell(fistPos);

        if (currentCell != lastCell)
        {
            ResetBreak(false);
            lastCell = currentCell;
        }

        TileBase tile = groundTilemap.GetTile(currentCell);
        if (tile == null)
        {
            ResetBreak();
            return;
        }

        if (!isBreaking)
        {
            isBreaking = true;
            currentBreakTime = GetBreakTime(tile);
            breakProgress = 0f;
        }

        breakProgress += Time.deltaTime / currentBreakTime;
        breakProgress = Mathf.Clamp01(breakProgress);

        UpdateCrackOverlay(currentCell);

        if (breakProgress >= 1f)
            BreakBlock(currentCell, tile);
    }

    private void BreakBlock(Vector3Int cellPos, TileBase tile)
    {
        Vector3 worldPos = groundTilemap.GetCellCenterWorld(cellPos);
        groundTilemap.SetTile(cellPos, null);
        Debug.Log($"Broke {tile.name} at {cellPos}");

        Sprite tileSprite = GetTileSprite(tile);
        BlockDropSpawner.Instance?.SpawnDrop(tile.name, tile, tileSprite, 1, worldPos);

        ResetBreak();
    }

    // ── PLACING ───────────────────────────────────────────────────────────────

    private void TryPlaceBlock()
    {
        // Get selected block from inventory
        InventorySlot selected = inventory.GetSelectedSlot();
        if (selected == null || selected.count <= 0 || selected.blockTile == null)
            return;

        // Get mouse world position
        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;

        // Range check
        float dist = Vector2.Distance(transform.position, mousePos);
        if (dist > reachDistance)
        {
            ResetPlace();
            return;
        }

        placeCell = groundTilemap.WorldToCell(mousePos);

        // Reset progress if moved to different cell
        if (placeCell != lastPlaceCell)
        {
            ResetPlace(false);
            lastPlaceCell = placeCell;
        }

        // Check cell is empty
        if (groundTilemap.GetTile(placeCell) != null)
        {
            ResetPlace();
            return;
        }

        // Accumulate hold progress
        if (!isPlacing)
        {
            isPlacing = true;
            placeHoldProgress = 0f;
        }

        placeHoldProgress += Time.deltaTime / placeHoldTime;
        placeHoldProgress = Mathf.Clamp01(placeHoldProgress);

        if (placeHoldProgress >= 1f)
            PlaceBlock(placeCell, selected);
    }

    private void PlaceBlock(Vector3Int cellPos, InventorySlot slot)
    {
        groundTilemap.SetTile(cellPos, slot.blockTile);
        Debug.Log($"Placed {slot.blockName} at {cellPos}");

        // Remove 1 from inventory
        inventory.RemoveBlock(inventory.selectedSlot, 1);

        ResetPlace();
    }

    // ── HELPERS ───────────────────────────────────────────────────────────────

    private Sprite GetTileSprite(TileBase tile)
    {
        if (tile is Tile t) return t.sprite;
        return null;
    }

    private void UpdateCrackOverlay(Vector3Int cellPos)
    {
        if (breakOverlay == null || crackSprites == null || crackSprites.Length == 0)
            return;

        breakOverlay.gameObject.SetActive(true);
        breakOverlay.transform.position = groundTilemap.GetCellCenterWorld(cellPos);

        int stage = Mathf.FloorToInt(breakProgress * crackSprites.Length);
        stage = Mathf.Clamp(stage, 0, crackSprites.Length - 1);
        breakOverlay.sprite = crackSprites[stage];

        Color c = breakOverlay.color;
        c.a = Mathf.Lerp(0.3f, 1f, breakProgress);
        breakOverlay.color = c;
    }

    private void ResetBreak(bool clearCell = true)
    {
        isBreaking = false;
        breakProgress = 0f;
        currentBreakTime = 0f;

        if (clearCell)
        {
            currentCell = Vector3Int.zero;
            lastCell = Vector3Int.zero;
        }

        if (breakOverlay != null)
            breakOverlay.gameObject.SetActive(false);
    }

    private void ResetPlace(bool clearCell = true)
    {
        isPlacing = false;
        placeHoldProgress = 0f;

        if (clearCell)
        {
            placeCell = Vector3Int.zero;
            lastPlaceCell = Vector3Int.zero;
        }
    }

    private float GetBreakTime(TileBase tile)
    {
        if (hardnessTable.TryGetValue(tile.name, out float time))
            return time;
        return defaultBreakTime;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, reachDistance);
    }
}