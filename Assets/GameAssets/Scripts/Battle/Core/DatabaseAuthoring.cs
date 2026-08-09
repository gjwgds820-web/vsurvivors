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
                // Blob 諛곗뿴 ?앹꽦 以鍮?
                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<EnemyDatabaseBlob>();
                var arrayBuilder = builder.Allocate(ref root.Enemies, authoring._enemyDatabase.enemies.Count);

                // ?곗씠??蹂듭궗
                for (int i = 0; i < authoring._enemyDatabase.enemies.Count; i++)
                {
                    var data = authoring._enemyDatabase.enemies[i];
                    arrayBuilder[i] = new EnemyDefBlob
                    {
                        ID = data.ID,
                        Type = data.Type,
                        Rank = ParseEnemyRank(data),
                        MaxHealth = data.MaxHealth,
                        AttackPower = data.AttackPower,
                        Defence = data.Def,
                        AttackRange = data.AttackRange,
                        AttackCooldown = data.AttackCooldown,
                        MoveSpeed = data.MoveSpeed,
                        IsBoss = data.IsBoss
                    };
                }

                // 鍮뚮뱶??Blob?곗씠?곕? 李몄“濡?留뚮뱾怨?而댄룷?뚰듃濡?異붽?
                var blobRef = builder.CreateBlobAssetReference<EnemyDatabaseBlob>(Allocator.Persistent);
                AddBlobAsset(ref blobRef, out var hash);

                AddComponent(entity, new EnemyDatabaseComponent
                {
                    DatabaseRef = blobRef,
                });

                builder.Dispose();
            }

            if (authoring._enemyDatabase != null && authoring._enemyDatabase.bossPatterns.Count > 0)
            {
                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<BossPatternDatabaseBlob>();

                var patterns = builder.Allocate(ref root.Patterns, authoring._enemyDatabase.bossPatterns.Count);
                for (int i = 0; i < authoring._enemyDatabase.bossPatterns.Count; i++)
                {
                    var data = authoring._enemyDatabase.bossPatterns[i];
                    patterns[i] = new BossPatternDefBlob
                    {
                        BossID = data.BossID,
                        Phase = data.Phase,
                        HealthRate = data.HealthRate,
                        StartSkillID = data.StartSkillID,
                        Skill1 = data.SkillIDs[0],
                        Skill2 = data.SkillIDs[1],
                        Skill3 = data.SkillIDs[2],
                        Skill4 = data.SkillIDs[3]
                    };
                }

                var activeSkills = builder.Allocate(ref root.ActiveSkills, authoring._enemyDatabase.bossActiveSkills.Count);
                for (int i = 0; i < authoring._enemyDatabase.bossActiveSkills.Count; i++)
                {
                    var data = authoring._enemyDatabase.bossActiveSkills[i];
                    activeSkills[i] = new BossActiveSkillDefBlob
                    {
                        SkillID = data.SkillID,
                        Target = data.Target == "player" ? BossSkillTarget.Player : BossSkillTarget.Nearest,
                        AttackRate = data.AttackRate,
                        Cooldown = data.Cooldown,
                        IsForced = data.IsForced,
                        GroggyDuration = data.GroggyDuration,
                        RangeRate = data.RangeRate,
                        Shape = data.Shape
                    };
                }

                var passiveEffects = builder.Allocate(ref root.PassiveEffects, authoring._enemyDatabase.bossPassiveSkillEffects.Count);
                for (int i = 0; i < authoring._enemyDatabase.bossPassiveSkillEffects.Count; i++)
                {
                    var data = authoring._enemyDatabase.bossPassiveSkillEffects[i];
                    passiveEffects[i] = new BossPassiveEffectDefBlob
                    {
                        SkillID = data.SkillID,
                        Stat = data.Stat == "speed" ? BossPassiveStat.MoveSpeed : BossPassiveStat.Attack,
                        BuffValue = data.BuffValue,
                        Duration = data.Duration
                    };
                }

                var blobRef = builder.CreateBlobAssetReference<BossPatternDatabaseBlob>(Allocator.Persistent);
                AddBlobAsset(ref blobRef, out var hash);
                AddComponent(entity, new BossPatternDatabaseComponent { DatabaseRef = blobRef });
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
                    
                    arrayBuilder[i].MaxHealth = data.MaxHealth;
                    arrayBuilder[i].AttackPower = data.AttackPower;
                    arrayBuilder[i].AttackRange = data.AttackRange;
                    arrayBuilder[i].AttackCooldown = data.AttackCooldown;

                    arrayBuilder[i].Element = data.Element;
                    arrayBuilder[i].MaxPierce = data.MaxPierce;
                    arrayBuilder[i].Defence = data.Defence;
                    arrayBuilder[i].MoveSpeed = data.MoveSpeed;
                    arrayBuilder[i].Recognize = data.Recognize;
                    arrayBuilder[i].SkillID = data.SkillID;
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

        private static EnemyRank ParseEnemyRank(EnemyData data)
        {
            if (data.EliteType == "elite") return EnemyRank.Elite;
            return data.IsBoss ? EnemyRank.Boss : EnemyRank.Normal;
        }
    }
}
