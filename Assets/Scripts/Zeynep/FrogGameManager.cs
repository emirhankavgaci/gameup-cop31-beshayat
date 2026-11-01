
using UnityEngine;
using UnityEngine.SceneManagement;

public class FrogGameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PondHealthManager pondHealth;

    [Header("Win Conditions")]
    [SerializeField] private int requiredFedFrogs = 3;
    [SerializeField] private int requiredPlantedSeeds = 3;
    [SerializeField] private float requiredPondHealth = 100f;

    private FriendFrog[] friends;
    private PlantingSpot[] plantingSpots;

    private bool gameEnded;
    private bool playerWon;

    private int fedCount;
    private int plantedCount;

    private void Start()
    {
        Time.timeScale = 1f;

        if (pondHealth == null)
            pondHealth = FindFirstObjectByType<PondHealthManager>();

        friends = FindObjectsByType<FriendFrog>(
            FindObjectsSortMode.None);

        plantingSpots = FindObjectsByType<PlantingSpot>(
            FindObjectsSortMode.None);

        if (pondHealth == null)
        {
            Debug.LogError("PondHealthManager bulunamadý!");
            enabled = false;
            return;
        }

        if (friends.Length < requiredFedFrogs ||
            plantingSpots.Length < requiredPlantedSeeds)
        {
            Debug.LogWarning(
                "Sahnede kazanmak için yeterli kurbaða veya ekim noktasý yok!");
        }
    }

    private void Update()
    {
        if (gameEnded)
        {
            if (Input.GetKeyDown(KeyCode.R))
                RestartGame();

            return;
        }

        fedCount = 0;
        foreach (FriendFrog friend in friends)
        {
            if (friend != null && friend.IsFed)
                fedCount++;
        }

        plantedCount = 0;
        foreach (PlantingSpot spot in plantingSpots)
        {
            if (spot != null && spot.IsPlanted)
                plantedCount++;
        }

        if (pondHealth.CurrentHealth <= 0f)
        {
            EndGame(false);
        }
        else if (
            fedCount >= requiredFedFrogs &&
            plantedCount >= requiredPlantedSeeds &&
            pondHealth.CurrentHealth >= requiredPondHealth)
        {
            EndGame(true);
        }
    }

    private void EndGame(bool won)
    {
        gameEnded = true;
        playerWon = won;

        Time.timeScale = 0f;

        Debug.Log(won
            ? "Kazandýn! Gölet kurtarýldý."
            : "Kaybettin! Gölet kurudu.");
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex);
    }

    private void OnGUI()
    {
        if (!gameEnded)
            return;

        float width = 420f;
        float height = 180f;

        Rect rect = new Rect(
            (Screen.width - width) / 2f,
            (Screen.height - height) / 2f,
            width,
            height);

        GUI.Box(rect, "");

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 28;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor =
            playerWon ? Color.green : Color.red;

        GUI.Label(
            new Rect(rect.x, rect.y + 25f, width, 60f),
            playerWon ? "DOGA KURTARILDI!" : "GOLET KURUDU!",
            titleStyle);

        GUIStyle infoStyle = new GUIStyle(GUI.skin.label);
        infoStyle.fontSize = 18;
        infoStyle.alignment = TextAnchor.MiddleCenter;

        GUI.Label(
            new Rect(rect.x, rect.y + 100f, width, 40f),
            "Yeniden baslamak icin R tusuna bas",
            infoStyle);
    }
}
