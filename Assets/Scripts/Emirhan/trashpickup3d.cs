using System;
using UnityEngine;

public enum DebrisType
{
    Trash,  // Çöp: Düşük su katkısı (+5), düşük sızıntı azaltma
    Branch  // Dal / Kütük: Yüksek su katkısı (+10), yüksek sızıntı azaltma
}

/// <summary>
/// Unity 6 (6000.x) uyumlu, toplanabilir çöp / dal / kütük (Debris) objesi kontrolcüsü.
/// Hem gerçek fizikle yere düşer (katı Collider + Rigidbody) hem de otomatik Trigger alanı ile oyuncuyu algılar.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class TrashPickup3D : MonoBehaviour
{
    [Header("Nesne Türü (Çöp vs Dal)")]
    [SerializeField] private DebrisType debrisType = DebrisType.Trash;
    [SerializeField] private float customWaterBonus = -1f; // -1 ise türe göre otomatik (Çöp: +5, Dal: +10)
    [SerializeField] private float customDrainReduction = -1f; // -1 ise türe göre otomatik (Çöp: 0.03, Dal: 0.07)

    [Header("Arayüz ve Algılama")]
    [SerializeField] private GameObject interactionPromptUI;
    [SerializeField] private float pickupTriggerRadius = 1.2f; // Oyuncunun çöpü algılama yarıçapı

    [Header("Soket Yerleşimi")]
    [SerializeField] private Vector3 holdLocalPosition = new Vector3(0f, 0f, 0.5f);
    [SerializeField] private Vector3 holdLocalRotation = Vector3.zero;

    private Collider physicsCollider;
    private SphereCollider pickupTriggerCollider;
    private Rigidbody itemRb;
    private bool isCarried = false;

    public bool IsCarried => isCarried;
    public DebrisType Type => debrisType;

    /// <summary>
    /// Baraja eklendiğinde anlık kazandıracağı su miktarı (100 üzerinden: Çöp = +5, Dal = +10).
    /// </summary>
    public float WaterRestoreAmount => customWaterBonus >= 0f
        ? customWaterBonus
        : (debrisType == DebrisType.Branch ? 10f : 5f);

    /// <summary>
    /// Baraja eklendiğinde suyun saniyelik azalma hızını düşürme miktarı.
    /// </summary>
    public float DrainRateReduction => customDrainReduction >= 0f
        ? customDrainReduction
        : (debrisType == DebrisType.Branch ? 0.06f : 0.025f);

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

    public void LockIntoDam(Transform damParentSlot, Vector3 localOffset, Quaternion localRot)
    {
        isCarried = false;

        if (interactionPromptUI != null)
        {
            interactionPromptUI.SetActive(false);
        }

        transform.SetParent(damParentSlot);
        transform.localPosition = localOffset;
        transform.localRotation = localRot;

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
            if (!itemRb.isKinematic)
            {
                itemRb.linearVelocity = Vector3.zero;
                itemRb.angularVelocity = Vector3.zero;
            }
            itemRb.isKinematic = true;
            itemRb.detectCollisions = true;
        }

        enabled = false;
    }

    public void LockIntoDam(Transform damParentSlot)
    {
        LockIntoDam(damParentSlot, Vector3.zero, Quaternion.identity);
    }
}