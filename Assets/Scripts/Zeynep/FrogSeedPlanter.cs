
using UnityEngine;

public class FrogSeedPlanter : MonoBehaviour
{
    [Header("Planting")]
    [SerializeField] private float plantingRange = 3f;

    [Header("UI")]
    [SerializeField] private bool showPlantingHint = true;

    private PlantingSpot nearestSpot;

    private void Update()
    {
        nearestSpot = FindNearestSpot();

        if (!Input.GetMouseButtonDown(1))
            return;

        if (nearestSpot == null)
        {
            Debug.Log("Yakýnda uygun ekim noktasý yok.");
            return;
        }

        if (nearestSpot.Plant())
            nearestSpot = null;
    }

    private PlantingSpot FindNearestSpot()
    {
        PlantingSpot[] spots =
            FindObjectsByType<PlantingSpot>(
                FindObjectsSortMode.None
            );

        PlantingSpot nearest = null;
        float nearestDistance = plantingRange;

        foreach (PlantingSpot spot in spots)
        {
            if (spot.IsPlanted)
                continue;

            float distance = Vector3.Distance(
                transform.position,
                spot.transform.position
            );

            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearest = spot;
            }
        }

        return nearest;
    }

    private void OnGUI()
    {
        if (!showPlantingHint || nearestSpot == null)
            return;

        GUIStyle style = new GUIStyle(GUI.skin.box);

        style.fontSize = 20;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;

        float width = 260f;
        float height = 45f;

        Rect rect = new Rect(
            (Screen.width - width) / 2f,
            Screen.height - 110f,
            width,
            height
        );

        GUI.Box(rect, "Sað Týk - Tohum Ek", style);
    }
}
