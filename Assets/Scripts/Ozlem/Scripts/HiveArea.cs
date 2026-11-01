using UnityEngine;

public class HiveArea : MonoBehaviour
{
    [Header("Capacity")]
    [SerializeField] private PollenCapacitySO capacitySO;
    [SerializeField] private float maxCapacity = 100f;
    [SerializeField] private float currentNectar = 60f;

    [Header("Nectar Consumption")]
    [Tooltip("Her saniye kovandan tüketilen nektar miktarı")]
    [SerializeField] private float drainRatePerSecond = 0.8f;
    [SerializeField] private bool autoDrain = true;

    public float CurrentNectar => currentNectar;
    public float MaxCapacity => maxCapacity;
    public int CurrentPollen => Mathf.RoundToInt(currentNectar);
    public int Capacity => Mathf.RoundToInt(maxCapacity);

    private void Awake()
    {
        if (capacitySO != null && capacitySO.capacities != null)
        {
            PollenCapacity data = capacitySO.capacities.Find(
                x => x.type == PollenCapacityType.Hive
            );

            if (data != null && data.capacity > 0)
            {
                maxCapacity = data.capacity;
            }
        }

        if (maxCapacity <= 0)
            maxCapacity = 100f;

        // Başlangıçta kovan boş olmasın, %60 dolu başlasın
        if (currentNectar <= 0f)
            currentNectar = maxCapacity * 0.6f;
        else
            currentNectar = Mathf.Clamp(currentNectar, 10f, maxCapacity);
    }

    private void Update()
    {
        if (autoDrain && currentNectar > 0f)
        {
            currentNectar -= drainRatePerSecond * Time.deltaTime;
            if (currentNectar < 0f)
                currentNectar = 0f;
        }
    }

    public int StorePollen(int amount)
    {
        return AddNectar(amount);
    }

    public int AddNectar(float amount)
    {
        if (amount <= 0f)
            return 0;

        float available = maxCapacity - currentNectar;
        float accepted = Mathf.Min(amount, available);

        currentNectar += accepted;

        return Mathf.RoundToInt(accepted);
    }

    public void SetAutoDrain(bool enabled)
    {
        autoDrain = enabled;
    }
}