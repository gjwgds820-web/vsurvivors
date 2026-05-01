using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;

public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    // 오브젝트의 프리팹 어드레스(메모리 키)별로 비활성화된 오브젝트들을 담아둘 대기열(큐)
    private Dictionary<string, Queue<GameObject>> _poolQueue = new Dictionary<string, Queue<GameObject>>();
    
    // 현재 맵(하이어라키)에 나와있는 객체들이 어떤 어드레스 소속인지 추적하는 딕셔너리
    private Dictionary<GameObject, string> _spawnedObjects = new Dictionary<GameObject, string>();

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

    /// <summary>
    /// Addressables를 기반으로 캐싱된 풀에서 오브젝트를 가져오거나 새로 동적 생성합니다.
    /// </summary>
    public async UniTask<GameObject> GetAsync(string address)
    {
        if (!_poolQueue.ContainsKey(address))
        {
            _poolQueue[address] = new Queue<GameObject>();
        }

        // 1. 창고에 남은 여유분이 있다면 꺼내어 활성화
        if (_poolQueue[address].Count > 0)
        {
            GameObject obj = _poolQueue[address].Dequeue();
            obj.SetActive(true);
            return obj;
        }
        else
        {
            // 2. 창고에 남은 게 없다면 Addressables로부터 새로 인스턴스화 (레퍼런스 카운트 1 증가)
            GameObject newObj = await Addressables.InstantiateAsync(address).Task;
            
            if (newObj != null)
            {
                // 나중에 다시 집어넣을 수 있게 출신(Address)을 꼬리표로 달아줌
                _spawnedObjects[newObj] = address;
            }
            return newObj;
        }
    }

    /// <summary>
    /// 오브젝트를 파괴(Destroy)하지 않고 창고(Pool)에 비활성화 상태로 다시 넣습니다.
    /// </summary>
    public void Release(GameObject obj)
    {
        if (obj == null) return;

        // 이 매니저에서 생성된 게 맞는지 확인
        if (_spawnedObjects.TryGetValue(obj, out string address))
        {
            obj.SetActive(false);
            obj.transform.SetParent(this.transform); // 하이어라키를 덜 지저분하게 묶어둡니다
            _poolQueue[address].Enqueue(obj);
        }
        else
        {
            // 풀 시스템을 타지 않고 일반 Instantiate된 객체라면 그냥 파괴
            Destroy(obj);
        }
    }

    /// <summary>
    /// 씬을 넘어가거나 대규모로 비워야할 때 모든 메모리 캐시를 정상적으로 릴리즈합니다.
    /// </summary>
    public void ClearPool()
    {
        foreach (var queue in _poolQueue.Values)
        {
            while (queue.Count > 0)
            {
                GameObject obj = queue.Dequeue();
                if (obj != null)
                {
                    // 단순 Destroy가 아닌 Addressables의 인스턴스 해제 함수 (레퍼런스 카운트 차감 및 메모리 반환)
                    Addressables.ReleaseInstance(obj);
                }
            }
        }
        _poolQueue.Clear();
        _spawnedObjects.Clear();
        
        Debug.Log("[PoolManager] Addressables Object Pool Cleared & Memory Released.");
    }
}
