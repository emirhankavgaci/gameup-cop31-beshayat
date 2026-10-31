
using UnityEngine;

public class FrogGameUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PondHealthManager pondHealthManager;
    [SerializeField] private FrogCroak frogCroak;
    [SerializeField] private FrogFoodCollector foodCollector;

    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle centerStyle;

    private void Awake()
    {
        if (pondHealthManager == null)
            pondHealthManager = FindFirstObjectByType<PondHealthManager>();

        if (frogCroak == null)
            frogCroak = FindFirstObjectByType<FrogCroak>();

        if (foodCollector == null)
            foodCollector = FindFirstObjectByType<FrogFoodCollector>();
    }

    private void OnGUI()
    {
        if (pondHealthManager == null || frogCroak == null)
            return;

        InitializeStyles();

        float panelWidth = 280f;
        float panelHeight = 210f;

        Rect panel = new Rect(
            20f,
            20f,
            panelWidth,
            panelHeight
        );

        GUI.Box(panel, GUIContent.none);

        // Gölet saðlýðý baþlýðý
        GUI.Label(
            new Rect(35f, 30f, 250f, 30f),
            "GOLET SAGLIGI",
            titleStyle
        );

        // Gölet saðlýðý barý
        float health = pondHealthManager.CurrentHealth;

        DrawBar(
            new Rect(35f, 65f, 250f, 22f),
            health / 100f,
            Color.Lerp(
                Color.red,
                Color.green,
                health / 100f
            )
        );

        GUI.Label(
            new Rect(35f, 65f, 250f, 22f),
            $"%{health:F0}",
            centerStyle
        );

        // Výraklama durumu
        float cooldown = frogCroak.CooldownRemaining;

        string croakText = cooldown <= 0f
            ? "[E] VIRAKLA - HAZIR"
            : $"VIRAKLAMA: {cooldown:F1} sn";

        GUI.Label(
            new Rect(35f, 100f, 250f, 30f),
            croakText,
            textStyle
        );

        // Toplanan sinek sayýsý
        if (foodCollector != null)
        {
            GUI.Label(
                new Rect(35f, 135f, 250f, 30f),
                $"TOPLANAN SINEK: {foodCollector.FoodCount}",
                textStyle
            );
        }

        // Výraklama cooldown barý
        float cooldownDuration = frogCroak.CooldownDuration;
        float cooldownRemaining = frogCroak.CooldownRemaining;

        float cooldownProgress = cooldownDuration <= 0f
            ? 1f
            : 1f - cooldownRemaining / cooldownDuration;

        DrawBar(
            new Rect(35f, 175f, 250f, 12f),
            cooldownProgress,
            new Color(0.3f, 0.75f, 1f)
        );
    }

    private void DrawBar(Rect rect, float value, Color color)
    {
        value = Mathf.Clamp01(value);

        Color previousColor = GUI.color;

        // Bar arka planý
        GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        // Bar doluluk oraný
        GUI.color = color;

        GUI.DrawTexture(
            new Rect(
                rect.x,
                rect.y,
                rect.width * value,
                rect.height
            ),
            Texture2D.whiteTexture
        );

        GUI.color = previousColor;
    }

    private void InitializeStyles()
    {
        if (titleStyle != null)
            return;

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 18;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;

        textStyle = new GUIStyle(GUI.skin.label);
        textStyle.fontSize = 16;
        textStyle.normal.textColor = Color.white;

        centerStyle = new GUIStyle(GUI.skin.label);
        centerStyle.fontSize = 14;
        centerStyle.fontStyle = FontStyle.Bold;
        centerStyle.alignment = TextAnchor.MiddleCenter;
        centerStyle.normal.textColor = Color.white;
    }
}
