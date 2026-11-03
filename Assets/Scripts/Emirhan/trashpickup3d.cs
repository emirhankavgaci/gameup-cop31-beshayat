using System;
using UnityEngine;

public enum DebrisType  //The type of debris determines the water restoration and drain reduction effects when added to the dam
{
    Trash, 
    Branch  
}

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class TrashPickup3D : MonoBehaviour
{
    [Header("Type of Debris")] 
    [SerializeField] private DebrisType debrisType = DebrisType.Trash;
    [SerializeField] private float customWaterBonus = -1f; 
    [SerializeField] private float customDrainReduction = -1f; 

    [Header("Interface and Detection")]
    [SerializeField] private GameObject interactionPromptUI;
    [SerializeField] private float pickupTriggerRadius = 1.2f; // Player's debris detection radius

    [Header("Socket Placement")]
    [SerializeField] private Vector3 holdLocalPosition = new Vector3(0f, 0f, 0.5f);
    [SerializeField] private Vector3 holdLocalRotation = Vector3.zero;

    [Header("Fall Respawn Protection and Water Surface Layer")]
    [SerializeField] private float fallRespawnY = -15f;
    [SerializeField] private int itemRenderQueue = 2010; // For placing items in the upper layer to avoid collision with water and dam

    private Collider physicsCollider;
    private SphereCollider pickupTriggerCollider;
    private Rigidbody itemRb;
    private Vector3 initialSpawnPosition;
    private Quaternion initialSpawnRotation;
    private bool isCarried = false;

    public bool IsCarried => isCarried;
    public DebrisType Type => debrisType;

    public float WaterRestoreAmount => customWaterBonus >= 0f
        ? customWaterBonus
        : (debrisType == DebrisType.Branch ? 10f : 5f);

    public float DrainRateReduction => customDrainReduction >= 0f
        ? customDrainReduction
        : (debrisType == DebrisType.Branch ? 0.06f : 0.025f);

    public static event Action<TrashPickup3D> OnTrashPickedUp;
    public static event Action<TrashPickup3D> OnTrashDropped;


    public static void ResetStaticEvents() // Call this method when changing scenes to avoid null reference errors from lingering event subscriptions
    {
        OnTrashPickedUp = null;
        OnTrashDropped = null;
    }

    private void Awake()
    {
        initialSpawnPosition = transform.position;
        initialSpawnRotation = transform.rotation;

        itemRb = GetComponent<Rigidbody>();
        itemRb.useGravity = true;
        itemRb.isKinematic = false;

        // Renderer for water surface layer placement
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] mats = renderers[i].materials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] != null)
                {
                    mats[m].renderQueue = itemRenderQueue;
                }
            }
        }

        // Main collider is made solid for physics
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

        // Add a trigger collider for player detection if it doesn't exist
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

    private void Update()
    {
        // If the item is not being carried and falls below a certain Y level, reset its position to the initial spawn point
        if (!isCarried && transform.position.y < fallRespawnY)
        {
            if (itemRb != null && !itemRb.isKinematic)
            {
                itemRb.linearVelocity = Vector3.zero;
                itemRb.angularVelocity = Vector3.zero;
            }
            transform.position = initialSpawnPosition + Vector3.up * 0.2f;
            transform.rotation = initialSpawnRotation;
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

        // When the item is picked up, disable its physics and colliders to prevent interference with the player
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

        // When the item is dropped, re-enable its physics and colliders so it can interact with the environment
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

        // Activate physics for the item so it can fall and collide with the environment
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