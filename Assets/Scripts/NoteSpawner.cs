using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NoteSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BeatmapPlayer beatmapPlayer;
    [SerializeField] private GameObject notePrefab;
    [SerializeField] private MidiHit midiHit; // Auto-sync with MidiHit mappings
    
    [Header("Hitzones")]
    [SerializeField] private Transform[] hitzones; // Assign your hitzone GameObjects here
    [SerializeField] private bool autoSyncWithMidiHit = true; // Automatically build from MidiHit
    
    [Header("MIDI to Lane Mapping")]
    [Tooltip("Maps MIDI note numbers to lane indices. MUST match Python script's PITCH_TO_LANE dictionary!")]
    [SerializeField] private MidiToLaneMapping[] midiToLaneMap = new MidiToLaneMapping[]
    {
        // This MUST match the PITCH_TO_LANE dict in music_xml_to_beatmap.py
        new MidiToLaneMapping { midiNote = 36, lane = 0 }, // Bass Drum
        new MidiToLaneMapping { midiNote = 49, lane = 1 }, // Crash
        // Hi-Hat - ALL variations go to lane 2
        new MidiToLaneMapping { midiNote = 21, lane = 2 }, // Hi-Hat (alt)
        new MidiToLaneMapping { midiNote = 23, lane = 2 }, // Hi-Hat (alt)
        new MidiToLaneMapping { midiNote = 42, lane = 2 }, // Hi-Hat Closed
        new MidiToLaneMapping { midiNote = 44, lane = 2 }, // Hi-Hat Pedal
        new MidiToLaneMapping { midiNote = 46, lane = 2 }, // Hi-Hat Open
      
        // Snare - variations go to lane 3
        new MidiToLaneMapping { midiNote = 38, lane = 3 }, // Snare
        new MidiToLaneMapping { midiNote = 40, lane = 3 }, // Snare (rim shot)
        // Tom 1 - lane 4
        new MidiToLaneMapping { midiNote = 48, lane = 4 }, // Tom 1
        new MidiToLaneMapping { midiNote = 50, lane = 4 }, // Tom 1 (alt)

        // Tom 2 - lane 5
        new MidiToLaneMapping { midiNote = 45, lane = 5 }, // Tom 2
        new MidiToLaneMapping { midiNote = 47, lane = 5 }, // Tom 2 (alt)
        // Tom 3 - lane 6
        new MidiToLaneMapping { midiNote = 43, lane = 6 }, // Tom 3
        new MidiToLaneMapping { midiNote = 58, lane = 6 }, // Tom 3 (alt)
        // Ride - lane 7
        new MidiToLaneMapping { midiNote = 51, lane = 7 }, // Ride
    };
    
    [System.Serializable]
    public class MidiToLaneMapping
    {
        public int midiNote;
        public int lane;
    }
    
    [Header("Spawn Settings")]
    [SerializeField] private float spawnHeight = 100f; // Height above hitzone
    [SerializeField] private float fallDuration = 2f; // How long notes take to fall
    
    private List<BeatmapNote> notesToSpawn = new List<BeatmapNote>();
    private int nextNoteIndex = 0;
    private float playbackStartTime = 0f;
    private bool isPlaying = false;
    
    void Start()
    {
        // Find BeatmapPlayer if not assigned
        if (beatmapPlayer == null)
        {
            beatmapPlayer = FindFirstObjectByType<BeatmapPlayer>();
        }
        
        // Find MidiHit if not assigned
        if (midiHit == null && autoSyncWithMidiHit)
        {
            midiHit = FindFirstObjectByType<MidiHit>();
        }
        
        // Auto-sync hitzones with MidiHit mappings
        if (autoSyncWithMidiHit && midiHit != null)
        {
            BuildHitzonesFromMidiHit();
        }
        
        if (beatmapPlayer != null)
        {
            beatmapPlayer.OnBeatmapLoaded += OnBeatmapLoaded;
            beatmapPlayer.OnPlaybackStarted += OnPlaybackStarted;
            beatmapPlayer.OnPlaybackPaused += OnPlaybackPaused;
            beatmapPlayer.OnPlaybackStopped += OnPlaybackStopped;
        }
        
        ValidateSetup();
    }
    
    void BuildHitzonesFromMidiHit()
    {
        if (midiHit == null || midiHit.mappings == null || midiToLaneMap == null)
        {
            Debug.LogWarning("[NoteSpawner] Cannot build hitzones - MidiHit or mappings not available");
            return;
        }
        
        // Find the maximum lane number to size the array
        int maxLane = 0;
        foreach (var mapping in midiToLaneMap)
        {
            if (mapping.lane > maxLane) maxLane = mapping.lane;
        }
        
        // Create hitzones array
        hitzones = new Transform[maxLane + 1];
        
        // Map each MidiHit mapping to the corresponding lane
        foreach (var midiMapping in midiHit.mappings)
        {
            // Find which lane this MIDI note corresponds to
            foreach (var laneMapping in midiToLaneMap)
            {
                if (laneMapping.midiNote == midiMapping.midiNote)
                {
                    if (midiMapping.hitZone != null)
                    {
                        hitzones[laneMapping.lane] = midiMapping.hitZone.transform;
                        Debug.Log($"[NoteSpawner] Mapped MIDI {midiMapping.midiNote} → Lane {laneMapping.lane} → {midiMapping.hitZone.name}");
                    }
                    break;
                }
            }
        }
        
        Debug.Log($"[NoteSpawner] Auto-synced {hitzones.Length} hitzones from MidiHit");
    }
    
    void OnDestroy()
    {
        if (beatmapPlayer != null)
        {
            beatmapPlayer.OnBeatmapLoaded -= OnBeatmapLoaded;
            beatmapPlayer.OnPlaybackStarted -= OnPlaybackStarted;
            beatmapPlayer.OnPlaybackPaused -= OnPlaybackPaused;
            beatmapPlayer.OnPlaybackStopped -= OnPlaybackStopped;
        }
    }
    
    void ValidateSetup()
    {
        if (notePrefab == null)
        {
            Debug.LogError("[NoteSpawner] Note prefab is not assigned!");
        }
        
        if (hitzones == null || hitzones.Length == 0)
        {
            Debug.LogError("[NoteSpawner] No hitzones assigned!");
            if (autoSyncWithMidiHit)
            {
                Debug.LogError("[NoteSpawner] Auto-sync is enabled but failed. Check that MidiHit component exists with mappings.");
            }
        }
        else
        {
            Debug.Log($"[NoteSpawner] ===== Hitzone Setup =====");
            for (int i = 0; i < hitzones.Length; i++)
            {
                if (hitzones[i] == null)
                {
                    Debug.LogWarning($"[NoteSpawner] Lane {i}: NOT ASSIGNED");
                }
                else
                {
                    string midiNote = GetMidiNoteForLane(i);
                    Debug.Log($"[NoteSpawner] Lane {i}: {hitzones[i].name} at {hitzones[i].position} {midiNote}");
                }
            }
            Debug.Log($"[NoteSpawner] ==========================");
        }
    }
    
    string GetMidiNoteForLane(int lane)
    {
        foreach (var mapping in midiToLaneMap)
        {
            if (mapping.lane == lane)
            {
                return $"(MIDI {mapping.midiNote})";
            }
        }
        return "";
    }
    
    void OnBeatmapLoaded(BeatmapData beatmap)
    {
        Debug.Log($"[NoteSpawner] Beatmap loaded: {beatmap.title}");
        
        if (beatmap.beatmap == null || beatmap.beatmap.Count == 0)
        {
            Debug.LogWarning("[NoteSpawner] Beatmap has no notes!");
            notesToSpawn.Clear();
            return;
        }
        
        // Load and sort notes by spawn time
        notesToSpawn = beatmap.beatmap.OrderBy(n => n.spawnTime).ToList();
        nextNoteIndex = 0;
        
        Debug.Log($"[NoteSpawner] Loaded {notesToSpawn.Count} notes");
        Debug.Log($"[NoteSpawner] First note: lane={notesToSpawn[0].lane}, spawnTime={notesToSpawn[0].spawnTime}s, hitTime={notesToSpawn[0].time}s");
    }
    
    void OnPlaybackStarted()
    {
        Debug.Log("[NoteSpawner] Playback started");
        playbackStartTime = Time.time;
        nextNoteIndex = 0;
        isPlaying = true;
    }
    
    void OnPlaybackPaused()
    {
        Debug.Log("[NoteSpawner] Playback paused");
        isPlaying = false;
    }
    
    void OnPlaybackStopped()
    {
        Debug.Log("[NoteSpawner] Playback stopped");
        isPlaying = false;
        nextNoteIndex = 0;
        
        // Clean up any remaining notes
        CleanupAllNotes();
    }
    
    void Update()
    {
        if (!isPlaying || notesToSpawn.Count == 0) return;
        
        float currentTime = Time.time - playbackStartTime;
        
        // Spawn notes that are due
        while (nextNoteIndex < notesToSpawn.Count && notesToSpawn[nextNoteIndex].spawnTime <= currentTime)
        {
            SpawnNote(notesToSpawn[nextNoteIndex]);
            nextNoteIndex++;
        }
    }
    
    void SpawnNote(BeatmapNote noteData)
    {
        // Validate lane
        if (noteData.lane < 0 || noteData.lane >= hitzones.Length)
        {
            Debug.LogWarning($"[NoteSpawner] Invalid lane {noteData.lane}. Available lanes: 0-{hitzones.Length - 1}");
            return;
        }
        
        Transform hitzone = hitzones[noteData.lane];
        if (hitzone == null)
        {
            string midiNote = GetMidiNoteForLane(noteData.lane);
            Debug.LogWarning($"[NoteSpawner] Hitzone for lane {noteData.lane} {midiNote} is null!");
            return;
        }
        
        if (notePrefab == null)
        {
            Debug.LogError("[NoteSpawner] Note prefab is null!");
            return;
        }
        
        // Calculate spawn position: directly above the hitzone
        Vector3 spawnPosition = hitzone.position + Vector3.up * spawnHeight;
        
        // Instantiate note
        GameObject noteObject = Instantiate(notePrefab, spawnPosition, Quaternion.identity);
        string midiInfo = GetMidiNoteForLane(noteData.lane);
        noteObject.name = $"Note_Lane{noteData.lane}{midiInfo}_Time{noteData.time:F2}";
        
        // Add FallingNote component if not already on prefab
        FallingNote fallingNote = noteObject.GetComponent<FallingNote>();
        if (fallingNote == null)
        {
            fallingNote = noteObject.AddComponent<FallingNote>();
        }
        
        // Initialize the falling note
        float fallTime = noteData.time - (Time.time - playbackStartTime);
        if (fallTime < 0.1f) fallTime = 0.1f; // Minimum fall time
        
        fallingNote.Initialize(hitzone.position, fallTime, hitzone.gameObject, noteData.time);
        
        Debug.Log($"[NoteSpawner] Spawned note at lane {noteData.lane} {midiInfo}, position {spawnPosition}, will hit at {noteData.time:F2}s");
    }
    
    void CleanupAllNotes()
    {
        FallingNote[] allNotes = FindObjectsByType<FallingNote>(FindObjectsSortMode.None);
        foreach (FallingNote note in allNotes)
        {
            Destroy(note.gameObject);
        }
        Debug.Log($"[NoteSpawner] Cleaned up {allNotes.Length} notes");
    }
    
    // Public methods for runtime configuration
    public void SetSpawnHeight(float height)
    {
        spawnHeight = height;
        Debug.Log($"[NoteSpawner] Spawn height set to {height}");
    }
    
    public void SetFallDuration(float duration)
    {
        fallDuration = duration;
        Debug.Log($"[NoteSpawner] Fall duration set to {duration}s");
    }
    
    // Manually trigger re-sync with MidiHit
    public void RebuildHitzonesFromMidiHit()
    {
        if (midiHit == null)
        {
            midiHit = FindFirstObjectByType<MidiHit>();
        }
        
        if (midiHit != null)
        {
            BuildHitzonesFromMidiHit();
            ValidateSetup();
        }
        else
        {
            Debug.LogError("[NoteSpawner] Cannot rebuild - MidiHit component not found!");
        }
    }
    
    // Helper method to add a hitzone at runtime
    public void AddHitzone(Transform hitzone)
    {
        if (hitzone == null)
        {
            Debug.LogWarning("[NoteSpawner] Cannot add null hitzone!");
            return;
        }
        
        List<Transform> hitzoneList = new List<Transform>(hitzones ?? new Transform[0]);
        hitzoneList.Add(hitzone);
        hitzones = hitzoneList.ToArray();
        
        Debug.Log($"[NoteSpawner] Added hitzone: {hitzone.name} at lane {hitzoneList.Count - 1}");
    }
    
    // Helper method to set all hitzones at once
    public void SetHitzones(Transform[] newHitzones)
    {
        hitzones = newHitzones;
        ValidateSetup();
    }
    
    // Get current lane count
    public int GetLaneCount()
    {
        return hitzones != null ? hitzones.Length : 0;
    }
    
    // Check if a specific lane is configured
    public bool IsLaneConfigured(int lane)
    {
        return hitzones != null && lane >= 0 && lane < hitzones.Length && hitzones[lane] != null;
    }
}
