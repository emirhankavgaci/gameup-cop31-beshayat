
using UnityEngine;

public class FlyMovement : MonoBehaviour
{
    [Header("Floating")]
    [SerializeField] private float floatHeight = 0.15f;
    [SerializeField] private float floatSpeed = 3f;

    [Header("Wandering")]
    [SerializeField] private float wanderRadius = 3f;
    [SerializeField] private float moveSpeed = 0.8f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float arrivalDistance = 0.15f;

    [Header("Timing")]
    [SerializeField] private float minWaitTime = 0.5f;
    [SerializeField] private float maxWaitTime = 1.5f;

    private Vector3 startPosition;
    private Vector3 targetPosition;

    private float baseHeight;
    private float floatOffset;
    private float waitTimer;

    private void Start()
    {
        startPosition = transform.position;
        baseHeight = transform.position.y;

        floatOffset = Random.Range(0f, Mathf.PI * 2f);

        ChooseNewTarget();
    }

    private void Update()
    {
        MoveAround();
        ApplyFloating();
    }

    private void MoveAround()
    {
        Vector3 current = transform.position;
        current.y = 0f;

        Vector3 target = targetPosition;
        target.y = 0f;

        float distance = Vector3.Distance(current, target);

        if (distance <= arrivalDistance)
        {
            waitTimer -= Time.deltaTime;

            if (waitTimer <= 0f)
                ChooseNewTarget();

            return;
        }

        Vector3 direction = (target - current).normalized;

        Vector3 nextPosition = Vector3.MoveTowards(
            current,
            target,
            moveSpeed * Time.deltaTime
        );

        transform.position = new Vector3(
            nextPosition.x,
            transform.position.y,
            nextPosition.z
        );

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }

    private void ApplyFloating()
    {
        float yOffset = Mathf.Sin(
            Time.time * floatSpeed + floatOffset
        ) * floatHeight;

        Vector3 position = transform.position;
        position.y = baseHeight + yOffset;

        transform.position = position;
    }

    private void ChooseNewTarget()
    {
        Vector2 randomPoint =
            Random.insideUnitCircle * wanderRadius;

        targetPosition = startPosition + new Vector3(
            randomPoint.x,
            0f,
            randomPoint.y
        );

        waitTimer = Random.Range(minWaitTime, maxWaitTime);
    }
}
