using System.Collections.Generic;
using UnityEngine;

public class FlowerArea : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Renderer areaRenderer;
    [SerializeField] private Color lowColor = Color.red;
    [SerializeField] private Color mediumColor = Color.yellow;
    [SerializeField] private Color highColor = Color.green;

    [Header("Color Thresholds")]
    [SerializeField, Range(0f, 1f)] private float lowThreshold = 0.3f;
    [SerializeField, Range(0f, 1f)] private float mediumThreshold = 0.7f;

    private FlowerSpawnManager spawnManager;
    private readonly List<FlowerNectar> flowers = new();

    private int initialPollenCapacity;
    private MaterialPropertyBlock propertyBlock;
    private int colorPropertyId;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        if (areaRenderer != null && areaRenderer.sharedMaterial != null)
        {
            colorPropertyId = areaRenderer.sharedMaterial.HasProperty("_BaseColor")
                ? Shader.PropertyToID("_BaseColor")
                : Shader.PropertyToID("_Color");
        }
    }

    public void SetSpawnManager(FlowerSpawnManager manager)
    {
        spawnManager = manager;
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterFlower(other.GetComponentInParent<FlowerNectar>());
    }

    private void OnTriggerExit(Collider other)
    {
        FlowerNectar flower = other.GetComponentInParent<FlowerNectar>();

        if (flower != null)
            flowers.Remove(flower);
    }

    public void RegisterFlower(FlowerNectar flower)
    {
        if (flower == null || flowers.Contains(flower))
            return;

        flowers.Add(flower);
        UpdateAreaColor();
    }

    public void InitializePollenCapacity()
    {
        initialPollenCapacity = 0;

        foreach (FlowerNectar flower in flowers)
        {
            if (flower != null)
                initialPollenCapacity += flower.Capacity;
        }

        if (initialPollenCapacity <= 0)
            initialPollenCapacity = 1;

        UpdateAreaColor();
    }

    public bool TryCollectNectar(out Vector3 flowerPosition)
    {
        flowerPosition = Vector3.zero;

        for (int i = 0; i < flowers.Count; i++)
        {
            if (flowers[i] == null)
            {
                flowers.RemoveAt(i);
                i--;
                continue;
            }

            FlowerNectar flower = flowers[i];
            Vector3 position = flower.transform.position;

            if (flower.TryTakeNectar())
            {
                flowerPosition = position;
                UpdateAreaColor();
                return true;
            }
        }

        return false;
    }

    public void RegisterPollen(int amount)
    {
        if (spawnManager == null)
        {
            Debug.LogError($"{name}: FlowerSpawnManager bağlantısı yok!");
            return;
        }

        spawnManager.RegisterPollen(amount, this);
    }

    private void UpdateAreaColor()
    {
        if (areaRenderer == null || initialPollenCapacity <= 0)
            return;

        int currentPollen = 0;

        foreach (FlowerNectar flower in flowers)
        {
            if (flower != null)
                currentPollen += flower.CurrentNectar;
        }

        float ratio = Mathf.Clamp01(
            (float)currentPollen / initialPollenCapacity
        );

        Color targetColor;

        if (ratio <= lowThreshold)
            targetColor = lowColor;
        else if (ratio <= mediumThreshold)
            targetColor = mediumColor;
        else
            targetColor = highColor;

        areaRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(colorPropertyId, targetColor);
        areaRenderer.SetPropertyBlock(propertyBlock);
    }
}
