using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Collections;
using System;

public class PhoneUIManager : MonoBehaviour
{
    [Header("Khóa Camera & Nhân Vật Khi Mở Điện Thoại")]
    public List<MonoBehaviour> scriptsToDisableWhenPhoneOpen;

    private UIDocument uiDocument;
    private VisualElement phoneUI;
    private VisualElement appPage;
    private Label appTitle;
    private ScrollView appBody;
    private Label timeLabel;
    private Label calendarMonth;
    private Label calendarDay;
    private Label toastLabel;

    private Button btnTogglePhone;
    private Button btnHome;
    private Button btnCloseApp;

    private bool isPhoneOpen = false;
    private Coroutine toastCoroutine;

    void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;

        var root = uiDocument.rootVisualElement;

        // Lấy thành phần UI chính
        phoneUI = root.Q<VisualElement>(className: "phone");
        appPage = root.Q<VisualElement>("app-page");
        appTitle = root.Q<Label>("app-title");
        appBody = root.Q<ScrollView>("app-body");
        timeLabel = root.Q<Label>("time-label");
        calendarMonth = root.Q<Label>("calendar-month");
        calendarDay = root.Q<Label>("calendar-day");
        toastLabel = root.Q<Label>("toast");

        // Các nút bật/tắt & Home
        btnTogglePhone = root.Q<Button>("btn-toggle-phone");
        if (btnTogglePhone != null) btnTogglePhone.clicked += TogglePhoneState;

        btnHome = root.Q<Button>("btn-home");
        if (btnHome != null) btnHome.clicked += CloseAppOrToggle;

        btnCloseApp = root.Q<Button>("btn-close");
        if (btnCloseApp != null) btnCloseApp.clicked += CloseApp;

        // --- ĐĂNG KÝ 16 APP MÀN HÌNH CHÍNH ---
        RegisterApp("app-safari", "Safari", "search");
        RegisterApp("app-messages", "Tin nhắn", "messages");
        RegisterApp("app-photos", "Ảnh", "photos");
        RegisterApp("app-camera", "Camera", "camera");
        RegisterApp("app-music", "Âm nhạc", "music");
        RegisterApp("app-settings", "Cài đặt", "settings");
        RegisterApp("app-calendar", "Lịch", "calendar");
        RegisterApp("app-clock", "Đồng hồ", "clock");
        RegisterApp("app-weather", "Thời tiết", "weather");
        RegisterApp("app-notes", "Ghi chú", "notes");
        RegisterApp("app-appstore", "App Store", "appstore");
        RegisterApp("app-calculator", "Máy tính", "calculator");
        RegisterApp("app-mail", "Mail", "mail");
        RegisterApp("app-maps", "Bản đồ", "maps");
        RegisterApp("app-files", "Tệp", "files");
        RegisterApp("app-contacts", "Danh bạ", "contacts");

        // --- ĐĂNG KÝ APP Ở THANH DOCK ---
        RegisterApp("dock-phone", "Điện thoại", "phone");
        RegisterApp("dock-safari", "Safari", "search");
        RegisterApp("dock-messages", "Tin nhắn", "messages");
        RegisterApp("dock-music", "Âm nhạc", "music");

