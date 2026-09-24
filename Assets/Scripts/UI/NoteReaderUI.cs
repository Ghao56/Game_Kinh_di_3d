using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class NoteReaderUI : MonoBehaviour
{
    [SerializeField] private Text contentText;
    [SerializeField] private ThirdPersonController controller;
    [SerializeField] private ThirdPersonCamera cameraRig;
    [SerializeField] private Interactor interactor;
    [SerializeField] private InteractPromptUI promptUI;
    [SerializeField] private InputActionAsset actions;
    [SerializeField] private float fadeDuration = 0.25f;

    private CanvasGroup canvasGroup;
    private InputAction interactAction;
    private InputAction cancelAction;
    private int openedFrame = -1;
    private bool isOpen;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (contentText == null) contentText = GetComponentInChildren<Text>();
        if (controller == null) controller = FindFirstObjectByType<ThirdPersonController>();
        if (cameraRig == null) cameraRig = FindFirstObjectByType<ThirdPersonCamera>();
        if (interactor == null) interactor = FindFirstObjectByType<Interactor>();
        if (promptUI == null) promptUI = FindFirstObjectByType<InteractPromptUI>();

        if (actions != null)
        {
            interactAction = actions.FindActionMap("Player")?.FindAction("Interact");
            cancelAction = actions.FindActionMap("UI")?.FindAction("Cancel");
        }
    }

    public void Open(string text)
    {
        if (isOpen || canvasGroup.alpha > 0f) return;
        if (contentText == null)
        {
            Debug.LogWarning("[NoteReaderUI] Thiếu contentText.", this);
            return;
        }

        openedFrame = Time.frameCount;
        isOpen = true;
        contentText.text = text;

        if (controller != null) controller.enabled = false;
        if (cameraRig != null) cameraRig.enabled = false;
        if (interactor != null) interactor.enabled = false;

        if (promptUI != null)
        {
            promptUI.Hide();
            CanvasGroup promptGroup = promptUI.GetComponent<CanvasGroup>();
            if (promptGroup != null) promptGroup.alpha = 0f;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        interactAction?.Enable();
        cancelAction?.Enable();
    }

    private void Update()
    {
        if (isOpen)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, Time.unscaledDeltaTime / fadeDuration);
            if (Time.frameCount == openedFrame) return;
            if (ShouldClose()) Close();
            return;
        }

        if (canvasGroup.alpha > 0f)
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, Time.unscaledDeltaTime / fadeDuration);
    }

    private bool ShouldClose()
    {
        if (interactAction != null && interactAction.WasPressedThisFrame()) return true;
        if (cancelAction != null && cancelAction.WasPressedThisFrame()) return true;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
        return false;
    }

    private void Close()
    {
        isOpen = false;

        interactAction?.Disable();
        cancelAction?.Disable();

        if (controller != null) controller.enabled = true;
        if (cameraRig != null) cameraRig.enabled = true;

        StartCoroutine(ReenableInteractor());
    }

    private IEnumerator ReenableInteractor()
    {
        yield return null;
        if (interactor != null) interactor.enabled = true;
    }
}