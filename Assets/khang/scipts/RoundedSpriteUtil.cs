using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Tiện ích dùng chung: sinh sprite nền bo góc (kèm 9-slice border) lúc runtime,
// để làm panel UI hiện đại mà không cần nạp ảnh mẫu từ disk.
// Trong Editor: sprite được LƯU RA ASSET (Assets/khang/UI_Sprites) để UI bake sẵn
// vào scene giữ được reference sprite khi lưu scene.
public static class RoundedSpriteUtil
{
    private static Sprite cached;
    private static Sprite circle;

    // Chấm tròn 64x64 - dùng làm icon vật phẩm
    public static Sprite Circle()
    {
#if UNITY_EDITOR
        circle = TaiAsset("RoundedCircle");
        if (circle != null)
            return circle;
#endif
        if (circle != null)
            return circle;

        const int size = 64;
        float r = size / 2f - 1f;

        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - size / 2f;
                float dy = y + 0.5f - size / 2f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - dist + 0.5f)));
            }

        tex.Apply();
        circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
#if UNITY_EDITOR
        circle = LuuAsset(tex, "RoundedCircle", Vector4.zero);
#endif
        return circle;
    }

    // Sprite nền bo góc 9-slice: border 32px trên texture 128px
    public static Sprite Get()
    {
#if UNITY_EDITOR
        cached = TaiAsset("RoundedPanel");
        if (cached != null)
            return cached;
#endif
        if (cached != null)
            return cached;

        const int size = 128;
        const int radius = 32;

        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, RoundedAlpha(x + 0.5f, y + 0.5f, size, size, radius)));

        tex.Apply();

        cached = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius)
        );

#if UNITY_EDITOR
        cached = LuuAsset(tex, "RoundedPanel", new Vector4(radius, radius, radius, radius));
#endif
        return cached;
    }

#if UNITY_EDITOR
    // Lưu texture + sprite vào asset để scene bake sẵn serialize được reference
    private static Sprite LuuAsset(Texture2D tex, string ten, Vector4 border)
    {
        const string dir = "Assets/khang/UI_Sprites";
        string path = dir + "/" + ten + ".png";
        if (AssetDatabase.LoadAssetAtPath<Sprite>(path) != null)
        {
            tex = null; // đã có asset, khỏi cần texture tạm nữa
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.spriteBorder = border;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite TaiAsset(string ten)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/khang/UI_Sprites/" + ten + ".png");
    }
#endif

    // Alpha kiểu SDF chữ nhật bo góc, dịu 1-2 px ở mép
    private static float RoundedAlpha(float x, float y, int w, int h, float r)
    {
        float dx = Mathf.Abs(x - w / 2f) - (w / 2f - r);
        float dy = Mathf.Abs(y - h / 2f) - (h / 2f - r);
        float inside = Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f);
        float dist = Mathf.Sqrt(inside) + Mathf.Min(Mathf.Max(dx, dy), 0f);
        return Mathf.Clamp01(1f - dist * 1.4f);
    }
}
