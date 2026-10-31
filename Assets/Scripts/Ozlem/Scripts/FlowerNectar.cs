using UnityEngine;

public class FlowerNectar : MonoBehaviour
{
    [SerializeField] private PollenCapacitySO capacitySO;

    [SerializeField] private int currentNectar;
    [SerializeField] private int capacity;

    public int CurrentNectar => currentNectar;
    public int Capacity => capacity;

    private void Awake()
    {
        PollenCapacity data = capacitySO.capacities.Find(
            x => x.type == PollenCapacityType.Flower);

        capacity = data != null ? data.capacity : 0;
        currentNectar = capacity;
    }

    public bool TryTakeNectar()
    {
        if (currentNectar <= 0)
            return false;

        currentNectar--;

        if (currentNectar <= 0)
            Destroy(gameObject);

        return true;
    }
}