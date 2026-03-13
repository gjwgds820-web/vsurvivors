using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class FloatingJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("UI Elements")]
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;
    [Header("Joystick Settings")]
    [SerializeField] private float maxMovement = 150f;

    public Vector2 InputVector { get; private set; }
    private int pointerId = -999;
    private bool isDragging = false;

    private void Start()
    {
        background.gameObject.SetActive(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isDragging) return;
        isDragging = true;
        pointerId = eventData.pointerId;
        background.position = eventData.position;
        background.gameObject.SetActive(true);
        handle.anchoredPosition = Vector2.zero;

        CalculateInput(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != pointerId) return;
        CalculateInput(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != pointerId) return;
        isDragging = false;
        pointerId = -999;
        InputVector = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
        background.gameObject.SetActive(false);
    }

    private void CalculateInput(PointerEventData eventData)
    {
        Vector2 position = eventData.position;
        Vector2 center = RectTransformUtility.WorldToScreenPoint(null, background.position);
        Vector2 offset = position - center;

        Vector2 clampedOffset = Vector2.ClampMagnitude(offset, maxMovement);
        handle.anchoredPosition = clampedOffset;
        InputVector = clampedOffset / maxMovement;
    }
}