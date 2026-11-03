using UnityEngine;

/// <summary>
/// CANOPY: The Last Run - Sel Kovalamacası Debug HUD ve Üst Ekran Sel Mesafe Çubuğu.
/// Gösterilenler: Sel hızı, mesafe, bölge adı, ölüm sayısı, toplam süre, son checkpoint,
/// oyun sonu (ending) sekansı durumu (Idle / FloodTeleported / Drowning / Fading / Info)
/// ve ekranın üst kısmında görsel sel mesafe çubuğu.
/// </summary>
public class FloodDebugHUD : MonoBehaviour
{
    [Header("Sistem Referansları")]
    [Tooltip("Sel hızı ve mesafe bilgisi için FloodChaser referansı.")]
    [SerializeField] private FloodChaser floodChaser;

    [Tooltip("Aktif bölge adı için ZoneManager referansı.")]
    [SerializeField] private ZoneManager zoneManager;

    [Tooltip("Son checkpoint bilgisi için CheckpointManager referansı.")]
    [SerializeField] private CheckpointManager checkpointManager;

    [Tooltip("Ölüm sayısı ve toplam süre için GameFlowManager referansı.")]
    [SerializeField] private GameFlowManager gameFlowManager;

    [Tooltip("Oyun sonu sekans durumu (Idle / FloodTeleported / Drowning / Fading / Info) için EndingSequence referansı.")]
    [SerializeField] private EndingSequence endingSequence;

    [Header("HUD Görünüm Ayarları")]
    [Tooltip("Sel Debug HUD panelini ve üst mesafe çubuğunu göster/gizle.")]
    [SerializeField] private bool showHud = true;

    [Tooltip("HUD panelini açıp kapatmak için kısayol tuşu (varsayılan F4).")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F4;

    [Tooltip("Bilgi panelinin ekranın sağ üst köşesinden X kenar boşluğu (piksel).")]
    [SerializeField] private float rightMargin = 16f;

    [Tooltip("Bilgi panelinin ekranın üst köşesinden Y kenar boşluğu (piksel).")]
    [SerializeField] private float topMargin = 16f;

    [Tooltip("Bilgi paneli genişliği (piksel).")]
    [SerializeField] private float panelWidth = 370f;

    [Tooltip("Bilgi paneli yüksekliği (piksel).")]
    [SerializeField] private float panelHeight = 255f;

    [Header("Üst Sel Mesafe Çubuğu Ayarları")]
    [Tooltip("Mesafe çubuğunun tam dolu görüneceği güvenli maksimum mesafe (varsayılan 60 m).")]
    [SerializeField] private float barMaxDistance = 60f;

    [Tooltip("Üst mesafe çubuğunun genişliği (piksel).")]
    [SerializeField] private float barWidth = 420f;

    [Tooltip("Üst mesafe çubuğunun yüksekliği (piksel).")]
    [SerializeField] private float barHeight = 22f;

    [Tooltip("Üst mesafe çubuğunun ekran tepesinden Y uzaklığı (piksel).")]
    [SerializeField] private float barTopOffset = 18f;

    private GUIStyle boxStyle;
    private GUIStyle headerStyle;
    private GUIStyle labelStyle;
    private GUIStyle barLabelStyle;
    private Texture2D whiteTex;

    private void Awake()
    {
        if (floodChaser == null)
            floodChaser = FindObjectOfType<FloodChaser>();

        if (zoneManager == null)
            zoneManager = FindObjectOfType<ZoneManager>();

        if (checkpointManager == null)
            checkpointManager = FindObjectOfType<CheckpointManager>();

        if (gameFlowManager == null)
            gameFlowManager = FindObjectOfType<GameFlowManager>();

        if (endingSequence == null)
            endingSequence = FindObjectOfType<EndingSequence>();
    }

    private void Update()
    {
        // Sel mesafe çubuğunun daima ekranda kalması için F4 toggle kapatıldı
    }

    private void OnGUI()
    {
        if (!showHud)
            return;

        EnsureStyles();

        DrawTopDistanceBar();
        // DrawInfoPanel() kaldırıldı: Kullanıcı isteği üzerine sağ üstteki HUD kaldırıldı, sadece sel mesafesi gösteriliyor.
    }

    private void DrawTopDistanceBar()
    {
        if (floodChaser == null)
            return;

        float dist = Mathf.Max(0f, floodChaser.DistanceToPlayer);
        float fillRatio = Mathf.Clamp01(dist / Mathf.Max(1f, barMaxDistance));

        float barX = (Screen.width - barWidth) * 0.5f;
        float barY = barTopOffset;

        DrawColoredRect(new Rect(barX - 2f, barY - 2f, barWidth + 4f, barHeight + 4f), new Color(0f, 0f, 0f, 0.75f));
        DrawColoredRect(new Rect(barX, barY, barWidth, barHeight), new Color(0.15f, 0.15f, 0.18f, 0.9f));

        Color barColor;
        if (dist <= floodChaser.NearDistance)
        {
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 14f);
            barColor = new Color(1f, 0.18f, 0.18f, pulse);
        }
        else if (dist <= 25f)
        {
            float t = Mathf.InverseLerp(floodChaser.NearDistance, 25f, dist);
            barColor = Color.Lerp(new Color(1f, 0.35f, 0.1f, 0.95f), new Color(1f, 0.85f, 0.2f, 0.95f), t);
        }
        else
        {
            barColor = new Color(0.2f, 0.85f, 0.95f, 0.9f);
        }

        float filledWidth = barWidth * fillRatio;
        DrawColoredRect(new Rect(barX, barY, filledWidth, barHeight), barColor);

        string statusText = floodChaser.IsWaitingForStartDelay
            ? $"SEL BAŞLIYOR: {floodChaser.RemainingStartDelay:F1} sn | Mesafe: {dist:F1} m"
            : $"SEL MESAFESİ: {dist:F1} m";

        GUI.Label(new Rect(barX, barY, barWidth, barHeight), statusText, barLabelStyle);
    }

