
using UnityEngine;

public class FrogObjectiveUI : MonoBehaviour
{
    [Header("Requirements")]
    [SerializeField] private int requiredFrogs = 3;
    [SerializeField] private int requiredSeeds = 3;

    [Header("UI")]
    [SerializeField] private bool showObjectives = true;

    private FriendFrog[] friends;
    private PlantingSpot[] spots;

    private GUIStyle titleStyle;
    private GUIStyle textStyle;

    private void Start()
    {
        friends = FindObjectsByType<FriendFrog>(
            FindObjectsSortMode.None);

        spots = FindObjectsByType<PlantingSpot>(
            FindObjectsSortMode.None);
    }

    private void OnGUI()
    {
        if (!showObjectives)
            return;

        int fedCount = 0;

        foreach (FriendFrog friend in friends)
        {
            if (friend != null && friend.IsFed)
                fedCount++;
        }

        int plantedCount = 0;

        foreach (PlantingSpot spot in spots)
        {
            if (spot != null && spot.IsPlanted)
                plantedCount++;
        }

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 20;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;

            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.fontSize = 16;
            textStyle.normal.textColor = Color.white;
        }

        float margin = 20f;
        float panelWidth = Mathf.Min(340f, Screen.width - 2f * margin);
        float panelHeight = 120f;

        Rect panel = new Rect(
            Screen.width - panelWidth - margin,
            margin,
            panelWidth,
            panelHeight
        );

        GUI.Box(panel, "");

        float textX = panel.x + 15f;
        float textWidth = panelWidth - 30f;

        GUI.Label(
            new Rect(textX, panel.y + 10f, textWidth, 30f),
            "GÖREVLER",
            titleStyle
        );

        bool frogsCompleted = fedCount >= requiredFrogs;
        bool seedsCompleted = plantedCount >= requiredSeeds;

        GUI.Label(
            new Rect(textX, panel.y + 45f, textWidth, 25f),
            $"{(frogsCompleted ? "✓" : "○")} Kurbağaları besle: {fedCount}/{requiredFrogs}",
            textStyle
        );

        GUI.Label(
            new Rect(textX, panel.y + 75f, textWidth, 25f),
            $"{(seedsCompleted ? "✓" : "○")} Tohumları ek: {plantedCount}/{requiredSeeds}",
            textStyle
        );
    }
}
