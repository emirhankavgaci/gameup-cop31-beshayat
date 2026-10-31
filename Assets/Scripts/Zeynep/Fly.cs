using UnityEngine;

public class Fly : MonoBehaviour
{
    [SerializeField] private int foodValue = 1;

    public void Catch(FrogFoodCollector collector)
    {
        collector.AddFood(foodValue);
        Destroy(gameObject);
    }
}
