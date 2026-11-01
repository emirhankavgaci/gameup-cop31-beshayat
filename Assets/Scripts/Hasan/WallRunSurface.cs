using UnityEngine;

/// <summary>
/// CANOPY: The Last Run - Tek Yüzlü Duvar Koşusu Yüzey Tanımlayıcısı (WallRunSurface).
/// Duvar objesine eklenir ve çift collider kullanmaya gerek kalmadan duvarın yalnızca
/// belirlenen yüzünde (localRunNormal) Wall Run yapılabilmesini sağlar.
/// Scene görünümünde koşulabilir yüzü yarı saydam yeşil kare + yeşil normal oku ile,
/// geçersiz arka yüzü ise kırmızı ok ve kırmızı çarpı (X) ile çizer.
/// </summary>
public class WallRunSurface : MonoBehaviour
{
    private const float GIZMO_ARROW_LENGTH = 1.6f;
    private const float GIZMO_ARROW_HEAD_SIZE = 0.35f;
    private const float GIZMO_QUAD_SIZE = 1.8f;
    private const float GIZMO_SURFACE_OFFSET = 0.04f;

    [Header("Koşulabilir Yüzey Yönü Ayarları")]
    [Tooltip("Duvarın koşulabilir ön yüzünün yerel (local) normal vektörü (varsayılan Vector3.forward = yerel +Z yönü).")]
    [SerializeField] private Vector3 localRunNormal = Vector3.forward;

    [Tooltip("Raycast çarpan yüzey normali ile koşulabilir dünya normali arasındaki minimum Dot Product eşiği (varsayılan 0.7).")]
    [Range(0.1f, 0.99f)]
    [SerializeField] private float normalTolerance = 0.7f;

    [Tooltip("Açık olursa duvarın her iki yüzünde de Wall Run yapılabilir (eski davranış). Kapalıysa sadece yeşil ok tarafında koşulur.")]
    [SerializeField] private bool allowBothSides = false;

    /// <summary>
    /// Koşulabilir yüzün yerel normal vektörü.
    /// </summary>
    public Vector3 LocalRunNormal
    {
        get => localRunNormal;
        set => localRunNormal = value.sqrMagnitude > 0.0001f ? value.normalized : Vector3.forward;
    }

    /// <summary>
    /// Normal doğrulama Dot Product eşiği (varsayılan 0.7).
    /// </summary>
    public float NormalTolerance => normalTolerance;

    /// <summary>
    /// İki yüzün de geçerli olup olmadığı.
    /// </summary>
    public bool AllowBothSides => allowBothSides;

    /// <summary>
    /// Koşulabilir yüzün dünya uzayındaki (World Space) birim normal vektörü: transform.TransformDirection(localRunNormal).
    /// </summary>
    public Vector3 WorldRunNormal
    {
        get
        {
            Vector3 safeLocal = localRunNormal.sqrMagnitude > 0.0001f ? localRunNormal.normalized : Vector3.forward;
            Vector3 worldDir = transform.TransformDirection(safeLocal);
            return worldDir.sqrMagnitude > 0.0001f ? worldDir.normalized : transform.forward;
        }
    }

    /// <summary>
    /// Verilen RaycastHit yüzey normalinin bu duvarın koşulabilir yüzüne ait olup olmadığını kontrol eder.
    /// Vector3.Dot(hitNormal, runNormalWorld) >= normalTolerance ise true döner.
    /// </summary>
    public bool IsHitNormalValid(Vector3 hitNormal)
    {
        if (allowBothSides)
            return true;

        Vector3 cleanHitNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.forward;
        float dot = Vector3.Dot(cleanHitNormal, WorldRunNormal);
        return dot >= normalTolerance;
    }

    [ContextMenu("Yönü Ters Çevir (Invert Local Run Normal)")]
    private void InvertLocalRunNormal()
    {
        localRunNormal = -localRunNormal;
    }

    [ContextMenu("Yönü +Z Yap (Vector3.forward)")]
    private void SetNormalForward() => localRunNormal = Vector3.forward;

    [ContextMenu("Yönü -Z Yap (Vector3.back)")]
    private void SetNormalBack() => localRunNormal = Vector3.back;

    [ContextMenu("Yönü +X Yap (Vector3.right)")]
    private void SetNormalRight() => localRunNormal = Vector3.right;

    [ContextMenu("Yönü -X Yap (Vector3.left)")]
    private void SetNormalLeft() => localRunNormal = Vector3.left;

