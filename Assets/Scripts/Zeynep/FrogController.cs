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

    private bool isInMud;

    private Rigidbody rb;
    private Vector3 movement;
    private Vector3 dashDirection;

    private float dashTimer;
    private float cooldownTimer;
    private bool isDashing;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        movement = new Vector3(horizontal, 0f, vertical).normalized;

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
            dashDirection = movement;
            isDashing = true;
            dashTimer = dashDuration;
            cooldownTimer = dashCooldown;
        }
    }

    private void FixedUpdate()
    {
        Vector3 direction = isDashing ? dashDirection : movement;
        float speed = isDashing ? dashSpeed : swimSpeed;

        if (isInMud)
            speed *= mudSpeedMultiplier;

        rb.MovePosition(
            rb.position + direction * speed * Time.fixedDeltaTime
        );

        if (direction != Vector3.zero)
        {
            //transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Mud"))
        {
            isInMud = true;
            Debug.Log("Kurbaða çamura girdi!");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Mud"))
        {
            isInMud = false;
            Debug.Log("Kurbaða çamurdan çýktý!");
        }
    }
}
