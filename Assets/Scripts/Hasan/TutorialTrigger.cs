using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// CANOPY: The Last Run - Tek Seferlik Tutorial Tetikleyicisi (TutorialTrigger).
/// Oyuncu ("Player" tag'i veya PlayerMotor bileşeni) BoxCollider (isTrigger) içine girdiğinde
/// mesajı TutorialUI kuyruğuna gönderir ve kendini devre dışı bırakır.
/// Static HashSet sayesinde oyuncu checkpoint'ten respawn olsa veya sahne yeniden yüklense bile
/// aynı tutorialId tekrar tetiklenmez.
///
/// ============================================================================
/// LEVEL İÇİ HAZIR TUTORIAL TRIGGER KONFİGÜRASYONU (SIRASIYLA 6 TRIGGER)
/// (Tuş adları <color=#FFD54A> ile altın sarısı renkte vurgulanmıştır)
/// ============================================================================
/// 1. Trigger:
///    - tutorialId : "tut_01_run"
///    - message    : "RUN  <color=#FFD54A>[SHIFT]</color>"
///    - duration   : 2.0 | fadeInTime: 0.25 | fadeOutTime: 0.25
///
/// 2. Trigger:
///    - tutorialId : "tut_02_jump"
///    - message    : "JUMP  <color=#FFD54A>[SPACE]</color>"
///    - duration   : 2.0 | fadeInTime: 0.25 | fadeOutTime: 0.25
///
/// 3. Trigger:
///    - tutorialId : "tut_03_swing"
///    - message    : "JUMP TO BRANCH AND SWING  <color=#FFD54A>[W & S]</color>"
///    - duration   : 2.0 | fadeInTime: 0.25 | fadeOutTime: 0.25
///
/// 4. Trigger:
///    - tutorialId : "tut_04_slide"
///    - message    : "SLIDE  <color=#FFD54A>[C]</color>"
///    - duration   : 2.0 | fadeInTime: 0.25 | fadeOutTime: 0.25
///
/// 5. Trigger:
///    - tutorialId : "tut_05_wallrun"
///    - message    : "WALL RUN"
///    - duration   : 2.0 | fadeInTime: 0.25 | fadeOutTime: 0.25
///
/// 6. Trigger:
///    - tutorialId : "tut_06_branch_transfer"
///    - message    : "BE THE MONKEY AND GRAB BRANCH  <color=#FFD54A>[E]</color>"
///    - duration   : 2.0 | fadeInTime: 0.25 | fadeOutTime: 0.25
/// ============================================================================
/// (Inspector'da bileşene sağ tıklayıp "Preset 1..6" menüsünden tek tıkla doldurabilirsiniz.)
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TutorialTrigger : MonoBehaviour
{
    public const string KEY_HIGHLIGHT_COLOR_HEX = "#FFD54A";

    private const float DEFAULT_DURATION = 2.0f;
    private const float DEFAULT_FADE_IN_TIME = 0.25f;
    private const float DEFAULT_FADE_OUT_TIME = 0.25f;
    private const float GIZMO_LABEL_HEIGHT_OFFSET = 1.2f;

    /// <summary>
    /// Oyun boyunca tetiklenmiş tutorial ID'lerini tutan kalıcı (static) küme.
    /// Sahne yeniden başlasa veya oyuncu checkpoint'ten respawn olsa bile aynı tutorial tekrar çıkmaz.
    /// </summary>
    private static readonly HashSet<string> triggeredTutorialIds = new HashSet<string>();

    [Header("Kimlik ve Tek Seferlik Tetikleme")]
    [Tooltip("Bu tutorial tetikleyicisinin benzersiz kimliği (ör: tut_01_run). Static HashSet'te saklanır.")]
    [SerializeField] private string tutorialId = "tut_01_run";

    [Header("Tutorial Mesajı ve Süre Ayarları")]
    [Tooltip("Ekranda gösterilecek TMP Rich Text destekli, çok satırlı tutorial mesajı.")]
    [TextArea(2, 5)]
    [SerializeField] private string message = "RUN  <color=#FFD54A>[SHIFT]</color>";

    [Tooltip("Yazının tam görünür halde ekranda kalma süresi (varsayılan 2 sn).")]
    [SerializeField] private float duration = DEFAULT_DURATION;

    [Tooltip("Yazının ekrana gelirken beliriş (fade-in) süresi (varsayılan 0.25 sn).")]
    [SerializeField] private float fadeInTime = DEFAULT_FADE_IN_TIME;

    [Tooltip("Yazının ekrandan giderken kayboluş (fade-out) süresi (varsayılan 0.25 sn).")]
    [SerializeField] private float fadeOutTime = DEFAULT_FADE_OUT_TIME;

    [Header("Durum ve Referanslar")]
    [Tooltip("Bu trigger'ın tetiklenip tetiklenmediğini gösterir (tetiklenince true olur).")]
    [SerializeField] private bool triggered;

    [Tooltip("İsteğe bağlı TutorialUI referansı (boş bırakılırsa sahnede otomatik bulunur veya oluşturulur).")]
    [SerializeField] private TutorialUI tutorialUI;

    /// <summary>
    /// Yeni bir oyun oturumu başlatıldığında (Editor Play Mode başlangıcında) static kayıtları temizler.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearStaticIdsOnSubsystemRegistration()
    {
        triggeredTutorialIds.Clear();
    }

    /// <summary>
    /// İstenirse tüm tutorial geçmişini manuel olarak sıfırlamak için yardımcı metot.
    /// </summary>
    public static void ResetAllTriggeredTutorials()
    {
        triggeredTutorialIds.Clear();
    }

    private void Awake()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        // Eğer bu tutorialId daha önce tetiklendiyse (respawn veya sahne tekrarı) baştan devre dışı bırak
        string effectiveId = GetEffectiveTutorialId();
        if (triggeredTutorialIds.Contains(effectiveId))
        {
            triggered = true;
            enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered || other == null)
            return;

        // Checkpoint sistemindeki gibi "Player" tag'i veya PlayerMotor bileşeni ile algıla
        bool isPlayer = other.CompareTag("Player") ||
                        other.GetComponent<PlayerMotor>() != null ||
                        other.GetComponentInParent<PlayerMotor>() != null;

        if (!isPlayer)
            return;

        string effectiveId = GetEffectiveTutorialId();
        if (triggeredTutorialIds.Contains(effectiveId))
        {
            triggered = true;
            enabled = false;
            return;
        }

        triggered = true;
        triggeredTutorialIds.Add(effectiveId);

        TutorialUI targetUi = tutorialUI != null ? tutorialUI : TutorialUI.Instance;
        if (targetUi != null)
        {
            targetUi.EnqueueTutorialMessage(message, duration, fadeInTime, fadeOutTime);
        }

        // Tetiklendikten sonra bileşeni devre dışı bırak
        enabled = false;
    }

    private string GetEffectiveTutorialId()
    {
        if (!string.IsNullOrWhiteSpace(tutorialId))
        {
            return tutorialId.Trim();
        }

        // Eğer Inspector'da boş bırakılmışsa obje adı ve yaklaşık konumundan benzersiz bir anahtar üret
        Vector3 pos = transform.position;
        return $"{gameObject.name}_{Mathf.RoundToInt(pos.x)}_{Mathf.RoundToInt(pos.y)}_{Mathf.RoundToInt(pos.z)}";
    }

    #region Hazır Mesaj Presetleri (Inspector Context Menu)

    [ContextMenu("Preset 1: RUN [SHIFT]")]
    private void ApplyPreset1Run()
    {
        tutorialId = "tut_01_run";
        message = $"RUN  <color={KEY_HIGHLIGHT_COLOR_HEX}>[SHIFT]</color>";
        duration = DEFAULT_DURATION;
        fadeInTime = DEFAULT_FADE_IN_TIME;
        fadeOutTime = DEFAULT_FADE_OUT_TIME;
    }

    [ContextMenu("Preset 2: JUMP [SPACE]")]
    private void ApplyPreset2Jump()
    {
        tutorialId = "tut_02_jump";
        message = $"JUMP  <color={KEY_HIGHLIGHT_COLOR_HEX}>[SPACE]</color>";
        duration = DEFAULT_DURATION;
        fadeInTime = DEFAULT_FADE_IN_TIME;
        fadeOutTime = DEFAULT_FADE_OUT_TIME;
    }

    [ContextMenu("Preset 3: JUMP TO BRANCH AND SWING [W & S]")]
    private void ApplyPreset3Swing()
    {
        tutorialId = "tut_03_swing";
        message = $"JUMP TO BRANCH AND SWING  <color={KEY_HIGHLIGHT_COLOR_HEX}>[W & S]</color>";
        duration = DEFAULT_DURATION;
        fadeInTime = DEFAULT_FADE_IN_TIME;
        fadeOutTime = DEFAULT_FADE_OUT_TIME;
    }

    [ContextMenu("Preset 4: SLIDE [C]")]
    private void ApplyPreset4Slide()
    {
        tutorialId = "tut_04_slide";
        message = $"SLIDE  <color={KEY_HIGHLIGHT_COLOR_HEX}>[C]</color>";
        duration = DEFAULT_DURATION;
        fadeInTime = DEFAULT_FADE_IN_TIME;
        fadeOutTime = DEFAULT_FADE_OUT_TIME;
    }

    [ContextMenu("Preset 5: WALL RUN")]
    private void ApplyPreset5WallRun()
    {
        tutorialId = "tut_05_wallrun";
        message = "WALL RUN";
        duration = DEFAULT_DURATION;
        fadeInTime = DEFAULT_FADE_IN_TIME;
        fadeOutTime = DEFAULT_FADE_OUT_TIME;
    }

    [ContextMenu("Preset 6: BE THE MONKEY AND GRAB BRANCH [E]")]
    private void ApplyPreset6BranchTransfer()
    {
        tutorialId = "tut_06_branch_transfer";
        message = $"BE THE MONKEY AND GRAB BRANCH  <color={KEY_HIGHLIGHT_COLOR_HEX}>[E]</color>";
        duration = DEFAULT_DURATION;
        fadeInTime = DEFAULT_FADE_IN_TIME;
        fadeOutTime = DEFAULT_FADE_OUT_TIME;
    }

    #endregion

    private void OnDrawGizmos()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
            return;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Color fillColor = triggered
            ? new Color(0.5f, 0.5f, 0.5f, 0.18f)
            : new Color(1f, 0.84f, 0.25f, 0.25f);

        Color wireColor = triggered
            ? new Color(0.6f, 0.6f, 0.6f, 0.65f)
            : new Color(1f, 0.84f, 0.25f, 0.95f);

        Gizmos.color = fillColor;
        Gizmos.DrawCube(col.center, col.size);

        Gizmos.color = wireColor;
        Gizmos.DrawWireCube(col.center, col.size);

        Gizmos.matrix = oldMatrix;

#if UNITY_EDITOR
        Vector3 worldTop = transform.TransformPoint(col.center + Vector3.up * (col.size.y * 0.5f)) + Vector3.up * GIZMO_LABEL_HEIGHT_OFFSET;
        GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            richText = true,
            fontSize = 11
        };
        style.normal.textColor = triggered ? Color.gray : new Color(1f, 0.88f, 0.35f, 1f);
        Handles.Label(worldTop, $"[Tutorial: {tutorialId}]\n{message}", style);
#endif
    }
}
