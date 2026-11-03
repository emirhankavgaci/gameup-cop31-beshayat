using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider))]
public class WaterLevelController : MonoBehaviour
{
    [Header("Water Amount Settings")]
    [SerializeField] private float maxWaterAmount = 100f; 
    [SerializeField] private float baseDrainDurationSeconds = 60f; 
    [SerializeField] private float droughtEscalationPerSecond = 0.015f;
    [SerializeField] private float minDrainRateFloor = 0.35f; 
    [SerializeField] private float waterFillLerpSpeed = 25f;

    [Header("Water Level Settings")]
    [SerializeField] private bool useScenePositionAsMaxY = true; 
    [SerializeField] private float maxWaterY = 1.0f; 
    [SerializeField] private float minWaterY = -0.5f; 
    [SerializeField] private Transform damCeilingReference;
    [SerializeField] private float damMaxWaterOffset = -0.15f; 

    [Header("Water Appearance")]
    [SerializeField] private bool solidWaterSurface = false; 
    [SerializeField] private bool renderBehindPlayerAndDam = true; 
    [SerializeField] private int waterRenderQueue = 2005; 
    [SerializeField] private float waterSpeedMultiplier = 0.75f; 

    [Header("Water Flow")]
    [SerializeField] private Vector2 flowSpeed = new Vector2(0f, 0.5f);

    [Header("Water Bar")]
    [SerializeField] private Slider waterBarSlider; 
    [SerializeField] private Image waterBarFillImage;

    private float currentWaterAmount;
    private float targetWaterAmount;
    private float baseDrainRate; // 100 / 60 = 1.666f per second
    private float totalDrainReduction = 0f;
    private float elapsedDroughtTime = 0f;

    private float currentWaterY;
    private bool isDryingActive = true;
    private bool isRiverSaved = false;
    private bool hasRiverDriedOutTriggered = false;

    private BoxCollider boxCollider;
    private Renderer rend;
    private Vector2 currentTextureOffset;

    public event Action<float> OnWaterLevelRatioChanged;

    public event Action OnRiverDriedOut;

    public event Action OnRiverSaved;

    public float CurrentWaterRatio => Mathf.Clamp01(currentWaterAmount / maxWaterAmount);
    public float CurrentWaterAmount => currentWaterAmount;
    public bool IsRiverSaved => isRiverSaved;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = !solidWaterSurface; // If solidWaterSurface is true, the water will have a solid collider; otherwise, it will be a trigger.

