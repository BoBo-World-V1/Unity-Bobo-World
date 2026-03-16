// ──────────────────────────────────────────────────────────────────────────────
// FUTURE REFACTOR NOTES — WeaponSystem.cs
// ──────────────────────────────────────────────────────────────────────────────
//
// STEP 1 — Refactor to WeaponBase system when 2nd weapon is added
//   - Create WeaponBase.cs (abstract class) with shared logic
//   - Move Aim(), pivot rotation, onAttack event, trail into WeaponBase
//   - Rename this file to FistWeapon.cs extending WeaponBase
//   - Each new weapon gets its own script extending WeaponBase
//
// STEP 2 — Create WeaponManager.cs
//   - Holds list of all weapons player owns
//   - Handles equipping/switching weapons
//   - Syncs equipped weapon with Java backend
//
// STEP 3 — Java Backend Integration
//   - Attack action should be sent to Java via PacketSender.cs
//   - Java validates attack (range, cooldown, permissions)
//   - onAttack event should trigger PacketSender.SendAttack()
//
// STEP 4 — Mobile support
//   - Replace Input.GetMouseButton with New Input System touch actions
//   - Virtual attack button for mobile UI
//   - Test fist position with touch input on device
//
// STEP 5 — Hit detection
//   - Add OverlapCircle or Raycast at fist position on attack
//   - Detect enemies, players, and interactable objects in range
//   - Send hit event to Java for validation
//
// ──────────────────────────────────────────────────────────────────────────────
using UnityEngine;
using System;
using System.Collections;
using UnityEngine.EventSystems;

public class WeaponSystem : MonoBehaviour
{
    [SerializeField] private Transform Pivot;
    [SerializeField] private Transform FistSprite;
    public event EventHandler<AttackEventArgs> onAttack;

    public class AttackEventArgs : EventArgs
    {
        public Vector3 Origin { get; set; }
        public Vector3 TargetPosition { get; set; }
    }

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
    public Color trailStartColor = new Color(1f, 1f, 1f, 0.8f);
    public Color trailEndColor = new Color(1f, 1f, 1f, 0f);

    [Header("Idle Hover")]
    public float hoverAmplitude = 0.1f;
    public float hoverSpeed = 2f;
    public float returnSpeed = 8f;

    [Header("UI Blockers")]
    public RectTransform inventoryPanel;  // Drag InventoryPanel here
    public RectTransform joystickZone;    // Drag joystick zone here
    public RectTransform HotBarRow; // Drag action buttons zone here

    [Header("References")]
    public Inventory inventory;   // drag Player inventory here

    // ── internals ──────────────────────────────────────────────────────────────
    private Camera mainCamera;
    private float maxRangeSqr;
    private TrailRenderer trail;
    private Coroutine animCoroutine;
    private Coroutine returnCoroutine;
    private bool isHolding;
    private Vector3 idleLocalPosition;

    private void Awake()
    {
        mainCamera = Camera.main;
        maxRangeSqr = maxRange * maxRange;

        // Save wherever the fist is placed in Unity as the idle position
        idleLocalPosition = FistSprite.localPosition;

        FistSprite.localScale = Vector3.one;
        FistSprite.gameObject.SetActive(true);

        SetupTrail();
    }

