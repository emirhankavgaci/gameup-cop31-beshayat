
using UnityEngine;

public class FriendGroundFollower : MonoBehaviour
{
    [Header("Ground")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float rayHeight = 5f;
    [SerializeField] private float rayDistance = 15f;

    [Header("Frog Dimensions")]
    [SerializeField] private float halfWidth = 0.3f;
    [SerializeField] private float halfLength = 0.4f;
    [SerializeField] private float groundOffset = 0.15f;

    [Header("Settings")]
    [SerializeField] private bool alignToSlope = true;

    private void LateUpdate()
    {
        Vector3 center = transform.position;

        Vector3 right = transform.right * halfWidth;
        Vector3 forward = transform.forward * halfLength;

        Vector3[] points =
        {
            center,
            center + forward + right,
            center + forward - right,
            center - forward + right,
            center - forward - right
        };

        float highestY = float.NegativeInfinity;
        Vector3 normalSum = Vector3.zero;
        int hitCount = 0;

        foreach (Vector3 point in points)
        {
            Vector3 origin = point + Vector3.up * rayHeight;

            if (Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                rayDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore))
            {
                highestY = Mathf.Max(highestY, hit.point.y);
                normalSum += hit.normal;
                hitCount++;
            }
        }

        if (hitCount == 0)
        {
            Debug.LogWarning(
                "FriendFrog: Ground bulunamadý!",
                this
            );
            return;
        }

        Vector3 position = transform.position;
        position.y = highestY + groundOffset;
        transform.position = position;

        if (alignToSlope)
        {
            Vector3 normal = (normalSum / hitCount).normalized;
            Vector3 forwardOnSlope =
                Vector3.ProjectOnPlane(transform.forward, normal);

            if (forwardOnSlope.sqrMagnitude > 0.001f)
            {
                transform.rotation =
                    Quaternion.LookRotation(forwardOnSlope, normal);
            }
        }
    }
}