        rend = GetComponent<Renderer>();
        if (rend != null && rend.material != null)
        {
            currentTextureOffset = rend.material.mainTextureOffset;

            // If the water should render behind the player and dam, adjust the render queue and disable ZWrite to prevent depth conflicts
            if (renderBehindPlayerAndDam)
            {
                Material[] mats = rend.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null)
                    {
                        mats[i].SetInt("_ZWrite", 0);
                        mats[i].renderQueue = waterRenderQueue;
                    }
                }
            }
        }
    }

    private void Start()
    {
        if (useScenePositionAsMaxY)
        {
            float depthRange = Mathf.Max(0.1f, Mathf.Abs(maxWaterY - minWaterY));
            maxWaterY = transform.position.y;
            minWaterY = maxWaterY - depthRange;
        }

        // If a dam ceiling reference is provided, ensure the water level does not exceed the ceiling 
        if (damCeilingReference != null)
        {
            maxWaterY = Mathf.Min(maxWaterY, damCeilingReference.position.y + damMaxWaterOffset);
        }

        baseDrainRate = maxWaterAmount / Mathf.Max(1f, baseDrainDurationSeconds);
        currentWaterAmount = maxWaterAmount;
        targetWaterAmount = maxWaterAmount;
        currentWaterY = maxWaterY;
        hasRiverDriedOutTriggered = false;

        transform.position = new Vector3(transform.position.x, currentWaterY, transform.position.z);
        UpdateWaterUI(1f);
        OnWaterLevelRatioChanged?.Invoke(1f);
    }

    private void OnDestroy()
    {
        // Reset events to avoid memory leaks or dangling references
        OnWaterLevelRatioChanged = null;
        OnRiverDriedOut = null;
        OnRiverSaved = null;
    }

    private void Update()
    {
        // Water surface texture scrolling
        if (rend != null && rend.material != null)
        {
            currentTextureOffset += flowSpeed * Time.deltaTime;
            rend.material.mainTextureOffset = currentTextureOffset;
        }

        // if drying is active and the river hasn't been saved, decrease the water amount over time
        if (isDryingActive && !isRiverSaved)
        {
            elapsedDroughtTime += Time.deltaTime;

            // Zaman geçtikçe kuraklık baskısı artar; böylece baraj tamamlanmazsa su kaçınılmaz olarak tükenir
            float escalation = elapsedDroughtTime * droughtEscalationPerSecond;
            float effectiveDrainRate = Mathf.Max(minDrainRateFloor, baseDrainRate - totalDrainReduction) + escalation;

            targetWaterAmount -= effectiveDrainRate * Time.deltaTime;
        }

        // Fix water amount to be within bounds of 0-100
        targetWaterAmount = Mathf.Clamp(targetWaterAmount, 0f, maxWaterAmount);
        currentWaterAmount = Mathf.MoveTowards(currentWaterAmount, targetWaterAmount, waterFillLerpSpeed * Time.deltaTime);

        // Calculate the water level ratio
        float ratio = Mathf.Clamp01(currentWaterAmount / maxWaterAmount);

        float effectiveMaxY = maxWaterY;
        if (damCeilingReference != null)
        {
            effectiveMaxY = Mathf.Min(maxWaterY, damCeilingReference.position.y + damMaxWaterOffset);
        }

        currentWaterY = Mathf.Lerp(minWaterY, effectiveMaxY, ratio);
        transform.position = new Vector3(transform.position.x, currentWaterY, transform.position.z);

        // UI update for water bar
        UpdateWaterUI(ratio);
        OnWaterLevelRatioChanged?.Invoke(ratio);

        // Trigger the river dried out event if the water amount reaches zero
        if (currentWaterAmount <= 0.01f)
        {
            if (!hasRiverDriedOutTriggered)
            {
                hasRiverDriedOutTriggered = true;
                OnRiverDriedOut?.Invoke();
            }
        }
        else
        {
            hasRiverDriedOutTriggered = false;
        }
    }

    private void UpdateWaterUI(float ratio)
    {
        if (waterBarSlider != null && waterBarSlider.gameObject != null)
        {
            waterBarSlider.value = waterBarSlider.maxValue > 1.5f ? ratio * maxWaterAmount : ratio;
        }

        if (waterBarFillImage != null && waterBarFillImage.gameObject != null)
        {
            waterBarFillImage.fillAmount = ratio;
        }
    }

    public void AddWaterByDebris(float waterBonusAmount, float drainReductionAmount = 0f)
    {
        targetWaterAmount = Mathf.Clamp(targetWaterAmount + waterBonusAmount, 0f, maxWaterAmount);
        totalDrainReduction += Mathf.Max(0f, drainReductionAmount);
    }

    public void CompleteDamAndSaveRiver()
    {
        isRiverSaved = true;
        isDryingActive = false;
        targetWaterAmount = maxWaterAmount;
        OnRiverSaved?.Invoke();
    }

    public void SetDryingState(bool active)
    {
        isDryingActive = active;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerMovement3D>(out PlayerMovement3D player))
        {
            player.ApplySpeedModifier(waterSpeedMultiplier);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerMovement3D>(out PlayerMovement3D player))
        {
            player.ResetSpeed();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.TryGetComponent<PlayerMovement3D>(out PlayerMovement3D player))
        {
            player.ApplySpeedModifier(waterSpeedMultiplier);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.TryGetComponent<PlayerMovement3D>(out PlayerMovement3D player))
        {
            player.ResetSpeed();
        }
    }
}