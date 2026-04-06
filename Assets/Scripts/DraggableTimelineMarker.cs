using UnityEngine;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// Minimal draggable marker.
/// Does not modify styling, anchors, pivot, or hitboxes.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DraggableTimelineMarker : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Interaction")]
    [SerializeField] private RectTransform customReferenceRect; // Optional: use this rect instead of parent for width calculations
    
    private RectTransform rectTransform;
    private RectTransform parentRect; // Timeline rect
    private RectTransform effectiveRect; // The rect actually used for calculations (parent or custom)
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
        
        if (rectTransform == null)
        {
            Debug.LogError("[DraggableTimelineMarker] RectTransform component not found!");
            return;
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
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
        
        if (effectiveRect == null || rectTransform == null) return;

        Vector3[] corners = new Vector3[4];
        effectiveRect.GetWorldCorners(corners);
        float worldX = Mathf.Lerp(corners[0].x, corners[3].x, normalizedPosition);

        Vector3 worldPos = rectTransform.position;
        worldPos.x = worldX;
        rectTransform.position = worldPos;
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        // Intentionally left blank: do not alter visual style.
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
        // Intentionally left blank: do not alter visual style.
    }
}