    private void OnDrawGizmos()
    {
        Vector3 runNormalWorld = WorldRunNormal;
        if (runNormalWorld.sqrMagnitude < 0.001f)
            return;

        // Duvar merkezini ve yüzey kalınlığını Collider üzerinden bul (varsa)
        Vector3 centerWorld = transform.position;
        float halfThickness = 0.25f;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            centerWorld = col.bounds.center;
            Vector3 extents = col.bounds.extents;
            halfThickness = Mathf.Max(0.05f,
                Mathf.Abs(runNormalWorld.x) * extents.x +
                Mathf.Abs(runNormalWorld.y) * extents.y +
                Mathf.Abs(runNormalWorld.z) * extents.z);
        }

        Vector3 frontFaceCenter = centerWorld + runNormalWorld * (halfThickness + GIZMO_SURFACE_OFFSET);
        Vector3 backFaceCenter = centerWorld - runNormalWorld * (halfThickness + GIZMO_SURFACE_OFFSET);

        // 1. Ön yüz (Koşulabilir Yüz): Yarı saydam yeşil kare + yeşil normal oku
        DrawSurfaceQuad(frontFaceCenter, runNormalWorld, new Color(0.15f, 0.95f, 0.35f, 0.28f), new Color(0.15f, 1f, 0.35f, 0.95f));
        DrawNormalArrow(frontFaceCenter, runNormalWorld, new Color(0.15f, 1f, 0.35f, 1f));

        // 2. Arka yüz: allowBothSides açıksa yeşil, kapalıysa kırmızı çarpı (X) ve kırmızı ok
        if (allowBothSides)
        {
            DrawSurfaceQuad(backFaceCenter, -runNormalWorld, new Color(0.15f, 0.95f, 0.35f, 0.28f), new Color(0.15f, 1f, 0.35f, 0.95f));
            DrawNormalArrow(backFaceCenter, -runNormalWorld, new Color(0.15f, 1f, 0.35f, 1f));
        }
        else
        {
            DrawInvalidBackFaceCrossAndArrow(backFaceCenter, -runNormalWorld, new Color(1f, 0.2f, 0.2f, 0.95f));
        }
    }

    private void DrawSurfaceQuad(Vector3 faceCenter, Vector3 normal, Color fillColor, Color wireColor)
    {
        Quaternion faceRot = Quaternion.LookRotation(normal, Vector3.up);
        Matrix4x4 prevMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(faceCenter, faceRot, new Vector3(GIZMO_QUAD_SIZE, GIZMO_QUAD_SIZE, 0.02f));

        Gizmos.color = fillColor;
        Gizmos.DrawCube(Vector3.zero, Vector3.one);

        Gizmos.color = wireColor;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

        Gizmos.matrix = prevMatrix;
    }

    private void DrawNormalArrow(Vector3 start, Vector3 direction, Color arrowColor)
    {
        Gizmos.color = arrowColor;
        Vector3 end = start + direction * GIZMO_ARROW_LENGTH;
        Gizmos.DrawLine(start, end);

        Vector3 right = Vector3.Cross(Vector3.up, direction);
        if (right.sqrMagnitude < 0.001f)
        {
            right = Vector3.Cross(Vector3.right, direction);
        }
        right.Normalize();
        Vector3 up = Vector3.Cross(direction, right).normalized;

        Vector3 headBase = end - direction * GIZMO_ARROW_HEAD_SIZE;
        Gizmos.DrawLine(end, headBase + right * (GIZMO_ARROW_HEAD_SIZE * 0.6f));
        Gizmos.DrawLine(end, headBase - right * (GIZMO_ARROW_HEAD_SIZE * 0.6f));
        Gizmos.DrawLine(end, headBase + up * (GIZMO_ARROW_HEAD_SIZE * 0.6f));
        Gizmos.DrawLine(end, headBase - up * (GIZMO_ARROW_HEAD_SIZE * 0.6f));
    }

    private void DrawInvalidBackFaceCrossAndArrow(Vector3 faceCenter, Vector3 backNormal, Color redColor)
    {
        DrawNormalArrow(faceCenter, backNormal, redColor);

        Vector3 right = Vector3.Cross(Vector3.up, backNormal);
        if (right.sqrMagnitude < 0.001f)
        {
            right = Vector3.Cross(Vector3.right, backNormal);
        }
        right.Normalize();
        Vector3 up = Vector3.Cross(backNormal, right).normalized;

        float halfSize = GIZMO_QUAD_SIZE * 0.45f;
        Vector3 c1 = faceCenter + (right + up) * halfSize;
        Vector3 c2 = faceCenter + (-right - up) * halfSize;
        Vector3 c3 = faceCenter + (-right + up) * halfSize;
        Vector3 c4 = faceCenter + (right - up) * halfSize;

        Gizmos.color = redColor;
        Gizmos.DrawLine(c1, c2);
        Gizmos.DrawLine(c3, c4);
    }
}

