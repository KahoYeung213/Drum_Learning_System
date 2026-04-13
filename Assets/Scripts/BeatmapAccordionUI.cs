using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class BeatmapAccordionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform accordionContent; // The "accordion" container
    [SerializeField] private GameObject accordionItemPrefab; // Prefab for accordion items
    [SerializeField] private bool allowAutoResolveBindings = false;
    [SerializeField] private bool bindToBeatmapLibrary = true;
    [SerializeField] private BeatmapLibrary beatmapLibrary;
    [SerializeField] private BeatmapPlayer beatmapPlayer; // Reference to the player
    
    [Header("Item References (for prefab setup)")]
    [SerializeField] private string headerTextPath = "Title"; // Path to header text in prefab
    [SerializeField] private string contentTextPath = "Maps"; // Path to content text in prefab
    [SerializeField] private string playButtonPath = "PlayButton"; // Path to the Play button in prefab
    [SerializeField] private string deleteButtonPath = "DeleteButton"; // Path to the Delete button in prefab
    [SerializeField] private string actionsContainerPath = "Actions"; // Optional parent object that wraps Select/Delete buttons
    [SerializeField] private bool hideInlineBeatmapInfo = true;

    [Header("External Beatmap Info UI")]
    [SerializeField] private TMP_Text selectedBeatmapTitleText;
    [SerializeField] private TMP_Text selectedBeatmapInfoText;

    [Header("Nested Snips (Course-style)")]
    [SerializeField] private bool useNestedSnipButtons = true;
    [SerializeField] private bool allowCollapseExpandedBeatmap = true;
    [SerializeField] private string parentHeaderButtonPath = "Header";
    [SerializeField] private string snipsContainerPath = "SnipsContainer";
    [SerializeField] private GameObject snipItemButtonPrefab;
    [SerializeField] private string snipTitleTextPath = "Title";
    [SerializeField] private string snipPlayButtonPath = "PlayButton";
    [SerializeField] private string snipDeleteButtonPath = "DeleteButton";
    
    private List<GameObject> accordionItems = new List<GameObject>();
    private bool isSubscribedToLibrary;
    private readonly HashSet<string> expandedBeatmapKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool missingSnipsContainerWarningLogged;

    void Awake()
    {
        ResolveAccordionReferences();
    }
    
    void Start()
    {
        if (!enabled)
        {
            return;
        }

        ResolveAccordionReferences();

        if (!bindToBeatmapLibrary)
        {
            Debug.Log($"[BeatmapAccordionUI] Library binding disabled on '{name}' (scene: {gameObject.scene.name}, instance: {GetInstanceID()}).");
            return;
        }

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
        if (!bindToBeatmapLibrary)
        {
            return;
        }

        Debug.Log($"[BeatmapAccordionUI] PopulateAccordion called with {beatmaps.Count} beatmaps");

        // Avoid inherited expanded state across a full repopulation (e.g. after import/delete).
        expandedBeatmapKeys.Clear();
        
        // Clear existing items
        ClearAccordion();

        var childMap = BuildChildMap(beatmaps);
        var childJsonPaths = new HashSet<string>(childMap.Values.SelectMany(list => list).Select(b => NormalizePath(b.jsonFilePath)), StringComparer.OrdinalIgnoreCase);

        // Create only top-level parents. Children are rendered inside each parent's nested container.
        foreach (BeatmapData beatmap in beatmaps.OrderBy(b => b.title))
        {
            string currentJsonPath = NormalizePath(beatmap.jsonFilePath);
            if (childJsonPaths.Contains(currentJsonPath))
                continue;

            string parentKey = NormalizePath(beatmap.jsonFilePath);
            childMap.TryGetValue(parentKey, out List<BeatmapData> children);
            CreateAccordionItem(beatmap, children, parentKey);
        }
        
        Debug.Log($"[BeatmapAccordionUI] Created {accordionItems.Count} accordion items");
    }
    
    void AddBeatmapToAccordion(BeatmapData beatmap)
    {
        if (beatmapLibrary != null)
            PopulateAccordion(beatmapLibrary.Beatmaps);
    }
    
    GameObject CreateAccordionItem(BeatmapData beatmap, List<BeatmapData> children, string beatmapKey)
    {
        ResolveAccordionReferences();

        if (accordionItemPrefab == null || accordionContent == null)
        {
            Debug.LogError($"[BeatmapAccordionUI] Accordion item prefab or content container not assigned on '{name}' (scene: {gameObject.scene.name}, instance: {GetInstanceID()}). Content={(accordionContent != null ? accordionContent.name : "NULL")}, Prefab={(accordionItemPrefab != null ? accordionItemPrefab.name : "NULL")}");
            return null;
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
        
        // Inline content can be hidden when using external info panel.
        Transform contentText = item.transform.Find(contentTextPath);
        if (contentText != null)
        {
            if (hideInlineBeatmapInfo)
            {
                contentText.gameObject.SetActive(false);
            }
            else
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
        if (playButton == null)
            playButton = FindLikelyActionButton(item.transform, actionsContainer != null ? actionsContainer.transform : null, true, null);
        
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
        if (deleteButton == null)
            deleteButton = FindLikelyActionButton(item.transform, actionsContainer != null ? actionsContainer.transform : null, false, playButton);
        
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
            // Start collapsed to prevent prefab default toggle state from expanding every item.
            itemToggle.SetIsOnWithoutNotify(false);
            SetActionButtonsVisible(playButton, deleteButton, actionsContainer, false);
            itemToggle.onValueChanged.AddListener(isOpen => SetActionButtonsVisible(playButton, deleteButton, actionsContainer, isOpen));
        }

        if (useNestedSnipButtons)
        {
            Transform snipsContainer = FindTransformByPathOrName(item.transform, snipsContainerPath);
            bool hasChildren = PopulateSnipButtons(snipsContainer, children);

            if (!hasChildren)
                expandedBeatmapKeys.Remove(beatmapKey);

            bool isExpanded = !string.IsNullOrEmpty(beatmapKey) && expandedBeatmapKeys.Contains(beatmapKey);
            SetSnipsContainerVisibility(snipsContainer, hasChildren && isExpanded);

            if (itemToggle != null)
            {
                bool shouldOpen = hasChildren && isExpanded;
                itemToggle.SetIsOnWithoutNotify(shouldOpen);
                SetActionButtonsVisible(playButton, deleteButton, actionsContainer, shouldOpen);
            }

            Button headerButton = null;
            Transform headerTransform = FindTransformByPathOrName(item.transform, parentHeaderButtonPath);
            if (headerTransform != null)
            {
                headerButton = headerTransform.GetComponent<Button>();
                if (headerButton == null)
                    headerButton = headerTransform.GetComponentInChildren<Button>(true);
            }

            if (headerButton != null)
            {
                headerButton.onClick.AddListener(() =>
                {
                    ToggleBeatmapSnips(beatmapKey, snipsContainer, hasChildren);
                    UpdateExternalBeatmapInfo(beatmap);
                });
            }
            else if (itemToggle != null)
            {
                itemToggle.onValueChanged.AddListener(_ =>
                {
                    ToggleBeatmapSnips(beatmapKey, snipsContainer, hasChildren);
                    UpdateExternalBeatmapInfo(beatmap);
                });
            }
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

        return item;
    }

    bool PopulateSnipButtons(Transform snipsContainer, List<BeatmapData> children)
    {
        if (snipsContainer == null)
        {
            if (children != null && children.Count > 0 && !missingSnipsContainerWarningLogged)
            {
                missingSnipsContainerWarningLogged = true;
                Debug.LogWarning($"[BeatmapAccordionUI] Parent beatmaps have snips but no snips container was found at '{snipsContainerPath}'. Nested snips are disabled for this UI instance.");
            }
            return false;
        }

        GameObject template = GetSnipTemplate(snipsContainer);
        ClearNestedList(snipsContainer, template);

        if (template != null)
            template.SetActive(false);

        List<BeatmapData> safeChildren = children ?? new List<BeatmapData>();
        foreach (BeatmapData child in safeChildren.OrderBy(c => c.title))
        {
            CreateSnipItem(snipsContainer, template, child);
        }

        return safeChildren.Count > 0;
    }

    void ToggleBeatmapSnips(string beatmapKey, Transform snipsContainer, bool hasChildren)
    {
        if (!hasChildren || snipsContainer == null)
        {
            SetSnipsContainerVisibility(snipsContainer, false);
            return;
        }

        if (string.IsNullOrEmpty(beatmapKey))
        {
            bool currentlyVisible = snipsContainer.gameObject.activeSelf;
            SetSnipsContainerVisibility(snipsContainer, allowCollapseExpandedBeatmap ? !currentlyVisible : true);
            return;
        }

        bool isExpanded = expandedBeatmapKeys.Contains(beatmapKey);
        if (isExpanded && allowCollapseExpandedBeatmap)
        {
            expandedBeatmapKeys.Remove(beatmapKey);
            SetSnipsContainerVisibility(snipsContainer, false);
            return;
        }

        if (isExpanded)
        {
            SetSnipsContainerVisibility(snipsContainer, true);
            return;
        }

        expandedBeatmapKeys.Add(beatmapKey);
        SetSnipsContainerVisibility(snipsContainer, true);
    }

    void SetSnipsContainerVisibility(Transform snipsContainer, bool visible)
    {
        if (snipsContainer != null)
            snipsContainer.gameObject.SetActive(visible);
    }

    GameObject GetSnipTemplate(Transform snipsContainer)
    {
        if (snipItemButtonPrefab != null)
            return snipItemButtonPrefab;

        if (snipsContainer != null && snipsContainer.childCount > 0)
            return snipsContainer.GetChild(0).gameObject;

        return null;
    }

    void CreateSnipItem(Transform snipsContainer, GameObject template, BeatmapData snipBeatmap)
    {
        GameObject source = template != null ? template : snipItemButtonPrefab;
        if (source == null)
        {
            Debug.LogWarning("[BeatmapAccordionUI] Cannot create snip item: no template or snip item prefab.");
            return;
        }

        GameObject snipItem = Instantiate(source, snipsContainer);
        snipItem.name = $"SnipItem_{snipBeatmap.title}";
        snipItem.SetActive(true);

        SetTextOnPath(snipItem.transform, snipTitleTextPath, snipBeatmap.title);

        Button playButton = FindButtonWithFallback(snipItem.transform, null, snipPlayButtonPath);
        if (playButton == null)
            playButton = snipItem.GetComponent<Button>();
        if (playButton == null)
            playButton = snipItem.GetComponentInChildren<Button>(true);

        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(() => OnBeatmapSelected(snipBeatmap));
        }

        Button deleteButton = FindButtonWithFallback(snipItem.transform, null, snipDeleteButtonPath);
        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => OnBeatmapDelete(snipBeatmap, snipItem));
        }
    }

    void ClearNestedList(Transform root, GameObject preserveItem = null)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            if (preserveItem != null && child == preserveItem)
                continue;

            // Preserve label elements (Text/TMP_Text without Button)
            if (child.GetComponent<Button>() == null && (child.GetComponent<Text>() != null || child.GetComponent<TMP_Text>() != null))
                continue;

            Destroy(child);
        }

        if (preserveItem != null)
            preserveItem.transform.SetAsFirstSibling();
    }

    void SetTextOnPath(Transform root, string path, string value)
    {
        Transform target = FindTransformByPathOrName(root, path);
        if (target == null)
            target = root;

        TMP_Text tmp = target.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            tmp.text = value;
            return;
        }

        Text text = target.GetComponent<Text>();
        if (text != null)
        {
            text.text = value;
            return;
        }

        TMP_Text fallbackTmp = root.GetComponentInChildren<TMP_Text>(true);
        if (fallbackTmp != null)
        {
            fallbackTmp.text = value;
            return;
        }

        Text fallbackText = root.GetComponentInChildren<Text>(true);
        if (fallbackText != null)
            fallbackText.text = value;
    }

    Dictionary<string, List<BeatmapData>> BuildChildMap(List<BeatmapData> beatmaps)
    {
        var beatmapByJsonPath = new Dictionary<string, BeatmapData>(StringComparer.OrdinalIgnoreCase);
        foreach (BeatmapData beatmap in beatmaps)
        {
            string jsonPath = NormalizePath(beatmap.jsonFilePath);
            if (!string.IsNullOrEmpty(jsonPath) && !beatmapByJsonPath.ContainsKey(jsonPath))
                beatmapByJsonPath.Add(jsonPath, beatmap);
        }

        var childMap = new Dictionary<string, List<BeatmapData>>(StringComparer.OrdinalIgnoreCase);
        foreach (BeatmapData beatmap in beatmaps)
        {
            string beatmapJsonPath = NormalizePath(beatmap.jsonFilePath);
            string parentJsonPath = GetParentBeatmapJsonPath(beatmapJsonPath);
            if (string.IsNullOrEmpty(parentJsonPath))
                continue;

            if (!beatmapByJsonPath.ContainsKey(parentJsonPath))
                continue;

            if (!childMap.TryGetValue(parentJsonPath, out List<BeatmapData> children))
            {
                children = new List<BeatmapData>();
                childMap[parentJsonPath] = children;
            }

            children.Add(beatmap);
        }

        return childMap;
    }

    string GetParentBeatmapJsonPath(string jsonFilePath)
    {
        if (string.IsNullOrEmpty(jsonFilePath))
            return null;

        string beatmapFolder = Path.GetDirectoryName(jsonFilePath);
        if (string.IsNullOrEmpty(beatmapFolder))
            return null;

        string parentFolder = Directory.GetParent(beatmapFolder)?.FullName;
        if (string.IsNullOrEmpty(parentFolder))
            return null;

        return NormalizePath(Path.Combine(parentFolder, "beatmap.json"));
    }

    string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        return path.Replace('\\', '/').Trim();
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

    Button FindLikelyActionButton(Transform itemRoot, Transform actionsRoot, bool preferPlay, Button excludeButton)
    {
        string[] prioritizedNames = preferPlay
            ? new[] { "PlayButton", "Play", "play", "SelectButton", "Select", "select" }
            : new[] { "DeleteButton", "Delete", "delete", "Remove", "remove", "Trash", "trash" };

        foreach (string candidate in prioritizedNames)
        {
            Button candidateButton = FindButtonWithFallback(itemRoot, actionsRoot, candidate);
            if (candidateButton != null && candidateButton != excludeButton)
                return candidateButton;
        }

        Transform searchRoot = actionsRoot != null ? actionsRoot : itemRoot;
        if (searchRoot == null)
            return null;

        Button[] allButtons = searchRoot.GetComponentsInChildren<Button>(true);
        foreach (Button btn in allButtons)
        {
            if (btn != null && btn != excludeButton)
                return btn;
        }

        return null;
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
        UpdateExternalBeatmapInfo(beatmap);
        
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
        
        try
        {
            // Destroy the UI item immediately
            if (accordionItem != null)
            {
                DestroyImmediate(accordionItem);
            }
            
            // Delete from library (this will fire OnBeatmapsLoaded event)
            beatmapLibrary.DeleteBeatmap(beatmap);
            
            // Full repopulation will happen via OnBeatmapsLoaded event
            // Just log for confirmation
            Debug.Log($"[BeatmapAccordionUI] Successfully deleted beatmap: {beatmap.title}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BeatmapAccordionUI] Error deleting beatmap: {e.Message}");
        }
    }

    void UpdateExternalBeatmapInfo(BeatmapData beatmap)
    {
        if (beatmap == null)
            return;

        if (selectedBeatmapTitleText != null)
            selectedBeatmapTitleText.text = beatmap.title;

        if (selectedBeatmapInfoText != null)
            selectedBeatmapInfoText.text = beatmap.GetDisplayInfo();
    }
}
