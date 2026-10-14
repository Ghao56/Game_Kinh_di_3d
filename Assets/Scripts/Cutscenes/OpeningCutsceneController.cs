using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Video;

[DisallowMultipleComponent]
public class OpeningCutsceneController : MonoBehaviour
{
    [Header("Flow")]
    public bool playOnStart = true;

    [Header("Timeline")]
    public PlayableDirector director;

    [Header("Cameras")]
    public Camera exteriorCamera;
    public Camera interiorCamera;
    public Camera gameplayCamera;

    [Header("Fade")]
    public GameObject fadeCanvas;
    public CanvasGroup fadeGroup;

    [Header("Player")]
    public ThirdPersonController playerController;
    public Interactor playerInteractor;

    [Header("Content")]
    public DialogueData introDialogue;
    public VideoPlayer footballVideo;

    bool _captured;
    bool _restored;
    bool _prevControllerEnabled;
    bool _prevInteractorEnabled;

    void OnEnable()
    {
        if (director != null) director.stopped += OnDirectorStopped;
    }

    void OnDisable()
    {
        if (director != null) director.stopped -= OnDirectorStopped;
        RestorePlayerControl();
    }

    void Start()
    {
        if (playOnStart) Play();
    }

    public void Play()
    {
        CaptureState();

        SetPlayerControl(false);

        if (fadeCanvas != null) fadeCanvas.SetActive(true);
        if (fadeGroup != null)
        {
            fadeGroup.alpha = 1f;
            fadeGroup.blocksRaycasts = true;
            fadeGroup.interactable = false;
        }

        SetActiveCamera(exteriorCamera);

        if (footballVideo != null)
        {
            footballVideo.Stop();
            footballVideo.time = 0.0;
        }

        if (director != null)
        {
            director.time = 0.0;
            director.Play();
        }
    }

    void CaptureState()
    {
        if (_captured) return;
        _captured = true;
        _restored = false;
        if (playerController != null) _prevControllerEnabled = playerController.enabled;
        if (playerInteractor != null) _prevInteractorEnabled = playerInteractor.enabled;
    }

    void SetPlayerControl(bool enabled)
    {
        if (playerController != null) playerController.enabled = enabled;
        if (playerInteractor != null) playerInteractor.enabled = enabled;
    }

    void RestorePlayerControl()
    {
        if (!_captured || _restored) return;
        _restored = true;
        if (playerController != null) playerController.enabled = _prevControllerEnabled;
        if (playerInteractor != null) playerInteractor.enabled = _prevInteractorEnabled;
    }

    void SetActiveCamera(Camera cam)
    {
        if (exteriorCamera != null) exteriorCamera.enabled = (exteriorCamera == cam);
        if (interiorCamera != null) interiorCamera.enabled = (interiorCamera == cam);
        if (gameplayCamera != null) gameplayCamera.enabled = (gameplayCamera == cam);
    }

    // ---- Signal receivers (called by Timeline SignalReceiver) ----

    public void OnSignal_ShowInterior()
    {
        SetActiveCamera(interiorCamera);
    }

    public void OnSignal_PlayTV()
    {
        if (footballVideo == null) return;
        footballVideo.Stop();
        footballVideo.time = 0.0;
        footballVideo.Play();
    }

    public void OnSignal_ShowVaoDi()
    {
        if (DialogueManager.Instance == null || introDialogue == null) return;
        if (director != null) director.Pause();
        DialogueManager.Instance.ShowDialogue(introDialogue, OnIntroDialogueComplete);
    }

    void OnIntroDialogueComplete()
    {
        if (director != null) director.Play();
    }

    public void OnSignal_SwitchToGameplay()
    {
        SetActiveCamera(gameplayCamera);
        RestorePlayerControl();
    }

    void OnDirectorStopped(PlayableDirector d)
    {
        if (d != director) return;
        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
            fadeGroup.interactable = false;
        }
        if (fadeCanvas != null) fadeCanvas.SetActive(false);
        SetActiveCamera(gameplayCamera);
        RestorePlayerControl();
    }
}
