using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Globalization;

public class BeatmapSnipper : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button scissorsButton;
    [SerializeField] private GameObject snipModePanel; // Panel shown when in snip mode (has buttons)
    [SerializeField] private Button confirmSnipButton;
    [SerializeField] private Button cancelSnipButton;
    [SerializeField] private TMP_InputField startTimeInput;
    [SerializeField] private TMP_InputField endTimeInput;
    [SerializeField] private TMP_InputField snipNameInput;
    [SerializeField] private Button trimmerPlayPauseButton;
    [SerializeField] private Sprite trimmerPlayIcon;
    [SerializeField] private Sprite trimmerPauseIcon;
    
    [Header("Draggable Markers (Children of Timeline)")]
    [SerializeField] private DraggableTimelineMarker startMarker; // Must be child of timeline slider
    [SerializeField] private DraggableTimelineMarker endMarker;   // Must be child of timeline slider
    [SerializeField] private Image rangeHighlight; // Must be child of timeline slider
    [SerializeField] private RectTransform fillAreaRect; // Optional: the Fill Area of the slider for precise positioning
    [Tooltip("Markers should be direct children of the timeline slider, siblings with Background/Fill Area/Handle.")]
    
    [Header("References")]
    [SerializeField] private BeatmapPlayer beatmapPlayer;
    [SerializeField] private BeatmapLibrary beatmapLibrary;
    [SerializeField] private Slider timelineSlider; // Reference to main timeline slider
    
    [Header("Audio Trimming")]
    [SerializeField] private bool trimAudio = true; // Requires ffmpeg
    [SerializeField] private float countdownDuration = 3.0f; // Silence before snipped audio
    [SerializeField, Range(0f, 1f)] private float trimmerPreviewVolume = 0.35f;

    [Header("Waveform Preview (Optional)")]
    [SerializeField] private RawImage waveformImage;
    [SerializeField] private RectTransform waveformPlayhead;
    [SerializeField] private RectTransform waveformStartMarker;
    [SerializeField] private RectTransform waveformEndMarker;
    [SerializeField] private int waveformTextureWidth = 1024;
    [SerializeField] private int waveformTextureHeight = 160;
    [SerializeField] private Color waveformBackgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    [SerializeField] private Color waveformColor = new Color(0.25f, 0.85f, 0.75f, 1f);
    [SerializeField] private Color waveformRangeColor = new Color(0.95f, 0.55f, 0.2f, 1f);
    [SerializeField] private bool useRmsForWaveform = true;
    [SerializeField, Range(0.50f, 1.00f)] private float waveformNormalizationPercentile = 0.95f;
    [SerializeField, Range(0.10f, 1.00f)] private float waveformHeadroom = 0.85f;

    [Header("Precise Trim Controls (Optional)")]
    [SerializeField] private TMP_InputField preciseStartInput;
    [SerializeField] private TMP_InputField preciseEndInput;
    [SerializeField] private Button preciseStartDownButton;
    [SerializeField] private Button preciseStartUpButton;
    [SerializeField] private Button preciseEndDownButton;
    [SerializeField] private Button preciseEndUpButton;
    [SerializeField] private float preciseTimeStep = 0.01f;
    
    private bool isSnipModeActive = false;
    private float startTime = 0f;
    private float endTime = 0f;
    private string loadedPreviewAudioPath;
    private Texture2D waveformTexture;
    private float[] waveformAmplitudes;
    private WaveformSeekArea waveformSeekArea;
    private AudioSource trimmerPreviewAudioSource;
    private AudioClip trimmerPreviewClip;
    
    void Awake()
    {
        // Hide markers immediately on creation
        HideMarkers();
        
        // Validate that all markers and highlight share the same parent
        Transform markerParent = null;
        
        if (startMarker != null)
            markerParent = startMarker.transform.parent;
        else if (endMarker != null)
            markerParent = endMarker.transform.parent;
        else if (rangeHighlight != null)
            markerParent = rangeHighlight.transform.parent;
        
        if (markerParent != null)
        {
            if (startMarker != null && startMarker.transform.parent != markerParent)
            {
                UnityEngine.Debug.LogWarning("[BeatmapSnipper] Start marker must share the same parent as other markers!");
            }
            
            if (endMarker != null && endMarker.transform.parent != markerParent)
            {
                UnityEngine.Debug.LogWarning("[BeatmapSnipper] End marker must share the same parent as other markers!");
            }
            
            if (rangeHighlight != null && rangeHighlight.transform.parent != markerParent)
            {
                UnityEngine.Debug.LogWarning("[BeatmapSnipper] Range highlight MUST be a sibling of the markers (same parent)!");
            }
            
            // Suggest the proper parent
            UnityEngine.Debug.Log($"[BeatmapSnipper] Markers' parent: {markerParent.name}. All markers and highlight should be children of this object.");
        }
    }
    
    void Start()
    {
        // Find references if not assigned
        if (beatmapPlayer == null)
            beatmapPlayer = FindFirstObjectByType<BeatmapPlayer>();
        
        if (beatmapLibrary == null)
            beatmapLibrary = FindFirstObjectByType<BeatmapLibrary>();
        
        // Setup button listeners
        if (scissorsButton != null)
            scissorsButton.onClick.AddListener(OnScissorsClicked);

        if (trimmerPlayPauseButton != null)
            trimmerPlayPauseButton.onClick.AddListener(OnTrimmerPlayPauseClicked);
        
        if (confirmSnipButton != null)
            confirmSnipButton.onClick.AddListener(OnConfirmSnip);
        
        if (cancelSnipButton != null)
            cancelSnipButton.onClick.AddListener(OnCancelSnip);
        
        // Setup marker callbacks
        if (startMarker != null)
        {
            startMarker.OnPositionChanged += OnStartMarkerMoved;
        }
        
        if (endMarker != null)
        {
            endMarker.OnPositionChanged += OnEndMarkerMoved;
        }
        
        // Setup time input listeners
        if (startTimeInput != null)
            startTimeInput.onEndEdit.AddListener(OnStartTimeInputChanged);
        
        if (endTimeInput != null)
            endTimeInput.onEndEdit.AddListener(OnEndTimeInputChanged);

        if (preciseStartInput != null)
            preciseStartInput.onEndEdit.AddListener(OnPreciseStartInputChanged);

        if (preciseEndInput != null)
            preciseEndInput.onEndEdit.AddListener(OnPreciseEndInputChanged);

        if (preciseStartDownButton != null)
            preciseStartDownButton.onClick.AddListener(OnPreciseStartStepDown);

        if (preciseStartUpButton != null)
            preciseStartUpButton.onClick.AddListener(OnPreciseStartStepUp);

        if (preciseEndDownButton != null)
            preciseEndDownButton.onClick.AddListener(OnPreciseEndStepDown);

        if (preciseEndUpButton != null)
            preciseEndUpButton.onClick.AddListener(OnPreciseEndStepUp);

        SetupWaveformSeekArea();
        SetupTrimmerPreviewAudioSource();
        
        // Hide snip mode UI initially
        if (snipModePanel != null)
            snipModePanel.SetActive(false);

        SetConfirmSnipInteractable(true);
        
        HideMarkers();
    }

    void Update()
    {
        if (!isSnipModeActive)
            return;

        UpdateWaveformPlayhead();
        UpdateTrimmerPlayPauseIcon();
    }

    void OnDestroy()
    {
        if (waveformTexture != null)
        {
            Destroy(waveformTexture);
            waveformTexture = null;
        }

        StopAndDisposeTrimmerPreviewClip();
    }
    
    void OnScissorsClicked()
    {
        if (!beatmapPlayer.IsLoaded)
        {
            UnityEngine.Debug.LogWarning("[BeatmapSnipper] No beatmap loaded!");
            return;
        }
        
        isSnipModeActive = true;
        SetConfirmSnipInteractable(true);
        
        // Show snip mode UI
        if (snipModePanel != null)
            snipModePanel.SetActive(true);
        
        // Determine the reference rect for positioning
        RectTransform referenceRect = GetReferenceRect();
        
        if (referenceRect == null)
        {
            UnityEngine.Debug.LogError("[BeatmapSnipper] Cannot activate snip mode - no reference rect available!");
            SetConfirmSnipInteractable(true);
            return;
        }
        
        // Initialize markers to cover full duration
        startTime = 0f;
        endTime = beatmapPlayer.Duration;
        
        if (startMarker != null)
        {
            startMarker.SetReferenceRect(referenceRect);
            startMarker.SetNormalizedPosition(0f);
            startMarker.SetDuration(beatmapPlayer.Duration);
        }
        
        if (endMarker != null)
        {
            endMarker.SetReferenceRect(referenceRect);
            endMarker.SetNormalizedPosition(1f);
            endMarker.SetDuration(beatmapPlayer.Duration);
        }
        
        ShowMarkers();
        UpdateRangeHighlight();
        UpdateTimeTexts();
        UpdateWaveformRangeMarkers();
        UpdateWaveformPlayhead();
        UpdateTrimmerPlayPauseIcon();

        StartCoroutine(LoadPreviewAudioForSnipper());
        
        // Suggest a name
        if (snipNameInput != null && beatmapPlayer.CurrentBeatmap != null)
        {
            snipNameInput.text = beatmapPlayer.CurrentBeatmap.title + "_snip";
        }
        
        UnityEngine.Debug.Log("[BeatmapSnipper] Snip mode activated. Adjust markers and click Confirm.");
    }
    
    RectTransform GetReferenceRect()
    {
        // Option 1: Use explicitly assigned Fill Area
        if (fillAreaRect != null)
        {
            return fillAreaRect;
        }
        
        // Option 2: Try to find Fill Area as a child of timeline
        if (timelineSlider != null)
        {
            Transform fillArea = timelineSlider.transform.Find("Fill Area");
            if (fillArea != null)
            {
                RectTransform fillRect = fillArea.GetComponent<RectTransform>();
                if (fillRect != null)
                {
                    UnityEngine.Debug.Log($"[BeatmapSnipper] Using Fill Area rect for marker positioning (width: {fillRect.rect.width})");
                    return fillRect;
                }
            }
        }
        
        // Option 3: Fall back to timeline slider rect
        if (timelineSlider != null)
        {
            RectTransform sliderRect = timelineSlider.GetComponent<RectTransform>();
            if (sliderRect != null)
            {
                UnityEngine.Debug.LogWarning($"[BeatmapSnipper] Fill Area not found, using timeline slider rect (width: {sliderRect.rect.width})");
                return sliderRect;
            }
        }
        
        UnityEngine.Debug.LogError("[BeatmapSnipper] No reference rect available!");
        return null;
    }
    
    void OnStartMarkerMoved(float normalizedPosition)
    {
        if (!beatmapPlayer.IsLoaded) return;
        
        startTime = normalizedPosition * beatmapPlayer.Duration;
        
        // Ensure start < end
        if (startTime >= endTime)
        {
            endTime = Mathf.Min(startTime + 1f, beatmapPlayer.Duration);
            if (endMarker != null)
                endMarker.SetNormalizedPosition(endTime / beatmapPlayer.Duration);
        }
        
        UpdateRangeHighlight();
        UpdateTimeTexts();
        UpdateWaveformRangeMarkers();
    }
    
    void OnEndMarkerMoved(float normalizedPosition)
    {
        if (!beatmapPlayer.IsLoaded) return;
        
        endTime = normalizedPosition * beatmapPlayer.Duration;
        
        // Ensure end > start
        if (endTime <= startTime)
        {
            startTime = Mathf.Max(endTime - 1f, 0f);
            if (startMarker != null)
                startMarker.SetNormalizedPosition(startTime / beatmapPlayer.Duration);
        }
        
        UpdateRangeHighlight();
        UpdateTimeTexts();
        UpdateWaveformRangeMarkers();
    }
    
    void UpdateRangeHighlight()
    {
        if (rangeHighlight == null || !beatmapPlayer.IsLoaded) return;
        
        RectTransform highlightRect = rangeHighlight.GetComponent<RectTransform>();
        if (highlightRect == null) return;
        
        // Use the same reference rect as the markers
        RectTransform referenceRect = GetReferenceRect();
        if (referenceRect == null) return;
        
        float startNormalized = startTime / beatmapPlayer.Duration;
        float endNormalized = endTime / beatmapPlayer.Duration;
        
        // Clamp to ensure it doesn't exceed bounds
        startNormalized = Mathf.Clamp01(startNormalized);
        endNormalized = Mathf.Clamp01(endNormalized);
        
        // Get the actual width of the reference area
        float timelineWidth = referenceRect.rect.width;
        
        // Position highlight between markers
        float leftPos = startNormalized * timelineWidth;
        float rightPos = endNormalized * timelineWidth;
        float width = Mathf.Max(0, rightPos - leftPos); // Ensure non-negative width
        
        // Match the positioning style of the markers
        highlightRect.anchorMin = new Vector2(0, 0);
        highlightRect.anchorMax = new Vector2(0, 1);
        highlightRect.pivot = new Vector2(0, 0.5f);
        highlightRect.anchoredPosition = new Vector2(leftPos, 0);
        highlightRect.sizeDelta = new Vector2(width, 0);
    }
    
    void UpdateTimeTexts()
    {
        if (startTimeInput != null)
            startTimeInput.text = FormatTime(startTime);
        
        if (endTimeInput != null)
            endTimeInput.text = FormatTime(endTime);

        if (preciseStartInput != null)
            preciseStartInput.SetTextWithoutNotify(startTime.ToString("F3", CultureInfo.InvariantCulture));

        if (preciseEndInput != null)
            preciseEndInput.SetTextWithoutNotify(endTime.ToString("F3", CultureInfo.InvariantCulture));
    }
    
    void OnStartTimeInputChanged(string input)
    {
        float parsedTime = ParseTimeInput(input);
        if (parsedTime >= 0 && beatmapPlayer.IsLoaded)
        {
            startTime = Mathf.Clamp(parsedTime, 0f, beatmapPlayer.Duration);
            
            // Ensure start < end
            if (startTime >= endTime)
            {
                endTime = Mathf.Min(startTime + 1f, beatmapPlayer.Duration);
                if (endMarker != null)
                    endMarker.SetNormalizedPosition(endTime / beatmapPlayer.Duration);
            }
            
            // Update marker position
            if (startMarker != null)
                startMarker.SetNormalizedPosition(startTime / beatmapPlayer.Duration);
            
            UpdateRangeHighlight();
            UpdateTimeTexts();
            UpdateWaveformRangeMarkers();
        }
        else
        {
            // Invalid input, reset to current value
            UpdateTimeTexts();
        }
    }
    
    void OnEndTimeInputChanged(string input)
    {
        float parsedTime = ParseTimeInput(input);
        if (parsedTime >= 0 && beatmapPlayer.IsLoaded)
        {
            endTime = Mathf.Clamp(parsedTime, 0f, beatmapPlayer.Duration);
            
            // Ensure end > start
            if (endTime <= startTime)
            {
                startTime = Mathf.Max(endTime - 1f, 0f);
                if (startMarker != null)
                    startMarker.SetNormalizedPosition(startTime / beatmapPlayer.Duration);
            }
            
            // Update marker position
            if (endMarker != null)
                endMarker.SetNormalizedPosition(endTime / beatmapPlayer.Duration);
            
            UpdateRangeHighlight();
            UpdateTimeTexts();
            UpdateWaveformRangeMarkers();
        }
        else
        {
            // Invalid input, reset to current value
            UpdateTimeTexts();
        }
    }
    
    float ParseTimeInput(string input)
    {
        // Remove whitespace
        input = input.Trim();
        
        // Try parsing MM:SS format
        if (input.Contains(":"))
        {
            string[] parts = input.Split(':');
            if (parts.Length == 2)
            {
                bool minutesOk = int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes)
                                 || int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.CurrentCulture, out minutes);
                bool secondsOk = float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds)
                                 || float.TryParse(parts[1], NumberStyles.Float, CultureInfo.CurrentCulture, out seconds);

                if (minutesOk && secondsOk)
                {
                    return minutes * 60f + seconds;
                }
            }
        }
        // Try parsing as raw seconds
        else if (float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds)
                 || float.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out seconds))
        {
            return seconds;
        }
        
        return -1f; // Invalid
    }
    
    string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return $"{minutes:00}:{secs:00}";
    }

    void OnPreciseStartInputChanged(string input)
    {
        if (!beatmapPlayer.IsLoaded)
            return;

        if (!TryParsePreciseSeconds(input, out float parsedTime))
        {
            UpdateTimeTexts();
            return;
        }

        startTime = Mathf.Clamp(parsedTime, 0f, beatmapPlayer.Duration);
        if (startTime >= endTime)
        {
            endTime = Mathf.Min(startTime + 1f, beatmapPlayer.Duration);
            if (endMarker != null)
                endMarker.SetNormalizedPosition(endTime / beatmapPlayer.Duration);
        }

        if (startMarker != null)
            startMarker.SetNormalizedPosition(startTime / beatmapPlayer.Duration);

        UpdateRangeHighlight();
        UpdateTimeTexts();
        UpdateWaveformRangeMarkers();
    }

    void OnPreciseEndInputChanged(string input)
    {
        if (!beatmapPlayer.IsLoaded)
            return;

        if (!TryParsePreciseSeconds(input, out float parsedTime))
        {
            UpdateTimeTexts();
            return;
        }

        endTime = Mathf.Clamp(parsedTime, 0f, beatmapPlayer.Duration);
        if (endTime <= startTime)
        {
            startTime = Mathf.Max(endTime - 1f, 0f);
            if (startMarker != null)
                startMarker.SetNormalizedPosition(startTime / beatmapPlayer.Duration);
        }

        if (endMarker != null)
            endMarker.SetNormalizedPosition(endTime / beatmapPlayer.Duration);

        UpdateRangeHighlight();
        UpdateTimeTexts();
        UpdateWaveformRangeMarkers();
    }

    void OnPreciseStartStepDown()
    {
        NudgeStartTime(-Mathf.Abs(preciseTimeStep));
    }

    void OnPreciseStartStepUp()
    {
        NudgeStartTime(Mathf.Abs(preciseTimeStep));
    }

    void OnPreciseEndStepDown()
    {
        NudgeEndTime(-Mathf.Abs(preciseTimeStep));
    }

    void OnPreciseEndStepUp()
    {
        NudgeEndTime(Mathf.Abs(preciseTimeStep));
    }

    void NudgeStartTime(float delta)
    {
        if (!beatmapPlayer.IsLoaded)
            return;

        OnPreciseStartInputChanged((startTime + delta).ToString("F3", CultureInfo.InvariantCulture));
    }

    void NudgeEndTime(float delta)
    {
        if (!beatmapPlayer.IsLoaded)
            return;

        OnPreciseEndInputChanged((endTime + delta).ToString("F3", CultureInfo.InvariantCulture));
    }

    bool TryParsePreciseSeconds(string text, out float value)
    {
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;

        return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }
    
    void OnConfirmSnip()
    {
        if (confirmSnipButton != null && !confirmSnipButton.interactable)
            return;

        if (!beatmapPlayer.IsLoaded || beatmapPlayer.CurrentBeatmap == null)
        {
            UnityEngine.Debug.LogWarning("[BeatmapSnipper] No beatmap loaded!");
            return;
        }
        
        string snipName = snipNameInput != null && !string.IsNullOrEmpty(snipNameInput.text) 
            ? snipNameInput.text 
            : beatmapPlayer.CurrentBeatmap.title + "_snip";
        
        UnityEngine.Debug.Log($"[BeatmapSnipper] Creating snip '{snipName}' from {startTime:F2}s to {endTime:F2}s");
        
        // Disable confirm button while processing
        SetConfirmSnipInteractable(false);
        
        StartCoroutine(CreateSnippedBeatmapAsync(snipName, startTime, endTime));
    }
    
    void OnCancelSnip()
    {
        isSnipModeActive = false;
        SetConfirmSnipInteractable(true);
        
        if (snipModePanel != null)
            snipModePanel.SetActive(false);

        StopTrimmerPreviewPlayback();
        UpdateTrimmerPlayPauseIcon();
        
        HideMarkers();
    }

    void SetupTrimmerPreviewAudioSource()
    {
        if (trimmerPreviewAudioSource != null)
            return;

        GameObject audioObj = new GameObject("SnipperPreviewAudioSource");
        audioObj.transform.SetParent(transform);
        trimmerPreviewAudioSource = audioObj.AddComponent<AudioSource>();
        trimmerPreviewAudioSource.playOnAwake = false;
        trimmerPreviewAudioSource.loop = false;
        trimmerPreviewAudioSource.volume = trimmerPreviewVolume;
    }

    void OnTrimmerPlayPauseClicked()
    {
        if (!isSnipModeActive)
            return;

        if (trimmerPreviewAudioSource == null || trimmerPreviewAudioSource.clip == null)
        {
            UnityEngine.Debug.LogWarning("[BeatmapSnipper] Preview audio is not ready.");
            return;
        }

        if (trimmerPreviewAudioSource.isPlaying)
            trimmerPreviewAudioSource.Pause();
        else
            trimmerPreviewAudioSource.Play();

        UpdateTrimmerPlayPauseIcon();
    }

    void StopTrimmerPreviewPlayback()
    {
        if (trimmerPreviewAudioSource != null && trimmerPreviewAudioSource.isPlaying)
            trimmerPreviewAudioSource.Stop();
    }

    void UpdateTrimmerPlayPauseIcon()
    {
        if (trimmerPlayPauseButton == null)
            return;

        Image buttonImage = trimmerPlayPauseButton.GetComponent<Image>();
        if (buttonImage == null)
            return;

        bool isPlaying = trimmerPreviewAudioSource != null && trimmerPreviewAudioSource.isPlaying;
        if (isPlaying && trimmerPauseIcon != null)
            buttonImage.sprite = trimmerPauseIcon;
        else if (!isPlaying && trimmerPlayIcon != null)
            buttonImage.sprite = trimmerPlayIcon;

        TMP_Text buttonText = trimmerPlayPauseButton.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
            buttonText.text = string.Empty;
    }

    void StopAndDisposeTrimmerPreviewClip()
    {
        StopTrimmerPreviewPlayback();

        if (trimmerPreviewAudioSource != null)
            trimmerPreviewAudioSource.clip = null;

        if (trimmerPreviewClip != null)
        {
            Destroy(trimmerPreviewClip);
            trimmerPreviewClip = null;
        }

        loadedPreviewAudioPath = null;
        UpdateTrimmerPlayPauseIcon();
    }
    
    void ShowMarkers()
    {
        if (startMarker != null)
            startMarker.gameObject.SetActive(true);
        
        if (endMarker != null)
            endMarker.gameObject.SetActive(true);
        
        if (rangeHighlight != null)
            rangeHighlight.gameObject.SetActive(true);

        if (waveformStartMarker != null)
            waveformStartMarker.gameObject.SetActive(true);

        if (waveformEndMarker != null)
            waveformEndMarker.gameObject.SetActive(true);
    }
    
    void HideMarkers()
    {
        if (startMarker != null && startMarker.gameObject != null)
            startMarker.gameObject.SetActive(false);
        
        if (endMarker != null && endMarker.gameObject != null)
            endMarker.gameObject.SetActive(false);
        
        if (rangeHighlight != null && rangeHighlight.gameObject != null)
            rangeHighlight.gameObject.SetActive(false);

        if (waveformStartMarker != null && waveformStartMarker.gameObject != null)
            waveformStartMarker.gameObject.SetActive(false);

        if (waveformEndMarker != null && waveformEndMarker.gameObject != null)
            waveformEndMarker.gameObject.SetActive(false);
    }

    void SetupWaveformSeekArea()
    {
        if (waveformImage == null)
            return;

        waveformSeekArea = waveformImage.GetComponent<WaveformSeekArea>();
        if (waveformSeekArea == null)
            waveformSeekArea = waveformImage.gameObject.AddComponent<WaveformSeekArea>();

        waveformSeekArea.Initialize(waveformImage, OnWaveformSeekNormalized);
    }

    void OnWaveformSeekNormalized(float normalized)
    {
        if (!isSnipModeActive || !beatmapPlayer.IsLoaded)
            return;

        normalized = Mathf.Clamp01(normalized);
        float clickedTime = normalized * beatmapPlayer.Duration;

        bool moveStart = Mathf.Abs(clickedTime - startTime) <= Mathf.Abs(clickedTime - endTime);
        if (moveStart)
            OnPreciseStartInputChanged(clickedTime.ToString("F3", CultureInfo.InvariantCulture));
        else
            OnPreciseEndInputChanged(clickedTime.ToString("F3", CultureInfo.InvariantCulture));

        if (trimmerPreviewAudioSource != null && trimmerPreviewAudioSource.clip != null)
        {
            float maxTime = Mathf.Max(0f, trimmerPreviewAudioSource.clip.length - 0.01f);
            trimmerPreviewAudioSource.time = Mathf.Clamp(clickedTime, 0f, maxTime);
        }
        UpdateWaveformPlayhead();
    }

    IEnumerator LoadPreviewAudioForSnipper()
    {
        if (waveformImage == null || beatmapPlayer == null || beatmapPlayer.CurrentBeatmap == null)
            yield break;

        string audioPath = beatmapPlayer.CurrentBeatmap.audioFilePath;
        if (string.IsNullOrEmpty(audioPath) || !File.Exists(audioPath))
        {
            UnityEngine.Debug.LogWarning("[BeatmapSnipper] Cannot build waveform: audio file not found.");
            yield break;
        }

        if (trimmerPreviewAudioSource != null)
            trimmerPreviewAudioSource.volume = trimmerPreviewVolume;

        if (trimmerPreviewClip != null && loadedPreviewAudioPath == audioPath)
        {
            BuildWaveformTexture(trimmerPreviewClip);
            if (trimmerPreviewAudioSource != null)
            {
                trimmerPreviewAudioSource.clip = trimmerPreviewClip;
                trimmerPreviewAudioSource.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, trimmerPreviewClip.length - 0.01f));
            }
            UpdateTrimmerPlayPauseIcon();
            yield break;
        }

        StopAndDisposeTrimmerPreviewClip();

        string uri = "file:///" + audioPath.Replace("\\", "/");
        using (var www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.UNKNOWN))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogWarning($"[BeatmapSnipper] Failed to load waveform audio: {www.error}");
                yield break;
            }

            AudioClip clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
            if (clip == null)
            {
                UnityEngine.Debug.LogWarning("[BeatmapSnipper] Failed to decode audio clip for waveform.");
                yield break;
            }

            trimmerPreviewClip = clip;
            loadedPreviewAudioPath = audioPath;

            if (trimmerPreviewAudioSource != null)
            {
                trimmerPreviewAudioSource.clip = trimmerPreviewClip;
                trimmerPreviewAudioSource.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, trimmerPreviewClip.length - 0.01f));
            }
            UpdateTrimmerPlayPauseIcon();

            BuildWaveformTexture(clip);
            UpdateWaveformRangeMarkers();
            UpdateWaveformPlayhead();
        }
    }

    void BuildWaveformTexture(AudioClip clip)
    {
        if (clip == null || waveformImage == null)
            return;

        int width = Mathf.Max(128, waveformTextureWidth);
        int height = Mathf.Max(32, waveformTextureHeight);

        if (waveformTexture != null)
        {
            Destroy(waveformTexture);
            waveformTexture = null;
        }

        waveformTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[width * height];
        Color32 background = waveformBackgroundColor;
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = background;

        int channels = Mathf.Max(1, clip.channels);
        int frameCount = Mathf.Max(1, clip.samples);
        int framesPerPixel = Mathf.Max(1, frameCount / width);

        float[] audioData = new float[frameCount * channels];
        clip.GetData(audioData, 0);

        waveformAmplitudes = new float[width];
        for (int x = 0; x < width; x++)
        {
            int frameStart = x * framesPerPixel;
            int frameEnd = Mathf.Min(frameCount, frameStart + framesPerPixel);

            float peak = 0f;
            float sumSquares = 0f;
            int sampleCount = 0;

            for (int frame = frameStart; frame < frameEnd; frame++)
            {
                int sampleIndex = frame * channels;
                for (int c = 0; c < channels; c++)
                {
                    float amplitude = Mathf.Abs(audioData[sampleIndex + c]);
                    if (amplitude > peak)
                        peak = amplitude;

                    sumSquares += amplitude * amplitude;
                    sampleCount++;
                }
            }

            float rms = sampleCount > 0 ? Mathf.Sqrt(sumSquares / sampleCount) : 0f;
            waveformAmplitudes[x] = useRmsForWaveform ? rms : peak;
        }

        waveformTexture.SetPixels32(pixels);
        waveformImage.texture = waveformTexture;
        RedrawWaveformTextureWithRange();
    }

    void RedrawWaveformTextureWithRange()
    {
        if (waveformTexture == null || waveformAmplitudes == null || waveformImage == null)
            return;

        int width = waveformTexture.width;
        int height = waveformTexture.height;
        if (waveformAmplitudes.Length != width)
            return;

        Color32[] pixels = new Color32[width * height];
        Color32 background = waveformBackgroundColor;
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = background;

        float normalizationReference = GetAmplitudePercentile(waveformAmplitudes, waveformNormalizationPercentile);
        normalizationReference = Mathf.Max(0.01f, normalizationReference);

        int halfHeight = height / 2;
        float duration = beatmapPlayer != null && beatmapPlayer.IsLoaded ? Mathf.Max(0.01f, beatmapPlayer.Duration) : 1f;
        int startX = Mathf.Clamp(Mathf.RoundToInt((startTime / duration) * (width - 1)), 0, width - 1);
        int endX = Mathf.Clamp(Mathf.RoundToInt((endTime / duration) * (width - 1)), 0, width - 1);
        if (startX > endX)
        {
            int tmp = startX;
            startX = endX;
            endX = tmp;
        }

        for (int x = 0; x < width; x++)
        {
            bool inRange = x >= startX && x <= endX;
            Color32 wave = inRange ? waveformRangeColor : waveformColor;
            float normalized = Mathf.Clamp01(waveformAmplitudes[x] / normalizationReference) * waveformHeadroom;
            int ampHeight = Mathf.Clamp(Mathf.RoundToInt(normalized * (halfHeight - 1)), 1, halfHeight - 1);
            int yMin = halfHeight - ampHeight;
            int yMax = halfHeight + ampHeight;

            for (int y = yMin; y <= yMax; y++)
                pixels[y * width + x] = wave;
        }

        waveformTexture.SetPixels32(pixels);
        waveformTexture.Apply(false, false);
    }

    float GetAmplitudePercentile(float[] values, float percentile)
    {
        if (values == null || values.Length == 0)
            return 1f;

        float[] sorted = new float[values.Length];
        Array.Copy(values, sorted, values.Length);
        Array.Sort(sorted);

        float clampedPercentile = Mathf.Clamp01(percentile);
        int index = Mathf.Clamp(Mathf.FloorToInt((sorted.Length - 1) * clampedPercentile), 0, sorted.Length - 1);
        return sorted[index];
    }

    void UpdateWaveformPlayhead()
    {
        if (waveformPlayhead == null || waveformImage == null)
            return;

        Rect rect = waveformImage.rectTransform.rect;
        if (rect.width <= 0f)
            return;

        float duration = beatmapPlayer != null && beatmapPlayer.IsLoaded ? beatmapPlayer.Duration : 0f;
        if (duration <= 0f)
            return;

        float currentTime = 0f;
        if (trimmerPreviewAudioSource != null && trimmerPreviewAudioSource.clip != null)
            currentTime = trimmerPreviewAudioSource.time;
        else if (beatmapPlayer != null && beatmapPlayer.IsLoaded)
            currentTime = beatmapPlayer.CurrentTime;

        float normalized = Mathf.Clamp01(currentTime / duration);
        float x = Mathf.Lerp(rect.xMin, rect.xMax, normalized);

        Vector2 anchored = waveformPlayhead.anchoredPosition;
        anchored.x = x;
        waveformPlayhead.anchoredPosition = anchored;
    }

    void UpdateWaveformRangeMarkers()
    {
        if (!beatmapPlayer.IsLoaded || waveformImage == null)
            return;

        float duration = beatmapPlayer.Duration;
        if (duration <= 0f)
            return;

        Rect rect = waveformImage.rectTransform.rect;
        if (rect.width <= 0f)
            return;

        if (waveformStartMarker != null)
        {
            float normalizedStart = Mathf.Clamp01(startTime / duration);
            float xStart = Mathf.Lerp(rect.xMin, rect.xMax, normalizedStart);
            Vector2 anchoredStart = waveformStartMarker.anchoredPosition;
            anchoredStart.x = xStart;
            waveformStartMarker.anchoredPosition = anchoredStart;
        }

        if (waveformEndMarker != null)
        {
            float normalizedEnd = Mathf.Clamp01(endTime / duration);
            float xEnd = Mathf.Lerp(rect.xMin, rect.xMax, normalizedEnd);
            Vector2 anchoredEnd = waveformEndMarker.anchoredPosition;
            anchoredEnd.x = xEnd;
            waveformEndMarker.anchoredPosition = anchoredEnd;
        }

        RedrawWaveformTextureWithRange();
    }
    
    IEnumerator CreateSnippedBeatmapAsync(string snipName, float startTime, float endTime)
    {
        BeatmapData originalBeatmap = beatmapPlayer.CurrentBeatmap;
        
        if (originalBeatmap == null || originalBeatmap.beatmap == null)
        {
            UnityEngine.Debug.LogError("[BeatmapSnipper] Original beatmap is null!");
            SetConfirmSnipInteractable(true);
            yield break;
        }
        
        bool originalHasCountdown = originalBeatmap?.metadata?.audio_includes_countdown ?? false;

        // Rebase note timing against the actual start of the audio that will be written.
        // Imported beatmaps use CurrentTime = audio time + countdownDuration, so if the snip
        // starts inside that ready-timer window we cannot trim from a negative audio position.
        float timeShift = originalHasCountdown
            ? startTime - countdownDuration
            : Mathf.Max(0f, startTime - countdownDuration);
        float snippedLengthSeconds = originalHasCountdown
            ? (endTime - startTime) + countdownDuration
            : endTime - Mathf.Max(0f, startTime - countdownDuration);

        // Filter notes within the time range and adjust their timing
        List<BeatmapNote> snippedNotes = new List<BeatmapNote>();
        
        foreach (var note in originalBeatmap.beatmap)
        {
            if (note.time >= startTime && note.time <= endTime)
            {
                BeatmapNote newNote = new BeatmapNote
                {
                    lane = note.lane,
                    // Align note timing with the actual start of the new audio clip.
                    time = note.time - timeShift,
                    spawnTime = note.spawnTime - timeShift,
                    velocity = note.velocity
                };
                
                // Ensure spawn time is not negative
                if (newNote.spawnTime < 0)
                    newNote.spawnTime = 0;
                
                snippedNotes.Add(newNote);
            }
        }
        
        if (snippedNotes.Count == 0)
        {
            UnityEngine.Debug.LogWarning("[BeatmapSnipper] No notes found in selected range!");
            SetConfirmSnipInteractable(true);
            yield break;
        }
        
        // Create new beatmap data
        BeatmapData snippedBeatmap = new BeatmapData
        {
            title = snipName,
            midiFilePath = originalBeatmap.midiFilePath,
            audioFilePath = originalBeatmap.audioFilePath,
            beatmap = snippedNotes,
            metadata = new BeatmapData.BeatmapMetadata
            {
                title = snipName,
                source = originalBeatmap.metadata?.source ?? "Snipped from " + originalBeatmap.title,
                bpm_min = originalBeatmap.metadata?.bpm_min ?? 120f,
                bpm_max = originalBeatmap.metadata?.bpm_max ?? 120f,
                bpm_avg = originalBeatmap.metadata?.bpm_avg ?? 120f,
                time_signatures = originalBeatmap.metadata?.time_signatures ?? new List<string>(),
                length_seconds = snippedLengthSeconds,
                events_count = snippedNotes.Count,
                lanes_used = snippedNotes.Select(n => n.lane).Distinct().OrderBy(l => l).ToList(),
                events_per_second = snippedNotes.Count / (endTime - startTime),
                drum_start_offset = 0f, // Audio has countdown built-in, play from the start (don't skip)
                audio_includes_countdown = true // Snipped audio has countdown silence built in
            }
        };
        
        // Save the snipped beatmap (async)
        yield return StartCoroutine(SaveSnippedBeatmapAsync(snippedBeatmap, originalBeatmap, startTime, endTime));
        
        // Re-enable confirm button and close snip mode
        SetConfirmSnipInteractable(true);
        
        OnCancelSnip();
    }

    void SetConfirmSnipInteractable(bool interactable)
    {
        if (confirmSnipButton != null)
            confirmSnipButton.interactable = interactable;
    }
    
    IEnumerator SaveSnippedBeatmapAsync(BeatmapData beatmap, BeatmapData originalBeatmap, float beatmapStartTime, float beatmapEndTime)
    {
        string originalBeatmapFolder = "";
        string beatmapFolder = "";
        string newAudioPath = "";
        string newMidiPath = "";
        
        // Calculate audio file positions from CurrentTime (timeline positions)
        bool originalHasCountdown = originalBeatmap?.metadata?.audio_includes_countdown ?? false;
        float audioStartTime, audioEndTime;
        
        if (originalHasCountdown)
        {
            // Original is snipped: CurrentTime = audio time directly
            audioStartTime = beatmapStartTime;
            audioEndTime = beatmapEndTime;
        }
        else
        {
            // Original is imported: CurrentTime = audioSource.time + countdownDuration.
            // Clamp start to 0 because audio cannot begin before the file starts.
            audioStartTime = Mathf.Max(0f, beatmapStartTime - countdownDuration);
            audioEndTime = Mathf.Max(audioStartTime, beatmapEndTime - countdownDuration);
            
            UnityEngine.Debug.Log($"[BeatmapSnipper] Converting CurrentTime [{beatmapStartTime:F2}-{beatmapEndTime:F2}] to audio positions [{audioStartTime:F2}-{audioEndTime:F2}]");
        }
        
        try
        {
            originalBeatmapFolder = Path.GetDirectoryName(originalBeatmap.jsonFilePath);
            if (string.IsNullOrEmpty(originalBeatmapFolder) || !Directory.Exists(originalBeatmapFolder))
            {
                UnityEngine.Debug.LogError("[BeatmapSnipper] Original beatmap folder not found. Cannot create nested snip.");
                yield break;
            }

            beatmapFolder = Path.Combine(originalBeatmapFolder, beatmap.title);
            
            // If folder exists, add timestamp
            if (Directory.Exists(beatmapFolder))
            {
                beatmap.title = beatmap.title + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                beatmapFolder = Path.Combine(originalBeatmapFolder, beatmap.title);
            }
            
            Directory.CreateDirectory(beatmapFolder);
            
            // Trim and copy audio file
            string audioFileName = Path.GetFileName(beatmap.audioFilePath);
            string midiFileName = Path.GetFileName(beatmap.midiFilePath);
            
            newAudioPath = Path.Combine(beatmapFolder, audioFileName);
            newMidiPath = Path.Combine(beatmapFolder, midiFileName);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[BeatmapSnipper] Error preparing beatmap folder: {e.Message}");
            yield break;
        }
        
        // Try to trim audio with countdown (outside try-catch), fallback to full copy
        bool audioTrimmed = false;
        if (trimAudio)
        {
            yield return StartCoroutine(TrimAudioWithCountdownAsync(
                beatmap.audioFilePath,
                newAudioPath,
                audioStartTime,
                audioEndTime,
                countdownDuration,
                (success) => audioTrimmed = success
            ));
            
            if (!audioTrimmed)
            {
                UnityEngine.Debug.LogWarning("[BeatmapSnipper] Audio trimming failed, copying full audio file. Install ffmpeg for audio trimming.");
                try
                {
                    File.Copy(beatmap.audioFilePath, newAudioPath, true);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[BeatmapSnipper] Error copying audio: {e.Message}");
                    yield break;
                }
            }
        }
        else
        {
            try
            {
                File.Copy(beatmap.audioFilePath, newAudioPath, true);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[BeatmapSnipper] Error copying audio: {e.Message}");
                yield break;
            }
        }
        
        try
        {
            File.Copy(beatmap.midiFilePath, newMidiPath, true);
            
            // Update paths
            beatmap.audioFilePath = newAudioPath;
            beatmap.midiFilePath = newMidiPath;
            
            // Save JSON
            string jsonPath = Path.Combine(beatmapFolder, "beatmap.json");
            
            var wrapper = new BeatmapWrapper
            {
                metadata = beatmap.metadata,
                beatmap = beatmap.beatmap
            };
            
            string json = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(jsonPath, json);
            
            beatmap.jsonFilePath = jsonPath;
            
            UnityEngine.Debug.Log($"[BeatmapSnipper] Saved snipped beatmap: {beatmap.title}");
            UnityEngine.Debug.Log($"[BeatmapSnipper] Contains {beatmap.beatmap.Count} notes");
            
            // Add to library
            if (beatmapLibrary != null)
            {
                beatmapLibrary.LoadAllBeatmaps(); // Reload to include the new snip
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[BeatmapSnipper] Error saving snipped beatmap: {e.Message}");
        }
    }
    
    IEnumerator TrimAudioWithCountdownAsync(string inputPath, string outputPath, float startTime, float endTime, float countdown, Action<bool> callback)
    {
        Process process = null;
        ProcessStartInfo processInfo = null;
        string duration = "";
        
        try
        {
            // Use ffmpeg to:
            // 1. Extract audio segment from startTime to endTime
            // 2. Add countdown seconds of silence at the beginning
            
            // ffmpeg command:
            // ffmpeg -i input.mp3 -af "aevalsrc=0:d=3[silence];[0:a]atrim=start=13:end=50[clip];[silence][clip]concat=n=2:v=0:a=1" output.mp3
            
            duration = (endTime - startTime).ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            string startTimeStr = startTime.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            string countdownStr = countdown.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            
            // Complex filter: generate silence + trim audio + concatenate.
            // This must use filter_complex because the graph creates and combines multiple audio streams.
            string audioFilter = $"aevalsrc=0:d={countdownStr}[silence];[0:a]atrim=start={startTimeStr}:duration={duration},asetpts=PTS-STARTPTS[clip];[silence][clip]concat=n=2:v=0:a=1[out]";
            
            processInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i \"{inputPath}\" -filter_complex \"{audioFilter}\" -map \"[out]\" -y \"{outputPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            UnityEngine.Debug.Log($"[BeatmapSnipper] Trimming audio with ffmpeg: {processInfo.Arguments}");
            
            process = Process.Start(processInfo);
            
            if (process == null)
            {
                UnityEngine.Debug.LogError("[BeatmapSnipper] Failed to start ffmpeg process");
                callback?.Invoke(false);
                yield break;
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[BeatmapSnipper] Audio trimming error: {e.Message}");
            callback?.Invoke(false);
            yield break;
        }
        
        // Wait for process to exit without blocking (outside try-catch)
        while (!process.HasExited)
        {
            yield return null; // Wait one frame
        }
        
        try
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            
            if (process.ExitCode == 0)
            {
                UnityEngine.Debug.Log($"[BeatmapSnipper] Audio trimmed: {countdown}s countdown + audio segment from {startTime:F3}s to {endTime:F3}s ({duration}s)");
                callback?.Invoke(true);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[BeatmapSnipper] ffmpeg failed (exit code {process.ExitCode})");
                if (!string.IsNullOrEmpty(error))
                    UnityEngine.Debug.LogWarning($"[BeatmapSnipper] ffmpeg error: {error}");
                callback?.Invoke(false);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[BeatmapSnipper] Audio trimming error: {e.Message}");
            callback?.Invoke(false);
        }
    }
    
    [Serializable]
    private class BeatmapWrapper
    {
        public BeatmapData.BeatmapMetadata metadata;
        public List<BeatmapNote> beatmap;
    }
}
