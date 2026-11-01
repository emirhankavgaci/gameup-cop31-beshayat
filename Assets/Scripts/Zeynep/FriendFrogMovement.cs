
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
        startingPosition = transform.position;

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
        StartCoroutine(WanderRoutine());
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

            target = new Vector3(
                startingPosition.x + limitedOffset.x,
                start.y,
                startingPosition.z + limitedOffset.z
            );
        }

        Vector3 direction = target - start;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            yield break;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction.normalized);

        // Zýplama boyunca run animasyonu oynar.
        SetMoving(true);

        float elapsed = 0f;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / jumpDuration
            );

            // Yatay hareket
            Vector3 position = Vector3.Lerp(
                start,
                target,
                progress
            );

            // Yay þeklinde zýplama
            position.y += Mathf.Sin(progress * Mathf.PI)
                          * jumpHeight;

            transform.position = position;

            // Hareket yönüne yumuþak dönüþ
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );

            yield return null;
        }

        transform.position = target;

        // Yere inince stand animasyonuna döner.
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
