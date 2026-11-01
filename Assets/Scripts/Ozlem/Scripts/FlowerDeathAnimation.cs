
using System.Collections;
using UnityEngine;

public class FlowerDeathAnimation : MonoBehaviour
{
    [Header("Death Animation")]
    [SerializeField] private float duration = 1.2f;
    [SerializeField] private float rotationSpeed = 540f;
    [SerializeField] private float sinkDistance = 0.8f;

    private bool isDying;

    public void PlayDeath()
    {
        if (isDying)
            return;

        isDying = true;

        // Çiçe?in tekrar toplanmas?n? engelle.
        Collider[] colliders = GetComponentsInChildren<Collider>();

        foreach (Collider col in colliders)
            col.enabled = false;

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float progress = elapsed / duration;

            transform.Rotate(
                Vector3.up,
                rotationSpeed * Time.deltaTime,
                Space.World
            );

            transform.position = startPosition +
                Vector3.down * (sinkDistance * progress);

            elapsed += Time.deltaTime;

            yield return null;
        }

        Destroy(gameObject);
    }
}
