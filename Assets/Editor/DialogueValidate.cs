using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Kiểm tra hệ thống thoại mà không cần vào Play Mode. Chạy lại được nhiều lần, không sửa gì.
public static class DialogueValidate
{
    private static int pass;
    private static int fail;

    [MenuItem("Tools/Validate Dialogue")]
    public static void Run()
    {
        pass = 0;
        fail = 0;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("[DialogueValidate] Hãy mở scene đã lưu (vd TestCutscreen.unity) trước khi chạy.");
            return;
        }

        var manager = Object.FindFirstObjectByType<DialogueManager>();
        Check(manager != null, "DialogueManager tồn tại trong scene");
        if (manager == null)
        {
            Report();
            return;
        }

        var so = new SerializedObject(manager);
        Check(Ref(so, "panelRoot") != null, "panelRoot được gán");
        Check(Ref(so, "panelGroup") != null, "panelGroup được gán");
        Check(Ref(so, "bodyText") != null, "bodyText được gán");
        Check(Ref(so, "speakerText") != null, "speakerText được gán");
        Check(Ref(so, "charJump") != null, "charJump được gán");
        Check(Ref(so, "continueIndicator") != null, "continueIndicator được gán");
        Check(Ref(so, "blipSource") != null, "blipSource được gán");
        Check(Ref(so, "blipClip") != null, "blipClip được gán");
        Check(Ref(so, "controller") != null, "controller được gán");
        Check(Ref(so, "cameraRig") != null, "cameraRig được gán");
        Check(Ref(so, "interactor") != null, "interactor được gán");

        var canvasGo = GameObject.Find("DialogueCanvas");
        var found = canvasGo != null ? canvasGo.GetComponent<Canvas>() : null;
        Check(found != null, "DialogueCanvas tồn tại");
        if (found != null)
        {
            Check(found.renderMode == RenderMode.ScreenSpaceOverlay, "Canvas là Screen Space - Overlay");
            Check(found.sortingOrder > 0, $"sortingOrder = {found.sortingOrder} (cao hơn HUD)");

            var others = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            bool highest = true;
            foreach (var c in others)
            {
                if (c == found) continue;
                if (c.sortingOrder >= found.sortingOrder) highest = false;
            }
            Check(highest, "DialogueCanvas cao hơn MỌI canvas khác trong scene");
        }

        var body = Ref(so, "bodyText") as TMP_Text;
        var speaker = Ref(so, "speakerText") as TMP_Text;
        Check(body != null && body.font != null, "bodyText có font");
        Check(speaker != null && speaker.font != null, "speakerText có font");

        if (body != null)
        {
            Check(!body.raycastTarget, "bodyText raycastTarget = false (không chặn chuột)");
            Check(body.richText, "bodyText bật rich text");
            Check(body.textWrappingMode == TextWrappingModes.Normal, "bodyText bật wrap chữ");
        }
        if (speaker != null) Check(!speaker.raycastTarget, "speakerText raycastTarget = false");

        var group = Ref(so, "panelGroup") as CanvasGroup;
        if (group != null)
        {
            Check(!group.interactable, "CanvasGroup.interactable = false");
            Check(!group.blocksRaycasts, "CanvasGroup.blocksRaycasts = false");
        }

        var bodyGo = body != null ? body.gameObject : null;
        Check(bodyGo != null && bodyGo.GetComponent<TMPCharJump>() != null, "TMPCharJump nằm trên bodyText");

        var clip = Ref(so, "blipClip") as AudioClip;
        Check(clip != null, "blipClip có gán");
        Check(clip != null && clip.channels == 1, "blip mono (channels=" + (clip != null ? clip.channels.ToString() : "null") + ")");

        var src = Ref(so, "blipSource") as AudioSource;
        Check(src != null, "blipSource có gán");
        if (src != null)
        {
            Check(!src.mute, "AudioSource không bị mute");
            Check(src.volume > 0f, "AudioSource volume=" + src.volume);
            Check(src.spatialBlend < 0.01f, "AudioSource 2D (spatialBlend=" + src.spatialBlend + ")");
            Check(!src.loop, "AudioSource không loop");
            Check(src.gameObject.activeInHierarchy, "AudioSource GameObject đang active");
            Check(src.enabled, "AudioSource component đang bật");
        }

        float minBlip = 0f;
        SerializedProperty minBlipProp = so.FindProperty("minBlipInterval");
        if (minBlipProp != null) minBlip = minBlipProp.floatValue;
        Check(minBlip <= 0f, $"minBlipInterval={minBlip} (0 = blip mỗi ký tự)");

        var listener = Object.FindFirstObjectByType<AudioListener>();
        Check(listener != null && listener.enabled, "Có AudioListener đang bật trong scene");

        Check(BlipPerCharacter(), "Logic blip: mỗi ký tự chữ đều kêu, khoảng trắng/dấu câu thì không");

        if (body != null && body.font != null)
        {
            var fa = body.font;
            Check(fa.atlasPopulationMode == AtlasPopulationMode.Dynamic,
                  $"font populationMode = {fa.atlasPopulationMode}");

            Check(GlyphsRender(fa, out string renderReport), "Glyph tiếng Việt render thật: " + renderReport);
        }

