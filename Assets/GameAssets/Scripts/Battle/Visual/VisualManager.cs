using UnityEngine;
using UnityEngine.AddressableAssets;

public class VisualManager : MonoBehaviour
{
    public static VisualManager Instance;

    [Header("Visual Prefabs")]
    public GameObject PortalVisualPrefab;
    public GameObject EnemyVisualPrefab;
    public GameObject BossVisualPrefab;
    public GameObject ShadowVisualPrefab;

    public GameObject ExpVisualPrefab;
    public GameObject GoldVisualPrefab;
    public GameObject MagnetVisualPrefab;
    public GameObject BombVisualPrefab;

    [Header("Telegraph Prefabs")]
    public GameObject TelegraphConePrefab;
    public GameObject TelegraphBoxPrefab;
    public GameObject TelegraphArrowPrefab;

    [Header("Effect & HitBox Prefabs")]
    [Tooltip("Add visual prefabs for hitboxes and projectiles here. Use the index as the PrefabID in EffectVisualInfo.")]
    public GameObject[] EffectVisualPrefabs;

    private System.Collections.Generic.Dictionary<int, GameObject> _shadowVisualCache = new System.Collections.Generic.Dictionary<int, GameObject>();
    private System.Collections.Generic.Dictionary<int, GameObject> _portalVisualCache = new System.Collections.Generic.Dictionary<int, GameObject>();

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        // 메모리 누수 방지: 어드레서블로 동적 로드한 프리팹 에셋 메모리 해제
        foreach (var prefab in _shadowVisualCache.Values)
        {
            if (prefab != null && prefab != ShadowVisualPrefab)
            {
                Addressables.Release(prefab);
            }
        }
        _shadowVisualCache.Clear();

        foreach (var prefab in _portalVisualCache.Values)
        {
            if (prefab != null && prefab != PortalVisualPrefab)
            {
                Addressables.Release(prefab);
            }
        }
        _portalVisualCache.Clear();
    }

    public GameObject GetShadowVisual(int visualId)
    {
        // 모든 그림자는 1레벨 비주얼 프리팹을 공유합니다.
        // ex) 21010102 (레벨 2) -> 21010101 (레벨 1)
        int baseVisualId = (visualId / 100) * 100 + 1;

        if (_shadowVisualCache.TryGetValue(baseVisualId, out GameObject cachedPrefab))
        {
            return cachedPrefab;
        }

        var pPath = $"{baseVisualId}Visual";
        GameObject prefab = null;

        try
        {
            prefab = Addressables.LoadAssetAsync<GameObject>(pPath).WaitForCompletion();
        }
        catch (System.Exception)
        {
            prefab = null;
        }
        
        if (prefab == null) 
        {
            prefab = ShadowVisualPrefab; // Fallback
        }
        
        // 캐싱하여 다음 호출 시 로딩을 피함
        _shadowVisualCache[baseVisualId] = prefab;
        
        return prefab;
    }

    public GameObject GetPortalVisual(int portalId)
    {
        if (_portalVisualCache.TryGetValue(portalId, out GameObject cachedPrefab))
        {
            return cachedPrefab;
        }

        var pPath = $"{portalId}Visual";
        GameObject prefab = null;

        try
        {
            prefab = Addressables.LoadAssetAsync<GameObject>(pPath).WaitForCompletion();
        }
        catch (System.Exception)
        {
            prefab = null;
        }
        
        if (prefab == null) 
        {
            prefab = PortalVisualPrefab; // Fallback
            if (prefab == null)
            {
                Debug.LogError($"[VisualManager] Portal visual prefab missing for ID: {portalId} and no fallback assigned.");
            }
            else
            {
                Debug.LogWarning($"[VisualManager] Portal visual prefab missing for ID: {portalId}. Using fallback PortalVisualPrefab.");
            }
        }
        
        _portalVisualCache[portalId] = prefab;
        
        return prefab;
    }

    public void SpawnTelegraph(Transform bossTransform, int attackIndex, float duration)
    {
        GameObject prefabToSpawn = null;
        bool isTracking = false;

        switch (attackIndex)
        {
            case 0: // Melee (Cone)
                prefabToSpawn = TelegraphConePrefab;
                isTracking = false; // 부채꼴은 보스 위치 시점에 고정
                break;
            case 1: // Dash (Arrow)
                prefabToSpawn = TelegraphArrowPrefab;
                isTracking = true;  // 화살표는 보스 이동을 실시간 추적
                break;
            case 2: // AxeThrow (Box)
                prefabToSpawn = TelegraphBoxPrefab;
                isTracking = false; // 투척 투사체 경로 고정
                break;
        }

        if (prefabToSpawn != null && duration > 0f)
        {
            GameObject telegraphGo = Instantiate(prefabToSpawn, bossTransform.position, bossTransform.rotation);
            TelegraphUI telegraphUI = telegraphGo.GetComponent<TelegraphUI>();
            if (telegraphUI != null)
            {
                // 약간 바닥에서 위로 띄움 (Y 0.2)
                telegraphUI.Setup(bossTransform, new Vector3(0, 0.2f, 0), duration, isTracking);
            }
        }
    }
}

