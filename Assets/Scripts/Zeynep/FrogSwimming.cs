
using UnityEngine;

public class FrogSwimming : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform waterSurface;
    [SerializeField] private Transform frogVisual;

    [Header("Swimming")]
    [SerializeField] private float waterSurfaceOffset = 0.15f;
    [SerializeField] private float enterWaterDepth = 0.25f;
    [SerializeField] private float floatSpeed = 5f;

    [Header("Floating Animation")]
    [SerializeField] private float bobHeight = 0.04f;
    [SerializeField] private float bobSpeed = 2f;

    private FrogController frogController;
    private Vector3 originalVisualLocalPosition;

    private void Awake()
    {
        frogController = GetComponent<FrogController>();

        if (frogVisual != null)
            originalVisualLocalPosition = frogVisual.localPosition;
    }

    private void LateUpdate()
    {
        if (waterSurface == null || frogVisual == null)
            return;

        float waterY = waterSurface.position.y;

        // Kurbaða suya yeterince girdi mi?
        bool isSwimming =
            waterY > transform.position.y - enterWaterDepth;

        if (!isSwimming)
        {
            frogVisual.localPosition = Vector3.Lerp(
                frogVisual.localPosition,
                originalVisualLocalPosition,
                floatSpeed * Time.deltaTime
            );

            return;
        }

        float bob =
            Mathf.Sin(Time.time * bobSpeed) * bobHeight;

        Vector3 visualPosition =
            originalVisualLocalPosition;

        visualPosition.y +=
            (waterY - transform.position.y)
            + waterSurfaceOffset
            + bob;

        frogVisual.localPosition = Vector3.Lerp(
            frogVisual.localPosition,
            visualPosition,
            floatSpeed * Time.deltaTime
        );
    }
}
