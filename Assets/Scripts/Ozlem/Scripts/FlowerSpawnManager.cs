using System.Collections.Generic;
using UnityEngine;

public class FlowerSpawnManager : MonoBehaviour
{
    [Header("Flower Prefabs")]
    [SerializeField] private List<GameObject> flowerPrefabs = new();

    [Header("Spawn Areas")]
    [SerializeField] private List<FlowerArea> spawnAreas = new();

    [Header("Flower Configuration")]
    [SerializeField] private PollenCapacitySO capacitySO;

    [Header("Initial Spawn")]
    [SerializeField] private int minFlowerCount = 3;
    [SerializeField] private int maxFlowerCount = 10;
    [Tooltip("If true, flowers are spawned for each area. If false, flower count is distributed across areas.")]
    [SerializeField] private bool spawnPerArea = true;

    [Header("Pollination")]
    [SerializeField] private int pollenPerFlower = 2;

    private int collectedPollen;

    private void Awake()
    {
#if UNITY_EDITOR
        if (flowerPrefabs == null || flowerPrefabs.Count == 0)
        {
            LoadFlowersFromOzlemFolder();
        }

        if (capacitySO == null)
        {
            capacitySO = UnityEditor.AssetDatabase.LoadAssetAtPath<PollenCapacitySO>("Assets/Scripts/Ozlem/SO/Capacity.asset");
            if (capacitySO != null)
                UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    private void Start()
    {
        foreach (FlowerArea area in spawnAreas)
        {
            if (area != null)
                area.SetSpawnManager(this);
        }

        if (spawnPerArea)
        {
            foreach (FlowerArea area in spawnAreas)
            {
                if (area != null)
                {
                    int flowerCount = Random.Range(minFlowerCount, maxFlowerCount + 1);
                    for (int i = 0; i < flowerCount; i++)
                        SpawnFlower(area);
                }
            }
        }
        else
        {
            int flowerCount = Random.Range(minFlowerCount, maxFlowerCount + 1);
            for (int i = 0; i < flowerCount; i++)
            {
                if (spawnAreas.Count > 0)
                {
                    FlowerArea area = spawnAreas[i % spawnAreas.Count];
                    SpawnFlower(area);
                }
            }
        }

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

    public bool SpawnFlower()
    {
        if (spawnAreas == null || spawnAreas.Count == 0)
            return false;

        FlowerArea area = spawnAreas[Random.Range(0, spawnAreas.Count)];
        return SpawnFlower(area);
    }

    public bool SpawnFlower(FlowerArea area)
    {
        if (area == null)
            return false;

        Collider areaCollider = area.GetComponent<Collider>();
        if (areaCollider == null)
        {
            Debug.LogError($"{area.name}: Collider bulunamadi!");
            return false;
        }

        List<GameObject> validPrefabs = new();
        if (flowerPrefabs != null)
        {
            foreach (GameObject prefab in flowerPrefabs)
            {
                if (prefab != null)
                    validPrefabs.Add(prefab);
            }
        }

        if (validPrefabs.Count == 0)
        {
            Debug.LogError("FlowerSpawnManager: Gecerli cicek prefabi bulunamadi!");
            return false;
        }

        GameObject selectedPrefab = validPrefabs[Random.Range(0, validPrefabs.Count)];

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

        // Flower tag
        if (!flower.CompareTag("Flower"))
        {
            try { flower.tag = "Flower"; } catch { }
        }

        // Collider
        Collider flowerCol = flower.GetComponentInChildren<Collider>();
        if (flowerCol == null)
        {
            SphereCollider sphereCol = flower.AddComponent<SphereCollider>();
            sphereCol.isTrigger = true;
            sphereCol.radius = 0.5f;
            sphereCol.center = new Vector3(0, 0.25f, 0);
        }

        // Death Animation
        FlowerDeathAnimation deathAnim = flower.GetComponentInChildren<FlowerDeathAnimation>();
        if (deathAnim == null)
        {
            deathAnim = flower.AddComponent<FlowerDeathAnimation>();
        }

        // FlowerNectar
        FlowerNectar nectar = flower.GetComponentInChildren<FlowerNectar>();
        if (nectar == null)
        {
            nectar = flower.AddComponent<FlowerNectar>();
        }

        if (capacitySO != null)
        {
            nectar.Initialize(capacitySO, deathAnim);
        }
        else if (deathAnim != null)
        {
            nectar.SetDeathAnimation(deathAnim);
        }

        area.RegisterFlower(nectar);

        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (flowerPrefabs == null || flowerPrefabs.Count == 0)
        {
            LoadFlowersFromOzlemFolder();
        }

        if (capacitySO == null)
        {
            capacitySO = UnityEditor.AssetDatabase.LoadAssetAtPath<PollenCapacitySO>("Assets/Scripts/Ozlem/SO/Capacity.asset");
        }
    }

    [ContextMenu("Load Flowers From Models/Ozlem")]
    public void LoadFlowersFromOzlemFolder()
    {
        flowerPrefabs = new List<GameObject>();
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Models/Ozlem" });
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null)
                flowerPrefabs.Add(go);
        }
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"FlowerSpawnManager: {flowerPrefabs.Count} flower prefabs loaded from Assets/Models/Ozlem.");
    }
#endif
}