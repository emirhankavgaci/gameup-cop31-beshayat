using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TMPro;

/// <summary>
/// Unity 6 (6000.x) ve Yeni Input System uyumlu çöp / kütük taşıma ve dinamik etkileşim prompt kontrolcüsü.
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

    [Header("Dinamik Etkileşim Promptu (HUD Text - Opsiyonel)")]
    [SerializeField] private TMP_Text interactionPromptText;
    [SerializeField] private bool useTurkishPrompt = true;

    private TrashPickup3D nearbyItem = null;
    private TrashPickup3D currentItem = null;

    public bool HasItem => currentItem != null;
    public TrashPickup3D NearbyItem => nearbyItem;
    public TrashPickup3D CurrentItem => currentItem;
    public Transform MouthSocket => mouthSocket != null ? mouthSocket : transform;

    private void Start()
    {
        UpdatePromptUI();
    }

    private void Update()
    {
        HandleHoldInput();
        UpdatePromptUI();
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
    /// Oyuncu nesneye yaklaştığında veya nesneyi taşırken ekrandaki "Hold {Buton}" yazısını günceller.
    /// </summary>
    private void UpdatePromptUI()
    {
        if (interactionPromptText == null || interactionPromptText.gameObject == null)
            return;

        string buttonName = GetButtonDisplayName(mouseButtonIndex);

        if (HasItem && currentItem != null)
        {
            interactionPromptText.gameObject.SetActive(true);
            interactionPromptText.text = useTurkishPrompt
                ? $"[{buttonName} Basılı Tut] (Baraja Götür)"
                : $"Hold [{buttonName}] (Carry to Dam)";
        }
        else if (nearbyItem != null)
        {
            interactionPromptText.gameObject.SetActive(true);
            string itemName = nearbyItem.Type == DebrisType.Branch
                ? (useTurkishPrompt ? "Dalı" : "Branch")
                : (useTurkishPrompt ? "Çöpü" : "Trash");

            interactionPromptText.text = useTurkishPrompt
                ? $"[{buttonName} Basılı Tut] ({itemName} Al)"
                : $"Hold [{buttonName}] (to Pick Up {itemName})";
        } 
        else
        {
            if (interactionPromptText.gameObject.activeSelf)
            {
                interactionPromptText.gameObject.SetActive(false);
            }
        }
    }

    public string GetButtonDisplayName(int index)
    {
        if (useTurkishPrompt)
        {
            return index switch
            {
                0 => "Sol Tık",
                1 => "Sağ Tık",
                2 => "Orta Tık",
                _ => "Sol Tık"
            };
        }

        return index switch
        {
            0 => "LMB",
            1 => "RMB",
            2 => "MMB",
            _ => "LMB"
        };
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
        UpdatePromptUI();
    }

    private void ReleaseItem()
    {
        if (!HasItem) return;

        Vector3 dropPosition = transform.position + (transform.forward * dropForwardOffset) + (Vector3.up * dropHeightOffset);
        TrashPickup3D itemToDrop = currentItem;
        currentItem = null;
        itemToDrop.Drop(dropPosition);
        UpdatePromptUI();
    }

    public void SetNearbyItem(TrashPickup3D item)
    {
        if (!HasItem)
        {
            nearbyItem = item;
            UpdatePromptUI();
        }
    }

    public void ClearNearbyItem(TrashPickup3D item)
    {
        if (nearbyItem == item)
        {
            nearbyItem = null;
            UpdatePromptUI();
        }
    }

    public TrashPickup3D ReleaseItemForDam()
    {
        if (!HasItem) return null;

        TrashPickup3D item = currentItem;
        currentItem = null;
        UpdatePromptUI();
        return item;
    }
}