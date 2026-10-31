using UnityEngine;

/// <summary>
/// CANOPY: The Last Run - Ağaç Dalı Veri ve Geometri Bileşeni.
/// Her dal objesine eklenir. Dalın başlangıç/bitiş noktalarını, eksenini, uzunluğunu ve
/// herhangi bir dünya noktasının dal üzerindeki en yakın noktasını / normalize konumunu (t: 0..1) hesaplar.
/// </summary>
[ExecuteAlways]
public class BranchData : MonoBehaviour
{
    [Header("Dal Uç Noktaları (Boşsa Otomatik Hesaplanır)")]
    [Tooltip("Dalın başlangıç ucunu temsil eden opsiyonel Transform.")]
    [SerializeField] private Transform startPoint;

    [Tooltip("Dalın bitiş ucunu temsil eden opsiyonel Transform.")]
    [SerializeField] private Transform endPoint;

    [Header("Otomatik Uç Hesaplama Ayarları")]
    [Tooltip("Uç Transform'lar atanmamışsa Collider boyundan içeride bırakılacak güvenlik payı (metre).")]
    [SerializeField] private float autoEndPadding = 0.15f;

    [Tooltip("Collider da yoksa varsayılan yerel dal uzunluğu (metre).")]
    [SerializeField] private float fallbackBranchLength = 4f;

    [Header("Gizmos Görselleştirme")]
    [Tooltip("Scene görünümünde dal ekseni çizgisinin rengi.")]
    [SerializeField] private Color gizmoLineColor = new Color(0.25f, 0.95f, 0.35f, 1f);

    [Tooltip("Scene görünümünde dal uç kürelerinin rengi.")]
    [SerializeField] private Color gizmoEndpointColor = new Color(1f, 0.85f, 0.15f, 1f);

    [Tooltip("Gizmo uç kürelerinin yarıçapı (metre).")]
    [SerializeField] private float gizmoEndpointRadius = 0.12f;

    // Dahili önbellek
    private Vector3 cachedStartWorld;
    private Vector3 cachedEndWorld;
    private Vector3 cachedAxis = Vector3.forward;
    private float cachedLength = 1f;

    /// <summary>Dalın dünya uzayındaki başlangıç noktası (t = 0).</summary>
    public Vector3 StartWorldPosition
    {
        get
        {
            RecalculateGeometry();
            return cachedStartWorld;
        }
    }

    /// <summary>Dalın dünya uzayındaki bitiş noktası (t = 1).</summary>
    public Vector3 EndWorldPosition
    {
        get
        {
            RecalculateGeometry();
            return cachedEndWorld;
        }
    }

    /// <summary>Start noktasından End noktasına doğru normalize edilmiş dal eksen vektörü.</summary>
    public Vector3 Axis
    {
        get
        {
            RecalculateGeometry();
            return cachedAxis;
        }
    }

    /// <summary>Dalın kullanılabilir toplam uzunluğu (metre).</summary>
    public float Length
    {
        get
        {
            RecalculateGeometry();
            return cachedLength;
        }
    }

    private void Awake()
    {
        RecalculateGeometry();
    }

    private void OnValidate()
    {
        RecalculateGeometry();
    }

    /// <summary>
    /// Start ve End Transform'ları atanmışsa onları kullanır; atanmamışsa objenin Collider veya Scale
    /// bilgisinden en uzun ekseni bularak dal uçlarını dünya uzayında hesaplar.
    /// </summary>
    public void RecalculateGeometry()
    {
        if (startPoint != null && endPoint != null)
        {
            cachedStartWorld = startPoint.position;
            cachedEndWorld = endPoint.position;
        }
        else
        {
            ComputeEndpointsFromColliderOrBounds(out cachedStartWorld, out cachedEndWorld);
        }

        Vector3 delta = cachedEndWorld - cachedStartWorld;
        float dist = delta.magnitude;

        if (dist > 0.001f)
        {
            cachedLength = dist;
            cachedAxis = delta / dist;
        }
        else
        {
            cachedLength = 0.1f;
            cachedAxis = transform.forward;
        }
    }

    /// <summary>
    /// Verilen dünya pozisyonunun dal doğru parçası üzerindeki normalize konumunu (0 ile 1 arasında t) döndürür.
    /// </summary>
    public float GetT(Vector3 worldPos)
    {
        RecalculateGeometry();

        Vector3 startToPoint = worldPos - cachedStartWorld;
        float projectedDist = Vector3.Dot(startToPoint, cachedAxis);
        return Mathf.Clamp01(projectedDist / cachedLength);
    }

