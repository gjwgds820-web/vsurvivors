using Unity.Entities;
using UnityEngine;

public class BossAuthoring : MonoBehaviour
{
    [Header("Boss Attack Prefabs")]
    public GameObject MeleeHitBoxPrefab;
    public GameObject AxeHitBoxPrefab;
    public GameObject DashHitBoxPrefab;

    [Header("Skill Bindings")]
    public int MeleeSkillID = 32201011;
    public int AxeSkillID = 32201012;
    public int DashSkillID = 32201013;

    [Header("Boss Config")]
    public float DashSpeed = 15f;
    [Min(0.1f)] public float PatternSizeReference = 3f;
    [Min(0.1f)] public float BodyRadius = 1.5f;
    [Range(1f, 360f)] public float ConeAngle = 90f;
    [Min(0.1f)] public float BoxWidthRate = 0.5f;
    [Min(0f)] public float EnrageDuration = 1.5f;

    public class BossBaker : Baker<BossAuthoring>
    {
        public override void Bake(BossAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // BossTag is likely already added or needed here if not in EnemyAuthoring. Let's add it to be safe, or just ensure it exists.
            AddComponent<BossTag>(entity);
            AddComponent(entity, new BossCombatData { DashSpeed = authoring.DashSpeed });
            AddComponent(entity, new BossAuthoringConfig
            {
                SizeReference = authoring.PatternSizeReference,
                BodyRadius = authoring.BodyRadius,
                ConeAngle = authoring.ConeAngle,
                BoxWidthRate = authoring.BoxWidthRate,
                EnrageDuration = authoring.EnrageDuration
            });

            AddComponent(entity, new BossAttackPrefabs
            {
                MeleeHitBoxPrefab = GetEntity(authoring.MeleeHitBoxPrefab, TransformUsageFlags.Dynamic),
                AxeHitBoxPrefab = GetEntity(authoring.AxeHitBoxPrefab, TransformUsageFlags.Dynamic),
                DashHitBoxPrefab = GetEntity(authoring.DashHitBoxPrefab, TransformUsageFlags.Dynamic)
            });

            var skillPrefabs = AddBuffer<BossSkillPrefabElement>(entity);
            skillPrefabs.Add(new BossSkillPrefabElement
            {
                SkillID = authoring.MeleeSkillID,
                Prefab = GetEntity(authoring.MeleeHitBoxPrefab, TransformUsageFlags.Dynamic),
                AnimationIndex = 0,
                ExecutionType = BossSkillExecutionType.HitBox
            });
            skillPrefabs.Add(new BossSkillPrefabElement
            {
                SkillID = authoring.AxeSkillID,
                Prefab = GetEntity(authoring.AxeHitBoxPrefab, TransformUsageFlags.Dynamic),
                AnimationIndex = 2,
                ExecutionType = BossSkillExecutionType.Projectile
            });
            skillPrefabs.Add(new BossSkillPrefabElement
            {
                SkillID = authoring.DashSkillID,
                Prefab = GetEntity(authoring.DashHitBoxPrefab, TransformUsageFlags.Dynamic),
                AnimationIndex = 1,
                ExecutionType = BossSkillExecutionType.Charge
            });
        }
    }
}
