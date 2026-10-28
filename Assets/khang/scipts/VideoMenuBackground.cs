
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

public class VideoMenuBackground : MonoBehaviour
{
    public UIDocument uiDocument;
    public VideoPlayer videoPlayer;
    public RenderTexture videoTexture;

    [Tooltip("So pixel dich video lech len goc tren trai de che watermark (VD: chu 'AI'). Tang dan cho den khi bi an het.")]
    public int hideCornerWatermarkPixels = 160;

    private VisualElement videoBackground;

    void Awake()
    {
        if (uiDocument == null || videoPlayer == null || videoTexture == null)
        {
            Debug.LogError("Chua gan du UIDocument, VideoPlayer hoac RenderTexture.");
            return;
        }

        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoTexture;
        videoPlayer.isLooping = true;
        videoPlayer.playOnAwake = false;

        videoBackground = new VisualElement();
        videoBackground.name = "video-background";
        videoBackground.pickingMode = PickingMode.Ignore;

        videoBackground.style.position = Position.Absolute;
        // Lech video len goc tren trai de watermark "AI" o goc roi ra khoi man hinh.
        // Van dam bao che kin toan man hinh vi keo dai them phia ben phai / duoi.
        videoBackground.style.left = -hideCornerWatermarkPixels;
        videoBackground.style.top = -hideCornerWatermarkPixels;
        videoBackground.style.right = 0;
        videoBackground.style.bottom = 0;
        videoBackground.style.backgroundImage =
            Background.FromRenderTexture(videoTexture);
        videoBackground.style.backgroundSize =
            new BackgroundSize(BackgroundSizeType.Cover);

        uiDocument.rootVisualElement.Insert(0, videoBackground);
    }

    void Start()
    {
        if (videoPlayer != null && videoPlayer.clip != null)
        {
            videoPlayer.Play();
        }
        else
        {
            Debug.LogError("VideoPlayer chua duoc gan Video Clip.");
        }
    }
}