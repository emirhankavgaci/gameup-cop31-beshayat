
using UnityEngine;

public class PondHealthManager : MonoBehaviour
{
    [Header("Pond Health")]
    [SerializeField, Range(0f, 100f)]
    private float startingHealth = 50f;

    [SerializeField] private float healthDecreasePerSecond = 0.5f;

    private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float HealthPercentage => currentHealth / 100f;

    private void Awake()
    {
        currentHealth = startingHealth;
    }

    private void Update()
    {
        if (currentHealth <= 0f || currentHealth >= 100f)
            return;

        currentHealth -= healthDecreasePerSecond * Time.deltaTime;
        currentHealth = Mathf.Clamp(currentHealth, 0f, 100f);
    }

    public void Heal(float amount)
    {
        if (amount <= 0f)
            return;

        currentHealth = Mathf.Clamp(
            currentHealth + amount,
            0f,
            100f
        );

        Debug.Log($"Gölet saðlýðý: %{currentHealth:F1}");

        if (currentHealth >= 100f)
            Debug.Log("Gölet tamamen iyileþti!");
    }
}
