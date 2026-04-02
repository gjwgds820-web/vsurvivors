using Unity.Entities;
using UnityEngine;

public class GameDirectorAuthoring : MonoBehaviour
{
    [SerializeField] private GameObject gatePrefab;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject bossPrefab;

    [Header("Testing & Balance")]
    public float enemySpawnInterval = 5f;
    public float bossSpawnInterval = 300f;
    public float expRequirementBase = 100f;

    public class GameDirectorBaker : Baker<GameDirectorAuthoring>
    {
        public override void Bake(GameDirectorAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            
            AddComponent(entity, new GameDirectorData
            {
                GatePrefab = GetEntity(authoring.gatePrefab, TransformUsageFlags.Dynamic),
                EnemyPrefab = GetEntity(authoring.enemyPrefab, TransformUsageFlags.Dynamic),
                BossPrefab = GetEntity(authoring.bossPrefab, TransformUsageFlags.Dynamic),
                WaveTimer = 5f,
                EnemySpawnTimer = 1f,
                EnemySpawnInterval = authoring.enemySpawnInterval,
                CurrentWave = 1,
                CurrentPhase = GamePhase.NormalWave,
                GlobalTimer = 0f,
                BossTimer = 180f,
                BossSpawnInterval = authoring.bossSpawnInterval,
                ExpRequirementBase = authoring.expRequirementBase
            });
            AddComponent<SubSceneVisualModel>(entity);
        }
    }
}
