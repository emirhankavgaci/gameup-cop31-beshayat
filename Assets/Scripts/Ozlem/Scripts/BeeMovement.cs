using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BeeMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float sprintMultiplier = 1.8f;

    [Header("Rotation")]
    [Tooltip("Arinin hareket yonune dogru yumusak donus hizi.")]
    [SerializeField] private float rotationSpeed = 12f;

    private Rigidbody rb;
    private Vector3 moveDirection;
    private Quaternion targetRotation;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true;

        // Y rotasyonuna izin ver, X ve Z rotasyonlarini ve Y pozisyonunu kilitle
        rb.constraints = RigidbodyConstraints.FreezePositionY |
                         RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ;

        targetRotation = transform.rotation;
    }

    private void Update()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 inputDir = new Vector3(x, 0f, z);
        if (inputDir.sqrMagnitude > 0.001f)
        {
            moveDirection = inputDir.normalized;
            targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        }
        else
        {
            moveDirection = Vector3.zero;
        }
    }

    private void FixedUpdate()
    {
        float currentSpeed = moveSpeed;

        if (Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift))
        {
            currentSpeed *= sprintMultiplier;
        }

        Vector3 nextPosition = rb.position +
            moveDirection * currentSpeed * Time.fixedDeltaTime;

        rb.MovePosition(nextPosition);

        // Arinin baktigi yonu hareket yonune dogru yumusak (smooth) sekilde dondur
        Quaternion nextRotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(nextRotation);
    }
}
