using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System;

public class PhoneUIManager : MonoBehaviour
{
    [Header("Khóa Camera & Nhân Vật Khi Mở Điện Thoại")]
    [Tooltip("Kéo script xoay camera + script di chuyển nhân vật vào đây. Để trống thì camera/di chuyển sẽ KHÔNG bị khóa.")]
    [SerializeField] private List<MonoBehaviour> scriptsToDisableWhenPhoneOpen;

    [Header("Hội thoại hoảng hốt của Cha")]
    [Tooltip("Mỗi dòng của Cha sẽ được tự động trả lời bằng dòng cùng chỉ số trong danh sách bên dưới")]
    [TextArea(2, 3)]
    [SerializeField]
    private string[] fatherPanicLines =
    {
        "Con ơi... con đang ở đâu vậy?!",
        "Trời ơi, mau trả lời cha đi!!",
        "Hình như... có ai đó đang ở ngoài cửa nhà mình!",
        "Làm ơn đừng về nhà vội, nguy hiểm lắm con ơi!",
        "CHA SỢ LẮM... con có nghe thấy cha nói gì không?!",
        "Xin con... hãy cẩn thận..."
    };

    [TextArea(2, 3)]
    [SerializeField]
    private string[] autoReplyLines =
    {
        "Cha ơi bình tĩnh lại đã, con đang nghe đây.",
        "Dạ con nghe rồi, cha đừng lo quá.",
        "Cha khóa hết cửa lại đi, đừng mở cho ai cả.",
        "Được rồi, con sẽ cẩn thận. Cha giữ bình tĩnh nhé.",
        "Con đây, con đang cố gắng về nhanh nhất có thể.",
        "Cha ơi... cha vẫn ổn chứ?"
    };

    [SerializeField] private float timeBetweenFatherMessages = 20f;
    [SerializeField] private float autoReplyDelayMin = 2f;
    [SerializeField] private float autoReplyDelayMax = 4f;

    private struct ChatMessage
    {
        public bool isFromPlayer;
        public string text;
        public string timeLabel;
    }

    private readonly List<ChatMessage> messageHistory = new List<ChatMessage>();
    private int unreadCount = 0;

    private UIDocument uiDocument;
    private VisualElement phoneUI;
    private VisualElement appPage;
    private VisualElement messageList;
    private ScrollView messageScroll;
    private Label appTitle;
    private Label timeLabel;
    private Label toastLabel;
    private Label messagesBadge;
    private Label dockMessagesBadge;

    private Button btnTogglePhone;
    private Button btnHome;
    private Button btnCloseApp;

    private bool isPhoneOpen = false;
    private bool isMessagesAppOpen = false;
    private Coroutine toastCoroutine;
    private Coroutine conversationCoroutine;

    public bool IsPhoneOpen => isPhoneOpen;

