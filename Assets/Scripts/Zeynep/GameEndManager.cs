using UnityEngine;
using UnityEngine.SceneManagement;

public class GameEndManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FrogObjectiveUI objectiveUI;
    [SerializeField] private PondHealthManager pondHealthManager;

    [Header("Next Scene")]
    [Tooltip("Bölüm tamamlandığında geçilecek hedef sahne adı.")]
    [SerializeField] private string nextSceneName = "Emirhan";

    [Header("UI Settings")]
    [SerializeField] private string buttonText = "TAMAM";

    private bool isCompleted;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle textStyle;
    private GUIStyle buttonStyle;

    private void Start()
    {
        if (objectiveUI == null)
            objectiveUI = FindFirstObjectByType<FrogObjectiveUI>();

        if (pondHealthManager == null)
            pondHealthManager = FindFirstObjectByType<PondHealthManager>();
    }

    private void Update()
    {
        if (isCompleted || objectiveUI == null ||
            pondHealthManager == null)
            return;

        if (objectiveUI.AllObjectivesCompleted &&
            pondHealthManager.CurrentHealth >= 100f)
        {
            CompleteLevel();
        }
    }

    private void CompleteLevel()
    {
        isCompleted = true;
        Time.timeScale = 0f;
    }

    private void OnGUI()
    {
        if (!isCompleted)
            return;

        InitializeStyles();

        float width = Mathf.Min(640f, Screen.width - 40f);
        float height = 480f;

        Rect panel = new Rect(
            (Screen.width - width) / 2f,
            (Screen.height - height) / 2f,
            width,
            height
        );

        // Arka planı karart.
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(
            new Rect(0, 0, Screen.width, Screen.height),
            Texture2D.whiteTexture
        );
        GUI.color = previousColor;

        GUI.Box(panel, GUIContent.none);

        GUI.Label(
            new Rect(panel.x + 20, panel.y + 25, width - 40, 50),
            "BÖLÜM TAMAMLANDI!",
            titleStyle
        );

        GUI.Label(
            new Rect(panel.x + 30, panel.y + 95, width - 60, 50),
            "BİR GÖLET, BİNLERCE HAYAT",
            subtitleStyle ?? titleStyle
        );

        GUI.Label(
            new Rect(panel.x + 40, panel.y + 170, width - 80, 180),
            "Senin sayende gölet yeniden hayat buldu!\n\n" +
            "Kuraklık yalnızca suyun azalması değildir. " +
            "Su kaynakları azaldığında kurbağalar, bitkiler " +
            "ve diğer canlılar yaşam alanlarını kaybeder.\n\n" +
            "Unutma: Suyu korumak, yaşamı korumaktır.",
            textStyle
        );

        string label = string.IsNullOrEmpty(buttonText) ? "TAMAM" : buttonText;
        if (GUI.Button(
            new Rect(panel.x + width / 2f - 130f,
                     panel.y + 390f, 260f, 55f),
            label,
            buttonStyle))
        {
            ContinueGame();
        }
    }

    public void ContinueGame()
    {
        Time.timeScale = 1f;

        string targetScene = string.IsNullOrWhiteSpace(nextSceneName) ? "Emirhan" : nextSceneName.Trim();
        SceneManager.LoadScene(targetScene);
    }

    private void InitializeStyles()
    {
        if (titleStyle != null)
            return;

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 28;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = Color.white;
        titleStyle.wordWrap = true;

        subtitleStyle = new GUIStyle(titleStyle);
        subtitleStyle.fontSize = 22;
        subtitleStyle.normal.textColor = new Color(0.95f, 0.85f, 0.35f);

        textStyle = new GUIStyle(GUI.skin.label);
        textStyle.fontSize = 19;
        textStyle.alignment = TextAnchor.UpperCenter;
        textStyle.normal.textColor = Color.white;
        textStyle.wordWrap = true;

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 24;
        buttonStyle.fontStyle = FontStyle.Bold;
    }

    private void OnDisable()
    {
        if (isCompleted)
            Time.timeScale = 1f;
    }
}
