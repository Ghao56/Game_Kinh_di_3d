using UnityEngine;
using UnityEngine.UI;

// Gắn script này vào 1 object cha rỗng trong Canvas.
// Kéo vào "Slot Texts" đúng 5 (hoặc bằng số throwables) object Text con,
// theo ĐÚNG THỨ TỰ khớp với "Throwables" bên component ThrowableSelector
// (Text thứ 0 hiển thị cho throwables[0], v.v).
// Mỗi ô hiện "Tên: Số lượng", ô đang được chọn có thêm dấu ▲ phía trước.
public class HotbarUI : MonoBehaviour
{
    public ThrowableSelector selector;
    public PlayerInventory inventory;

    public Text[] slotTexts;

    void Update()
    {
        if (selector == null || inventory == null)
            return;

        for (int i = 0; i < slotTexts.Length; i++)
        {
            if (slotTexts[i] == null)
                continue;

            if (i >= selector.throwables.Length)
            {
                slotTexts[i].text = "";
                continue;
            }

            if (!(selector.throwables[i] is IThrowable item))
            {
                slotTexts[i].text = "";
                continue;
            }

            int count = item.GetCount();

            // Chưa từng nhặt (count == 0) -> ẩn luôn ô này
            if (count <= 0)
            {
                slotTexts[i].text = "";
                continue;
            }

            bool selected = (i == selector.CurrentIndex);

            slotTexts[i].text = (selected ? "➤ " : "   ") + $"{i + 1}. {item.DisplayName}: {count}";
        }
    }
}