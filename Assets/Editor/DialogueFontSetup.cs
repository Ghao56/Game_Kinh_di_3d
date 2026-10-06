using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

/// Tạo TMP Font Asset (Dynamic) từ Be Vietnam Pro để có đủ glyph tiếng Việt.
/// Font LiberationSans mặc định của TMP là static, thiếu toàn bộ chữ có dấu nên không dùng được.
public static class DialogueFontSetup
{
    private const string FontDir = "Assets/TextMesh Pro/Fonts";
    private const string FontAssetDir = "Assets/TextMesh Pro/Resources/Fonts & Materials";
    private const string RegularPath = FontDir + "/BeVietnamPro-Regular.ttf";
    private const string SemiBoldPath = FontDir + "/BeVietnamPro-SemiBold.ttf";

    private const string RegularAssetPath = FontAssetDir + "/BeVietnamPro-Regular SDF.asset";
    private const string SemiBoldAssetPath = FontAssetDir + "/BeVietnamPro-SemiBold SDF.asset";

    private const int SamplingPointSize = 90;
    private const int AtlasPadding = 9;
    private const int AtlasWidth = 2048;
    private const int AtlasHeight = 2048;

    [MenuItem("Tools/Setup Dialogue Font")]
    public static void Run()
    {
        TMP_FontAsset regular = EnsureFontAsset(RegularPath, RegularAssetPath, "BeVietnamPro-Regular SDF");
        TMP_FontAsset semiBold = EnsureFontAsset(SemiBoldPath, SemiBoldAssetPath, "BeVietnamPro-SemiBold SDF");

        if (regular == null)
        {
            Debug.LogError("[DialogueFontSetup] Không tạo được font asset chính.");
            return;
        }

        AddGlobalFallback(regular);
        if (semiBold != null) AddGlobalFallback(semiBold);

        AssetDatabase.SaveAssets();

        Debug.Log($"[DialogueFontSetup] Xong. Regular={regular.name} (glyph trong RAM={regular.glyphTable.Count}) " +
                  $"SemiBold={(semiBold != null ? semiBold.name : "null")} " +
                  $"| sourceFontFile={(regular.sourceFontFile != null ? regular.sourceFontFile.name : "NULL")} " +
                  $"| populationMode={regular.atlasPopulationMode} (Dynamic: glyph nạp lúc render)");
    }

    private static readonly char[] RequiredVietnamese =
        {
            'đ', 'ơ', 'ư', 'ạ', 'ả', 'ầ', 'ằ', 'ặ', 'ế', 'ệ', 'ị', 'ọ', 'ỗ', 'ộ',
            'ớ', 'ợ', 'ụ', 'ự', 'ỹ', 'À', 'Á', 'Ă', 'Â', 'Đ', 'È', 'Ê', 'Ò', 'Ô', 'Ơ', 'Ư', 'A', ' '
        };

    private static int CountMissing(TMP_FontAsset asset)
    {
        int missing = 0;
        foreach (char c in RequiredVietnamese)
        {
            if (!asset.HasCharacter(c)) missing++;
        }
        return missing;
    }

