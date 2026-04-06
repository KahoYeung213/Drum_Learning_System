using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class BeatmapAccordionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform accordionContent; // The "accordion" container
    [SerializeField] private GameObject accordionItemPrefab; // Prefab for accordion items
    [SerializeField] private bool allowAutoResolveBindings = false;
    [SerializeField] private bool runThisAccordionInstance = false;
    [SerializeField] private BeatmapLibrary beatmapLibrary;
    [SerializeField] private BeatmapPlayer beatmapPlayer; // Reference to the player
    
    [Header("Item References (for prefab setup)")]
    [SerializeField] private string headerTextPath = "Title"; // Path to header text in prefab
    [SerializeField] private string contentTextPath = "Maps"; // Path to content text in prefab
    [SerializeField] private string playButtonPath = "PlayButton"; // Path to the Play button in prefab
    [SerializeField] private string deleteButtonPath = "DeleteButton"; // Path to the Delete button in prefab
    [SerializeField] private string actionsContainerPath = "Actions"; // Optional parent object that wraps Select/Delete buttons
    
    private List<GameObject> accordionItems = new List<GameObject>();
    private bool isSubscribedToLibrary;

    void Awake()
    {
        if (!runThisAccordionInstance)
        {
            enabled = false;
            Debug.LogWarning($"[BeatmapAccordionUI] Disabled on '{name}' because runThisAccordionInstance is off.");
            return;
        }

        ResolveAccordionReferences();
    }
    
    void Start()
    {
        if (!enabled)
        {
            return;
        }

        ResolveAccordionReferences();

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
            if (HasValidAccordionBindings())
            {
                beatmapLibrary.OnBeatmapsLoaded += PopulateAccordion;
                beatmapLibrary.OnBeatmapAdded += AddBeatmapToAccordion;
                isSubscribedToLibrary = true;
            }
            else
            {
                Debug.LogWarning($"[BeatmapAccordionUI] Skipping BeatmapLibrary subscription on '{name}' (scene: {gameObject.scene.name}, instance: {GetInstanceID()}) because accordionContent/prefab is not configured. This instance will not react to imports until references are fixed.");
            }
        }
        else
        {
            Debug.LogError($"[BeatmapAccordionUI] BeatmapLibrary not found on object '{name}' in scene '{gameObject.scene.name}'.");
        }
        
        // Initial population if beatmaps are already loaded
        if (isSubscribedToLibrary && beatmapLibrary != null && beatmapLibrary.Beatmaps.Count > 0)
        {
            PopulateAccordion(beatmapLibrary.Beatmaps);
        }

        Debug.Log($"[BeatmapAccordionUI] Initialized '{name}' (scene: {gameObject.scene.name}, instance: {GetInstanceID()}) content={(accordionContent != null ? accordionContent.name : "NULL")} prefab={(accordionItemPrefab != null ? accordionItemPrefab.name : "NULL")}");
    }
    
    void OnDestroy()
    {
        if (isSubscribedToLibrary && beatmapLibrary != null)
        {
            beatmapLibrary.OnBeatmapsLoaded -= PopulateAccordion;
            beatmapLibrary.OnBeatmapAdded -= AddBeatmapToAccordion;
            isSubscribedToLibrary = false;
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
        ResolveAccordionReferences();

        if (accordionItemPrefab == null || accordionContent == null)
        {
            Debug.LogError($"[BeatmapAccordionUI] Accordion item prefab or content container not assigned on '{name}' (scene: {gameObject.scene.name}, instance: {GetInstanceID()}). Content={(accordionContent != null ? accordionContent.name : "NULL")}, Prefab={(accordionItemPrefab != null ? accordionItemPrefab.name : "NULL")}");
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
        
        GameObject actionsContainer = null;
        if (!string.IsNullOrEmpty(actionsContainerPath))
        {
            Transform actionsTransform = FindTransformByPathOrName(item.transform, actionsContainerPath);
            if (actionsTransform != null)
                actionsContainer = actionsTransform.gameObject;
        }

        // Look for the Play button and add click listener
        Button playButton = FindButtonWithFallback(item.transform, actionsContainer != null ? actionsContainer.transform : null, playButtonPath);
        
        if (playButton != null)
        {
            playButton.onClick.AddListener(() => OnBeatmapSelected(beatmap));
        }
        else
        {
            Debug.LogWarning($"Play button not found at path '{playButtonPath}' for beatmap: {beatmap.title}");
        }
        
        // Look for the Delete button and add click listener
        Button deleteButton = FindButtonWithFallback(item.transform, actionsContainer != null ? actionsContainer.transform : null, deleteButtonPath);
        
        if (deleteButton != null)
        {
            deleteButton.onClick.AddListener(() => OnBeatmapDelete(beatmap, item));
        }
        else
        {
            Debug.LogWarning($"Delete button not found at path '{deleteButtonPath}' for beatmap: {beatmap.title}");
        }

        // Keep action buttons hidden while accordion element is collapsed.
        Toggle itemToggle = item.GetComponent<Toggle>();
        if (itemToggle != null)
        {
            SetActionButtonsVisible(playButton, deleteButton, actionsContainer, itemToggle.isOn);
            itemToggle.onValueChanged.AddListener(isOpen => SetActionButtonsVisible(playButton, deleteButton, actionsContainer, isOpen));
        }
        else
        {
            // Fallback for non-toggle prefabs: leave buttons visible.
            SetActionButtonsVisible(playButton, deleteButton, actionsContainer, true);
        }
        
        // Debug: Check if item was properly added to parent
        Debug.Log($"[BeatmapAccordionUI] Item parent: {item.transform.parent.name}, Accordion content has {accordionContent.childCount} children");
        
        // Force layout rebuild
        LayoutRebuilder.ForceRebuildLayoutImmediate(accordionContent.GetComponent<RectTransform>());
    }

    Button FindButtonWithFallback(Transform itemRoot, Transform actionsRoot, string pathOrName)
    {
        Transform found = FindTransformByPathOrName(itemRoot, pathOrName);

        if (found == null && actionsRoot != null)
            found = FindTransformByPathOrName(actionsRoot, pathOrName);

        if (found == null)
            return null;

        return found.GetComponent<Button>();
    }

    Transform FindTransformByPathOrName(Transform root, string pathOrName)
    {
        if (root == null || string.IsNullOrEmpty(pathOrName))
            return null;

        Transform found = root.Find(pathOrName);
        if (found != null)
            return found;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
        {
            if (t.name == pathOrName)
                return t;
        }

        return null;
    }

    void SetActionButtonsVisible(Button playButton, Button deleteButton, GameObject actionsContainer, bool isVisible)
    {
        if (actionsContainer != null)
        {
            actionsContainer.SetActive(isVisible);
            return;
        }

        if (playButton != null)
            playButton.gameObject.SetActive(isVisible);

        if (deleteButton != null)
            deleteButton.gameObject.SetActive(isVisible);
    }

    void ResolveAccordionReferences()
    {
        if (!allowAutoResolveBindings)
        {
            return;
        }

        if (accordionContent == null || accordionItemPrefab == null)
        {
            Debug.LogWarning($"[BeatmapAccordionUI] Auto-resolve is enabled but bindings are incomplete on '{name}'. Please assign accordionContent and accordionItemPrefab explicitly in the Inspector.");
        }
    }

    bool HasValidAccordionBindings()
    {
        ResolveAccordionReferences();
        return accordionContent != null && accordionItemPrefab != null;
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
