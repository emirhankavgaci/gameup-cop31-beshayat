using UnityEngine;

[RequireComponent(typeof(AudioSource))]

public class FriendFrog : MonoBehaviour
{
    [Header("Friend")]
    [SerializeField] private string friendName = "Mavi";

    [Header("Audio")]
    [SerializeField] private AudioClip frogSound;

    private AudioSource audioSource;
    private bool isAlive = true;

    public string FriendName => friendName;
    public bool IsAlive => isAlive;

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

    public void Die()
    {
        if (!isAlive)
            return;

        isAlive = false;
        Debug.Log($"{friendName} artýk hayatta deðil.");
    }
}
