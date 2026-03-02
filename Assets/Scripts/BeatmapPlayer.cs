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
    public bool IsPlaying => audioSource != null && audioSource.isPlaying;
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
            
            // Imported: CurrentTime = audio.time + 3
            // Because we added 3 to all notes, we add 3 to CurrentTime too
            // offset=13: audio 10→13 = CurrentTime 13→16
            // offset=0: audio 0→3 = CurrentTime 3→6
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
            audioSource.time = 0f;
            audioSource.Play();
            Debug.Log($"[BeatmapPlayer] Playing snipped beatmap from start: {currentBeatmap.title}");
        }
        else
        {
            // Imported: audio starts at (offset - 3) for ready timer
            float drumOffset = currentBeatmap?.metadata?.drum_start_offset ?? 0f;
            float startPosition = Mathf.Max(0f, drumOffset - audioOffset);
            
            audioSource.time = startPosition;
            audioSource.Play();
            
            Debug.Log($"[BeatmapPlayer] Playing: {currentBeatmap.title}");
            Debug.Log($"[BeatmapPlayer] Audio starting at: {startPosition:F1}s");
            Debug.Log($"[BeatmapPlayer] First note at beatmap time: {drumOffset + audioOffset:F1}s");
            Debug.Log($"[BeatmapPlayer] Ready timer: {startPosition:F1}s → {drumOffset:F1}s (3 seconds)");
        }
        
        OnPlaybackStarted?.Invoke();
        
        if (metronomeEnabled)
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
        
        Debug.Log("[BeatmapPlayer] Paused");
        audioSource.Pause();
        OnPlaybackPaused?.Invoke();
    }
    
    public void Stop()
    {
        if (audioSource.isPlaying || audioSource.time > 0)
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
        
        if (audioSource.isPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }
    
    public void Seek(float time)
    {
        if (!isLoaded)
        {
            Debug.LogWarning("No beatmap loaded!");
            return;
        }
        
        time = Mathf.Clamp(time, 0f, Duration);
        
        // Check if this is a snipped beatmap
        bool audioHasCountdown = currentBeatmap?.metadata?.audio_includes_countdown ?? false;
        
        if (!audioHasCountdown)
        {
            // Imported: subtract 3 from time since CurrentTime has +3 offset
            time -= audioOffset;
        }
        
        audioSource.time = time;
        
        Debug.Log($"Seeked to: {time:F2}s");
    }
    
    public void SeekNormalized(float normalizedTime)
    {
        if (!isLoaded)
        {
            Debug.LogWarning("No beatmap loaded!");
            return;
        }
        
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
        // Toggle speed panel visibility
        if (speedPanel != null)
        {
            speedPanel.SetActive(metronomeEnabled);
        }        
        // Toggle speed panel visibility
        if (speedPanel != null)
        {
            speedPanel.SetActive(metronomeEnabled);
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
