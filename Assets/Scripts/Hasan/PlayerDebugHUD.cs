using UnityEngine;

/// <summary>
/// CANOPY: The Last Run - Parkur sistemleri debug paneli.
/// Sol ust debug HUD kullanici talebi dogrultusunda kaldirilmistir.
/// </summary>
public class PlayerDebugHUD : MonoBehaviour
{
    [Header("Gorunum Ayarlari")]
    [SerializeField] private bool showDebugHud = false;

    private void Awake()
    {
        showDebugHud = false;
        enabled = false;
    }

    private void OnGUI()
    {
        // Sol ust debug paneli tamamen kaldirildi
        return;
    }
}
