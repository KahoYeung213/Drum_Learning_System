using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class LessonVideoPlayerUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject playerPanelRoot;
    [SerializeField] private RawImage videoSurface;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statusText;

    [Header("Controls")]
    [SerializeField] private Button playPauseButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Image playPauseIcon;
    [SerializeField] private Sprite playIcon;
    [SerializeField] private Sprite pauseIcon;
    [SerializeField] private Slider seekSlider;
    [SerializeField] private Slider volumeSlider;

    [Header("Video")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private RenderTexture renderTexture;
    [SerializeField] private Vector2Int renderTextureSize = new Vector2Int(1920, 1080);

    private bool ownsRenderTexture;
    private string currentVideoUrl = string.Empty;
    private LessonVideoData currentVideo;
    private bool autoPlayOnPrepare;
    private bool suppressSeekSliderCallback;

    public bool HasPlayableVideo(CourseLessonData lesson)
    {
        return TryGetPlayableVideoUrl(lesson, out _);
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureRenderTexture();
        BindEvents();
        InitializeControls();
        Hide();
    }

    private void Update()
    {
        UpdateSeekSlider();
    }

    private void OnDestroy()
    {
        UnbindEvents();
        ReleaseRenderTexture();
    }

    private void ResolveReferences()
    {
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
            if (videoPlayer == null)
            {
                videoPlayer = gameObject.AddComponent<VideoPlayer>();
            }
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
    }

    private void BindEvents()
    {
        if (playPauseButton != null)
        {
            playPauseButton.onClick.AddListener(TogglePlayPause);
        }

        if (stopButton != null)
        {
            stopButton.onClick.AddListener(StopVideo);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);
        }

        if (seekSlider != null)
        {
            seekSlider.onValueChanged.AddListener(OnSeekSliderChanged);
        }

        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener(OnVolumeSliderChanged);
        }

        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted += HandlePrepareCompleted;
            videoPlayer.errorReceived += HandleErrorReceived;
            videoPlayer.loopPointReached += HandleLoopPointReached;
        }
    }

    private void UnbindEvents()
    {
        if (playPauseButton != null)
        {
            playPauseButton.onClick.RemoveListener(TogglePlayPause);
        }

        if (stopButton != null)
        {
            stopButton.onClick.RemoveListener(StopVideo);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
        }

        if (seekSlider != null)
        {
            seekSlider.onValueChanged.RemoveListener(OnSeekSliderChanged);
        }

        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveListener(OnVolumeSliderChanged);
        }

        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= HandlePrepareCompleted;
            videoPlayer.errorReceived -= HandleErrorReceived;
            videoPlayer.loopPointReached -= HandleLoopPointReached;
        }
    }

    public bool PreviewLesson(CourseLessonData lesson)
    {
        if (!TryGetPlayableVideoUrl(lesson, out LessonVideoData videoData))
        {
            SetStatus("No playable video file is configured for this lesson.");
            return false;
        }

        string videoUrl = ResolveVideoUrl(videoData);
        if (string.IsNullOrWhiteSpace(videoUrl))
        {
            SetStatus("No playable video source was found.");
            return false;
        }

        currentVideo = videoData;
        currentVideoUrl = videoUrl;

        if (playerPanelRoot != null)
        {
            playerPanelRoot.SetActive(true);
        }

        if (titleText != null)
        {
            titleText.text = string.IsNullOrWhiteSpace(videoData.title) ? lesson?.title ?? string.Empty : videoData.title;
        }

        autoPlayOnPrepare = false;
        SetStatus("Loading preview...");
        ConfigurePlayer(videoUrl);
        SetSeekNormalized(0f);
        UpdatePlayPauseIcon(false);
        videoPlayer.Prepare();
        return true;
    }

    public bool PlayLesson(CourseLessonData lesson)
    {
        if (!PreviewLesson(lesson))
        {
            return false;
        }

        autoPlayOnPrepare = true;

        if (videoPlayer != null && videoPlayer.isPrepared)
        {
            videoPlayer.Play();
            SetStatus("Playing");
            UpdatePlayPauseIcon(true);
        }

        return true;
    }

    public void Hide()
    {
        StopVideo();

        if (playerPanelRoot != null)
        {
            playerPanelRoot.SetActive(false);
        }
    }

    public void StopVideo()
    {
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        currentVideo = null;
        currentVideoUrl = string.Empty;
        autoPlayOnPrepare = false;
        SetSeekNormalized(0f);
        UpdatePlayPauseIcon(false);
        SetStatus(string.Empty);
    }

    private void TogglePlayPause()
    {
        if (videoPlayer == null)
        {
            return;
        }

        if (!videoPlayer.isPrepared)
        {
            if (!string.IsNullOrWhiteSpace(currentVideoUrl))
            {
                videoPlayer.Prepare();
                SetStatus("Loading video...");
            }
            return;
        }

        if (videoPlayer.isPlaying)
        {
            videoPlayer.Pause();
            SetStatus("Paused");
            UpdatePlayPauseIcon(false);
        }
        else
        {
            videoPlayer.Play();
            SetStatus("Playing");
            UpdatePlayPauseIcon(true);
        }
    }

    private void ConfigurePlayer(string videoUrl)
    {
        if (videoPlayer == null)
        {
            return;
        }

        EnsureRenderTexture();

        videoPlayer.playOnAwake = false;
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = videoUrl;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.controlledAudioTrackCount = 1;
        videoPlayer.EnableAudioTrack(0, true);

        if (audioSource != null)
        {
            videoPlayer.SetTargetAudioSource(0, audioSource);
        }

        if (videoSurface != null)
        {
            videoSurface.texture = renderTexture;
        }
    }

    private void HandlePrepareCompleted(VideoPlayer source)
    {
        if (source == null)
        {
            return;
        }

        if (autoPlayOnPrepare)
        {
            source.Play();
            SetStatus("Playing");
            UpdatePlayPauseIcon(true);
            return;
        }

        source.Pause();
        source.frame = 0;
        SetStatus("Ready to play");
        UpdatePlayPauseIcon(false);
    }

    private void HandleLoopPointReached(VideoPlayer source)
    {
        if (source == null)
        {
            return;
        }

        SetStatus("Finished");
        SetSeekNormalized(1f);
        UpdatePlayPauseIcon(false);
    }

    private void HandleErrorReceived(VideoPlayer source, string message)
    {
        string videoName = currentVideo != null && !string.IsNullOrWhiteSpace(currentVideo.title)
            ? currentVideo.title
            : "video";
        SetStatus($"Could not play {videoName}: {message}");
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message ?? string.Empty;
        }
    }

    private void InitializeControls()
    {
        if (seekSlider != null)
        {
            seekSlider.minValue = 0f;
            seekSlider.maxValue = 1f;
            SetSeekNormalized(0f);
        }

        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            float volume = audioSource != null ? audioSource.volume : 1f;
            volumeSlider.SetValueWithoutNotify(volume);
        }

        UpdatePlayPauseIcon(false);
    }

    private void OnSeekSliderChanged(float normalized)
    {
        if (suppressSeekSliderCallback)
        {
            return;
        }

        if (videoPlayer == null || !videoPlayer.isPrepared)
        {
            return;
        }

        if (videoPlayer.length <= 0d)
        {
            return;
        }

        double targetTime = normalized * videoPlayer.length;
        videoPlayer.time = Math.Max(0d, Math.Min(targetTime, videoPlayer.length));
    }

    private void OnVolumeSliderChanged(float volume)
    {
        if (audioSource == null)
        {
            return;
        }

        audioSource.volume = Mathf.Clamp01(volume);
    }

    private void UpdateSeekSlider()
    {
        if (seekSlider == null || suppressSeekSliderCallback)
        {
            return;
        }

        if (videoPlayer == null || !videoPlayer.isPrepared || videoPlayer.length <= 0d)
        {
            return;
        }

        float normalized = (float)(videoPlayer.time / videoPlayer.length);
        SetSeekNormalized(normalized);
    }

    private void SetSeekNormalized(float normalized)
    {
        if (seekSlider == null)
        {
            return;
        }

        suppressSeekSliderCallback = true;
        seekSlider.SetValueWithoutNotify(Mathf.Clamp01(normalized));
        suppressSeekSliderCallback = false;
    }

    private void UpdatePlayPauseIcon(bool isPlaying)
    {
        if (playPauseIcon == null)
        {
            return;
        }

        Sprite target = isPlaying ? pauseIcon : playIcon;
        if (target != null)
        {
            playPauseIcon.sprite = target;
        }
    }

    private void EnsureRenderTexture()
    {
        if (renderTexture != null)
        {
            return;
        }

        if (renderTextureSize.x <= 0 || renderTextureSize.y <= 0)
        {
            renderTextureSize = new Vector2Int(1920, 1080);
        }

        renderTexture = new RenderTexture(renderTextureSize.x, renderTextureSize.y, 0, RenderTextureFormat.ARGB32)
        {
            name = "LessonVideoRenderTexture"
        };
        renderTexture.Create();
        ownsRenderTexture = true;
    }

    private void ReleaseRenderTexture()
    {
        if (!ownsRenderTexture || renderTexture == null)
        {
            return;
        }

        renderTexture.Release();
        Destroy(renderTexture);
        renderTexture = null;
    }

    private bool TryGetPlayableVideoUrl(CourseLessonData lesson, out LessonVideoData videoData)
    {
        videoData = null;

        if (lesson == null || lesson.learningVideos == null || lesson.learningVideos.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < lesson.learningVideos.Count; i++)
        {
            LessonVideoData candidate = lesson.learningVideos[i];
            if (candidate == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(candidate.videoFilePath) || !string.IsNullOrWhiteSpace(candidate.videoUrl))
            {
                videoData = candidate;
                return true;
            }
        }

        return false;
    }

    private string ResolveVideoUrl(LessonVideoData videoData)
    {
        if (videoData == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(videoData.videoUrl))
        {
            return videoData.videoUrl;
        }

        if (string.IsNullOrWhiteSpace(videoData.videoFilePath))
        {
            return string.Empty;
        }

        string localPath = Path.Combine(Application.streamingAssetsPath, videoData.videoFilePath);
        return new Uri(Path.GetFullPath(localPath)).AbsoluteUri;
    }
}
