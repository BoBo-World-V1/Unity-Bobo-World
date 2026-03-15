using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
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
    private const float touchDeadZone = 10f;

    // [OPT 2] Cache animator hash — string lookups every frame are slow
    private static readonly int IsRunningHash = Animator.StringToHash("isRunning");
    private static readonly int IsJumpHash = Animator.StringToHash("isJump");

    // [OPT 3] Reuse a single velocity variable to avoid struct allocation each FixedUpdate
    private Vector2 velocity;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        initialLocalScale = transform.localScale;
        if (animator == null) animator = GetComponent<Animator>();

        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            moveAction = playerInput.actions["Move"];
            jumpAction = playerInput.actions["Jump"]; // [OPT 1] cached here

        }

        isMobilePlatform = Application.isMobilePlatform;
    }

    void OnEnable()
    {
        if (isMobilePlatform) EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        if (isMobilePlatform) EnhancedTouchSupport.Disable();
    }

    void Update()
    {
        movement = Vector2.zero;

        if (moveAction != null)
        {
            movement = moveAction.ReadValue<Vector2>();
            // [OPT 1] jumpAction cached — no dictionary lookup every frame
            if (!jumpRequested && isGrounded && jumpAction != null && jumpAction.WasPressedThisFrame())
            {
                jumpRequested = true;
                isGrounded = false;
            }
        }
        else
        {
            movement.x = Input.GetAxisRaw("Horizontal");
            if (!jumpRequested && isGrounded && Input.GetButtonDown("Jump"))
            {
                jumpRequested = true;
                isGrounded = false;
            }
        }

        // [OPT 4] sqrMagnitude avoids a sqrt — sufficient for the > 1 check
        if (movement.sqrMagnitude > 1f) movement.Normalize();

        UpdateFacingDirection();
        UpdateAnimation();
    }

    void FixedUpdate()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // [OPT 3] Reuse cached velocity struct
        velocity.x = movement.x * moveSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;

        if (jumpRequested)
        {
            rb.AddForce(new Vector2(0f, jumpForce), ForceMode2D.Impulse);
            jumpRequested = false;
        }
    }

    void OnCollisionEnter2D(Collision2D col) {Debug.Log("Hit: " + col.gameObject.name);}

    private void UpdateAnimation()
    {
        if (animator == null) return;

        bool isInJumpState = jumpRequested || !isGrounded || rb.linearVelocity.y > 0.01f;
        animator.SetBool(IsRunningHash, movement.x > 0.01f || movement.x < -0.01f);
        animator.SetBool(IsJumpHash, isInJumpState);
    }

    private void UpdateFacingDirection()
    {
        if (movement.x > 0.01f)
            transform.localScale = new Vector3(-Mathf.Abs(initialLocalScale.x), initialLocalScale.y, initialLocalScale.z);
        else if (movement.x < -0.01f)
            transform.localScale = new Vector3(Mathf.Abs(initialLocalScale.x), initialLocalScale.y, initialLocalScale.z);
    }

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
        return delta.sqrMagnitude < touchDeadZone * touchDeadZone ? Vector2.zero : delta.normalized; // [OPT 4]
    }

    public void SetMobileMovement(Vector2 input)
    {
        if (!isMobilePlatform) return;
        movement = input;
        if (movement.sqrMagnitude > 1f) movement.Normalize();
    }
}