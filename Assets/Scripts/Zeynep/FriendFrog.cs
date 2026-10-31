
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class FriendFrog : MonoBehaviour
{
    [Header("Friend")]
    [SerializeField] private string friendName = "Mavi";

    [Header("Audio")]
    [SerializeField] private AudioClip frogSound;

    [Header("Feeding")]
    [SerializeField] private int requiredFood = 1;

    [Header("Pond Restoration")]
    [SerializeField] private float healAmount = 20f;

    [Header("Effects")]
    [SerializeField] private FriendFrogHeartEffect heartEffect;

    private AudioSource audioSource;
    private bool isAlive = true;
    private bool isFed = false;

    public string FriendName => friendName;
    public bool IsAlive => isAlive;
    public bool IsFed => isFed;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        if (heartEffect == null)
            heartEffect = GetComponent<FriendFrogHeartEffect>();
    }

    public void Interact()
    {
        if (!isAlive)
            return;

        Debug.Log($"{friendName}: Výrak!");

        if (frogSound != null)
            audioSource.PlayOneShot(frogSound);
    }


    public bool Feed(FrogFoodCollector collector)
    {
        if (!isAlive || isFed || collector == null)
            return false;

        if (!collector.TrySpendFood(requiredFood))
        {
            Debug.Log($"{friendName} için yeterli besin yok.");
            return false;
        }

        isFed = true;

        if (heartEffect != null)
            heartEffect.Play();

        Debug.Log($"{friendName} beslendi ve mutlu oldu!");

        // Arkadaþ kurbaða mutlu bir ses çýkarýr.
        Interact();

        // Göletin saðlýðýný artýrýr.
        PondHealthManager pond = FindFirstObjectByType<PondHealthManager>();

        if (pond != null)
            pond.Heal(healAmount);
        else
            Debug.LogWarning("PondHealthManager bulunamadý!");

        return true;
    }


    public void Die()
    {
        if (!isAlive)
            return;

        isAlive = false;
        Debug.Log($"{friendName} artýk hayatta deðil.");
    }
}