    private static TMP_FontAsset EnsureFontAsset(string sourcePath, string assetPath, string assetName)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null)
        {
            int missing = CountMissing(existing);
            if (missing > 0)
            {
                Debug.Log($"[DialogueFontSetup] '{assetName}' thiếu {missing} glyph, nạp lại " +
                          $"(characterTable={existing.characterTable.Count}).");
                PreWarm(existing);
            }
            else
            {
                Debug.Log($"[DialogueFontSetup] Đã có '{assetName}' ({existing.characterTable.Count} ký tự), dùng lại.");
            }
            return existing;
        }

        var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (source == null)
        {
            Debug.LogError($"[DialogueFontSetup] Không tìm thấy font nguồn '{sourcePath}'.");
            return null;
        }

        TMP_FontAsset created = null;
        try
        {
            created = TMP_FontAsset.CreateFontAsset(
                source,
                SamplingPointSize,
                AtlasPadding,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                AtlasWidth,
                AtlasHeight,
                AtlasPopulationMode.Dynamic,
                true);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DialogueFontSetup] CreateFontAsset thất bại cho '{assetPath}': {e.Message}");
            return null;
        }

        if (created == null)
        {
            Debug.LogError($"[DialogueFontSetup] CreateFontAsset trả về null cho '{assetPath}'.");
            return null;
        }

        created.name = assetName;

        EnsureFolder(System.IO.Path.GetDirectoryName(assetPath));

        AssetDatabase.CreateAsset(created, assetPath);
        if (created.material != null) AssetDatabase.AddObjectToAsset(created.material, created);
        if (created.atlasTextures != null)
        {
            foreach (Texture2D tex in created.atlasTextures)
            {
                if (tex == null) continue;
                tex.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(tex, created);
            }
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        var reloaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (reloaded == null) return created;

        PreWarm(reloaded);
        return reloaded;
    }

    private static void PreWarm(TMP_FontAsset asset)
    {
        var sb = new StringBuilder();
        AppendRange(sb, 0x20, 0x7E);
        AppendRange(sb, 0xA0, 0xFF);
        AppendRange(sb, 0x100, 0x17F);
        AppendRange(sb, 0x180, 0x1FF);
        AppendRange(sb, 0x1EA0, 0x1EF9);
        AppendRange(sb, 0x2013, 0x2014);
        AppendRange(sb, 0x2018, 0x201D);
        AppendRange(sb, 0x2026, 0x2026);

        string charset = sb.ToString();
        asset.TryAddCharacters(charset, true);

        int missing = 0;
        foreach (char c in charset)
        {
            if (asset.HasCharacter(c)) continue;
            missing++;
        }

        char[] must = RequiredVietnamese;
        var absent = new System.Collections.Generic.List<char>();
        foreach (char c in must)
        {
            if (!asset.HasCharacter(c)) absent.Add(c);
        }

        if (absent.Count > 0)
        {
            Debug.LogError($"[DialogueFontSetup] {asset.name} THIẾU glyph tiếng Việt: " +
                           string.Join(", ", absent.ConvertAll(c => c.ToString()).ToArray()));
        }
        else
        {
            Debug.Log($"[DialogueFontSetup] {asset.name}: OK, đủ glyph tiếng Việt.");
        }

        Debug.Log($"[DialogueFontSetup] {asset.name}: nạp trước {charset.Length} ký tự, " +
                  $"thiếu {missing} mã không có trong font, tổng glyph={asset.glyphTable.Count}.");

        int atlases = 0;
        if (asset.atlasTextures != null)
        {
            foreach (Texture2D tex in asset.atlasTextures)
            {
                if (tex == null) continue;
                EditorUtility.SetDirty(tex);
                atlases++;
            }
        }

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        Debug.Log($"[DialogueFontSetup] {asset.name}: RAM glyphTable={asset.glyphTable.Count} " +
                  $"characterTable={asset.characterTable.Count} atlas={atlases}");

        int afterMissing = CountMissing(asset);
        if (afterMissing > 0)
        {
            Debug.LogError($"[DialogueFontSetup] {asset.name} thiếu {afterMissing}/{RequiredVietnamese.Length} glyph " +
                           "sau khi nạp trước.");
        }
    }

    /// TMP ở chế độ Dynamic không lưu bảng glyph/atlas vào .asset (m_ClearDynamicDataOnBuild = 1),
    /// nên sau khi mở lại project characterTable luôn rỗng và glyph được nạp lại lúc render.
    /// Đừng kết luận font hỏng dựa vào HasCharacter; hãy chạy DialogueValidate (render thật).
    private static void AppendRange(StringBuilder sb, int from, int to)
    {
        for (int cp = from; cp <= to; cp++) sb.Append((char)cp);
    }

    private static void AddGlobalFallback(TMP_FontAsset asset)
    {
        var fallbacks = TMP_Settings.fallbackFontAssets;
        if (fallbacks == null) return;

        int dangling = fallbacks.RemoveAll(f => f == null);
        if (dangling > 0) Debug.LogWarning($"[DialogueFontSetup] Đã gỡ {dangling} fallback font asset bị mất.");

        if (fallbacks.Contains(asset)) return;

        fallbacks.Add(asset);

        TMP_Settings settings = TMP_Settings.instance;
        if (settings != null) EditorUtility.SetDirty(settings);

        Debug.Log($"[DialogueFontSetup] Đã thêm '{asset.name}' vào TMP fallback fonts ({fallbacks.Count} mục).");
    }

    internal static void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}