
using UnityEngine;

public class FrogFoodCollector : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float catchRange = 5f;

    private int foodCount;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0) || playerCamera == null)
            return;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            Fly fly = hit.collider.GetComponentInParent<Fly>();

            if (fly == null)
                return;

            float distance = Vector3.Distance(
                transform.position,
                fly.transform.position
            );

            if (distance <= catchRange)
                fly.Catch(this);
        }
    }

    public void AddFood(int amount)
    {
        foodCount += amount;
        Debug.Log($"Toplanan besin: {foodCount}");
    }
}
