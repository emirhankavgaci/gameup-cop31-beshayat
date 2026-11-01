
using UnityEngine;

public class GameStartMenu : MonoBehaviour
{
    private bool gameStarted;
    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle buttonStyle;

    private readonly string[] instructions =
    {
        "W / A / D / S  - Hareket ve donus",
        "SPACE  - Zipla",
        "SHIFT  - Hizlan",
        "SOL TIK  - Sinek yakala",
        "E  - Virakla / etkilesim",
        "F  - Arkadasini besle",
        "SAG TIK  - Tohum ek"
    };

    private void Start()
    {
        gameStarted = false;
        Time.timeScale = 0f;
    }

    private void OnGUI()
    {
        if (gameStarted)
            return;

        InitializeStyles();

        float width = 460f;
        float height = 520f;

        Rect panel = new Rect(
            (Screen.width - width) / 2f,
            (Screen.height - height) / 2f,
            width,
            height
        );

        GUI.Box(panel, GUIContent.none);

        GUI.Label(
            new Rect(panel.x + 20, panel.y + 25, 420, 50),
            "DONGU: BES HAYAT",
            titleStyle
        );

        GUI.Label(
            new Rect(panel.x + 30, panel.y + 85, 400, 30),
            "Kuruyan goleti yeniden hayata dondur!",
            textStyle
        );

        GUI.Label(
            new Rect(panel.x + 30, panel.y + 130, 400, 30),
            "NASIL OYNANIR?",
            textStyle
        );

        for (int i = 0; i < instructions.Length; i++)
        {
            GUI.Label(
                new Rect(
                    panel.x + 35,
                    panel.y + 170 + i * 30,
                    400,
                    25
                ),
                instructions[i],
                textStyle
            );
        }

        GUI.Label(
            new Rect(panel.x + 30, panel.y + 395, 400, 40),
            "3 arkadasini besle, 3 tohum ek ve goleti kurtar!",
            textStyle
        );

        if (GUI.Button(
            new Rect(panel.x + 110, panel.y + 455, 240, 45),
            "OYUNA BASLA",
            buttonStyle))
        {
            StartGame();
        }
    }

    private void StartGame()
    {
        gameStarted = true;
        Time.timeScale = 1f;
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

        textStyle = new GUIStyle(GUI.skin.label);
        textStyle.fontSize = 16;
        textStyle.normal.textColor = Color.white;
        textStyle.wordWrap = true;

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 20;
        buttonStyle.fontStyle = FontStyle.Bold;
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
    }
}
