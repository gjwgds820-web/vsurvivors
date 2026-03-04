using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    // UI 업데이트를 위한 이벤트
    public event Action<int> OnSectionChanged;

    [Header("Camera Settings")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float[] sectionPositions = new float[5] { -10f, -5f, 0f, 5f, 10f };
    [SerializeField] private float moveSpeed = 5f;

    [Header("Swipe Settings")]
    [SerializeField] private float swipeThresholdRatio = 0.33f;
    [SerializeField] private float dragSensitivity = 0.01f;
    [SerializeField] private float dragActivationThreshold = 10f;

    [Header("Input")]
    [SerializeField] private InputSystem_Actions inputAction;

    [Header("Current State")]
    [SerializeField] private int currentSection = 2;
    private Vector3 _targetPosition;
    private Vector3 _velocity = Vector3.zero;
    private Vector3 _originalPosition;

    [Header("Touch Input")]
    private Vector2 _touchStartPos;
    private bool _isTouchDown = false;
    private bool _isDragging = false;
    private bool _isProcessingDrag = false;
    private int _lastSectionBeforeDrag;
    private float _screenWidth;

    private void Awake()
    {
        if (inputAction == null)
        {
            inputAction = new InputSystem_Actions();
        }
    }

    private void OnEnable()
    {
        inputAction.Enable();
    }
    private void OnDisable()
    {
        inputAction.Disable();
    }

    private void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        _screenWidth = Screen.width;

        // 초기 위치 설정
        MoveToSection(currentSection, true);
    }

    private void Update()
    {
        if (Pointer.current == null) return;

        Vector2 currentPointerPos = Pointer.current.position.ReadValue();

        // 입력 감지
        if (Pointer.current.press.wasPressedThisFrame)
        {
            if (!_isProcessingDrag)
            {
                _touchStartPos = currentPointerPos;
                _isTouchDown = true;
                _isDragging = false;
            }
        }
        
        // 드래그 처리
        if (_isTouchDown && Pointer.current.press.isPressed)
        {
            if (!_isDragging && !_isProcessingDrag)
            {
                float distance = Vector2.Distance(_touchStartPos, currentPointerPos);
                if (distance > dragActivationThreshold)
                {
                    StartDrag();
                }
            }

            if (_isDragging)
            {
                HandleDrag(currentPointerPos);
            }
        }

        // 입력 종료 감지
        if (Pointer.current.press.wasReleasedThisFrame && _isTouchDown)
        {
            _isTouchDown = false;

            if (_isDragging)
            {
                EndDrag(currentPointerPos);
            }
            else if (!_isProcessingDrag)
            {
                // 드래그 임계값을 넘지 않은 경우 click으로 간주하여 UI 요소와 상호작용
                HandlePress(_touchStartPos);
            }
        }

        UpdateCameraPosition();
    }

    private void StartDrag()
    {
        _isDragging = true;
        _isProcessingDrag = true;
        _lastSectionBeforeDrag = currentSection;
        _originalPosition = mainCamera.transform.position;
    }

    private void HandleDrag(Vector2 currentTouchPos)
    {
        float dragDelta = _touchStartPos.x - currentTouchPos.x;
        float worldDragDelta = dragDelta * dragSensitivity;

        float newX = Mathf.Clamp(
            _originalPosition.x + worldDragDelta,
            sectionPositions[0],
            sectionPositions[sectionPositions.Length - 1]
        );

        _targetPosition = new Vector3(newX, mainCamera.transform.position.y, mainCamera.transform.position.z);
    }

    private void EndDrag(Vector2 touchEndPos)
    {
        float swipeDelta = touchEndPos.x - _touchStartPos.x;
        float swipeThreshold = _screenWidth * swipeThresholdRatio;

        _isDragging = false;

        if (Mathf.Abs(swipeDelta) >= swipeThreshold)
        {
            if (swipeDelta > 0)
            {
                MoveToPreviousSection();
            }
            else
            {
                MoveToNextSection();
            }
        }
        else
        {
            // 드래그가 충분히 멀지 않은 경우 원래 위치로 되돌리기
            MoveToSection(currentSection);
        }

        _isProcessingDrag = false;
    }

    private void HandlePress(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return;

        // 화면 좌표를 기반으로 이벤트 발생
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        List<RaycastResult> raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, raycastResults);

        if (raycastResults.Count > 0)
        {
            // 터치된 가장 앞에 있는 UI 요소에 이벤트 전달
            GameObject targetUI = raycastResults[0].gameObject;
            ExecuteEvents.Execute(targetUI, pointerData, ExecuteEvents.pointerClickHandler);
        }
    }

    private void UpdateCameraPosition()
    {
        Vector3 currentPos = mainCamera.transform.position;
        Vector3 newPos = Vector3.SmoothDamp(currentPos, _targetPosition, ref _velocity, 1f / moveSpeed);
        mainCamera.transform.position = newPos;
    }

    public void MoveToSection(int sectionIndex, bool immediate = false)
    {
        if (sectionIndex < 0 || sectionIndex >= sectionPositions.Length)
        {
            Debug.LogWarning("Invalid section index: " + sectionIndex);
            return;
        }

        currentSection = sectionIndex;
        // 타겟 위치 갱신
        _targetPosition = new Vector3(sectionPositions[sectionIndex], mainCamera.transform.position.y, mainCamera.transform.position.z);

        if (immediate)
        {
            mainCamera.transform.position = _targetPosition;
        }

        OnSectionChanged?.Invoke(currentSection);
    }

    public void MoveToNextSection()
    {
        if (currentSection < sectionPositions.Length - 1)
        {
            MoveToSection(currentSection + 1);
        }
    }

    public void MoveToPreviousSection()
    {
        if (currentSection > 0)
        {
            MoveToSection(currentSection - 1);
        }
    }

    public void OnSectionButtonClick(int sectionIndex)
    {
        if (_isDragging)
        {
            _isDragging = false;
        }

        MoveToSection(sectionIndex);
        _isProcessingDrag = false;
    }

    public int GetCurrentSection()
    {
        return currentSection;
    }
}
