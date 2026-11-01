
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FrogController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float swimSpeed = 4f;
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField] private float backTurnSpeed = 360f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 10f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Mud")]
    [SerializeField] private float mudSpeedMultiplier = 0.4f;

    [Header("Ground Following")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckHeight = 3f;
    [SerializeField] private float groundCheckDistance = 8f;
    [SerializeField] private float groundOffset = 0.8f;
    [SerializeField] private float groundFollowSpeed = 20f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.3f;
    [SerializeField] private float jumpDuration = 0.8f;
    [SerializeField] private float jumpCooldown = 0.15f;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Attack")]
    [SerializeField] private Camera attackCamera;
    [SerializeField] private float attackRotationSpeed = 360f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Obstacle Detection")]
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private CapsuleCollider frogCollider;
    [SerializeField] private float obstacleSkin = 0.05f;

    private Rigidbody rb;

    private float moveInput;
    private float turnInput;

    private Vector3 dashDirection;
    private float dashTimer;
    private float cooldownTimer;

    private bool isDashing;
    private bool isInMud;

    private bool isTurningBack;
    private float targetBackYaw;

    private bool isTurningToFly;
    private Quaternion flyTargetRotation;

    private bool isJumping;
    private bool jumpRequested;
    private float jumpTimer;
    private float jumpCooldownTimer;
    private float jumpStartY;
    private float jumpGroundY;
    private float jumpLastHeight;

    private static readonly int IsMovingHash =
        Animator.StringToHash("IsMoving");

    private static readonly int IsDashingHash =
        Animator.StringToHash("IsDashing");

    private static readonly int AttackHash =
        Animator.StringToHash("Attack");

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true;

        rb.interpolation = RigidbodyInterpolation.Interpolate;

        rb.constraints =
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (attackCamera == null)
            attackCamera = Camera.main;

        if (animator == null)
        {
            Debug.LogError(
                "FrogController: Animator bulunamadý!",
                this
            );
        }
        else
        {
            animator.applyRootMotion = false;
        }
    }

    private void Update()
    {
        HandleMovementInput();
        HandleDash();
        HandleJumpInput();
        HandleAttack();
        UpdateAnimator();
    }

    private void HandleMovementInput()
    {
        float vertical = Input.GetAxisRaw("Vertical");

        moveInput = Mathf.Max(0f, vertical);
        turnInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetKeyDown(KeyCode.S)
            && !isTurningBack
            && !isDashing)
        {
            StartBackTurn();
        }

        if (isTurningBack)
        {
            moveInput = 0f;
            turnInput = 0f;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            moveInput = 1f;
        }
    }

    private void StartBackTurn()
    {
        isTurningBack = true;

        targetBackYaw =
            rb.rotation.eulerAngles.y + 180f;

        moveInput = 0f;
        turnInput = 0f;
    }

    private void HandleDash()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer =
                Mathf.Max(0f, cooldownTimer - Time.deltaTime);
        }

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;

            if (dashTimer <= 0f)
                isDashing = false;
        }

        if (Input.GetKeyDown(KeyCode.LeftShift)
            && !isDashing
            && !isTurningBack
            && cooldownTimer <= 0f
            && moveInput > 0f)
        {
            StartDash();
        }
    }

    private void StartDash()
    {
        dashDirection = transform.forward;

        isDashing = true;
        dashTimer = dashDuration;
        cooldownTimer = dashCooldown;
    }

    private void HandleJumpInput()
    {
        if (jumpCooldownTimer > 0f)
        {
            jumpCooldownTimer = Mathf.Max(
                0f,
                jumpCooldownTimer - Time.deltaTime
            );
        }

        if (Input.GetKeyDown(KeyCode.Space)
            && !isJumping
            && !isTurningBack
            && jumpCooldownTimer <= 0f)
        {
            jumpRequested = true;
        }
    }

    private void StartJump()
    {
        if (!TryGetGroundHeight(rb.position, out float groundY))
            return;

        // Yalnýzca zemine yakýnken zýplanabilir.
        if (Mathf.Abs(rb.position.y - groundY) > 0.2f)
            return;

        isJumping = true;
        jumpTimer = 0f;

        jumpStartY = rb.position.y;
        jumpGroundY = groundY;
        jumpLastHeight = 0f;

        if (enableDebugLogs)
            Debug.Log("Kurbaða zýpladý!", this);
    }

    private bool TryGetGroundHeight(
        Vector3 position,
        out float groundY)
    {
        Vector3 rayOrigin =
            position + Vector3.up * groundCheckHeight;

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            groundCheckDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y + groundOffset;
            return true;
        }

        groundY = position.y;
        return false;
    }

    private void HandleAttack()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        if (attackCamera == null || isTurningBack)
            return;

        Ray ray =
            attackCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        if (!hit.collider.CompareTag("Fly"))
            return;

        Vector3 direction =
            hit.collider.bounds.center - rb.position;

        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            flyTargetRotation =
                Quaternion.LookRotation(direction.normalized);

            isTurningToFly = true;
        }

        if (animator != null)
            animator.SetTrigger(AttackHash);

        if (enableDebugLogs)
        {
            Debug.Log(
                $"Kurbaða sineðe saldýrdý: {hit.collider.name}",
                this
            );
        }
    }


    private Vector3 CheckObstacle(Vector3 movement)
    {
        if (movement.sqrMagnitude < 0.000001f)
            return movement;

        if (frogCollider == null)
            return movement;

        Vector3 center = transform.TransformPoint(
            frogCollider.center
        );

        Vector3 scale = transform.lossyScale;

        float radius = frogCollider.radius *
            Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));

        float height = Mathf.Max(
            frogCollider.height * Mathf.Abs(scale.y),
            radius * 2f
        );

        float halfSegment = height * 0.5f - radius;

        Vector3 bottom =
            center - Vector3.up * halfSegment;

        Vector3 top =
            center + Vector3.up * halfSegment;

        Vector3 direction = movement.normalized;
        float distance = movement.magnitude;

        if (Physics.CapsuleCast(
            bottom,
            top,
            radius,
            direction,
            out RaycastHit hit,
            distance + obstacleSkin,
            obstacleLayer,
            QueryTriggerInteraction.Ignore))
        {
            float allowedDistance = Mathf.Max(
                0f,
                hit.distance - obstacleSkin
            );

            return direction *
                Mathf.Min(distance, allowedDistance);
        }

        return movement;
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        bool isMoving =
            !isTurningBack &&
            (moveInput > 0.01f || isDashing);

        animator.SetBool(IsMovingHash, isMoving);
        animator.SetBool(IsDashingHash, isDashing);
    }

    private void FixedUpdate()
    {
        if (jumpRequested)
        {
            jumpRequested = false;

            if (!isJumping)
                StartJump();
        }

        if (isTurningBack)
        {
            Quaternion targetRotation =
                Quaternion.Euler(0f, targetBackYaw, 0f);

            Quaternion newRotation =
                Quaternion.RotateTowards(
                    rb.rotation,
                    targetRotation,
                    backTurnSpeed * Time.fixedDeltaTime
                );

            rb.MoveRotation(newRotation);

            if (Quaternion.Angle(
                newRotation,
                targetRotation) < 0.5f)
            {
                rb.MoveRotation(targetRotation);
                isTurningBack = false;
            }

            // Dönüþ sýrasýnda da zýplama devam edebilir.
            MoveFrog(Vector3.zero, 0f);
            return;
        }

        if (isTurningToFly)
        {
            if (Mathf.Abs(turnInput) > 0.01f || isDashing)
            {
                isTurningToFly = false;
            }
            else
            {
                Quaternion newRotation =
                    Quaternion.RotateTowards(
                        rb.rotation,
                        flyTargetRotation,
                        attackRotationSpeed * Time.fixedDeltaTime
                    );

                rb.MoveRotation(newRotation);

                if (Quaternion.Angle(
                    newRotation,
                    flyTargetRotation) < 0.5f)
                {
                    isTurningToFly = false;
                }
            }
        }
        else if (!isDashing && Mathf.Abs(turnInput) > 0.01f)
        {
            float rotationAmount =
                turnInput * rotationSpeed * Time.fixedDeltaTime;

            Quaternion targetRotation =
                rb.rotation *
                Quaternion.Euler(0f, rotationAmount, 0f);

            rb.MoveRotation(targetRotation);
        }

        Vector3 direction =
            isDashing
                ? dashDirection
                : transform.forward * moveInput;

        float speed =
            isDashing ? dashSpeed : swimSpeed;

        if (isInMud)
            speed *= mudSpeedMultiplier;

        MoveFrog(direction, speed);
    }

    private void MoveFrog(Vector3 direction, float speed)
    {
        float dt = Time.fixedDeltaTime;

        Vector3 movement = direction * speed * dt;

        // Önümüzde engel varsa hareket mesafesini sýnýrla.
        movement = CheckObstacle(movement);

        Vector3 nextPosition = rb.position + movement;

        if (TryGetGroundHeight(
            nextPosition,
            out float targetGroundY))
        {
            if (isJumping)
            {
                jumpGroundY = targetGroundY;
            }
            else
            {
                nextPosition.y = Mathf.MoveTowards(
                    rb.position.y,
                    targetGroundY,
                    groundFollowSpeed * dt
                );
            }
        }

        if (isJumping)
        {
            jumpTimer += dt;

            float progress = Mathf.Clamp01(
                jumpTimer / Mathf.Max(0.01f, jumpDuration)
            );

            // 0 -> 1 -> 0 þeklinde zýplama yayý.
            float jumpHeightNow =
                Mathf.Sin(progress * Mathf.PI) * jumpHeight;

            // Zýplama yüksekliðini baþlangýç konumuna
            // göre uygula; zemin yükselirse iniþi de takip et.
            float baseY = Mathf.MoveTowards(
                rb.position.y - jumpLastHeight,
                jumpGroundY,
                groundFollowSpeed * dt
            );

            nextPosition.y = baseY + jumpHeightNow;
            jumpLastHeight = jumpHeightNow;

            if (progress >= 1f)
            {
                isJumping = false;
                jumpLastHeight = 0f;
                jumpCooldownTimer = jumpCooldown;

                nextPosition.y = Mathf.MoveTowards(
                    nextPosition.y,
                    jumpGroundY,
                    groundFollowSpeed * dt
                );
            }
        }

        rb.MovePosition(nextPosition);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Mud"))
        {
            isInMud = true;

            if (enableDebugLogs)
            {
                Debug.Log("Kurbaða çamura girdi!", this);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Mud"))
        {
            isInMud = false;

            if (enableDebugLogs)
            {
                Debug.Log("Kurbaða çamurdan çýktý!", this);
            }
        }
    }
}
