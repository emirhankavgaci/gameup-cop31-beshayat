using UnityEngine;
using UnityEngine.InputSystem;

//on player object

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

    [Header("Startup Ground Snap & Fall Safety")]
    [SerializeField] private bool snapToGroundOnStart = true; // Prevent the character from falling through the ground at the start of the game (unused feature)
    [SerializeField] private float fallRespawnY = -15f;
    [SerializeField] private int playerRenderQueue = 2010; // For water surface not to hide the player, default is 2000

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


    // stores character movement state and camera orientation, provides public properties for other scripts to access
    public Vector3 FacingDirection => transform.forward; 
    public bool IsMoving => canMove && moveDirection.sqrMagnitude > 0.01f;
    public bool IsSprinting => isSprinting;
    public bool IsGrounded => isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        defaultMoveSpeed = moveSpeed;
        defaultSprintSpeed = sprintSpeed;

        // To prevent the character from tipping over when colliding with objects, freeze rotation on all axes
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // If there are multiple colliders on the character, set all except the main capsule collider to trigger to avoid physics issues
        Collider[] allColliders = GetComponents<Collider>();
        for (int i = 0; i < allColliders.Length; i++)
        {
            if (allColliders[i] != capsuleCollider)
            {
                allColliders[i].isTrigger = true;
            }
        }

        yaw = transform.eulerAngles.y;

        // render queue for player and held items to avoid being hidden by water surface
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

        // find the main camera if not assigned in the inspector
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        // helper method to detach the camera from the player if it is a child of the player object
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
        // Ground check using capsule collider's bottom point, no extra objects or layers needed
        isGrounded = CheckGrounded();
        if (isGrounded && transform.position.y > fallRespawnY + 2f)
        {
            lastSafeGroundedPosition = transform.position;
        }

        // If the character falls below a certain Y level, reset its position to the last safe grounded position or initial spawn point
        if (transform.position.y < fallRespawnY)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Vector3 safePos = lastSafeGroundedPosition.y > fallRespawnY + 1f ? lastSafeGroundedPosition : initialSpawnPosition;
            transform.position = safePos + Vector3.up * 0.5f;
            return;
        }

        // Verify if the character is allowed to move; if not, reset movement variables and exit early
        if (!canMove)
        {
            moveDirection = Vector3.zero;
            isSprinting = false;
            jumpRequested = false;
            return;
        }

        // Escape key toggles cursor lock state, and left mouse button locks the cursor if not already locked
        HandleCursorLockToggle();

        // Mouse look input
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            HandleMouseLook();
        }

        // 2. Movement input is read from keyboard (WASD/Arrow keys) or gamepad 
        Vector2 input = ReadMoveInput();
        moveDirection = (transform.forward * input.y + transform.right * input.x).normalized;

        // 3. Sprint controls: Shift key or left stick button on gamepad
        bool shiftHeld = (Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed))
                         || (Gamepad.current != null && Gamepad.current.leftStickButton.isPressed);
        isSprinting = shiftHeld && moveDirection.sqrMagnitude > 0.01f;

        // 4. Spacebar or gamepad south button triggers jump if grounded
        bool jumpPressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                           || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        if (jumpPressed && isGrounded)
        {
            jumpRequested = true;
        }
    }

    private void FixedUpdate()
    {
        // Determine the current speed based on whether the character is sprinting or walking
        float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;

        // Preserve the vertical velocity to allow for gravity and jumping, while applying horizontal movement based on input
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

        // 3rd person camera that follows the character with a shoulder offset and adjustable pitch/yaw angles
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
            // Ignore the character's own collider and any child colliders to avoid false positives
            if (hits[i] != capsuleCollider && !hits[i].transform.IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }

    // rotate the character and adjust camera pitch
    private void HandleMouseLook()
    {
        if (Mouse.current == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();


        yaw += mouseDelta.x * mouseSensitivity;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        pitch = Mathf.Clamp(pitch - mouseDelta.y * mouseSensitivity, minPitch, maxPitch);
    }

    // / Read movement input 
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

    // slow down the character if the player is in water
    public void ApplySpeedModifier(float multiplier)
    {
        float clampedMultiplier = Mathf.Max(0f, multiplier);
        moveSpeed = defaultMoveSpeed * clampedMultiplier;
        sprintSpeed = defaultSprintSpeed * clampedMultiplier;
    }

    // reset the character's speed to default values
    public void ResetSpeed()
    {
        moveSpeed = defaultMoveSpeed;
        sprintSpeed = defaultSprintSpeed;
    }
}