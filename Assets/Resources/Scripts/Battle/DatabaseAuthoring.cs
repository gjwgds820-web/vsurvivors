using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DatabaseAuthoring : MonoBehaviour
{
    [SerializeField] private EnemyDatabase _enemyDatabase;
    [SerializeField] private ShadowDatabase _shadowDatabase;

    class Baker : Baker<DatabaseAuthoring>
    {
        public override void Bake(DatabaseAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            if (authoring._enemyDatabase != null && authoring._enemyDatabase.enemies.Count > 0)
            {
                // Blob 배열 생성 준비
                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<EnemyDatabaseBlob>();
                var arrayBuilder = builder.Allocate(ref root.Enemies, authoring._enemyDatabase.enemies.Count);

                // 데이터 복사
                for (int i = 0; i < authoring._enemyDatabase.enemies.Count; i++)
                {
                    var data = authoring._enemyDatabase.enemies[i];
                    arrayBuilder[i] = new EnemyDefBlob
                    {
                        ID = data.ID,
                        Type = data.Type,
                        MaxHealth = data.MaxHealth,
                        AttackPower = data.AttackPower,
                        AttackRange = data.AttackRange,
                        AttackCooldown = data.AttackCooldown,
                        MoveSpeed = data.MoveSpeed,
                        HitBoxShape = data.HitBoxShape,
                        HitboxRadius = data.HitboxRadius,
                        HitboxDuration = data.HitboxDuration,
                        IsPiercing = data.IsPiercing,
                        IsBoss = data.IsBoss
                    };
                }

                // 빌드한 Blob데이터를 참조로 만들고 컴포넌트로 추가
                var blobRef = builder.CreateBlobAssetReference<EnemyDatabaseBlob>(Allocator.Persistent);
                AddBlobAsset(ref blobRef, out var hash);

                AddComponent(entity, new EnemyDatabaseComponent
                {
                    DatabaseRef = blobRef,
                });

                builder.Dispose();
            }

            if (authoring._shadowDatabase != null && authoring._shadowDatabase.shadows.Count > 0)
            {
                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<ShadowDatabaseBlob>();
                var arrayBuilder = builder.Allocate(ref root.Shadows, authoring._shadowDatabase.shadows.Count);

                for (int i = 0; i < authoring._shadowDatabase.shadows.Count; i++)
                {
                    var data = authoring._shadowDatabase.shadows[i];
                    arrayBuilder[i].ID = data.ID;
                    arrayBuilder[i].AttackType = data.AttackType;
                    arrayBuilder[i].TargetPriority = data.TargetPriority;

                    var levelStatsBuilder = builder.Allocate(ref arrayBuilder[i].LevelStats, data.LevelStats.Length);
                    for (int j = 0; j < data.LevelStats.Length; j++)
                    {
                        var stat = data.LevelStats[j];
                        levelStatsBuilder[j] = new ShadowLevelStatBlob
                        {
                            MaxHealth = stat.MaxHealth,
                            AttackPower = stat.AttackPower,
                            AttackRange = stat.AttackRange,
                            AttackCooldown = stat.AttackCooldown
                        };
                    }
                }

                var blobRef = builder.CreateBlobAssetReference<ShadowDatabaseBlob>(Allocator.Persistent);
                AddBlobAsset(ref blobRef, out var hash);

                AddComponent(entity, new ShadowDatabaseComponent
                {
                    DatabaseRef = blobRef,
                });

                builder.Dispose();
            }
        }
    }
}