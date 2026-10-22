using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

// Gắn script này vào object Player.
// Quản lý việc CHỌN vật phẩm đang cầm (phím số hàng trên chữ cái 1-5) và NÉM
// vật phẩm đang chọn bằng 1 phím chung.
//
// THỨ TỰ HIỂN THỊ theo THỨ TỰ NHẶT ĐƯỢC LẦN ĐẦU TIÊN: vật phẩm nào có count > 0
// trước thì chiếm vị trí số 1, vật phẩm tiếp theo chiếm số 2, v.v.
// Vật phẩm DÙNG HẾT về 0 bị GỠ khỏi hàng -> các vật còn lại DỒN LÊN (muối ở số 2
// lên số 1 và được cầm luôn). Nhặt lại sau này thì vật xuất hiện ở CUỐI hàng.
public class ThrowableSelector : MonoBehaviour
{
    // Toàn bộ vật phẩm CÓ THỂ ném (mỗi phần tử phải implement IThrowable,
    // ví dụ SaltBagThrower, WineSprayer...). Thứ tự ở đây KHÔNG quyết định
    // thứ tự hiển thị nữa - chỉ là danh sách để script dò xem cái nào đã nhặt.
    public MonoBehaviour[] throwables;

    public Key throwKey = Key.F;

    // Thứ tự thực tế hiển thị - vị trí 0 là vật phẩm nhặt ĐẦU TIÊN, lưu theo
    // chỉ số trong mảng throwables.
    private List<int> unlockedOrder = new List<int>();

    private int currentSlot = 0;

    // Chỗ khác (HotbarUI, ThrowableSelectorUI) đọc để biết đang chọn vị trí nào
    public int CurrentIndex => currentSlot;

    // Số lượng vật phẩm ĐÃ TỪNG nhặt (không phải tổng số throwables khai báo)
    public int ThrowableCount => unlockedOrder.Count;

    public event System.Action OnSelectionChanged;

    private static readonly Key[] digitKeys = new Key[]
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5
    };

    public string CurrentItemName =>
        unlockedOrder.Count > 0 && GetItemAt(currentSlot) is IThrowable t
            ? t.DisplayName
            : "(trống)";

    // Lấy tên vật phẩm tại VỊ TRÍ HIỂN THỊ (không phải chỉ số trong mảng throwables)
    public string GetItemName(int slot)
    {
        IThrowable t = GetItemAt(slot);
        return t != null ? t.DisplayName : "?";
    }

    // Lấy số lượng vật phẩm tại VỊ TRÍ HIỂN THỊ
    public int GetItemCount(int slot)
    {
        IThrowable t = GetItemAt(slot);
        return t != null ? t.GetCount() : 0;
    }

    // Trả về chính IThrowable tại VỊ TRÍ HIỂN THỊ, để UI tự lấy DisplayName/GetCount()
    // mà không cần biết gì về cấu trúc throwables/unlockedOrder bên trong.
    public IThrowable GetItemAt(int slot)
    {
        if (slot < 0 || slot >= unlockedOrder.Count)
            return null;

        return throwables[unlockedOrder[slot]] as IThrowable;
    }

    void Update()
    {
        // DỒN SLOT: vật phẩm dùng hết về 0 -> gỡ khỏi hàng hiển thị, vật còn lại
        // trượt lên đầy vào lỗ trống. Nhặt lại sau -> được thêm vào CUỐI hàng.
        int truoc = unlockedOrder.Count;
        unlockedOrder.RemoveAll(i => throwables[i] is not IThrowable it || it.GetCount() <= 0);
        int sau = unlockedOrder.Count;

        // Vật đang cầm vừa hết / bị dồn -> gài lại vào vật trượt lên (cầm luôn)
        if (sau > 0)
            currentSlot = Mathf.Clamp(currentSlot, 0, sau - 1);
        else
            currentSlot = 0;

        if (sau != truoc)
        {
            if (sau > 0)
                Debug.Log($"Dồn hàng - đang cầm: {CurrentItemName}");
            OnSelectionChanged?.Invoke();
        }

        DetectNewlyUnlockedItems();

        if (unlockedOrder.Count == 0 || Keyboard.current == null)
            return;

        for (int i = 0; i < digitKeys.Length && i < unlockedOrder.Count; i++)
        {
            if (Keyboard.current[digitKeys[i]].wasPressedThisFrame && currentSlot != i)
            {
                currentSlot = i;
                Debug.Log($"Đang chọn: {CurrentItemName}");

                OnSelectionChanged?.Invoke();

                break;
            }
        }

        if (Keyboard.current[throwKey].wasPressedThisFrame)
        {
            if (GetItemAt(currentSlot) is IThrowable current)
            {
                current.TryThrow();
            }
        }
    }

    // Quét toàn bộ throwables, phát hiện cái nào VỪA có count > 0 lần đầu
    // (chưa từng có trong unlockedOrder) rồi thêm vào CUỐI danh sách hiển thị.
    void DetectNewlyUnlockedItems()
    {
        for (int i = 0; i < throwables.Length; i++)
        {
            if (unlockedOrder.Contains(i))
                continue;

            if (throwables[i] is IThrowable t && t.GetCount() > 0)
            {
                unlockedOrder.Add(i);
                Debug.Log($"Mở khoá vật phẩm mới ở vị trí {unlockedOrder.Count}: {t.DisplayName}");
            }
        }
    }
}