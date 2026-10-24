using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Gắn script này vào object Player.
//
// Giờ đây script quản lý HOTBAR 4 Ô CỐ ĐỊNH (phím 1-4), không còn dồn hàng
// theo thứ tự nhặt nữa. Mỗi mục có thể chiếm 1 hoặc nhiều ô liên tiếp:
//   - Muối/rượu: 1 ô, do IThrowable quản lý.
//   - Loot: số ô lấy từ LootItem.slotCost (1..3).
// Các ô của cùng một mục cùng trỏ về một HotbarEntry duy nhất.
//
// Bấm 1-4: chọn mục đang chiếm ô đó. Ô trống -> bỏ chọn (không cầm gì).
// Bấm phím ném (mặc định F): chỉ ném nếu mục đang chọn là IThrowable.
// Loot không bao giờ trở thành đồ ném chỉ vì đang nằm trong hotbar.
public class ThrowableSelector : MonoBehaviour
{
    // Toàn bộ vật phẩm CÓ THỂ ném (mỗi phần tử phải implement IThrowable,
    // ví dụ SaltBagThrower, WineSprayer...). Danh sách này chỉ dùng để dò
    // xem vật nào đã nhặt; thứ tự ô do hotbar tự cấp phát.
    public MonoBehaviour[] throwables;

    public Key throwKey = Key.F;

    // Hotbar luôn có đúng 4 ô vật lý.
    public const int HotbarSlotCount = 4;
    public int SlotCount => HotbarSlotCount;

    // Toàn bộ mục đang chiếm hotbar (mỗi mục xuất hiện đúng 1 lần).
    private readonly List<HotbarEntry> entries = new List<HotbarEntry>();

    // Ô đang chọn (0-based). -1 = không chọn gì (ô trống hoặc vừa bỏ chọn).
    private int currentSlot = -1;

    // Chỗ khác (HotbarUI, ThrowableSelectorUI, SlotsHotbarUI) đọc để biết
    // đang chọn ô nào. Với vật nhiều ô, đây là ô người chơi vừa bấm.
    public int CurrentIndex => currentSlot;
    public int SelectedSlot => currentSlot;

    // Mục đang được chọn (null nếu ô đang chọn trống).
    public HotbarEntry SelectedEntry => GetEntryAt(currentSlot);

    // Số mục ném được hiện có trong hotbar (giữ API cũ cho UI).
    public int ThrowableCount
    {
        get
        {
            int n = 0;
            foreach (var e in entries)
                if (e.IsThrowable)
                    n++;
            return n;
        }
    }

    // Danh sách chỉ đọc để UI duyệt toàn bộ mục (cả loot lẫn throwable).
    public IReadOnlyList<HotbarEntry> Entries => entries;

    public event System.Action OnSelectionChanged;

    // Bắn khi danh sách mục/ô thay đổi (thêm, bớt, hết hàng).
    public event System.Action OnHotbarChanged;

