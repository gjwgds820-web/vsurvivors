using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Cysharp.Threading.Tasks;

public class ResourceManager : MonoBehaviour, IAsyncInitializable
{
    public static ResourceManager Instance { get; private set; }

    private Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();
    private Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

    private bool _isInitialized = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public UniTask InitAsync()
    {
        if (_isInitialized) return UniTask.CompletedTask;
        
        // 현재는 메모리 상의 딕셔너리 할당이 끝이므로 즉시 반환하지만,
        // 필요하다면 Addressables 로딩 등을 이곳에 추가할 수 있습니다.
        _isInitialized = true;
        return UniTask.CompletedTask;
    }

    public GameObject LoadPrefab(string path)
    {
        if (_prefabCache.TryGetValue(path, out var prefab))
        {
            return prefab;
        }

        // 경로에 슬래시가 포함되어 있다면, 파일명만 잘라내서 어드레스로 사용
        string addressName = System.IO.Path.GetFileNameWithoutExtension(path);

        try
        {
            prefab = Addressables.LoadAssetAsync<GameObject>(addressName).WaitForCompletion();
            if (prefab != null)
            {
                _prefabCache[path] = prefab;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load Addressable GameObject: {addressName} (original path: {path}) - {e.Message}");
        }

        return prefab;
    }

    public Sprite LoadSprite(string path)
    {
        if (_spriteCache.TryGetValue(path, out var sprite))
        {
            return sprite;
        }

        string addressName = System.IO.Path.GetFileNameWithoutExtension(path);

        try
        {
            sprite = Addressables.LoadAssetAsync<Sprite>(addressName).WaitForCompletion();
            if (sprite != null)
            {
                _spriteCache[path] = sprite;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load Addressable Sprite: {addressName} (original path: {path}) - {e.Message}");
        }

        return sprite;
    }

    /// <summary>
    /// 로드되어 캐시된 모든 어드레서블 에셋 메모리를 해제합니다.
    /// (주로 씬 전환 시 불필요한 메타데이터와 메모리를 비우기 위해 호출합니다)
    /// </summary>
    public void ClearCache()
    {
        foreach (var prefab in _prefabCache.Values)
        {
            if (prefab != null)
            {
                Addressables.Release(prefab);
            }
        }
        _prefabCache.Clear();

        foreach (var sprite in _spriteCache.Values)
        {
            if (sprite != null)
            {
                Addressables.Release(sprite);
            }
        }
        _spriteCache.Clear();

        Debug.Log("[ResourceManager] Addressables Cache Cleared.");
    }

    public GameObject Instantiate(string path, Transform parent = null)
    {
        GameObject prefab = LoadPrefab(path);
        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab, parent);
            instance.name = prefab.name;
            return instance;
        }
        return null;
    }
}