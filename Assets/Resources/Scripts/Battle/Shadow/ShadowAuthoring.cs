using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Physics.GraphicsIntegration;

public class ShadowAuthoring : MonoBehaviour
{
    [Header("Shadow Stats")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float maxHealth = 50f;

    [Header("Shadow Combat Stats")]
    [SerializeField] private AttackType attackType = AttackType.Melee;
    [SerializeField] private TargetingType targetingType = TargetingType.Nearest;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float attackRadius = 1f;
    [SerializeField] private float attackDuration = 0.5f;
    [SerializeField] private bool isPiercing = true;

    class Baker : Baker<ShadowAuthoring>
    {
        public override void Bake(ShadowAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            AddComponent(entity, new ShadowData
            {
                Index = 0,
                CurrentState = FormationState.Idle,
                StateChangeTimer = 0f,
                MoveSpeed = authoring.moveSpeed
            });

            AddComponent(entity, new ShadowCombatData
            {
                CurrentTarget = Entity.Null,
                ScanTimer = 0f,
                AttackType = authoring.attackType,
                TargetPriority = authoring.targetingType,
                AttackPrefab = GetEntity(authoring.attackPrefab, TransformUsageFlags.Dynamic),
                MaxHealth = authoring.maxHealth,
                CurrentHealth = authoring.maxHealth,
                AttackPower = authoring.attackDamage,
                AttackRange = authoring.attackRange,
                AttackCooldown = authoring.attackCooldown,
                CurrentCooldown = 0f,
                InvincibilityTimer = 0f,
                IsAlive = true
            });

            AddComponent(entity, new TargetPositionData
            {
                Value = float3.zero
            });

            AddComponent(entity, new HitBoxData
            {
                Damage = authoring.attackDamage,
                Radius = authoring.attackRadius,
                Duration = authoring.attackDuration,
                TargetFaction = 0, 
                IsPiercing = authoring.isPiercing
            });

            AddBuffer<DamageBufferElement>(entity);
            AddComponent<ShadowTag>(entity);
            AddComponent<PhysicsGraphicalInterpolationBuffer>(entity);
        }
    }
}