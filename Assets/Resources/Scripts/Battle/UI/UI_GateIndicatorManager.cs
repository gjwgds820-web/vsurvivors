using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

public class UI_GateIndicatorManager : MonoBehaviour
{
    [Header("UI Settings")]
    public GameObject indicatorPrefab; // 화살표 UI 프리팹 (Image 컴포넌트 포함)
    public RectTransform parentCanvas; // 인디케이터가 생성될 캔버스의 RectTransform
    public float padding = 50f; // 화면 가장자리 여백

    private Camera _mainCamera;
    private EntityManager _entityManager;
    private EntityQuery _gateQuery;
    
    // 인디케이터 오브젝트 풀
    private List<GameObject> _indicatorPool = new List<GameObject>();

    private void Start()
    {
        _mainCamera = Camera.main;
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        
        // GateData와 위치(LocalTransform)를 가지고 있는 엔티티 검색
        _gateQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<GateData>(),
            ComponentType.ReadOnly<LocalTransform>()
        );
    }

    private void LateUpdate()
    {
        if (_entityManager == default || _gateQuery.IsEmptyIgnoreFilter)
        {
            HideAllIndicators();
            return;
        }

        var gateEntities = _gateQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        int activeIndicatorIndex = 0;

        foreach (var entity in gateEntities)
        {
            var gateData = _entityManager.GetComponentData<GateData>(entity);

            // 게이트가 활성화되어 있는 경우에만 처리
            if (gateData.IsActive)
            {
                var gateTransform = _entityManager.GetComponentData<LocalTransform>(entity);
                if (UpdateIndicator(activeIndicatorIndex, gateTransform.Position))
                {
                    activeIndicatorIndex++;
                }
            }
        }

        // 남은 풀의 인디케이터들은 비활성화 (숨김)
        for (int i = activeIndicatorIndex; i < _indicatorPool.Count; i++)
        {
            _indicatorPool[i].SetActive(false);
        }

        gateEntities.Dispose();
    }

    private void HideAllIndicators()
    {
        foreach (var indicator in _indicatorPool)
        {
            if (indicator != null && indicator.activeSelf)
                indicator.SetActive(false);
        }
    }

    private bool UpdateIndicator(int index, Unity.Mathematics.float3 worldPosition)
    {
        if (_mainCamera == null) return false;

        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPosition);

        // 화면 내/외부 판별 (z < 0 이면 카메라 뒤쪽)
        bool isOffScreen = screenPos.z < 0 || screenPos.x < 0 || screenPos.x > Screen.width || screenPos.y < 0 || screenPos.y > Screen.height;

        if (!isOffScreen)
        {
            // 화면 안에 있으면 화살표 숨김 (사용하지 않음)
            return false;
        }

        // 1. 필요한 개수만큼 풀링 확장
        if (index >= _indicatorPool.Count)
        {
            var newInst = Instantiate(indicatorPrefab, parentCanvas);
            _indicatorPool.Add(newInst);
        }

        GameObject indicatorObj = _indicatorPool[index];
        indicatorObj.SetActive(true);
        RectTransform indicatorRect = indicatorObj.GetComponent<RectTransform>();

        // 카메라 뒤에 있는 경우(z < 0) 방향 반전 및 보정 코직
        if (screenPos.z < 0)
        {
            screenPos *= -1;
        }

        // 화면 중앙을 (0, 0)으로 맞추기
        Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
        screenPos -= screenCenter;

        // 원점에서 목표를 향하는 라디안 각도 계산
        float angle = Mathf.Atan2(screenPos.y, screenPos.x);
        float slope = Mathf.Tan(angle);

        // 화면 중앙점 기준 가로/세로 한계선 (여백 적용)
        float halfWidth = (Screen.width / 2f) - padding;
        float halfHeight = (Screen.height / 2f) - padding;

        Vector3 clampedPos = screenPos;

        // X 한계선 돌파 체크 및 Y 비율 조정
        if (screenPos.x > 0)
        {
            clampedPos = new Vector3(halfWidth, halfWidth * slope, 0f);
        }
        else
        {
            clampedPos = new Vector3(-halfWidth, -halfWidth * slope, 0f);
        }

        // 위에서 계산한 Y가 Y 한계선을 벗어나는지 확인하고 X 비율 다시 조정
        if (clampedPos.y > halfHeight)
        {
            clampedPos = new Vector3(halfHeight / slope, halfHeight, 0f);
        }
        else if (clampedPos.y < -halfHeight)
        {
            clampedPos = new Vector3(-halfHeight / slope, -halfHeight, 0f);
        }

        // 원래 스크린 좌표계(좌하단 0,0 기준)로 복구
        clampedPos += screenCenter;

        // 위치 적용
        indicatorRect.position = clampedPos;
        
        // 방향 회전: 이미지 스프라이트가 상단(Up)을 바라보고 있으므로 90도를 빼서 보정해줍니다.
        float angleDegrees = angle * Mathf.Rad2Deg - 90f;
        indicatorRect.rotation = Quaternion.Euler(0, 0, angleDegrees);

        return true;
    }
}
