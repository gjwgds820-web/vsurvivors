using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

public class UI_PortalIndicatorManager : MonoBehaviour
{
    [Header("UI Settings")]
    public GameObject indicatorPrefab; // ?붿궡??UI ?꾨━??(Image 而댄룷?뚰듃 ?ы븿)
    public RectTransform parentCanvas; // ?몃뵒耳?댄꽣媛 ?앹꽦??罹붾쾭?ㅼ쓽 RectTransform
    public float padding = 50f; // ?붾㈃ 媛?μ옄由??щ갚

    private Camera _mainCamera;
    private EntityManager _entityManager;
    private EntityQuery _portalQuery;
    
    // ?몃뵒耳?댄꽣 ?ㅻ툕?앺듃 ?
    private List<GameObject> _indicatorPool = new List<GameObject>();

    private void Start()
    {
        _mainCamera = Camera.main;
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        
        // CPortalData? ?꾩튂(LocalTransform)瑜?媛吏怨??덈뒗 ?뷀떚??寃??
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
            var CPortalData = _entityManager.GetComponentData<CPortalData>(entity);

            // 寃뚯씠?멸? ?쒖꽦?붾릺???덈뒗 寃쎌슦?먮쭔 泥섎━
            if (CPortalData.IsActive)
            {
                var portalTransform = _entityManager.GetComponentData<LocalTransform>(entity);
                if (UpdateIndicator(activeIndicatorIndex, portalTransform.Position))
                {
                    activeIndicatorIndex++;
                }
            }
        }

        // ?⑥? ????몃뵒耳?댄꽣?ㅼ? 鍮꾪솢?깊솕 (?④?)
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

    private bool UpdateIndicator(int index, Unity.Mathematics.float3 worldPosition)
    {
        if (_mainCamera == null) return false;

        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPosition);

        // ?붾㈃ ???몃? ?먮퀎 (z < 0 ?대㈃ 移대찓???ㅼそ)
        bool isOffScreen = screenPos.z < 0 || screenPos.x < 0 || screenPos.x > Screen.width || screenPos.y < 0 || screenPos.y > Screen.height;

        if (!isOffScreen)
        {
            // ?붾㈃ ?덉뿉 ?덉쑝硫??붿궡???④? (?ъ슜?섏? ?딆쓬)
            return false;
        }

        // 1. ?꾩슂??媛쒖닔留뚰겮 ?留??뺤옣
        if (index >= _indicatorPool.Count)
        {
            var newInst = Instantiate(indicatorPrefab, parentCanvas);
            _indicatorPool.Add(newInst);
        }

        GameObject indicatorObj = _indicatorPool[index];
        indicatorObj.SetActive(true);
        RectTransform indicatorRect = indicatorObj.GetComponent<RectTransform>();

        // 移대찓???ㅼ뿉 ?덈뒗 寃쎌슦(z < 0) 諛⑺뼢 諛섏쟾 諛?蹂댁젙 肄붿쭅
        if (screenPos.z < 0)
        {
            screenPos *= -1;
        }

        // ?붾㈃ 以묒븰??(0, 0)?쇰줈 留욎텛湲?
        Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
        screenPos -= screenCenter;

        // ?먯젏?먯꽌 紐⑺몴瑜??ν븯???쇰뵒??媛곷룄 怨꾩궛
        float angle = Mathf.Atan2(screenPos.y, screenPos.x);
        float slope = Mathf.Tan(angle);

        // ?붾㈃ 以묒븰??湲곗? 媛濡??몃줈 ?쒓퀎??(?щ갚 ?곸슜)
        float halfWidth = (Screen.width / 2f) - padding;
        float halfHeight = (Screen.height / 2f) - padding;

        Vector3 clampedPos = screenPos;

        // X ?쒓퀎???뚰뙆 泥댄겕 諛?Y 鍮꾩쑉 議곗젙
        if (screenPos.x > 0)
        {
            clampedPos = new Vector3(halfWidth, halfWidth * slope, 0f);
        }
        else
        {
            clampedPos = new Vector3(-halfWidth, -halfWidth * slope, 0f);
        }

        // ?꾩뿉??怨꾩궛??Y媛 Y ?쒓퀎?좎쓣 踰쀬뼱?섎뒗吏 ?뺤씤?섍퀬 X 鍮꾩쑉 ?ㅼ떆 議곗젙
        if (clampedPos.y > halfHeight)
        {
            clampedPos = new Vector3(halfHeight / slope, halfHeight, 0f);
        }
        else if (clampedPos.y < -halfHeight)
        {
            clampedPos = new Vector3(-halfHeight / slope, -halfHeight, 0f);
        }

        // ?먮옒 ?ㅽ겕由?醫뚰몴怨?醫뚰븯??0,0 湲곗?)濡?蹂듦뎄
        clampedPos += screenCenter;

        // ?꾩튂 ?곸슜
        indicatorRect.position = clampedPos;
        
        // 諛⑺뼢 ?뚯쟾: ?대?吏 ?ㅽ봽?쇱씠?멸? ?곷떒(Up)??諛붾씪蹂닿퀬 ?덉쑝誘濡?90?꾨? 鍮쇱꽌 蹂댁젙?댁쨳?덈떎.
        float angleDegrees = angle * Mathf.Rad2Deg - 90f;
        indicatorRect.rotation = Quaternion.Euler(0, 0, angleDegrees);

        return true;
    }
}


