using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages switching between Learning Mode and Gameplay Mode
/// - Learning Mode: Timeline, speed control, trimming buttons visible
/// - Gameplay Mode: Score/accuracy UI visible, timeline hidden
/// </summary>
public class GameModeManager : MonoBehaviour
{
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
    
    [Header("Always Visible UI")]
    [SerializeField] private GameObject playbackControls; // Play/Pause/Stop buttons (always visible)
    
    [Header("References")]
    [SerializeField] private BeatmapPlayer beatmapPlayer; // To sync speed panel visibility with metronome
    
    [Header("Settings")]
    [SerializeField] private bool startInLearningMode = true;
    
    private bool isLearningMode = true;
    
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
        isLearningMode = startInLearningMode;
        UpdateModeUI(false); // Don't log on startup
        
        Debug.Log($"[GameModeManager] Initialized in {(isLearningMode ? "FreePlay" : "Gameplay")} mode");
    }
    
    /// <summary>
    /// Toggle between Learning and Gameplay modes
    /// </summary>
    public void ToggleMode()
    {
        isLearningMode = !isLearningMode;
        UpdateModeUI(true);
        
        Debug.Log($"[GameModeManager] Switched to {(isLearningMode ? "FreePlay" : "Gameplay")} mode");
    }
    
    /// <summary>
    /// Set mode explicitly
    /// </summary>
    public void SetMode(bool learningMode)
    {
        if (isLearningMode != learningMode)
        {
            isLearningMode = learningMode;
            UpdateModeUI(true);
        }
    }
    
    /// <summary>
    /// Update UI visibility based on current mode
    /// </summary>
    void UpdateModeUI(bool useAnimation)
    {
        // Learning Mode UI (visible only in learning mode)
        SetActive(timelinePanel, isLearningMode);
        // Note: speedPanel visibility is managed by BeatmapPlayer based on metronome state
        SetActive(snipModePanel, false); // Snip mode panel only shown when scissors is clicked
        if (scissorsButton != null)
        {
            SetActive(scissorsButton.gameObject, isLearningMode);
        }
        
        // Gameplay Mode UI (visible only in gameplay mode)
        SetActive(scorePanel, !isLearningMode);
        
        // Update speed panel visibility through BeatmapPlayer
        // Speed panel only visible when: metronome ON AND learning mode
        if (beatmapPlayer != null)
        {
            beatmapPlayer.UpdateSpeedPanelVisibility();
        }
        
        // Update toggle button text
        if (modeButtonText != null)
        {
            modeButtonText.text = isLearningMode ? "Switch to Gameplay" : "Switch to FreePlay";
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
    public bool IsLearningMode => isLearningMode;
    public bool IsGameplayMode => !isLearningMode;
}
