using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// CANOPY: The Last Run - Oyun Sonu Bitiş Tetikleyicisi (Finish Trigger).
/// Oyuncu ulaştığında sel durur ve GameFlowManager üzerinden "Kurtuldun" ekranı açılır.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class FinishTrigger : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Oyun akışını ve bitiş ekranını yöneten GameFlowManager (boşsa otomatik bulunur).")]
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

        if (gameFlowManager != null)
        {
            gameFlowManager.CompleteLevel();
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

