using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;

public class BeatmapPlayer : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    
    private BeatmapData currentBeatmap;
    private bool isLoaded = false;
    
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
    public float CurrentTime => audioSource != null ? audioSource.time : 0f;
    public float Duration => audioSource != null && audioSource.clip != null ? audioSource.clip.length : 0f;
    public float Progress => Duration > 0 ? CurrentTime / Duration : 0f;
    
    void Awake()
    {
        // Create audio source if not assigned
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
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
        
        Debug.Log($"[BeatmapPlayer] Playing: {currentBeatmap.title}");
        audioSource.Play();
        OnPlaybackStarted?.Invoke();
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
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
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
}
