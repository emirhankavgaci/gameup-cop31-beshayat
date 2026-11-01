
using System.Collections.Generic;
using UnityEngine;

public class IslandFlowerSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PondHealthManager pondHealth;
    [SerializeField] private Collider islandCollider;
    [SerializeField] private GameObject[] flowerPrefabs;

    [Header("Spawn")]
    [SerializeField] private int maxFlowers = 25;
    [SerializeField] private int maxAttemptsPerFlower = 30;
    [SerializeField] private float surfaceOffset = 0.02f;
    [SerializeField] private float minSpacing = 0.7f;
    [SerializeField] private float maxSlopeAngle = 35f;

    [Header("Bloom")]
    [SerializeField] private float startBloomHealth = 55f;
    [SerializeField] private float fullBloomHealth = 100f;
    [SerializeField] private float bloomInterval = 0.4f;

    private readonly List<GameObject> flowers = new();
    private readonly List<Vector3> positions = new();

    private int visibleCount;
    private float bloomTimer;

    private void Start()
    {
        if (pondHealth == null ||
            islandCollider == null ||
            flowerPrefabs == null ||
            flowerPrefabs.Length == 0)
        {
            Debug.LogError(
                "IslandFlowerSpawner: References missing!",
                this
            );
            enabled = false;
            return;
        }

        SpawnFlowers();
    }


    private void SpawnFlowers()
    {
        Bounds bounds = islandCollider.bounds;

        int createdCount = 0;

        for (int i = 0; i < maxFlowers; i++)
        {
            for (int attempt = 0;
                 attempt < maxAttemptsPerFlower;
                 attempt++)
            {
                float x = Random.Range(bounds.min.x, bounds.max.x);
                float z = Random.Range(bounds.min.z, bounds.max.z);

                Vector3 origin = new Vector3(
                    x,
                    bounds.max.y + 10f,
                    z
                );

                Ray ray = new Ray(origin, Vector3.down);

                // Yalnýzca Island Collider üzerinde arama yap.
                if (!islandCollider.Raycast(
                    ray,
                    out RaycastHit hit,
                    bounds.size.y + 20f))
                {
                    continue;
                }

                if (Vector3.Angle(hit.normal, Vector3.up)
                    > maxSlopeAngle)
                {
                    continue;
                }

                Vector3 position =
                    hit.point + Vector3.up * surfaceOffset;

                bool tooClose = false;

                foreach (Vector3 existing in positions)
                {
                    Vector2 a = new Vector2(
                        existing.x, existing.z
                    );

                    Vector2 b = new Vector2(
                        position.x, position.z
                    );

                    if (Vector2.Distance(a, b) < minSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                    continue;

                GameObject prefab = flowerPrefabs[
                    Random.Range(0, flowerPrefabs.Length)
                ];

                if (prefab == null)
                    continue;

                GameObject flower = Instantiate(
                    prefab,
                    position,
                    Quaternion.Euler(
                        0f,
                        Random.Range(0f, 360f),
                        0f
                    ),
                    transform
                );

                flower.SetActive(false);

                flowers.Add(flower);
                positions.Add(position);
                createdCount++;

                break;
            }
        }

        Debug.Log(
            $"IslandFlowerSpawner: {createdCount} çiçek oluþturuldu.",
            this
        );
    }


    private void Update()
    {
        if (flowers.Count == 0)
            return;

        float progress = Mathf.InverseLerp(
            startBloomHealth,
            fullBloomHealth,
            pondHealth.CurrentHealth
        );

        int targetCount = Mathf.FloorToInt(
            progress * flowers.Count
        );

        if (visibleCount >= targetCount)
            return;

        bloomTimer += Time.deltaTime;

        if (bloomTimer < bloomInterval)
            return;

        bloomTimer = 0f;

        flowers[visibleCount].SetActive(true);
        visibleCount++;
    }
}
