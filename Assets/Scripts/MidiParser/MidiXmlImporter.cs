using UnityEngine;
using UnityEngine.UI;
using SFB;
using System.IO;
using System.Diagnostics;
using System.Collections;

public class MidiXmlImporter : MonoBehaviour
{
    [SerializeField] private Button importButton;
    [SerializeField] private float spawnLeadTime = 2.0f;
    
    private string pythonScriptPath;
    private string outputDirectory;
    
    void Start()
    {
        // Set up paths
        pythonScriptPath = Path.Combine(Application.dataPath, "Scripts", "MidiParser", "music_xml_to_beatmap.py");
        outputDirectory = Path.Combine(Application.dataPath, "Beatmaps");
        
        // Create output directory if it doesn't exist
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
        
        if (importButton != null)
        {
            importButton.onClick.AddListener(OnImportButtonClicked);
        }
    }
    
    void OnImportButtonClicked()
    {
        var extensions = new[] {
            new ExtensionFilter("Music Files", "mid", "midi", "xml", "musicxml", "mxl"),
            new ExtensionFilter("MIDI Files", "mid", "midi"),
            new ExtensionFilter("MusicXML Files", "xml", "musicxml", "mxl"),
            new ExtensionFilter("All Files", "*")
        };
        
        StandaloneFileBrowser.OpenFilePanelAsync(
            "Import MIDI/MusicXML File", 
            "", 
            extensions, 
            false,
            (string[] paths) => {
                if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
                {
                    StartCoroutine(ProcessFile(paths[0]));
                }
            }
        );
    }
    
    IEnumerator ProcessFile(string inputPath)
    {
        UnityEngine.Debug.Log($"Processing file: {inputPath}");
        
        // Generate output filename
        string fileName = Path.GetFileNameWithoutExtension(inputPath);
        string outputPath = Path.Combine(outputDirectory, fileName + "_beatmap.json");
        
        // Call Python script
        yield return StartCoroutine(RunPythonParser(inputPath, outputPath));
        
        // Load the generated beatmap
        if (File.Exists(outputPath))
        {
            LoadBeatmap(outputPath);
        }
        else
        {
            UnityEngine.Debug.LogError("Failed to generate beatmap file.");
        }
    }
    
    IEnumerator RunPythonParser(string inputPath, string outputPath)
    {
        Process process = new Process();
        process.StartInfo.FileName = "python"; // or "python3" on some systems
        process.StartInfo.Arguments = $"\"{pythonScriptPath}\" \"{inputPath}\" \"{outputPath}\" {spawnLeadTime}";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;
        
        UnityEngine.Debug.Log($"Running: {process.StartInfo.FileName} {process.StartInfo.Arguments}");
        
        try
        {
            process.Start();
            
            // Read output
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            
            process.WaitForExit();
            
            if (!string.IsNullOrEmpty(output))
            {
                UnityEngine.Debug.Log($"Python Output: {output}");
            }
            
            if (!string.IsNullOrEmpty(error))
            {
                UnityEngine.Debug.LogError($"Python Error: {error}");
            }
            
            if (process.ExitCode != 0)
            {
                UnityEngine.Debug.LogError($"Python script exited with code: {process.ExitCode}");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Error running Python script: {e.Message}");
        }
        
        yield return null;
    }
    
    void LoadBeatmap(string jsonPath)
    {
        try
        {
            string jsonContent = File.ReadAllText(jsonPath);
            UnityEngine.Debug.Log($"Successfully loaded beatmap from: {jsonPath}");
            UnityEngine.Debug.Log($"Beatmap content: {jsonContent.Substring(0, Mathf.Min(200, jsonContent.Length))}...");
            
            // TODO: Parse JSON and use it in your game
            // You can use JsonUtility or a JSON library like Newtonsoft.Json
            // Example:
            // BeatmapData beatmap = JsonUtility.FromJson<BeatmapData>(jsonContent);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Error loading beatmap: {e.Message}");
        }
    }
}