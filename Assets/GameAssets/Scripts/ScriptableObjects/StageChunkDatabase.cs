using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;

[System.Serializable]
public class StageChunkMapping
{
    [Tooltip("배틀 스테이지 ID (예: 41010001)")]
    public int StageID;
    
    [Tooltip("이 스테이지에서 생성될 청크 프리팹들 (Addressables)")]
    public List<AssetReference> ChunkPrefabs; 
}

[CreateAssetMenu(fileName = "StageChunkDatabase", menuName = "Database/Stage Chunk Database")]
public class StageChunkDatabase : ScriptableObject
{
    public List<StageChunkMapping> mappings = new List<StageChunkMapping>();

    public List<AssetReference> GetChunksForStage(int stageId)
    {
        foreach (var mapping in mappings)
        {
            if (mapping.StageID == stageId)
                return mapping.ChunkPrefabs;
        }
        return null;
    }
}
