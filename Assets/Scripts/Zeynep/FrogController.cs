
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

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Attack")]
    [SerializeField] private Camera attackCamera;
    [SerializeField] private float attackRotationSpeed = 360f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

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
        HandleAttack();
        UpdateAnimator();
    }

    private void HandleMovementInput()
    {
        float vertical = Input.GetAxisRaw("Vertical");

        // W ile ileri hareket.
        moveInput = Mathf.Max(0f, vertical);

        // A / D ile kendi etrafýnda dönüþ.
        turnInput = Input.GetAxisRaw("Horizontal");

        // S'ye ilk basýldýðýnda 180 derece dönüþ baþlat.
        if (Input.GetKeyDown(KeyCode.S)
            && !isTurningBack
            && !isDashing)
        {
            StartBackTurn();
        }

        // Dönüþ sýrasýnda hareket etme.
        if (isTurningBack)
        {
            moveInput = 0f;
            turnInput = 0f;
        }
        // S basýlý tutuluyorsa dönüþten sonra ileri git.
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

    private void HandleAttack()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        if (attackCamera == null || isTurningBack)
            return;

        Ray ray = attackCamera.ScreenPointToRay(Input.mousePosition);

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
        // S ile 180 derece dönüþ.
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

            if (Quaternion.Angle(newRotation, flyTargetRotation) < 0.5f)
            {
                isTurningToFly = false;
            }
        }

        // Sineðe yumuþak dönüþ.
        if (isTurningToFly)
        {
            // A/D veya Dash kullanýlýrsa sineðe dönüþü iptal et.
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

                if (Quaternion.Angle(newRotation, flyTargetRotation) < 0.5f)
                {
                    isTurningToFly = false;
                }
            }
        }
        // A / D ile kendi etrafýnda dön.
        else if (!isDashing && Mathf.Abs(turnInput) > 0.01f)
        {
            float rotationAmount =
                turnInput * rotationSpeed * Time.fixedDeltaTime;

            Quaternion targetRotation =
                rb.rotation *
                Quaternion.Euler(0f, rotationAmount, 0f);

            rb.MoveRotation(targetRotation);
        }

        // W veya S ile baktýðý yöne ilerle.
        Vector3 direction =
            isDashing
                ? dashDirection
                : transform.forward * moveInput;

        float speed =
            isDashing ? dashSpeed : swimSpeed;

        if (isInMud)
            speed *= mudSpeedMultiplier;

        rb.MovePosition(
            rb.position +
            direction * speed * Time.fixedDeltaTime
        );
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Mud"))
        {
            isInMud = true;

            if (enableDebugLogs)
            {
                Debug.Log(
                    "Kurbaða çamura girdi!",
                    this
                );
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
                Debug.Log(
                    "Kurbaða çamurdan çýktý!",
                    this
                );
            }
        }
    }
}
