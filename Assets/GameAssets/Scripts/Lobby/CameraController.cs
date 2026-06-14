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
    [SerializeField] private float[] sectionPositions = new float[5] { -2160f, -720f, 720f, 2160f, 3600f };
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float overscrollLimit = 720f;

    [Header("Swipe Settings")]
    [SerializeField] private float swipeThresholdRatio = 0.33f;
    [SerializeField] private float dragSensitivity = 1f;
    [SerializeField] private float dragActivationThreshold = 20f;

    [Header("Input")]
    [SerializeField] private InputSystem_Actions inputAction;

    [Header("Current State")]
    [SerializeField] private int currentSection = 2;
    private Vector3 _targetPosition;
    private Vector3 _velocity = Vector3.zero;
    private Vector3 _originalPosition;
    private bool _isInputBlocked = false;

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

    [Header("Dynamic Layout")]
    [Tooltip("체크 시 해상도에 맞춰 섹션 간격과 배경 너비를 동적으로 조정합니다.")]
    [SerializeField] private bool useDynamicLayout = true;
    [Tooltip("로비의 실제 UI 캔버스")]
    [SerializeField] private Canvas lobbyCanvas;
    [Tooltip("8640 등으로 고정된 로비 배경화면의 RectTransform")]
    [SerializeField] private RectTransform lobbyBackground;
    [Tooltip("섹션의 갯수 (기본 5개)")]
    [SerializeField] private int sectionCount = 5;

    private void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        _screenWidth = Screen.width;

        if (useDynamicLayout)
        {
            ApplyDynamicLayout();
        }

        // 초기 위치 설정
        MoveToSection(currentSection, true);
    }

    private void ApplyDynamicLayout()
    {
        if (lobbyCanvas == null)
        {
            lobbyCanvas = FindAnyObjectByType<Canvas>();
        }

        if (lobbyCanvas != null)
        {
            float worldWidth;
            // 1. 월드 스페이스 캔버스의 경우, 화면 비율(Aspect Ratio)에 따른 실제 월드 너비를 계산해야 합니다.
            if (mainCamera.orthographic)
            {
                worldWidth = mainCamera.orthographicSize * 2f * mainCamera.aspect;
            }
            else
            {
                // 투시(Perspective) 카메라일 경우 캔버스와의 거리를 기반으로 절두체 너비 계산
                float distance = Vector3.Dot(lobbyCanvas.transform.position - mainCamera.transform.position, mainCamera.transform.forward);
                float worldHeight = 2.0f * distance * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                worldWidth = worldHeight * mainCamera.aspect;
            }

            // 2. 월드 너비를 캔버스의 로컬(RectTransform) 너비로 변환
            float sectionCanvasWidth = worldWidth / lobbyCanvas.transform.localScale.x;

            // 3. 배경 너비를 1화면 너비의 sectionCount 배수로 정확히 재조정 (현재 기획: 5배)
            if (lobbyBackground != null)
            {
                lobbyBackground.sizeDelta = new Vector2(sectionCanvasWidth * sectionCount, lobbyBackground.sizeDelta.y);
                
                // 자식UI들이 앵커 기반으로 정렬된 경우, 혹여나 HorizontalLayoutGroup을 쓰는 경우를 위해 강제 업데이트
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(lobbyBackground);
            }

            // 4. 카메라 섹션 좌표 배열을 실제 카메라 월드 너비(worldWidth)에 맞춰 동적 재생성
            // 5개 기준, 가운데(인덱스 2)를 중심으로 배치
            sectionPositions = new float[sectionCount];
            int centerIndex = sectionCount / 2; // 홀수(5)인 경우 인덱스 2
            
            // 기존 고정값 보정 (-2160, -720 등) 
            // 원본 코드는 -2160, -720, 720, 2160, 3600 (간격 1440, 중심은 720) 
            // 이를 동적 worldWidth/2 로 오프셋 보정하여 완벽히 일치시킵니다.
            float baseOffset = worldWidth / 2f; 
            for (int i = 0; i < sectionCount; i++)
            {
                sectionPositions[i] = (i - centerIndex) * worldWidth + baseOffset;
            }
        }
    }

    private void Update()
    {
        if (Pointer.current == null) return;

        Vector2 currentPointerPos = Pointer.current.position.ReadValue();
        
        if (!_isInputBlocked)
        {
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
                        Vector2 delta = currentPointerPos - _touchStartPos;
                        // 수평 스크롤일 때만 카메라를 이동 (수직 스크롤은 무시)
                        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                        {
                            StartDrag();
                        }
                        else
                        {
                            // 수직 스크롤 판정 시 이 터치에 대한 카메라 드래그 인식 취소
                            _isTouchDown = false; 
                        }
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
            sectionPositions[0] - overscrollLimit,
            sectionPositions[sectionPositions.Length - 1] + overscrollLimit
        );

        _targetPosition = new Vector3(newX, mainCamera.transform.position.y, mainCamera.transform.position.z);
    }

    private void EndDrag(Vector2 touchEndPos)
    {
        float swipeDelta = touchEndPos.x - _touchStartPos.x;
        float swipeThreshold = Screen.width * swipeThresholdRatio;

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
        else
        {
            MoveToSection(currentSection); // 오버스크롤 허용 범위 내에서 현재 위치로 이동
        }
    }

    public void MoveToPreviousSection()
    {
        if (currentSection > 0)
        {
            MoveToSection(currentSection - 1);
        }
        else
        {
            MoveToSection(currentSection); // 오버스크롤 허용 범위 내에서 현재 위치로 이동
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

    public void BlockInput(bool block)
    {
        _isInputBlocked = block;

        if (block)
        {
            _isTouchDown = false;
            _isDragging = false;
            _isProcessingDrag = false;
        }
    }
}
