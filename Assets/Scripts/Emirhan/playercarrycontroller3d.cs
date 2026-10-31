using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Unity 6 (6000.x) ve Yeni Input System uyumlu çöp / kütük taşıma kontrolcüsü.
/// Oyuncu (Player) objesine eklenir.
/// </summary>
public class PlayerCarryController3D : MonoBehaviour
{
    [Header("Ağız Yuvası")]
    [SerializeField] private Transform mouthSocket;
    [SerializeField] private float dropForwardOffset = 0.8f;
    [SerializeField] private float dropHeightOffset = 0.2f;

    [Header("Girdi Ayarı (0: Sol Tık, 1: Sağ Tık, 2: Orta Tık)")]
    [SerializeField] private int mouseButtonIndex = 0;

    private TrashPickup3D nearbyItem = null;
    private TrashPickup3D currentItem = null;

    public bool HasItem => currentItem != null;
    public Transform MouthSocket => mouthSocket != null ? mouthSocket : transform;

    private void Update()
    {
        HandleHoldInput();
    }

    private void HandleHoldInput()
    {
        ButtonControl mouseButton = GetMouseButton(mouseButtonIndex);
        if (mouseButton == null) return;

        // Tuşa ilk basıldığı anda yakındaki objeyi tut
        if (mouseButton.wasPressedThisFrame)
        {
            if (nearbyItem != null && !HasItem)
            {
                GrabItem(nearbyItem);
            }
        }

        // Tuş bırakıldığında eldeki objeyi bırak
        if (mouseButton.wasReleasedThisFrame)
        {
            if (HasItem)
            {
                ReleaseItem();
            }
        }

        // Güvenlik kontrolü: Tuş basılı değilse ve hala obje tutuluyorsa bırak
        if (HasItem && !mouseButton.isPressed)
        {
            ReleaseItem();
        }
    }

    /// <summary>
    /// Yeni Input System üzerinden seçilen mouse butonunu döndürür.
    /// </summary>
    private ButtonControl GetMouseButton(int index)
    {
        if (Mouse.current == null) return null;

        return index switch
        {
            0 => Mouse.current.leftButton,
            1 => Mouse.current.rightButton,
            2 => Mouse.current.middleButton,
            _ => Mouse.current.leftButton
        };
    }

    private void GrabItem(TrashPickup3D item)
    {
        currentItem = item;
        nearbyItem = null;
        currentItem.AttachToSocket(MouthSocket);
    }

    private void ReleaseItem()
    {
        if (!HasItem) return;

        Vector3 dropPosition = transform.position + (transform.forward * dropForwardOffset) + (Vector3.up * dropHeightOffset);
        TrashPickup3D itemToDrop = currentItem;
        currentItem = null;
        itemToDrop.Drop(dropPosition);
    }

    public void SetNearbyItem(TrashPickup3D item)
    {
        if (!HasItem)
        {
            nearbyItem = item;
        }
    }

    public void ClearNearbyItem(TrashPickup3D item)
    {
        if (nearbyItem == item)
        {
            nearbyItem = null;
        }
    }

    public TrashPickup3D ReleaseItemForDam()
    {
        if (!HasItem) return null;

        TrashPickup3D item = currentItem;
        currentItem = null;
        return item;
    }
}