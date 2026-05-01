using Unity.Entities;
using UnityEngine;
using Unity.Physics.GraphicsIntegration;

public class EnemyAuthoring : MonoBehaviour
{
    [Header("Enemy Stats")]
    [SerializeField] private EnemyType type = EnemyType.Melee;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float attackPower = 10f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private bool isBoss = false;

    public class EnemyBaker : Baker<EnemyAuthoring>
    {
        public override void Bake(EnemyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new CEnemyData
            {
                ID = 0, // ID는 나중에 데이터베이스에서 할당
                Type = authoring.type,
                CurrentState = EnemyState.Move,
                AttackPrefab = GetEntity(authoring.attackPrefab, TransformUsageFlags.Dynamic),
                AttackPower = authoring.attackPower,
                AttackRange = authoring.attackRange,
                AttackCooldown = authoring.attackCooldown,
                CurrentCooldown = 0f,
                MoveSpeed = authoring.moveSpeed,
                IsBoss = authoring.isBoss,
                IsAlive = true
            });

            AddComponent(entity, new HealthData
            {
                MaxHealth = authoring.maxHealth,
                CurrentHealth = authoring.maxHealth,
                DamageReduction = 0f,
                InvincibilityTimer = 0f
            });

            AddComponent(entity, new TargetingData
            {
                CurrentTarget = Entity.Null,
                Faction = TargetingFaction.Enemy,
                Priority = TargetingType.Nearest,
                ScanTimer = 0f,
                ScanInterval = 0.3f,
                MaxSearchRangeSq = authoring.isBoss ? float.MaxValue : (authoring.attackRange * 10f) * (authoring.attackRange * 10f),
                MaxFollowRangeSq = float.MaxValue, // Enemies follow forever basically
                UseCrowdControl = true
            });

            AddComponent<EnemyTag>(entity);
            AddComponent<PhysicsGraphicalInterpolationBuffer>(entity);
            AddBuffer<DamageBufferElement>(entity);
        }
    }
}
