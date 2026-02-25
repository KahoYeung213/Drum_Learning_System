using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// Draggable vertical line marker that can be placed on a timeline.
/// Attach this to an Image (vertical line) that is a child of the timeline.
/// The script automatically adds a wider invisible hit area for easier clicking.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DraggableTimelineMarker : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Visual")]
    [SerializeField] private Color normalColor = Color.yellow;
    [SerializeField] private Color draggingColor = Color.white;
    [SerializeField] private Image visualLine; // The thin visible line (assign the Image component)
    
    [Header("Interaction")]
    [SerializeField] private float hitAreaWidth = 50f; // Width of the clickable area in pixels
    [SerializeField] private RectTransform customReferenceRect; // Optional: use this rect instead of parent for width calculations
    
    private RectTransform rectTransform;
    private RectTransform parentRect; // Timeline rect
    private RectTransform effectiveRect; // The rect actually used for calculations (parent or custom)
    private Image hitAreaImage; // Invisible wider area for clicking
    private bool isDragging = false;
    private float normalizedPosition = 0f; // 0-1
    private float duration = 1f;
    
    public event Action<float> OnPositionChanged; // Sends normalized position (0-1)
    
    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        
        if (transform.parent != null)
            parentRect = transform.parent.GetComponent<RectTransform>();
        
        // Use custom reference rect if provided, otherwise use parent
        effectiveRect = customReferenceRect != null ? customReferenceRect : parentRect;
        
        // Ensure rectTransform is valid before setting properties
        if (rectTransform == null)
        {
            Debug.LogError("[DraggableTimelineMarker] RectTransform component not found!");
            return;
        }
        
        // Set up the rect transform for proper anchoring
        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        
        // Create invisible hit area for easier clicking
        hitAreaImage = GetComponent<Image>();
        if (hitAreaImage == null)
        {
            hitAreaImage = gameObject.AddComponent<Image>();
        }
        hitAreaImage.color = new Color(0, 0, 0, 0); // Fully transparent
        hitAreaImage.raycastTarget = true;
        
        // Set the hit area width
        rectTransform.sizeDelta = new Vector2(hitAreaWidth, 0);
        
        // If visualLine is assigned, set its color
        if (visualLine != null)
        {
            visualLine.color = normalColor;
        }
        else
        {
            // If no visual line assigned, try to find a child Image
            Image[] childImages = GetComponentsInChildren<Image>();
            if (childImages.Length > 1)
            {
                foreach (var img in childImages)
                {
                    if (img != hitAreaImage)
                    {
                        visualLine = img;
                        visualLine.color = normalColor;
                        break;
                    }
                }
            }
        }
    }
    
    public void SetReferenceRect(RectTransform refRect)
    {
        customReferenceRect = refRect;
        effectiveRect = customReferenceRect != null ? customReferenceRect : parentRect;
        
        // Only update position if we're fully initialized
        if (rectTransform != null)
            UpdateVisualPosition();
    }
    
    public void SetNormalizedPosition(float normalizedPos)
    {
        normalizedPosition = Mathf.Clamp01(normalizedPos);
        UpdateVisualPosition();
    }
    
    public void SetDuration(float dur)
    {
        duration = dur;
    }
    
    public float GetTime()
    {
        return normalizedPosition * duration;
    }
    
    void UpdateVisualPosition()
    {
        // Ensure we're initialized
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
        
        if (effectiveRect == null || rectTransform == null) return;
        
        // Account for the effective rect's width properly
        float width = effectiveRect.rect.width;
        float xPos = normalizedPosition * width;
        
        // Ensure marker is centered on the position
        rectTransform.anchoredPosition = new Vector2(xPos, 0);
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        
        if (visualLine != null)
            visualLine.color = draggingColor;
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (effectiveRect == null) return;
        
        // Convert screen position to local position in effective rect
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            effectiveRect,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint
        );
        
        // Account for rect positioning - convert from local point to normalized
        Rect effectiveRectRect = effectiveRect.rect;
        float width = effectiveRectRect.width;
        float leftEdge = effectiveRectRect.xMin;
        
        // Calculate normalized position accounting for the rect's left edge
        float newNormalizedPos = Mathf.Clamp01((localPoint.x - leftEdge) / width);
        
        if (Mathf.Abs(newNormalizedPos - normalizedPosition) > 0.001f)
        {
            normalizedPosition = newNormalizedPos;
            UpdateVisualPosition();
            OnPositionChanged?.Invoke(normalizedPosition);
        }
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        
        if (visualLine != null)
            visualLine.color = normalColor;
    }
}
