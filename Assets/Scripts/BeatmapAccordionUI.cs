using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class BeatmapAccordionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform accordionContent; // The "accordion" container
    [SerializeField] private GameObject accordionItemPrefab; // Prefab for accordion items
    [SerializeField] private BeatmapLibrary beatmapLibrary;
    [SerializeField] private BeatmapPlayer beatmapPlayer; // Reference to the player
    
    [Header("Item References (for prefab setup)")]
    [SerializeField] private string headerTextPath = "Title"; // Path to header text in prefab
    [SerializeField] private string contentTextPath = "Maps"; // Path to content text in prefab
    [SerializeField] private string playButtonPath = "PlayButton"; // Path to the Play button in prefab
    [SerializeField] private string deleteButtonPath = "DeleteButton"; // Path to the Delete button in prefab
    
    [Header("Play Button Configuration")]
    [SerializeField] private Vector2 playButtonSize = new Vector2(60f, 30f); // Width x Height
    [SerializeField] private float playButtonRightOffset = 10f; // Distance from right edge
    
    [Header("Delete Button Configuration")]
    [SerializeField] private Vector2 deleteButtonSize = new Vector2(60f, 30f); // Width x Height
    [SerializeField] private float deleteButtonRightOffset = 80f; // Distance from right edge
    
    private List<GameObject> accordionItems = new List<GameObject>();
    
    void Start()
    {
        // Find library if not assigned
        if (beatmapLibrary == null)
        {
            beatmapLibrary = FindFirstObjectByType<BeatmapLibrary>();
        }
        
        // Find player if not assigned
        if (beatmapPlayer == null)
        {
            beatmapPlayer = FindFirstObjectByType<BeatmapPlayer>();
        }
        
        if (beatmapLibrary != null)
        {
            beatmapLibrary.OnBeatmapsLoaded += PopulateAccordion;
            beatmapLibrary.OnBeatmapAdded += AddBeatmapToAccordion;
        }
        
        // Initial population if beatmaps are already loaded
        if (beatmapLibrary != null && beatmapLibrary.Beatmaps.Count > 0)
        {
            PopulateAccordion(beatmapLibrary.Beatmaps);
        }
    }
    
    void OnDestroy()
    {
        if (beatmapLibrary != null)
        {
            beatmapLibrary.OnBeatmapsLoaded -= PopulateAccordion;
            beatmapLibrary.OnBeatmapAdded -= AddBeatmapToAccordion;
        }
    }
    
    public void PopulateAccordion(List<BeatmapData> beatmaps)
    {
        Debug.Log($"[BeatmapAccordionUI] PopulateAccordion called with {beatmaps.Count} beatmaps");
        
        // Clear existing items
        ClearAccordion();
        
        // Create new items
        foreach (BeatmapData beatmap in beatmaps)
        {
            CreateAccordionItem(beatmap);
        }
        
        Debug.Log($"[BeatmapAccordionUI] Created {accordionItems.Count} accordion items");
    }
    
    void AddBeatmapToAccordion(BeatmapData beatmap)
    {
        CreateAccordionItem(beatmap);
    }
    
    void CreateAccordionItem(BeatmapData beatmap)
    {
        if (accordionItemPrefab == null || accordionContent == null)
        {
            Debug.LogError("Accordion item prefab or content container not assigned!");
            return;
        }
        
        Debug.Log($"[BeatmapAccordionUI] Creating accordion item for: {beatmap.title}");
        
        GameObject item = Instantiate(accordionItemPrefab, accordionContent);
        item.name = $"BeatmapItem_{beatmap.title}";
        item.SetActive(true);
        
        Debug.Log($"[BeatmapAccordionUI] Item created and activated: {item.name}");
        
        // Set header text (title)
        Transform headerText = item.transform.Find(headerTextPath);
        if (headerText != null)
        {
            TextMeshProUGUI headerTMP = headerText.GetComponent<TextMeshProUGUI>();
            if (headerTMP != null)
            {
                headerTMP.text = beatmap.title;
            }
            else
            {
                Text headerUI = headerText.GetComponent<Text>();
                if (headerUI != null)
                {
                    headerUI.text = beatmap.title;
                }
            }
        }
        
        // Set content text (beatmap info)
        Transform contentText = item.transform.Find(contentTextPath);
        if (contentText != null)
        {
            TextMeshProUGUI contentTMP = contentText.GetComponent<TextMeshProUGUI>();
            if (contentTMP != null)
            {
                contentTMP.text = beatmap.GetDisplayInfo();
            }
            else
            {
                Text contentUI = contentText.GetComponent<Text>();
                if (contentUI != null)
                {
                    contentUI.text = beatmap.GetDisplayInfo();
                }
            }
        }
        
        // Store reference
        accordionItems.Add(item);
        
        // Look for the Play button and add click listener
        Button playButton = null;
        
        if (!string.IsNullOrEmpty(playButtonPath))
        {
            Transform buttonTransform = item.transform.Find(playButtonPath);
            if (buttonTransform != null)
            {
                playButton = buttonTransform.GetComponent<Button>();
            }
        }
        
        if (playButton != null)
        {
            // Configure the button to ignore layout groups and position it on the right
            RectTransform buttonRect = playButton.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                // Make the button ignore any layout group controls
                LayoutElement layoutElement = playButton.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = playButton.gameObject.AddComponent<LayoutElement>();
                }
                layoutElement.ignoreLayout = true;
                
                // Set anchors to middle-right (not stretched)
                buttonRect.anchorMin = new Vector2(1f, 0.5f);
                buttonRect.anchorMax = new Vector2(1f, 0.5f);
                buttonRect.pivot = new Vector2(1f, 0.5f);
                
                // Set button size (configurable in inspector)
                buttonRect.sizeDelta = playButtonSize;
                
                // Position it from the right edge, centered vertically (configurable offset)
                buttonRect.anchoredPosition = new Vector2(-playButtonRightOffset, 0f);
            }
            
            playButton.onClick.AddListener(() => OnBeatmapSelected(beatmap));
            
            // Optionally set the button text to "Play"
            TextMeshProUGUI buttonText = playButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = "Play";
            }
            else
            {
                Text legacyText = playButton.GetComponentInChildren<Text>();
                if (legacyText != null)
                {
                    legacyText.text = "Play";
                }
            }
        }
        else
        {
            Debug.LogWarning($"Play button not found at path '{playButtonPath}' for beatmap: {beatmap.title}");
        }
        
        // Look for the Delete button and add click listener
        Button deleteButton = null;
        
        if (!string.IsNullOrEmpty(deleteButtonPath))
        {
            Transform buttonTransform = item.transform.Find(deleteButtonPath);
            if (buttonTransform != null)
            {
                deleteButton = buttonTransform.GetComponent<Button>();
            }
        }
        
        if (deleteButton != null)
        {
            // Configure the delete button to ignore layout groups and position it on the right
            RectTransform buttonRect = deleteButton.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                // Make the button ignore any layout group controls
                LayoutElement layoutElement = deleteButton.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = deleteButton.gameObject.AddComponent<LayoutElement>();
                }
                layoutElement.ignoreLayout = true;
                
                // Set anchors to middle-right (not stretched)
                buttonRect.anchorMin = new Vector2(1f, 0.5f);
                buttonRect.anchorMax = new Vector2(1f, 0.5f);
                buttonRect.pivot = new Vector2(1f, 0.5f);
                
                // Set button size (configurable in inspector)
                buttonRect.sizeDelta = deleteButtonSize;
                
                // Position it from the right edge, centered vertically (configurable offset)
                buttonRect.anchoredPosition = new Vector2(-deleteButtonRightOffset, 0f);
            }
            
            deleteButton.onClick.AddListener(() => OnBeatmapDelete(beatmap, item));
            
            // Set the button text to "Delete"
            TextMeshProUGUI buttonText = deleteButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = "Delete";
            }
            else
            {
                Text legacyText = deleteButton.GetComponentInChildren<Text>();
                if (legacyText != null)
                {
                    legacyText.text = "Delete";
                }
            }
        }
        
        // Debug: Check if item was properly added to parent
        Debug.Log($"[BeatmapAccordionUI] Item parent: {item.transform.parent.name}, Accordion content has {accordionContent.childCount} children");
        
        // Force layout rebuild
        LayoutRebuilder.ForceRebuildLayoutImmediate(accordionContent.GetComponent<RectTransform>());
    }
    
    void ClearAccordion()
    {
        Debug.Log($"[BeatmapAccordionUI] Clearing {accordionItems.Count} accordion items");
        
        foreach (GameObject item in accordionItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        accordionItems.Clear();
        
        Debug.Log($"[BeatmapAccordionUI] Accordion cleared. Content has {accordionContent.childCount} children");
    }
    
    void OnBeatmapSelected(BeatmapData beatmap)
    {
        Debug.Log($"Selected beatmap: {beatmap.title}");
        
        // Load beatmap into player
        if (beatmapPlayer != null)
        {
            beatmapPlayer.LoadBeatmap(beatmap);
        }
        else
        {
            Debug.LogWarning("BeatmapPlayer not found! Cannot load beatmap for playback.");
        }
    }
    
    void OnBeatmapDelete(BeatmapData beatmap, GameObject accordionItem)
    {
        Debug.Log($"[BeatmapAccordionUI] Delete requested for beatmap: {beatmap.title}");
        
        if (beatmapLibrary == null)
        {
            Debug.LogError("[BeatmapAccordionUI] Cannot delete - BeatmapLibrary not found!");
            return;
        }
        
        // Confirm deletion (you can add a confirmation dialog here later)
        try
        {
            // Remove from library (this will delete the folder)
            beatmapLibrary.DeleteBeatmap(beatmap);
            
            // Remove from UI
            if (accordionItems.Contains(accordionItem))
            {
                accordionItems.Remove(accordionItem);
            }
            Destroy(accordionItem);
            
            // Force layout rebuild
            LayoutRebuilder.ForceRebuildLayoutImmediate(accordionContent.GetComponent<RectTransform>());
            
            Debug.Log($"[BeatmapAccordionUI] Successfully deleted beatmap: {beatmap.title}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BeatmapAccordionUI] Error deleting beatmap: {e.Message}");
        }
    }
}
