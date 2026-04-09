// ──────────────────────────────────────────────────────────────────────────────
// FUTURE REFACTOR NOTES — PlayerController.cs
// ──────────────────────────────────────────────────────────────────────────────
//
// STEP 1 — Java Backend Integration
//   - Movement should be sent to Java via PacketSender.cs every FixedUpdate
//   - Java validates movement (speed hack prevention, collision server-side)
//   - Other players positions come from PacketReceiver.cs not this script
//
// STEP 2 — Create OtherPlayerController.cs
//   - This script is ONLY for the local player
//   - Other players in the world use OtherPlayerController.cs
//   - Driven purely by server data, no input reading
//
// STEP 3 — Mobile joystick
//   - Uncomment and wire up FloatingJoystick when Joystick Pack is imported
//   - Test on Android/iOS build, not just Unity editor
//
// STEP 4 — Player stats
//   - Add health, level, and gem count synced from Java
//   - Java is the source of truth for all player stats
//
// ──────────────────────────────────────────────────────────────────────────────
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    private const string MoveAxisName = "Horizontal";
    private const string JumpButtonName = "Jump";
    private const float MoveDeadZone = 0.01f;
    private const float TouchDeadZone = 10f;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    [Header("Jump Settings")]
    public float jumpForce = 10f;
    private bool isGrounded = true;
    private bool jumpRequested = false;

    [Header("Ground Check Settings")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    // ── internals ──────────────────────────────────────────────────────────────
    private Rigidbody2D rb;
    private Vector2 movement;
    private Vector3 initialLocalScale;

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction; // [OPT 1] cache jump action instead of looking it up every frame

    private bool isMobilePlatform;
    private Vector2 touchStartPos;
    private bool isTouching;

    // [OPT 2] Cache animator hash — string lookups every frame are slow
    private static readonly int IsRunningHash = Animator.StringToHash("isRunning");
    private static readonly int IsJumpHash = Animator.StringToHash("isJump");

    // [OPT 3] Reuse a single velocity variable to avoid struct allocation each FixedUpdate
    private Vector2 velocity;

    void Awake(){
        CacheComponents();
        initialLocalScale = transform.localScale;
        isMobilePlatform = Application.isMobilePlatform;
    }

    void OnEnable() { if (isMobilePlatform) EnhancedTouchSupport.Enable(); }
    void OnDisable(){ if (isMobilePlatform) EnhancedTouchSupport.Disable(); }

    void Update(){
        ReadMovementInput();
        HandleJumpRequest();
        NormalizeMovement();
        UpdateFacingDirection();
        UpdateAnimation();
    }

    void FixedUpdate(){
        UpdateGroundedState();
        ApplyHorizontalVelocity();
        ApplyJumpIfRequested();
    }

    void OnCollisionEnter2D(Collision2D col) { Debug.Log("Hit: " + col.gameObject.name); }

    private void CacheComponents(){
        rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();

        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null) return;

        moveAction = playerInput.actions["Move"];
        jumpAction = playerInput.actions["Jump"]; // [OPT 1] cached here
    }

    private void ReadMovementInput(){
        movement = Vector2.zero;

        if (moveAction != null){
            movement = moveAction.ReadValue<Vector2>();
            return;
        }

        movement.x = Input.GetAxisRaw(MoveAxisName);
    }

    private void HandleJumpRequest(){
        if (jumpRequested || !isGrounded) return;
        if (moveAction != null){
            // [OPT 1] jumpAction cached — no dictionary lookup every frame
            if (jumpAction != null && jumpAction.WasPressedThisFrame()){
                jumpRequested = true;
                isGrounded = false;
            }
            return;
        }

        if (Input.GetButtonDown(JumpButtonName)){
            jumpRequested = true;
            isGrounded = false;
        }
    }

    private void NormalizeMovement(){
        // [OPT 4] sqrMagnitude avoids a sqrt — sufficient for the > 1 check
        if (movement.sqrMagnitude > 1f) movement.Normalize();
    }

    private void UpdateGroundedState(){
        if (groundCheck == null) return;
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void ApplyHorizontalVelocity(){
        if (rb == null) return;

        // [OPT 3] Reuse cached velocity struct
        velocity.x = movement.x * moveSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;
    }

    private void ApplyJumpIfRequested(){
        if (rb == null || !jumpRequested) return;

        if (jumpRequested){
            rb.AddForce(new Vector2(0f, jumpForce), ForceMode2D.Impulse);
            jumpRequested = false;
        }
    }

    private void UpdateAnimation(){
        if (animator == null) return;

        bool isInJumpState = jumpRequested || !isGrounded || (rb != null && rb.linearVelocity.y > MoveDeadZone);
        animator.SetBool(IsRunningHash, movement.x > MoveDeadZone || movement.x < -MoveDeadZone);
        animator.SetBool(IsJumpHash, isInJumpState);
    }

    private void UpdateFacingDirection(){
        if (movement.x > MoveDeadZone)
            transform.localScale = new Vector3(-Mathf.Abs(initialLocalScale.x), initialLocalScale.y, initialLocalScale.z);
        else if (movement.x < -MoveDeadZone)
            transform.localScale = new Vector3(Mathf.Abs(initialLocalScale.x), initialLocalScale.y, initialLocalScale.z);
    }

    private Vector2 ReadTouchInput(){
        var touches = Touch.activeTouches;
        if (touches.Count == 0){
            isTouching = false;
            return Vector2.zero;
        }

        var touch = touches[0];

        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began){
            touchStartPos = touch.screenPosition;
            isTouching = true;
        }

        if (!isTouching) return Vector2.zero;

        Vector2 delta = touch.screenPosition - touchStartPos;
        return delta.sqrMagnitude < TouchDeadZone * TouchDeadZone ? Vector2.zero : delta.normalized; // [OPT 4]
    }

    public void SetMobileMovement(Vector2 input){
        if (!isMobilePlatform) return;
        movement = input;
        if (movement.sqrMagnitude > 1f) movement.Normalize();
    }
}