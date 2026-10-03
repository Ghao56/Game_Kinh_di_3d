using UnityEngine;
using UnityEngine.InputSystem;

// Gắn script này vào bất kỳ object nào (ví dụ Player, hoặc 1 object quản lý UI riêng).
// Nhấn "toggleKey" để ẩn/hiện object "target" (ví dụ cả Canvas Inventory).
public class CanvasToggle : MonoBehaviour
{
    public GameObject target;

    public Key toggleKey = Key.R;

    void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current[toggleKey].wasPressedThisFrame &&
            target != null)
        {
            target.SetActive(!target.activeSelf);
        }
    }
}