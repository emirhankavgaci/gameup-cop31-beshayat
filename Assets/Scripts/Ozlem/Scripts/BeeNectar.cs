
using UnityEngine;

public class BeeNectar : MonoBehaviour
{
    [SerializeField] private PollenCapacitySO capacitySO;

    [Header("Collection")]
    [SerializeField] private float collectionStartDelay = 2f;
    [SerializeField] private float collectInterval = 0.5f;

    [Header("Pollination")]
    [SerializeField] private float pollinationDelay = 3f;

    [SerializeField] private int currentNectar;
    [SerializeField] private int capacity;

    private float areaEnterTimer;
    private float collectTimer;
    private float pollinationTimer;

    public int CurrentNectar => currentNectar;
    public int Capacity => capacity;

    private void Awake()
    {
        PollenCapacity data = capacitySO.capacities.Find(
            x => x.type == PollenCapacityType.Bee);

        capacity = data != null ? data.capacity : 0;
        currentNectar = 0;
    }

    private void OnTriggerStay(Collider other)
    {
        FlowerArea area = other.GetComponent<FlowerArea>();

        if (area == null)
            return;

        // Kapasite doluysa polen b?rak.
        if (currentNectar >= capacity)
        {
            pollinationTimer += Time.fixedDeltaTime;

            if (pollinationTimer >= pollinationDelay)
            {
                area.RegisterPollen(currentNectar);

                currentNectar = 0;
                pollinationTimer = 0f;
                areaEnterTimer = 0f;
                collectTimer = 0f;
            }

            return;
        }

        pollinationTimer = 0f;

        areaEnterTimer += Time.fixedDeltaTime;

        if (areaEnterTimer < collectionStartDelay)
            return;

        collectTimer += Time.fixedDeltaTime;

        if (collectTimer >= collectInterval)
        {
            if (area.TryCollectNectar())
                currentNectar++;

            collectTimer = 0f;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<FlowerArea>() != null)
        {
            areaEnterTimer = 0f;
            collectTimer = 0f;
            pollinationTimer = 0f;
        }
    }
}
