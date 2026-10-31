
using System.Collections;
using UnityEngine;

public class Fly : MonoBehaviour
{
    [Header("Food")]
    [SerializeField] private int foodValue = 1;

    [Header("Catch Settings")]
    [SerializeField] private float destroyDelay = 0.6f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem catchEffectPrefab;

    private bool isCaught = false;

    public void Catch(FrogFoodCollector collector)
    {
        if (isCaught)
            return;

        isCaught = true;

        // Yiyecek puanýný ekle.
        collector.AddFood(foodValue);

        // Sineðin kaybolmasýný ve efekti geciktir.
        StartCoroutine(CatchSequence());
    }

    private IEnumerator CatchSequence()
    {
        // Dil çýkarma animasyonunu bekle.
        yield return new WaitForSeconds(destroyDelay);

        // Sineðin kaybolduðu noktada efekt oluþtur.
        if (catchEffectPrefab != null)
        {
            ParticleSystem effect = Instantiate(
                catchEffectPrefab,
                transform.position,
                Quaternion.identity
            );

            effect.Play();

            // Efekt bittikten sonra temizle.
            float effectLifetime =
                effect.main.duration +
                effect.main.startLifetime.constantMax;

            Destroy(effect.gameObject, effectLifetime + 0.2f);
        }

        // Efekt çýktýðý anda sineði yok et.
        Destroy(gameObject);
    }
}
