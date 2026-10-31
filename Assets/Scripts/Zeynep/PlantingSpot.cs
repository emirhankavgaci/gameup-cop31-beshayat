
using UnityEngine;

public class PlantingSpot : MonoBehaviour
{
    [Header("Planting")]
    [SerializeField] private float healAmount = 15f;

    private bool isPlanted;

    public bool IsPlanted => isPlanted;

    public bool Plant()
    {
        if (isPlanted)
            return false;

        isPlanted = true;

        PondHealthManager pond =
            FindFirstObjectByType<PondHealthManager>();

        if (pond != null)
            pond.Heal(healAmount);

        Debug.Log($"Tohum ekildi: {gameObject.name}");

        return true;
    }
}
