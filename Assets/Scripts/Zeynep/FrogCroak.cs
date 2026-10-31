
using UnityEngine;

public class FrogCroak : MonoBehaviour
{
    [Header("Croak Settings")]
    [SerializeField] private KeyCode croakKey = KeyCode.E;
    [SerializeField] private float croakCooldown = 15f;
    [SerializeField] private float healAmount = 10f;

    [Header("References")]
    [SerializeField] private PondHealthManager pondHealthManager;
    [SerializeField] private AudioClip croakSound;

    private AudioSource audioSource;
    private float cooldownTimer;

    public float CooldownRemaining => cooldownTimer;
    public float CooldownDuration => croakCooldown;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (pondHealthManager == null)
            pondHealthManager = FindFirstObjectByType<PondHealthManager>();
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer = Mathf.Max(
                0f,
                cooldownTimer - Time.deltaTime
            );

        if (Input.GetKeyDown(croakKey))
            Croak();
    }

    private void Croak()
    {
        if (cooldownTimer > 0f)
        {
            Debug.Log($"Výraklama için {cooldownTimer:F1} saniye bekle.");
            return;
        }

        if (pondHealthManager == null)
        {
            Debug.LogWarning("PondHealthManager bulunamadý!");
            return;
        }

        cooldownTimer = croakCooldown;

        if (audioSource != null && croakSound != null)
            audioSource.PlayOneShot(croakSound);

        pondHealthManager.Heal(healAmount);

        Debug.Log("Výrak! Gölet iyileþiyor.");
    }
}
