using UnityEngine;

public class FrogInteraction : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionRange = 2f;
    [SerializeField] private LayerMask interactionLayers = ~0;

    private FriendFrog nearestFriend;

    private void Update()
    {
        FindNearestFriend();

        if (nearestFriend != null &&
            Input.GetKeyDown(KeyCode.E))
        {
            nearestFriend.Interact();
        }
    }

    private void FindNearestFriend()
    {
        nearestFriend = null;
        float nearestDistance = interactionRange;

        Collider[] nearby = Physics.OverlapSphere(
            transform.position,
            interactionRange,
            interactionLayers
        );

        foreach (Collider hit in nearby)
        {
            FriendFrog friend =
                hit.GetComponentInParent<FriendFrog>();

            if (friend == null || !friend.IsAlive)
                continue;

            float distance = Vector3.Distance(
                transform.position,
                friend.transform.position
            );

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestFriend = friend;
            }
        }
    }

    private void OnGUI()
    {
        if (nearestFriend == null)
            return;

        GUI.Label(
            new Rect(
                Screen.width / 2f - 90f,
                Screen.height - 100f,
                220f,
                30f
            ),
            $"[E] {nearestFriend.FriendName} ile etkileþim"
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            interactionRange
        );
    }
}
