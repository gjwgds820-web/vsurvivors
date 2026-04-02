using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct UnitHealthSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        // Process damage and apply it to HealthData
        foreach (var (health, damageBuffer, entity) in
                 SystemAPI.Query<RefRW<HealthData>, DynamicBuffer<DamageBufferElement>>()
                 .WithNone<DeathTag>()
                 .WithEntityAccess())
        {
            if (entity.Index < 0) continue; 

            // Invincibility handling
            if (health.ValueRO.InvincibilityTimer > 0f)
            {
                health.ValueRW.InvincibilityTimer -= deltaTime;
                damageBuffer.Clear(); // Ignore damage during invincibility
                continue;
            }

            if (damageBuffer.Length > 0)
            {
                float totalDamage = 0f;
                for (int i = 0; i < damageBuffer.Length; i++)
                {
                    totalDamage += damageBuffer[i].Damage;
                }

                float finalDamage = math.max(0f, totalDamage - health.ValueRO.DamageReduction);
                health.ValueRW.CurrentHealth -= finalDamage;
                health.ValueRW.InvincibilityTimer = 0.5f; // Set Invincibility after taking hit

                damageBuffer.Clear();
            }

            if (health.ValueRO.CurrentHealth <= 0f)
            {
                health.ValueRW.CurrentHealth = 0f;
                ecb.AddComponent<DeathTag>(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
