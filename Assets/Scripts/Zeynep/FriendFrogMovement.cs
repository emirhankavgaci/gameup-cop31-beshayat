
using System.Collections;
using UnityEngine;

public class FriendFrogMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float wanderRadius = 2f;
    [SerializeField] private float jumpDistance = 0.8f;
    [SerializeField] private float jumpHeight = 0.25f;
    [SerializeField] private float jumpDuration = 0.6f;
    [SerializeField] private float rotationSpeed = 360f;

    [Header("Ground Following")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float rayStartHeight = 5f;
    [SerializeField] private float rayDistance = 15f;
    [SerializeField] private float groundOffset = 0.15f;
    [SerializeField] private bool alignToSlope = true;
    [SerializeField] private float slopeRotationSpeed = 8f;

    [Header("Waiting")]
    [SerializeField] private float minWaitTime = 2f;
    [SerializeField] private float maxWaitTime = 5f;
    [SerializeField] private float fedWaitMultiplier = 0.5f;

    [Header("References")]
    [SerializeField] private FriendFrog friendFrog;
    [SerializeField] private Animator animator;

    private Vector3 startingPosition;

    private static readonly int IsMovingHash =
        Animator.StringToHash("IsMoving");

    private void Awake()
    {
        if (friendFrog == null)
            friendFrog = GetComponent<FriendFrog>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.SetBool(IsMovingHash, false);
        }
    }

    private void Start()
    {
        // Baþlangýçta kurbaðayý zemine oturt.
        SnapToGround();

        startingPosition = transform.position;

        StartCoroutine(WanderRoutine());
    }

    private bool TryGetGround(
        Vector3 position,
        out RaycastHit hit)
    {
        Vector3 origin =
            position + Vector3.up * rayStartHeight;

        return Physics.Raycast(
            origin,
            Vector3.down,
            out hit,
            rayDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );
    }

    private void SnapToGround()
    {
        if (!TryGetGround(transform.position, out RaycastHit hit))
        {
            Debug.LogWarning(
                "FriendFrog: Zemin bulunamadý!",
                this
            );
            return;
        }

        Vector3 position = transform.position;
        position.y = hit.point.y + groundOffset;
        transform.position = position;

        if (alignToSlope)
        {
            Vector3 forward = Vector3.ProjectOnPlane(
                transform.forward,
                hit.normal
            );

            if (forward.sqrMagnitude > 0.001f)
            {
                transform.rotation =
                    Quaternion.LookRotation(forward, hit.normal);
            }
        }
    }

    private IEnumerator WanderRoutine()
    {
        while (true)
        {
            if (friendFrog != null && !friendFrog.IsAlive)
            {
                SetMoving(false);
                yield break;
            }

            SetMoving(false);

            float waitTime = Random.Range(
                minWaitTime,
                maxWaitTime
            );

            if (friendFrog != null && friendFrog.IsFed)
                waitTime *= fedWaitMultiplier;

            yield return new WaitForSeconds(waitTime);

            if (friendFrog != null && !friendFrog.IsAlive)
                continue;

            yield return StartCoroutine(Jump());
        }
    }

    private IEnumerator Jump()
    {
        Vector2 randomDirection = Random.insideUnitCircle;

        if (randomDirection.sqrMagnitude < 0.001f)
            randomDirection = Vector2.up;

        Vector3 offset = new Vector3(
            randomDirection.x,
            0f,
            randomDirection.y
        ).normalized * jumpDistance;

        Vector3 start = transform.position;
        Vector3 target = start + offset;

        Vector3 fromOrigin = target - startingPosition;
        fromOrigin.y = 0f;

        if (fromOrigin.magnitude > wanderRadius)
        {
            Vector3 limitedOffset =
                Vector3.ClampMagnitude(
                    fromOrigin,
                    wanderRadius
                );

            target.x = startingPosition.x + limitedOffset.x;
            target.z = startingPosition.z + limitedOffset.z;
        }

        Vector3 direction = target - start;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            yield break;

        // Hedefte zemin yoksa zýplama.
        if (!TryGetGround(target, out RaycastHit targetHit))
            yield break;

        target.y = targetHit.point.y + groundOffset;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction.normalized);

        SetMoving(true);

        float elapsed = 0f;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / Mathf.Max(0.01f, jumpDuration)
            );

            // Yatay konumu hesapla.
            Vector3 position = Vector3.Lerp(
                start,
                target,
                progress
            );

            // Her karede bulunduðu yerdeki zemini ölç.
            if (TryGetGround(position, out RaycastHit groundHit))
            {
                position.y =
                    groundHit.point.y + groundOffset;
            }
            else
            {
                position.y = Mathf.Lerp(
                    start.y,
                    target.y,
                    progress
                );
            }

            // Zeminden yukarý doðru zýplama yayý.
            position.y +=
                Mathf.Sin(progress * Mathf.PI) * jumpHeight;

            transform.position = position;

            // Hareket yönüne dön.
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );

            yield return null;
        }

        // Hedefe tam olarak yerleþ.
        transform.position = target;

        // Zeminin eðimine uyum saðla.
        if (alignToSlope)
        {
            Vector3 forward = Vector3.ProjectOnPlane(
                transform.forward,
                targetHit.normal
            );

            if (forward.sqrMagnitude > 0.001f)
            {
                Quaternion slopeRotation =
                    Quaternion.LookRotation(
                        forward,
                        targetHit.normal
                    );

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    slopeRotation,
                    Mathf.Clamp01(
                        slopeRotationSpeed * Time.deltaTime
                    )
                );
            }
        }

        SetMoving(false);
    }

    private void SetMoving(bool isMoving)
    {
        if (animator != null)
            animator.SetBool(IsMovingHash, isMoving);
    }

    private void OnDisable()
    {
        if (animator != null)
            SetMoving(false);
    }
}
