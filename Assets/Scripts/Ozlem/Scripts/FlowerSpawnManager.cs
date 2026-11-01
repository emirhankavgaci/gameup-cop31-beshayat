
using System.Collections.Generic;
using UnityEngine;

public class FlowerSpawnManager : MonoBehaviour
{
    [Header("Flower Prefabs")]
    [SerializeField] private List<GameObject> flowerPrefabs;

    [Header("Spawn Areas")]
    [SerializeField] private List<FlowerArea> spawnAreas;

    [Header("Initial Spawn")]
    [SerializeField] private int minFlowerCount = 15;
    [SerializeField] private int maxFlowerCount = 25;

    [Header("Pollination")]
    [SerializeField] private int pollenPerFlower = 2;

    private int collectedPollen;

    private void Start()
    {
        foreach (FlowerArea area in spawnAreas)
        {
            if (area != null)
                area.SetSpawnManager(this);
        }

        int flowerCount = Random.Range(
            minFlowerCount,
            maxFlowerCount + 1);

        for (int i = 0; i < flowerCount; i++)
            SpawnFlower();

        foreach (FlowerArea area in spawnAreas)
        {
            if (area != null)
                area.InitializePollenCapacity();
        }
    }

    public void RegisterPollen(int amount, FlowerArea targetArea)
    {
        if (amount <= 0 || pollenPerFlower <= 0 || targetArea == null)
            return;

        collectedPollen += amount;

        while (collectedPollen >= pollenPerFlower)
        {
            if (!SpawnFlower(targetArea))
                return;

            collectedPollen -= pollenPerFlower;
        }
    }

    private bool SpawnFlower()
    {
        if (spawnAreas == null || spawnAreas.Count == 0)
            return false;

        FlowerArea area = spawnAreas[
            Random.Range(0, spawnAreas.Count)];

        return SpawnFlower(area);
    }

    private bool SpawnFlower(FlowerArea area)
    {
        if (area == null || flowerPrefabs == null)
            return false;

        Collider areaCollider = area.GetComponent<Collider>();

        if (areaCollider == null)
        {
            Debug.LogError($"{area.name}: Collider bulunamad?!");
            return false;
        }

        List<GameObject> validPrefabs = new();

        foreach (GameObject prefab in flowerPrefabs)
        {
            if (prefab != null &&
                prefab.GetComponentInChildren<FlowerNectar>() != null)
            {
                validPrefabs.Add(prefab);
            }
        }

        if (validPrefabs.Count == 0)
        {
            Debug.LogError("FlowerSpawnManager: Geçerli FlowerNectar prefab? bulunamad?!");
            return false;
        }

        GameObject selectedPrefab = validPrefabs[
            Random.Range(0, validPrefabs.Count)];

        Bounds bounds = areaCollider.bounds;

        Vector3 position = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            area.transform.position.y,
            Random.Range(bounds.min.z, bounds.max.z)
        );

        position = areaCollider.ClosestPoint(position);
        position.y = area.transform.position.y;

        GameObject flower = Instantiate(
            selectedPrefab,
            position,
            selectedPrefab.transform.rotation
        );

        FlowerNectar nectar = flower.GetComponentInChildren<FlowerNectar>();

        if (nectar == null)
        {
            Destroy(flower);
            return false;
        }

        area.RegisterFlower(nectar);

        return true;
    }
}
