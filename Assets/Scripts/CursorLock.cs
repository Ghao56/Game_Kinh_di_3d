using UnityEngine;

public class CursorLock : MonoBehaviour
{
    [SerializeField] private Unity.Cinemachine.CinemachineInputAxisController mouseLook;

    void OnEnable()
    {
        if (mouseLook != null) mouseLook.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        if (mouseLook != null) mouseLook.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
