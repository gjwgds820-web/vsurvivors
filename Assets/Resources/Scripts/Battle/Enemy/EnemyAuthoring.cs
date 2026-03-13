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
    [SerializeField] private float hitboxRadius = 0.5f;
    [SerializeField] private float hitboxDuration = 0.5f;
    [SerializeField] private bool isPiercing = false;


    public class EnemyBaker : Baker<EnemyAuthoring>
    {
        public override void Bake(EnemyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            AddComponent(entity, new EnemyData
            {
                Type = authoring.type,
                CurrentState = EnemyState.Move,
                AttackPrefab = GetEntity(authoring.attackPrefab, TransformUsageFlags.Dynamic),
                MaxHealth = authoring.maxHealth,
                CurrentHealth = authoring.maxHealth,
                AttackPower = authoring.attackPower,
                AttackRange = authoring.attackRange,
                AttackCooldown = authoring.attackCooldown,
                CurrentCooldown = 0f,
                MoveSpeed = authoring.moveSpeed,
                SearchTimer = 0f,
                HitboxRadius = authoring.hitboxRadius,
                HitboxDuration = authoring.hitboxDuration,
                IsPiercing = authoring.isPiercing,
                IsAlive = true
            });

            AddComponent(entity, new EnemyTargetData
            {
                CurrentTarget = Entity.Null,
                IsTargetingShadow = false
            });

            AddComponent<EnemyTag>(entity);
            AddComponent<PhysicsGraphicalInterpolationBuffer>(entity);
        }
    }
}