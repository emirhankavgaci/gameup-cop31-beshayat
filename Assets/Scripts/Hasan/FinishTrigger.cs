using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// CANOPY: The Last Run - Oyun Sonu Bitiş Tetikleyicisi (Finish Trigger).
/// Oyuncu ulaştığında kontrolü kilitlemeden EndingSequence (selin arkaya ışınlanması, boğulma ve
/// IUCN sinematik bilgi ekranı) sekansını başlatır. Eski "Kurtuldun" ekranı kaldırılmıştır.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class FinishTrigger : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Oyun sonu boğulma ve bilgi ekranı sekansı yöneticisi (boşsa sahnede otomatik bulunur/oluşturulur).")]
    [SerializeField] private EndingSequence endingSequence;

    [Tooltip("Oyun akışını yöneten GameFlowManager (boşsa otomatik bulunur).")]
    [SerializeField] private GameFlowManager gameFlowManager;

    [Header("Olaylar")]
    [Tooltip("Oyuncu bitiş çizgisine ulaştığında tetiklenen UnityEvent.")]
    [SerializeField] private UnityEvent onFinishReachedEvent;

    public event Action OnFinishReached;

    private bool hasFinished;

    private void Awake()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (endingSequence == null)
        {
            endingSequence = FindObjectOfType<EndingSequence>();
        }

        if (gameFlowManager == null)
        {
            gameFlowManager = FindObjectOfType<GameFlowManager>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasFinished || other == null)
            return;

        bool isPlayer = other.GetComponent<PlayerMotor>() != null ||
                        other.GetComponentInParent<PlayerMotor>() != null ||
                        other.CompareTag("Player");

        if (!isPlayer)
            return;

        hasFinished = true;
        onFinishReachedEvent?.Invoke();
        OnFinishReached?.Invoke();

        // FinishTrigger tetiklendiğinde maymun boğulma ölüm sesini (whenmonkeydie.mp3) çal
        PlayerAudioHandler.Instance?.PlayWhenMonkeyDieSound();

        if (endingSequence == null)
        {
            endingSequence = FindObjectOfType<EndingSequence>();
        }

        if (endingSequence != null)
        {
            endingSequence.StartEndingSequence();
        }
        else if (gameFlowManager != null)
        {
            gameFlowManager.CompleteLevel();
        }
        else
        {
            GameObject seqObj = new GameObject("EndingSequence_Runtime");
            endingSequence = seqObj.AddComponent<EndingSequence>();
            endingSequence.StartEndingSequence();
        }
    }

    public void ResetFinishTrigger()
    {
        hasFinished = false;
    }

    private void OnDrawGizmos()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
            return;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.35f);
        Gizmos.DrawCube(col.center, col.size);

        Gizmos.color = new Color(0.1f, 1f, 0.2f, 0.95f);
        Gizmos.DrawWireCube(col.center, col.size);

        Gizmos.matrix = oldMatrix;
    }
}
