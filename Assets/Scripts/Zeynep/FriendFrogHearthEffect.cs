
using System.Collections;
using UnityEngine;

public class FriendFrogHeartEffect : MonoBehaviour
{
    [Header("Heart Settings")]
    [SerializeField] private float height = 1.5f;
    [SerializeField] private float riseDistance = 0.8f;
    [SerializeField] private float duration = 1.2f;
    [SerializeField] private float heartSize = 0.12f;

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    public void Play()
    {
        StartCoroutine(ShowHeart());
    }

    private IEnumerator ShowHeart()
    {
        GameObject heartObject = new GameObject("HappyHeart");

        heartObject.transform.position =
            transform.position + Vector3.up * height;

        TextMesh textMesh = heartObject.AddComponent<TextMesh>();

        // Unicode kalp karakteri
        textMesh.text = "\u2665";
        textMesh.fontSize = 64;
        textMesh.characterSize = heartSize;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = new Color(1f, 0.3f, 0.6f);

        Vector3 startPosition = heartObject.transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / duration);

            heartObject.transform.position =
                startPosition + Vector3.up * (riseDistance * progress);

            // Kalp kameraya dönük kalır.
            if (mainCamera != null)
            {
                heartObject.transform.rotation =
                    mainCamera.transform.rotation;
            }

            // Yavaşça saydamlaşır.
            Color color = textMesh.color;
            color.a = 1f - progress;
            textMesh.color = color;

            yield return null;
        }

        Destroy(heartObject);
    }
}
