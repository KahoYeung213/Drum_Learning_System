using UnityEngine;
using UnityEngine.UI;
using SFB;

public class FileImport : MonoBehaviour
{
    [SerializeField] private Button importButton;
    
    void Start()
    {
        if (importButton != null)
        {
            importButton.onClick.AddListener(OnImportButtonClicked);
        }
    }
    
    void OnImportButtonClicked()
    {
        var extensions = new[] {
            new ExtensionFilter("Audio Files", "mp3", "wav", "ogg"),
            new ExtensionFilter("All Files", "*")
        };
        
        StandaloneFileBrowser.OpenFilePanelAsync(
            "Select File to Import", 
            "", 
            extensions, 
            false,
            (string[] paths) => {
                if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
                {
                    ImportFile(paths[0]);
                }
            }
        );
    }
    
    void ImportFile(string filePath)
    {
        Debug.Log("Imported file: " + filePath);
        // Process your file here
    }
}