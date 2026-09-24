using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PaperNote : MonoBehaviour, IInteractable
{
    [Header("Giao diện")]
    [SerializeField] private string promptText = "Đọc giấy";

    [Header("Nội dung")]
    [TextArea(5, 15)]
    [SerializeField] private string noteContent = "Tờ giấy trống.";

    [Header("UI")]
    [SerializeField] private NoteReaderUI noteUI;

    public string PromptText => promptText;
    public bool HoldToInteract => false;
    public float HoldDuration => 0f;

    public void OnInteract(Interactor interactor)
    {
        if (noteUI == null)
        {
            Debug.LogWarning($"[{nameof(PaperNote)}] Chưa gán NoteReaderUI trong Inspector.", this);
            return;
        }
        noteUI.Open(noteContent);
    }
}