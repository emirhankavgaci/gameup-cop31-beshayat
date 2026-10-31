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

    [Header("Otomatik Dizilim Boyutları (Parçaları Barajda Göster)")]
    [SerializeField] private float blockSpacingX = 1.2f; // 5 bloğun yan yana (X ekseni) aralığı
    [SerializeField] private float itemLayerSpacingY = 0.25f; // Her bloktaki 4 parçanın üst üste (Y ekseni) aralığı

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
            waterController = FindFirstObjectByType<WaterLevelController>();
        }

        // Başlangıçta opsiyonel blok görsellerini kapat
        if (blockVisuals != null)
        {
            for (int i = 0; i < blockVisuals.Length; i++)
            {
                if (blockVisuals[i] != null)
                {
                    blockVisuals[i].gameObject.SetActive(false);
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
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

        // Parçayı 5x4'lük baraj ızgarasında kendi bloğuna ve katmanına hizala
        float startX = -((totalBlocks - 1) * blockSpacingX) * 0.5f;
        Vector3 slotLocalPos = new Vector3(
            startX + (blockIndex * blockSpacingX),
            slotInBlock * itemLayerSpacingY,
            0f
        );

        item.LockIntoDam(transform, slotLocalPos, Quaternion.identity);

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
}