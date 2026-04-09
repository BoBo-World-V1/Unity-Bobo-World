using UnityEngine;
using UnityEngine.EventSystems;

public class InventoryDragHandle : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Header("References")]
    public RectTransform inventoryPanel;  // Drag InventoryPanel here

    [Header("Bounds")]
    public float minY = -136f;     // Lowest position (fully hidden, just handle visible)
    public float maxY = -22f;   // Highest position (fully open)

    private Vector2 dragOffset;
    private Canvas canvas;
    private RectTransform canvasRect;

    void Awake(){
        canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? canvas.transform as RectTransform : null;

        // Auto detect panel if not assigned
        if (inventoryPanel == null && transform.parent != null)
            inventoryPanel = transform.parent.GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData){
        if (!TryGetLocalPointerPoint(eventData, out Vector2 localPoint)) return;

        // Calculate offset between panel position and click position
        dragOffset = inventoryPanel.anchoredPosition - localPoint;
    }

    public void OnDrag(PointerEventData eventData){
        if (inventoryPanel == null) return;
        if (!TryGetLocalPointerPoint(eventData, out Vector2 localPoint)) return;

        // Calculate new Y position
        float newY = Mathf.Clamp(localPoint.y + dragOffset.y, minY, maxY);

        // Only move on Y axis
        Vector2 anchoredPosition = inventoryPanel.anchoredPosition;
        anchoredPosition.y = newY;
        inventoryPanel.anchoredPosition = anchoredPosition;
    }

    private bool TryGetLocalPointerPoint(PointerEventData eventData, out Vector2 localPoint){
        if (canvasRect == null){
            localPoint = default;
            return false;
        }

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint
        );
    }
}