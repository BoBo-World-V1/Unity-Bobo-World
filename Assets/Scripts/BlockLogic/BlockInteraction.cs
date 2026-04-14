using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class BlockInteraction : MonoBehaviour
{
    private const float HealSecondsPerCrackStage = 5f;
    private const float MinimumBreakTime = 0.05f;
    private const float ControllerTriggerPressThreshold = 0.5f;

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

    [Header("Controller")]
    [Range(1000f, 20000f)] public float controllerCursorMoveSpeed = 12000f;
    [Range(0.01f, 0.5f)] public float controllerAimDeadZone = 0.08f;
    public Color controllerPointerColor = new(1f, 1f, 1f, 0.9f);
    public Color controllerTargetColor = new(1f, 0.88f, 0.28f, 0.28f);
    public Color controllerBlockedTargetColor = new(1f, 0.35f, 0.35f, 0.28f);

    [Header("Break Progress Visual")]
    public SpriteRenderer breakOverlay;
    public Sprite[] crackSprites;

    private readonly Dictionary<Vector3Int, CrackState> crackStates = new();
    private readonly Dictionary<Vector3Int, SpriteRenderer> crackVisuals = new();
    private readonly List<RaycastResult> uiRaycastResults = new();
    private readonly List<Vector3Int> crackCellBuffer = new();
    private readonly List<Vector3Int> removalBuffer = new();
    private readonly List<Collider2D> playerColliders = new();

    private static Sprite controllerTargetSprite;
    private static BlockInteraction activeInstance;

    private Camera mainCamera;
    private PlayerInput playerInput;
    private InputAction lookAction;
    private Vector3Int activeBreakCell;
    private Vector2 controllerPointerScreenPosition;
    private Vector2 previousHardwarePointerScreenPosition;
    private Image controllerPointerImage;
    private RectTransform controllerPointerRect;
    private SpriteRenderer controllerTargetRenderer;
    private bool hasActiveBreakCell;
    private bool hasControllerPointerPosition;
    private bool useControllerPointer;
    private bool wasControllerBreakHeld;
    private bool wasControllerPrimaryHeld;

    private void Awake()
    {
        activeInstance = this;
        mainCamera = Camera.main;
        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null){
            lookAction = playerInput.actions["Look"];
        }

        CachePlayerColliders();
        EnsureControllerPointerOverlay();
        EnsureControllerTargetRenderer();
        previousHardwarePointerScreenPosition = ReadHardwarePointerScreenPosition();

        if (breakOverlay != null){
            breakOverlay.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        UpdateControllerCursor();
        UpdateControllerPointerVisual();
        UpdateControllerTargetVisual();
        HandlePrimaryInteraction();
        UpdateBreakHealing();
    }

    private void HandlePrimaryInteraction()
    {
        bool controllerPrimaryHeld = IsControllerPrimaryHeld();
        bool mousePrimaryHeld = Input.GetMouseButton(0) || controllerPrimaryHeld;
        bool mousePrimaryReleased = Input.GetMouseButtonUp(0) || (wasControllerPrimaryHeld && !controllerPrimaryHeld);
        bool controllerBreakHeld = IsControllerBreakHeld();
        bool controllerBreakReleased = wasControllerBreakHeld && !controllerBreakHeld;
        bool controllerPlacePressed = IsControllerPlacePressed();

        bool wantsBreak = mousePrimaryHeld || controllerBreakHeld;
        bool releasedBreak = mousePrimaryReleased || controllerBreakReleased;

        if (!wantsBreak && !controllerPlacePressed){
            if (releasedBreak){
                StopBreak();
            }

            wasControllerBreakHeld = controllerBreakHeld;
            wasControllerPrimaryHeld = controllerPrimaryHeld;
            return;
        }

        if (IsInteractionBlockedByUI(mousePrimaryHeld, mousePrimaryReleased)){
            StopBreak();
            wasControllerBreakHeld = controllerBreakHeld;
            wasControllerPrimaryHeld = controllerPrimaryHeld;
            return;
        }

        if (inventory == null || inventory.CanUseSelectedItemForBreaking){
            if (wantsBreak){
                TryBreakBlock(true);
                wasControllerBreakHeld = controllerBreakHeld;
                wasControllerPrimaryHeld = controllerPrimaryHeld;
                return;
            }

            StopBreak();
            wasControllerBreakHeld = controllerBreakHeld;
            wasControllerPrimaryHeld = controllerPrimaryHeld;
            return;
        }

        if (inventory.IsSelectedPlaceableBlock){
            StopBreak();
            if (mousePrimaryHeld || controllerPlacePressed){
                TryPlaceBlock(true);
            }

            wasControllerBreakHeld = controllerBreakHeld;
            wasControllerPrimaryHeld = controllerPrimaryHeld;
            return;
        }

        StopBreak();
        wasControllerBreakHeld = controllerBreakHeld;
        wasControllerPrimaryHeld = controllerPrimaryHeld;
    }

    private void TryBreakBlock(bool usePointerTarget)
    {
        if (groundTilemap == null || fistTransform == null){
            return;
        }

        if (!TryGetInteractionCell(usePointerTarget, out Vector3Int currentCell, out Vector3 interactionWorld)){
            StopBreak();
            return;
        }

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
        UpdateWeaponTarget(interactionWorld);

        if (state.progress >= 1f){
            BreakBlock(currentCell, tile);
        }
    }

    private void BreakBlock(Vector3Int cellPos, TileBase tile)
    {
        Vector3 worldPos = groundTilemap.GetCellCenterWorld(cellPos);
        Sprite tileSprite = GetTileSprite(tile);

        groundTilemap.SetTile(cellPos, null);
        GameAudio.PlayBlockBreak();
        Debug.Log($"Broke {tile.name} at {cellPos}");

        BlockDropSpawner.Instance?.SpawnDrop(tile.name, tile, tileSprite, 1, worldPos);

        RemoveCrackState(cellPos);
        StopBreak();
    }

    private void TryPlaceBlock(bool usePointerTarget)
    {
        if (inventory == null || groundTilemap == null || mainCamera == null){
            return;
        }

        InventorySlot selected = inventory.GetSelectedSlot();
        if (selected == null || selected.IsEmpty || selected.PlacementTile == null){
            return;
        }

        if (!TryGetInteractionCell(usePointerTarget, out Vector3Int currentCell, out _)){
            return;
        }

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
        GameAudio.PlayBlockPlace();
        Debug.Log($"Placed {blockName} at {cellPos}");
    }

    private bool IsWithinReach(Vector3 worldPosition)
    {
        return Vector2.Distance(transform.position, worldPosition) <= reachDistance;
    }

    private bool TryGetInteractionCell(bool usePointerTarget, out Vector3Int cell, out Vector3 interactionWorld)
    {
        if (groundTilemap == null){
            cell = default;
            interactionWorld = default;
            return false;
        }

        if (!usePointerTarget && TryGetControllerTargetCell(out cell, out interactionWorld)){
            return true;
        }

        interactionWorld = GetPointerWorldPosition();
        if (!IsWithinReach(interactionWorld)){
            cell = default;
            return false;
        }

        cell = groundTilemap.WorldToCell(interactionWorld);
        return true;
    }

    private Vector3 GetPointerWorldPosition()
    {
        Vector3 pointerWorld = mainCamera.ScreenToWorldPoint(GetPointerScreenPosition());
        pointerWorld.z = 0f;
        return pointerWorld;
    }

    private bool TryGetControllerTargetCell(out Vector3Int cell, out Vector3 interactionWorld)
    {
        if (!HasControllerStyleInput() || groundTilemap == null){
            cell = default;
            interactionWorld = default;
            return false;
        }

        if (!hasControllerPointerPosition){
            cell = default;
            interactionWorld = default;
            return false;
        }

        interactionWorld = GetPointerWorldPosition();
        if (!IsWithinReach(interactionWorld)){
            cell = default;
            return false;
        }

        cell = groundTilemap.WorldToCell(interactionWorld);
        return true;
    }

    private void UpdateControllerCursor()
    {
        Vector2 hardwarePointer = ReadHardwarePointerScreenPosition();
        bool hardwarePointerMoved = (hardwarePointer - previousHardwarePointerScreenPosition).sqrMagnitude > 1f;

        if (!HasControllerStyleInput()){
            useControllerPointer = false;
            previousHardwarePointerScreenPosition = hardwarePointer;
            return;
        }

        EnsureControllerPointerInitialized();
        Vector2 rawAim = ReadControllerAimInput();
        bool isUsingControllerAim = rawAim.sqrMagnitude >= controllerAimDeadZone * controllerAimDeadZone;

        if (isUsingControllerAim){
            useControllerPointer = true;
            float screenScale = Mathf.Max(1f, Screen.height / 1080f);
            Vector2 cursorVelocity = rawAim.normalized * (controllerCursorMoveSpeed * screenScale);
            controllerPointerScreenPosition += cursorVelocity * Time.deltaTime;
        }
        else if (hardwarePointerMoved){
            useControllerPointer = false;
            controllerPointerScreenPosition = hardwarePointer;
        }

        controllerPointerScreenPosition.x = Mathf.Clamp(controllerPointerScreenPosition.x, 0f, Screen.width);
        controllerPointerScreenPosition.y = Mathf.Clamp(controllerPointerScreenPosition.y, 0f, Screen.height);
        previousHardwarePointerScreenPosition = hardwarePointer;
        hasControllerPointerPosition = true;
    }

    private bool IsControllerBreakHeld()
    {
        Gamepad gamepad = ReadActiveGamepad();
        return gamepad != null && gamepad.rightTrigger.ReadValue() >= ControllerTriggerPressThreshold;
    }

    private bool IsControllerPrimaryHeld()
    {
        Gamepad gamepad = ReadActiveGamepad();
        return gamepad != null && gamepad.leftTrigger.ReadValue() >= ControllerTriggerPressThreshold;
    }

    private bool IsControllerPlacePressed()
    {
        Gamepad gamepad = ReadActiveGamepad();
        return gamepad != null && gamepad.rightShoulder.wasPressedThisFrame;
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

    private bool IsInteractionBlockedByUI(bool usePointerHeld, bool usePointerReleased)
    {
        if (IsControllerUIBlockingInteraction()){
            return true;
        }

        if (!usePointerHeld && !usePointerReleased){
            return false;
        }

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

    private bool IsControllerUIBlockingInteraction()
    {
        if (EventSystem.current == null){
            return false;
        }

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        if (selectedObject == null){
            return false;
        }

        return selectedObject.GetComponentInParent<Selectable>() != null
            || selectedObject.GetComponentInParent<InventoryDragHandle>() != null;
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
        if (HasControllerPointerControl()){
            return controllerPointerScreenPosition;
        }

        if (Input.touchCount > 0){
            return Input.GetTouch(0).position;
        }

        if (Mouse.current != null){
            return Mouse.current.position.ReadValue();
        }

        return Input.mousePosition;
    }

    private Vector2 ReadHardwarePointerScreenPosition()
    {
        if (Mouse.current != null){
            return Mouse.current.position.ReadValue();
        }

        return Input.mousePosition;
    }

    public static bool TryGetActiveControllerPointerScreenPosition(out Vector2 screenPosition)
    {
        if (activeInstance != null && activeInstance.HasControllerPointerControl()){
            screenPosition = activeInstance.controllerPointerScreenPosition;
            return true;
        }

        screenPosition = default;
        return false;
    }

    private bool HasControllerPointerControl()
    {
        return HasControllerStyleInput() && hasControllerPointerPosition && useControllerPointer;
    }

    private bool HasControllerStyleInput()
    {
        if (playerInput != null && !string.IsNullOrEmpty(playerInput.currentControlScheme)){
            return playerInput.currentControlScheme != "Keyboard&Mouse"
                && playerInput.currentControlScheme != "Touch";
        }

        return Gamepad.current != null || Joystick.current != null;
    }

    private Gamepad ReadActiveGamepad()
    {
        return Gamepad.current;
    }

    private Vector2 ReadControllerAimInput()
    {
        if (lookAction != null && HasControllerStyleInput()){
            Vector2 actionValue = lookAction.ReadValue<Vector2>();
            if (actionValue.sqrMagnitude > 0f){
                return actionValue;
            }
        }

        if (Gamepad.current != null){
            return Gamepad.current.rightStick.ReadValue();
        }

        if (Joystick.current != null){
            return Joystick.current.stick.ReadValue();
        }

        return Vector2.zero;
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
            if (!RuntimeItemCatalog.TryGetBlock(tile, out BlockItemDefinition blockDefinition)){
                blockDefinition = RuntimeItemCatalog.GetOrCreateBlock(
                    tile.name,
                    tile,
                    GetTileSprite(tile),
                    defaultBreakTime);
            }

            if (blockDefinition != null){
                float breakSpeedMultiplierFromInventory = inventory != null ? inventory.SelectedBreakSpeedMultiplier : 1f;
                return Mathf.Max(MinimumBreakTime, blockDefinition.BreakTime / Mathf.Max(1f, breakSpeedMultiplierFromInventory));
            }
        }

        float breakSpeedMultiplier = inventory != null ? inventory.SelectedBreakSpeedMultiplier : 1f;
        return Mathf.Max(MinimumBreakTime, defaultBreakTime / Mathf.Max(1f, breakSpeedMultiplier));
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

    private void EnsureControllerTargetRenderer()
    {
        if (controllerTargetRenderer != null){
            return;
        }

        GameObject targetObject = new("ControllerTarget");
        targetObject.transform.SetParent(groundTilemap != null ? groundTilemap.transform : null, false);

        controllerTargetRenderer = targetObject.AddComponent<SpriteRenderer>();
        controllerTargetRenderer.sprite = GetControllerTargetSprite();
        controllerTargetRenderer.color = controllerTargetColor;
        controllerTargetRenderer.sortingOrder = breakOverlay != null ? breakOverlay.sortingOrder - 1 : 50;
        controllerTargetRenderer.enabled = false;
    }

    private void EnsureControllerPointerOverlay()
    {
        if (controllerPointerImage != null && controllerPointerRect != null){
            return;
        }

        GameObject canvasObject = new("ControllerPointerCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject pointerObject = new("ControllerPointer");
        pointerObject.transform.SetParent(canvasObject.transform, false);

        controllerPointerRect = pointerObject.AddComponent<RectTransform>();
        controllerPointerRect.anchorMin = Vector2.zero;
        controllerPointerRect.anchorMax = Vector2.zero;
        controllerPointerRect.pivot = new Vector2(0.5f, 0.5f);
        controllerPointerRect.sizeDelta = new Vector2(18f, 18f);

        controllerPointerImage = pointerObject.AddComponent<Image>();
        controllerPointerImage.sprite = GetControllerTargetSprite();
        controllerPointerImage.color = controllerPointerColor;
        controllerPointerImage.raycastTarget = false;
        controllerPointerImage.enabled = false;
    }

    private void UpdateControllerPointerVisual()
    {
        if (controllerPointerImage == null || controllerPointerRect == null){
            return;
        }

        bool shouldShow = HasControllerPointerControl();
        if (!shouldShow){
            controllerPointerImage.enabled = false;
            return;
        }

        controllerPointerImage.enabled = true;
        controllerPointerRect.anchoredPosition = controllerPointerScreenPosition;
        controllerPointerImage.color = controllerPointerColor;
    }

    private void UpdateControllerTargetVisual()
    {
        if (controllerTargetRenderer == null || groundTilemap == null){
            return;
        }

        bool hasTargetCell = TryGetControllerTargetCell(out Vector3Int cell, out _);
        bool shouldShow = HasControllerStyleInput()
            && inventory != null
            && (inventory.CanUseSelectedItemForBreaking || inventory.IsSelectedPlaceableBlock)
            && hasTargetCell;

        if (!shouldShow){
            controllerTargetRenderer.enabled = false;
            return;
        }

        controllerTargetRenderer.enabled = true;
        controllerTargetRenderer.transform.position = groundTilemap.GetCellCenterWorld(cell);
        Vector3 cellSize = groundTilemap.layoutGrid.cellSize;
        controllerTargetRenderer.transform.localScale = new Vector3(cellSize.x * 0.92f, cellSize.y * 0.92f, 1f);

        bool blocked = inventory.IsSelectedPlaceableBlock
            && (groundTilemap.GetTile(cell) != null || WouldPlaceInsidePlayer(cell));
        controllerTargetRenderer.color = blocked ? controllerBlockedTargetColor : controllerTargetColor;
        UpdateWeaponTarget(groundTilemap.GetCellCenterWorld(cell));
    }

    private void UpdateWeaponTarget(Vector3 targetWorld)
    {
        if (weaponSystem == null){
            return;
        }

        weaponSystem.SetExternalTarget(targetWorld);
    }

    private static Sprite GetControllerTargetSprite()
    {
        if (controllerTargetSprite != null){
            return controllerTargetSprite;
        }

        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        controllerTargetSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return controllerTargetSprite;
    }

    private void EnsureControllerPointerInitialized()
    {
        if (hasControllerPointerPosition){
            return;
        }

        float facingDirection = transform.localScale.x < 0f ? 1f : -1f;
        Vector3 worldStart = transform.position + new Vector3(facingDirection * Mathf.Min(reachDistance, 1f), 0f, 0f);
        worldStart.z = 0f;
        if (mainCamera != null){
            controllerPointerScreenPosition = mainCamera.WorldToScreenPoint(worldStart);
        }
        else{
            controllerPointerScreenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        controllerPointerScreenPosition.x = Mathf.Clamp(controllerPointerScreenPosition.x, 0f, Screen.width);
        controllerPointerScreenPosition.y = Mathf.Clamp(controllerPointerScreenPosition.y, 0f, Screen.height);
        hasControllerPointerPosition = true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, reachDistance);
    }

    private void OnDestroy()
    {
        if (activeInstance == this){
            activeInstance = null;
        }
    }
}
