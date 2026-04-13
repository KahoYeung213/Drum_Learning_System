using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SFB;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;

public class BeatmapImporter : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button importButton;
    [SerializeField] private TMP_InputField drumOffsetInput; // Optional: Input field for drum start offset (legacy)

    [Header("Import Panel UI")]
    [SerializeField] private GameObject importPanel; // Panel with MIDI/Audio upload boxes
    [SerializeField] private Button importConfirmButton; // Confirm button inside import panel
    [SerializeField] private Button importCancelButton; // Cancel button inside import panel
    [SerializeField] private TMP_Text importLinkText; // Optional TMP link text in import panel
    [SerializeField] private string fallbackImportLinkUrl = "https://www.onlinesequencer.net/";
    
    [Header("Drag and Drop Areas (Optional - Alternative to File Browser)")]
    [SerializeField] private DragDropFileArea midiDropArea; // Drag-drop area for MIDI files
    [SerializeField] private DragDropFileArea audioDropArea; // Drag-drop area for audio files
    [SerializeField] private TMP_Text importStatusText; // Shows which files are ready

    [Header("Transition Loading UI")]
    [SerializeField] private GameObject transitionLoadingPanel; // Fullscreen/blocking panel shown between UI states
    [SerializeField] private TMP_Text transitionLoadingText; // Optional status text
    [SerializeField] private RectTransform transitionLoadingSpinner; // Optional spinner icon to rotate
    [SerializeField] private float transitionSpinnerSpeed = 220f;
    
    [Header("Tap-to-Sync Preview UI")]
    [SerializeField] private GameObject previewPanel; // Panel shown during audio preview
    [SerializeField] private Button playPauseButton;
    [SerializeField] private Button markDrumStartButton;
    [SerializeField] private Button confirmOffsetButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text currentTimeText;
    [SerializeField] private TMP_Text markedTimeText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private Slider audioSeekSlider; // Optional: scrub through audio
    [SerializeField] private TMP_InputField preciseTimeInput; // Optional: precise marked time edit
    [SerializeField] private Button preciseTimeDownButton; // Optional: nudge marked time backward
    [SerializeField] private Button preciseTimeUpButton; // Optional: nudge marked time forward
    [SerializeField] private float preciseTimeStep = 0.01f;

    [Header("Play/Pause Icons")]
    [SerializeField] private Sprite playIcon;
    [SerializeField] private Sprite pauseIcon;

    [Header("Waveform Preview (Optional)")]
    [SerializeField] private RawImage waveformImage; // Waveform display in drum mark UI
    [SerializeField] private RectTransform waveformPlayhead; // Vertical line that tracks playback time
    [SerializeField] private RectTransform waveformMarkedTimeMarker; // Marker for marked drum start position
    [SerializeField] private int waveformTextureWidth = 1024;
    [SerializeField] private int waveformTextureHeight = 160;
    [SerializeField] private Color waveformBackgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    [SerializeField] private Color waveformColor = new Color(0.25f, 0.85f, 0.75f, 1f);
    [SerializeField] private bool useRmsForWaveform = true;
    [SerializeField, Range(0.50f, 1.00f)] private float waveformNormalizationPercentile = 0.95f;
    [SerializeField, Range(0.10f, 1.00f)] private float waveformHeadroom = 0.85f;

    [Header("Preview Audio")]
    [SerializeField, Range(0f, 1f)] private float previewAudioVolume = 0.35f;
    
    [Header("Beatmap Settings")]
    [SerializeField] private float spawnLeadTime = 2.0f;
    [SerializeField] private float audioOffset = 3.0f; // Countdown time before first note
    [SerializeField] private float defaultDrumStartOffset = 0.0f; // Default offset in seconds
    
    private string pythonScriptPath;
    private string beatmapsDirectory;
    private string tempMidiPath;
    private string tempAudioPath;
    
    // Drag-drop selected files
    private string selectedMidiPath = null;
    private string selectedAudioPath = null;
    
    // Preview state
    private AudioSource previewAudioSource;
    private float markedDrumStartTime = -1f;
    private bool isPreviewMode = false;
    private bool isTransitionLoading = false;
    private Texture2D waveformTexture;
    private WaveformSeekArea waveformSeekArea;
    
    public event Action<BeatmapData> OnBeatmapImported;
    
    void Start()
    {
        // Set up paths
        pythonScriptPath = Path.Combine(Application.dataPath, "Scripts", "MidiParser", "music_xml_to_beatmap.py");
        beatmapsDirectory = Path.Combine(Application.persistentDataPath, "Beatmaps");
        
        // Create directory if it doesn't exist
        if (!Directory.Exists(beatmapsDirectory))
        {
            Directory.CreateDirectory(beatmapsDirectory);
        }
        
        if (importButton != null)
        {
            importButton.onClick.AddListener(OnImportButtonClicked);
            importButton.interactable = true;
        }

        if (importConfirmButton != null)
            importConfirmButton.onClick.AddListener(OnImportConfirmClicked);

        if (importCancelButton != null)
            importCancelButton.onClick.AddListener(OnImportCancelClicked);

        if (importPanel != null)
        {
            importPanel.SetActive(false);
        }
        
        // Set up preview UI
        if (previewPanel != null)
        {
            previewPanel.SetActive(false);
        }

        SetTransitionLoading(false);

        SetupImportLinkHandling();
        
        // Create preview audio source
        GameObject audioObj = new GameObject("PreviewAudioSource");
        audioObj.transform.SetParent(transform);
        previewAudioSource = audioObj.AddComponent<AudioSource>();
        previewAudioSource.playOnAwake = false;
        previewAudioSource.volume = previewAudioVolume;
        
        // Connect drag-drop areas
        if (midiDropArea != null)
        {
            midiDropArea.SetFileType(FileType.MIDI);
            midiDropArea.OnFileSelected += OnMidiFileSelected;
        }
        
        if (audioDropArea != null)
        {
            audioDropArea.SetFileType(FileType.Audio);
            audioDropArea.OnFileSelected += OnAudioFileSelected;
        }
        
        UpdateImportStatus();
        
        // Connect preview UI buttons
        if (playPauseButton != null)
            playPauseButton.onClick.AddListener(OnPlayPauseClicked);
        if (markDrumStartButton != null)
            markDrumStartButton.onClick.AddListener(OnMarkDrumStart);
        if (confirmOffsetButton != null)
            confirmOffsetButton.onClick.AddListener(OnConfirmOffset);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelPreview);
        if (audioSeekSlider != null)
            audioSeekSlider.onValueChanged.AddListener(OnSeekSliderChanged);
        if (preciseTimeInput != null)
            preciseTimeInput.onEndEdit.AddListener(OnPreciseTimeInputEndEdit);
        if (preciseTimeDownButton != null)
            preciseTimeDownButton.onClick.AddListener(OnPreciseTimeStepDown);
        if (preciseTimeUpButton != null)
            preciseTimeUpButton.onClick.AddListener(OnPreciseTimeStepUp);

        SetupWaveformSeekArea();
    }
    
    void OnImportButtonClicked()
    {
        OpenImportPanel();
    }

    void OpenImportPanel()
    {
        if (importPanel == null)
        {
            const string warningMessage = "[BeatmapImporter] Import panel is not assigned.";
            UnityEngine.Debug.LogWarning(warningMessage);
            AppErrorPopup.Show(warningMessage);
            return;
        }

        importPanel.SetActive(true);
        UpdateImportStatus();
    }

    void OnImportConfirmClicked()
    {
        if (string.IsNullOrEmpty(selectedMidiPath) || string.IsNullOrEmpty(selectedAudioPath))
        {
            const string warningMessage = "[BeatmapImporter] Please select both MIDI and audio files before confirming.";
            UnityEngine.Debug.LogWarning(warningMessage);
            AppErrorPopup.Show(warningMessage);
            UpdateImportStatus();
            return;
        }

        UnityEngine.Debug.Log("[BeatmapImporter] Starting import with panel-selected files");
        UnityEngine.Debug.Log($"[BeatmapImporter] MIDI: {selectedMidiPath}");
        UnityEngine.Debug.Log($"[BeatmapImporter] Audio: {selectedAudioPath}");

        if (importPanel != null)
            importPanel.SetActive(false);

        SetTransitionLoading(true, "Preparing audio preview...");

        StartCoroutine(ProcessImportWithDragDropFiles(selectedMidiPath, selectedAudioPath));
    }

    void OnImportCancelClicked()
    {
        if (importPanel != null)
            importPanel.SetActive(false);

        SetTransitionLoading(false);

        ResetDragDropSelection();
        UnityEngine.Debug.Log("[BeatmapImporter] Import panel closed.");
    }

    void SetupImportLinkHandling()
    {
        if (importLinkText == null) return;

        var linkHandler = importLinkText.GetComponent<ImportPanelLinkHandler>();
        if (linkHandler == null)
        {
            linkHandler = importLinkText.gameObject.AddComponent<ImportPanelLinkHandler>();
        }

        linkHandler.Initialize(importLinkText, fallbackImportLinkUrl);
    }
    
    void SelectAudioFile()
    {
        var audioExtensions = new[] {
            new ExtensionFilter("Audio Files", "mp3", "wav", "ogg"),
            new ExtensionFilter("All Files", "*")
        };
        
        StandaloneFileBrowser.OpenFilePanelAsync(
            "Select Audio File", 
            "", 
            audioExtensions, 
            false,
            (string[] paths) => {
                if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
                {
                    tempAudioPath = paths[0];
                    StartCoroutine(LoadAudioForPreview());
                }
                else
                {
                    const string warningMessage = "No audio file selected. Import cancelled.";
                    UnityEngine.Debug.LogWarning(warningMessage);
                    AppErrorPopup.Show(warningMessage);
                    tempMidiPath = null;
                }
            }
        );
    }
    
    IEnumerator LoadAudioForPreview()
    {
        UnityEngine.Debug.Log($"Loading audio for preview: {tempAudioPath}");
        
        // Load audio clip
        string uri = "file:///" + tempAudioPath.Replace("\\", "/");
        using (var www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.UNKNOWN))
        {
            yield return www.SendWebRequest();
            
            if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError($"Failed to load audio: {www.error}");
                tempMidiPath = null;
                tempAudioPath = null;
                yield break;
            }
            
            AudioClip clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
            previewAudioSource.clip = clip;
            BuildWaveformTexture(clip);
            
            // Enter preview mode
            EnterPreviewMode();
        }
    }
    
    void EnterPreviewMode()
    {
        isPreviewMode = true;
        markedDrumStartTime = -1f;
        SetTransitionLoading(false);

        if (previewAudioSource != null)
        {
            previewAudioSource.volume = previewAudioVolume;
        }
        
        if (previewPanel != null)
        {
            previewPanel.SetActive(true);
        }

        RefreshPreciseTimeInput();
        UpdateMarkedTimeMarker();
        UpdateWaveformPlayhead();
        
        UpdatePreviewUI();
        
        UnityEngine.Debug.Log("Preview mode active. Play audio and tap 'Mark Drum Start' when you hear the first drum hit.");
    }
    
    void ExitPreviewMode()
    {
        isPreviewMode = false;
        
        if (previewAudioSource != null && previewAudioSource.isPlaying)
        {
            previewAudioSource.Stop();
        }
        
        if (previewPanel != null)
        {
            previewPanel.SetActive(false);
        }
    }
    
    void Update()
    {
        if (isTransitionLoading && transitionLoadingSpinner != null)
        {
            transitionLoadingSpinner.Rotate(0f, 0f, -transitionSpinnerSpeed * Time.unscaledDeltaTime);
        }

        if (isPreviewMode)
        {
            UpdatePreviewUI();
        }
    }
    
    void OnDestroy()
    {
        SetTransitionLoading(false);

        // Clean up preview audio
        if (previewAudioSource != null && previewAudioSource.isPlaying)
        {
            previewAudioSource.Stop();
        }

        if (waveformTexture != null)
        {
            Destroy(waveformTexture);
            waveformTexture = null;
        }
    }
    
    void UpdatePreviewUI()
    {
        if (!isPreviewMode) return;
        
        // Update time display
        if (currentTimeText != null && previewAudioSource != null && previewAudioSource.clip != null)
        {
            float current = previewAudioSource.time;
            float duration = previewAudioSource.clip.length;
            currentTimeText.text = $"Time: {current:F2}s / {duration:F2}s";
        }
        
        // Update marked time display
        if (markedTimeText != null)
        {
            if (markedDrumStartTime >= 0f)
            {
                markedTimeText.text = $"Drum Start: {markedDrumStartTime:F2}s";
            }
            else
            {
                markedTimeText.text = "Drum Start: Not marked";
            }
        }
        
        // Update instruction text
        if (instructionText != null)
        {
            if (markedDrumStartTime < 0f)
            {
                instructionText.text = "Play the audio and press 'Mark Drum Start' when you hear the first drum hit.";
            }
            else
            {
                instructionText.text = $"Drum start marked at {markedDrumStartTime:F2}s. Confirm to continue or mark again.";
            }
        }
        
        // Update play/pause button icon
        if (playPauseButton != null)
        {
            bool playing = previewAudioSource != null && previewAudioSource.isPlaying;
            if (playIcon != null && pauseIcon != null)
            {
                var btnImage = playPauseButton.GetComponent<Image>();
                if (btnImage != null)
                    btnImage.sprite = playing ? pauseIcon : playIcon;
            }
            var btnText = playPauseButton.GetComponentInChildren<TMP_Text>();
            if (btnText != null)
                btnText.text = string.Empty;
        }
        
        // Update confirm button interactable
        if (confirmOffsetButton != null)
        {
            confirmOffsetButton.interactable = (markedDrumStartTime >= 0f);
        }
        
        // Update seek slider
        if (audioSeekSlider != null && previewAudioSource != null && previewAudioSource.clip != null)
        {
            if (!audioSeekSlider.IsActive() || !Input.GetMouseButton(0)) // Don't update while dragging
            {
                audioSeekSlider.SetValueWithoutNotify(previewAudioSource.time / previewAudioSource.clip.length);
            }
        }

        UpdateWaveformPlayhead();
    }
    
    void OnPlayPauseClicked()
    {
        if (previewAudioSource == null || previewAudioSource.clip == null) return;
        
        if (previewAudioSource.isPlaying)
        {
            previewAudioSource.Pause();
        }
        else
        {
            previewAudioSource.Play();
        }
    }
    
    void OnMarkDrumStart()
    {
        if (previewAudioSource == null || previewAudioSource.clip == null) return;

        SetMarkedDrumStartTime(previewAudioSource.time, false);
        UnityEngine.Debug.Log($"Drum start marked at {markedDrumStartTime:F2}s");
    }
    
    void OnSeekSliderChanged(float value)
    {
        if (previewAudioSource == null || previewAudioSource.clip == null) return;
        
        previewAudioSource.time = value * previewAudioSource.clip.length;
        UpdateWaveformPlayhead();
    }

    void OnPreciseTimeInputEndEdit(string value)
    {
        if (!isPreviewMode || previewAudioSource == null || previewAudioSource.clip == null)
        {
            return;
        }

        if (TryParseFloat(value, out float parsed))
        {
            SetMarkedDrumStartTime(parsed, true);
        }
        else
        {
            RefreshPreciseTimeInput();
        }
    }

    void OnPreciseTimeStepDown()
    {
        NudgeMarkedTime(-Mathf.Abs(preciseTimeStep));
    }

    void OnPreciseTimeStepUp()
    {
        NudgeMarkedTime(Mathf.Abs(preciseTimeStep));
    }

    void NudgeMarkedTime(float delta)
    {
        if (!isPreviewMode || previewAudioSource == null || previewAudioSource.clip == null)
        {
            return;
        }

        float baseTime = markedDrumStartTime >= 0f ? markedDrumStartTime : previewAudioSource.time;
        SetMarkedDrumStartTime(baseTime + delta, true);
    }

    bool TryParseFloat(string text, out float value)
    {
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    void SetMarkedDrumStartTime(float value, bool seekAudio)
    {
        if (previewAudioSource == null || previewAudioSource.clip == null)
        {
            return;
        }

        float duration = previewAudioSource.clip.length;
        float clamped = Mathf.Clamp(value, 0f, duration);
        markedDrumStartTime = clamped;

        if (seekAudio)
        {
            previewAudioSource.time = clamped;
            if (audioSeekSlider != null && duration > 0f)
            {
                audioSeekSlider.SetValueWithoutNotify(clamped / duration);
            }
        }

        RefreshPreciseTimeInput();
        UpdateMarkedTimeMarker();
        UpdateWaveformPlayhead();
    }

    void RefreshPreciseTimeInput()
    {
        if (preciseTimeInput == null)
        {
            return;
        }

        if (markedDrumStartTime >= 0f)
        {
            preciseTimeInput.SetTextWithoutNotify(markedDrumStartTime.ToString("F3", CultureInfo.InvariantCulture));
        }
        else
        {
            preciseTimeInput.SetTextWithoutNotify(string.Empty);
        }
    }

    void SetupWaveformSeekArea()
    {
        if (waveformImage == null)
        {
            return;
        }

        waveformSeekArea = waveformImage.GetComponent<WaveformSeekArea>();
        if (waveformSeekArea == null)
        {
            waveformSeekArea = waveformImage.gameObject.AddComponent<WaveformSeekArea>();
        }

        waveformSeekArea.Initialize(waveformImage, OnWaveformSeekNormalized);
    }

    void OnWaveformSeekNormalized(float normalized)
    {
        if (previewAudioSource == null || previewAudioSource.clip == null)
        {
            return;
        }

        normalized = Mathf.Clamp01(normalized);
        previewAudioSource.time = normalized * previewAudioSource.clip.length;

        if (audioSeekSlider != null)
        {
            audioSeekSlider.SetValueWithoutNotify(normalized);
        }

        UpdateWaveformPlayhead();
    }

    void BuildWaveformTexture(AudioClip clip)
    {
        if (clip == null || waveformImage == null)
        {
            return;
        }

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
        Color32 wave = waveformColor;

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = background;
        }

        int channels = Mathf.Max(1, clip.channels);
        int frameCount = Mathf.Max(1, clip.samples);
        int framesPerPixel = Mathf.Max(1, frameCount / width);

        float[] audioData = new float[frameCount * channels];
        clip.GetData(audioData, 0);

        float[] amplitudes = new float[width];
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
                    {
                        peak = amplitude;
                    }

                    sumSquares += amplitude * amplitude;
                    sampleCount++;
                }
            }

            float rms = sampleCount > 0 ? Mathf.Sqrt(sumSquares / sampleCount) : 0f;
            amplitudes[x] = useRmsForWaveform ? rms : peak;
        }

        float normalizationReference = GetAmplitudePercentile(amplitudes, waveformNormalizationPercentile);
        normalizationReference = Mathf.Max(0.01f, normalizationReference);

        int halfHeight = height / 2;
        for (int x = 0; x < width; x++)
        {
            float normalized = Mathf.Clamp01(amplitudes[x] / normalizationReference) * waveformHeadroom;
            int ampHeight = Mathf.Clamp(Mathf.RoundToInt(normalized * (halfHeight - 1)), 1, halfHeight - 1);
            int yMin = halfHeight - ampHeight;
            int yMax = halfHeight + ampHeight;

            for (int y = yMin; y <= yMax; y++)
            {
                pixels[y * width + x] = wave;
            }
        }

        waveformTexture.SetPixels32(pixels);
        waveformTexture.Apply(false, false);
        waveformImage.texture = waveformTexture;
        UpdateMarkedTimeMarker();
        UpdateWaveformPlayhead();
    }

    float GetAmplitudePercentile(float[] values, float percentile)
    {
        if (values == null || values.Length == 0)
        {
            return 1f;
        }

        float[] sorted = new float[values.Length];
        Array.Copy(values, sorted, values.Length);
        Array.Sort(sorted);

        float clampedPercentile = Mathf.Clamp01(percentile);
        int index = Mathf.Clamp(Mathf.FloorToInt((sorted.Length - 1) * clampedPercentile), 0, sorted.Length - 1);
        return sorted[index];
    }

    void UpdateWaveformPlayhead()
    {
        if (waveformPlayhead == null || waveformImage == null || previewAudioSource == null || previewAudioSource.clip == null)
        {
            return;
        }

        Rect rect = waveformImage.rectTransform.rect;
        if (rect.width <= 0f)
        {
            return;
        }

        float duration = previewAudioSource.clip.length;
        if (duration <= 0f)
        {
            return;
        }

        float normalized = Mathf.Clamp01(previewAudioSource.time / duration);
        float x = Mathf.Lerp(rect.xMin, rect.xMax, normalized);

        Vector2 anchored = waveformPlayhead.anchoredPosition;
        anchored.x = x;
        waveformPlayhead.anchoredPosition = anchored;
    }

    void UpdateMarkedTimeMarker()
    {
        if (waveformMarkedTimeMarker == null)
        {
            return;
        }

        bool hasMark = markedDrumStartTime >= 0f;
        waveformMarkedTimeMarker.gameObject.SetActive(hasMark);

        if (!hasMark || waveformImage == null || previewAudioSource == null || previewAudioSource.clip == null)
        {
            return;
        }

        Rect rect = waveformImage.rectTransform.rect;
        float duration = previewAudioSource.clip.length;
        if (rect.width <= 0f || duration <= 0f)
        {
            return;
        }

        float normalized = Mathf.Clamp01(markedDrumStartTime / duration);
        float x = Mathf.Lerp(rect.xMin, rect.xMax, normalized);

        Vector2 anchored = waveformMarkedTimeMarker.anchoredPosition;
        anchored.x = x;
        waveformMarkedTimeMarker.anchoredPosition = anchored;
    }
    
    void OnConfirmOffset()
    {
        if (markedDrumStartTime < 0f)
        {
            const string warningMessage = "No drum start time marked!";
            UnityEngine.Debug.LogWarning(warningMessage);
            AppErrorPopup.Show(warningMessage);
            return;
        }
        
        ExitPreviewMode();
        SetTransitionLoading(true, "Generating beatmap...");
        StartCoroutine(ProcessBeatmap());
    }
    
    void OnCancelPreview()
    {
        ExitPreviewMode();
        
        // Clear temp paths
        tempMidiPath = null;
        tempAudioPath = null;
        
        // Reset drag-drop selections
        ResetDragDropSelection();
        
        UnityEngine.Debug.Log("Import cancelled.");
    }
    
    IEnumerator ProcessBeatmap()
    {
        SetTransitionLoading(true, "Generating beatmap...");

        UnityEngine.Debug.Log($"Processing MIDI: {tempMidiPath}");
        UnityEngine.Debug.Log($"With audio: {tempAudioPath}");
        
        // Use marked drum start time, or fall back to input field/default
        float drumStartOffset = defaultDrumStartOffset;
        
        if (markedDrumStartTime >= 0f)
        {
            drumStartOffset = markedDrumStartTime;
            UnityEngine.Debug.Log($"Using marked drum start offset: {drumStartOffset:F2}s");
        }
        else if (drumOffsetInput != null && !string.IsNullOrEmpty(drumOffsetInput.text))
        {
            if (float.TryParse(drumOffsetInput.text, out float parsedOffset))
            {
                drumStartOffset = parsedOffset;
                UnityEngine.Debug.Log($"Using drum start offset from input: {drumStartOffset}s");
            }
            else
            {
                string warningMessage = $"Invalid offset input '{drumOffsetInput.text}', using default: {defaultDrumStartOffset}s";
                UnityEngine.Debug.LogWarning(warningMessage);
                AppErrorPopup.Show(warningMessage);
            }
        }
        
        // Generate unique folder for this beatmap
        string beatmapName = Path.GetFileNameWithoutExtension(tempMidiPath);
        string beatmapFolder = Path.Combine(beatmapsDirectory, beatmapName);
        
        // If folder exists, add timestamp
        if (Directory.Exists(beatmapFolder))
        {
            beatmapName = beatmapName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            beatmapFolder = Path.Combine(beatmapsDirectory, beatmapName);
        }
        
        Directory.CreateDirectory(beatmapFolder);
        
        // Define file paths
        string jsonPath = Path.Combine(beatmapFolder, "beatmap.json");
        string midiCopyPath = Path.Combine(beatmapFolder, Path.GetFileName(tempMidiPath));
        string audioCopyPath = Path.Combine(beatmapFolder, Path.GetFileName(tempAudioPath));
        
        // Copy files to beatmap folder
        File.Copy(tempMidiPath, midiCopyPath, true);
        File.Copy(tempAudioPath, audioCopyPath, true);
        
        // Parse MIDI to JSON with offset
        SetTransitionLoading(true, "Parsing MIDI and building beatmap...");
        yield return StartCoroutine(RunPythonParser(midiCopyPath, jsonPath, drumStartOffset));
        
        // Load and create BeatmapData
        if (File.Exists(jsonPath))
        {
            BeatmapData beatmapData = LoadBeatmapData(jsonPath, midiCopyPath, audioCopyPath);
            
            if (beatmapData != null)
            {
                UnityEngine.Debug.Log($"Successfully imported beatmap: {beatmapData.title}");
                OnBeatmapImported?.Invoke(beatmapData);
                
                // Reset drag-drop selections after successful import
                ResetDragDropSelection();
            }
        }
        else
        {
            UnityEngine.Debug.LogError("Failed to generate beatmap JSON file.");
        }
        
        // Clear temp paths and preview state
        tempMidiPath = null;
        tempAudioPath = null;
        markedDrumStartTime = -1f;
        RefreshPreciseTimeInput();
        UpdateMarkedTimeMarker();

        SetTransitionLoading(false);
    }
    
    IEnumerator RunPythonParser(string inputPath, string outputPath, float drumStartOffset)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "python",
            // Pass all parameters: input, output, spawn_lead, audio_offset, drum_start_offset
            Arguments = $"\"{pythonScriptPath}\" \"{inputPath}\" \"{outputPath}\" {spawnLeadTime} {audioOffset} {drumStartOffset}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        UnityEngine.Debug.Log($"Running: {startInfo.FileName} {startInfo.Arguments}");
        UnityEngine.Debug.Log($"Drum Start Offset: {drumStartOffset}s (song will skip to this position on playback)");
        
        Process process = new Process { StartInfo = startInfo };
        process.Start();
        
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        
        while (!process.HasExited)
        {
            yield return null;
        }
        
        if (!string.IsNullOrEmpty(output))
            UnityEngine.Debug.Log("Python Output: " + output);
        
        if (!string.IsNullOrEmpty(error))
            UnityEngine.Debug.LogError("Python Error: " + error);
        
        if (process.ExitCode != 0)
        {
            UnityEngine.Debug.LogError($"Python script failed with exit code: {process.ExitCode}");
        }
    }
    
    BeatmapData LoadBeatmapData(string jsonPath, string midiPath, string audioPath)
    {
        try
        {
            string jsonContent = File.ReadAllText(jsonPath);
            
            // Parse using JsonUtility
            var wrapper = JsonUtility.FromJson<BeatmapWrapper>(jsonContent);
            
            BeatmapData beatmapData = new BeatmapData
            {
                title = wrapper.metadata.title,
                midiFilePath = midiPath,
                audioFilePath = audioPath,
                jsonFilePath = jsonPath,
                metadata = wrapper.metadata,
                beatmap = wrapper.beatmap
            };
            
            if (beatmapData.metadata != null)
            {
                UnityEngine.Debug.Log($"[BeatmapImporter] Loaded beatmap: {beatmapData.title}, offset={beatmapData.metadata.drum_start_offset:F1}s, audio_includes_countdown={beatmapData.metadata.audio_includes_countdown}");
            }
            
            return beatmapData;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error loading beatmap data: {e.Message}");
            return null;
        }
    }
    
    // Drag-drop callback methods
    void OnMidiFileSelected(string filePath)
    {
        selectedMidiPath = filePath;
        UnityEngine.Debug.Log($"[BeatmapImporter] MIDI file selected: {Path.GetFileName(filePath)}");
        UpdateImportStatus();
    }
    
    void OnAudioFileSelected(string filePath)
    {
        selectedAudioPath = filePath;
        UnityEngine.Debug.Log($"[BeatmapImporter] Audio file selected: {Path.GetFileName(filePath)}");
        UpdateImportStatus();
    }
    
    void UpdateImportStatus()
    {
        if (importStatusText == null) return;
        
        bool hasMidi = !string.IsNullOrEmpty(selectedMidiPath);
        bool hasAudio = !string.IsNullOrEmpty(selectedAudioPath);
        
        if (hasMidi && hasAudio)
        {
            importStatusText.text = "Ready to import";
            importStatusText.color = Color.green;
            
            if (importButton != null)
                importButton.interactable = true;
        }
        else if (hasMidi)
        {
            importStatusText.text = "Audio file required";
            importStatusText.color = Color.yellow;
            
            if (importButton != null)
                importButton.interactable = false;
        }
        else if (hasAudio)
        {
            importStatusText.text = "MIDI file required";
            importStatusText.color = Color.yellow;
            
            if (importButton != null)
                importButton.interactable = false;
        }
        else
        {
            importStatusText.text = "Select MIDI and Audio files";
            importStatusText.color = Color.white;
        }

        if (importConfirmButton != null)
            importConfirmButton.interactable = hasMidi && hasAudio;

        if (importButton != null)
            importButton.interactable = true;
    }
    
    IEnumerator ProcessImportWithDragDropFiles(string midiPath, string audioPath)
    {
        tempMidiPath = midiPath;
        tempAudioPath = audioPath;
        
        // Load audio for preview
        UnityEngine.Debug.Log($"Loading audio for preview: {tempAudioPath}");
        
        using (var www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip("file:///" + tempAudioPath, AudioType.UNKNOWN))
        {
            yield return www.SendWebRequest();
            
            if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError($"Failed to load audio: {www.error}");
                SetTransitionLoading(false);
                yield break;
            }
            
            AudioClip clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
            
            if (clip == null)
            {
                UnityEngine.Debug.LogError("Failed to create audio clip!");
                SetTransitionLoading(false);
                yield break;
            }
            
            previewAudioSource.clip = clip;
            BuildWaveformTexture(clip);
            
            // Show preview panel
            EnterPreviewMode();
        }
    }

    void SetTransitionLoading(bool isVisible, string statusText = "Loading...")
    {
        isTransitionLoading = isVisible;

        if (transitionLoadingText != null)
        {
            transitionLoadingText.text = isVisible ? statusText : string.Empty;
        }

        if (transitionLoadingPanel != null)
        {
            transitionLoadingPanel.SetActive(isVisible);
        }
    }
    
    public void ResetDragDropSelection()
    {
        selectedMidiPath = null;
        selectedAudioPath = null;
        
        if (midiDropArea != null)
            midiDropArea.ClearSelection();
        
        if (audioDropArea != null)
            audioDropArea.ClearSelection();
        
        UpdateImportStatus();
    }
    
    [Serializable]
    private class BeatmapWrapper
    {
        public BeatmapData.BeatmapMetadata metadata;
        public System.Collections.Generic.List<BeatmapNote> beatmap;
    }
}

