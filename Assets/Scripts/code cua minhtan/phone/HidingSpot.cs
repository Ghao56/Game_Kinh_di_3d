using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class HidingSpot : MonoBehaviour
{
    [Header("Các vị trí mỏ neo")]
    [SerializeField] private Transform hideSpot;  // Vị trí chui dưới gầm bàn
    [SerializeField] private Transform exitSpot;  // Vị trí bước ra ngoài bàn

    [Header("Giao diện UI")]
    [SerializeField] private GameObject interactPrompt; // Text "Nhấn E để núp"

    [Header("Phím tương tác")]
    [SerializeField] private Key interactKey = Key.E;

    [Header("Script cần khóa khi đang núp (kéo script di chuyển/camera của Player vào đây)")]
    [SerializeField] private Behaviour[] scriptsToDisableWhileHiding;

    [Header("Tùy chọn AI / tàng hình")]
    [SerializeField] private bool changeTagWhileHiding = true;
    [SerializeField] private string hiddenTag = "Untagged";
    [SerializeField] private string normalTag = "Player";

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private bool isPlayerInsideZone = false;
    private bool isHiding = false;

    private GameObject playerRoot;
    private CharacterController playerController;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(normalTag) && !other.transform.root.CompareTag(normalTag)) return;

        if (logDebug) Debug.Log("-> ĐÃ BẮT ĐƯỢC VA CHẠM VỚI GẦM BÀN!");
        isPlayerInsideZone = true;

        playerRoot = other.transform.root.gameObject;
        playerController = playerRoot.GetComponentInChildren<CharacterController>();

        if (interactPrompt != null && !isHiding)
            interactPrompt.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (playerRoot == null || !other.transform.root.gameObject.Equals(playerRoot)) return;

        if (logDebug) Debug.Log("-> ĐÃ RA KHỎI GẦM BÀN!");
        isPlayerInsideZone = false;

        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    private void Update()
    {
        if (!(isPlayerInsideZone || isHiding)) return;
        if (Keyboard.current == null || !Keyboard.current[interactKey].wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (!isHiding)
            StartHiding();
        else
            StopHiding();
    }

    private void StartHiding()
    {
        if (playerRoot == null || hideSpot == null) return;

        isHiding = true;
        if (interactPrompt != null) interactPrompt.SetActive(false);

        if (playerController != null) playerController.enabled = false;
        SetScriptsEnabled(false);

        playerRoot.transform.SetPositionAndRotation(hideSpot.position, hideSpot.rotation);

        if (changeTagWhileHiding)
            playerRoot.tag = hiddenTag;
    }

    private void StopHiding()
    {
        if (playerRoot == null || exitSpot == null) return;

        isHiding = false;

        playerRoot.transform.SetPositionAndRotation(exitSpot.position, exitSpot.rotation);

        if (playerController != null) playerController.enabled = true;
        SetScriptsEnabled(true);

        if (changeTagWhileHiding)
            playerRoot.tag = normalTag;

        if (isPlayerInsideZone && interactPrompt != null)
            interactPrompt.SetActive(true);
    }

    private void SetScriptsEnabled(bool value)
    {
        if (scriptsToDisableWhileHiding == null) return;

        foreach (var script in scriptsToDisableWhileHiding)
        {
            if (script != null)
                script.enabled = value;
        }
    }
}