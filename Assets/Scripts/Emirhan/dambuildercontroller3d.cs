using System;
using UnityEngine;

/// <summary>
/// Unity 6 (6000.x) uyumlu, 5 Blok x 4 İçerik (Toplam 20 Parça) Baraj İnşa Kontrolcüsü.
/// Oyuncu ağzında Çöp (+5 su) veya Dal (+10 su) ile baraj alanına girdiğinde parçayı otomatik yerleştirir.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class DamBuilderController3D : MonoBehaviour
{
    [Header("5x4 Baraj Yapısı Ayarları")]
    [SerializeField] private int totalBlocks = 5; // Toplam blok sayısı (5 blok)
    [SerializeField] private int itemsPerBlock = 4; // Her bloğu oluşturan dal/çöp sayısı (4 parça)
    [SerializeField] private bool winWhenDamCompleted = true; // "Korku değil ümit vermek": 5 blok tamamlanınca nehir kurtulsun mu?

    [Header("Bağlantılar")]
    [SerializeField] private WaterLevelController waterController;
    [SerializeField] private Transform[] blockVisuals; // Opsiyonel: Her 4 parçada bir aktifleşecek 5 hazır baraj bloğu objesi

    [Header("Otomatik Dizilim ve Başlangıç Offseti (Spot Light'tan Ayırmak İçin)")]
    [SerializeField] private Vector3 buildStartOffset = new Vector3(-5f, 0f, 0f); // Barajın Dam objesine göre başlayacağı offset (örn. 5 birim sol: X = -5)
    [SerializeField] private float blockSpacingX = 1.2f; // 5 bloğun yan yana (X ekseni) aralığı
    [SerializeField] private float itemLayerSpacingY = 0.25f; // Her bloktaki 4 parçanın üst üste (Y ekseni) aralığı

    [Header("Su Üstü Görünürlük (Render Queue Katmanı)")]
    [SerializeField] private int damRenderQueue = 2010; // Suyun (2005) baraj parçalarını kapatmaması için üst katman

    private int currentPlacedItems = 0;
    private int currentCompletedBlocks = 0;
    private BoxCollider triggerZone;

    public int TotalRequiredItems => totalBlocks * itemsPerBlock;
    public int CurrentPlacedItems => currentPlacedItems;
    public int CurrentCompletedBlocks => currentCompletedBlocks;
    public bool IsDamCompleted => currentCompletedBlocks >= totalBlocks;

    /// <summary>
    /// Parça eklendiğinde (eklenenParçaSayısı, toplamGerekenParça, tamamlananBlokSayısı) bilgisini fırlatır.
    /// </summary>
    public event Action<int, int, int> OnDamProgressUpdated;

    /// <summary>
    /// 5 bloğun tamamı (5x4 = 20 parça) tamamlandığında tetiklenir.
    /// </summary>
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

        // Başlangıçta opsiyonel blok görsellerini kapat
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

    private void PlaceItemIntoDam(TrashPickup3D item)
    {
        int blockIndex = currentPlacedItems / itemsPerBlock; // 0..4 arası hangi blok
        int slotInBlock = currentPlacedItems % itemsPerBlock; // 0..3 arası blok içi sıra

        // Parçayı buildStartOffset (örn. 5 birim sol) noktasından başlayarak 5x4 ızgarada hizala
        Vector3 slotLocalPos = buildStartOffset + new Vector3(
            blockIndex * blockSpacingX,
            slotInBlock * itemLayerSpacingY,
            0f
        );

        item.LockIntoDam(transform, slotLocalPos, Quaternion.identity);
        ApplyRenderQueueToHierarchy(item.transform, damRenderQueue);

        currentPlacedItems++;
        int newCompletedBlocks = currentPlacedItems / itemsPerBlock;

        // Yeni bir 4'lü blok tamamlandıysa opsiyonel blok görselini aç
        if (newCompletedBlocks > currentCompletedBlocks)
        {
            currentCompletedBlocks = newCompletedBlocks;
            int completedIdx = currentCompletedBlocks - 1;

            if (blockVisuals != null && completedIdx >= 0 && completedIdx < blockVisuals.Length && blockVisuals[completedIdx] != null)
            {
                blockVisuals[completedIdx].gameObject.SetActive(true);
            }
        }

        // Nehir suyuna anlık katkı yap (Çöp: +5, Dal: +10) ve azalma hızını düşür
        if (waterController != null)
        {
            waterController.AddWaterByDebris(item.WaterRestoreAmount, item.DrainRateReduction);
        }

        OnDamProgressUpdated?.Invoke(currentPlacedItems, TotalRequiredItems, currentCompletedBlocks);

        // 5x4 (20 parça) tamamlandı mı kontrol et
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