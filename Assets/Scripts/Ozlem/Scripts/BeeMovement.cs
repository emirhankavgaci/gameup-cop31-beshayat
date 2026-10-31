using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BeeMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody rb;
    private Vector3 moveDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        rb.constraints = RigidbodyConstraints.FreezePositionY |
                         RigidbodyConstraints.FreezeRotation;
    }

    private void Update()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        moveDirection = new Vector3(x, 0f, z).normalized;
    }

    private void FixedUpdate()
    {
        rb.MovePosition(rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime);
    }
}