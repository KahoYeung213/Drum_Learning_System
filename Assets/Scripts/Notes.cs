using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.InputSystem; // Add this for new Input System

[System.Serializable]
public class NoteData
{
    public int lane;
    public float time;
    public float spawnTime;
    public float velocity;
}

[System.Serializable]
public class NotesContainer
{
    public List<NoteData> beatmap; // Changed from 'notes' to 'beatmap' to match your JSON
}

public class Notes : MonoBehaviour
{
    [Header("Note System")]
    public GameObject notePrefab; // Assign a cube/sphere prefab
    public Transform[] laneSpawnPoints; // Where notes spawn for each lane
    public Transform[] laneHitTargets; // Where notes should hit (your drum objects)
    public float fallSpeed = 5f;
    public float noteLifetime = 10f; // Auto-destroy after this time
    
    [Header("JSON File")]
    public TextAsset jsonFile; // Drag your JSON file here in inspector
    
    private List<NoteData> songNotes = new List<NoteData>();
    private float songStartTime;
    private int nextNoteIndex = 0;
    private bool songPlaying = false;

    // ...existing code...
void Start()
{
    LoadNotesFromJSON();
    // Auto-start so you don't rely on input
    StartSong();
}
public void StartSong()
{
    songStartTime = Time.time;
    nextNoteIndex = 0;
    songPlaying = true;
    Debug.Log("Song started");
}


void LoadNotesFromJSON()
{
    if (jsonFile == null)
    {
        Debug.LogError("No JSON file assigned!");
        return;
    }

    try
    {
        var container = JsonUtility.FromJson<BeatmapContainer>(jsonFile.text);
        if (container == null || container.beatmap == null)
        {
            Debug.LogError("JSON parse returned null beatmap");
            return;
        }

        songNotes.Clear();

        // Parse lane/time/spawnTime/velocity directly from JSON
        for (int i = 0; i < container.beatmap.Count; i++)
        {
            var src = container.beatmap[i];
            NoteData note = new NoteData
            {
                lane = Mathf.Clamp(src.lane, 0, laneSpawnPoints.Length - 1),
                time = src.time,
                spawnTime = src.spawnTime,
                velocity = src.velocity
            };
            songNotes.Add(note);
        }

        // Ensure notes are sorted by spawnTime
        songNotes.Sort((a, b) => a.spawnTime.CompareTo(b.spawnTime));

        Debug.Log($"Loaded {songNotes.Count} notes from JSON");
        if (songNotes.Count > 0)
            Debug.Log($"First note: lane={songNotes[0].lane}, spawnTime={songNotes[0].spawnTime}, hitTime={songNotes[0].time}, velocity={songNotes[0].velocity}");
        if (songNotes.Count > 1)
            Debug.Log($"Second note: lane={songNotes[1].lane}, spawnTime={songNotes[1].spawnTime}, hitTime={songNotes[1].time}, velocity={songNotes[1].velocity}");
        if (songNotes.Count > 5)
            Debug.Log($"Sixth note: lane={songNotes[5].lane}, spawnTime={songNotes[5].spawnTime}, hitTime={songNotes[5].time}, velocity={songNotes[5].velocity}");
    }
    catch (Exception e)
    {
        Debug.LogError($"Failed to parse JSON: {e.Message}");
    }
}

void SpawnNotesAtCorrectTime()
{
    if (nextNoteIndex >= songNotes.Count) return;

    float elapsedTime = Time.time - songStartTime;
    Debug.Log($"Spawn loop tick: elapsed={elapsedTime:F2}, nextIndex={nextNoteIndex}, nextSpawn={songNotes[nextNoteIndex].spawnTime:F2}");

    // Spawn while next note is due
    while (nextNoteIndex < songNotes.Count && songNotes[nextNoteIndex].spawnTime <= elapsedTime)
    {
        SpawnNote(songNotes[nextNoteIndex]);
        nextNoteIndex++;
    }
}

void SpawnNote(NoteData noteData)
{
    Debug.Log($"Attempting to spawn note: lane={noteData.lane}, spawnTime={noteData.spawnTime}, elapsed={Time.time - songStartTime:F2}");

    if (noteData.lane < 0 || noteData.lane >= laneSpawnPoints.Length)
    {
        Debug.LogError($"Invalid lane {noteData.lane}. laneSpawnPoints length={laneSpawnPoints.Length}");
        return;
    }
    if (laneSpawnPoints[noteData.lane] == null)
    {
        Debug.LogError($"Lane spawn point {noteData.lane} is null");
        return;
    }
    if (laneHitTargets == null || noteData.lane >= laneHitTargets.Length || laneHitTargets[noteData.lane] == null)
    {
        Debug.LogError($"Lane hit target {noteData.lane} is not assigned");
        return;
    }
    if (notePrefab == null)
    {
        Debug.LogError("Note prefab is not assigned");
        return;
    }

    GameObject note = Instantiate(notePrefab, laneSpawnPoints[noteData.lane].position, Quaternion.identity);
    FallingNote fallingNote = note.AddComponent<FallingNote>();
    
    // Calculate fall duration based on time until hit
    float fallDuration = noteData.time - (Time.time - songStartTime);
    if (fallDuration <= 0) fallDuration = 0.1f; // Minimum duration
    
    fallingNote.Initialize(
        laneHitTargets[noteData.lane].position, // target position
        fallDuration, // duration
        laneHitTargets[noteData.lane].gameObject, // drum mesh
        noteData.time // hit time
    );

    Debug.Log($"Spawned note lane {noteData.lane} at {laneSpawnPoints[noteData.lane].position}");
}
// ...existing code...

[System.Serializable]
public class BeatmapNote
{
    public int lane;
    public float time;
    public float spawnTime;
    public float velocity;
}

[System.Serializable]
public class BeatmapContainer
{
    public List<BeatmapNote> beatmap;
}}