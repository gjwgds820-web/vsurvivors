using UnityEngine;
using UnityEngine.AddressableAssets;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

public class MapManager : MonoBehaviour
{
    [Header("Map Base Setup")]
    [SerializeField] private StageChunkDatabase chunkDatabase;
    
    [Header("Boundary Generation")]
    [Tooltip("모서리와 코너 장애물 자동 생성 활성화")]
    [SerializeField] private bool generateBoundaries = true;
    [Tooltip("내부 맵의 가로(X축) 타일/청크 개수")]
    [SerializeField] private int mapGridWidth = 3;
    [Tooltip("내부 맵의 세로(Z축) 타일/청크 개수")]
    [SerializeField] private int mapGridHeight = 3;
    [Tooltip("타일 1개의 폭 치수 (예: 50m)")]
    [SerializeField] private float tileSize = 50f;

    [Space(5)]
    [Tooltip("직선 외곽 벽 (Z+ 방향을 바라보게 제작된 프리팹)")]
    [SerializeField] private AssetReference edgePrefab;
    [Tooltip("코너 외곽 벽")]
    [SerializeField] private AssetReference cornerPrefab;

    [Header("Fallback Generation")]
    [Tooltip("에러 시 임시 맵 생성에 사용할 어드레서블 키 (Plane)")]
    [SerializeField] private string fallbackPlaneKey = "Chunk_Plane";
    [Tooltip("에러 시 임시 맵 생성에 사용할 어드레서블 키 (Edge)")]
    [SerializeField] private string fallbackEdgeKey = "Chunk_Edge";
    [Tooltip("에러 시 임시 맵 생성에 사용할 어드레서블 키 (Corner)")]
    [SerializeField] private string fallbackCornerKey = "Chunk_Corner";
    
    private List<GameObject> activeChunks = new List<GameObject>();

    private void Start()
    {
        InitializeMapAsync().Forget();
    }

    private async UniTask InitializeMapAsync()
    {
        if (DataManager.Instance == null || DataManager.Instance.currentUserData == null)
        {
            Debug.LogWarning("[MapManager] DataManager is not initialized. Cannot fetch current StageID.");
            return;
        }

        int currentStageID = DataManager.Instance.currentUserData.CurrentStage;
        
        if (chunkDatabase == null)
        {
            Debug.LogWarning("[MapManager] StageChunkDatabase is not assigned in the inspector. Triggering Fallback.");
            await SpawnFallbackMapAsync();
            return;
        }

        var chunksToLoad = chunkDatabase.GetChunksForStage(currentStageID);
        if (chunksToLoad == null || chunksToLoad.Count == 0)
        {
            Debug.LogWarning($"[MapManager] No chunks matching StageID {currentStageID} found. Triggering Fallback.");
            await SpawnFallbackMapAsync();
            return;
        }

        Debug.Log($"[MapManager] Start Spawning {chunksToLoad.Count} Chunks for Stage: {currentStageID}");

        bool loadFailed = false;

        // 1. 내부 맵 조각 로드
        foreach (var chunkRef in chunksToLoad)
        {
            if (chunkRef != null && chunkRef.RuntimeKeyIsValid())
            {
                try
                {
                    var chunkInstance = await Addressables.InstantiateAsync(chunkRef, transform).ToUniTask();
                    chunkInstance.transform.localPosition = Vector3.zero;
                    
                    // 런타임 최적화: 일반 콜라이더들을 ECS Physics 엔티티로 변환 후 컴포넌트 제거
                    MapColliderConverter.ConvertColliders(chunkInstance);

                    activeChunks.Add(chunkInstance);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[MapManager] Failed to load chunk asset. Triggering Fallback. Error: {ex.Message}");
                    loadFailed = true;
                    break;
                }
            }
            else
            {
                Debug.LogWarning("[MapManager] Invalid chunk reference found. Triggering Fallback.");
                loadFailed = true;
                break;
            }
        }

        if (loadFailed)
        {
            // 기존 로드된 것들 정리
            CleanUpActiveChunks();
            await SpawnFallbackMapAsync();
            return;
        }
        
        // 2. 맵 외곽선(바운더리) 장벽 타일 자동 생성 수행
        if (generateBoundaries)
        {
            await SpawnBoundariesAsync();
        }

        Debug.Log($"[MapManager] Finished spawning stage chunks and boundaries.");
    }

