using UnityEngine;

/// <summary>
/// CANOPY: The Last Run - Bölge Başlangıç Tetikleyicisi (Zone Trigger).
/// Oyuncu bu trigger'dan geçtiğinde ZoneManager.EnterZone(zoneIndex) metodunu çağırır.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ZoneTrigger : MonoBehaviour
{
    [Header("Bölge Ayarları")]
    [Tooltip("Aktifleştirilecek bölge indeksi (0 = Easy, 1 = Medium, 2 = Hard).")]
    [SerializeField] private int zoneIndex = 0;

    [Tooltip("Tetiklenecek ZoneManager referansı (boşsa sahnede otomatik bulunur).")]
    [SerializeField] private ZoneManager zoneManager;

    [Tooltip("Scene görünümünde bu bölge kapısının Gizmo rengi.")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.75f, 0.1f, 0.35f);

    private BoxCollider boxCollider;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
        }

        if (zoneManager == null)
        {
            zoneManager = FindObjectOfType<ZoneManager>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
            return;

        bool isPlayer = other.GetComponent<PlayerMotor>() != null ||
                        other.GetComponentInParent<PlayerMotor>() != null ||
                        other.CompareTag("Player");

        if (isPlayer && zoneManager != null)
        {
            zoneManager.EnterZone(zoneIndex);
        }
    }

    private void OnDrawGizmos()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
            return;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(col.center, col.size);

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.95f);
        Gizmos.DrawWireCube(col.center, col.size);

        Gizmos.matrix = oldMatrix;
    }
}