    private void DrawInfoPanel()
    {
        float panelX = Screen.width - panelWidth - rightMargin;
        float panelY = topMargin;

        GUI.Box(new Rect(panelX, panelY, panelWidth, panelHeight), GUIContent.none, boxStyle);

        GUILayout.BeginArea(new Rect(panelX + 12f, panelY + 10f, panelWidth - 24f, panelHeight - 20f));

        GUILayout.Label("FLOOD CHASER DEBUG HUD [F4]", headerStyle);
        GUILayout.Space(4f);

        if (floodChaser != null)
        {
            GUILayout.Label(
                $"Sel Hızı: <b>{floodChaser.CurrentSpeed:F2} m/s</b> (Temel: <b>{floodChaser.BaseSpeed:F1}</b> | Çarpan: <b>{floodChaser.CurrentRubberBandMultiplier:F2}x</b>)",
                labelStyle
            );

            string distColor = floodChaser.DistanceToPlayer < floodChaser.NearDistance
                ? "#FF5555"
                : (floodChaser.DistanceToPlayer > floodChaser.FarDistance ? "#55FF88" : "#FFDD55");

            GUILayout.Label(
                $"Mesafe: <color={distColor}><b>{floodChaser.DistanceToPlayer:F1} m</b></color> (Oyuncu: {floodChaser.PlayerProgress:F0}m | Sel: {floodChaser.FloodProgress:F0}m)",
                labelStyle
            );
        }
        else
        {
            GUILayout.Label("Sel Hızı / Mesafe: <color=#FF6666>FloodChaser Atanmadı</color>", labelStyle);
        }

        string zoneName = zoneManager != null ? zoneManager.CurrentZoneName : "Varsayılan";
        GUILayout.Label($"Bölge (Zone): <b>{zoneName}</b>", labelStyle);

        string cpName = checkpointManager != null ? checkpointManager.LastCheckpointName : "Başlangıç";
        GUILayout.Label($"Son Checkpoint: <b>{cpName}</b>", labelStyle);

        int deaths = gameFlowManager != null ? gameFlowManager.DeathCount : 0;
        float totalTime = gameFlowManager != null ? gameFlowManager.ElapsedTime : Time.timeSinceLevelLoad;

        GUILayout.Label($"Ölüm Sayısı: <b>{deaths}</b>", labelStyle);
        GUILayout.Label($"Toplam Süre: <b>{GameFlowManager.FormatElapsedTime(totalTime)}</b>", labelStyle);

        if (endingSequence == null && GameFlowManager.EndingActive)
        {
            endingSequence = FindObjectOfType<EndingSequence>();
        }
        EndingSequence.EndingState endState = endingSequence != null
            ? endingSequence.CurrentState
            : EndingSequence.EndingState.Idle;
        string endStateColor = endState == EndingSequence.EndingState.Idle ? "#AAAAAA" : "#55FF88";
        GUILayout.Label($"Ending Durumu: <color={endStateColor}><b>{endState}</b></color>", labelStyle);

        GUILayout.Space(2f);
        GUILayout.Label("<i>[R] Son Checkpoint'e Dön</i>", labelStyle);

        GUILayout.EndArea();
    }

    private void DrawColoredRect(Rect rect, Color color)
    {
        if (whiteTex == null)
        {
            whiteTex = Texture2D.whiteTexture;
        }

        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, whiteTex);
        GUI.color = prev;
    }

    private void EnsureStyles()
    {
        if (boxStyle != null)
            return;

        boxStyle = new GUIStyle(GUI.skin.box);

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };
        headerStyle.normal.textColor = new Color(0.3f, 0.85f, 1f, 1f);

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            richText = true
        };
        labelStyle.normal.textColor = Color.white;

        barLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        barLabelStyle.normal.textColor = Color.white;
    }
}
