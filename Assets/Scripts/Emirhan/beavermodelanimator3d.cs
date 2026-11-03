using UnityEngine;

public class BeaverModelAnimator3D : MonoBehaviour
{
    [Header("1.Front Paw Scale Fix")]
    [SerializeField] private bool fixFrontPawsScale = true;
    [SerializeField] private Vector3 frontPawScaleMultiplier = new Vector3(0.55f, 0.45f, 0.45f);

    [Header("2. Model General Size and Orientation Alignment")]
    [SerializeField] private bool autoAlignAndScaleModel = true;
    [SerializeField] private float overallModelScale = 0.22f;
    [SerializeField] private Vector3 modelLocalPositionOffset = new Vector3(0f, -0.85f, 0f);
    [SerializeField] private Vector3 modelLocalRotationOffset = new Vector3(0f, -90f, 0f); // Beaver model is facing +X, but we want +Z forward for the player

    [Header("3. Walk and Sprint Animation")]
    [SerializeField] private bool enableProceduralAnimation = true;
    [SerializeField] private float walkCycleSpeed = 10f;
    [SerializeField] private float sprintCycleSpeed = 16f;
    [SerializeField] private float legSwingAngle = 22f;
    [SerializeField] private float tailWagAngle = 12f;
    [SerializeField] private float bodyBobAmount = 1.5f;

    private PlayerMovement3D playerMovement;

    // kunduz.fbx
    private Transform bodyPart;       // Plane
    private Transform tailPart;       // Plane.001
    private Transform backRightLeg;   // Circle
    private Transform backLeftLeg;    // Circle.001
    private Transform frontRightPaw;  // Circle.002 
    private Transform frontLeftPaw;   // Circle.003 

    private Vector3 origFrontRightScale;
    private Vector3 origFrontLeftScale;

    private Quaternion origBodyRot;
    private Quaternion origTailRot;
    private Quaternion origBackRightRot;
    private Quaternion origBackLeftRot;
    private Quaternion origFrontRightRot;
    private Quaternion origFrontLeftRot;
    private Vector3 origBodyPos;

    private float animTimer = 0f;

    private void Awake()
    {
        playerMovement = GetComponentInParent<PlayerMovement3D>();

        // kunduz.fbx parts
        bodyPart = FindChildRecursive(transform, "Plane");
        tailPart = FindChildRecursive(transform, "Plane.001");
        backRightLeg = FindChildRecursive(transform, "Circle");
        backLeftLeg = FindChildRecursive(transform, "Circle.001");
        frontRightPaw = FindChildRecursive(transform, "Circle.002");
        frontLeftPaw = FindChildRecursive(transform, "Circle.003");

        // Auto align and scale the model 
        if (autoAlignAndScaleModel && transform.parent != null)
        {
            transform.localPosition = modelLocalPositionOffset;
            transform.localRotation = Quaternion.Euler(modelLocalRotationOffset);
            transform.localScale = Vector3.one * overallModelScale;
        }

        // Save original rotations and positions for later use
        if (bodyPart != null)
        {
            origBodyRot = bodyPart.localRotation;
            origBodyPos = bodyPart.localPosition;
        }
        if (tailPart != null) origTailRot = tailPart.localRotation;
        if (backRightLeg != null) origBackRightRot = backRightLeg.localRotation;
        if (backLeftLeg != null) origBackLeftRot = backLeftLeg.localRotation;

        if (frontRightPaw != null)
        {
            origFrontRightRot = frontRightPaw.localRotation;
            origFrontRightScale = frontRightPaw.localScale;
        }

        if (frontLeftPaw != null)
        {
            origFrontLeftRot = frontLeftPaw.localRotation;
            origFrontLeftScale = frontLeftPaw.localScale;
        }

        ApplyFrontPawScaleFix();
    }

    private void Update()
    {
        if (fixFrontPawsScale)
        {
            ApplyFrontPawScaleFix();
        }

        if (!enableProceduralAnimation)
            return;

        bool isMoving = playerMovement != null && playerMovement.IsMoving;
        bool isSprinting = playerMovement != null && playerMovement.IsSprinting;

        if (isMoving)
        {
            float cycleSpeed = isSprinting ? sprintCycleSpeed : walkCycleSpeed;
            float swingMultiplier = isSprinting ? 1.3f : 1f;
            animTimer += Time.deltaTime * cycleSpeed;

            float wave = Mathf.Sin(animTimer);
            float oppositeWave = Mathf.Sin(animTimer + Mathf.PI);
            float doubleWave = Mathf.Abs(Mathf.Sin(animTimer));

            // Realistic leg swing for walking/running
            if (frontRightPaw != null)
                frontRightPaw.localRotation = origFrontRightRot * Quaternion.Euler(0f, wave * legSwingAngle * swingMultiplier, 0f);

            if (backLeftLeg != null)
                backLeftLeg.localRotation = origBackLeftRot * Quaternion.Euler(0f, wave * legSwingAngle * swingMultiplier, 0f);

            if (frontLeftPaw != null)
                frontLeftPaw.localRotation = origFrontLeftRot * Quaternion.Euler(0f, oppositeWave * legSwingAngle * swingMultiplier, 0f);

            if (backRightLeg != null)
                backRightLeg.localRotation = origBackRightRot * Quaternion.Euler(0f, oppositeWave * legSwingAngle * swingMultiplier, 0f);

            // Tail wagging
            if (tailPart != null)
                tailPart.localRotation = origTailRot * Quaternion.Euler(0f, 0f, Mathf.Sin(animTimer * 1.5f) * tailWagAngle * swingMultiplier);

            // Bob amount for body (unused in the original code, but can be used for subtle up-down movement)
            if (bodyPart != null)
                bodyPart.localPosition = origBodyPos + new Vector3(0f, doubleWave * bodyBobAmount, 0f);
        }
        else
        {
            // move the legs and paws back to their original positions when idle 
            float idleLerp = Time.deltaTime * 8f;

            if (frontRightPaw != null)
                frontRightPaw.localRotation = Quaternion.Slerp(frontRightPaw.localRotation, origFrontRightRot, idleLerp);
            if (frontLeftPaw != null)
                frontLeftPaw.localRotation = Quaternion.Slerp(frontLeftPaw.localRotation, origFrontLeftRot, idleLerp);
            if (backRightLeg != null)
                backRightLeg.localRotation = Quaternion.Slerp(backRightLeg.localRotation, origBackRightRot, idleLerp);
            if (backLeftLeg != null)
                backLeftLeg.localRotation = Quaternion.Slerp(backLeftLeg.localRotation, origBackLeftRot, idleLerp);

            if (bodyPart != null)
                bodyPart.localPosition = Vector3.Lerp(bodyPart.localPosition, origBodyPos, idleLerp);

            if (tailPart != null)
            {
                float idleTail = Mathf.Sin(Time.time * 2.5f) * (tailWagAngle * 0.25f);
                tailPart.localRotation = origTailRot * Quaternion.Euler(0f, 0f, idleTail);
            }
        }
    }

    private void ApplyFrontPawScaleFix()
    {
        if (frontRightPaw != null)
        {
            frontRightPaw.localScale = Vector3.Scale(origFrontRightScale, frontPawScaleMultiplier);
        }

        if (frontLeftPaw != null)
        {
            frontLeftPaw.localScale = Vector3.Scale(origFrontLeftScale, frontPawScaleMultiplier);
        }
    }

    private Transform FindChildRecursive(Transform parent, string exactName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == exactName)
                return child;

            Transform found = FindChildRecursive(child, exactName);
            if (found != null)
                return found;
        }
        return null;
    }
}