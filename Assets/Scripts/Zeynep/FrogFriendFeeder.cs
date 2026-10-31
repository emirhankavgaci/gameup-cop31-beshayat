
using UnityEngine;

[RequireComponent(typeof(FrogFoodCollector))]
public class FrogFriendFeeder : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionRange = 3f;

    private FrogFoodCollector foodCollector;

    private void Awake()
    {
        foodCollector = GetComponent<FrogFoodCollector>();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.F))
            return;

        FriendFrog closestFriend = FindClosestFriend();

        if (closestFriend == null)
        {
            Debug.Log("Yakýnda beslenebilecek kurbaða yok.");
            return;
        }

        closestFriend.Feed(foodCollector);
    }

    private FriendFrog FindClosestFriend()
    {
        FriendFrog[] friends = FindObjectsByType<FriendFrog>(
            FindObjectsSortMode.None
        );

        FriendFrog closestFriend = null;
        float closestDistance = interactionRange;

        foreach (FriendFrog friend in friends)
        {
            if (!friend.IsAlive || friend.IsFed)
                continue;

            float distance = Vector3.Distance(
                transform.position,
                friend.transform.position
            );

            if (distance <= closestDistance)
            {
                closestDistance = distance;
                closestFriend = friend;
            }
        }

        return closestFriend;
    }
}
