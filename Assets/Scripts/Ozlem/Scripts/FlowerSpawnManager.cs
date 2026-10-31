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
    [SerializeField] private int pollenPerFlower = 10;

    private int collectedPollen;

    private void Start()
    {
        int flowerCount = Random.Range(
            minFlowerCount,
            maxFlowerCount + 1);

        for (int i = 0; i < flowerCount; i++)
        {
            SpawnFlower();
        }
    }

    public void RegisterPollen(int amount)
    {
        collectedPollen += amount;

        while (collectedPollen >= pollenPerFlower)
        {
            if (!SpawnFlower())
                return;

            collectedPollen -= pollenPerFlower;
        }
    }

    private bool SpawnFlower()
    {
        if (flowerPrefabs.Count == 0 || spawnAreas.Count == 0)
            return false;

        FlowerArea area = spawnAreas[
            Random.Range(0, spawnAreas.Count)];

        GameObject prefab = flowerPrefabs[
            Random.Range(0, flowerPrefabs.Count)];

        if (prefab == null || !prefab.CompareTag("Flower"))
            return false;

        FlowerNectar flowerNectar = prefab.GetComponent<FlowerNectar>();

        if (flowerNectar == null)
            return false;

        Collider areaCollider = area.GetComponent<Collider>();

        if (areaCollider == null)
            return false;

        Bounds bounds = areaCollider.bounds;

        Vector3 position = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            area.transform.position.y,
            Random.Range(bounds.min.z, bounds.max.z)
        );

        position = areaCollider.ClosestPoint(position);
        position.y = area.transform.position.y;

        GameObject flower = Instantiate(
            prefab,
            position,
            prefab.transform.rotation
        );

        area.RegisterFlower(flower.GetComponent<FlowerNectar>());

        return true;
    }
}