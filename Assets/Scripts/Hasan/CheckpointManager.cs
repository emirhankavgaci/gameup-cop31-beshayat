using System;
using UnityEngine;

/// <summary>
/// CANOPY: The Last Run - Kayıt Noktası (Checkpoint) Yöneticisi.
/// Oyuncunun geçtiği son CheckpointTrigger'ın pozisyon, rotasyon ve isim bilgisini saklar.
/// </summary>
public class CheckpointManager : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Başlangıç konumunu varsayılan ilk checkpoint olarak kaydetmek için oyuncu Transform'u.")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("Yalnızca mevcut checkpoint'ten daha ileri indeksli checkpoint'leri kaydet (geri dönüşte eskiyi yazmasın).")]
    [SerializeField] private bool onlyAllowForwardCheckpoints = false;

    /// <summary>
    /// Yeni bir checkpoint kaydedildiğinde tetiklenir: (checkpointIndex, checkpointName).
    /// </summary>
    public event Action<int, string> OnCheckpointReached;

    // Dahili kayıt durumu
    private Vector3 lastCheckpointPosition;
    private Quaternion lastCheckpointRotation = Quaternion.identity;
    private string lastCheckpointName = "Başlangıç";
    private int lastCheckpointIndex = -1;
    private bool hasSavedCheckpoint;

    // Dış erişim özellikleri (GameFlowManager ve FloodDebugHUD için)
    public Vector3 LastCheckpointPosition => lastCheckpointPosition;
    public Quaternion LastCheckpointRotation => lastCheckpointRotation;
    public string LastCheckpointName => lastCheckpointName;
    public int LastCheckpointIndex => lastCheckpointIndex;
    public bool HasSavedCheckpoint => hasSavedCheckpoint;

    private void Awake()
    {
        if (playerTransform == null)
        {
            PlayerMotor motor = FindObjectOfType<PlayerMotor>();
            if (motor != null)
            {
                playerTransform = motor.transform;
            }
        }
    }

    private void Start()
    {
        if (playerTransform != null && !hasSavedCheckpoint)
        {
            lastCheckpointPosition = playerTransform.position;
            lastCheckpointRotation = playerTransform.rotation;
            lastCheckpointName = "Başlangıç (Start)";
            lastCheckpointIndex = 0;
            hasSavedCheckpoint = true;
        }
    }

    /// <summary>
    /// Oyuncu bir CheckpointTrigger'dan geçtiğinde çağrılır ve konum/rotasyon bilgisini kaydeder.
    /// </summary>
    public void SaveCheckpoint(int checkpointIndex, string checkpointName, Vector3 position, Quaternion rotation)
    {
        if (GameFlowManager.EndingActive)
            return;

        if (onlyAllowForwardCheckpoints && hasSavedCheckpoint && checkpointIndex < lastCheckpointIndex)
            return;

        lastCheckpointIndex = checkpointIndex;
        lastCheckpointName = string.IsNullOrWhiteSpace(checkpointName) ? $"CP #{checkpointIndex}" : checkpointName;
        lastCheckpointPosition = position;

        Vector3 euler = rotation.eulerAngles;
        lastCheckpointRotation = Quaternion.Euler(0f, euler.y, 0f);
        hasSavedCheckpoint = true;

        OnCheckpointReached?.Invoke(lastCheckpointIndex, lastCheckpointName);
    }
}

