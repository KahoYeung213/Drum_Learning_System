using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System;
using System.Collections;

public class BeatmapPlayer : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Slider volumeSlider;
    [SerializeField][Range(0f, 1f)] private float defaultVolume = 1.0f;
    
    [Header("BPM Control")]
    [SerializeField] private TMP_InputField bpmInputField;
    [SerializeField] private Button resetBpmButton; // Optional: Reset to original BPM
    [SerializeField] private TMP_Text currentBpmText; // Optional: Display current BPM
    [SerializeField] private float minBpmPercent = 25f; // Minimum % of original BPM
    [SerializeField] private float maxBpmPercent = 200f; // Maximum % of original BPM
    
    [Header("Metronome")]
    [SerializeField] private Button metronomeButton;
    [SerializeField] private GameObject speedPanel; // Panel shown when metronome is toggled
    [SerializeField] private AudioClip metronomeClickSound;
    [SerializeField] private AudioSource metronomeAudioSource;
    [SerializeField] private Slider metronomeVolumeSlider; // Slider to control metronome volume
    [SerializeField][Range(0f, 1f)] private float metronomeVolume = 0.5f;
    [SerializeField] private Color metronomeActiveColor = Color.green;
    [SerializeField] private Color metronomeInactiveColor = Color.white;
    
    [Header("Mode Management")]
    [SerializeField] private GameModeManager gameModeManager; // Optional: manages learning vs gameplay mode
    
    [Header("Timing Settings")]
    [SerializeField] private float audioOffset = 3.0f; // Countdown time built into beatmap
    
    private BeatmapData currentBeatmap;
    private bool isLoaded = false;
    private bool metronomeEnabled = false;
    private float nextBeatTime = 0f;
    private float beatInterval = 0f;
    private float currentSpeed = 1.0f;
    private float originalBpm = 120f; // Store original BPM from beatmap
    private float currentBpm = 120f;  // Current BPM (modified by user)

    // Pre-roll (countdown before audio starts when drum offset < audioOffset)
    private bool isInPreRoll = false;
    private float preRollStartRealTime = 0f;
    private Coroutine preRollCoroutine = null;
    
    // Events
    public event Action<BeatmapData> OnBeatmapLoaded;
    public event Action OnBeatmapUnloaded;
    public event Action OnPlaybackStarted;
    public event Action OnPlaybackPaused;
    public event Action OnPlaybackStopped;
    public event Action<float> OnSpeedChanged; // Notifies listeners when speed changes
    
    // Properties
    public BeatmapData CurrentBeatmap => currentBeatmap;
    public bool IsLoaded => isLoaded;
    public bool IsPlaying => (audioSource != null && audioSource.isPlaying) || isInPreRoll;
    public float CurrentSpeed => currentSpeed;
    public float CurrentTime 
    {
        get
        {
            if (audioSource == null) return 0f;
            
            // Check if snipped beatmap
            bool audioHasCountdown = currentBeatmap?.metadata?.audio_includes_countdown ?? false;
            
            if (audioHasCountdown)
            {
                return audioSource.time;
            }
            
            // During pre-roll: advance a virtual clock from 0 so notes can scroll in
            // before the audio file actually starts playing.
            if (isInPreRoll)
            {
                return Time.time - preRollStartRealTime;
            }
            
            // Imported: CurrentTime = audio.time + audioOffset
            // offset=13: audio starts at 10 → CurrentTime starts at 13, notes hit at 16
            // offset=0:  pre-roll covers 0→3, audio starts at 0 → CurrentTime = 3 (seamless)
            return audioSource.time + audioOffset;
        }
    }
    public float Duration 
    {
        get
        {
            if (audioSource == null || audioSource.clip == null) return 0f;
            
            // Check if snipped beatmap
            bool audioHasCountdown = currentBeatmap?.metadata?.audio_includes_countdown ?? false;
            
            if (audioHasCountdown)
            {
                return audioSource.clip.length;
            }
            
            // Imported: add 3 to duration since CurrentTime is offset by +3
            return audioSource.clip.length + audioOffset;
        }
    }
    public float Progress => Duration > 0 ? CurrentTime / Duration : 0f;
    
    void Awake()
    {
        // Create audio source if not assigned
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        
        // Set initial volume and speed
        audioSource.volume = defaultVolume;
        currentSpeed = 1.0f;
        audioSource.pitch = currentSpeed;
        
        // Create metronome audio source if not assigned
        if (metronomeAudioSource == null)
        {
            GameObject metronomeObj = new GameObject("Metronome AudioSource");
            metronomeObj.transform.SetParent(transform);
            metronomeAudioSource = metronomeObj.AddComponent<AudioSource>();
            metronomeAudioSource.playOnAwake = false;
            metronomeAudioSource.volume = metronomeVolume;
        }
        
        // Hide speed panel initially
        if (speedPanel != null)
        {
            speedPanel.SetActive(false);
        }
    }
    
    void Start()
    {
        // Setup volume slider
        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value = defaultVolume;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
        
        // Setup BPM input field
        if (bpmInputField != null)
        {
            bpmInputField.onEndEdit.AddListener(OnBpmInputChanged);
        }
        
        // Setup reset BPM button
        if (resetBpmButton != null)
        {
            resetBpmButton.onClick.AddListener(ResetBpm);
        }
        
        // Setup metronome volume slider
        if (metronomeVolumeSlider != null)
        {
            metronomeVolumeSlider.minValue = 0f;
            metronomeVolumeSlider.maxValue = 1f;
            metronomeVolumeSlider.value = metronomeVolume;
            metronomeVolumeSlider.onValueChanged.AddListener(SetMetronomeVolume);
        }
        
        // Setup metronome button
        if (metronomeButton != null)
        {
            metronomeButton.onClick.AddListener(ToggleMetronome);
            UpdateMetronomeButtonVisual();
        }
    }
    
    void Update()
    {
        // Handle metronome clicks
        if (metronomeEnabled && IsPlaying && isLoaded && beatInterval > 0)
        {
            float currentTime = CurrentTime;
            
            // Check if we've passed the next beat time
            if (currentTime >= nextBeatTime)
            {
                PlayMetronomeClick();
                
                // Calculate next beat time
                nextBeatTime += beatInterval;
                
                // Prevent beat accumulation if we're way behind
                if (nextBeatTime < currentTime - beatInterval)
                {
                    nextBeatTime = currentTime + beatInterval;
                }
            }
        }
    }
    
    public void LoadBeatmap(BeatmapData beatmap)
    {
        if (beatmap == null)
        {
            Debug.LogError("[BeatmapPlayer] Cannot load null beatmap!");
            return;
        }
        
        // Unload current beatmap if any
        if (isLoaded)
        {
            Debug.Log("[BeatmapPlayer] Unloading previous beatmap...");
            UnloadBeatmap();
        }
        
        Debug.Log($"[BeatmapPlayer] Loading beatmap: {beatmap.title}");
        Debug.Log($"[BeatmapPlayer] Audio file path: {beatmap.audioFilePath}");
        StartCoroutine(LoadBeatmapCoroutine(beatmap));
    }
    
    IEnumerator LoadBeatmapCoroutine(BeatmapData beatmap)
    {
        // Validate audio file exists
        if (string.IsNullOrEmpty(beatmap.audioFilePath) || !System.IO.File.Exists(beatmap.audioFilePath))
        {
            Debug.LogError($"[BeatmapPlayer] Audio file not found: {beatmap.audioFilePath}");
            yield break;
        }
        
        Debug.Log($"[BeatmapPlayer] Audio file exists, loading...");
        
        // Load audio file
        string audioPath = "file:///" + beatmap.audioFilePath.Replace("\\", "/");
        Debug.Log($"[BeatmapPlayer] Loading from URL: {audioPath}");
        
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(audioPath, GetAudioType(beatmap.audioFilePath)))
        {
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                audioSource.clip = clip;
                currentBeatmap = beatmap;
                isLoaded = true;
                
                Debug.Log($"[BeatmapPlayer] ✓ Successfully loaded audio: {beatmap.title} ({clip.length:F2}s)");
                Debug.Log($"[BeatmapPlayer] Audio is ready to play. Use Play button to start.");
                OnBeatmapLoaded?.Invoke(beatmap);
            }
            else
            {
                Debug.LogError($"[BeatmapPlayer] Failed to load audio: {www.error}");
                Debug.LogError($"[BeatmapPlayer] URL attempted: {audioPath}");
            }
        }
    }
    
    AudioType GetAudioType(string filePath)
    {
        string ext = System.IO.Path.GetExtension(filePath).ToLower();
        
        switch (ext)
        {
            case ".mp3":
                return AudioType.MPEG;
            case ".wav":
                return AudioType.WAV;
            case ".ogg":
                return AudioType.OGGVORBIS;
            default:
                return AudioType.UNKNOWN;
        }
    }
    
    public void UnloadBeatmap()
    {
        Stop();
        
        if (audioSource.clip != null)
        {
            Destroy(audioSource.clip);
            audioSource.clip = null;
        }
        
        currentBeatmap = null;
        isLoaded = false;
        
        OnBeatmapUnloaded?.Invoke();
        Debug.Log("Beatmap unloaded");
    }
    
    public void Play()
    {
        if (!isLoaded)
        {
            Debug.LogWarning("[BeatmapPlayer] No beatmap loaded! Please select a beatmap first.");
            return;
        }
        
        // Check if this is a snipped beatmap with countdown already in audio
        bool audioHasCountdown = currentBeatmap?.metadata?.audio_includes_countdown ?? false;
        
        if (audioHasCountdown)
        {
            // Snipped beatmap: Play from start (countdown is in the audio file)
            isInPreRoll = false;
            audioSource.time = 0f;
            audioSource.Play();
            Debug.Log($"[BeatmapPlayer] Playing snipped beatmap from start: {currentBeatmap.title}");
        }
        else
        {
            float drumOffset = currentBeatmap?.metadata?.drum_start_offset ?? 0f;
            float startPosition = Mathf.Max(0f, drumOffset - audioOffset);
            
            if (startPosition == 0f)
            {
                // Drum offset is within the countdown window (drumOffset < audioOffset).
                // Use a pre-roll: advance a virtual clock for audioOffset seconds so notes
                // can scroll into view, then start the audio at position 0.
                // CurrentTime runs 0 → audioOffset during the delay, then audio picks up
                // seamlessly at audioSource.time + audioOffset = 0 + audioOffset.
                isInPreRoll = true;
                preRollStartRealTime = Time.time;
                if (preRollCoroutine != null) StopCoroutine(preRollCoroutine);
                preRollCoroutine = StartCoroutine(PreRollThenPlay());
                Debug.Log($"[BeatmapPlayer] Playing: {currentBeatmap.title}");
                Debug.Log($"[BeatmapPlayer] Pre-roll: {audioOffset:F1}s countdown before audio starts (drumOffset={drumOffset:F1}s)");
            }
            else
            {
                // drumOffset >= audioOffset: audio can start audioOffset seconds early
                isInPreRoll = false;
                audioSource.time = startPosition;
                audioSource.Play();
                Debug.Log($"[BeatmapPlayer] Playing: {currentBeatmap.title}");
                Debug.Log($"[BeatmapPlayer] Audio starting at: {startPosition:F1}s");
                Debug.Log($"[BeatmapPlayer] First note at beatmap time: {drumOffset + audioOffset:F1}s");
                Debug.Log($"[BeatmapPlayer] Ready timer: {startPosition:F1}s → {drumOffset:F1}s ({audioOffset:F1} seconds)");
            }
        }
        
        OnPlaybackStarted?.Invoke();
        
        if (!isInPreRoll && metronomeEnabled)
        {
            InitializeMetronome();
        }
    }
    
    public void Pause()
    {
        if (!isLoaded)
        {
            Debug.LogWarning("[BeatmapPlayer] No beatmap loaded!");
            return;
        }

        // Cancel any active pre-roll coroutine
        if (preRollCoroutine != null)
        {
            StopCoroutine(preRollCoroutine);
            preRollCoroutine = null;
            isInPreRoll = false;
        }

        Debug.Log("[BeatmapPlayer] Paused");
        audioSource.Pause();
        OnPlaybackPaused?.Invoke();
    }
    
    public void Stop()
    {
        bool wasPreRolling = isInPreRoll;
        if (preRollCoroutine != null)
        {
            StopCoroutine(preRollCoroutine);
            preRollCoroutine = null;
            isInPreRoll = false;
        }

        if (audioSource.isPlaying || audioSource.time > 0 || wasPreRolling)
        {
            audioSource.Stop();
            audioSource.time = 0f;
            OnPlaybackStopped?.Invoke();
            Debug.Log("Playback stopped");
        }
    }
    
    public void TogglePlayPause()
    {
        if (!isLoaded) return;
        
        if (audioSource.isPlaying || isInPreRoll)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    private IEnumerator PreRollThenPlay()
    {
        yield return new WaitForSeconds(audioOffset);
        isInPreRoll = false;
        preRollCoroutine = null;
        audioSource.time = 0f;
        audioSource.Play();
        Debug.Log("[BeatmapPlayer] Pre-roll complete, audio started");

        if (metronomeEnabled)
        {
            InitializeMetronome();
        }
    }
    
    public void Seek(float time)
    {
        if (!isLoaded || audioSource == null || audioSource.clip == null)
        {
            Debug.LogWarning("[BeatmapPlayer] Cannot seek - no beatmap loaded or audio clip missing!");
            return;
        }
        
        // Clamp to valid duration range
        time = Mathf.Clamp(time, 0f, Duration);
        
        // Check if this is a snipped beatmap
        bool audioHasCountdown = currentBeatmap?.metadata?.audio_includes_countdown ?? false;
        
        float audioTime = time;
        
        if (!audioHasCountdown)
        {
            // Imported: subtract audioOffset from time since CurrentTime has +audioOffset
            audioTime = time - audioOffset;
        }
        
        // Clamp audio time to actual clip length (with small epsilon to avoid edge cases)
        float maxAudioTime = audioSource.clip.length - 0.01f;
        audioTime = Mathf.Clamp(audioTime, 0f, maxAudioTime);
        
        audioSource.time = audioTime;
        
        Debug.Log($"[BeatmapPlayer] Seeked to beatmap time: {time:F2}s (audio time: {audioTime:F2}s)");
    }
    
    public void SeekNormalized(float normalizedTime)
    {
        if (!isLoaded)
        {
            Debug.LogWarning("[BeatmapPlayer] No beatmap loaded!");
            return;
        }
        
        // Clamp normalized time to [0, 1] to avoid invalid seeks
        normalizedTime = Mathf.Clamp01(normalizedTime);
        
        float time = normalizedTime * Duration;
        Seek(time);
    }
    
    public void SetVolume(float volume)
    {
        audioSource.volume = Mathf.Clamp01(volume);
    }
    
    public void ToggleMetronome()
    {
        metronomeEnabled = !metronomeEnabled;
        
        if (metronomeEnabled)
        {
            InitializeMetronome();
            Debug.Log("[BeatmapPlayer] Metronome enabled");
        }
        else
        {
            Debug.Log("[BeatmapPlayer] Metronome disabled");
        }
        
        UpdateMetronomeButtonVisual();
        
        // Toggle speed panel visibility (only in Learning mode)
        UpdateSpeedPanelVisibility();
    }
    
    /// <summary>
    /// Update speed panel visibility based on metronome state and game mode
    /// Speed panel only visible when: metronome is ON AND in Learning mode (or no mode manager)
    /// </summary>
    public void UpdateSpeedPanelVisibility()
    {
        if (speedPanel != null)
        {
            // Check if we're in learning mode (or no mode manager exists)
            bool inLearningMode = gameModeManager == null || gameModeManager.IsLearningMode;
            
            // Speed panel visible only if metronome enabled AND in learning mode
            speedPanel.SetActive(metronomeEnabled && inLearningMode);
        }
    }
    
    void InitializeMetronome()
    {
        if (!isLoaded || currentBeatmap?.metadata == null)
        {
            Debug.LogWarning("[BeatmapPlayer] Cannot initialize metronome - no beatmap loaded");
            return;
        }
        
        // Get BPM from beatmap metadata
        originalBpm = currentBeatmap.metadata.bpm_avg;
        if (originalBpm <= 0)
        {
            originalBpm = 120f; // Default fallback
            Debug.LogWarning($"[BeatmapPlayer] Invalid BPM, using default: {originalBpm}");
        }
        
        // Start with original BPM
        currentBpm = originalBpm;
        currentSpeed = 1.0f;
        
        // Update BPM input field
        if (bpmInputField != null)
        {
            bpmInputField.SetTextWithoutNotify(currentBpm.ToString("F0"));
        }
        
        // Update BPM display text
        UpdateBpmDisplay();
        
        // Calculate beat interval in seconds
        beatInterval = 60f / currentBpm;
        
        // Sync next beat to current time
        float currentTime = CurrentTime;
        nextBeatTime = Mathf.Ceil(currentTime / beatInterval) * beatInterval;
        
        Debug.Log($"[BeatmapPlayer] Metronome initialized: Original BPM={originalBpm}, Current BPM={currentBpm}, Interval={beatInterval:F3}s");
    }
    
    void PlayMetronomeClick()
    {
        if (metronomeAudioSource != null && metronomeClickSound != null)
        {
            metronomeAudioSource.PlayOneShot(metronomeClickSound);
        }
    }
    
    void UpdateMetronomeButtonVisual()
    {
        if (metronomeButton != null)
        {
            var colors = metronomeButton.colors;
            colors.normalColor = metronomeEnabled ? metronomeActiveColor : metronomeInactiveColor;
            metronomeButton.colors = colors;
        }
    }
    
    public void SetMetronomeVolume(float volume)
    {
        metronomeVolume = Mathf.Clamp01(volume);
        if (metronomeAudioSource != null)
        {
            metronomeAudioSource.volume = metronomeVolume;
        }
    }
    
    // === BPM CONTROL ===
    
    void OnBpmInputChanged(string text)
    {
        if (float.TryParse(text, out float newBpm))
        {
            SetBpm(newBpm);
        }
        else
        {
            // Invalid input, reset to current BPM
            if (bpmInputField != null)
            {
                bpmInputField.SetTextWithoutNotify(currentBpm.ToString("F0"));
            }
            Debug.LogWarning($"[BeatmapPlayer] Invalid BPM input: {text}");
        }
    }
    
    public void SetBpm(float newBpm)
    {
        // Clamp BPM to percentage range of original BPM
        float minBpm = originalBpm * (minBpmPercent / 100f);
        float maxBpm = originalBpm * (maxBpmPercent / 100f);
        newBpm = Mathf.Clamp(newBpm, minBpm, maxBpm);
        
        currentBpm = newBpm;
        
        // Calculate speed multiplier
        currentSpeed = currentBpm / originalBpm;
        
        // Update audio pitch
        if (audioSource != null)
        {
            audioSource.pitch = currentSpeed;
        }
        
        // Update metronome interval
        beatInterval = 60f / currentBpm;
        
        // Resync metronome to current time
        if (IsPlaying)
        {
            float currentTime = CurrentTime;
            nextBeatTime = Mathf.Ceil(currentTime / beatInterval) * beatInterval;
        }
        
        // Update UI
        if (bpmInputField != null)
        {
            bpmInputField.SetTextWithoutNotify(currentBpm.ToString("F0"));
        }
        UpdateBpmDisplay();
        
        // Notify listeners (NoteSpawner needs to adjust fall duration)
        OnSpeedChanged?.Invoke(currentSpeed);
        
        Debug.Log($"[BeatmapPlayer] BPM changed to {currentBpm:F0} (speed: {currentSpeed:F2}x, interval: {beatInterval:F3}s)");
    }
    
    public void ResetBpm()
    {
        SetBpm(originalBpm);
        Debug.Log($"[BeatmapPlayer] BPM reset to original: {originalBpm:F0}");
    }
    
    void UpdateBpmDisplay()
    {
        if (currentBpmText != null)
        {
            currentBpmText.text = $"{currentBpm:F0} BPM ({currentSpeed:F2}x)";
        }
    }
    
    public float GetOriginalBpm() => originalBpm;
    public float GetCurrentBpm() => currentBpm;
}
