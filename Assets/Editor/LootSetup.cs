using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// shortcut: gắn nhanh hệ thống túi đồ loot vào scene ĐANG MỞ trong editor
public static class LootSetup
{
    [MenuItem("Tools/Setup Loot Bag In Open Scene")]
    public static void Setup()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        // 1. Player tag "Player" (hoặc tên chứa "player") -> gắn PlayerLootBag
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            foreach (var root in scene.GetRootGameObjects())
                if (root.name.ToLower().Contains("player")) { player = root; break; }

        if (player == null)
            Debug.LogError("[LootSetup] Không tìm thấy Player trong scene — gắn tay PlayerLootBag lên player.");
        else if (player.GetComponent<PlayerLootBag>() == null)
        {
            player.AddComponent<PlayerLootBag>();
            Debug.Log("[LootSetup] Đã gắn PlayerLootBag vào " + player.name);
        }

        // 2. Gắn LootItem cho mọi object tag "Item" chưa có component
        int added = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (!go.CompareTag("Item")) continue;
            if (go.GetComponentInParent<LootItem>() != null) continue;

            var li = go.AddComponent<LootItem>();
            li.itemName = go.name;
            li.valueInMillions = 1;
            li.slotCost = 1;
            added++;
        }
        Debug.Log($"[LootSetup] Gắn LootItem cho {added} object tag 'Item' (mặc định 1tr/1 ô — sửa giá trị trên Inspector).");

        // 3. Vùng an toàn tính tiền
        var zone = GameObject.Find("Loot Safe Zone");
        if (zone == null)
        {
            zone = new GameObject("Loot Safe Zone");
            var col = zone.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(3f, 3f, 3f);
            zone.AddComponent<LootSafeZone>();
            Debug.Log("[LootSetup] Đã tạo 'Loot Safe Zone' tại (0,0,0) — kéo tới cửa nhà an toàn.");
        }

        // 4. Panel túi đồ (dựng runtime khi Play nhưng cần object Text trên Canvas)
        Canvas canvas = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            canvas = root.GetComponent<Canvas>();
            if (canvas != null) break;
        }
        if (canvas == null)
        {
            var canvasGo = new GameObject("Canvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
        }
        if (GameObject.Find("BagPanelLootText") == null)
        {
            var textGo = new GameObject("BagPanelLootText", typeof(RectTransform), typeof(Text), typeof(LootBagPanelUI));
            textGo.transform.SetParent(canvas.transform, false);

            var rect = (RectTransform)textGo.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.color = new Color(0.85f, 0.94f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            Debug.Log("[LootSetup] Đã tạo 'BagPanelLootText' — LootBagPanelUI sẽ dựng panel khi Play.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[LootSetup] Xong — đã lưu " + scene.path);
    }
}
