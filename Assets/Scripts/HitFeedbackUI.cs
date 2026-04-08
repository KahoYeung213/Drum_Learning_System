using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// Simple UI to display hit detection results and scores
/// </summary>
public class HitFeedbackUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HitDetector hitDetector;
    [SerializeField] private BeatmapPlayer beatmapPlayer;
    
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private TextMeshProUGUI accuracyText;
    [SerializeField] private TextMeshProUGUI hitFeedbackText;

    [Header("End Stats UI")]
    [SerializeField] private TextMeshProUGUI endScoreText;
    [SerializeField] private TextMeshProUGUI endAccuracyText;
    [SerializeField] private TextMeshProUGUI endMaxComboText;
    [SerializeField] private TextMeshProUGUI endPerfectHitsText;
    [SerializeField] private TextMeshProUGUI endGoodHitsText;
    [SerializeField] private TextMeshProUGUI endOkHitsText;
    [SerializeField] private TextMeshProUGUI endMissHitsText;
    [SerializeField] private TextMeshProUGUI endEarlyHitsText;
    [SerializeField] private TextMeshProUGUI endLateHitsText;

    [Header("Panels")]
    [SerializeField] private GameObject liveHudPanel;
    [SerializeField] private GameObject endStatsPanel;
    [SerializeField] private GameObject bottomPanel;
    [SerializeField] private Button endStatsBackButton;
    
    [Header("Feedback Settings")]
    [SerializeField] private float feedbackDisplayDuration = 1f;
    [SerializeField] private Color perfectColor = Color.cyan;
    [SerializeField] private Color goodColor = Color.green;
    [SerializeField] private Color okColor = Color.yellow;
    [SerializeField] private Color missColor = Color.red;
    
    private Coroutine feedbackCoroutine;
    private bool hasActiveRun;
    private ScoreData lastScore = new ScoreData();
    
    void Start()
    {
        // Auto-find HitDetector
        if (hitDetector == null)
            hitDetector = FindFirstObjectByType<HitDetector>();

        if (beatmapPlayer == null)
            beatmapPlayer = FindFirstObjectByType<BeatmapPlayer>();
        
        if (hitDetector != null)
        {
            hitDetector.OnNoteHit += OnNoteHit;
            hitDetector.OnScoreUpdated += OnScoreUpdated;
        }

        if (beatmapPlayer != null)
        {
            beatmapPlayer.OnPlaybackStarted += OnPlaybackStarted;
            beatmapPlayer.OnPlaybackStopped += OnPlaybackStopped;
            beatmapPlayer.OnPlaybackCompleted += OnPlaybackCompleted;
        }
        
        // Initialize UI
        UpdateScoreDisplay(new ScoreData());
        SetLiveHudVisible(true);
        SetEndStatsVisible(false);

        if (endStatsBackButton != null)
            endStatsBackButton.onClick.AddListener(CloseEndStatsScreen);
        
        if (hitFeedbackText != null)
            hitFeedbackText.text = "";
    }
    
    void OnDestroy()
    {
        if (hitDetector != null)
        {
            hitDetector.OnNoteHit -= OnNoteHit;
            hitDetector.OnScoreUpdated -= OnScoreUpdated;
        }

        if (beatmapPlayer != null)
        {
            beatmapPlayer.OnPlaybackStarted -= OnPlaybackStarted;
            beatmapPlayer.OnPlaybackStopped -= OnPlaybackStopped;
            beatmapPlayer.OnPlaybackCompleted -= OnPlaybackCompleted;
        }

        if (endStatsBackButton != null)
            endStatsBackButton.onClick.RemoveListener(CloseEndStatsScreen);
    }

    void OnPlaybackStarted()
    {
        hasActiveRun = true;
        SetLiveHudVisible(true);
        SetEndStatsVisible(false);
        SetBottomPanelVisible(true);
        if (hitFeedbackText != null)
        {
            hitFeedbackText.text = string.Empty;
        }
    }

    void OnPlaybackCompleted()
    {
        ShowEndStats(lastScore);
    }

    void OnPlaybackStopped()
    {
        if (hasActiveRun)
        {
            ShowEndStats(lastScore);
        }
    }
    
    void OnNoteHit(HitResult result)
    {
        // Show hit feedback
        if (hitFeedbackText != null)
        {
            float timing = result.timingError * 1000f;
            string timingLabel = GetTimingLabel(result);
            
            string feedback = $"{timingLabel}\n{Mathf.Abs(timing):F0}ms";
            
            Color color = GetGradeColor(result.grade);
            
            if (feedbackCoroutine != null)
                StopCoroutine(feedbackCoroutine);
            
            feedbackCoroutine = StartCoroutine(ShowFeedback(feedback, color));
        }
    }

    string GetTimingLabel(HitResult result)
    {
        bool isEarly = result.timingError > 0f;

        switch (result.grade)
        {
            case HitGrade.OK:
            case HitGrade.Miss:
                return isEarly ? "Super Early" : "Super Late";
            case HitGrade.Perfect:
                return "On Time";
            case HitGrade.Good:
            default:
                return isEarly ? "Early" : "Late";
        }
    }
    
    void OnScoreUpdated(ScoreData score)
    {
        lastScore = score;
        UpdateScoreDisplay(score);
    }
    
    void UpdateScoreDisplay(ScoreData score)
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score.totalScore}";
        
        if (comboText != null)
        {
            comboText.text = $"Combo: {score.combo}x";
            comboText.color = score.combo > 10 ? Color.yellow : Color.white;
        }
        
        if (accuracyText != null)
            accuracyText.text = $"Accuracy: {score.accuracy:F1}%";
    }

    void ShowEndStats(ScoreData score)
    {
        hasActiveRun = false;
        SetLiveHudVisible(false);
        SetEndStatsVisible(true);
        SetBottomPanelVisible(true);

        if (endScoreText != null)
            endScoreText.text = score.totalScore.ToString();

        if (endAccuracyText != null)
            endAccuracyText.text = $"{score.accuracy:F1}%";

        if (endMaxComboText != null)
            endMaxComboText.text = score.maxCombo.ToString();

        if (endPerfectHitsText != null)
            endPerfectHitsText.text = score.perfectHits.ToString();

        if (endGoodHitsText != null)
            endGoodHitsText.text = score.goodHits.ToString();

        if (endOkHitsText != null)
            endOkHitsText.text = score.okHits.ToString();

        if (endMissHitsText != null)
            endMissHitsText.text = score.missHits.ToString();

        if (endEarlyHitsText != null)
            endEarlyHitsText.text = score.earlyHits.ToString();

        if (endLateHitsText != null)
            endLateHitsText.text = score.lateHits.ToString();

        if (hitFeedbackText != null)
        {
            hitFeedbackText.text = string.Empty;
        }
    }

    public void CloseEndStatsScreen()
    {
        SetEndStatsVisible(false);
        SetLiveHudVisible(true);
        SetBottomPanelVisible(true);
    }

    void SetLiveHudVisible(bool visible)
    {
        if (liveHudPanel != null)
        {
            liveHudPanel.SetActive(visible);
            return;
        }

        if (scoreText != null) scoreText.gameObject.SetActive(visible);
        if (comboText != null) comboText.gameObject.SetActive(visible);
        if (accuracyText != null) accuracyText.gameObject.SetActive(visible);
        if (hitFeedbackText != null) hitFeedbackText.gameObject.SetActive(visible);
    }

    void SetEndStatsVisible(bool visible)
    {
        if (endStatsPanel != null)
        {
            endStatsPanel.SetActive(visible);
            return;
        }

        if (endScoreText != null) endScoreText.gameObject.SetActive(visible);
        if (endAccuracyText != null) endAccuracyText.gameObject.SetActive(visible);
        if (endMaxComboText != null) endMaxComboText.gameObject.SetActive(visible);
        if (endPerfectHitsText != null) endPerfectHitsText.gameObject.SetActive(visible);
        if (endGoodHitsText != null) endGoodHitsText.gameObject.SetActive(visible);
        if (endOkHitsText != null) endOkHitsText.gameObject.SetActive(visible);
        if (endMissHitsText != null) endMissHitsText.gameObject.SetActive(visible);
        if (endEarlyHitsText != null) endEarlyHitsText.gameObject.SetActive(visible);
        if (endLateHitsText != null) endLateHitsText.gameObject.SetActive(visible);
    }

    void SetBottomPanelVisible(bool visible)
    {
        if (bottomPanel != null)
        {
            bottomPanel.SetActive(visible);
        }
    }
    
    IEnumerator ShowFeedback(string text, Color color)
    {
        hitFeedbackText.text = text;
        hitFeedbackText.color = color;
        hitFeedbackText.fontSize = 48;
        
        // Pulse effect
        float elapsed = 0f;
        while (elapsed < feedbackDisplayDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / feedbackDisplayDuration;
            
            // Fade out
            Color c = color;
            c.a = 1f - t;
            hitFeedbackText.color = c;
            
            // Shrink slightly
            hitFeedbackText.fontSize = Mathf.Lerp(48, 36, t);
            
            yield return null;
        }
        
        hitFeedbackText.text = "";
    }
    
    Color GetGradeColor(HitGrade grade)
    {
        switch (grade)
        {
            case HitGrade.Perfect:
                return perfectColor;
            case HitGrade.Good:
                return goodColor;
            case HitGrade.OK:
                return okColor;
            case HitGrade.Miss:
                return missColor;
            default:
                return Color.white;
        }
    }
}
