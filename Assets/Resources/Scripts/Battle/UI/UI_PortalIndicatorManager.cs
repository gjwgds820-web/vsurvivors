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
        if (_entityManager == default || _portalQuery.IsEmptyIgnoreFilter || _mainCamera == null)
        {
            HideAllIndicators();
            return;
        }

        var portalEntities = _portalQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        
        // Find Player
        var playerQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerInput>(), ComponentType.ReadOnly<LocalTransform>());
        if (playerQuery.IsEmptyIgnoreFilter) return;
        var playerPos = _entityManager.GetComponentData<LocalTransform>(playerQuery.GetSingletonEntity()).Position;

        // Group by Type to find the closest per type
        Dictionary<int, Entity> closestPortals = new Dictionary<int, Entity>();
        Dictionary<int, float> closestDistances = new Dictionary<int, float>();

        foreach (var entity in portalEntities)
        {
            var cPortalData = _entityManager.GetComponentData<CPortalData>(entity);

            if (cPortalData.IsActive)
            {
                if (cPortalData.RequiredShadows == 0) continue; // QA: Hide 0/0 and Indestructible
                if (cPortalData.PortalID == 42010101) continue; // Hardcode override if needed

                var portalTransform = _entityManager.GetComponentData<LocalTransform>(entity);
                float distSq = Unity.Mathematics.math.distancesq(playerPos, portalTransform.Position);

                int typeKey = cPortalData.PortalID; // Grouping by ID
                if (!closestDistances.ContainsKey(typeKey) || distSq < closestDistances[typeKey])
                {
                    closestDistances[typeKey] = distSq;
                    closestPortals[typeKey] = entity;
                }
            }
        }

        int activeIndicatorIndex = 0;
        foreach (var kvp in closestPortals)
        {
            var entity = kvp.Value;
            var cPortalData = _entityManager.GetComponentData<CPortalData>(entity);
            var portalTransform = _entityManager.GetComponentData<LocalTransform>(entity);
            
            if (UpdateIndicator(activeIndicatorIndex, portalTransform.Position, cPortalData.PortalID))
            {
                activeIndicatorIndex++;
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