    private Action onToggleClicked;
    private Action onHomeClicked;
    private Action onCloseAppClicked;
    private readonly List<(Button button, Action handler)> appSubscriptions = new List<(Button, Action)>();

    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogError($"{nameof(PhoneUIManager)}: thiếu component UIDocument trên object này!", this);
            enabled = false;
            return;
        }

        var root = uiDocument.rootVisualElement;

        phoneUI = root.Q<VisualElement>(className: "phone");
        timeLabel = root.Q<Label>("time-label");
        toastLabel = root.Q<Label>("toast");

        appPage = root.Q<VisualElement>("app-page");
        appTitle = root.Q<Label>("app-title");
        messageScroll = root.Q<ScrollView>("message-scroll");
        messageList = root.Q<VisualElement>("message-list");

        messagesBadge = root.Q<Label>("messages-badge");
        dockMessagesBadge = root.Q<Label>("dock-messages-badge");

        if (phoneUI == null)
            Debug.LogWarning($"{nameof(PhoneUIManager)}: không tìm thấy class 'phone' trong UXML.", this);

        if (scriptsToDisableWhenPhoneOpen == null || scriptsToDisableWhenPhoneOpen.Count == 0)
            Debug.LogWarning($"{nameof(PhoneUIManager)}: 'scriptsToDisableWhenPhoneOpen' đang TRỐNG, camera/di chuyển sẽ không bị khóa khi mở điện thoại.", this);

        btnTogglePhone = root.Q<Button>("btn-toggle-phone");
        onToggleClicked = TogglePhoneState;
        if (btnTogglePhone != null) btnTogglePhone.clicked += onToggleClicked;

        btnHome = root.Q<Button>("btn-home");
        onHomeClicked = TogglePhoneState;
        if (btnHome != null) btnHome.clicked += onHomeClicked;

        btnCloseApp = root.Q<Button>("btn-close");
        onCloseAppClicked = CloseApp;
        if (btnCloseApp != null) btnCloseApp.clicked += onCloseAppClicked;

        // --- 6 app màn hình chính ---
        RegisterApp("app-safari", "Safari");
        RegisterApp("app-messages", "Tin nhắn");
        RegisterApp("app-photos", "Ảnh");
        RegisterApp("app-camera", "Camera");
        RegisterApp("app-settings", "Cài đặt");
        RegisterApp("app-weather", "Thời tiết");

        // --- 4 app thanh Dock ---
        RegisterApp("dock-phone", "Điện thoại");
        RegisterApp("dock-safari", "Safari");
        RegisterApp("dock-messages", "Tin nhắn");
        RegisterApp("dock-music", "Nhạc");

        UpdatePhoneVisibility();
        UpdateBadges();

        if (conversationCoroutine == null)
            conversationCoroutine = StartCoroutine(FatherConversationRoutine());
    }

    private void OnDisable()
    {
        if (btnTogglePhone != null) btnTogglePhone.clicked -= onToggleClicked;
        if (btnHome != null) btnHome.clicked -= onHomeClicked;
        if (btnCloseApp != null) btnCloseApp.clicked -= onCloseAppClicked;

        foreach (var (button, handler) in appSubscriptions)
        {
            if (button != null) button.clicked -= handler;
        }
        appSubscriptions.Clear();

        if (conversationCoroutine != null)
        {
            StopCoroutine(conversationCoroutine);
            conversationCoroutine = null;
        }
    }

    private void RegisterApp(string buttonName, string appName)
    {
        var btn = uiDocument.rootVisualElement.Q<Button>(buttonName);
        if (btn == null) return;

        Action handler = () => OpenApp(appName);
        btn.clicked += handler;
        appSubscriptions.Add((btn, handler));
    }

    private void Update()
    {
        DateTime now = DateTime.Now;
        if (timeLabel != null) timeLabel.text = now.ToString("HH:mm");

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            TogglePhoneState();
    }

    private void TogglePhoneState()
    {
        isPhoneOpen = !isPhoneOpen;
        if (!isPhoneOpen) CloseApp();
        UpdatePhoneVisibility();
    }

    private void UpdatePhoneVisibility()
    {
        if (phoneUI != null)
            phoneUI.style.display = isPhoneOpen ? DisplayStyle.Flex : DisplayStyle.None;

        if (btnTogglePhone != null)
            btnTogglePhone.style.display = isPhoneOpen ? DisplayStyle.None : DisplayStyle.Flex;

        SetCursorState(isPhoneOpen);
    }

    private void SetCursorState(bool phoneIsOpen)
    {
        UnityEngine.Cursor.lockState = phoneIsOpen ? CursorLockMode.None : CursorLockMode.Locked;
        UnityEngine.Cursor.visible = phoneIsOpen;

        if (scriptsToDisableWhenPhoneOpen == null) return;

        foreach (var script in scriptsToDisableWhenPhoneOpen)
        {
            if (script != null)
                script.enabled = !phoneIsOpen;
        }
    }

    private void OpenApp(string appName)
    {
        if (appName == "Tin nhắn")
        {
            isMessagesAppOpen = true;
            unreadCount = 0;
            UpdateBadges();
            if (appTitle != null) appTitle.text = "Cha";
            RenderMessageList();
            appPage?.AddToClassList("open");
        }
        else
        {
            ShowToast("Đang mở: " + appName);
            Debug.Log("Đã bấm vào ứng dụng: " + appName);
        }
    }

    private void CloseApp()
    {
        isMessagesAppOpen = false;
        appPage?.RemoveFromClassList("open");
    }

    // --- Hội thoại tự động: Cha nhắn hoảng hốt, hệ thống tự trả lời ---
    private IEnumerator FatherConversationRoutine()
    {
        int count = Mathf.Min(fatherPanicLines.Length, autoReplyLines.Length);

        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(timeBetweenFatherMessages);
            AddMessage(isFromPlayer: false, text: fatherPanicLines[i]);

            float delay = UnityEngine.Random.Range(autoReplyDelayMin, autoReplyDelayMax);
            yield return new WaitForSeconds(delay);
            AddMessage(isFromPlayer: true, text: autoReplyLines[i]);
        }

        conversationCoroutine = null;
    }

    private void AddMessage(bool isFromPlayer, string text)
    {
        messageHistory.Add(new ChatMessage
        {
            isFromPlayer = isFromPlayer,
            text = text,
            timeLabel = DateTime.Now.ToString("HH:mm")
        });

        if (!isFromPlayer && !isMessagesAppOpen)
        {
            unreadCount++;
            UpdateBadges();
            ShowToast("📩 Cha: " + text);
        }

        if (isMessagesAppOpen)
            RenderMessageList();
    }

    private void UpdateBadges()
    {
        string text = unreadCount > 0 ? (unreadCount > 9 ? "9+" : unreadCount.ToString()) : "";
        bool show = unreadCount > 0;

        if (messagesBadge != null)
        {
            messagesBadge.text = text;
            messagesBadge.EnableInClassList("show", show);
        }

        if (dockMessagesBadge != null)
        {
            dockMessagesBadge.text = text;
            dockMessagesBadge.EnableInClassList("show", show);
        }
    }

    private void RenderMessageList()
    {
        if (messageList == null) return;

        messageList.Clear();

        var row = new VisualElement();
        row.AddToClassList("msg-row");

        foreach (var msg in messageHistory)
        {
            var bubble = new VisualElement();
            bubble.AddToClassList("msg-bubble");
            bubble.AddToClassList(msg.isFromPlayer ? "msg-bubble-player" : "msg-bubble-father");

            var textLabel = new Label(msg.text);
            textLabel.AddToClassList(msg.isFromPlayer ? "msg-text-player" : "msg-text-father");
            bubble.Add(textLabel);

            var timeTag = new Label(msg.timeLabel);
            timeTag.AddToClassList("msg-time");
            bubble.Add(timeTag);

            row.Add(bubble);
        }

        messageList.Add(row);

        messageScroll?.schedule.Execute(() =>
        {
            messageScroll.scrollOffset = new Vector2(0, messageList.worldBound.height);
        });
    }

    // --- Toast ---
    public void ShowToast(string message)
    {
        if (toastCoroutine != null) StopCoroutine(toastCoroutine);
        toastCoroutine = StartCoroutine(ShowToastRoutine(message));
    }

    private IEnumerator ShowToastRoutine(string message)
    {
        if (toastLabel == null) yield break;

        toastLabel.text = message;
        toastLabel.AddToClassList("show");

        yield return new WaitForSeconds(1.5f);

        toastLabel.RemoveFromClassList("show");
    }
}