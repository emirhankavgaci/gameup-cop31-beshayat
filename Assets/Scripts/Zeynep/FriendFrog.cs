using UnityEngine;

public class FriendFrog : MonoBehaviour
{
    [SerializeField] private string friendName = "Mavi";

    private bool isAlive = true;

    public string FriendName => friendName;
    public bool IsAlive => isAlive;

    public void Interact()
    {
        if (!isAlive)
            return;

        Debug.Log($"{friendName}: Merhaba! Birlikte yüzelim!");
    }

    public void Die()
    {
        if (!isAlive)
            return;

        isAlive = false;

        Debug.Log($"{friendName} artýk hayatta deðil.");

        // Ýleride ölüm animasyonu ekleyeceðiz.
    }
}
