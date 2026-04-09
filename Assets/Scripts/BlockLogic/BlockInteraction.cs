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
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BlockInteraction : MonoBehaviour
{
    private const float HealSecondsPerCrackStage = 5f;

    private struct CrackState
    {
        public float progress;
        public float healTimer;
        public int healStage;
        public bool isBeingBroken;
    }

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
    private readonly Dictionary<Vector3Int, CrackState> crackStates = new Dictionary<Vector3Int, CrackState>();
    private readonly Dictionary<Vector3Int, SpriteRenderer> crackVisuals = new Dictionary<Vector3Int, SpriteRenderer>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private Vector3Int activeBreakCell;
    private bool hasActiveBreakCell;

    // Place state
    private Vector3Int placeCell;
    private Vector3Int lastPlaceCell;

    private void Awake(){
        mainCamera = Camera.main;

        if (breakOverlay != null) breakOverlay.gameObject.SetActive(false);

        // Listen to weapon system attack events to trigger block interactions
        if (weaponSystem != null) weaponSystem.onAttack += OnWeaponAttack;
    }
    private void OnDestroy(){
        if (weaponSystem != null) weaponSystem.onAttack -= OnWeaponAttack;
    }

    private void OnWeaponAttack(object sender, WeaponSystem.AttackEventArgs e) {
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

        Vector3Int currentCell = groundTilemap.WorldToCell(fistPos);
        TileBase tile = groundTilemap.GetTile(currentCell);
        if (tile == null){
            StopBreak();
            return;
        }

        SetActiveBreakCell(currentCell);

        CrackState state = GetCrackState(currentCell);
        float breakTime = GetBreakTime(tile);
        state.isBeingBroken = true;
        state.healTimer = 0f;
        state.progress = Mathf.Clamp01(state.progress + Time.deltaTime / breakTime);
        state.healStage = GetCurrentCrackStage(state.progress);
        crackStates[currentCell] = state;

        UpdateCrackOverlay(currentCell, state.progress);

        if (state.progress >= 1f) BreakBlock(currentCell, tile);
    }

    private void BreakBlock(Vector3Int cellPos, TileBase tile){
        Vector3 worldPos = groundTilemap.GetCellCenterWorld(cellPos);
        groundTilemap.SetTile(cellPos, null);
        Debug.Log($"Broke {tile.name} at {cellPos}");

        Sprite tileSprite = GetTileSprite(tile);
        BlockDropSpawner.Instance?.SpawnDrop(tile.name, tile, tileSprite, 1, worldPos);

        RemoveCrackState(cellPos);
        StopBreak();
    }

    // ── PLACING ───────────────────────────────────────────────────────────────

    private void TryPlaceBlock(){
        if (inventory == null || groundTilemap == null || mainCamera == null) return;

        if (IsPointerOverBlockingUIForPlacement()){
            ResetPlace();
            return;
        }

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

    private void UpdateCrackOverlay(Vector3Int cellPos, float progress){
        if (breakOverlay == null || crackSprites == null || crackSprites.Length == 0) return;

        SpriteRenderer visual = GetOrCreateCrackVisual(cellPos);
        if (visual == null) return;

        visual.gameObject.SetActive(true);
        visual.transform.position = groundTilemap.GetCellCenterWorld(cellPos);

        int stage = Mathf.FloorToInt(progress * crackSprites.Length);
        stage = Mathf.Clamp(stage, 0, crackSprites.Length - 1);
        visual.sprite = crackSprites[stage];

        Color c = visual.color;
        c.a = Mathf.Lerp(0.3f, 1f, progress);
        visual.color = c;
    }

    private void StopBreak(){
        if (!hasActiveBreakCell) return;

        if (crackStates.TryGetValue(activeBreakCell, out CrackState state)){
            state.isBeingBroken = false;
            crackStates[activeBreakCell] = state;
        }

        hasActiveBreakCell = false;
    }

    private CrackState GetCrackState(Vector3Int cellPos){
        if (!crackStates.TryGetValue(cellPos, out CrackState state)){
            state = new CrackState();
        }

        return state;
    }

    private SpriteRenderer GetOrCreateCrackVisual(Vector3Int cellPos){
        if (crackVisuals.TryGetValue(cellPos, out SpriteRenderer visual) && visual != null){
            return visual;
        }

        if (breakOverlay == null) return null;

        SpriteRenderer newVisual = Instantiate(breakOverlay, breakOverlay.transform.parent);
        newVisual.gameObject.name = $"CrackOverlay_{cellPos.x}_{cellPos.y}_{cellPos.z}";
        newVisual.gameObject.SetActive(false);
        crackVisuals[cellPos] = newVisual;
        return newVisual;
    }

    private void SetActiveBreakCell(Vector3Int cellPos){
        List<Vector3Int> keys = new List<Vector3Int>(crackStates.Keys);
        foreach (Vector3Int key in keys){
            CrackState state = crackStates[key];
            state.isBeingBroken = false;
            crackStates[key] = state;
        }

        CrackState activeState = GetCrackState(cellPos);
        activeState.isBeingBroken = true;
        crackStates[cellPos] = activeState;

        activeBreakCell = cellPos;
        hasActiveBreakCell = true;
    }

    private void RemoveCrackState(Vector3Int cellPos){
        crackStates.Remove(cellPos);

        if (crackVisuals.TryGetValue(cellPos, out SpriteRenderer visual) && visual != null){
            Destroy(visual.gameObject);
        }

        crackVisuals.Remove(cellPos);

        if (hasActiveBreakCell && activeBreakCell == cellPos){
            hasActiveBreakCell = false;
        }
    }

    private void UpdateBreakHealing(){
        if (crackStates.Count == 0) return;

        List<Vector3Int> cellsToRemove = null;
        List<Vector3Int> cells = new List<Vector3Int>(crackStates.Keys);

        foreach (Vector3Int cellPos in cells){
            CrackState state = crackStates[cellPos];

            if (state.isBeingBroken || state.progress <= 0f) continue;

            int stage = GetCurrentCrackStage(state.progress);
            if (stage < 0) continue;

            if (stage != state.healStage){
                state.healStage = stage;
                state.healTimer = 0f;
            }

            float healTime = GetHealTimeForCrackStage(stage);
            if (healTime <= 0f) continue;

            state.healTimer += Time.deltaTime;
            if (state.healTimer < healTime){
                crackStates[cellPos] = state;
                continue;
            }

            state.healTimer = 0f;

            int nextStage = stage - 1;
            if (nextStage >= 0){
                state.progress = GetProgressForCrackStage(nextStage);
                state.healStage = nextStage;
                crackStates[cellPos] = state;
                UpdateCrackOverlay(cellPos, state.progress);
                continue;
            }

            if (cellsToRemove == null) cellsToRemove = new List<Vector3Int>();
            cellsToRemove.Add(cellPos);
        }

        if (cellsToRemove == null) return;

        foreach (Vector3Int cellPos in cellsToRemove){
            RemoveCrackState(cellPos);
        }
    }

    private int GetCurrentCrackStage(float progress){
        int stageCount = GetCrackStageCount();
        if (stageCount <= 0) return -1;

        int stage = Mathf.FloorToInt(progress * stageCount);
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

    private bool IsPointerOverBlockingUIForPlacement(){
        if (EventSystem.current == null) return false;

        Vector2 screenPos = GetPointerScreenPosition();

        if (IsPointerInsideInventoryRegions(screenPos)) return true;

        PointerEventData eventData = new PointerEventData(EventSystem.current){ position = screenPos };
        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, uiRaycastResults);

        foreach (RaycastResult result in uiRaycastResults){
            GameObject hitObject = result.gameObject;
            if (hitObject == null) continue;

            if (hitObject.GetComponentInParent<Selectable>() != null) return true;
            if (hitObject.GetComponentInParent<InventoryDragHandle>() != null) return true;
        }

        return false;
    }

    private bool IsPointerInsideInventoryRegions(Vector2 screenPos){
        if (inventory == null) return false;

        // Block placement over inventory layout regions even if decorative graphics don't raycast.
        if (IsPointerInsideRect(inventory.hotbarParent, screenPos)) return true;
        if (IsPointerInsideRect(inventory.mainGridParent, screenPos)) return true;

        if (inventory.hotbarParent != null && IsPointerInsideRect(inventory.hotbarParent.parent, screenPos)) return true;
        if (inventory.mainGridParent != null && IsPointerInsideRect(inventory.mainGridParent.parent, screenPos)) return true;

        return false;
    }

    private Vector2 GetPointerScreenPosition(){
        if (Input.touchCount > 0) return Input.GetTouch(0).position;
        return Input.mousePosition;
    }

    private bool IsPointerInsideRect(Transform target, Vector2 screenPos){
        if (target == null) return false;

        RectTransform rect = target as RectTransform;
        if (rect == null) return false;

        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, null);
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