    private static readonly Key[] digitKeys = new Key[]
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4
    };

    public string CurrentItemName
    {
        get
        {
            HotbarEntry e = SelectedEntry;
            return e != null ? e.Name : "(trống)";
        }
    }

    // Lấy tên vật phẩm tại VỊ TRÍ Ô (1 ô có thể thuộc vật nhiều ô).
    public string GetItemName(int slot)
    {
        HotbarEntry e = GetEntryAt(slot);
        return e != null ? e.Name : string.Empty;
    }

    // Lấy số lượng vật phẩm tại VỊ TRÍ Ô.
    public int GetItemCount(int slot)
    {
        HotbarEntry e = GetEntryAt(slot);
        return e != null ? e.Count : 0;
    }

    // Trả về IThrowable tại ô (null nếu ô trống hoặc là loot).
    // Nhờ vậy code cũ dùng GetItemAt vẫn chạy đúng và loot không bị ném.
    public IThrowable GetItemAt(int slot)
    {
        HotbarEntry e = GetEntryAt(slot);
        return e != null ? e.Throwable : null;
    }

    // Trả về mục đang chiếm ô. Nhiều ô của cùng một vật trả về CÙNG entry.
    public HotbarEntry GetEntryAt(int slot)
    {
        if (slot < 0 || slot >= HotbarSlotCount)
            return null;

        foreach (var e in entries)
            if (e.Occupies(slot) && e.IsUsable)
                return e;

        return null;
    }

    // Tìm mục mà một món loot cụ thể đang chiếm (null nếu chưa đăng ký).
    // PlayerGrab dùng để đồng bộ lựa chọn hotbar với món vừa nhấc lên tay.
    public HotbarEntry GetEntryOf(LootItem loot)
    {
        if (loot == null)
            return null;

        foreach (var e in entries)
            if (ReferenceEquals(e.Loot, loot))
                return e;

        return null;
    }

    // Kho có đủ dãy ô TRỐNG LIÊN TIẾP cho món loot này không?
    // Tính trên phân bổ ô thật của hotbar (FindFreeRun), không dựa vào việc
    // tay đang cầm món gì. PlayerGrab dùng để chỉ báo "hết chỗ" đúng lúc.
    public bool HasSpaceForLoot(LootItem loot)
    {
        if (loot == null)
            return true;

        // Đã đăng ký rồi thì không cần thêm ô nào.
        if (GetEntryOf(loot) != null)
            return true;

        int size = Mathf.Clamp(loot.slotCost, 1, HotbarSlotCount);
        return FindFreeRun(size) >= 0;
    }

    void Update()
    {
        SyncHotbarEntries();

        if (Keyboard.current == null)
            return;

        // 1-4: chọn mục ở ô tương ứng. Ô trống -> bỏ chọn.
        for (int i = 0; i < digitKeys.Length && i < HotbarSlotCount; i++)
        {
            if (Keyboard.current[digitKeys[i]].wasPressedThisFrame)
            {
                SelectSlot(i);
                break;
            }
        }

        // Phím ném dùng chung: chỉ ném mục đang chọn nếu nó là IThrowable.
        if (Keyboard.current[throwKey].wasPressedThisFrame)
            TryThrowSelected();
    }

    // Chọn ô theo phím 1-4. Trả về true nếu ô đó có mục.
    public bool SelectSlot(int slot)
    {
        if (slot < 0 || slot >= HotbarSlotCount)
            return false;

        HotbarEntry entry = GetEntryAt(slot);

        if (entry == null)
        {
            // Ô trống: không cầm gì.
            if (currentSlot != -1)
            {
                currentSlot = -1;
                Debug.Log("Đang chọn: (trống)");
                OnSelectionChanged?.Invoke();
            }
            return false;
        }

        if (currentSlot != slot)
        {
            currentSlot = slot;
            Debug.Log($"Đang chọn: {entry.Name}");
            OnSelectionChanged?.Invoke();
        }

        return true;
    }

    // Ném/dùng mục đang chọn. Loot và ô trống trả về false.
    public bool TryThrowSelected()
    {
        HotbarEntry entry = GetEntryAt(currentSlot);
        if (entry == null || entry.Throwable == null)
            return false;

        bool ok = entry.Throwable.TryThrow();

        // Ném có thể làm số lượng về 0 -> cập nhật hotbar ngay trong frame.
        if (ok)
            SyncHotbarEntries();

        return ok;
    }

    // Đăng ký một món loot THẬT vừa được nhặt. Trả về false nếu không đủ
    // dãy ô liên tiếp; khi đó PlayerGrab sẽ không nhặt (không huỷ vật phẩm).
    public bool TryRegisterLoot(LootItem loot)
    {
        if (loot == null)
            return false;

        // Cùng một instance được gọi đăng ký nhiều lần -> không tạo mục mới.
        foreach (var e in entries)
            if (e.Loot == loot)
                return true;

        string id = !string.IsNullOrEmpty(loot.ItemId) ? loot.ItemId.Trim() : loot.name;
        string displayName = !string.IsNullOrEmpty(loot.itemName) ? loot.itemName : id;

        var entry = new HotbarEntry
        {
            ItemId = id,
            DisplayName = displayName,
            SlotSize = Mathf.Clamp(loot.slotCost, 1, HotbarSlotCount),
            Loot = loot
        };

        bool wasEmpty = entries.Count == 0;

        if (!TryInsertEntry(entry))
        {
            Debug.LogWarning(
                $"[Hotbar] Không đủ {entry.SlotSize} ô liên tiếp cho '{entry.Name}'. " +
                "Vật phẩm được giữ nguyên, không bị huỷ.");
            return false;
        }

        if (wasEmpty && currentSlot < 0)
            currentSlot = entry.StartSlot;

        NotifyHotbarChanged();
        return true;
    }

    // Bỏ món loot khỏi hotbar (khi thả xuống đất/chất lên xe/bị huỷ).
    // Giải phóng TOÀN BỘ ô mà món đó chiếm.
    public bool RemoveLoot(LootItem loot)
    {
        if (loot == null)
            return false;

        bool removed = false;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].Loot == loot)
            {
                entries.RemoveAt(i);
                removed = true;
            }
        }

        if (removed)
            AfterEntriesChanged();

        return removed;
    }

    // Tự đồng bộ throwable theo số lượng trong PlayerInventory:
    // - count > 0 và chưa có -> cấp dãy ô trống đầu tiên.
    // - count <= 0 -> gỡ khỏi hotbar, giải phóng mọi ô.
    void SyncHotbarEntries()
    {
        bool changed = false;
        bool wasEmpty = entries.Count == 0;

        // 1) Dọn mục không còn dùng được (hết hàng hoặc object loot bị huỷ).
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            HotbarEntry e = entries[i];
            if (e.Throwable == null && e.Loot == null)
            {
                entries.RemoveAt(i);
                changed = true;
                continue;
            }

            if (!e.IsUsable)
            {
                entries.RemoveAt(i);
                changed = true;
            }
        }

        // 2) Thêm throwable vừa có hàng vào chỗ trống liên tiếp.
        if (throwables != null)
        {
            foreach (MonoBehaviour mb in throwables)
            {
                if (mb == null || mb is not IThrowable t)
                    continue;

                if (t.GetCount() <= 0)
                    continue;

                if (ContainsThrowable(t))
                    continue;

                var entry = new HotbarEntry
                {
                    ItemId = t.ItemId,
                    DisplayName = t.DisplayName,
                    SlotSize = 1,
                    Throwable = t
                };

                if (TryInsertEntry(entry))
                    changed = true;
            }
        }

        if (!changed)
            return;

        if (wasEmpty && currentSlot < 0 && entries.Count > 0)
            currentSlot = entries[0].StartSlot;

        AfterEntriesChanged();
    }

    bool ContainsThrowable(IThrowable target)
    {
        foreach (var e in entries)
            if (e.Throwable != null && ReferenceEquals(e.Throwable, target))
                return true;

        return false;
    }

    // Cấp phát dãy ô liên tiếp đầu tiên đủ chỗ. Không bao giờ chồng ô.
    bool TryInsertEntry(HotbarEntry entry)
    {
        int start = FindFreeRun(entry.SlotSize);
        if (start < 0)
            return false;

        entry.StartSlot = start;
        entries.Add(entry);
        entries.Sort((a, b) => a.StartSlot.CompareTo(b.StartSlot));
        return true;
    }

    int FindFreeRun(int size)
    {
        if (size <= 0 || size > HotbarSlotCount)
            return -1;

        bool[] occupied = new bool[HotbarSlotCount];
        foreach (var e in entries)
        {
            int end = Mathf.Min(e.StartSlot + e.SlotSize, HotbarSlotCount);
            for (int i = Mathf.Max(0, e.StartSlot); i < end; i++)
                occupied[i] = true;
        }

        int run = 0;
        int start = -1;

        for (int i = 0; i < HotbarSlotCount; i++)
        {
            if (occupied[i])
            {
                run = 0;
                start = -1;
                continue;
            }

            if (run == 0)
                start = i;
            run++;

            if (run >= size)
                return start;
        }

        return -1;
    }

    // Giữ lựa chọn hiện tại nếu còn hợp lệ; nếu mục đang chọn vừa bị gỡ thì
    // chọn mục còn lại đầu tiên. Trạng thái "không chọn" (-1) được giữ nguyên.
    void AfterEntriesChanged()
    {
        if (currentSlot >= 0 && GetEntryAt(currentSlot) == null)
            currentSlot = entries.Count > 0 ? entries[0].StartSlot : -1;

        NotifyHotbarChanged();
    }

    void NotifyHotbarChanged()
    {
        OnHotbarChanged?.Invoke();
        OnSelectionChanged?.Invoke();
    }
}
