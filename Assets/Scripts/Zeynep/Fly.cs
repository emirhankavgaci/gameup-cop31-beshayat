
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Fly : MonoBehaviour
{
    [Header("Food")]
    [SerializeField] private int foodValue = 1;

    [Header("Catch Settings")]
    [SerializeField] private float destroyDelay = 0.6f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem catchEffectPrefab;

    [Header("Fly Audio")]
    [SerializeField] private AudioClip flySound;
    [SerializeField, Range(0f, 1f)]
    private float flyVolume = 0.25f;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 12f;

    private AudioSource audioSource;
    private bool isCaught = false;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
        audioSource.volume = flyVolume;
    }

    private void Start()
    {
        if (flySound == null)
            return;

        audioSource.clip = flySound;
        audioSource.Play();
    }

    public void Catch(FrogFoodCollector collector)
    {
        if (isCaught)
            return;

        isCaught = true;

        // Sinek yakalandýðýnda uçma sesini durdur.
        audioSource.Stop();

        collector.AddFood(foodValue);

        StartCoroutine(CatchSequence());
    }

    private IEnumerator CatchSequence()
    {
        yield return new WaitForSeconds(destroyDelay);

        AudioManager.Instance?.PlayFlyEat();

        if (catchEffectPrefab != null)
        {
            ParticleSystem effect = Instantiate(
                catchEffectPrefab,
                transform.position,
                Quaternion.identity
            );

            effect.Play();

            float effectLifetime =
                effect.main.duration +
                effect.main.startLifetime.constantMax;

            Destroy(effect.gameObject, effectLifetime + 0.2f);
        }

        Destroy(gameObject);
    }
}
