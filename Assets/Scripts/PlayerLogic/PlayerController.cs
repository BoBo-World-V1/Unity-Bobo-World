using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    private const string MoveAxisName = "Horizontal";
    private const string JumpButtonName = "Jump";
    private const float MoveDeadZone = 0.01f;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    [Header("Jump Settings")]
    public float jumpForce = 10f;

    [Header("Ground Check Settings")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    private static readonly int IsRunningHash = Animator.StringToHash("isRunning");
    private static readonly int IsJumpHash = Animator.StringToHash("isJump");

    private Rigidbody2D rb;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;
    private Vector2 movement;
    private Vector2 velocity;
    private Vector2 mobileMovementInput;
    private Vector3 initialLocalScale;
    private bool isGrounded = true;
    private bool jumpRequested;
    private bool mobileJumpRequested;
    private bool useMobileMovementInput;
    private bool hasGroundedState;

    private void Awake()
    {
        CacheComponents();
        initialLocalScale = transform.localScale;
    }

    private void Update()
    {
        ReadMovementInput();
        HandleJumpRequest();
        NormalizeMovement();
        UpdateFacingDirection();
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        bool wasGrounded = isGrounded;
        UpdateGroundedState();
        if (hasGroundedState && !wasGrounded && isGrounded){
            GameAudio.PlayLand();
        }

        hasGroundedState = true;
        ApplyHorizontalVelocity();
        ApplyJumpIfRequested();
    }

    public void SetMobileMovement(Vector2 input)
    {
        mobileMovementInput = Vector2.ClampMagnitude(input, 1f);
        useMobileMovementInput = true;
    }

    public void RequestJump()
    {
        mobileJumpRequested = true;
    }

    private void CacheComponents()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();

        if (animator == null){
            animator = GetComponent<Animator>();
        }

        if (playerInput == null){
            return;
        }

        moveAction = playerInput.actions["Move"];
        jumpAction = playerInput.actions["Jump"];
    }

    private void ReadMovementInput()
    {
        if (useMobileMovementInput){
            movement = mobileMovementInput;
            return;
        }

        if (moveAction != null){
            movement = moveAction.ReadValue<Vector2>();
            return;
        }

        movement = new Vector2(Input.GetAxisRaw(MoveAxisName), 0f);
    }

    private void HandleJumpRequest()
    {
        bool jumpPressed = jumpAction != null ? jumpAction.WasPressedThisFrame() : Input.GetButtonDown(JumpButtonName);
        jumpPressed |= mobileJumpRequested;
        mobileJumpRequested = false;

        if (!jumpPressed || jumpRequested || !isGrounded){
            return;
        }

        jumpRequested = true;
        isGrounded = false;
    }

    private void NormalizeMovement()
    {
        if (movement.sqrMagnitude > 1f){
            movement.Normalize();
        }
    }

    private void UpdateGroundedState()
    {
        if (groundCheck == null){
            return;
        }

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void ApplyHorizontalVelocity()
    {
        if (rb == null){
            return;
        }

        velocity.x = movement.x * moveSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;
    }

    private void ApplyJumpIfRequested()
    {
        if (rb == null || !jumpRequested){
            return;
        }

        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        jumpRequested = false;
        GameAudio.PlayJump();
    }

    private void UpdateAnimation()
    {
        if (animator == null){
            return;
        }

        bool isInJumpState = jumpRequested || !isGrounded || (rb != null && rb.linearVelocity.y > MoveDeadZone);
        animator.SetBool(IsRunningHash, Mathf.Abs(movement.x) > MoveDeadZone);
        animator.SetBool(IsJumpHash, isInJumpState);
    }

    private void UpdateFacingDirection()
    {
        if (movement.x > MoveDeadZone){
            transform.localScale = new Vector3(-Mathf.Abs(initialLocalScale.x), initialLocalScale.y, initialLocalScale.z);
        }
        else if (movement.x < -MoveDeadZone){
            transform.localScale = new Vector3(Mathf.Abs(initialLocalScale.x), initialLocalScale.y, initialLocalScale.z);
        }
    }
}
