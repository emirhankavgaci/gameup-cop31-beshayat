
using UnityEngine;

public class HiveArea : MonoBehaviour
{
    [SerializeField] private PollenCapacitySO capacitySO;
    [SerializeField] private int currentPollen;
    [SerializeField] private int capacity;

    public int CurrentPollen => currentPollen;
    public int Capacity => capacity;

    private void Awake()
    {
        PollenCapacity data = capacitySO.capacities.Find(
            x => x.type == PollenCapacityType.Hive
        );

        capacity = data != null ? data.capacity : 0;
        currentPollen = 0;
    }

    public int StorePollen(int amount)
    {
        int availableSpace = capacity - currentPollen;
        int acceptedAmount = Mathf.Min(amount, availableSpace);

        currentPollen += acceptedAmount;

        return acceptedAmount;
    }
}
