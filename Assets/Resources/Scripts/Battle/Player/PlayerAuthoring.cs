using Unity.Entities;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    [SerializeField] private int level = 1;
    [SerializeField] private float exp = 0f;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float healthRegenPerSecond = 0f;
    [SerializeField] private float damageReduction = 0f;
    [SerializeField] private float maxShadow = 5f;
    [SerializeField] private float shadowRegenCooldown = 15f;
    [SerializeField] private float summonAnimationDelay = 0.25f;
    [SerializeField] private float magnetismRadius = 3f;
    [SerializeField] private float collectRadius = 0.5f;
    [SerializeField] private GameObject shadowPrefab;

    class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new PlayerInput());
            AddComponent(entity, new PlayerMovementData
            {
                MoveSpeed = authoring.moveSpeed,
                RotationSpeed = authoring.rotationSpeed
            });
            AddComponent(entity, new PlayerData
            {
                Level = authoring.level,
                EXP = authoring.exp,
                HealthRegenPerSecond = authoring.healthRegenPerSecond,
                MaxShadow = authoring.maxShadow,
                CurrentShadow = authoring.maxShadow,
                ShadowRegenCooldown = authoring.shadowRegenCooldown,
                ShadowRegenTimer = authoring.shadowRegenCooldown,
                SummonAnimationDelay = authoring.summonAnimationDelay,
                MagnetismRadius = authoring.magnetismRadius,
                CollectRadius = authoring.collectRadius,
                IsAlive = true,
                InitialShadowsSpawned = false
            });

            AddComponent(entity, new HealthData
            {
                MaxHealth = authoring.maxHealth,
                CurrentHealth = authoring.maxHealth,
                DamageReduction = authoring.damageReduction,
                InvincibilityTimer = 0f
            });

            AddComponent(entity, new ShadowSpawnData
            {
                ShadowPrefab = GetEntity(authoring.shadowPrefab, TransformUsageFlags.Dynamic)
            });
            AddBuffer<ShadowSlotElement>(entity);
            AddBuffer<DamageBufferElement>(entity);
            AddComponent<CameraTargetTag>(entity);
            AddComponent<PhysicsGraphicalInterpolationBuffer>(entity);
        }
    }
}