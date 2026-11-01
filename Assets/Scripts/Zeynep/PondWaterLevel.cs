
using UnityEngine;

public class PondWaterLevel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PondHealthManager pondHealth;
    [SerializeField] private Transform waterTransform;

    [Header("Water Level")]
    [SerializeField] private float minWaterHeight = -1f;
    [SerializeField] private float maxWaterHeight = 1f;
    [SerializeField] private float waterRiseSpeed = 0.5f;

    [Header("Water Audio")]
    [SerializeField] private AudioClip waterRiseSound;
    [SerializeField, Range(0f, 1f)]
    private float waterVolume = 0.3f;

    private AudioSource waterAudioSource;

    private void Awake()
    {
        waterAudioSource = gameObject.AddComponent<AudioSource>();

        waterAudioSource.clip = waterRiseSound;
        waterAudioSource.playOnAwake = false;
        waterAudioSource.loop = true;
        waterAudioSource.spatialBlend = 0f;
        waterAudioSource.volume = waterVolume;
    }

    private void Start()
    {
        if (pondHealth == null || waterTransform == null)
        {
            Debug.LogError(
                "PondWaterLevel: Referanslar eksik!",
                this
            );

            enabled = false;
            return;
        }

        UpdateWaterLevel(true);
    }

    private void Update()
    {
        UpdateWaterLevel(false);
    }

    private void UpdateWaterLevel(bool instant)
    {
        float health = pondHealth.HealthPercentage;

        float targetHeight = Mathf.Lerp(
            minWaterHeight,
            maxWaterHeight,
            health
        );

        Vector3 position = waterTransform.localPosition;

        float currentHeight = position.y;

        position.y = instant
            ? targetHeight
            : Mathf.MoveTowards(
                currentHeight,
                targetHeight,
                waterRiseSpeed * Time.deltaTime
            );

        waterTransform.localPosition = position;

        if (instant)
        {
            StopWaterSound();
            return;
        }

        bool isRising =
            position.y > currentHeight + 0.00001f;

        if (isRising)
            PlayWaterSound();
        else
            StopWaterSound();
    }

    private void PlayWaterSound()
    {
        if (waterRiseSound == null)
            return;

        if (!waterAudioSource.isPlaying)
        {
            waterAudioSource.clip = waterRiseSound;
            waterAudioSource.Play();
        }
    }

    private void StopWaterSound()
    {
        if (waterAudioSource.isPlaying)
            waterAudioSource.Stop();
    }

    private void OnDisable()
    {
        if (waterAudioSource != null)
            waterAudioSource.Stop();
    }
}
