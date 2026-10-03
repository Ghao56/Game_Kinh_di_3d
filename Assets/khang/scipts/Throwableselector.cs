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

    // Chỗ khác (ví dụ HotbarUI, ThrowableSelectorUI) đọc giá trị này để biết ô nào đang được chọn
    public int CurrentIndex => currentIndex;

    // Số lượng vật phẩm hiện có trong danh sách - UI dùng để biết cần vẽ bao nhiêu dòng
    public int ThrowableCount => throwables.Length;

    // UI đăng ký vào đây để biết CHÍNH XÁC lúc nào lựa chọn thay đổi,
    // thay vì phải tự kiểm tra mỗi frame.
    public event System.Action OnSelectionChanged;

    // Phím số 1-5 ở hàng trên chữ cái (KHÔNG phải Numpad), theo đúng thứ tự
    private static readonly Key[] digitKeys = new Key[]
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5
    };

    public string CurrentItemName =>
        throwables.Length > 0 && throwables[currentIndex] is IThrowable t
            ? t.DisplayName
            : "(trống)";

    // Lấy tên hiển thị của vật phẩm tại vị trí bất kỳ trong danh sách (không chỉ ô đang chọn)
    public string GetItemName(int index)
    {
        if (index < 0 || index >= throwables.Length)
            return "?";

        return throwables[index] is IThrowable t ? t.DisplayName : "?";
    }

    // Số lượng vật phẩm tại vị trí index - UI dùng để biết có nên ẩn ô này không (count == 0)
    public int GetItemCount(int index)
    {
        if (index < 0 || index >= throwables.Length)
            return 0;

        return throwables[index] is IThrowable t ? t.GetCount() : 0;
    }

    void Update()
    {
        if (throwables.Length == 0 || Keyboard.current == null)
            return;

        // Bấm đúng số nào (trong phạm vi có vật phẩm, tối đa 5) sẽ chọn thẳng vật phẩm đó
        for (int i = 0; i < digitKeys.Length && i < throwables.Length; i++)
        {
            if (Keyboard.current[digitKeys[i]].wasPressedThisFrame && currentIndex != i)
            {
                currentIndex = i;
                Debug.Log($"Đang chọn: {CurrentItemName}");

                OnSelectionChanged?.Invoke();

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