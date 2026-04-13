using UnityEngine;
using UnityEngine.EventSystems;

public class InventoryDragHandle : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Header("References")]
    public RectTransform inventoryPanel;

    [Header("Bounds")]
    public float minY = -136f;
    public float maxY = -22f;

    private Vector2 dragOffset;
    private Canvas canvas;
    private RectTransform canvasRect;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? canvas.transform as RectTransform : null;

        if (inventoryPanel == null && transform.parent != null){
            inventoryPanel = transform.parent.GetComponent<RectTransform>();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (inventoryPanel == null || !TryGetLocalPointerPoint(eventData, out Vector2 localPoint)){
            return;
        }

        dragOffset = inventoryPanel.anchoredPosition - localPoint;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (inventoryPanel == null || !TryGetLocalPointerPoint(eventData, out Vector2 localPoint)){
            return;
        }

        float newY = Mathf.Clamp(localPoint.y + dragOffset.y, minY, maxY);
        Vector2 anchoredPosition = inventoryPanel.anchoredPosition;
        anchoredPosition.y = newY;
        inventoryPanel.anchoredPosition = anchoredPosition;
    }

    private bool TryGetLocalPointerPoint(PointerEventData eventData, out Vector2 localPoint)
    {
        if (canvasRect == null){
            localPoint = default;
            return false;
        }

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint);
    }
}
