
using UnityEngine;

public class PondWaterLevel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PondHealthManager pondHealth;
    [SerializeField] private Transform waterTransform;

    [Header("Water Level")]
    [SerializeField] private float minWaterHeight = -1f;
    [SerializeField] private float maxWaterHeight = 1f;
    [SerializeField] private float waterRiseSpeed = 0.5f;

    private void Start()
    {
        if (pondHealth == null || waterTransform == null)
        {
            Debug.LogError(
                "PondWaterLevel: Referanslar eksik!",
                this
            );

            enabled = false;
            return;
        }

        UpdateWaterLevel(true);
    }

    private void Update()
    {
        UpdateWaterLevel(false);
    }

    private void UpdateWaterLevel(bool instant)
    {
        float health = pondHealth.HealthPercentage;

        float targetHeight = Mathf.Lerp(
            minWaterHeight,
            maxWaterHeight,
            health
        );

        Vector3 position = waterTransform.localPosition;

        position.y = instant
            ? targetHeight
            : Mathf.MoveTowards(
                position.y,
                targetHeight,
                waterRiseSpeed * Time.deltaTime
            );

        waterTransform.localPosition = position;
    }
}
