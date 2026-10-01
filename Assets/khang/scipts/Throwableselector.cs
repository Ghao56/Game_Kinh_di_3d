using UnityEngine;
using UnityEngine.InputSystem;

// Gắn script này vào object Player.
// Quản lý việc CHỌN vật phẩm đang cầm (phím số hàng trên chữ cái 1-5) và NÉM
// vật phẩm đang chọn bằng 1 phím chung. Không cần biết bên trong SaltBagThrower
// hay WineSprayer khác nhau ra sao, chỉ cần chúng implement IThrowable.
public class ThrowableSelector : MonoBehaviour
{
    // Kéo các component ném vào đây theo đúng thứ tự - vị trí 0 = phím 1,
    // vị trí 1 = phím 2, v.v, tối đa 5 ô (mỗi phần tử phải implement IThrowable,
    // ví dụ SaltBagThrower, WineSprayer, hoặc vật phẩm mới sau này).
    public MonoBehaviour[] throwables;

    public Key throwKey = Key.F;

    private int currentIndex = 0;

    // Chỗ khác (ví dụ HotbarUI) đọc giá trị này để biết ô nào đang được chọn
    public int CurrentIndex => currentIndex;

    // Phím số 1-5 ở hàng trên chữ cái (KHÔNG phải Numpad), theo đúng thứ tự
    private static readonly Key[] digitKeys = new Key[]
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5
    };

    public string CurrentItemName =>
        throwables.Length > 0 && throwables[currentIndex] is IThrowable t
            ? t.DisplayName
            : "(trống)";

    void Update()
    {
        if (throwables.Length == 0 || Keyboard.current == null)
            return;

        // Bấm đúng số nào (trong phạm vi có vật phẩm, tối đa 5) sẽ chọn thẳng vật phẩm đó
        for (int i = 0; i < digitKeys.Length && i < throwables.Length; i++)
        {
            if (Keyboard.current[digitKeys[i]].wasPressedThisFrame)
            {
                currentIndex = i;
                Debug.Log($"Đang chọn: {CurrentItemName}");
                break;
            }
        }

        if (Keyboard.current[throwKey].wasPressedThisFrame)
        {
            if (throwables[currentIndex] is IThrowable current)
            {
                current.TryThrow();
            }
            else
            {
                Debug.LogWarning("Phần tử trong throwables chưa implement IThrowable!");
            }
        }
    }
}