
using UnityEngine;

public class SmoothCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Position")]
    [SerializeField] private float distance = 6f;
    [SerializeField] private float height = 3f;
    [SerializeField] private float followSmoothTime = 0.2f;

    [Header("Rotation")]
    [SerializeField] private float rotationSmoothSpeed = 3f;
    [SerializeField] private float rotationDeadZone = 60f;

    private float cameraYaw;
    private Vector3 velocity;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogError(
                "SmoothCameraFollow: Target atanmadý!",
                this
            );
            enabled = false;
            return;
        }

        cameraYaw = transform.eulerAngles.y;
    }

    private void LateUpdate()
    {
        float targetYaw = target.eulerAngles.y;

        float angleDifference = Mathf.DeltaAngle(
            cameraYaw,
            targetYaw
        );

        // Küçük dönüþlerde kamera açýsýný deðiþtirme.
        if (Mathf.Abs(angleDifference) > rotationDeadZone)
        {
            // Kurbaðanýn arkasýna yumuþakça yaklaþ.
            float desiredYaw =
                targetYaw - Mathf.Sign(angleDifference) * rotationDeadZone;

            cameraYaw = Mathf.LerpAngle(
                cameraYaw,
                desiredYaw,
                1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime)
            );
        }

        Quaternion orbitRotation =
            Quaternion.Euler(0f, cameraYaw, 0f);

        Vector3 offset =
            orbitRotation * new Vector3(0f, height, -distance);

        Vector3 desiredPosition = target.position + offset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocity,
            followSmoothTime
        );

        // Kameranýn kurbaðaya bakmasýný saðla.
        Vector3 lookTarget = target.position + Vector3.up * 1f;

        transform.rotation = Quaternion.LookRotation(
            lookTarget - transform.position
        );
    }
}