public class WaveformSeekArea : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private RawImage targetWaveform;
    private Action<float> onSeekNormalized;

    public void Initialize(RawImage waveform, Action<float> seekCallback)
    {
        targetWaveform = waveform;
        onSeekNormalized = seekCallback;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        SeekFromPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        SeekFromPointer(eventData);
    }

    private void SeekFromPointer(PointerEventData eventData)
    {
        if (targetWaveform == null || onSeekNormalized == null)
        {
            return;
        }

        RectTransform rectTransform = targetWaveform.rectTransform;
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            return;
        }

        Rect rect = rectTransform.rect;
        float normalized = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        onSeekNormalized.Invoke(normalized);
    }
}

public class ImportPanelLinkHandler : MonoBehaviour, IPointerClickHandler
{
    private TMP_Text targetText;
    private string fallbackUrl;

    public void Initialize(TMP_Text textComponent, string defaultUrl)
    {
        targetText = textComponent;
        fallbackUrl = defaultUrl;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (targetText == null)
        {
            return;
        }

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(targetText, eventData.position, eventData.pressEventCamera);

        if (linkIndex >= 0)
        {
            TMP_LinkInfo linkInfo = targetText.textInfo.linkInfo[linkIndex];
            string linkId = linkInfo.GetLinkID();
            string url = string.IsNullOrWhiteSpace(linkId) ? fallbackUrl : linkId;
            Application.OpenURL(url);
            return;
        }

        if (!string.IsNullOrWhiteSpace(fallbackUrl))
        {
            Application.OpenURL(fallbackUrl);
        }
    }
}
