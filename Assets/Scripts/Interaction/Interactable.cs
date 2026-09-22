using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class Interactable : MonoBehaviour, IInteractable
{
    [SerializeField] private string promptText = "Tương tác";
    [SerializeField] private bool holdToInteract = false;
    [SerializeField] private float holdDuration = 0.5f;
    [SerializeField] private UnityEvent onInteractEvent;

    public string PromptText => promptText;
    public bool HoldToInteract => holdToInteract;
    public float HoldDuration => holdDuration;

    public virtual void OnInteract(Interactor interactor)
    {
        Debug.Log($"[Interact] {gameObject.name} — {promptText}");
        onInteractEvent?.Invoke();
    }
}