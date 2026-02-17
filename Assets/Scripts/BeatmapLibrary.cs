using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

public class BeatmapLibrary : MonoBehaviour
{
    private List<BeatmapData> beatmaps = new List<BeatmapData>();
    private string beatmapsDirectory;
    
    public event Action<List<BeatmapData>> OnBeatmapsLoaded;
    public event Action<BeatmapData> OnBeatmapAdded;
    
    public List<BeatmapData> Beatmaps => beatmaps;
    
    void Awake()
    {
        beatmapsDirectory = Path.Combine(Application.persistentDataPath, "Beatmaps");
        
        if (!Directory.Exists(beatmapsDirectory))
        {
            Directory.CreateDirectory(beatmapsDirectory);
        }
        
        LoadAllBeatmaps();
    }
    
    void OnEnable()
    {
        // Subscribe to importer events
        BeatmapImporter importer = FindFirstObjectByType<BeatmapImporter>();
        if (importer != null)
        {
            importer.OnBeatmapImported += AddBeatmap;
        }
    }
    
    void OnDisable()
    {
        // Unsubscribe from importer events
        BeatmapImporter importer = FindFirstObjectByType<BeatmapImporter>();
        if (importer != null)
        {
            importer.OnBeatmapImported -= AddBeatmap;
        }
    }
    
    public void LoadAllBeatmaps()
    {
        beatmaps.Clear();
        
        if (!Directory.Exists(beatmapsDirectory))
        {
            UnityEngine.Debug.LogWarning("Beatmaps directory does not exist yet.");
            OnBeatmapsLoaded?.Invoke(beatmaps);
            return;
        }
        
        // Scan all subdirectories for beatmap.json files
        string[] beatmapFolders = Directory.GetDirectories(beatmapsDirectory);
        
        foreach (string folder in beatmapFolders)
        {
            string jsonPath = Path.Combine(folder, "beatmap.json");
            
            if (File.Exists(jsonPath))
            {
                BeatmapData beatmap = LoadBeatmapFromFolder(folder);
                if (beatmap != null)
                {
                    beatmaps.Add(beatmap);
                }
            }
        }
        
        UnityEngine.Debug.Log($"Loaded {beatmaps.Count} beatmaps from library.");
        OnBeatmapsLoaded?.Invoke(beatmaps);
    }
    
    BeatmapData LoadBeatmapFromFolder(string folderPath)
    {
        try
        {
            string jsonPath = Path.Combine(folderPath, "beatmap.json");
            string jsonContent = File.ReadAllText(jsonPath);
            
            var wrapper = JsonUtility.FromJson<BeatmapWrapper>(jsonContent);
            
            // Find MIDI and audio files in the folder
            string[] files = Directory.GetFiles(folderPath);
            string midiPath = null;
            string audioPath = null;
            
            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                
                if (ext == ".mid" || ext == ".midi" || ext == ".xml" || ext == ".musicxml")
                {
                    midiPath = file;
                }
                else if (ext == ".mp3" || ext == ".wav" || ext == ".ogg")
                {
                    audioPath = file;
                }
            }
            
            BeatmapData beatmapData = new BeatmapData
            {
                title = wrapper.metadata.title,
                midiFilePath = midiPath,
                audioFilePath = audioPath,
                jsonFilePath = jsonPath,
                metadata = wrapper.metadata,
                beatmap = wrapper.beatmap
            };
            
            return beatmapData;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error loading beatmap from {folderPath}: {e.Message}");
            return null;
        }
    }
    
    public void AddBeatmap(BeatmapData beatmap)
    {
        if (beatmap != null && !beatmaps.Contains(beatmap))
        {
            beatmaps.Add(beatmap);
            UnityEngine.Debug.Log($"Added beatmap to library: {beatmap.title}");
            OnBeatmapAdded?.Invoke(beatmap);
        }
    }
    
    public BeatmapData GetBeatmapByTitle(string title)
    {
        return beatmaps.Find(b => b.title == title);
    }
    
    public void DeleteBeatmap(BeatmapData beatmap)
    {
        if (beatmap != null && beatmaps.Contains(beatmap))
        {
            // Delete the folder
            string folderPath = Path.GetDirectoryName(beatmap.jsonFilePath);
            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, true);
            }
            
            beatmaps.Remove(beatmap);
            OnBeatmapsLoaded?.Invoke(beatmaps);
        }
    }
    
    [Serializable]
    private class BeatmapWrapper
    {
        public BeatmapData.BeatmapMetadata metadata;
        public List<BeatmapNote> beatmap;
    }
}
