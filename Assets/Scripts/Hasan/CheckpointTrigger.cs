using UnityEngine;

/// <summary>
/// CANOPY: The Last Run - Kayıt Noktası Tetikleyicisi (Checkpoint Trigger).
/// Oyuncu içinden geçtiğinde pozisyon ve rotasyonu CheckpointManager'a kaydeder.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class CheckpointTrigger : MonoBehaviour
{
    [Header("Checkpoint Bilgileri")]
    [Tooltip("Checkpoint sıra numarası (örneğin 1, 2, 3...).")]
    [SerializeField] private int checkpointIndex = 1;

    [Tooltip("Debug HUD üzerinde görünecek checkpoint adı (örneğin 'Easy - CP 1').")]
    [SerializeField] private string checkpointName = "CP 1";

    [Tooltip("İsteğe bağlı: Oyuncunun doğacağı özel nokta (boşsa bu objenin kendi pozisyon ve rotasyonu kullanılır).")]
    [SerializeField] private Transform customSpawnPoint;

    [Tooltip("Oyuncu doğduğunda zemine gömülmemesi için eklenecek dikey güvenlik ofseti (metre).")]
    [SerializeField] private float spawnVerticalOffset = 0.15f;

    [Tooltip("İlgili CheckpointManager referansı (boşsa sahnede otomatik bulunur).")]
    [SerializeField] private CheckpointManager checkpointManager;

    private bool isActivated;

    private void Awake()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (checkpointManager == null)
        {
            checkpointManager = FindObjectOfType<CheckpointManager>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || GameFlowManager.EndingActive)
            return;

        bool isPlayer = other.GetComponent<PlayerMotor>() != null ||
                        other.GetComponentInParent<PlayerMotor>() != null ||
                        other.CompareTag("Player");

        if (!isPlayer || checkpointManager == null)
            return;

        Transform refTransform = customSpawnPoint != null ? customSpawnPoint : transform;
        Vector3 spawnPos = refTransform.position + Vector3.up * spawnVerticalOffset;
        Quaternion spawnRot = refTransform.rotation;

        isActivated = true;
        checkpointManager.SaveCheckpoint(checkpointIndex, checkpointName, spawnPos, spawnRot);
    }

    private void OnDrawGizmos()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
            return;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Color baseColor = isActivated
            ? new Color(0.2f, 1f, 0.4f, 0.30f)
            : new Color(0.2f, 0.85f, 1f, 0.25f);

        Gizmos.color = baseColor;
        Gizmos.DrawCube(col.center, col.size);

        Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.9f);
        Gizmos.DrawWireCube(col.center, col.size);

        Gizmos.matrix = oldMatrix;

        // Doğuş yönü oku
        Transform spawnRef = customSpawnPoint != null ? customSpawnPoint : transform;
        Gizmos.color = Color.green;
        Vector3 start = spawnRef.position + Vector3.up * 0.5f;
        Vector3 end = start + spawnRef.forward * 2.0f;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawSphere(end, 0.12f);
    }
}

