
using UnityEngine;

public class LilyPadFloat : MonoBehaviour
{
    [Header("Water Reference")]
    [SerializeField] private Transform waterTransform;

    [Header("Floating")]
    [SerializeField] private float surfaceOffset = 0.05f;
    [SerializeField] private float followSpeed = 3f;

    [Header("Optional Bobbing")]
    [SerializeField] private bool enableBobbing = false;
    [SerializeField] private float bobHeight = 0.025f;
    [SerializeField] private float bobSpeed = 1.5f;

    private float initialWaterY;
    private float initialPadY;
    private float bobPhase;

    private void Start()
    {
        if (waterTransform == null)
        {
            Debug.LogError(
                "LilyPadFloat: Water Transform atanmadý!",
                this
            );
            enabled = false;
            return;
        }

        initialWaterY = waterTransform.position.y;
        initialPadY = transform.position.y;

        bobPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void LateUpdate()
    {
        float waterChange =
            waterTransform.position.y - initialWaterY;

        float targetY =
            initialPadY + waterChange + surfaceOffset;

        if (enableBobbing)
        {
            targetY += Mathf.Sin(
                Time.time * bobSpeed + bobPhase
            ) * bobHeight;
        }

        Vector3 position = transform.position;

        position.y = Mathf.MoveTowards(
            position.y,
            targetY,
            followSpeed * Time.deltaTime
        );

        transform.position = position;
    }
}
