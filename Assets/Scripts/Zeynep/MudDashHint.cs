
using UnityEngine;

public class MudDashHint : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string mudTag = "Mud";

    [Header("UI")]
    [SerializeField]
    private string hintText =
        "Camurdasin! Hizlanmak icin SHIFT'e bas!";

    private int mudContacts;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(mudTag))
            mudContacts++;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(mudTag))
            mudContacts = Mathf.Max(0, mudContacts - 1);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(mudTag))
            mudContacts++;
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag(mudTag))
            mudContacts = Mathf.Max(0, mudContacts - 1);
    }

    private void OnGUI()
    {
        if (mudContacts <= 0)
            return;

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 20;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;

        float width = Mathf.Min(520f, Screen.width - 30f);

        GUI.Box(
            new Rect(
                (Screen.width - width) / 2f,
                Screen.height - 165f,
                width,
                55f),
            hintText,
            style);
    }
}
