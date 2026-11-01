
using UnityEngine;

public class CameraFollowTarget : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Movement")]
    [SerializeField] private float smoothTime = 0.2f;

    private float fixedY;
    private Vector3 velocity;

    private void Awake()
    {
        fixedY = transform.position.y;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 targetPosition = new Vector3(
            target.position.x,
            fixedY,
            target.position.z
        );

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref velocity,
            smoothTime
        );
    }
}