    /// <summary>
    /// Verilen dünya pozisyonuna dal ekseni üzerindeki en yakın noktayı döndürür.
    /// </summary>
    public Vector3 GetClosestPointOnBranch(Vector3 worldPos)
    {
        float t = GetT(worldPos);
        return cachedStartWorld + cachedAxis * (t * cachedLength);
    }

    /// <summary>
    /// 0 ile 1 arasındaki normalize t değerine karşılık gelen dal üzerindeki dünya noktasını döndürür.
    /// </summary>
    public Vector3 GetPointAtT(float t)
    {
        RecalculateGeometry();
        float clampedT = Mathf.Clamp01(t);
        return cachedStartWorld + cachedAxis * (clampedT * cachedLength);
    }

    /// <summary>
    /// Transform uçları atanmadığında CapsuleCollider, BoxCollider veya Mesh/Scale üzerinden uzun ekseni otomatik bulur.
    /// </summary>
    private void ComputeEndpointsFromColliderOrBounds(out Vector3 startWorld, out Vector3 endWorld)
    {
        Collider col = GetComponent<Collider>();
        Vector3 localCenter = Vector3.zero;
        Vector3 localAxis = Vector3.up; // Unity silindir ve kapsüllerinde varsayılan uzun eksen Y'dir
        float halfSpan = fallbackBranchLength * 0.5f;

        if (col is CapsuleCollider capsule)
        {
            localCenter = capsule.center;
            if (capsule.direction == 0)
            {
                localAxis = Vector3.right;
                halfSpan = (capsule.height * 0.5f) * Mathf.Abs(transform.lossyScale.x);
            }
            else if (capsule.direction == 1)
            {
                localAxis = Vector3.up;
                halfSpan = (capsule.height * 0.5f) * Mathf.Abs(transform.lossyScale.y);
            }
            else
            {
                localAxis = Vector3.forward;
                halfSpan = (capsule.height * 0.5f) * Mathf.Abs(transform.lossyScale.z);
            }
        }
        else if (col is BoxCollider box)
        {
            localCenter = box.center;
            Vector3 scaledSize = Vector3.Scale(box.size, transform.lossyScale);
            scaledSize = new Vector3(Mathf.Abs(scaledSize.x), Mathf.Abs(scaledSize.y), Mathf.Abs(scaledSize.z));

            if (scaledSize.x >= scaledSize.y && scaledSize.x >= scaledSize.z)
            {
                localAxis = Vector3.right;
                halfSpan = scaledSize.x * 0.5f;
            }
            else if (scaledSize.y >= scaledSize.x && scaledSize.y >= scaledSize.z)
            {
                localAxis = Vector3.up;
                halfSpan = scaledSize.y * 0.5f;
            }
            else
            {
                localAxis = Vector3.forward;
                halfSpan = scaledSize.z * 0.5f;
            }
        }
        else
        {
            // MeshCollider veya Collider yoksa lossyScale üzerinden en uzun ekseni seç
            Vector3 absScale = new Vector3(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y),
                Mathf.Abs(transform.lossyScale.z)
            );

            // Unity standart Cylinder mesh'inin yüksekliği 2 birimdir (Y ekseni)
            if (absScale.y * 2f >= absScale.x && absScale.y * 2f >= absScale.z)
            {
                localAxis = Vector3.up;
                halfSpan = absScale.y;
            }
            else if (absScale.x >= absScale.z)
            {
                localAxis = Vector3.right;
                halfSpan = absScale.x * 0.5f;
            }
            else
            {
                localAxis = Vector3.forward;
                halfSpan = absScale.z * 0.5f;
            }
        }

        halfSpan = Mathf.Max(0.15f, halfSpan - autoEndPadding);
        Vector3 worldCenter = transform.TransformPoint(localCenter);
        Vector3 worldDir = transform.TransformDirection(localAxis).normalized;

        startWorld = worldCenter - worldDir * halfSpan;
        endWorld = worldCenter + worldDir * halfSpan;
    }

    private void OnDrawGizmos()
    {
        RecalculateGeometry();

        // Dal eksen çizgisi
        Gizmos.color = gizmoLineColor;
        Gizmos.DrawLine(cachedStartWorld, cachedEndWorld);

        // Başlangıç ve bitiş uç küreleri
        Gizmos.color = gizmoEndpointColor;
        Gizmos.DrawSphere(cachedStartWorld, gizmoEndpointRadius);
        Gizmos.DrawSphere(cachedEndWorld, gizmoEndpointRadius);

        // Orta noktada yön oku/halkası
        Vector3 midPoint = (cachedStartWorld + cachedEndWorld) * 0.5f;
        Gizmos.DrawWireSphere(midPoint, gizmoEndpointRadius * 0.75f);
    }
}