    private void SetupTrail()
    {
        trail = FistSprite.GetComponent<TrailRenderer>();
        if (trail == null)
            trail = FistSprite.gameObject.AddComponent<TrailRenderer>();

        trail.time = trailTime;
        trail.startWidth = trailStartWidth;
        trail.endWidth = trailEndWidth;
        trail.minVertexDistance = 0.01f;
        trail.autodestruct = false;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(trailStartColor, 0f),
                new GradientColorKey(trailEndColor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(trailStartColor.a, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = gradient;
        trail.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void Update()
    {
         // Disable weapon system when block is selected
        if (inventory != null && !inventory.IsFistSelected)
        {
            if (isHolding)
            {
                isHolding = false;
                StopAttack();
            }
            ApplyHover();
            return;  // skip all attack logic
        }
        Vector3 mouseWorld = Aim();

        // Block attack if clicking on UI
        if (IsPointerOverBlockingUI())
        {
            if (isHolding)
            {
                isHolding = false;
                StopAttack();
            }
            if (!isHolding)
                ApplyHover();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            isHolding = true;
            StopReturnCoroutine();
            StartAttack(mouseWorld);
        }
        else if (Input.GetMouseButton(0) && isHolding)
        {
            // Only rotate pivot while attacking
            Vector2 direction = mouseWorld - transform.position;
            Pivot.rotation = Quaternion.Euler(0, 0,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);

            MoveToMouse(mouseWorld);
        }
        else if (Input.GetMouseButtonUp(0) && isHolding)
        {
            isHolding = false;
            StopAttack();
        }

        // Idle hover — only when not holding
        if (!isHolding)
            ApplyHover();
    }

    private bool IsPointerOverBlockingUI()
    {
        Vector2 screenPos = Application.isMobilePlatform && Input.touchCount > 0
            ? (Vector2)Input.GetTouch(0).position
            : (Vector2)Input.mousePosition;

        if (inventoryPanel != null && RectTransformUtility.RectangleContainsScreenPoint(
            inventoryPanel, screenPos, null))
            return true;

        if (joystickZone != null && RectTransformUtility.RectangleContainsScreenPoint(
            joystickZone, screenPos, null))
            return true;
        
        if (HotBarRow != null && RectTransformUtility.RectangleContainsScreenPoint(
            HotBarRow, screenPos, null))
            return true;

        return false;
    }

    private void ApplyHover()
    {
        Pivot.localRotation = Quaternion.identity;

        float sin = Mathf.Sin(Time.time * hoverSpeed); // reuse same sin wave

        // Hover bob (already have this)
        float hoverOffset = sin * hoverAmplitude;

        // Wobble rotation — rocks ±15 degrees
        float wobble = sin * 15f;

        // Breathe scale — pulses between 0.9 and 1.1
        float breathe = 1f + Mathf.Sin(Time.time * hoverSpeed * 0.5f) * 0.1f;

        FistSprite.localPosition = new Vector3(
            idleLocalPosition.x,
            idleLocalPosition.y + hoverOffset,
            idleLocalPosition.z
        );
        FistSprite.localRotation = Quaternion.Euler(0, 0, wobble);
        FistSprite.localScale = new Vector3(breathe, breathe, 1f);
    }

    private void StartAttack(Vector3 mouseWorld)
    {
        // Rotate pivot to face mouse on first click
        Vector2 direction = mouseWorld - transform.position;
        Pivot.rotation = Quaternion.Euler(0, 0,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);

        MoveToMouse(mouseWorld);

        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(PopIn());

        onAttack?.Invoke(this, new AttackEventArgs {
            Origin = Pivot.position,
            TargetPosition = mouseWorld
        });
    }

    private void MoveToMouse(Vector3 mouseWorld)
    {
        Vector3 direction = mouseWorld - Pivot.position;
        if (direction.sqrMagnitude > maxRangeSqr)
            mouseWorld = Pivot.position + direction.normalized * maxRange;

        FistSprite.position = mouseWorld;
    }

    private void StopAttack()
    {
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(PopOut());
    }

    private IEnumerator PopIn()
    {
        float elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popDuration;
            float scale = Mathf.Lerp(0f, maxScale, t);
            FistSprite.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        FistSprite.localScale = new Vector3(maxScale, maxScale, 1f);
    }

    private IEnumerator PopOut()
    {
        // Shrink back to normal scale
        float elapsed = 0f;
        Vector3 startScale = FistSprite.localScale;
        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shrinkDuration;
            float scale = Mathf.Lerp(startScale.x, 1f, t);
            FistSprite.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        FistSprite.localScale = Vector3.one;
        trail.Clear();

        // Smoothly return fist to idle position
        returnCoroutine = StartCoroutine(ReturnToIdle());
    }

    private IEnumerator ReturnToIdle()
    {
        while (Vector3.Distance(FistSprite.localPosition, idleLocalPosition) > 0.01f)
        {
            FistSprite.localPosition = Vector3.Lerp(
                FistSprite.localPosition,
                idleLocalPosition,
                Time.deltaTime * returnSpeed
            );
            yield return null;
        }
        FistSprite.localPosition = idleLocalPosition;
    }

    private void StopReturnCoroutine()
    {
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }
    }

    private Vector3 Aim()
    {
        Vector3 vec = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        return new Vector3(vec.x, vec.y, 0f);
    }
}