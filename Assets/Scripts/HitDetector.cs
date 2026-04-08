using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Detects note hits, calculates timing accuracy, and manages scoring
/// </summary>
public class HitDetector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BeatmapPlayer beatmapPlayer;
    [SerializeField] private NoteSpawner noteSpawner;
    [SerializeField] private MidiHit midiHit;
    
    [Header("Timing Windows (seconds)")]
    [SerializeField] private float perfectWindow = 0.05f;  // ±50ms
    [SerializeField] private float goodWindow = 0.1f;      // ±100ms
    [SerializeField] private float okWindow = 0.15f;       // ±150ms
    [SerializeField] private float missWindow = 0.2f;      // ±200ms
    
    [Header("Scoring")]
    [SerializeField] private int perfectScore = 100;
    [SerializeField] private int goodScore = 75;
    [SerializeField] private int okScore = 50;
    [SerializeField] private int missScore = 0;
    
    // Events for UI updates
    public event Action<HitResult> OnNoteHit;
    public event Action<ScoreData> OnScoreUpdated;
    
    // Current score data
    private ScoreData currentScore = new ScoreData();
    
    // MIDI note to lane mapping (must match NoteSpawner)
    private Dictionary<int, int> midiToLane = new Dictionary<int, int>()
    {
        {36, 0}, // Bass Drum
        {49, 1}, // Crash
        {21, 2}, {23, 2}, {42, 2}, {44, 2}, {46, 2}, // Hi-Hat variations
        {38, 3}, {40, 3}, // Snare
        {48, 4}, {50, 4}, // Tom 1
        {45, 5}, {47, 5}, // Tom 2
        {43, 6}, {58, 6}, // Tom 3
        {51, 7}, // Ride
    };
    
    private bool isPlaying = false;
    
    void Start()
    {
        // Auto-find references
        if (beatmapPlayer == null)
            beatmapPlayer = FindFirstObjectByType<BeatmapPlayer>();
        
        if (noteSpawner == null)
            noteSpawner = FindFirstObjectByType<NoteSpawner>();
        
        if (midiHit == null)
            midiHit = FindFirstObjectByType<MidiHit>();
        
        // Subscribe to playback events
        if (beatmapPlayer != null)
        {
            beatmapPlayer.OnPlaybackStarted += OnPlaybackStarted;
            beatmapPlayer.OnPlaybackStopped += OnPlaybackStopped;
        }
        
        // Subscribe to MIDI hits
        if (midiHit != null)
        {
            MidiJack.MidiMaster.noteOnDelegate += OnMidiNoteReceived;
        }
        
        Debug.Log("[HitDetector] Initialized");
    }
    
    void OnDestroy()
    {
        if (beatmapPlayer != null)
        {
            beatmapPlayer.OnPlaybackStarted -= OnPlaybackStarted;
            beatmapPlayer.OnPlaybackStopped -= OnPlaybackStopped;
        }
        
        MidiJack.MidiMaster.noteOnDelegate -= OnMidiNoteReceived;
    }
    
    void OnPlaybackStarted()
    {
        isPlaying = true;
        ResetScore();
        Debug.Log("[HitDetector] Playback started - hit detection active");
    }
    
    void OnPlaybackStopped()
    {
        isPlaying = false;
        Debug.Log($"[HitDetector] Playback stopped - Final Score: {currentScore.totalScore}");
    }
    
    void OnMidiNoteReceived(MidiJack.MidiChannel channel, int midiNote, float velocity)
    {
        if (!isPlaying) return;
        
        // Get current time
        float hitTime = beatmapPlayer.CurrentTime;
        
        // Find which lane this MIDI note belongs to
        if (!midiToLane.TryGetValue(midiNote, out int lane))
        {
            Debug.LogWarning($"[HitDetector] Unknown MIDI note: {midiNote}");
            return;
        }
        
        // Process the hit
        ProcessHit(lane, midiNote, hitTime, velocity);
    }
    
    void ProcessHit(int lane, int midiNote, float hitTime, float velocity)
    {
        // Get all active notes from the spawner
        var activeNotes = noteSpawner.GetActiveNotesForLane(lane);
        
        if (activeNotes == null || activeNotes.Count == 0)
        {
            // No notes in this lane - this is a miss/ghost hit
            Debug.Log($"[HitDetector] Ghost hit - Lane {lane} at {hitTime:F3}s (no active notes)");
            return;
        }
        
        // Find the EARLIEST unhit note within the timing window
        // This prevents hitting later notes when there are multiple notes close together
        FallingNote targetNote = null;
        float bestDifference = float.MaxValue;
        
        // Filter to only unhit notes that are currently hittable (not too far in the future)
        var hittableNotes = activeNotes
            .Where(n => !n.IsHit && n.HitTime <= hitTime + missWindow)
            .OrderBy(n => n.HitTime) // Sort by hit time (earliest first)
            .ToList();
        
        if (hittableNotes.Count == 0)
        {
            // No unhit notes within range
            Debug.Log($"[HitDetector] Miss - Lane {lane} at {hitTime:F3}s (no hittable notes)");
            return;
        }
        
        // Check the earliest note first - if it's within timing window, use it
        // This ensures we always hit notes in order
        foreach (var note in hittableNotes)
        {
            float difference = Mathf.Abs(hitTime - note.HitTime);
            
            if (difference <= missWindow)
            {
                // Take the first (earliest) note within window
                targetNote = note;
                bestDifference = difference;
                break; // Stop at first valid note
            }
        }
        
        if (targetNote == null)
        {
            // Hit outside timing window
            Debug.Log($"[HitDetector] Miss - Lane {lane} at {hitTime:F3}s (outside timing window)");
            RecordMiss();
            return;
        }
        
        // Calculate timing difference (positive = early, negative = late)
        float timingError = hitTime - targetNote.HitTime;
        
        // Grade the hit
        HitGrade grade = GradeHit(Mathf.Abs(timingError));
        
        // Create hit result
        HitResult result = new HitResult
        {
            lane = lane,
            midiNote = midiNote,
            noteTime = targetNote.HitTime,
            hitTime = hitTime,
            timingError = timingError,
            grade = grade,
            velocity = velocity
        };
        
        // Mark note as hit IMMEDIATELY to prevent double-hits
        targetNote.MarkAsHit();
        
        // Update score
        UpdateScore(result);
        
        // Destroy the note (it was hit)
        noteSpawner.DestroyNote(targetNote);
        
        // Broadcast result
        OnNoteHit?.Invoke(result);
        
        // Log the hit
        string timing = timingError > 0 ? $"+{timingError * 1000:F1}ms (early)" : $"{timingError * 1000:F1}ms (late)";
        Debug.Log($"[HitDetector] {grade} - Lane {lane}, MIDI {midiNote}, {timing}");
    }
    
    HitGrade GradeHit(float absoluteError)
    {
        if (absoluteError <= perfectWindow) return HitGrade.Perfect;
        if (absoluteError <= goodWindow) return HitGrade.Good;
        if (absoluteError <= okWindow) return HitGrade.OK;
        return HitGrade.Miss;
    }
    
    void UpdateScore(HitResult result)
    {
        int points = 0;

        if (result.timingError > 0f)
            currentScore.earlyHits++;
        else if (result.timingError < 0f)
            currentScore.lateHits++;
        
        switch (result.grade)
        {
            case HitGrade.Perfect:
                currentScore.perfectHits++;
                points = perfectScore;
                break;
            case HitGrade.Good:
                currentScore.goodHits++;
                points = goodScore;
                break;
            case HitGrade.OK:
                currentScore.okHits++;
                points = okScore;
                break;
            case HitGrade.Miss:
                currentScore.missHits++;
                points = missScore;
                break;
        }
        
        currentScore.totalScore += points;
        currentScore.totalNotes++;
        currentScore.combo++;
        
        if (currentScore.combo > currentScore.maxCombo)
            currentScore.maxCombo = currentScore.combo;
        
        // Calculate accuracy
        int totalPossibleScore = currentScore.totalNotes * perfectScore;
        currentScore.accuracy = totalPossibleScore > 0 
            ? (float)currentScore.totalScore / totalPossibleScore * 100f 
            : 0f;
        
        OnScoreUpdated?.Invoke(currentScore);
    }
    
    void RecordMiss()
    {
        currentScore.missHits++;
        currentScore.totalNotes++;
        currentScore.combo = 0; // Break combo
        
        OnScoreUpdated?.Invoke(currentScore);
    }
    
    void ResetScore()
    {
        currentScore = new ScoreData();
        OnScoreUpdated?.Invoke(currentScore);
        Debug.Log("[HitDetector] Score reset");
    }
    
    public ScoreData GetCurrentScore()
    {
        return currentScore;
    }
    
    // Public methods for UI
    public void SetTimingWindows(float perfect, float good, float ok, float miss)
    {
        perfectWindow = perfect;
        goodWindow = good;
        okWindow = ok;
        missWindow = miss;
        Debug.Log($"[HitDetector] Timing windows updated: Perfect={perfect*1000}ms, Good={good*1000}ms, OK={ok*1000}ms, Miss={miss*1000}ms");
    }
}

[System.Serializable]
public class HitResult
{
    public int lane;
    public int midiNote;
    public float noteTime;      // When the note should be hit
    public float hitTime;       // When the user actually hit
    public float timingError;   // Difference (positive = early, negative = late)
    public HitGrade grade;
    public float velocity;
}

public enum HitGrade
{
    Perfect,
    Good,
    OK,
    Miss
}

[System.Serializable]
public class ScoreData
{
    public int totalScore;
    public int totalNotes;
    public int perfectHits;
    public int goodHits;
    public int okHits;
    public int missHits;
    public int earlyHits;
    public int lateHits;
    public int combo;
    public int maxCombo;
    public float accuracy; // 0-100%
}
