using System;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class DamBuilderController3D : MonoBehaviour
{
    [Header("Dam Settings")]
    [SerializeField] private int totalBlocks = 5; 
    [SerializeField] private int itemsPerBlock = 4; 
    [SerializeField] private bool winWhenDamCompleted = true; // decision to give a good ending when the dam is fully built

    [Header("Controller References")]
    [SerializeField] private WaterLevelController waterController;
    [SerializeField] private Transform[] blockVisuals; 

    [Header("Placement Settings")] // dam builder will automatically place the items in a grid-like structure based on these settings
    [SerializeField] private Vector3 buildStartOffset = new Vector3(-5f, 0f, 0f); 
    [SerializeField] private Vector3 placedItemLocalRotation = new Vector3(0f, 0f, 90f); 
    [SerializeField] private float blockSpacingX = 1.2f; 
    [SerializeField] private float itemLayerSpacingY = 0.25f;

    [Header("Render Queue")]
    [SerializeField] private int damRenderQueue = 2010; // dam render queue for it not to be hidden by water surface default is 2000

    private int currentPlacedItems = 0;
    private int currentCompletedBlocks = 0;
    private BoxCollider triggerZone;

    public int TotalRequiredItems => totalBlocks * itemsPerBlock;
    public int CurrentPlacedItems => currentPlacedItems;
    public int CurrentCompletedBlocks => currentCompletedBlocks;
    public bool IsDamCompleted => currentCompletedBlocks >= totalBlocks;

    // Events to notify other systems about dam progress

    public event Action<int, int, int> OnDamProgressUpdated;

    public event Action OnDamFullyBuilt;

    private void Awake()
    {
        triggerZone = GetComponent<BoxCollider>();
        triggerZone.isTrigger = true;

        if (waterController == null)
        {
            waterController = FindAnyObjectByType<WaterLevelController>();
        }

        ApplyRenderQueueToHierarchy(transform, damRenderQueue);

        // if block visuals are assigned, set them to inactive initially
        if (blockVisuals != null)
        {
            for (int i = 0; i < blockVisuals.Length; i++)
            {
                if (blockVisuals[i] != null)
                {
                    ApplyRenderQueueToHierarchy(blockVisuals[i], damRenderQueue);
                    blockVisuals[i].gameObject.SetActive(false);
                }
            }
        }
    }

    private void OnDestroy()
    {
        OnDamProgressUpdated = null;
        OnDamFullyBuilt = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryPlaceCarriedItem(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryPlaceCarriedItem(other);
    }

    private void TryPlaceCarriedItem(Collider other)
    {
        if (IsDamCompleted) return;

        if (other.TryGetComponent<PlayerCarryController3D>(out PlayerCarryController3D carrier))
        {
            if (carrier.HasItem)
            {
                TrashPickup3D item = carrier.ReleaseItemForDam();
                if (item != null)
                {
                    PlaceItemIntoDam(item);
                }
            }
        }
    }

    private void PlaceItemIntoDam(TrashPickup3D item) //block placement logic, item is placed in the correct position and rotation
    {
        int blockIndex = currentPlacedItems / itemsPerBlock; 
        int slotInBlock = currentPlacedItems % itemsPerBlock; 

        
        Vector3 slotLocalPos = buildStartOffset + new Vector3(
            blockIndex * blockSpacingX,
            slotInBlock * itemLayerSpacingY,
            0f
        );

        // rotate the item to be horizontal (laying flat) instead of vertical
        Quaternion horizontalRotation = Quaternion.Euler(placedItemLocalRotation);
        item.LockIntoDam(transform, slotLocalPos, horizontalRotation);
        ApplyRenderQueueToHierarchy(item.transform, damRenderQueue);

        currentPlacedItems++;
        int newCompletedBlocks = currentPlacedItems / itemsPerBlock;

        // deactivate the visual for the completed block
        if (newCompletedBlocks > currentCompletedBlocks)
        {
            currentCompletedBlocks = newCompletedBlocks;
            int completedIdx = currentCompletedBlocks - 1;

            if (blockVisuals != null && completedIdx >= 0 && completedIdx < blockVisuals.Length && blockVisuals[completedIdx] != null)
            {
                blockVisuals[completedIdx].gameObject.SetActive(true);
            }
        }

        // add water restoration and drain rate reduction using the item's properties 
        if (waterController != null)
        {
            waterController.AddWaterByDebris(item.WaterRestoreAmount, item.DrainRateReduction);
        }

        OnDamProgressUpdated?.Invoke(currentPlacedItems, TotalRequiredItems, currentCompletedBlocks);

        // is the dam fully built?
        if (currentPlacedItems >= TotalRequiredItems)
        {
            OnDamFullyBuilt?.Invoke();

            if (winWhenDamCompleted && waterController != null)
            {
                waterController.CompleteDamAndSaveRiver();
            }
        }
    }

    private void ApplyRenderQueueToHierarchy(Transform root, int queue)
    {
        if (root == null) return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] mats = renderers[i].materials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] != null)
                {
                    mats[m].renderQueue = queue;
                }
            }
        }
    }
}