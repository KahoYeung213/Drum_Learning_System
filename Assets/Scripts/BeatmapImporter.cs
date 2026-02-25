using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SFB;
using System;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class BeatmapImporter : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button importButton;
    [SerializeField] private TMP_InputField drumOffsetInput; // Optional: Input field for drum start offset
    
    [Header("Beatmap Settings")]
    [SerializeField] private float spawnLeadTime = 2.0f;
    [SerializeField] private float audioOffset = 3.0f; // Countdown time before first note
    [SerializeField] private float defaultDrumStartOffset = 0.0f; // Default offset in seconds
    
    private string pythonScriptPath;
    private string beatmapsDirectory;
    private string tempMidiPath;
    private string tempAudioPath;
    
    public event Action<BeatmapData> OnBeatmapImported;
    
    void Start()
    {
        // Set up paths
        pythonScriptPath = Path.Combine(Application.dataPath, "Scripts", "MidiParser", "music_xml_to_beatmap.py");
        beatmapsDirectory = Path.Combine(Application.persistentDataPath, "Beatmaps");
        
        // Create directory if it doesn't exist
        if (!Directory.Exists(beatmapsDirectory))
        {
            Directory.CreateDirectory(beatmapsDirectory);
        }
        
        if (importButton != null)
        {
            importButton.onClick.AddListener(OnImportButtonClicked);
        }
    }
    
    void OnImportButtonClicked()
    {
        UnityEngine.Debug.LogWarning("[BeatmapImporter] Import button clicked - Opening file browser...");
        
        // First, select MIDI file
        var midiExtensions = new[] {
            new ExtensionFilter("MIDI/XML Files", "mid", "midi", "xml", "musicxml"),
            new ExtensionFilter("All Files", "*")
        };
        
        StandaloneFileBrowser.OpenFilePanelAsync(
            "Select MIDI/MusicXML File", 
            "", 
            midiExtensions, 
            false,
            (string[] paths) => {
                if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
                {
                    tempMidiPath = paths[0];
                    SelectAudioFile();
                }
            }
        );
    }
    
    void SelectAudioFile()
    {
        var audioExtensions = new[] {
            new ExtensionFilter("Audio Files", "mp3", "wav", "ogg"),
            new ExtensionFilter("All Files", "*")
        };
        
        StandaloneFileBrowser.OpenFilePanelAsync(
            "Select Audio File", 
            "", 
            audioExtensions, 
            false,
            (string[] paths) => {
                if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
                {
                    tempAudioPath = paths[0];
                    StartCoroutine(ProcessBeatmap());
                }
                else
                {
                    UnityEngine.Debug.LogWarning("No audio file selected. Import cancelled.");
                    tempMidiPath = null;
                }
            }
        );
    }
    
    IEnumerator ProcessBeatmap()
    {
        UnityEngine.Debug.Log($"Processing MIDI: {tempMidiPath}");
        UnityEngine.Debug.Log($"With audio: {tempAudioPath}");
        
        // Get drum start offset from input field or use default
        float drumStartOffset = defaultDrumStartOffset;
        if (drumOffsetInput != null && !string.IsNullOrEmpty(drumOffsetInput.text))
        {
            if (float.TryParse(drumOffsetInput.text, out float parsedOffset))
            {
                drumStartOffset = parsedOffset;
                UnityEngine.Debug.Log($"Using drum start offset from input: {drumStartOffset}s");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"Invalid offset input '{drumOffsetInput.text}', using default: {defaultDrumStartOffset}s");
            }
        }
        
        // Generate unique folder for this beatmap
        string beatmapName = Path.GetFileNameWithoutExtension(tempMidiPath);
        string beatmapFolder = Path.Combine(beatmapsDirectory, beatmapName);
        
        // If folder exists, add timestamp
        if (Directory.Exists(beatmapFolder))
        {
            beatmapName = beatmapName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            beatmapFolder = Path.Combine(beatmapsDirectory, beatmapName);
        }
        
        Directory.CreateDirectory(beatmapFolder);
        
        // Define file paths
        string jsonPath = Path.Combine(beatmapFolder, "beatmap.json");
        string midiCopyPath = Path.Combine(beatmapFolder, Path.GetFileName(tempMidiPath));
        string audioCopyPath = Path.Combine(beatmapFolder, Path.GetFileName(tempAudioPath));
        
        // Copy files to beatmap folder
        File.Copy(tempMidiPath, midiCopyPath, true);
        File.Copy(tempAudioPath, audioCopyPath, true);
        
        // Parse MIDI to JSON with offset
        yield return StartCoroutine(RunPythonParser(midiCopyPath, jsonPath, drumStartOffset));
        
        // Load and create BeatmapData
        if (File.Exists(jsonPath))
        {
            BeatmapData beatmapData = LoadBeatmapData(jsonPath, midiCopyPath, audioCopyPath);
            
            if (beatmapData != null)
            {
                UnityEngine.Debug.Log($"Successfully imported beatmap: {beatmapData.title}");
                OnBeatmapImported?.Invoke(beatmapData);
            }
        }
        else
        {
            UnityEngine.Debug.LogError("Failed to generate beatmap JSON file.");
        }
        
        // Clear temp paths
        tempMidiPath = null;
        tempAudioPath = null;
    }
    
    IEnumerator RunPythonParser(string inputPath, string outputPath, float drumStartOffset)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "python",
            // Pass all parameters: input, output, spawn_lead, audio_offset, drum_start_offset
            Arguments = $"\"{pythonScriptPath}\" \"{inputPath}\" \"{outputPath}\" {spawnLeadTime} {audioOffset} {drumStartOffset}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        UnityEngine.Debug.Log($"Running: {startInfo.FileName} {startInfo.Arguments}");
        UnityEngine.Debug.Log($"Drum Start Offset: {drumStartOffset}s (song will skip to this position on playback)");
        
        Process process = new Process { StartInfo = startInfo };
        process.Start();
        
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        
        while (!process.HasExited)
        {
            yield return null;
        }
        
        if (!string.IsNullOrEmpty(output))
            UnityEngine.Debug.Log("Python Output: " + output);
        
        if (!string.IsNullOrEmpty(error))
            UnityEngine.Debug.LogError("Python Error: " + error);
        
        if (process.ExitCode != 0)
        {
            UnityEngine.Debug.LogError($"Python script failed with exit code: {process.ExitCode}");
        }
    }
    
    BeatmapData LoadBeatmapData(string jsonPath, string midiPath, string audioPath)
    {
        try
        {
            string jsonContent = File.ReadAllText(jsonPath);
            
            // Parse using JsonUtility
            var wrapper = JsonUtility.FromJson<BeatmapWrapper>(jsonContent);
            
            BeatmapData beatmapData = new BeatmapData
            {
                title = wrapper.metadata.title,
                midiFilePath = midiPath,
                audioFilePath = audioPath,
                jsonFilePath = jsonPath,
                metadata = wrapper.metadata,
                beatmap = wrapper.beatmap
            };
            
            // Ensure imported beatmaps have audio_includes_countdown = false
            if (beatmapData.metadata != null)
            {
                beatmapData.metadata.audio_includes_countdown = false;
                UnityEngine.Debug.Log($"[BeatmapImporter] Loaded beatmap: {beatmapData.title}, offset={beatmapData.metadata.drum_start_offset:F1}s, audio_includes_countdown={beatmapData.metadata.audio_includes_countdown}");
            }
            
            return beatmapData;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error loading beatmap data: {e.Message}");
            return null;
        }
    }
    
    [Serializable]
    private class BeatmapWrapper
    {
        public BeatmapData.BeatmapMetadata metadata;
        public System.Collections.Generic.List<BeatmapNote> beatmap;
    }
}
