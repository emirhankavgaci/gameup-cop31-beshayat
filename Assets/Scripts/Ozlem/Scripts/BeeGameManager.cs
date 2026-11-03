using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BeeGameManager : MonoBehaviour
{
    private static BeeGameManager instance;
    public static BeeGameManager Instance => instance;

    [Header("Game Time")]
    [Tooltip("Sure siniri (Saniye). Oyuncuya gosterilmez, arkada isler.")]
    [SerializeField] private float gameDuration = 60f; // 1 dakika (kullanici talebiyle 2 dakikadan 1 dakikaya indirildi)
    private float timer = 0f;

    [Header("References")]
    [SerializeField] private HiveArea hive;

    [Header("Next Level")]
    [SerializeField] private string nextSceneName = "Main Menu";

    private bool isGameWon = false;
    private bool isGameOver = false;
    private List<BeeNectar> bees = new();

    // UI Dokulari ve Stilleri
    private Texture2D bgBoxTex;
    private Texture2D barBgTex;
    private Texture2D barFillTex;
    private Texture2D blackTex;
    private Texture2D buttonTex;

    private GUIStyle headerStyle;
    private GUIStyle valueStyle;
    private GUIStyle winTitleStyle;
    private GUIStyle winMessageStyle;
    private GUIStyle buttonStyle;
    private GUIStyle warningStyle;
    private bool stylesInitialized = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedCallback()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name.Equals("Ozlem", System.StringComparison.OrdinalIgnoreCase))
        {
            EnsureInstanceExists();
        }
    }

    public static void EnsureInstanceExists()
    {
        if (FindAnyObjectByType<BeeGameManager>() == null)
        {
            GameObject go = new GameObject("BeeGameManager");
            go.AddComponent<BeeGameManager>();
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        Time.timeScale = 1f;

        if (hive == null)
            hive = FindAnyObjectByType<HiveArea>();
    }

    private void OnEnable()
    {
        if (hive == null)
            hive = FindAnyObjectByType<HiveArea>();
    }

    private void Start()
    {
        if (hive == null)
            hive = FindAnyObjectByType<HiveArea>();

        RefreshBees();
    }

    private void RefreshBees()
    {
        bees.Clear();
        bees.AddRange(FindObjectsByType<BeeNectar>(FindObjectsSortMode.None));
    }

    private void Update()
    {
        if (isGameWon || isGameOver)
            return;

        if (hive == null)
        {
            hive = FindAnyObjectByType<HiveArea>();
            if (hive == null)
                return;
        }

        // Zaman sayaci (arkada isler, 1 dakika)
        timer += Time.deltaTime;

        // Kaybetme kosulu: Kovan nektari 0'a inerse
        if (hive.CurrentNectar <= 0f)
        {
            isGameOver = true;
            hive.SetAutoDrain(false);
            Time.timeScale = 0f;
            return;
        }

        // Kazanma kosulu: 1 dakika boyunca kovani hayatta tutarsa
        if (timer >= gameDuration)
        {
            isGameWon = true;
            hive.SetAutoDrain(false);
            Time.timeScale = 0f;
            return;
        }
    }

    private void InitStyles()
    {
        if (stylesInitialized && bgBoxTex != null && barBgTex != null && barFillTex != null && blackTex != null && buttonTex != null)
            return;

        bgBoxTex = MakeTexture(2, 2, new Color(0.08f, 0.08f, 0.08f, 0.88f));
        barBgTex = MakeTexture(2, 2, new Color(0.2f, 0.2f, 0.2f, 0.9f));
        barFillTex = MakeTexture(2, 2, Color.white);
        blackTex = MakeTexture(2, 2, new Color(0.02f, 0.02f, 0.02f, 0.88f));
        buttonTex = MakeTexture(2, 2, new Color(0.18f, 0.55f, 0.24f, 1f));

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        headerStyle.normal.textColor = new Color(0.98f, 0.78f, 0.25f);

        valueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight
        };
        valueStyle.normal.textColor = Color.white;

        warningStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        warningStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);

        winTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        winTitleStyle.normal.textColor = new Color(0.98f, 0.82f, 0.28f);

        winMessageStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            wordWrap = true,
            alignment = TextAnchor.MiddleCenter
        };
        winMessageStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f);

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        buttonStyle.normal.background = buttonTex;
        buttonStyle.normal.textColor = Color.white;

        stylesInitialized = true;
    }

    private void OnGUI()
    {
        InitStyles();

        // 1) Kazanma Ekrani (Tum ekrani kaplayan mesaj ekrani)
        if (isGameWon)
        {
            DrawWinScreen();
            return;
        }

        // 2) Kaybetme Ekrani (Panel ortada gosterilir)
        if (isGameOver)
        {
            DrawGameOverScreen();
        }

        // 3) Sag Ust Kose Kovan Nektari HUD (Hem oyun sirasinda hem de oyun kaybedildiginde sag ustte her zaman gorunur)
        DrawNectarHUD();
    }

    private void DrawNectarHUD()
    {
        if (hive == null)
        {
            hive = FindAnyObjectByType<HiveArea>();
            if (hive == null)
                return;
        }

        if (bees.Count == 0)
        {
            RefreshBees();
        }

        float width = 310f;
        float height = 100f;
        float x = Screen.width - width - 20f;
        float y = 20f;

        // Arka Plan Kutusu
        GUI.DrawTexture(new Rect(x, y, width, height), bgBoxTex);

        // Baslik
        GUI.Label(new Rect(x + 14, y + 10, 180, 24), "KOVAN NEKTARI", headerStyle);

        // Deger
        int curNectar = Mathf.CeilToInt(hive.CurrentNectar);
        int maxNectar = Mathf.RoundToInt(hive.MaxCapacity);
        GUI.Label(new Rect(x + 140, y + 10, width - 155, 24), $"{curNectar} / {maxNectar}", valueStyle);

        // Ilerleme Cubugu (Progress Bar)
        float barX = x + 14;
        float barY = y + 40;
        float barW = width - 28;
        float barH = 18;

        GUI.DrawTexture(new Rect(barX, barY, barW, barH), barBgTex);

        float fillRatio = Mathf.Clamp01(hive.CurrentNectar / hive.MaxCapacity);
        float fillW = barW * fillRatio;

        Color fillColor;
        if (fillRatio > 0.45f)
            fillColor = new Color(0.96f, 0.72f, 0.18f); // Altin sarisi bal rengi
        else if (fillRatio > 0.2f)
            fillColor = new Color(0.95f, 0.5f, 0.15f);  // Turuncu
        else
            fillColor = (Mathf.Sin(Time.unscaledTime * 6f) > 0f) ? new Color(1f, 0.2f, 0.2f) : new Color(0.6f, 0.1f, 0.1f); // Yanip sonen kirmizi

        Color oldColor = GUI.color;
        GUI.color = fillColor;
        GUI.DrawTexture(new Rect(barX, barY, fillW, barH), barFillTex);
        GUI.color = oldColor;

        // Arilarin Tasidigi Polen Bilgisi
        int carriedPollen = 0;
        for (int i = 0; i < bees.Count; i++)
        {
            if (bees[i] != null)
                carriedPollen += bees[i].CurrentNectar;
        }

        if (fillRatio <= 0.2f || isGameOver)
        {
            string warnText = isGameOver ? "Nektar tukendi!" : "Nektar tukeniyor! Cicek toplayip kovana don!";
            GUI.Label(new Rect(x + 14, y + 66, width - 28, 22), warnText, warningStyle);
        }
        else
        {
            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            subStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            GUI.Label(new Rect(x + 14, y + 66, width - 28, 22), $"Tasinan Polen: {carriedPollen}", subStyle);
        }
    }

    private void DrawWinScreen()
    {
        // Tum ekrani kaplayan siyah arka plan
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTex);

        float panelWidth = Mathf.Min(780f, Screen.width - 40f);
        float panelHeight = Mathf.Min(500f, Screen.height - 40f);
        float panelX = (Screen.width - panelWidth) / 2f;
        float panelY = (Screen.height - panelHeight) / 2f;

        // Baslik
        GUI.Label(
            new Rect(panelX, panelY + 15, panelWidth, 45),
            "BOLUM TAMAMLANDI - KOVANI KORUDUN!",
            winTitleStyle
        );

        string message = "Iklim krizi, arilarin yasamini ciddi sekilde tehdit ediyor. " +
                         "Artan sicakliklar, kuraklik ve degisen mevsimler ciceklerin azalmasina, " +
                         "arilarin besin kaynaklarinin tukenmesine neden oluyor. " +
                         "Bu durum, ari populasyonlarinin azalmasina ve bazi turlerin neslinin " +
                         "tukenme tehlikesiyle karsi karsiya kalmasina yol aciyor. " +
                         "Arilari korumak, doganin dengesini korumak icin buyuk onem tasiyor.";

        GUI.Label(
            new Rect(panelX + 30, panelY + 80, panelWidth - 60, panelHeight - 190),
            message,
            winMessageStyle
        );

        // Devam Et Butonu
        float btnWidth = 240f;
        float btnHeight = 52f;
        float btnX = panelX + (panelWidth - btnWidth) / 2f;
        float btnY = panelY + panelHeight - btnHeight - 20f;

        if (GUI.Button(new Rect(btnX, btnY, btnWidth, btnHeight), "DEVAM ET", buttonStyle))
        {
            LoadNextScene();
        }
    }

    private void DrawGameOverScreen()
    {
        // Yari saydam arka plan (arkadaki oyun ve sag ustteki HUD hafifce gorunsun)
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTex);

        float panelWidth = Mathf.Min(600f, Screen.width - 40f);
        float panelHeight = 320f;
        float panelX = (Screen.width - panelWidth) / 2f;
        float panelY = (Screen.height - panelHeight) / 2f;

        // Kutu arka plani
        GUI.DrawTexture(new Rect(panelX, panelY, panelWidth, panelHeight), bgBoxTex);

        GUIStyle lostTitle = new GUIStyle(winTitleStyle);
        lostTitle.normal.textColor = new Color(1f, 0.25f, 0.25f);

        GUI.Label(new Rect(panelX, panelY + 20, panelWidth, 40), "KOVAN NEKTARI TUKENDI!", lostTitle);

        GUI.Label(
            new Rect(panelX + 20, panelY + 80, panelWidth - 40, 100),
            "Kovandaki nektar tukendi ve arilar besinsiz kaldi.\n\n" +
            "Ciceklerden daha hizli nektar toplayip kovana tasimalisin!",
            winMessageStyle
        );

        float btnWidth = 220f;
        float btnHeight = 50f;
        float btnX = panelX + (panelWidth - btnWidth) / 2f;
        float btnY = panelY + panelHeight - btnHeight - 20f;

        GUIStyle retryStyle = new GUIStyle(buttonStyle);
        retryStyle.normal.background = MakeTexture(2, 2, new Color(0.7f, 0.2f, 0.2f, 1f));

        if (GUI.Button(new Rect(btnX, btnY, btnWidth, btnHeight), "TEKRAR DENE", retryStyle))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    public void LoadNextScene()
    {
        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(nextSceneName) && Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
            if (nextIndex < SceneManager.sceneCountInBuildSettings)
            {
                SceneManager.LoadScene(nextIndex);
            }
            else
            {
                if (SceneManager.sceneCountInBuildSettings > 0)
                    SceneManager.LoadScene(0);
                else
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
    }

    private Texture2D MakeTexture(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}
