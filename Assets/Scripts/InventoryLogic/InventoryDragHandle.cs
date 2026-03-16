using UnityEngine;
using UnityEngine.EventSystems;

public class InventoryDragHandle : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Header("References")]
    public RectTransform inventoryPanel;  // Drag InventoryPanel here

    [Header("Bounds")]
    public float minY = -136f;     // Lowest position (fully hidden, just handle visible)
    public float maxY = -22;   // Highest position (fully open)

    private Vector2 dragOffset;
    private Canvas canvas;

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>();

        // Auto detect panel if not assigned
        if (inventoryPanel == null)
            inventoryPanel = transform.parent.GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Calculate offset between panel position and click position
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );
        dragOffset = inventoryPanel.anchoredPosition - localPoint;
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );

        // Calculate new Y position
        float newY = localPoint.y + dragOffset.y;

        // Clamp between min and max
        newY = Mathf.Clamp(newY, minY, maxY);

        // Only move on Y axis
        inventoryPanel.anchoredPosition = new Vector2(
            inventoryPanel.anchoredPosition.x,
            newY
        );
    }
}