        UpdatePhoneVisibility();
    }

    void Update()
    {
        // Cập nhật thời gian thực
        DateTime now = DateTime.Now;
        if (timeLabel != null) timeLabel.text = now.ToString("HH:mm");
        if (calendarDay != null) calendarDay.text = now.ToString("dd");
        if (calendarMonth != null) calendarMonth.text = "THÁNG " + now.Month;

        // Bấm phím F để bật/tắt nhanh điện thoại
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            TogglePhoneState();
        }
    }

    private void RegisterApp(string buttonName, string title, string appType)
    {
        var btn = uiDocument.rootVisualElement.Q<Button>(buttonName);
        if (btn != null)
        {
            btn.clicked += () => OpenApp(title, appType);
        }
    }

    private void OpenApp(string title, string appType)
    {
        if (appTitle != null) appTitle.text = title;

        // Tạo giao diện bên trong App động theo kiểu dáng Card
        if (appBody != null)
        {
            appBody.Clear();
            BuildAppBodyContent(appType);
        }

        appPage?.AddToClassList("open");
    }

    private void BuildAppBodyContent(string appType)
    {
        switch (appType)
        {
            case "search":
                AddCard("⌕ Tìm kiếm", "Nhập địa chỉ trang web hoặc từ khóa...");
                AddBigEmoji("🌐");
                AddCard("Trang web yêu thích", "• Unity Documentation\n• Google Search\n• Github");
                break;

            case "messages":
                AddCard("💬 Minh", "Chào bạn! Hôm nay có làm tiếp game không? 👋");
                AddCard("💬 Nhóm Học Tập", "Hẹn gặp mọi người lúc 19:00 nhé.");
                break;

            case "photos":
                AddBigEmoji("🌈");
                AddCard("Thư viện ảnh", "• 🌄 Phong cảnh (12)\n• 🐱 Động vật (5)\n• 🏙️ Thành phố (30)");
                break;

            case "camera":
                AddBigEmoji("📷");
                AddCard("Camera", "Đang mở ống kính camera...");
                break;

            case "music":
                AddBigEmoji("🎵");
                AddCard("Bài hát đang phát", "Lo-Fi Chill Beats for Coding");
                break;

            case "settings":
                AddCard("📶 Wi-Fi", "Đang kết nối: Home_Network_5G");
                AddCard("🔵 Bluetooth", "Đã bật");
                AddCard("🌙 Chế độ tối", "Bật");
                break;

            case "calendar":
                AddBigEmoji("📅");
                AddCard("Sự kiện hôm nay", "Không có lịch trình nào cho hôm nay.");
                break;

            case "clock":
                AddBigEmoji("🕘");
                AddCard("Giờ hiện tại", DateTime.Now.ToString("HH:mm:ss"));
                break;

            case "weather":
                AddBigEmoji("☀️");
                AddCard("TP. Hồ Chí Minh", "29°C · Trời quang mây tạnh");
                break;

            case "notes":
                AddCard("📝 Danh sách công việc", "☐ Làm bài tập Unity UI Toolkit\n☐ Khóa camera nhân vật\n☐ Gắn script hoàn chỉnh");
                break;

            case "appstore":
                AddCard("⭐ Ứng dụng nổi bật", "Khám phá các ứng dụng mới nhất.");
                break;

            case "calculator":
                AddBigEmoji("🧮");
                AddCard("Máy tính", "0");
                break;

            case "mail":
                AddCard("✉ Hộp thư đến", "Bạn có 2 thư mới chưa đọc.");
                break;

            case "maps":
                AddBigEmoji("🗺️");
                AddCard("Vị trí hiện tại", "Thành phố Trung Tâm - Khu vực 1");
                break;

            case "files":
                AddCard("📁 Tệp của tôi", "• Documents/\n• Projects/\n• Downloads/");
                break;

            case "contacts":
                AddCard("👤 Minh", "SĐT: 0123 456 789");
                AddCard("👤 Tân", "SĐT: 0987 654 321");
                break;

            case "phone":
                AddCard("☎ Cuộc gọi gần đây", "Minh (2 phút trước)\nTân (Hôm qua)");
                break;
        }
    }

    private void AddCard(string title, string text)
    {
        VisualElement card = new VisualElement();
        card.AddToClassList("card");

        Label cardTitle = new Label(title);
        cardTitle.AddToClassList("card-title");

        Label cardText = new Label(text);
        cardText.AddToClassList("card-text");

        card.Add(cardTitle);
        card.Add(cardText);
        appBody.Add(card);
    }

    private void AddBigEmoji(string emoji)
    {
        Label label = new Label(emoji);
        label.AddToClassList("big-emoji");
        appBody.Add(label);
    }

    private void CloseAppOrToggle()
    {
        if (appPage != null && appPage.ClassListContains("open"))
        {
            CloseApp();
        }
        else
        {
            TogglePhoneState();
        }
    }

    private void CloseApp()
    {
        appPage?.RemoveFromClassList("open");
    }

    private void TogglePhoneState()
    {
        isPhoneOpen = !isPhoneOpen;
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
            if (script != null) script.enabled = !phoneIsOpen;
        }
    }

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