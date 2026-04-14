using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class WeaponSystem : MonoBehaviour
{
    private const float HoverWobbleDegrees = 15f;
    private const float HoverScaleAmplitude = 0.1f;
    private const float ControllerTriggerPressThreshold = 0.5f;

    [SerializeField] private Transform Pivot;
    [SerializeField] private Transform FistSprite;

    [Header("Range")]
    public float maxRange = 3f;

    [Header("Pop Animation")]
    public float popDuration = 0.1f;
    public float maxScale = 2f;
    public float shrinkDuration = 0.15f;

    [Header("Trail")]
    public float trailTime = 0.1f;
    public float trailStartWidth = 0.15f;
    public float trailEndWidth = 0f;
    public Color trailStartColor = new(1f, 1f, 1f, 0.8f);
    public Color trailEndColor = new(1f, 1f, 1f, 0f);

    [Header("Idle Hover")]
    public float hoverAmplitude = 0.1f;
    public float hoverSpeed = 2f;
    public float returnSpeed = 8f;

    [Header("UI Blockers")]
    public RectTransform inventoryPanel;
    public RectTransform joystickZone;
    public RectTransform HotBarRow;

    [Header("References")]
    public Inventory inventory;

    public event EventHandler<AttackEventArgs> onAttack;

    public class AttackEventArgs : EventArgs
    {
        public Vector3 Origin { get; set; }
        public Vector3 TargetPosition { get; set; }
    }

    private readonly List<RaycastResult> uiRaycastResults = new();

    private Camera mainCamera;
    private SpriteRenderer heldSpriteRenderer;
    private TrailRenderer trail;
    private Coroutine animCoroutine;
    private Coroutine returnCoroutine;
    private Material runtimeTrailMaterial;
    private Vector3 idleLocalPosition;
    private Sprite defaultHeldSprite;
    private Vector3 externalTargetWorld;
    private float maxRangeSqr;
    private float externalTargetTimestamp = -10f;
    private bool isHolding;
    private bool wasControllerAttackHeld;

    private void Awake()
    {
        mainCamera = Camera.main;
        maxRangeSqr = maxRange * maxRange;

        if (Pivot == null || FistSprite == null){
            Debug.LogWarning("WeaponSystem requires both Pivot and FistSprite references.");
            enabled = false;
            return;
        }

        heldSpriteRenderer = FistSprite.GetComponent<SpriteRenderer>();
        CaptureIdleState();
        SetupTrail();
    }

    private void OnDestroy()
    {
        if (runtimeTrailMaterial != null){
            Destroy(runtimeTrailMaterial);
        }
    }

    private void Update()
    {
        UpdateHeldSprite();

        if (!CanProcessWeaponLogic()){
            StopCurrentAttackIfNeeded();
            ApplyHover();
            return;
        }

        bool mouseAttackPressed = Input.GetMouseButtonDown(0);
        bool mouseAttackHeld = Input.GetMouseButton(0);
        bool mouseAttackReleased = Input.GetMouseButtonUp(0);
        bool controllerAttackHeld = IsControllerAttackHeld();
        bool controllerAttackPressed = controllerAttackHeld && !wasControllerAttackHeld;
        bool controllerAttackReleased = wasControllerAttackHeld && !controllerAttackHeld;
        bool usingPointerInput = mouseAttackPressed || mouseAttackHeld || mouseAttackReleased;

        Vector3 targetWorld = ResolveTargetWorld();
        if (IsInteractionBlockedByUI(usingPointerInput, controllerAttackPressed || controllerAttackHeld || controllerAttackReleased)){
            StopCurrentAttackIfNeeded();
            ApplyHover();
            wasControllerAttackHeld = controllerAttackHeld;
            return;
        }

        HandleAttackInput(targetWorld, mouseAttackPressed, mouseAttackHeld, mouseAttackReleased, controllerAttackPressed, controllerAttackHeld, controllerAttackReleased);
        wasControllerAttackHeld = controllerAttackHeld;

        if (!isHolding){
            ApplyHover();
        }
    }

    private bool CanProcessWeaponLogic()
    {
        return inventory == null || inventory.CanUseSelectedItemAsWeapon;
    }

    private void CaptureIdleState()
    {
        idleLocalPosition = FistSprite.localPosition;
        if (heldSpriteRenderer != null){
            defaultHeldSprite = heldSpriteRenderer.sprite;
        }

        FistSprite.localScale = Vector3.one;
        FistSprite.gameObject.SetActive(true);
    }

    private void UpdateHeldSprite()
    {
        if (heldSpriteRenderer == null){
            return;
        }

        ItemDefinition selectedItem = inventory != null ? inventory.SelectedItemDefinition : null;
        bool shouldUseSelectedIcon = selectedItem != null
            && selectedItem.SupportsAttackAnimation
            && selectedItem.Icon != null
            && selectedItem.Category != ItemCategory.Fist;

        heldSpriteRenderer.sprite = shouldUseSelectedIcon ? selectedItem.Icon : defaultHeldSprite;
    }

    private void SetupTrail()
    {
        trail = FistSprite.GetComponent<TrailRenderer>();
        if (trail == null){
            trail = FistSprite.gameObject.AddComponent<TrailRenderer>();
        }

        trail.time = trailTime;
        trail.startWidth = trailStartWidth;
        trail.endWidth = trailEndWidth;
        trail.minVertexDistance = 0.01f;
        trail.autodestruct = false;

        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(trailStartColor, 0f),
                new GradientColorKey(trailEndColor, 1f),
            },
            new[]
            {
                new GradientAlphaKey(trailStartColor.a, 0f),
                new GradientAlphaKey(0f, 1f),
            });
        trail.colorGradient = gradient;

        if (trail.sharedMaterial == null){
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null){
                runtimeTrailMaterial = new Material(shader);
                trail.material = runtimeTrailMaterial;
            }
        }
    }

    private void StopCurrentAttackIfNeeded()
    {
        if (!isHolding){
            return;
        }

        isHolding = false;
        StopAttack();
    }

    public void SetExternalTarget(Vector3 targetWorld)
    {
        externalTargetWorld = targetWorld;
        externalTargetTimestamp = Time.time;
    }

    private void HandleAttackInput(
        Vector3 targetWorld,
        bool mouseAttackPressed,
        bool mouseAttackHeld,
        bool mouseAttackReleased,
        bool controllerAttackPressed,
        bool controllerAttackHeld,
        bool controllerAttackReleased)
    {
        if (mouseAttackPressed || controllerAttackPressed){
            isHolding = true;
            StopReturnCoroutine();
            StartAttack(targetWorld);
        }
        else if ((mouseAttackHeld || controllerAttackHeld) && isHolding){
            MoveToTarget(targetWorld);
            RotatePivotTowards(targetWorld);
        }
        else if ((mouseAttackReleased || controllerAttackReleased) && isHolding){
            isHolding = false;
            StopAttack();
        }
    }

    private bool IsInteractionBlockedByUI(bool usingPointerInput, bool usingControllerInput)
    {
        if (usingControllerInput && IsControllerOverBlockingUI()){
            return true;
        }

        if (!usingPointerInput){
            return false;
        }

        Vector2 screenPos = GetPointerScreenPosition();

        if (joystickZone != null && RectTransformUtility.RectangleContainsScreenPoint(joystickZone, screenPos, null)){
            return true;
        }

        if (HotBarRow != null && RectTransformUtility.RectangleContainsScreenPoint(HotBarRow, screenPos, null)){
            return true;
        }

        return IsPointerOverInteractiveInventoryUI(screenPos);
    }

    private bool IsControllerOverBlockingUI()
    {
        if (EventSystem.current == null){
            return false;
        }

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        if (selectedObject == null){
            return false;
        }

        if (inventoryPanel != null && selectedObject.transform.IsChildOf(inventoryPanel)){
            return true;
        }

        return selectedObject.GetComponentInParent<Selectable>() != null
            || selectedObject.GetComponentInParent<InventoryDragHandle>() != null;
    }

    private bool IsPointerOverInteractiveInventoryUI(Vector2 screenPos)
    {
        if (inventoryPanel == null || EventSystem.current == null){
            return false;
        }

        PointerEventData eventData = new(EventSystem.current) { position = screenPos };
        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, uiRaycastResults);

        foreach (RaycastResult result in uiRaycastResults){
            GameObject hitObject = result.gameObject;
            if (hitObject == null || !hitObject.transform.IsChildOf(inventoryPanel)){
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

    private Vector2 GetPointerScreenPosition()
    {
        if (BlockInteraction.TryGetActiveControllerPointerScreenPosition(out Vector2 controllerPointerPosition)){
            return controllerPointerPosition;
        }

        if (Application.isMobilePlatform && Input.touchCount > 0){
            return Input.GetTouch(0).position;
        }

        return Input.mousePosition;
    }

    private void ApplyHover()
    {
        Pivot.localRotation = Quaternion.identity;

        float sin = Mathf.Sin(Time.time * hoverSpeed);
        float hoverOffset = sin * hoverAmplitude;
        float wobble = sin * HoverWobbleDegrees;
        float breathe = 1f + Mathf.Sin(Time.time * hoverSpeed * 0.5f) * HoverScaleAmplitude;

        FistSprite.localPosition = new Vector3(
            idleLocalPosition.x,
            idleLocalPosition.y + hoverOffset,
            idleLocalPosition.z);
        FistSprite.localRotation = Quaternion.Euler(0f, 0f, wobble);
        FistSprite.localScale = new Vector3(breathe, breathe, 1f);
    }

    private void StartAttack(Vector3 pointerWorld)
    {
        RotatePivotTowards(pointerWorld);
        MoveToTarget(pointerWorld);

        if (animCoroutine != null){
            StopCoroutine(animCoroutine);
        }

        animCoroutine = StartCoroutine(PopIn());
        GameAudio.PlaySwing();
        onAttack?.Invoke(this, new AttackEventArgs
        {
            Origin = Pivot.position,
            TargetPosition = pointerWorld,
        });
    }

    private void MoveToTarget(Vector3 pointerWorld)
    {
        Vector3 direction = pointerWorld - Pivot.position;
        if (direction.sqrMagnitude > maxRangeSqr){
            pointerWorld = Pivot.position + direction.normalized * maxRange;
        }

        FistSprite.position = pointerWorld;
    }

    private void RotatePivotTowards(Vector3 targetWorld)
    {
        Vector2 direction = targetWorld - transform.position;
        Pivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
    }

    private void StopAttack()
    {
        if (animCoroutine != null){
            StopCoroutine(animCoroutine);
        }

        animCoroutine = StartCoroutine(PopOut());
    }

    private IEnumerator PopIn()
    {
        float elapsed = 0f;

        while (elapsed < popDuration){
            elapsed += Time.deltaTime;
            float t = popDuration <= 0f ? 1f : elapsed / popDuration;
            float scale = Mathf.Lerp(0f, maxScale, t);
            FistSprite.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        FistSprite.localScale = new Vector3(maxScale, maxScale, 1f);
    }

    private IEnumerator PopOut()
    {
        float elapsed = 0f;
        Vector3 startScale = FistSprite.localScale;

        while (elapsed < shrinkDuration){
            elapsed += Time.deltaTime;
            float t = shrinkDuration <= 0f ? 1f : elapsed / shrinkDuration;
            float scale = Mathf.Lerp(startScale.x, 1f, t);
            FistSprite.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        FistSprite.localScale = Vector3.one;
        if (trail != null){
            trail.Clear();
        }

        returnCoroutine = StartCoroutine(ReturnToIdle());
    }

    private IEnumerator ReturnToIdle()
    {
        while (Vector3.Distance(FistSprite.localPosition, idleLocalPosition) > 0.01f){
            FistSprite.localPosition = Vector3.Lerp(
                FistSprite.localPosition,
                idleLocalPosition,
                Time.deltaTime * returnSpeed);
            yield return null;
        }

        FistSprite.localPosition = idleLocalPosition;
        returnCoroutine = null;
    }

    private void StopReturnCoroutine()
    {
        if (returnCoroutine == null){
            return;
        }

        StopCoroutine(returnCoroutine);
        returnCoroutine = null;
    }

    private Vector3 Aim()
    {
        if (mainCamera == null){
            return transform.position;
        }

        Vector3 screenPoint = GetPointerScreenPosition();
        Vector3 worldPoint = mainCamera.ScreenToWorldPoint(screenPoint);
        worldPoint.z = 0f;
        return worldPoint;
    }

    private Vector3 ResolveTargetWorld()
    {
        if (Time.time - externalTargetTimestamp <= 0.1f){
            return externalTargetWorld;
        }

        return Aim();
    }

    private bool IsControllerAttackHeld()
    {
        Gamepad gamepad = Gamepad.current;
        return gamepad != null && gamepad.rightTrigger.ReadValue() >= ControllerTriggerPressThreshold;
    }
}
