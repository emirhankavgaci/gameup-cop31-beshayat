using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BeeGameManager : MonoBehaviour
{
    private static BeeGameManager instance;
    public static BeeGameManager Instance => instance;

    [Header("Game Time")]
    [Tooltip("Süre sınırı (Saniye). Oyuncuya gösterilmez, arkada işler.")]
    [SerializeField] private float gameDuration = 120f; // 2 dakika
    private float timer = 0f;

    [Header("References")]
    [SerializeField] private HiveArea hive;

    [Header("Next Level")]
    [SerializeField] private string nextSceneName = "Zeynep";

    private bool isGameWon = false;
    private bool isGameOver = false;
    private List<BeeNectar> bees = new();

    // UI Dokuları ve Stilleri
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceInScene()
    {
        string activeScene = SceneManager.GetActiveScene().name;
        if (activeScene.Equals("Ozlem", System.StringComparison.OrdinalIgnoreCase))
        {
            if (FindFirstObjectByType<BeeGameManager>() == null)
            {
                GameObject go = new GameObject("BeeGameManager");
                go.AddComponent<BeeGameManager>();
            }
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
    }

    private void Start()
    {
        if (hive == null)
            hive = FindFirstObjectByType<HiveArea>();

        bees.Clear();
        bees.AddRange(FindObjectsByType<BeeNectar>(FindObjectsSortMode.None));
    }

    private void Update()
    {
        if (isGameWon || isGameOver)
            return;

        if (hive == null)
        {
            hive = FindFirstObjectByType<HiveArea>();
            if (hive == null)
                return;
        }

        // Zaman sayacı (arkada işler, 2 dakika)
        timer += Time.deltaTime;

        // Kaybetme koşulu: Kovan nektarı 0'a inerse
        if (hive.CurrentNectar <= 0f)
        {
            isGameOver = true;
            hive.SetAutoDrain(false);
            Time.timeScale = 0f;
            return;
        }

        // Kazanma koşulu: 2 dakika boyunca kovanı hayatta tutarsa
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
        if (stylesInitialized)
            return;

        bgBoxTex = MakeTexture(2, 2, new Color(0.08f, 0.08f, 0.08f, 0.85f));
        barBgTex = MakeTexture(2, 2, new Color(0.2f, 0.2f, 0.2f, 0.9f));
        barFillTex = MakeTexture(2, 2, Color.white);
        blackTex = MakeTexture(2, 2, new Color(0.02f, 0.02f, 0.02f, 0.98f));
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

        // 1) Sağ Üst Köşe Kovan Nektarı HUD
        if (!isGameWon && !isGameOver)
        {
            DrawNectarHUD();
        }
        // 2) Kazanma Ekranı (Siyah Arkaplan ve İklim Krizi Mesajı)
        else if (isGameWon)
        {
            DrawWinScreen();
        }
        // 3) Kaybetme Ekranı
        else if (isGameOver)
        {
            DrawGameOverScreen();
        }
    }

    private void DrawNectarHUD()
    {
        if (hive == null)
            return;

        float width = 310f;
        float height = 100f;
        float x = Screen.width - width - 20f;
        float y = 20f;

        // Arka Plan Kutusu
        GUI.DrawTexture(new Rect(x, y, width, height), bgBoxTex);

        // Başlık
        GUI.Label(new Rect(x + 14, y + 10, 180, 24), "🍯 KOVAN NEKTARI", headerStyle);

        // Değer
        int curNectar = Mathf.CeilToInt(hive.CurrentNectar);
        int maxNectar = Mathf.RoundToInt(hive.MaxCapacity);
        GUI.Label(new Rect(x + 140, y + 10, width - 155, 24), $"{curNectar} / {maxNectar}", valueStyle);

        // İlerleme Çubuğu (Progress Bar)
        float barX = x + 14;
        float barY = y + 40;
        float barW = width - 28;
        float barH = 18;

        GUI.DrawTexture(new Rect(barX, barY, barW, barH), barBgTex);

        float fillRatio = Mathf.Clamp01(hive.CurrentNectar / hive.MaxCapacity);
        float fillW = barW * fillRatio;

        Color fillColor;
        if (fillRatio > 0.45f)
            fillColor = new Color(0.96f, 0.72f, 0.18f); // Altın sarısı bal rengi
        else if (fillRatio > 0.2f)
            fillColor = new Color(0.95f, 0.5f, 0.15f);  // Turuncu
        else
            fillColor = (Mathf.Sin(Time.unscaledTime * 6f) > 0f) ? new Color(1f, 0.2f, 0.2f) : new Color(0.6f, 0.1f, 0.1f); // Yanıp sönen kırmızı

        Color oldColor = GUI.color;
        GUI.color = fillColor;
        GUI.DrawTexture(new Rect(barX, barY, fillW, barH), barFillTex);
        GUI.color = oldColor;

        // Arıların Taşıdığı Polen Bilgisi
        int carriedPollen = 0;
        for (int i = 0; i < bees.Count; i++)
        {
            if (bees[i] != null)
                carriedPollen += bees[i].CurrentNectar;
        }

        if (fillRatio <= 0.2f)
        {
            GUI.Label(new Rect(x + 14, y + 66, width - 28, 22), "⚠️ Nektar tükeniyor! Çiçek toplayıp kovana dön!", warningStyle);
        }
        else
        {
            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            subStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            GUI.Label(new Rect(x + 14, y + 66, width - 28, 22), $"🐝 Taşınan Polen: {carriedPollen}", subStyle);
        }
    }

    private void DrawWinScreen()
    {
        // Tüm ekranı kaplayan siyah arka plan
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTex);

        float panelWidth = Mathf.Min(780f, Screen.width - 40f);
        float panelHeight = Mathf.Min(500f, Screen.height - 40f);
        float panelX = (Screen.width - panelWidth) / 2f;
        float panelY = (Screen.height - panelHeight) / 2f;

        // Başlık
        GUI.Label(
            new Rect(panelX, panelY + 15, panelWidth, 45),
            "BÖLÜM TAMAMLANDI - KOVANI KORUDUN! 🐝",
            winTitleStyle
        );

        // Kullanıcının istediği birebir mesaj
        string message = "İklim krizi, arıların yaşamını ciddi şekilde tehdit ediyor. " +
                         "Artan sıcaklıklar, kuraklık ve değişen mevsimler çiçeklerin azalmasına, " +
                         "arıların besin kaynaklarının tükenmesine neden oluyor. " +
                         "Bu durum, arı popülasyonlarının azalmasına ve bazı türlerin neslinin " +
                         "tükenme tehlikesiyle karşı karşıya kalmasına yol açıyor. " +
                         "Arıları korumak, doğanın dengesini korumak için büyük önem taşıyor.";

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
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTex);

        float panelWidth = Mathf.Min(600f, Screen.width - 40f);
        float panelHeight = 320f;
        float panelX = (Screen.width - panelWidth) / 2f;
        float panelY = (Screen.height - panelHeight) / 2f;

        GUIStyle lostTitle = new GUIStyle(winTitleStyle);
        lostTitle.normal.textColor = new Color(1f, 0.25f, 0.25f);

        GUI.Label(new Rect(panelX, panelY + 20, panelWidth, 40), "KOVAN NEKTARI TÜKENDİ! 🥀", lostTitle);

        GUI.Label(
            new Rect(panelX + 20, panelY + 80, panelWidth - 40, 100),
            "Kovandaki nektar tükendi ve arılar besinsiz kaldı.\n\n" +
            "Çiçeklerden daha hızlı nektar toplayıp kovana taşımalısın!",
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
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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