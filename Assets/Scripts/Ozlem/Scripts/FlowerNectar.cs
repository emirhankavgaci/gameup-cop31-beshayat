using UnityEngine;

public class FlowerNectar : MonoBehaviour
{
    [SerializeField] private PollenCapacitySO capacitySO;
    [SerializeField] private FlowerDeathAnimation deathAnimation;

    [SerializeField] private int currentNectar;
    [SerializeField] private int capacity;

    public int CurrentNectar => currentNectar;
    public int Capacity => capacity;

    private void Awake()
    {
        InitializeCapacity();
    }

    public void Initialize(PollenCapacitySO so, FlowerDeathAnimation deathAnim = null)
    {
        if (so != null)
            capacitySO = so;

        if (deathAnim != null)
            deathAnimation = deathAnim;

        InitializeCapacity();
    }

    public void SetDeathAnimation(FlowerDeathAnimation deathAnim)
    {
        deathAnimation = deathAnim;
    }

    private void InitializeCapacity()
    {
        if (capacitySO != null && capacitySO.capacities != null)
        {
            PollenCapacity data = capacitySO.capacities.Find(
                x => x.type == PollenCapacityType.Flower);

            capacity = data != null ? data.capacity : 10;
        }
        else
        {
            if (capacity <= 0)
                capacity = 10;
        }

        currentNectar = capacity;
    }

    public bool TryTakeNectar()
    {
        if (currentNectar <= 0)
            return false;

        currentNectar--;

        if (currentNectar <= 0)
        {
            if (deathAnimation != null)
                deathAnimation.PlayDeath();
            else
                Destroy(gameObject);
        }

        return true;
    }
}
