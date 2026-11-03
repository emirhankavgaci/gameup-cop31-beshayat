using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// CANOPY: The Last Run - Kaktüs / Diken Engel Çarpışma Bileşeni.
/// Kaktüs objesine (BoxCollider bulunan objeye) eklenir.
/// BoxCollider ister "Is Trigger = Açık" ister "Is Trigger = Kapalı (Normal Katı Collider)" olsun,
/// oyuncu çarptığı anda oyuncuyu en son kaydedilen Checkpoint'e geri gönderir.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class CactusHazard : MonoBehaviour
{
    [Header("Çarpışma ve Respawn Ayarları")]
    [Tooltip("Açık olursa kaktüse çarptığın anda beklemeden (anında) son checkpoint'e ışınlar. Kapalı olursa 1.2 sn ekran kararma efektiyle ışınlar.")]
    [SerializeField] private bool instantRespawn = true;

    [Tooltip("Aynı kaktüsün aynı karede birden fazla kez tetiklenmesini önleyen koruma süresi (sn).")]
    [SerializeField] private float hitCooldown = 0.25f;

    [Tooltip("GameFlowManager yoksa respawn sonrasında selin oyuncunun kaç metre gerisine çekileceği (varsayılan 60 m).")]
    [SerializeField] private float fallbackRespawnFloodDistance = 80f;

    [Header("Referanslar (Boş Bırakılırsa Otomatik Bulunur)")]
    [Tooltip("Oyun akışı ve ölüm/respawn yöneticisi.")]
    [SerializeField] private GameFlowManager gameFlowManager;

    [Tooltip("Checkpoint yöneticisi (GameFlowManager yoksa doğrudan kullanılır).")]
    [SerializeField] private CheckpointManager checkpointManager;

    [Tooltip("Sel yöneticisi (Respawn olduğunda seli de geriye çekmek için).")]
    [SerializeField] private FloodChaser floodChaser;

    [Header("Olaylar (Ses / Efekt İçin)")]
    [Tooltip("Oyuncu kaktüse çarptığında tetiklenen UnityEvent.")]
    [SerializeField] private UnityEvent onCactusHit;

    private float lastHitTime = -999f;

    private void Awake()
    {
        if (gameFlowManager == null)
        {
            gameFlowManager = FindObjectOfType<GameFlowManager>();
        }

        if (checkpointManager == null)
        {
            checkpointManager = FindObjectOfType<CheckpointManager>();
        }

        if (floodChaser == null)
        {
            floodChaser = FindObjectOfType<FloodChaser>();
        }
    }

    // 1. Durum: BoxCollider üzerinde "Is Trigger" AÇIK ise burası çalışır
    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
            return;

        PlayerMotor motor = other.GetComponent<PlayerMotor>();
        if (motor == null)
        {
            motor = other.GetComponentInParent<PlayerMotor>();
        }

        if (motor != null)
        {
            HandlePlayerHit(motor);
        }
    }

    // 2. Durum: BoxCollider üzerinde "Is Trigger" KAPALI (katı çarpışma) ise
    // PlayerMotor.OnControllerColliderHit veya OnCollisionEnter üzerinden burası çalışır
    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null)
            return;

        PlayerMotor motor = collision.collider.GetComponent<PlayerMotor>();
        if (motor == null)
        {
            motor = collision.collider.GetComponentInParent<PlayerMotor>();
        }

        if (motor != null)
        {
            HandlePlayerHit(motor);
        }
    }

    /// <summary>
    /// Oyuncu kaktüse temas ettiğinde çağrılır ve oyuncuyu son checkpoint'e geri gönderir.
    /// </summary>
    public void HandlePlayerHit(PlayerMotor motor)
    {
        if (motor == null || GameFlowManager.EndingActive)
            return;

        if (Time.time - lastHitTime < hitCooldown)
            return;

        lastHitTime = Time.time;
        onCactusHit?.Invoke();

        // Kaktüse çarptığı anda oyuncunun hareketini dondur (ölüyken yürümeyi önler)
        motor.SetMovementLocked(true);

        // Sahnede GameFlowManager varsa hem ölüm sayacını artırır, hem oyuncuyu son checkpoint'e ışınlar, hem de seli geri çeker
        if (gameFlowManager != null)
        {
            gameFlowManager.TriggerObstacleRespawn(instantRespawn);
            return;
        }

        // Sahnede GameFlowManager yoksa doğrudan CheckpointManager ile son checkpoint'e ışınla
        if (checkpointManager != null && checkpointManager.HasSavedCheckpoint)
        {
            motor.TeleportAndResetState(checkpointManager.LastCheckpointPosition, checkpointManager.LastCheckpointRotation);
        }
        else
        {
            motor.ResetMotorVelocityAndState();
        }

        if (floodChaser != null)
        {
            floodChaser.RepositionBehindPlayer(fallbackRespawnFloodDistance, 0.5f);
        }
    }

    private void OnDrawGizmos()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
            return;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = new Color(1f, 0.15f, 0.2f, 0.25f);
        Gizmos.DrawCube(col.center, col.size);

        Gizmos.color = new Color(1f, 0.1f, 0.15f, 0.9f);
        Gizmos.DrawWireCube(col.center, col.size);

        Gizmos.matrix = oldMatrix;
    }
}

