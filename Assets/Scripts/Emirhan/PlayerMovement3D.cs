using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Unity 6 (6000.x) ve Yeni Input System uyumlu, 3rd Person Shooter / GTA Vice City (PC) tarzı
/// karakterin arkasından bakan kamera, Mouse ile yön dönme, WASD ile yürüme,
/// Shift + Yön tuşu ile koşma ve Space ile zıplama kontrolcüsü.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerMovement3D : MonoBehaviour
{
    [Header("Movement & Sprint Settings")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float sprintSpeed = 10f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 6.5f;
    [SerializeField] private float groundCheckOffset = 0.1f;
    [SerializeField] private LayerMask groundLayer = ~0;

    [Header("3rd Person Camera Settings (Behind Character)")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float cameraDistance = 4.5f;
    [SerializeField] private float cameraPivotHeight = 1.6f;
    [SerializeField] private float cameraShoulderOffset = 0f;
    [SerializeField] private float minPitch = -25f;
    [SerializeField] private float maxPitch = 65f;
    [SerializeField] private bool lockCursorOnStart = true;

    [Header("Startup Ground Snap & Fall Safety (Zemine Batmayı Önleme)")]
    [SerializeField] private bool snapToGroundOnStart = true; // Oyun başında yüksekten düşüp zemine gömülmeyi önler
    [SerializeField] private float fallRespawnY = -15f;
    [SerializeField] private int playerRenderQueue = 2010; // Suyun (2005) karakteri kapatmaması için üst katman

    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
    private Vector3 moveDirection;
    private bool canMove = true;
    private bool isSprinting;
    private bool isGrounded;
    private bool jumpRequested;

    private float defaultMoveSpeed;
    private float defaultSprintSpeed;
    private float yaw;
    private float pitch = 15f;

    private Vector3 initialSpawnPosition;
    private Vector3 lastSafeGroundedPosition;

    /// <summary>
    /// Karakterin baktığı yön (kuyruk darbesi, fidan/dal yerleştirme veya ateş etme hesapları için).
    /// </summary>
    public Vector3 FacingDirection => transform.forward;

    /// <summary>
    /// Karakterin şu anda aktif olarak hareket edip etmediğini belirtir.
    /// </summary>
    public bool IsMoving => canMove && moveDirection.sqrMagnitude > 0.01f;

    /// <summary>
    /// Karakterin şu anda Shift basılı tutularak koşup koşmadığını belirtir.
    /// </summary>
    public bool IsSprinting => isSprinting;

    /// <summary>
    /// Karakterin yerde olup olmadığını belirtir.
    /// </summary>
    public bool IsGrounded => isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        defaultMoveSpeed = moveSpeed;
        defaultSprintSpeed = sprintSpeed;

        // Karakterin fiziksel çarpışmalarda devrilmesini ve zeminin içine girmesini (tunneling) engelle
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Eğer karakterin üzerinde ikinci bir Collider (Pickup Range) varsa, onun katı çarpışma yapıp
        // karakteri zemine itmemesi için Trigger olduğundan emin ol
        Collider[] allColliders = GetComponents<Collider>();
        for (int i = 0; i < allColliders.Length; i++)
        {
            if (allColliders[i] != capsuleCollider)
            {
                allColliders[i].isTrigger = true;
            }
        }

        yaw = transform.eulerAngles.y;

        // Suyun karakterin görünümünü kapatmaması için karakterin materyallerini üst katmana (2010) al
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] mats = renderers[i].materials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] != null)
                {
                    mats[m].renderQueue = playerRenderQueue;
                }
            }
        }

        // Kamera atanmadıysa sahnedeki Main Camera'yı otomatik bul
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        // Kamera pozisyonunu LateUpdate içinde omuz/sırt arkasından hesaplayacağımız için hiyerarşiden ayır
        if (cameraTransform != null && cameraTransform.IsChildOf(transform))
        {
            cameraTransform.SetParent(null);
        }

        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Start()
    {
        if (snapToGroundOnStart)
        {
            SnapCharacterToGroundSurface();
        }

        initialSpawnPosition = transform.position;
        lastSafeGroundedPosition = transform.position;
    }

    /// <summary>
    /// Oyun başladığı anda karakterin yüksekten hızla düşüp ada zemininin içine gömülmesini önlemek için
    /// karakterin ayak altını tam ada yüzeyine sıfır hızla oturtur.
    /// </summary>
    private void SnapCharacterToGroundSurface()
    {
        if (capsuleCollider == null) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Bounds bounds = capsuleCollider.bounds;
        float pivotToBottom = transform.position.y - bounds.min.y;
        Vector3 rayOrigin = new Vector3(transform.position.x, bounds.center.y + 2f, transform.position.z);

        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 20f, groundLayer, QueryTriggerInteraction.Ignore);
        float highestGroundY = float.MinValue;
        bool foundGround = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCol = hits[i].collider;
            if (hitCol != capsuleCollider && !hitCol.transform.IsChildOf(transform) && !hitCol.TryGetComponent<WaterLevelController>(out _))
            {
                if (hits[i].point.y > highestGroundY)
                {
                    highestGroundY = hits[i].point.y;
                    foundGround = true;
                }
            }
        }

        if (foundGround)
        {
            transform.position = new Vector3(transform.position.x, highestGroundY + pivotToBottom + 0.02f, transform.position.z);
            Physics.SyncTransforms();
        }
    }

    private void Update()
    {
        // Zemin kontrolü ve son güvenli pozisyonu kaydetme
        isGrounded = CheckGrounded();
        if (isGrounded && transform.position.y > fallRespawnY + 2f)
        {
            lastSafeGroundedPosition = transform.position;
        }

        // Adadan aşağı düşerse son güvenli noktaya geri ışınla
        if (transform.position.y < fallRespawnY)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Vector3 safePos = lastSafeGroundedPosition.y > fallRespawnY + 1f ? lastSafeGroundedPosition : initialSpawnPosition;
            transform.position = safePos + Vector3.up * 0.5f;
            return;
        }

        // Oyun bittiğinde veya hareket kapatıldığında kameranın dönmesini ve yürümeyi durdur
        if (!canMove)
        {
            moveDirection = Vector3.zero;
            isSprinting = false;
            jumpRequested = false;
            return;
        }

        // Build içinde ESC ile imleci serbest bırakma / tekrar tıklayınca kilitleme
        HandleCursorLockToggle();

        // 1. Mouse ile karakteri sağa-sola döndür, kameranın dikey bakış açısını güncelle
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            HandleMouseLook();
        }

        // 2. WASD tuşlarıyla karakterin baktığı yöne göre İleri/Geri ve Sağ/Sol (Strafe) hareket et
        Vector2 input = ReadMoveInput();
        moveDirection = (transform.forward * input.y + transform.right * input.x).normalized;

        // 3. Shift + Yön tuşu basılıysa koş, Shift bırakıldığında yürümeye devam et
        bool shiftHeld = (Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed))
                         || (Gamepad.current != null && Gamepad.current.leftStickButton.isPressed);
        isSprinting = shiftHeld && moveDirection.sqrMagnitude > 0.01f;

        // 4. Space tuşu ile klasik zıplama (sadece yerdeyken)
        bool jumpPressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                           || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        if (jumpPressed && isGrounded)
        {
            jumpRequested = true;
        }
    }

    private void FixedUpdate()
    {
        // Aktif hızı belirle (Shift basılıysa koşu hızı, bırakıldıysa yürüyüş hızı)
        float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;

        // Mevcut dikey hızı koru, zıplama istendiyse dikey hızı jumpForce olarak ayarla
        float verticalVelocity = rb.linearVelocity.y;
        if (jumpRequested)
        {
            verticalVelocity = jumpForce;
            jumpRequested = false;
        }

        rb.linearVelocity = new Vector3(
            moveDirection.x * currentSpeed,
            verticalVelocity,
            moveDirection.z * currentSpeed
        );
    }

    private void LateUpdate()
    {
        if (cameraTransform == null)
            return;

        // Kamerayı karakterin sırtının/omzunun arkasında tut ve karaktere doğru baktır
        Quaternion camRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivotPosition = transform.position
                                + Vector3.up * cameraPivotHeight
                                + transform.right * cameraShoulderOffset;

        cameraTransform.position = pivotPosition - (camRotation * Vector3.forward * cameraDistance);
        cameraTransform.rotation = camRotation;
    }

    private void HandleCursorLockToggle()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    /// <summary>
    /// CapsuleCollider alt noktasından zemini kontrol eder (ekstra obje/layer kurulumu gerektirmez).
    /// </summary>
    private bool CheckGrounded()
    {
        if (capsuleCollider == null)
            return false;

        Bounds bounds = capsuleCollider.bounds;
        float radius = Mathf.Max(0.05f, bounds.extents.x * 0.9f);
        Vector3 spherePosition = new Vector3(bounds.center.x, bounds.min.y + radius - groundCheckOffset, bounds.center.z);

        Collider[] hits = Physics.OverlapSphere(spherePosition, radius, groundLayer, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            // Karakterin kendi collider'ını veya alt objelerini yoksay
            if (hits[i] != capsuleCollider && !hits[i].transform.IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Mouse X ile karakteri (ve arkasındaki kamerayı) yatayda döndürür, Mouse Y ile kamerayı dikeyde eğer.
    /// </summary>
    private void HandleMouseLook()
    {
        if (Mouse.current == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        // Yatay dönüş (Sağ - Sol): Doğrudan karakteri döndürür, kamera da arkasında döner
        yaw += mouseDelta.x * mouseSensitivity;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // Dikey bakış (Yukarı - Aşağı): Kameranın karaktere bakış açısını (pitch) ayarlar
        pitch = Mathf.Clamp(pitch - mouseDelta.y * mouseSensitivity, minPitch, maxPitch);
    }

    /// <summary>
    /// Yeni Input System (Klavye WASD/Ok tuşları ve Gamepad Sol Analog) girdilerini okur.
    /// </summary>
    private Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
        }

        if (input == Vector2.zero && Gamepad.current != null)
        {
            input = Gamepad.current.leftStick.ReadValue();
        }

        return input;
    }

    /// <summary>
    /// Karakterin hareket edebilme durumunu açar veya kapatır.
    /// </summary>
    /// <param name="state">Hareket izni durumu</param>
    public void SetCanMove(bool state)
    {
        canMove = state;

        if (!canMove)
        {
            moveDirection = Vector3.zero;
            isSprinting = false;
            jumpRequested = false;
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }

    /// <summary>
    /// Suya veya balçığa girildiğinde karakterin yürüme ve koşma hızını çarpan ile değiştirir.
    /// </summary>
    /// <param name="multiplier">Hız çarpanı (örn. 0.5f yarıya düşürür)</param>
    public void ApplySpeedModifier(float multiplier)
    {
        float clampedMultiplier = Mathf.Max(0f, multiplier);
        moveSpeed = defaultMoveSpeed * clampedMultiplier;
        sprintSpeed = defaultSprintSpeed * clampedMultiplier;
    }

    /// <summary>
    /// Karakterin yürüme ve koşma hızını başlangıçtaki varsayılan değerlerine döndürür.
    /// </summary>
    public void ResetSpeed()
    {
        moveSpeed = defaultMoveSpeed;
        sprintSpeed = defaultSprintSpeed;
    }
}