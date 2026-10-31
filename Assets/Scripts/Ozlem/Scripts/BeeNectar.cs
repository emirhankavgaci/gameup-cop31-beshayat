using UnityEngine;

public class BeeNectar : MonoBehaviour
{
    [SerializeField] private PollenCapacitySO capacitySO;
    [SerializeField] private float collectionStartDelay = 2f;
    [SerializeField] private float collectInterval = 0.5f;

    [SerializeField] private int currentNectar;
    [SerializeField] private int capacity;

    private float areaEnterTimer;
    private float collectTimer;

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
        if (currentNectar >= capacity)
            return;

        FlowerArea area = other.GetComponent<FlowerArea>();

        if (area == null)
            return;

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
        }
    }
}