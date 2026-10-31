using System;
using UnityEngine;

/// <summary>
/// Unity 6 (6000.x) uyumlu, toplanabilir çöp / dal / kütük (Debris) objesi kontrolcüsü.
/// Hem gerçek fizikle yere düşer (katı Collider + Rigidbody) hem de otomatik Trigger alanı ile oyuncuyu algılar.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class TrashPickup3D : MonoBehaviour
{
    [Header("Arayüz ve Algılama")]
    [SerializeField] private GameObject interactionPromptUI;
    [SerializeField] private float pickupTriggerRadius = 1.2f; // Oyuncunun çöpü algılama yarıçapı

    [Header("Soket Yerleşimi")]
    [SerializeField] private Vector3 holdLocalPosition = new Vector3(0f, 0f, 0.5f);
    [SerializeField] private Vector3 holdLocalRotation = Vector3.zero;

    [Header("Baraj Su Katkısı")]
    [SerializeField] private float waterRestoreAmount = 0.25f; // Baraja yerleştirildiğinde nehir suyunu yükseltme miktarı

    private Collider physicsCollider;
    private SphereCollider pickupTriggerCollider;
    private Rigidbody itemRb;
    private bool isCarried = false;

    public bool IsCarried => isCarried;
    public float WaterRestoreAmount => waterRestoreAmount;

    public static event Action<TrashPickup3D> OnTrashPickedUp;
    public static event Action<TrashPickup3D> OnTrashDropped;

    private void Awake()
    {
        itemRb = GetComponent<Rigidbody>();
        itemRb.useGravity = true;
        itemRb.isKinematic = false;

        // 1. Ana Collider'ı katı (isTrigger = false) yap ki yerçekimiyle düşerken zemine çarpıp dursun
        Collider[] existingColliders = GetComponents<Collider>();
        for (int i = 0; i < existingColliders.Length; i++)
        {
            if (!existingColliders[i].isTrigger && physicsCollider == null)
            {
                physicsCollider = existingColliders[i];
            }
            else if (existingColliders[i].isTrigger && existingColliders[i] is SphereCollider sphere)
            {
                pickupTriggerCollider = sphere;
            }
        }

        if (physicsCollider == null)
        {
            physicsCollider = existingColliders[0];
            physicsCollider.isTrigger = false;
        }

        // 2. Oyuncunun yaklaştığını algılamak için ikinci bir Trigger SphereCollider yoksa otomatik ekle
        if (pickupTriggerCollider == null)
        {
            pickupTriggerCollider = gameObject.AddComponent<SphereCollider>();
            pickupTriggerCollider.isTrigger = true;
            pickupTriggerCollider.radius = pickupTriggerRadius;
        }

        if (interactionPromptUI != null)
        {
            interactionPromptUI.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCarried) return;

        if (other.TryGetComponent<PlayerCarryController3D>(out var carrier))
        {
            carrier.SetNearbyItem(this);

            if (interactionPromptUI != null)
            {
                interactionPromptUI.SetActive(true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (isCarried) return;

        if (other.TryGetComponent<PlayerCarryController3D>(out var carrier))
        {
            carrier.ClearNearbyItem(this);

            if (interactionPromptUI != null)
            {
                interactionPromptUI.SetActive(false);
            }
        }
    }

    public void AttachToSocket(Transform socketTransform)
    {
        isCarried = true;

        if (interactionPromptUI != null)
        {
            interactionPromptUI.SetActive(false);
        }

        // Taşınırken yerçekimini ve çarpışmaları kapat
        if (itemRb != null)
        {
            if (!itemRb.isKinematic)
            {
                itemRb.linearVelocity = Vector3.zero;
                itemRb.angularVelocity = Vector3.zero;
            }
            itemRb.isKinematic = true;
            itemRb.detectCollisions = false;
        }

        if (physicsCollider != null) physicsCollider.enabled = false;
        if (pickupTriggerCollider != null) pickupTriggerCollider.enabled = false;

        transform.SetParent(socketTransform);
        transform.localPosition = holdLocalPosition;
        transform.localRotation = Quaternion.Euler(holdLocalRotation);

        OnTrashPickedUp?.Invoke(this);
    }

    public void Drop(Vector3 dropWorldPosition)
    {
        isCarried = false;
        transform.SetParent(null);
        transform.position = dropWorldPosition;

        // Yere bırakıldığında katı Collider'ı ve Trigger'ı tekrar aç
        if (physicsCollider != null)
        {
            physicsCollider.enabled = true;
            physicsCollider.isTrigger = false;
        }

        if (pickupTriggerCollider != null)
        {
            pickupTriggerCollider.enabled = true;
            pickupTriggerCollider.isTrigger = true;
        }

        // Fizik motorunu ve yerçekimini aktif et ki nesne yere düşsün
        if (itemRb != null)
        {
            itemRb.isKinematic = false;
            itemRb.useGravity = true;
            itemRb.detectCollisions = true;
            itemRb.linearVelocity = Vector3.zero;
            itemRb.angularVelocity = Vector3.zero;
        }

        OnTrashDropped?.Invoke(this);
    }

    public void LockIntoDam(Transform damParentSlot)
    {
        isCarried = false;

        if (interactionPromptUI != null)
        {
            interactionPromptUI.SetActive(false);
        }

        transform.SetParent(damParentSlot);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        if (physicsCollider != null)
        {
            physicsCollider.enabled = true;
            physicsCollider.isTrigger = false;
        }

        if (pickupTriggerCollider != null)
        {
            pickupTriggerCollider.enabled = false;
        }

        if (itemRb != null)
        {
            itemRb.isKinematic = true;
            itemRb.detectCollisions = true;
        }

        enabled = false;
    }
}