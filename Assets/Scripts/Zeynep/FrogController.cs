
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FrogController : MonoBehaviour
{
    [Header("Swimming")]
    [SerializeField] private float swimSpeed = 4f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 10f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Mud")]
    [SerializeField] private float mudSpeedMultiplier = 0.4f;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    private Rigidbody rb;

    private Vector3 movement;
    private Vector3 dashDirection;

    private float dashTimer;
    private float cooldownTimer;

    private bool isDashing;
    private bool isInMud;

    private static readonly int IsMovingHash =
        Animator.StringToHash("IsMoving");

    private static readonly int IsDashingHash =
        Animator.StringToHash("IsDashing");

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator == null)
        {
            Debug.LogError(
                "FrogController: Animator bulunamadý!",
                this
            );
            return;
        }

        animator.applyRootMotion = false;

        if (enableDebugLogs)
        {
            Debug.Log(
                $"Animator bulundu: {animator.gameObject.name}",
                this
            );

            Debug.Log(
                $"Animator Controller: " +
                $"{animator.runtimeAnimatorController?.name}",
                this
            );
        }
    }

    private void Update()
    {
        HandleMovementInput();
        HandleDash();
        UpdateAnimator();
    }

    private void HandleMovementInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        movement = new Vector3(
            horizontal,
            0f,
            vertical
        ).normalized;
    }

    private void HandleDash()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;

            if (dashTimer <= 0f)
                isDashing = false;
        }

        if (Input.GetKeyDown(KeyCode.LeftShift)
            && !isDashing
            && cooldownTimer <= 0f
            && movement != Vector3.zero)
        {
            StartDash();
        }
    }

    private void StartDash()
    {
        dashDirection = movement;

        isDashing = true;
        dashTimer = dashDuration;
        cooldownTimer = dashCooldown;
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        bool isMoving =
            movement.sqrMagnitude > 0.01f || isDashing;

        animator.SetBool(IsMovingHash, isMoving);
        animator.SetBool(IsDashingHash, isDashing);

        if (enableDebugLogs &&
            Input.GetKeyDown(KeyCode.W))
        {
            Debug.Log(
                $"Movement: {movement} | " +
                $"Animator: {animator.gameObject.name} | " +
                $"IsMoving: {animator.GetBool(IsMovingHash)}",
                this
            );
        }
    }

    private void FixedUpdate()
    {
        Vector3 direction =
            isDashing ? dashDirection : movement;

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
                Debug.Log("Kurbaða çamura girdi!", this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Mud"))
        {
            isInMud = false;

            if (enableDebugLogs)
                Debug.Log("Kurbaða çamurdan çýktý!", this);
        }
    }
}
