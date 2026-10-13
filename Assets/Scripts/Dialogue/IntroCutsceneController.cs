using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

/// Điều khiển cutscene mở đầu: đen -> panorama đô thị -> tiến về House_Player -> xem TV (video
/// Football) -> thoại "Vào đi" rồi mới thở dài -> trả lại trạng thái chơi game. Chạy một lần.
///
/// Tham chiếu nên gán trong Inspector. Nếu để trống, Awake tự tìm theo tên/kiểu đúng một lần.
/// Tự khoá nhân vật (ThirdPersonController/Interactor) trong lúc chạy và luôn khôi phục trạng
/// thái cũ ở cuối (kể cả khi bị dừng nửa chừng).
public class IntroCutsceneController : MonoBehaviour
{
    [Header("Phân cảnh")]
    [Tooltip("Bật để tự chạy cutscene khi scene mở.")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private GameObject cameraUrban;
    [SerializeField] private GameObject cameraApproach;
    [SerializeField] private GameObject cameraWatch;
    [Tooltip("Điểm camera 2 dừng lại. Để trống: lấy vị trí House_Player + lệch nhẹ.")]
    [SerializeField] private Transform approachEnd;
    [Tooltip("Đối tượng camera hướng về. Để trống: tự tìm House_Player.")]
    [SerializeField] private Transform houseTarget;

    [Header("UI / Nhân vật")]
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private ThirdPersonController playerController;
    [SerializeField] private Interactor playerInteractor;

    [Header("Thoại & video & âm thanh")]
    [Tooltip("Cuộn thoại cutscene (Cutscreen_1).")]
    [SerializeField] private DialogueData dialogue;
    [Tooltip("VideoPlayer của TV (TV_VideoPlayer). Để trống: tự tìm trong scene.")]
    [SerializeField] private VideoPlayer footballVideo;
    [Tooltip("Tiếng thở dài, phát sau khi thoại hoàn tất. Để trống nếu chưa có asset.")]
    [SerializeField] private AudioClip sighClip;
    [SerializeField] private AudioSource sighSource;

    [Header("Thời gian")]
    [SerializeField] private float fadeInDuration = 1.2f;
    [SerializeField] private float urbanEstablishDuration = 2.5f;
    [SerializeField] private float cameraApproachDuration = 6f;
    [SerializeField] private float watchSceneDelay = 0.8f;
    [SerializeField] private float dialogueStartDelay = 0.8f;

    private const float MaxSighWait = 5f;

    private readonly List<GameObject> cutsceneCameras = new List<GameObject>();
    private GameObject originalActiveCamera;
    private bool controllerEnabled;
    private bool interactorEnabled;
    private bool hasPlayed;
    private bool finished;
    private Coroutine routine;

    private void Awake()
    {
        if (cameraUrban == null) cameraUrban = GameObject.Find("CutsceneCamera_1");
        if (cameraApproach == null) cameraApproach = GameObject.Find("CutsceneCamera_2");
        if (cameraWatch == null) cameraWatch = GameObject.Find("CutsceneCamera_3");

        cutsceneCameras.Clear();
        if (cameraUrban != null) cutsceneCameras.Add(cameraUrban);
        if (cameraApproach != null) cutsceneCameras.Add(cameraApproach);
        if (cameraWatch != null) cutsceneCameras.Add(cameraWatch);
        if (cutsceneCameras.Count == 0)
            Debug.LogWarning($"[{nameof(IntroCutsceneController)}] Không tìm thấy camera cutscene nào.", this);

        if (fadeGroup == null)
        {
            var fade = GameObject.Find("FadePanel");
            if (fade != null) fadeGroup = fade.GetComponent<CanvasGroup>();
        }
        if (fadeGroup == null)
            Debug.LogWarning($"[{nameof(IntroCutsceneController)}] Thiếu FadePanel (CanvasGroup). Cutscene không fade được.", this);

        if (playerController == null) playerController = FindFirstObjectByType<ThirdPersonController>();
        if (playerInteractor == null) playerInteractor = FindFirstObjectByType<Interactor>();

        if (footballVideo == null) footballVideo = FindFirstObjectByType<VideoPlayer>();
        if (footballVideo == null)
            Debug.LogWarning($"[{nameof(IntroCutsceneController)}] Không tìm thấy VideoPlayer (TV_VideoPlayer). Bỏ qua cảnh xem TV.", this);

        if (houseTarget == null)
        {
            var house = GameObject.Find("House_Player");
            if (house != null) houseTarget = house.transform;
        }

        if (sighSource == null) sighSource = GetComponent<AudioSource>();
        if (sighSource == null) sighSource = gameObject.AddComponent<AudioSource>();
        sighSource.playOnAwake = false;
    }

    private void Start()
    {
        if (hasPlayed || !playOnStart) return;
        routine = StartCoroutine(PlayCutscene());
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        StopAllCoroutines();
        if (hasPlayed && !finished) RestoreGameplay();
    }

    private IEnumerator PlayCutscene()
    {
        if (hasPlayed) yield break;
        hasPlayed = true;

        try
        {
            // Luôn bắt đầu từ màn hình đen và khoá điều khiển nhân vật.
            LockPlayer();
            SetBlack(true);

            // Cảnh 1: panorama đô thị.
            SwitchCamera(cameraUrban);
            yield return FadeTo(0f, fadeInDuration);
            yield return WaitForSecondsRealtime(urbanEstablishDuration);

            // Cảnh 2: camera tiến gần về phía nhà (House_Player), rồi vào trong.
            SwitchCamera(cameraApproach);
            yield return ApproachHouse(cameraApproach, cameraApproachDuration);

            // Cảnh 3: nhìn màn hình TV, bật video Football (chạy tiếp sau cutscene).
            SwitchCamera(cameraWatch);
            PlayFootball();
            yield return WaitForSecondsRealtime(watchSceneDelay);
            yield return WaitForSecondsRealtime(dialogueStartDelay);

            // Cảnh 4: thoại "Vào đi" hoàn tất rồi mới phát tiếng thở dài.
            if (dialogue == null || DialogueManager.Instance == null)
            {
                Debug.LogWarning($"[{nameof(IntroCutsceneController)}] Thiếu dialogue hoặc DialogueManager. Bỏ qua thoại.", this);
                yield return WaitForSecondsRealtime(1f);
            }
            else
            {
                DialogueManager.Instance.ShowDialogue(dialogue, OnDialogueComplete);
                while (!finished) yield return null;
            }
        }
        finally
        {
            // Luôn trả lại trạng thái chơi game dù cutscene bị dừng giữa chừng.
            RestoreGameplay();
        }
    }

    private IEnumerator ApproachHouse(GameObject cam, float duration)
    {
        if (cam == null) yield break;

        Transform t = cam.transform;
        Vector3 startPos = t.position;
        Vector3 endPos;
        if (approachEnd != null)
            endPos = approachEnd.position;
        else if (houseTarget != null)
            endPos = houseTarget.position + houseTarget.forward * 3f + Vector3.up * 1.2f;
        else
        {
            Debug.LogWarning($"[{nameof(IntroCutsceneController)}] Thiếu approachEnd và House_Player nên camera không di chuyển.", this);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float s = Mathf.Clamp01(elapsed / duration);
            s = s * s * (3f - 2f * s); // smoothstep: chậm - nhanh - chậm
            t.position = Vector3.Lerp(startPos, endPos, s);
            if (houseTarget != null) t.LookAt(houseTarget);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        t.position = endPos;
        if (houseTarget != null) t.LookAt(houseTarget);
    }

    private void OnDialogueComplete()
    {
        // Thoại đã hoàn tất (cả "Vào đi" lẫn dòng thở dài) mới đến bước này.
        if (sighClip != null)
        {
            sighSource.PlayOneShot(sighClip, 1f);
            StartCoroutine(WaitSighThenFinish());
        }
        else
        {
            finished = true;
        }
    }

    private IEnumerator WaitSighThenFinish()
    {
        float waited = 0f;
        while (sighSource.isPlaying && waited < MaxSighWait)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        finished = true;
    }

    private void PlayFootball()
    {
        if (footballVideo == null) return;
        footballVideo.Stop();
        footballVideo.time = 0d;
        footballVideo.Play();
    }

    private void LockPlayer()
    {
        originalActiveCamera = ActiveCamera();

        if (playerController != null)
        {
            controllerEnabled = playerController.enabled;
            playerController.enabled = false;
        }
        if (playerInteractor != null)
        {
            interactorEnabled = playerInteractor.enabled;
            playerInteractor.enabled = false;
        }
    }

    private void RestoreGameplay()
    {
        if (playerController != null) playerController.enabled = controllerEnabled;
        if (playerInteractor != null) playerInteractor.enabled = interactorEnabled;

        // Bật lại đúng camera đang dùng trước khi cutscene chạy.
        foreach (var cam in cutsceneCameras)
        {
            if (cam == null) continue;
            cam.SetActive(cam == originalActiveCamera);
        }
        if (originalActiveCamera != null && !originalActiveCamera.activeSelf)
            originalActiveCamera.SetActive(true);
    }

    private GameObject ActiveCamera()
    {
        foreach (var cam in cutsceneCameras)
        {
            if (cam != null && cam.activeSelf) return cam;
        }
        return cameraApproach;
    }

    private void SwitchCamera(GameObject target)
    {
        foreach (var cam in cutsceneCameras)
        {
            if (cam == null) continue;
            cam.SetActive(cam == target);
        }
        if (target != null && !target.activeSelf) target.SetActive(true);
    }

    private void SetBlack(bool black)
    {
        if (fadeGroup == null) return;
        fadeGroup.alpha = black ? 1f : 0f;
        fadeGroup.blocksRaycasts = black;
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        if (fadeGroup == null) yield break;
        float from = fadeGroup.alpha;
        if (duration <= 0f)
        {
            fadeGroup.alpha = target;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            fadeGroup.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / duration));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        fadeGroup.alpha = target;
    }

    private IEnumerator WaitForSecondsRealtime(float seconds)
    {
        float waited = 0f;
        while (waited < seconds)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}