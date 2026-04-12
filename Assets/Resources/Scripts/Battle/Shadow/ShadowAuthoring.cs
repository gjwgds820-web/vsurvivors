using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Physics.GraphicsIntegration;

public class ShadowAuthoring : MonoBehaviour
{
    [Header("Shadow Stats")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private ElementType elementType = ElementType.Fire;

    [Header("Shadow Combat Stats")]
    [SerializeField] private AttackType attackType = AttackType.Melee;
    [SerializeField] private TargetingType targetingType = TargetingType.Nearest;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 1f;

    class Baker : Baker<ShadowAuthoring>
    {
        public override void Bake(ShadowAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new CShadowData
            {
                Index = 0,
                CurrentState = FormationState.Idle,
                StateChangeTimer = 0f,
                MoveSpeed = authoring.moveSpeed
            });

            AddComponent(entity, new ShadowCombatData
            {
                AttackType = authoring.attackType,
                AttackPrefab = GetEntity(authoring.attackPrefab, TransformUsageFlags.Dynamic),
                AttackPower = authoring.attackDamage,
                AttackRange = authoring.attackRange,
                AttackCooldown = authoring.attackCooldown,
                CurrentCooldown = 0f,
                IsAlive = true
            });

            AddComponent(entity, new TargetingData
            {
                CurrentTarget = Entity.Null,
                Faction = TargetingFaction.Ally,
                Priority = authoring.targetingType,
                ScanTimer = 0f,
                ScanInterval = 0.2f, // 스캔 주기
                MaxSearchRangeSq = (authoring.attackRange + 0.5f) * (authoring.attackRange + 0.5f),
                MaxFollowRangeSq = (authoring.attackRange + 0.5f) * (authoring.attackRange + 0.5f),
                UseCrowdControl = true
            });

            AddComponent(entity, new HealthData
            {
                MaxHealth = authoring.maxHealth,
                CurrentHealth = authoring.maxHealth,
                DamageReduction = 0f,
                InvincibilityTimer = 0f
            });

            AddComponent(entity, new TargetPositionData
            {
                Value = float3.zero
            });

            AddBuffer<DamageBufferElement>(entity);
            AddComponent<ShadowTag>(entity);
            AddComponent<PhysicsGraphicalInterpolationBuffer>(entity);

            AddComponent(entity, new ShadowInstanceData
            {
                ShadowID = 0,
                CurrentLevel = 1,
                Element = authoring.elementType // Include if ShadowInstanceData has it
            });
        }
    }
}
