using Unity.Entities;
using UnityEngine;

public class GameDirectorAuthoring : MonoBehaviour
{
    [SerializeField] private GameObject gatePrefab;
    [SerializeField] private GameObject enemyPrefab;

    public class GameDirectorBaker : Baker<GameDirectorAuthoring>
    {
        public override void Bake(GameDirectorAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            
            AddComponent(entity, new GameDirectorData
            {
                GatePrefab = GetEntity(authoring.gatePrefab, TransformUsageFlags.Dynamic),
                EnemyPrefab = GetEntity(authoring.enemyPrefab, TransformUsageFlags.Dynamic),
                WaveTimer = 5f,
                CurrentWave = 0
            });
            AddComponent<SubSceneVisualModel>(entity);
        }
    }
}