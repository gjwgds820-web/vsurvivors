using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(HitBoxCollisionSystem))]
[BurstCompile]
public partial struct UnitHealthSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        // Process damage and apply it to HealthData
        foreach (var (health, damageBuffer, entity) in
                 SystemAPI.Query<RefRW<HealthData>, DynamicBuffer<DamageBufferElement>>()
                 .WithNone<DeathTag>()
                 .WithEntityAccess())
        {
            if (entity.Index < 0) continue; 

            bool isPlayerOrShadow = SystemAPI.HasComponent<PlayerData>(entity) || SystemAPI.HasComponent<ShadowCombatData>(entity);

            // Invincibility handling
            if (isPlayerOrShadow && health.ValueRO.InvincibilityTimer > 0f)
            {
                health.ValueRW.InvincibilityTimer -= deltaTime;
                damageBuffer.Clear(); // Ignore damage during invincibility
                continue;
            }

            if (damageBuffer.Length > 0)
            {
                float finalDamage = 0f;
                for (int i = 0; i < damageBuffer.Length; i++)
                {
                    // 각 피격마다 개별적으로 방어력을 적용하여 누적 (한 프레임에 여러 번 맞았을 때의 오차 방지)
                    finalDamage += math.max(0f, damageBuffer[i].Damage - health.ValueRO.DamageReduction);
                }

                health.ValueRW.CurrentHealth -= finalDamage;
                
                if (isPlayerOrShadow)
                {
                    health.ValueRW.InvincibilityTimer = 0.5f; // Set Invincibility after taking hit
                }

                if (SystemAPI.HasComponent<VisualAnimationState>(entity))
                {
                    SystemAPI.GetComponentRW<VisualAnimationState>(entity).ValueRW.TriggerHit = true;
                }

                damageBuffer.Clear();
            }

            if (health.ValueRO.CurrentHealth <= 0f)
            {
                health.ValueRW.CurrentHealth = 0f;
                ecb.AddComponent<DeathTag>(entity);

                if (SystemAPI.HasComponent<VisualAnimationState>(entity))
                {
                    SystemAPI.GetComponentRW<VisualAnimationState>(entity).ValueRW.IsDead = true;
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}




