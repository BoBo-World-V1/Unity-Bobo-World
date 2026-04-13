using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class BlockInteraction : MonoBehaviour
{
    private const float HealSecondsPerCrackStage = 5f;
    private const float MinimumBreakTime = 0.05f;

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
    public Inventory inventory;
    public WeaponSystem weaponSystem;

    [Header("Settings")]
    public float reachDistance = 3f;
    public float defaultBreakTime = 0.5f;
    [Header("Break Progress Visual")]
    public SpriteRenderer breakOverlay;
    public Sprite[] crackSprites;

    private readonly Dictionary<string, float> hardnessTable = new()
    {
        { "DirtTile", 0.5f },
    };

    private readonly Dictionary<Vector3Int, CrackState> crackStates = new();
    private readonly Dictionary<Vector3Int, SpriteRenderer> crackVisuals = new();
    private readonly List<RaycastResult> uiRaycastResults = new();
    private readonly List<Vector3Int> crackCellBuffer = new();
    private readonly List<Vector3Int> removalBuffer = new();
    private readonly List<Collider2D> playerColliders = new();

    private Camera mainCamera;
    private Vector3Int activeBreakCell;
    private bool hasActiveBreakCell;
    private void Awake()
    {
        mainCamera = Camera.main;
        CachePlayerColliders();

        if (breakOverlay != null){
            breakOverlay.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        HandlePrimaryInteraction();
        UpdateBreakHealing();
    }

    private void HandlePrimaryInteraction()
    {
        if (!Input.GetMouseButton(0)){
            if (Input.GetMouseButtonUp(0)){
                StopBreak();
            }
            return;
        }

        if (IsPointerOverBlockingUI()){
            StopBreak();
            return;
        }

        if (inventory == null || inventory.CanUseSelectedItemForBreaking){
            TryBreakBlock();
            return;
        }

        if (inventory.IsSelectedPlaceableBlock){
            StopBreak();
            TryPlaceBlock();
            return;
        }

        StopBreak();
    }

    private void TryBreakBlock()
    {
        if (groundTilemap == null || fistTransform == null){
            return;
        }

        Vector3 fistPosition = fistTransform.position;
        if (!IsWithinReach(fistPosition)){
            StopBreak();
            return;
        }

        Vector3Int currentCell = groundTilemap.WorldToCell(fistPosition);
        TileBase tile = groundTilemap.GetTile(currentCell);
        if (tile == null){
            StopBreak();
            return;
        }

        SetActiveBreakCell(currentCell);

        CrackState state = GetCrackState(currentCell);
        state.isBeingBroken = true;
        state.healTimer = 0f;
        state.progress = Mathf.Clamp01(state.progress + Time.deltaTime / GetBreakTime(tile));
        state.healStage = GetCurrentCrackStage(state.progress);
        crackStates[currentCell] = state;

        UpdateCrackOverlay(currentCell, state.progress);

        if (state.progress >= 1f){
            BreakBlock(currentCell, tile);
        }
    }

    private void BreakBlock(Vector3Int cellPos, TileBase tile)
    {
        Vector3 worldPos = groundTilemap.GetCellCenterWorld(cellPos);
        Sprite tileSprite = GetTileSprite(tile);

        groundTilemap.SetTile(cellPos, null);
        Debug.Log($"Broke {tile.name} at {cellPos}");

        BlockDropSpawner.Instance?.SpawnDrop(tile.name, tile, tileSprite, 1, worldPos);

        RemoveCrackState(cellPos);
        StopBreak();
    }

    private void TryPlaceBlock()
    {
        if (inventory == null || groundTilemap == null || mainCamera == null){
            return;
        }

        InventorySlot selected = inventory.GetSelectedSlot();
        if (selected == null || selected.IsEmpty || selected.PlacementTile == null){
            return;
        }

        Vector3 pointerWorld = GetPointerWorldPosition();
        if (!IsWithinReach(pointerWorld)){
            return;
        }

        Vector3Int currentCell = groundTilemap.WorldToCell(pointerWorld);
        if (groundTilemap.GetTile(currentCell) != null){
            return;
        }

        if (WouldPlaceInsidePlayer(currentCell)){
            return;
        }

        PlaceBlock(currentCell, selected);
    }

    private void PlaceBlock(Vector3Int cellPos, InventorySlot slot)
    {
        TileBase blockTile = slot.PlacementTile;
        string blockName = slot.DisplayName;
        int slotIndex = inventory.selectedSlot;
        if (!inventory.RemoveBlock(slotIndex, 1)){
            return;
        }

        groundTilemap.SetTile(cellPos, blockTile);
        Debug.Log($"Placed {blockName} at {cellPos}");
    }

    private bool IsWithinReach(Vector3 worldPosition)
    {
        return Vector2.Distance(transform.position, worldPosition) <= reachDistance;
    }

    private Vector3 GetPointerWorldPosition()
    {
        Vector3 pointerWorld = mainCamera.ScreenToWorldPoint(GetPointerScreenPosition());
        pointerWorld.z = 0f;
        return pointerWorld;
    }

    private Sprite GetTileSprite(TileBase tile)
    {
        return tile is Tile concreteTile ? concreteTile.sprite : null;
    }

    private void UpdateCrackOverlay(Vector3Int cellPos, float progress)
    {
        if (breakOverlay == null || crackSprites == null || crackSprites.Length == 0){
            return;
        }

        SpriteRenderer visual = GetOrCreateCrackVisual(cellPos);
        if (visual == null){
            return;
        }

        visual.gameObject.SetActive(true);
        visual.transform.position = groundTilemap.GetCellCenterWorld(cellPos);

        int stage = Mathf.Clamp(Mathf.FloorToInt(progress * crackSprites.Length), 0, crackSprites.Length - 1);
        visual.sprite = crackSprites[stage];

        Color color = visual.color;
        color.a = Mathf.Lerp(0.3f, 1f, progress);
        visual.color = color;
    }

    private void StopBreak()
    {
        if (!hasActiveBreakCell){
            return;
        }

        if (crackStates.TryGetValue(activeBreakCell, out CrackState state)){
            state.isBeingBroken = false;
            crackStates[activeBreakCell] = state;
        }

        hasActiveBreakCell = false;
    }

    private CrackState GetCrackState(Vector3Int cellPos)
    {
        return crackStates.TryGetValue(cellPos, out CrackState state) ? state : new CrackState();
    }

    private SpriteRenderer GetOrCreateCrackVisual(Vector3Int cellPos)
    {
        if (crackVisuals.TryGetValue(cellPos, out SpriteRenderer existingVisual) && existingVisual != null){
            return existingVisual;
        }

        if (breakOverlay == null){
            return null;
        }

        SpriteRenderer newVisual = Instantiate(breakOverlay, breakOverlay.transform.parent);
        newVisual.gameObject.name = $"CrackOverlay_{cellPos.x}_{cellPos.y}_{cellPos.z}";
        newVisual.gameObject.SetActive(false);
        crackVisuals[cellPos] = newVisual;
        return newVisual;
    }

    private void SetActiveBreakCell(Vector3Int cellPos)
    {
        crackCellBuffer.Clear();
        foreach (Vector3Int key in crackStates.Keys){
            crackCellBuffer.Add(key);
        }

        foreach (Vector3Int key in crackCellBuffer){
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

    private void RemoveCrackState(Vector3Int cellPos)
    {
        crackStates.Remove(cellPos);

        if (crackVisuals.TryGetValue(cellPos, out SpriteRenderer visual) && visual != null){
            Destroy(visual.gameObject);
        }

        crackVisuals.Remove(cellPos);

        if (hasActiveBreakCell && activeBreakCell == cellPos){
            hasActiveBreakCell = false;
        }
    }

    private void UpdateBreakHealing()
    {
        if (crackStates.Count == 0){
            return;
        }

        crackCellBuffer.Clear();
        foreach (Vector3Int cellPos in crackStates.Keys){
            crackCellBuffer.Add(cellPos);
        }

        removalBuffer.Clear();

        foreach (Vector3Int cellPos in crackCellBuffer){
            CrackState state = crackStates[cellPos];
            if (state.isBeingBroken || state.progress <= 0f){
                continue;
            }

            int stage = GetCurrentCrackStage(state.progress);
            if (stage < 0){
                continue;
            }

            if (stage != state.healStage){
                state.healStage = stage;
                state.healTimer = 0f;
            }

            float healTime = GetHealTimeForCrackStage(stage);
            if (healTime <= 0f){
                continue;
            }

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

            removalBuffer.Add(cellPos);
        }

        foreach (Vector3Int cellPos in removalBuffer){
            RemoveCrackState(cellPos);
        }
    }

    private int GetCurrentCrackStage(float progress)
    {
        int stageCount = GetCrackStageCount();
        if (stageCount <= 0){
            return -1;
        }

        int stage = Mathf.FloorToInt(progress * stageCount);
        return Mathf.Clamp(stage, 0, stageCount - 1);
    }

    private float GetHealTimeForCrackStage(int stage)
    {
        return (stage + 1) * HealSecondsPerCrackStage;
    }

    private float GetProgressForCrackStage(int stage)
    {
        int stageCount = GetCrackStageCount();
        if (stageCount <= 0){
            return 0f;
        }

        float step = 1f / stageCount;
        return Mathf.Clamp((stage + 0.5f) * step, 0.001f, 0.999f);
    }

    private int GetCrackStageCount()
    {
        return crackSprites == null ? 0 : crackSprites.Length;
    }

    private bool IsPointerOverBlockingUI()
    {
        if (EventSystem.current == null){
            return false;
        }

        Vector2 screenPos = GetPointerScreenPosition();
        if (IsPointerInsideInventoryRegions(screenPos)){
            return true;
        }

        PointerEventData eventData = new(EventSystem.current) { position = screenPos };
        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, uiRaycastResults);

        foreach (RaycastResult result in uiRaycastResults){
            GameObject hitObject = result.gameObject;
            if (hitObject == null){
                continue;
            }

            if (hitObject.GetComponentInParent<Selectable>() != null){
                return true;
            }

            if (hitObject.GetComponentInParent<InventoryDragHandle>() != null){
                return true;
            }
        }

        return false;
    }

    private bool IsPointerInsideInventoryRegions(Vector2 screenPos)
    {
        if (inventory == null){
            return false;
        }

        if (IsPointerInsideRect(inventory.hotbarParent, screenPos)) return true;
        if (IsPointerInsideRect(inventory.mainGridParent, screenPos)) return true;
        if (inventory.hotbarParent != null && IsPointerInsideRect(inventory.hotbarParent.parent, screenPos)) return true;
        if (inventory.mainGridParent != null && IsPointerInsideRect(inventory.mainGridParent.parent, screenPos)) return true;

        return false;
    }

    private Vector2 GetPointerScreenPosition()
    {
        if (Input.touchCount > 0){
            return Input.GetTouch(0).position;
        }

        return Input.mousePosition;
    }

    private bool IsPointerInsideRect(Transform target, Vector2 screenPos)
    {
        if (target is not RectTransform rect){
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, null);
    }

    private float GetBreakTime(TileBase tile)
    {
        if (tile != null){
            BlockItemDefinition blockDefinition = RuntimeItemCatalog.GetOrCreateBlock(
                tile.name,
                tile,
                GetTileSprite(tile),
                ResolveBaseBreakTime(tile.name));
            if (blockDefinition != null){
                float breakSpeedMultiplierFromInventory = inventory != null ? inventory.SelectedBreakSpeedMultiplier : 1f;
                return Mathf.Max(MinimumBreakTime, blockDefinition.BreakTime / Mathf.Max(1f, breakSpeedMultiplierFromInventory));
            }
        }

        float baseBreakTime = defaultBreakTime;
        if (tile != null && hardnessTable.TryGetValue(tile.name, out float breakTime)){
            baseBreakTime = breakTime;
        }

        float breakSpeedMultiplier = inventory != null ? inventory.SelectedBreakSpeedMultiplier : 1f;
        return Mathf.Max(MinimumBreakTime, baseBreakTime / Mathf.Max(1f, breakSpeedMultiplier));
    }

    private float ResolveBaseBreakTime(string blockName)
    {
        if (!string.IsNullOrWhiteSpace(blockName) && hardnessTable.TryGetValue(blockName, out float breakTime)){
            return breakTime;
        }

        return defaultBreakTime;
    }

    private void CachePlayerColliders()
    {
        playerColliders.Clear();

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        foreach (Collider2D collider in colliders){
            if (collider == null || !collider.enabled || collider.isTrigger){
                continue;
            }

            playerColliders.Add(collider);
        }
    }

    private bool WouldPlaceInsidePlayer(Vector3Int cellPos)
    {
        if (playerColliders.Count == 0 || groundTilemap == null){
            return false;
        }

        Vector3 center = groundTilemap.GetCellCenterWorld(cellPos);
        Vector3 size3D = Vector3.Scale(groundTilemap.layoutGrid.cellSize, groundTilemap.layoutGrid.transform.lossyScale);
        Bounds cellBounds = new(center, new Vector3(Mathf.Abs(size3D.x), Mathf.Abs(size3D.y), 1f));

        foreach (Collider2D collider in playerColliders){
            if (collider == null || !collider.enabled || collider.isTrigger){
                continue;
            }

            if (collider.bounds.Intersects(cellBounds)){
                return true;
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, reachDistance);
    }
}
