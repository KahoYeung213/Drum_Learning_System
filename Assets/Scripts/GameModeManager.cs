using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages switching between Learning Mode and Gameplay Mode
/// - Learning Mode: Timeline, speed control, trimming buttons visible
/// - Gameplay Mode: Score/accuracy UI visible, timeline hidden
/// </summary>
public class GameModeManager : MonoBehaviour
{
    public enum AppMode
    {
        FreePlay,
        Gameplay,
        Course
    }

    [Header("Mode Toggle Button")]
    [SerializeField] private Button modeToggleButton;
    [SerializeField] private TMPro.TextMeshProUGUI modeButtonText;
    
    [Header("Learning Mode UI (Hidden in Gameplay)")]
    [SerializeField] private GameObject timelinePanel; // Timeline slider and controls
    [SerializeField] private GameObject speedPanel; // BPM/speed controls from BeatmapPlayer
    [SerializeField] private GameObject snipModePanel; // Trimming/scissors button from BeatmapSnipper
    [SerializeField] private Button scissorsButton; // Individual scissors button (if not in panel)
    
    [Header("Gameplay Mode UI (Hidden in Learning)")]
    [SerializeField] private GameObject scorePanel; // Score, combo, accuracy display

    [Header("Course Mode UI")]
    [SerializeField] private GameObject coursePanel; // Course scaffolding panel
    [SerializeField] private bool includeCourseInToggle = false;
    
    [Header("Always Visible UI")]
    [SerializeField] private GameObject playbackControls; // Play/Pause/Stop buttons (always visible)
    
    [Header("References")]
    [SerializeField] private BeatmapPlayer beatmapPlayer; // To sync speed panel visibility with metronome
    
    [Header("Settings")]
    [SerializeField] private bool startInLearningMode = true;
    
    private AppMode currentMode = AppMode.FreePlay;
    
    void Start()
    {
        // Auto-find BeatmapPlayer if not assigned
        if (beatmapPlayer == null)
        {
            beatmapPlayer = FindFirstObjectByType<BeatmapPlayer>();
        }
        
        // Setup toggle button
        if (modeToggleButton != null)
        {
            modeToggleButton.onClick.RemoveAllListeners();
            modeToggleButton.onClick.AddListener(ToggleMode);
        }
        
        // Initialize mode
        currentMode = startInLearningMode ? AppMode.FreePlay : AppMode.Gameplay;
        UpdateModeUI(false); // Don't log on startup
        
        Debug.Log($"[GameModeManager] Initialized in {currentMode} mode");
    }
    
    /// <summary>
    /// Toggle between Learning and Gameplay modes
    /// </summary>
    public void ToggleMode()
    {
        if (includeCourseInToggle)
        {
            currentMode = currentMode switch
            {
                AppMode.FreePlay => AppMode.Gameplay,
                AppMode.Gameplay => AppMode.Course,
                _ => AppMode.FreePlay
            };
        }
        else
        {
            currentMode = currentMode == AppMode.FreePlay ? AppMode.Gameplay : AppMode.FreePlay;
        }

        UpdateModeUI(true);
        
        Debug.Log($"[GameModeManager] Switched to {currentMode} mode");
    }
    
    /// <summary>
    /// Set mode explicitly
    /// </summary>
    public void SetMode(bool learningMode)
    {
        AppMode targetMode = learningMode ? AppMode.FreePlay : AppMode.Gameplay;

        if (currentMode != targetMode)
        {
            currentMode = targetMode;
            UpdateModeUI(true);
        }
    }

    /// <summary>
    /// Explicitly enter course mode.
    /// </summary>
    public void SwitchToCourseMode()
    {
        if (currentMode != AppMode.Course)
        {
            currentMode = AppMode.Course;
            UpdateModeUI(true);
        }
    }
    
    /// <summary>
    /// Update UI visibility based on current mode
    /// </summary>
    void UpdateModeUI(bool useAnimation)
    {
        bool isLearningMode = currentMode == AppMode.FreePlay;
        bool isGameplayMode = currentMode == AppMode.Gameplay;
        bool isCourseMode = currentMode == AppMode.Course;

        // Learning Mode UI (visible only in learning mode)
        SetActive(timelinePanel, isLearningMode);
        // Note: speedPanel visibility is managed by BeatmapPlayer based on metronome state
        SetActive(snipModePanel, false); // Snip mode panel only shown when scissors is clicked
        if (scissorsButton != null)
        {
            SetActive(scissorsButton.gameObject, isLearningMode);
        }
        
        // Gameplay Mode UI (visible only in gameplay mode)
        SetActive(scorePanel, isGameplayMode);

        // Course mode UI
        SetActive(coursePanel, isCourseMode);
        
        // Update speed panel visibility through BeatmapPlayer
        // Speed panel only visible when: metronome ON AND learning mode
        if (beatmapPlayer != null)
        {
            beatmapPlayer.UpdateSpeedPanelVisibility();
        }
        
        // Update toggle button text
        if (modeButtonText != null)
        {
            if (includeCourseInToggle)
            {
                modeButtonText.text = currentMode switch
                {
                    AppMode.FreePlay => "Switch to Gameplay",
                    AppMode.Gameplay => "Switch to Course",
                    _ => "Switch to FreePlay"
                };
            }
            else
            {
                modeButtonText.text = isLearningMode ? "Switch to Gameplay" : "Switch to FreePlay";
            }
        }
        
        // Optional: trigger animation if needed
        if (useAnimation)
        {
            // Could add fade-in/fade-out animations here
        }
    }
    
    /// <summary>
    /// Safe SetActive that checks for null
    /// </summary>
    void SetActive(GameObject obj, bool active)
    {
        if (obj != null)
        {
            obj.SetActive(active);
        }
    }
    
    /// <summary>
    /// Get current mode
    /// </summary>
    public bool IsLearningMode => currentMode == AppMode.FreePlay;
    public bool IsGameplayMode => currentMode == AppMode.Gameplay;
    public bool IsCourseMode => currentMode == AppMode.Course;
    public AppMode CurrentMode => currentMode;
}
