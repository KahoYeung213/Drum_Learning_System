using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using SFB;
using System;
using System.Collections;
using System.IO;
using System.Linq;

/// <summary>
/// File upload area with click-to-browse support
/// </summary>
public class DragDropFileArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("File Type")]
    [SerializeField] private FileType fileType = FileType.MIDI;
    
    [Header("UI References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text fileNameText; // Optional: displays selected file name
    [SerializeField] private GameObject placeholderContent; // Content shown when no file is selected
    [SerializeField] private GameObject selectedContent; // Content shown when file is selected
    
    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color hoverColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
    [SerializeField] private Color selectedColor = new Color(0.3f, 0.5f, 0.3f, 0.9f);
    
    [Header("Labels")]
    [SerializeField] private string defaultLabel = "Click to upload";
    
    // Events
    public event Action<string> OnFileSelected; // Called when a file is selected with the file path
    
    // State
    private string selectedFilePath = null;
    private bool hasFile = false;

    // Global lock to prevent duplicate handlers from opening nested file pickers.
    private static bool isFileDialogOpen = false;
    private static bool isDialogUnlockQueued = false;
    
    void Start()
    {
        UpdateVisuals();
        UpdateContent();
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!hasFile)
        {
            UpdateVisuals(true);
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        UpdateVisuals();
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isFileDialogOpen)
        {
            return;
        }

        OpenFileBrowser();
    }
    
    void OpenFileBrowser()
    {
        isFileDialogOpen = true;

        string[] extensions = GetFileExtensions();
        string filterName = GetFileTypeName();

        StandaloneFileBrowser.OpenFilePanelAsync(
            $"Select {filterName} File",
            "",
            new[] { new ExtensionFilter(filterName, extensions) },
            false,
            (string[] paths) =>
            {
                if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
                {
                    SelectFile(paths[0]);
                }

                QueueDialogUnlock();
            }
        );
    }

    void QueueDialogUnlock()
    {
        if (isDialogUnlockQueued)
        {
            return;
        }

        isDialogUnlockQueued = true;
        StartCoroutine(UnlockDialogNextFrame());
    }

    IEnumerator UnlockDialogNextFrame()
    {
        yield return null;
        isFileDialogOpen = false;
        isDialogUnlockQueued = false;
    }
    
    void SelectFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"[DragDropFileArea] File does not exist: {filePath}");
            return;
        }
        
        selectedFilePath = filePath;
        hasFile = true;
        
        // Update UI
        if (fileNameText != null)
        {
            fileNameText.text = Path.GetFileName(filePath);
        }
        
        UpdateVisuals();
        UpdateContent();
        
        // Notify listeners
        OnFileSelected?.Invoke(filePath);
        
        Debug.Log($"[DragDropFileArea] File selected: {filePath}");
    }
    
    bool ValidateFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return false;
        
        string extension = Path.GetExtension(filePath).ToLower();
        string[] validExtensions = GetFileExtensions();
        
        return validExtensions.Any(ext => extension == "." + ext.ToLower());
    }
    
    string[] GetFileExtensions()
    {
        switch (fileType)
        {
            case FileType.MIDI:
                return new[] { "mid", "midi", "xml", "musicxml" };
            case FileType.Audio:
                return new[] { "mp3", "wav" };
            case FileType.JSON:
                return new[] { "json" };
            default:
                return new[] { "*" };
        }
    }
    
    string GetFileTypeName()
    {
        switch (fileType)
        {
            case FileType.MIDI:
                return "MIDI/MusicXML";
            case FileType.Audio:
                return "Audio (MP3/WAV)";
            case FileType.JSON:
                return "JSON";
            default:
                return "File";
        }
    }
    
    void UpdateVisuals(bool hover = false)
    {
        if (backgroundImage == null) return;
        
        Color targetColor;
        string targetLabel = defaultLabel;
        
        if (hasFile)
        {
            targetColor = selectedColor;
        }
        else if (hover)
        {
            targetColor = hoverColor;
        }
        else
        {
            targetColor = normalColor;
        }
        
        backgroundImage.color = targetColor;
        
        if (labelText != null && !hasFile)
        {
            labelText.text = targetLabel;
        }
    }
    
    void UpdateContent()
    {
        if (placeholderContent != null)
        {
            placeholderContent.SetActive(!hasFile);
        }
        
        if (selectedContent != null)
        {
            selectedContent.SetActive(hasFile);
        }
    }

    // Public methods
    public void ClearSelection()
    {
        selectedFilePath = null;
        hasFile = false;
        
        if (fileNameText != null)
        {
            fileNameText.text = "";
        }
        
        UpdateVisuals();
        UpdateContent();
    }
    
    public string GetSelectedFilePath()
    {
        return selectedFilePath;
    }
    
    public bool HasFile()
    {
        return hasFile;
    }
    
    public void SetFileType(FileType type)
    {
        fileType = type;
    }
}

public enum FileType
{
    MIDI,
    Audio,
    JSON,
    Any
}
