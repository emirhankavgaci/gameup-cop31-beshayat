
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

        Debug.Log($"{friendName} beslendi ve mutlu oldu!");

        Interact();

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
