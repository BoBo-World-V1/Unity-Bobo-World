using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    private const string MoveAxisName = "Horizontal";
    private const string JumpButtonName = "Jump";
    private const float MoveDeadZone = 0.01f;
    private const float GroundedVelocityThreshold = 0.15f;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    [Header("Jump Settings")]
    public float jumpForce = 5f;

    [Header("Ground Check Settings")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer;
    [Range(0.01f, 0.3f)] public float groundProbeDistance = 0.08f;
    [Range(0.5f, 1f)] public float groundProbeWidthFactor = 0.9f;
    [Range(0f, 0.25f)] public float groundedGraceTime = 0.08f;

    [Header("Collision Tuning")]
    [Range(0f, 0.08f)] public float colliderEdgeRadius = 0.03f;
    [Range(0f, 0.08f)] public float tileExtrusionFactor = 0.02f;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    private static readonly int IsRunningHash = Animator.StringToHash("isRunning");
    private static readonly int IsJumpHash = Animator.StringToHash("isJump");

    private Rigidbody2D rb;
    private BoxCollider2D bodyCollider;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;
    private Vector2 movement;
    private Vector2 velocity;
    private Vector2 mobileMovementInput;
    private RaycastHit2D[] groundHits = new RaycastHit2D[4];
    private Vector3 initialLocalScale;
    private bool isGrounded = true;
    private bool jumpRequested;
    private bool mobileJumpRequested;
    private bool useMobileMovementInput;
    private bool hasGroundedState;
    private float lastGroundedTime;

    private void Awake()
    {
        CacheComponents();
        OptimizeGroundColliders();
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
        bodyCollider = GetComponent<BoxCollider2D>();
        playerInput = GetComponent<PlayerInput>();

        if (animator == null){
            animator = GetComponent<Animator>();
        }

        if (bodyCollider != null && colliderEdgeRadius > 0f){
            bodyCollider.edgeRadius = colliderEdgeRadius;
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

        if (!jumpPressed || jumpRequested || !CanJump()){
            return;
        }

        jumpRequested = true;
        isGrounded = false;
    }

    private void NormalizeMovement()
    {
        if (playerInput != null && playerInput.currentControlScheme != "Keyboard&Mouse"){
            movement.y = 0f;
            if (Mathf.Abs(movement.x) > MoveDeadZone){
                movement.x = Mathf.Sign(movement.x);
            }
            else {
                movement.x = 0f;
            }
            return;
        }

        if (movement.sqrMagnitude > 1f){
            movement.Normalize();
        }
    }

    private void UpdateGroundedState()
    {
        bool detectedGround = DetectGroundBelow();
        if (detectedGround){
            isGrounded = true;
            lastGroundedTime = Time.time;
            return;
        }

        // Fall immediately when support is gone, but still allow a short coyote-time jump.
        isGrounded = false;
    }

    private void OptimizeGroundColliders()
    {
        if (tileExtrusionFactor <= 0f){
            return;
        }

        TilemapCollider2D[] tilemapColliders = FindObjectsByType<TilemapCollider2D>(FindObjectsInactive.Exclude);
        for (int i = 0; i < tilemapColliders.Length; i++){
            TilemapCollider2D tilemapCollider = tilemapColliders[i];
            if (tilemapCollider == null){
                continue;
            }

            if ((groundLayer.value & (1 << tilemapCollider.gameObject.layer)) == 0){
                continue;
            }

            if (tilemapCollider.extrusionFactor >= tileExtrusionFactor){
                continue;
            }

            tilemapCollider.extrusionFactor = tileExtrusionFactor;
            tilemapCollider.ProcessTilemapChanges();
        }
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

        bool isInJumpState = jumpRequested
            || !isGrounded
            || (rb != null && rb.linearVelocity.y > GroundedVelocityThreshold);
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

    private bool CanJump()
    {
        return isGrounded || Time.time - lastGroundedTime <= groundedGraceTime;
    }

    private bool DetectGroundBelow()
    {
        if (bodyCollider != null){
            return DetectGroundUsingColliderCast();
        }

        if (groundCheck == null){
            return false;
        }

        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private bool DetectGroundUsingColliderCast()
    {
        ContactFilter2D filter = new();
        filter.useLayerMask = true;
        filter.layerMask = groundLayer;
        filter.useTriggers = false;

        int hitCount = bodyCollider.Cast(Vector2.down, filter, groundHits, groundProbeDistance + 0.02f);
        for (int i = 0; i < hitCount; i++){
            RaycastHit2D hit = groundHits[i];
            if (hit.collider == null){
                continue;
            }

            if (hit.normal.y < 0.35f){
                continue;
            }

            float hitWidth = Mathf.Abs(hit.centroid.x - bodyCollider.bounds.center.x);
            float allowedWidth = bodyCollider.bounds.extents.x * Mathf.Clamp01(groundProbeWidthFactor + 0.1f);
            if (hitWidth <= allowedWidth + 0.08f){
                return true;
            }
        }

        Bounds bounds = bodyCollider.bounds;
        Vector2 origin = new(bounds.center.x, bounds.min.y - 0.01f);
        Vector2 size = new(bounds.size.x * groundProbeWidthFactor, groundProbeDistance);
        return Physics2D.OverlapBox(origin, size, 0f, groundLayer) != null;
    }

    private void OnDrawGizmosSelected()
    {
        if (bodyCollider == null){
            bodyCollider = GetComponent<BoxCollider2D>();
        }

        if (bodyCollider != null){
            Bounds bounds = bodyCollider.bounds;
            Vector3 center = new(bounds.center.x, bounds.min.y - (groundProbeDistance * 0.5f) - 0.01f, 0f);
            Vector3 size = new(bounds.size.x * groundProbeWidthFactor, groundProbeDistance, 0f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(center, size);
            return;
        }

        if (groundCheck != null){
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
