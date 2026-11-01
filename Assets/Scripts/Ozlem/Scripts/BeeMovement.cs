
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BeeMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float stoppingDistance = 0.5f;

    [SerializeField] private Camera mainCamera;

    private Rigidbody rb;
    private Vector3 targetPosition;
    private bool hasTarget;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true;

        rb.constraints = RigidbodyConstraints.FreezePositionY |
                         RigidbodyConstraints.FreezeRotation;

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void Update()
    {
        if (mainCamera == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        Plane groundPlane = new Plane(
            Vector3.up,
            new Vector3(0f, transform.position.y, 0f)
        );

        if (groundPlane.Raycast(ray, out float distance))
        {
            targetPosition = ray.GetPoint(distance);
            hasTarget = true;
        }
    }

    private void FixedUpdate()
    {
        if (!hasTarget)
            return;

        Vector3 target = new Vector3(
            targetPosition.x,
            rb.position.y,
            targetPosition.z
        );

        if (Vector3.Distance(rb.position, target) <= stoppingDistance)
            return;

        Vector3 direction = (target - rb.position).normalized;

        Vector3 nextPosition = Vector3.MoveTowards(
            rb.position,
            target - direction * stoppingDistance,
            moveSpeed * Time.fixedDeltaTime
        );

        rb.MovePosition(nextPosition);
    }
}
