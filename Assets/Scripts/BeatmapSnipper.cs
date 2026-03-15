using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;

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
    
    [Header("Draggable Markers (Children of Timeline)")]
    [SerializeField] private DraggableTimelineMarker startMarker; // Must be child of timeline slider
    [SerializeField] private DraggableTimelineMarker endMarker;   // Must be child of timeline slider
    [SerializeField] private Image rangeHighlight; // Must be child of timeline slider
    [SerializeField] private RectTransform fillAreaRect; // Optional: the Fill Area of the slider for precise positioning
    [Tooltip("Markers should be direct children of the timeline slider, siblings with Background/Fill Area/Handle.")]
    [SerializeField] private bool markersSetupCorrectly = false; // Just a visual reminder in inspector
    
    [Header("References")]
    [SerializeField] private BeatmapPlayer beatmapPlayer;
    [SerializeField] private BeatmapLibrary beatmapLibrary;
    [SerializeField] private Slider timelineSlider; // Reference to main timeline slider
    
    [Header("Audio Trimming")]
    [SerializeField] private bool trimAudio = true; // Requires ffmpeg
    [SerializeField] private float countdownDuration = 3.0f; // Silence before snipped audio
    
    private bool isSnipModeActive = false;
    private float startTime = 0f;
    private float endTime = 0f;
    
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
        
        // Hide snip mode UI initially
        if (snipModePanel != null)
            snipModePanel.SetActive(false);
        
        HideMarkers();
    }
    
    void OnScissorsClicked()
    {
        if (!beatmapPlayer.IsLoaded)
        {
            UnityEngine.Debug.LogWarning("[BeatmapSnipper] No beatmap loaded!");
            return;
        }
        
        isSnipModeActive = true;
        
        // Show snip mode UI
        if (snipModePanel != null)
            snipModePanel.SetActive(true);
        
        // Determine the reference rect for positioning
        RectTransform referenceRect = GetReferenceRect();
        
        if (referenceRect == null)
        {
            UnityEngine.Debug.LogError("[BeatmapSnipper] Cannot activate snip mode - no reference rect available!");
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
                if (int.TryParse(parts[0], out int minutes) && int.TryParse(parts[1], out int seconds))
                {
                    return minutes * 60f + seconds;
                }
            }
        }
        // Try parsing as raw seconds
        else if (float.TryParse(input, out float seconds))
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
    
    void OnConfirmSnip()
    {
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
        if (confirmSnipButton != null)
            confirmSnipButton.interactable = false;
        
        StartCoroutine(CreateSnippedBeatmapAsync(snipName, startTime, endTime));
    }
    
    void OnCancelSnip()
    {
        isSnipModeActive = false;
        
        if (snipModePanel != null)
            snipModePanel.SetActive(false);
        
        HideMarkers();
    }
    
    void ShowMarkers()
    {
        if (startMarker != null)
            startMarker.gameObject.SetActive(true);
        
        if (endMarker != null)
            endMarker.gameObject.SetActive(true);
        
        if (rangeHighlight != null)
            rangeHighlight.gameObject.SetActive(true);
    }
    
    void HideMarkers()
    {
        if (startMarker != null && startMarker.gameObject != null)
            startMarker.gameObject.SetActive(false);
        
        if (endMarker != null && endMarker.gameObject != null)
            endMarker.gameObject.SetActive(false);
        
        if (rangeHighlight != null && rangeHighlight.gameObject != null)
            rangeHighlight.gameObject.SetActive(false);
    }
    
    IEnumerator CreateSnippedBeatmapAsync(string snipName, float startTime, float endTime)
    {
        BeatmapData originalBeatmap = beatmapPlayer.CurrentBeatmap;
        
        if (originalBeatmap == null || originalBeatmap.beatmap == null)
        {
            UnityEngine.Debug.LogError("[BeatmapSnipper] Original beatmap is null!");
            if (confirmSnipButton != null)
                confirmSnipButton.interactable = true;
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
            if (confirmSnipButton != null)
                confirmSnipButton.interactable = true;
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
        if (confirmSnipButton != null)
            confirmSnipButton.interactable = true;
        
        OnCancelSnip();
    }
    
    IEnumerator SaveSnippedBeatmapAsync(BeatmapData beatmap, BeatmapData originalBeatmap, float beatmapStartTime, float beatmapEndTime)
    {
        string beatmapsDirectory = "";
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
            beatmapsDirectory = Path.Combine(Application.persistentDataPath, "Beatmaps");
            beatmapFolder = Path.Combine(beatmapsDirectory, beatmap.title);
            
            // If folder exists, add timestamp
            if (Directory.Exists(beatmapFolder))
            {
                beatmap.title = beatmap.title + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                beatmapFolder = Path.Combine(beatmapsDirectory, beatmap.title);
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
