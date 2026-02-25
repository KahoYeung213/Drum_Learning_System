using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.Collections;

public class BeatmapPlayer : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Slider volumeSlider;
    [SerializeField][Range(0f, 1f)] private float defaultVolume = 1.0f;
    
    [Header("Metronome")]
    [SerializeField] private Button metronomeButton;
    [SerializeField] private AudioClip metronomeClickSound;
    [SerializeField] private AudioSource metronomeAudioSource;
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
    
    // Events
    public event Action<BeatmapData> OnBeatmapLoaded;
    public event Action OnBeatmapUnloaded;
    public event Action OnPlaybackStarted;
    public event Action OnPlaybackPaused;
    public event Action OnPlaybackStopped;
    
    // Properties
    public BeatmapData CurrentBeatmap => currentBeatmap;
    public bool IsLoaded => isLoaded;
    public bool IsPlaying => audioSource != null && audioSource.isPlaying;
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
        
        // Set initial volume
        audioSource.volume = defaultVolume;
        
        // Create metronome audio source if not assigned
        if (metronomeAudioSource == null)
        {
            GameObject metronomeObj = new GameObject("Metronome AudioSource");
            metronomeObj.transform.SetParent(transform);
            metronomeAudioSource = metronomeObj.AddComponent<AudioSource>();
            metronomeAudioSource.playOnAwake = false;
            metronomeAudioSource.volume = metronomeVolume;
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
    }
    
    void InitializeMetronome()
    {
        if (!isLoaded || currentBeatmap?.metadata == null)
        {
            Debug.LogWarning("[BeatmapPlayer] Cannot initialize metronome - no beatmap loaded");
            return;
        }
        
        // Get BPM from beatmap metadata
        float bpm = currentBeatmap.metadata.bpm_avg;
        if (bpm <= 0)
        {
            bpm = 120f; // Default fallback
            Debug.LogWarning($"[BeatmapPlayer] Invalid BPM, using default: {bpm}");
        }
        
        // Calculate beat interval in seconds
        beatInterval = 60f / bpm;
        
        // Sync next beat to current time
        float currentTime = CurrentTime;
        nextBeatTime = Mathf.Ceil(currentTime / beatInterval) * beatInterval;
        
        Debug.Log($"[BeatmapPlayer] Metronome initialized: BPM={bpm}, Interval={beatInterval:F3}s");
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
}
