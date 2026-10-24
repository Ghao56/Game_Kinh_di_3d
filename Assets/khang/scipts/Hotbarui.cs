using UnityEngine;
using UnityEngine.UI;

// Gắn script này vào 1 object cha rỗng trong Canvas.
// Kéo vào "Slot Texts" đúng 4 (hoặc ít hơn) object Text con.
// Hiển thị nội dung hotbar 4 ô: mỗi mục một dòng ở ô bắt đầu của nó.
// Ô trống hoặc phần phụ của vật nhiều ô sẽ để trống chữ.
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

            if (i >= selector.SlotCount)
            {
                slotTexts[i].text = "";
                continue;
            }

            HotbarEntry entry = selector.GetEntryAt(i);

            // Chỉ in ở ô bắt đầu để vật nhiều ô không bị lặp dòng.
            if (entry == null || i != entry.StartSlot)
            {
                slotTexts[i].text = "";
                continue;
            }

            bool selected = ReferenceEquals(entry, selector.SelectedEntry);
            string count = entry.Count > 1 ? $": {entry.Count}" : "";
            slotTexts[i].text = (selected ? "➤ " : "   ") + $"{i + 1}. {entry.Name}{count}";
        }
    }
}
