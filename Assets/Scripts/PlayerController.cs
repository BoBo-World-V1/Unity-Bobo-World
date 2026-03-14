using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// PlayerController — works on PC (WASD/Arrow keys), Gamepad, and Mobile (virtual joystick or touch).
/// Requires: Unity New Input System package installed.
/// Attach to your player GameObject alongside a Rigidbody2D.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    [Header("Jump Settings")]
    public float jumpForce = 10f;
    private bool isGrounded = true; // Simple grounded check (you may want to improve this)
    private bool jumpRequested = false;

    [Header("Ground Check Settings")]
    public Transform groundCheck; // Assign a child GameObject for ground checking
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer; // Assign the ground layer in the Inspector

    [Header("Mobile Joystick (optional)")]
    [Tooltip("Assign your on-screen joystick UI script here if using one.")]
    // public FloatingJoystick mobileJoystick; // Optional — assign in Inspector if using a joystick asset

    // ── internals ──────────────────────────────────────────────────────────────
    private Rigidbody2D rb;
    private Vector2 movement;

    // New Input System actions (auto-detected for keyboard + gamepad)
    private PlayerInput playerInput;
    private InputAction moveAction;

    // Mobile touch fallback (used only if no joystick asset is assigned)
    private bool isMobilePlatform;
    private Vector2 touchStartPos;
    private bool isTouching;
    private const float touchDeadZone = 10f; // pixels

    // ── Unity lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Try to get PlayerInput component for New Input System
        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null) moveAction = playerInput.actions["Move"];
        

        isMobilePlatform = Application.isMobilePlatform;
    }

    void OnEnable()
    {
        // Enable enhanced touch support for mobile fallback
        if (isMobilePlatform) EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        if (isMobilePlatform) EnhancedTouchSupport.Disable();
    }

    void Update()
    {
        movement = Vector2.zero;

        // ── 1. New Input System (keyboard + gamepad) ───────────────────────────
        if (moveAction != null)
        {
            movement = moveAction.ReadValue<Vector2>();
            if (playerInput.actions["Jump"].WasPressedThisFrame() && isGrounded) jumpRequested = true;
        }
        // ── 2. Legacy fallback (if no PlayerInput component on GameObject) ─────
        else
        {
            movement.x = Input.GetAxisRaw("Horizontal");
            if (Input.GetButtonDown("Jump") && isGrounded) jumpRequested = true;
        }

        // ── 3. Mobile: virtual joystick asset (highest priority on mobile) ─────
        // if (isMobilePlatform && mobileJoystick != null)
        // {
        //     movement = mobileJoystick.Direction; // works with most joystick assets
        // }
        // // ── 4. Mobile: raw touch fallback (swipe direction) ───────────────────
        // else if (isMobilePlatform && mobileJoystick == null)
        // {
        //     movement = ReadTouchInput();
        // }

        // Normalize so diagonal movement isn't faster
        if (movement.magnitude > 1f)
            movement.Normalize();
    }

    void FixedUpdate()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        rb.linearVelocity = new Vector2(movement.x * moveSpeed, rb.linearVelocity.y);

        if (jumpRequested)
        {
            rb.AddForce(new Vector2(0f, jumpForce), ForceMode2D.Impulse);
            jumpRequested = false;
        }
    }
    void OnCollisionEnter2D(Collision2D col)
    {
        Debug.Log("Hit: " + col.gameObject.name);
    }

    // ── Mobile touch input (fallback when no joystick asset) ──────────────────

    private Vector2 ReadTouchInput()
    {
        var touches = Touch.activeTouches;
        if (touches.Count == 0)
        {
            isTouching = false;
            return Vector2.zero;
        }

        var touch = touches[0];

        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            touchStartPos = touch.screenPosition;
            isTouching = true;
        }

        if (!isTouching) return Vector2.zero;

        Vector2 delta = touch.screenPosition - touchStartPos;

        if (delta.magnitude < touchDeadZone)
            return Vector2.zero;

        return delta.normalized;
    }

    // ── Public API for custom mobile joystick UI ───────────────────────────────

    /// <summary>
    /// Call this from your own joystick UI script if not using FloatingJoystick.
    /// Pass a normalized Vector2 (-1 to 1 on each axis).
    /// </summary>
    public void SetMobileMovement(Vector2 input)
    {
        if (!isMobilePlatform) return;
        movement = input;
        if (movement.magnitude > 1f)
            movement.Normalize();
    }
}