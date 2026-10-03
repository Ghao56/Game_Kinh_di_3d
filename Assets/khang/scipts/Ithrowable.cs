// Interface chung cho mọi thứ có thể "ném/dùng" được (muối, rượu, vật phẩm tương lai...).
// ThrowableSelector chỉ cần biết interface này, không cần biết bên trong SaltBagThrower
// hay WineSprayer làm gì khác nhau.
public interface IThrowable
{
    // Tên hiển thị khi đang chọn vật phẩm này
    string DisplayName { get; }

    // Thực hiện ném/dùng. Trả về true nếu thành công, false nếu hết hàng/thiếu tham chiếu.
    bool TryThrow();

    // Số lượng hiện có trong kho của vật phẩm này - UI dùng để biết có nên hiện ô này không
    int GetCount();
}