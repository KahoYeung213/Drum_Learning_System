using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

public class BeatmapLibrary : MonoBehaviour
{
    private List<BeatmapData> beatmaps = new List<BeatmapData>();
    private string beatmapsDirectory;
    private string defaultBeatmapsSourceDirectory;
    
    public event Action<List<BeatmapData>> OnBeatmapsLoaded;
    public event Action<BeatmapData> OnBeatmapAdded;
    public event Action<BeatmapData> OnBeatmapDeleted;
    
    public List<BeatmapData> Beatmaps => beatmaps;
    
    void Awake()
    {
        beatmapsDirectory = Path.Combine(Application.persistentDataPath, "Beatmaps");
        defaultBeatmapsSourceDirectory = Path.Combine(Application.streamingAssetsPath, "Beatmaps");
        
        if (!Directory.Exists(beatmapsDirectory))
        {
            Directory.CreateDirectory(beatmapsDirectory);
        }

        SeedDefaultBeatmapsIfNeeded();
        
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

    private void SeedDefaultBeatmapsIfNeeded()
    {
        if (!Directory.Exists(defaultBeatmapsSourceDirectory))
        {
            return;
        }

        string[] sourceFolders = Directory.GetDirectories(defaultBeatmapsSourceDirectory, "*", SearchOption.AllDirectories);
        if (sourceFolders.Length == 0)
        {
            return;
        }

        foreach (string sourceFolder in sourceFolders)
        {
            string relativeFolder = GetRelativeFolderPath(defaultBeatmapsSourceDirectory, sourceFolder);
            string destinationFolder = Path.Combine(beatmapsDirectory, relativeFolder);

            if (Directory.Exists(destinationFolder) && File.Exists(Path.Combine(destinationFolder, "beatmap.json")))
            {
                continue;
            }

            CopyDirectory(sourceFolder, destinationFolder);
        }

        string rootBeatmapJson = Path.Combine(defaultBeatmapsSourceDirectory, "beatmap.json");
        if (File.Exists(rootBeatmapJson) && !File.Exists(Path.Combine(beatmapsDirectory, "beatmap.json")))
        {
            File.Copy(rootBeatmapJson, Path.Combine(beatmapsDirectory, "beatmap.json"), true);
        }
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        foreach (string filePath in Directory.GetFiles(sourceDirectory))
        {
            string destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(filePath));
            File.Copy(filePath, destinationPath, true);
        }

        foreach (string nestedDirectory in Directory.GetDirectories(sourceDirectory))
        {
            string nestedDestination = Path.Combine(destinationDirectory, Path.GetFileName(nestedDirectory));
            CopyDirectory(nestedDirectory, nestedDestination);
        }
    }

    private static string GetRelativeFolderPath(string rootDirectory, string fullDirectoryPath)
    {
        string normalizedRoot = rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (fullDirectoryPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            string relative = fullDirectoryPath.Substring(normalizedRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relative;
        }

        return Path.GetFileName(fullDirectoryPath);
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
        if (beatmap == null)
        {
            UnityEngine.Debug.LogWarning("[BeatmapLibrary] Cannot delete null beatmap!");
            return;
        }
        
        if (!beatmaps.Contains(beatmap))
        {
            UnityEngine.Debug.LogWarning($"[BeatmapLibrary] Beatmap '{beatmap.title}' not found in library!");
            return;
        }
        
        try
        {
            // Delete the folder
            string folderPath = Path.GetDirectoryName(beatmap.jsonFilePath);
            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, true);
                UnityEngine.Debug.Log($"[BeatmapLibrary] Deleted folder: {folderPath}");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[BeatmapLibrary] Folder not found: {folderPath}");
            }
            
            // Remove from list
            beatmaps.Remove(beatmap);
            UnityEngine.Debug.Log($"[BeatmapLibrary] Removed beatmap from library: {beatmap.title}");
            
            // Notify listeners
            OnBeatmapDeleted?.Invoke(beatmap);
            OnBeatmapsLoaded?.Invoke(beatmaps);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[BeatmapLibrary] Error deleting beatmap '{beatmap.title}': {e.Message}");
            throw;
        }
    }
    
    public void DeleteAllBeatmaps()
    {
        UnityEngine.Debug.Log($"[BeatmapLibrary] Deleting all {beatmaps.Count} beatmaps...");
        
        // Create a copy of the list to avoid modification during iteration
        List<BeatmapData> beatmapsToDelete = new List<BeatmapData>(beatmaps);
        
        int successCount = 0;
        int failCount = 0;
        
        foreach (var beatmap in beatmapsToDelete)
        {
            try
            {
                string folderPath = Path.GetDirectoryName(beatmap.jsonFilePath);
                if (Directory.Exists(folderPath))
                {
                    Directory.Delete(folderPath, true);
                    successCount++;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[BeatmapLibrary] Failed to delete '{beatmap.title}': {e.Message}");
                failCount++;
            }
        }
        
        beatmaps.Clear();
        UnityEngine.Debug.Log($"[BeatmapLibrary] Deleted {successCount} beatmaps, {failCount} failed");
        
        OnBeatmapsLoaded?.Invoke(beatmaps);
    }
    
    [Serializable]
    private class BeatmapWrapper
    {
        public BeatmapData.BeatmapMetadata metadata;
        public List<BeatmapNote> beatmap;
    }
}