    private async UniTask SpawnBoundariesAsync()
    {
        if (edgePrefab == null || cornerPrefab == null) return;
        
        float halfW = (mapGridWidth - 1) / 2f;
        float halfH = (mapGridHeight - 1) / 2f;

        // 경계선이 위치할 그리드 좌표 (내부 맵 바로 바깥쪽 타일 인덱스)
        float boundX = halfW + 1;
        float boundZ = halfH + 1;

        // 1. 코너 소환
        // (기준: 코너 프리팹의 0,0,0 회전 상태는 '좌측 하단(Bottom-Left)' 모서리에 쓰인다고 가정)
        await SpawnTileAsync(cornerPrefab, new Vector3(-boundX * tileSize, 0, boundZ * tileSize), Quaternion.Euler(0, 90, 0));  // Top-Left
        await SpawnTileAsync(cornerPrefab, new Vector3(boundX * tileSize, 0, boundZ * tileSize), Quaternion.Euler(0, 180, 0));  // Top-Right
        await SpawnTileAsync(cornerPrefab, new Vector3(-boundX * tileSize, 0, -boundZ * tileSize), Quaternion.Euler(0, 0, 0));  // Bottom-Left
        await SpawnTileAsync(cornerPrefab, new Vector3(boundX * tileSize, 0, -boundZ * tileSize), Quaternion.Euler(0, 270, 0)); // Bottom-Right

        // 2. 직선 외곽 벽 소환
        // 상단 / 하단 모서리
        for (float x = -halfW; x <= halfW; x += 1f)
        {
            await SpawnTileAsync(edgePrefab, new Vector3(x * tileSize, 0, boundZ * tileSize), Quaternion.Euler(0, 180, 0)); // Top 벽 (안쪽 -Z 보기)
            await SpawnTileAsync(edgePrefab, new Vector3(x * tileSize, 0, -boundZ * tileSize), Quaternion.Euler(0, 0, 0));  // Bottom 벽 (안쪽 +Z 보기)
        }

        // 좌측 / 우측 모서리
        for (float z = -halfH; z <= halfH; z += 1f)
        {
            await SpawnTileAsync(edgePrefab, new Vector3(-boundX * tileSize, 0, z * tileSize), Quaternion.Euler(0, 90, 0)); // Left 벽 (안쪽 +X 보기)
            await SpawnTileAsync(edgePrefab, new Vector3(boundX * tileSize, 0, z * tileSize), Quaternion.Euler(0, -90, 0)); // Right 벽 (안쪽 -X 보기)
        }
    }

    private async UniTask SpawnTileAsync(AssetReference prefabRef, Vector3 pos, Quaternion rot)
    {
        if (prefabRef == null || !prefabRef.RuntimeKeyIsValid()) return;
        var instance = await Addressables.InstantiateAsync(prefabRef, pos, rot, transform).ToUniTask();
        
        // 외곽선 절벽/바위 등의 콜라이더도 ECS 엔진으로 구동
        MapColliderConverter.ConvertColliders(instance);
        
        activeChunks.Add(instance);
    }

    private void OnDestroy()
    {
        CleanUpActiveChunks();
    }

    private void CleanUpActiveChunks()
    {
        foreach(var chunk in activeChunks)
        {
            if (chunk != null)
            {
                Addressables.ReleaseInstance(chunk);
            }
        }
        activeChunks.Clear();
    }

    private async UniTask SpawnFallbackMapAsync()
    {
        Debug.Log("[MapManager] Generating Fallback Temp Map using string keys...");
        
        float halfW = (mapGridWidth - 1) / 2f;
        float halfH = (mapGridHeight - 1) / 2f;

        // 1. 임시 바닥 생성 (Center Grid)
        for (float x = -halfW; x <= halfW; x += 1f)
        {
            for (float z = -halfH; z <= halfH; z += 1f)
            {
                await SpawnTileStringAsync(fallbackPlaneKey, new Vector3(x * tileSize, 0, z * tileSize), Quaternion.identity);
            }
        }

        // 2. 바운더리 생성
        if (generateBoundaries)
        {
            float boundX = halfW + 1;
            float boundZ = halfH + 1;

            // 코너
            await SpawnTileStringAsync(fallbackCornerKey, new Vector3(-boundX * tileSize, 0, boundZ * tileSize), Quaternion.Euler(0, 90, 0));  // Top-Left
            await SpawnTileStringAsync(fallbackCornerKey, new Vector3(boundX * tileSize, 0, boundZ * tileSize), Quaternion.Euler(0, 180, 0));  // Top-Right
            await SpawnTileStringAsync(fallbackCornerKey, new Vector3(-boundX * tileSize, 0, -boundZ * tileSize), Quaternion.Euler(0, 0, 0));  // Bottom-Left
            await SpawnTileStringAsync(fallbackCornerKey, new Vector3(boundX * tileSize, 0, -boundZ * tileSize), Quaternion.Euler(0, 270, 0)); // Bottom-Right

            // 상단 / 하단 모서리
            for (float x = -halfW; x <= halfW; x += 1f)
            {
                await SpawnTileStringAsync(fallbackEdgeKey, new Vector3(x * tileSize, 0, boundZ * tileSize), Quaternion.Euler(0, 180, 0));
                await SpawnTileStringAsync(fallbackEdgeKey, new Vector3(x * tileSize, 0, -boundZ * tileSize), Quaternion.Euler(0, 0, 0)); 
            }

            // 좌측 / 우측 모서리
            for (float z = -halfH; z <= halfH; z += 1f)
            {
                await SpawnTileStringAsync(fallbackEdgeKey, new Vector3(-boundX * tileSize, 0, z * tileSize), Quaternion.Euler(0, 90, 0)); 
                await SpawnTileStringAsync(fallbackEdgeKey, new Vector3(boundX * tileSize, 0, z * tileSize), Quaternion.Euler(0, -90, 0));
            }
        }

        Debug.Log("[MapManager] Fallback Map Generation Complete.");
    }

    private async UniTask SpawnTileStringAsync(string key, Vector3 pos, Quaternion rot)
    {
        if (string.IsNullOrEmpty(key)) return;
        try
        {
            var instance = await Addressables.InstantiateAsync(key, pos, rot, transform).ToUniTask();
            MapColliderConverter.ConvertColliders(instance);
            activeChunks.Add(instance);
        }
        catch(System.Exception ex)
        {
            Debug.LogError($"[MapManager] Fallback generation failed to load key {key}: {ex.Message}");
        }
    }
}
