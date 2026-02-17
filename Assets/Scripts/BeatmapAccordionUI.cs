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
    
    [Header("Item References (for prefab setup)")]
    [SerializeField] private string headerTextPath = "Title"; // Path to header text in prefab
    [SerializeField] private string contentTextPath = "Maps"; // Path to content text in prefab
    
    private List<GameObject> accordionItems = new List<GameObject>();
    
    void Start()
    {
        // Find library if not assigned
        if (beatmapLibrary == null)
        {
            beatmapLibrary = FindFirstObjectByType<BeatmapLibrary>();
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
        // Clear existing items
        ClearAccordion();
        
        // Create new items
        foreach (BeatmapData beatmap in beatmaps)
        {
            CreateAccordionItem(beatmap);
        }
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
        
        Debug.Log($"Creating accordion item for: {beatmap.title}");
        
        GameObject item = Instantiate(accordionItemPrefab, accordionContent);
        item.name = $"BeatmapItem_{beatmap.title}";
        item.SetActive(true);
        
        Debug.Log($"Item created: {item.name}, parent: {accordionContent.name}");
        
        // Set header text (title)
        Transform headerText = item.transform.Find(headerTextPath);
        if (headerText != null)
        {
            Debug.Log($"Found header at: {headerTextPath}");
            TextMeshProUGUI headerTMP = headerText.GetComponent<TextMeshProUGUI>();
            if (headerTMP != null)
            {
                headerTMP.text = beatmap.title;
                Debug.Log($"Set TMP header text to: {beatmap.title}");
            }
            else
            {
                Text headerUI = headerText.GetComponent<Text>();
                if (headerUI != null)
                {
                    headerUI.text = beatmap.title;
                    Debug.Log($"Set UI Text header text to: {beatmap.title}");
                }
                else
                {
                    Debug.LogWarning($"No TextMeshProUGUI or Text component found on header at {headerTextPath}");
                }
            }
        }
        else
        {
            Debug.LogError($"Could not find header at path: {headerTextPath}. Item children: {string.Join(", ", GetChildNames(item.transform))}");
        }
        
        // Set content text (beatmap info)
        Transform contentText = item.transform.Find(contentTextPath);
        if (contentText != null)
        {
            Debug.Log($"Found content at: {contentTextPath}");
            TextMeshProUGUI contentTMP = contentText.GetComponent<TextMeshProUGUI>();
            if (contentTMP != null)
            {
                contentTMP.text = beatmap.GetDisplayInfo();
                Debug.Log($"Set TMP content text");
            }
            else
            {
                Text contentUI = contentText.GetComponent<Text>();
                if (contentUI != null)
                {
                    contentUI.text = beatmap.GetDisplayInfo();
                    Debug.Log($"Set UI Text content text");
                }
                else
                {
                    Debug.LogWarning($"No TextMeshProUGUI or Text component found on content at {contentTextPath}");
                }
            }
        }
        else
        {
            Debug.LogError($"Could not find content at path: {contentTextPath}. Item children: {string.Join(", ", GetChildNames(item.transform))}");
        }
        
        // Store reference
        accordionItems.Add(item);
        
        // Add click listener to load beatmap when clicked
        Button itemButton = item.GetComponentInChildren<Button>();
        if (itemButton != null)
        {
            itemButton.onClick.AddListener(() => OnBeatmapSelected(beatmap));
        }
        
        Debug.Log($"Accordion now has {accordionItems.Count} items");
        
        // Force layout rebuild
        LayoutRebuilder.ForceRebuildLayoutImmediate(accordionContent.GetComponent<RectTransform>());
    }
    
    private string[] GetChildNames(Transform parent)
    {
        string[] names = new string[parent.childCount];
        for (int i = 0; i < parent.childCount; i++)
        {
            names[i] = parent.GetChild(i).name;
        }
        return names;
    }
    
    void ClearAccordion()
    {
        foreach (GameObject item in accordionItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        accordionItems.Clear();
    }
    
    void OnBeatmapSelected(BeatmapData beatmap)
    {
        Debug.Log($"Selected beatmap: {beatmap.title}");
        // TODO: Load the beatmap for playing
        // You can add your own logic here or fire an event
    }
}
