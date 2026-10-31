
using System.Collections;
using UnityEngine;

public class FriendFrogMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float wanderRadius = 2f;
    [SerializeField] private float jumpDistance = 0.8f;
    [SerializeField] private float jumpHeight = 0.35f;
    [SerializeField] private float jumpDuration = 0.6f;

    [Header("Waiting")]
    [SerializeField] private float minWaitTime = 2f;
    [SerializeField] private float maxWaitTime = 5f;

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
            animator.applyRootMotion = false;
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
                yield break;

            if (animator != null)
                animator.SetBool(IsMovingHash, false);

            float waitTime = Random.Range(
                minWaitTime,
                maxWaitTime
            );

            if (friendFrog != null && friendFrog.IsFed)
                waitTime *= 0.5f;

            yield return new WaitForSeconds(waitTime);

            yield return StartCoroutine(Jump());
        }
    }

    private IEnumerator Jump()
    {
        Vector2 randomDirection = Random.insideUnitCircle;

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
                Vector3.ClampMagnitude(fromOrigin, wanderRadius);

            target = new Vector3(
                startingPosition.x + limitedOffset.x,
                start.y,
                startingPosition.z + limitedOffset.z
            );
        }

        Vector3 direction = target - start;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);

        // Zýplama animasyonunu baþlat.
        if (animator != null)
            animator.SetBool(IsMovingHash, true);

        float elapsed = 0f;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / jumpDuration
            );

            Vector3 position = Vector3.Lerp(
                start,
                target,
                progress
            );

            position.y += Mathf.Sin(progress * Mathf.PI)
                          * jumpHeight;

            transform.position = position;

            yield return null;
        }

        transform.position = target;

        // Zýplama bitince bekleme animasyonuna dön.
        if (animator != null)
            animator.SetBool(IsMovingHash, false);
    }
}
