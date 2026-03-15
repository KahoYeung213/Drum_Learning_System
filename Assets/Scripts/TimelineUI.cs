using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class TimelineUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BeatmapPlayer beatmapPlayer;
    [SerializeField] private BeatmapSnipper beatmapSnipper; // For scissor/snip functionality
    [SerializeField] private Slider timelineSlider;
    [SerializeField] private TextMeshProUGUI currentTimeText;
    [SerializeField] private TextMeshProUGUI totalTimeText;
    [SerializeField] private TextMeshProUGUI songTitleText;
    [SerializeField] private Image progressFill;
    
    [Header("Playback Controls")]
    [SerializeField] private Button playPauseButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private TextMeshProUGUI playPauseButtonText;
    [SerializeField] private Sprite playIcon;
    [SerializeField] private Sprite pauseIcon;
    
    private bool isInitialized = false;
    
    void Start()
    {
        // Check for EventSystem
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            Debug.LogError("[TimelineUI] No EventSystem found in scene! UI interactions will not work. Add an EventSystem to the scene.");
        }
        
        InitializeReferences();
        SetupEventListeners();
        UpdateUI();
    }
    
    void InitializeReferences()
    {
        // IMPORTANT: This component should be attached to the Timeline slider or container,
        // NOT to a full-screen panel, or it will block all UI interactions!
        
        // Find beatmap player if not assigned
        if (beatmapPlayer == null)
        {
            beatmapPlayer = FindFirstObjectByType<BeatmapPlayer>();
            if (beatmapPlayer == null)
            {
                Debug.LogError("BeatmapPlayer not found! Please assign it or add a BeatmapPlayer to the scene.");
                return;
            }
        }
        
        // Find beatmap snipper if not assigned
        if (beatmapSnipper == null)
        {
            beatmapSnipper = FindFirstObjectByType<BeatmapSnipper>();
        }
        
        // Setup slider if assigned
        if (timelineSlider != null)
        {
            timelineSlider.minValue = 0f;
            timelineSlider.maxValue = 1f;
            timelineSlider.value = 0f;
            timelineSlider.onValueChanged.AddListener(OnSliderValueChanged);
        }
        
        // Setup playback buttons with validation
        if (playPauseButton != null)
        {
            // Check if this button is mistakenly assigned to BeatmapImporter
            BeatmapImporter importer = FindFirstObjectByType<BeatmapImporter>();
            if (importer != null)
            {
                // Use reflection to check if the same button is used
                var importerButton = importer.GetType().GetField("importButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(importer) as Button;
                if (importerButton == playPauseButton)
                {
                    Debug.LogError("[TimelineUI] Play/Pause button is the SAME as the Import button! Please assign separate buttons.");
                    Debug.LogError($"[TimelineUI] Button name: {playPauseButton.name} - This should be a PLAY button, NOT the import button!");
                }
            }
            
            // Clear any existing listeners to avoid duplicates
            playPauseButton.onClick.RemoveAllListeners();
            playPauseButton.onClick.AddListener(OnPlayPauseClicked);
            Debug.Log($"[TimelineUI] Play/Pause button connected: {playPauseButton.name}");
        }
        else
        {
            Debug.LogWarning("[TimelineUI] Play/Pause button not assigned!");
        }
        
        if (stopButton != null)
        {
            stopButton.onClick.RemoveAllListeners();
            stopButton.onClick.AddListener(OnStopClicked);
            Debug.Log($"[TimelineUI] Stop button connected: {stopButton.name}");
        }
        
        isInitialized = true;
    }
    
    void SetupEventListeners()
    {
        if (beatmapPlayer != null)
        {
            beatmapPlayer.OnBeatmapLoaded += OnBeatmapLoaded;
            beatmapPlayer.OnBeatmapUnloaded += OnBeatmapUnloaded;
            beatmapPlayer.OnPlaybackStarted += UpdatePlayPauseButton;
            beatmapPlayer.OnPlaybackPaused += UpdatePlayPauseButton;
            beatmapPlayer.OnPlaybackStopped += UpdatePlayPauseButton;
        }
    }
    
    void OnDestroy()
    {
        if (beatmapPlayer != null)
        {
            beatmapPlayer.OnBeatmapLoaded -= OnBeatmapLoaded;
            beatmapPlayer.OnBeatmapUnloaded -= OnBeatmapUnloaded;
            beatmapPlayer.OnPlaybackStarted -= UpdatePlayPauseButton;
            beatmapPlayer.OnPlaybackPaused -= UpdatePlayPauseButton;
            beatmapPlayer.OnPlaybackStopped -= UpdatePlayPauseButton;
        }
        
        if (timelineSlider != null)
        {
            timelineSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }
    }
    
    void Update()
    {
        if (!isInitialized || beatmapPlayer == null) return;
        
        // Update timeline progress while playing
        if (beatmapPlayer.IsPlaying)
        {
            UpdateTimelinePosition();
        }
    }
    
    void UpdateTimelinePosition()
    {
        if (beatmapPlayer == null || !beatmapPlayer.IsLoaded) return;
        
        float progress = beatmapPlayer.Progress;
        
        if (timelineSlider != null)
        {
            timelineSlider.value = progress;
        }
        
        if (progressFill != null)
        {
            progressFill.fillAmount = progress;
        }
        
        UpdateTimeText();
    }
    
    void UpdateTimeText()
    {
        if (beatmapPlayer == null || !beatmapPlayer.IsLoaded) return;
        
        if (currentTimeText != null)
        {
            currentTimeText.text = FormatTime(beatmapPlayer.CurrentTime);
        }
        
        if (totalTimeText != null)
        {
            totalTimeText.text = FormatTime(beatmapPlayer.Duration);
        }
    }
    
    string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        return $"{minutes:00}:{seconds:00}";
    }
    
    void OnBeatmapLoaded(BeatmapData beatmap)
    {
        Debug.Log($"Timeline: Beatmap loaded - {beatmap.title}");
        
        if (songTitleText != null)
        {
            songTitleText.text = beatmap.title;
        }
        
        UpdateTimeText();
        UpdatePlayPauseButton();
    }
    
    void OnBeatmapUnloaded()
    {
        Debug.Log("Timeline: Beatmap unloaded");
        
        if (songTitleText != null)
        {
            songTitleText.text = "No Beatmap Loaded";
        }
        
        if (currentTimeText != null)
        {
            currentTimeText.text = "00:00";
        }
        
        if (totalTimeText != null)
        {
            totalTimeText.text = "00:00";
        }
        
        if (timelineSlider != null)
        {
            timelineSlider.value = 0f;
        }
        
        if (progressFill != null)
        {
            progressFill.fillAmount = 0f;
        }
        
        UpdatePlayPauseButton();
    }
    
    void OnSliderValueChanged(float value)
    {
        if (beatmapPlayer != null && beatmapPlayer.IsLoaded)
        {
            beatmapPlayer.SeekNormalized(value);
            UpdateTimeText();
        }
    }
    
    void OnPlayPauseClicked()
    {
        Debug.Log("[TimelineUI] Play/Pause button clicked!");
        
        if (beatmapPlayer == null)
        {
            Debug.LogError("[TimelineUI] BeatmapPlayer is null!");
            return;
        }
        
        if (!beatmapPlayer.IsLoaded)
        {
            Debug.LogWarning("[TimelineUI] No beatmap loaded. Please select a beatmap from the library first.");
            return;
        }
        
        beatmapPlayer.TogglePlayPause();
    }
    
    void OnStopClicked()
    {
        if (beatmapPlayer == null || !beatmapPlayer.IsLoaded) return;
        
        beatmapPlayer.Stop();
        beatmapPlayer.Seek(0f);
        UpdateTimelinePosition();
    }
    
    void UpdatePlayPauseButton()
    {
        bool isPlaying = beatmapPlayer != null && beatmapPlayer.IsPlaying;

        if (playPauseButton != null && playIcon != null && pauseIcon != null)
        {
            var btnImage = playPauseButton.GetComponent<Image>();
            if (btnImage != null)
                btnImage.sprite = isPlaying ? pauseIcon : playIcon;
        }

        if (playPauseButtonText != null)
            playPauseButtonText.text = string.Empty;
    }
    
    void UpdateUI()
    {
        UpdateTimelinePosition();
        UpdatePlayPauseButton();
    }
    
    // Public method to manually update the UI
    public void RefreshUI()
    {
        UpdateUI();
    }
}
