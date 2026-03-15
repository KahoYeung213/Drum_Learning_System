using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Simple UI to display hit detection results and scores
/// </summary>
public class HitFeedbackUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HitDetector hitDetector;
    
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private TextMeshProUGUI accuracyText;
    [SerializeField] private TextMeshProUGUI hitFeedbackText;
    [SerializeField] private TextMeshProUGUI statsText;
    
    [Header("Feedback Settings")]
    [SerializeField] private float feedbackDisplayDuration = 1f;
    [SerializeField] private Color perfectColor = Color.cyan;
    [SerializeField] private Color goodColor = Color.green;
    [SerializeField] private Color okColor = Color.yellow;
    [SerializeField] private Color missColor = Color.red;
    
    private Coroutine feedbackCoroutine;
    
    void Start()
    {
        // Auto-find HitDetector
        if (hitDetector == null)
            hitDetector = FindFirstObjectByType<HitDetector>();
        
        if (hitDetector != null)
        {
            hitDetector.OnNoteHit += OnNoteHit;
            hitDetector.OnScoreUpdated += OnScoreUpdated;
        }
        
        // Initialize UI
        UpdateScoreDisplay(new ScoreData());
        
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
        
        if (statsText != null)
        {
            statsText.text = $"Perfect: {score.perfectHits}\tGood: {score.goodHits}\n" +
                           $"OK: {score.okHits}\t\tMiss: {score.missHits}\n" +
                           $"Max Combo: {score.maxCombo}";
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
