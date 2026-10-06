using UnityEngine;
using UnityEngine.UI;

// Gắn script này vào 1 object cha rỗng trong Canvas.
// Kéo vào "Slot Texts" đúng 5 (hoặc ít hơn) object Text con.
// Vị trí hiển thị KHÔNG cố định theo thứ tự khai báo trong ThrowableSelector nữa -
// mà theo thứ tự NHẶT ĐƯỢC LẦN ĐẦU TIÊN (xem ThrowableSelector.cs).
// Ô nào chưa từng nhặt, hoặc hiện đang hết hàng (count == 0), sẽ tự ẩn (để trống).
public class HotbarUI : MonoBehaviour
{
    public ThrowableSelector selector;

    public Text[] slotTexts;

    void Update()
    {
        if (selector == null)
            return;

        for (int i = 0; i < slotTexts.Length; i++)
        {
            if (slotTexts[i] == null)
                continue;

            IThrowable item = selector.GetItemAt(i);

            if (item == null)
            {
                slotTexts[i].text = "";
                continue;
            }

            int count = item.GetCount();

            // Hết hàng -> ẩn luôn ô này (vị trí vẫn được giữ, chỉ là không hiện chữ)
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