        var trigger = GameObject.Find("DialogueTrigger_Test");
        Check(trigger != null, "DialogueTrigger_Test tồn tại");
        if (trigger != null)
        {
            var col = trigger.GetComponent<Collider>();
            Check(col != null && col.isTrigger, "Collider của trigger đã bật Is Trigger");
            Check(trigger.GetComponent<DialogueTrigger>() != null, "có component DialogueTrigger");

            var tso = new SerializedObject(trigger.GetComponent<DialogueTrigger>());
            var data = Ref(tso, "dialogue") as DialogueData;
            Check(data != null, "trigger tham chiếu DialogueData");
            if (data != null)
            {
                Check(data.lines != null && data.lines.Length > 0, $"DialogueData có {data.lines?.Length ?? 0} dòng");
                bool hasVietnamese = false;
                if (data != null && data.lines != null)
                {
                    foreach (var line in data.lines)
                    {
                        if (line == null || string.IsNullOrEmpty(line.text)) continue;
                        foreach (char c in line.text)
                        {
                            int cp = c;
                            bool vietnamese = (cp >= 0x1EA0 && cp <= 0x1EF9) ||
                                              (cp >= 0x01A0 && cp <= 0x01FF) ||
                                              (cp >= 0x0100 && cp <= 0x017F) ||
                                              (cp >= 0x00C0 && cp <= 0x01FF);
                            if (vietnamese) { hasVietnamese = true; break; }
                        }
                        if (hasVietnamese) break;
                    }
                }
                Check(hasVietnamese, "dữ liệu có chữ tiếng Việt có dấu");
            }
        }

        int zones = Object.FindObjectsByType<QuestZone>(FindObjectsSortMode.None).Length;
        int objs = Object.FindObjectsByType<QuestObjectiveInteractable>(FindObjectsSortMode.None).Length;
        Check(zones > 0 && objs > 0, $"UI quest còn nguyên: {zones} QuestZone, {objs} QuestObjectiveInteractable");

        Report();
    }

    /// Đo xem TMP có thực sự tạo mesh cho chữ tiếng Việt hay không.
/// Bỏ qua việc font đã pre-warm hay chưa: Dynamic mode tự thêm glyph khi render.
/// Kiểm tra đúng kiểu Undertale: mọi chữ cái đều kêu, khoảng trắng và dấu câu thì im.
private static bool BlipPerCharacter()
{
    var line = new DialogueLine { blipVolume = 0.4f, blipOnSpaces = false };

    if (!DialogueManager.ShouldPlayBlip('a', line)) return false;
    if (!DialogueManager.ShouldPlayBlip('N', line)) return false;
    if (!DialogueManager.ShouldPlayBlip('đ', line)) return false;
    if (!DialogueManager.ShouldPlayBlip('ộ', line)) return false;

    if (DialogueManager.ShouldPlayBlip(' ', line)) return false;
    if (DialogueManager.ShouldPlayBlip('\n', line)) return false;
    if (DialogueManager.ShouldPlayBlip('.', line)) return false;
    if (DialogueManager.ShouldPlayBlip(',', line)) return false;
    if (DialogueManager.ShouldPlayBlip('…', line)) return false;

    var noBlip = new DialogueLine { blipVolume = 0f };
    return !DialogueManager.ShouldPlayBlip('a', noBlip);
}

private static bool GlyphsRender(TMP_FontAsset font, out string report)
{
    const string sample = "Nguyễn Thị Hường: Độ quỷ ễ ợ ự ỹ ạ ả ầ ằ ặ";
    GameObject probe = null;
    try
    {
        probe = new GameObject("TMP_VietnameseProbe") { hideFlags = HideFlags.HideAndDontSave };
        TextMeshPro tmp = probe.AddComponent<TextMeshPro>();
        tmp.font = font;
        tmp.text = sample;
        tmp.ForceMeshUpdate();

        TMP_TextInfo info = tmp.textInfo;
        int visible = 0;
        int withGlyph = 0;
        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo c = info.characterInfo[i];
            if (c.elementType != TMP_TextElementType.Character) continue;
            visible++;
            if (c.textElement != null && c.textElement.glyph.index != 0) withGlyph++;
        }

        int atlasW = font.atlasTextures != null && font.atlasTextures.Length > 0 && font.atlasTextures[0] != null
            ? font.atlasTextures[0].width : 0;
        int atlasH = font.atlasTextures != null && font.atlasTextures.Length > 0 && font.atlasTextures[0] != null
            ? font.atlasTextures[0].height : 0;

        bool ok = visible > 0 && withGlyph == visible;
        report = $"chars={info.characterCount} visible={visible} coGlyph={withGlyph} " +
                 $"atlas={atlasW}x{atlasH} charTable={font.characterTable.Count}";
        return ok;
    }
    catch (System.Exception e)
    {
        report = "lỗi: " + e.Message;
        return false;
    }
    finally
    {
        if (probe != null) Object.DestroyImmediate(probe);
    }
}

private static Object Ref(SerializedObject so, string field)
    {
        SerializedProperty p = so.FindProperty(field);
        return p != null ? p.objectReferenceValue : null;
    }

    private static void Check(bool ok, string label)
    {
        if (ok) pass++;
        else fail++;
        Debug.Log($"[DialogueValidate] {(ok ? "PASS" : "FAIL")} - {label}");
    }

    private static void Report()
    {
        Debug.Log($"[DialogueValidate] ===== {pass} pass, {fail} fail =====");
        if (fail > 0) Debug.LogError($"[DialogueValidate] CÓ {fail} mục CHƯA ĐẠT.");
    }
}