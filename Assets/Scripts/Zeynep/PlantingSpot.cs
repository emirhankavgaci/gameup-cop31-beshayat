
using UnityEngine;

public class PlantingSpot : MonoBehaviour
{
    [Header("Planting")]
    [SerializeField] private float healAmount = 15f;

    [Header("Flower")]
    [SerializeField] private GameObject flower;

    private bool isPlanted;

    public bool IsPlanted => isPlanted;

    private void Awake()
    {
        // Oyun baþladýðýnda çiçek görünmesin.
        if (flower != null)
            flower.SetActive(false);
    }

    public bool Plant()
    {
        if (isPlanted)
            return false;

        isPlanted = true;

        // Çiçeði görünür yap.
        if (flower != null)
        {
            flower.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                $"PlantingSpot: {name} için Flower atanmadý!",
                this
            );
        }

        // Gölet saðlýðýný artýr.
        PondHealthManager pond =
            FindFirstObjectByType<PondHealthManager>();

        if (pond != null)
            pond.Heal(healAmount);

        Debug.Log($"Çiçek ekildi: {gameObject.name}");

        return true;
    }
}
