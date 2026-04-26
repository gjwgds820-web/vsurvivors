using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

public class UI_PortalIndicatorManager : MonoBehaviour
{
    [Header("UI Settings")]
    public GameObject indicatorPrefab;
    public RectTransform parentCanvas;
    public float padding = 50f;

    private Camera _mainCamera;
    private EntityManager _entityManager;
    private EntityQuery _portalQuery;
    
    private List<GameObject> _indicatorPool = new List<GameObject>();

    private void Start()
    {
        _mainCamera = Camera.main;
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        
        _portalQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CPortalData>(),
            ComponentType.ReadOnly<LocalTransform>()
        );
    }

    private void LateUpdate()
    {
        if (_entityManager == default || _portalQuery.IsEmptyIgnoreFilter)
        {
            HideAllIndicators();
            return;
        }

        var portalEntities = _portalQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        int activeIndicatorIndex = 0;

        foreach (var entity in portalEntities)
        {
            var cPortalData = _entityManager.GetComponentData<CPortalData>(entity);

            if (cPortalData.IsActive)
            {
                // 42020101 (파괴 불가)는 인디케이터를 띄우지 않습니다.
                if (cPortalData.PortalID == 42020101) continue;

                var portalTransform = _entityManager.GetComponentData<LocalTransform>(entity);
                if (UpdateIndicator(activeIndicatorIndex, portalTransform.Position, cPortalData.PortalID))
                {
                    activeIndicatorIndex++;
                }
            }
        }

        for (int i = activeIndicatorIndex; i < _indicatorPool.Count; i++)
        {
            _indicatorPool[i].SetActive(false);
        }

        portalEntities.Dispose();
    }

    private void HideAllIndicators()
    {
        foreach (var indicator in _indicatorPool)
        {
            if (indicator != null && indicator.activeSelf)
                indicator.SetActive(false);
        }
    }

    private bool UpdateIndicator(int index, Unity.Mathematics.float3 worldPosition, int portalID)
    {
        if (_mainCamera == null) return false;

        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPosition);

        bool isOffScreen = screenPos.z < 0 || screenPos.x < 0 || screenPos.x > Screen.width || screenPos.y < 0 || screenPos.y > Screen.height;

        if (!isOffScreen) return false;

        if (index >= _indicatorPool.Count)
        {
            var newInst = Instantiate(indicatorPrefab, parentCanvas);
            _indicatorPool.Add(newInst);
        }

        GameObject indicatorObj = _indicatorPool[index];
        indicatorObj.SetActive(true);
        RectTransform indicatorRect = indicatorObj.GetComponent<RectTransform>();
        
        Image img = indicatorObj.GetComponent<Image>();
        if (img != null)
        {
            if (portalID == 42020103) img.color = Color.red;
            else img.color = Color.yellow;
        }

        if (screenPos.z < 0) screenPos *= -1;

        Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
        screenPos -= screenCenter;

        float angle = Mathf.Atan2(screenPos.y, screenPos.x);
        float slope = Mathf.Tan(angle);

        float halfWidth = (Screen.width / 2f) - padding;
        float halfHeight = (Screen.height / 2f) - padding;

        Vector3 clampedPos = screenPos;

        if (screenPos.x > 0)
        {
            clampedPos = new Vector3(halfWidth, halfWidth * slope, 0f);
        }
        else
        {
            clampedPos = new Vector3(-halfWidth, -halfWidth * slope, 0f);
        }

        if (clampedPos.y > halfHeight)
        {
            clampedPos = new Vector3(halfHeight / slope, halfHeight, 0f);
        }
        else if (clampedPos.y < -halfHeight)
        {
            clampedPos = new Vector3(-halfHeight / slope, -halfHeight, 0f);
        }

        clampedPos += screenCenter;

        indicatorRect.position = clampedPos;
        float angleDegrees = angle * Mathf.Rad2Deg - 90f;
        indicatorRect.rotation = Quaternion.Euler(0, 0, angleDegrees);

        return true;
    }
}
