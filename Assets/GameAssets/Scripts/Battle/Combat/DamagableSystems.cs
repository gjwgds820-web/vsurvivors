using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(HitBoxCollisionSystem))]
public partial struct PlayerHealthSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        uint frameTick = (uint)math.floor((float)(SystemAPI.Time.ElapsedTime * 1000.0));
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (playerData, health, damageBuffer, entity) in
                 SystemAPI.Query<RefRO<PlayerData>, RefRW<HealthData>, DynamicBuffer<DamageBufferElement>>()
                 .WithNone<DeathTag>()
                 .WithEntityAccess())
        {
            if (health.ValueRO.InvincibilityTimer > 0f)
            {
                health.ValueRW.InvincibilityTimer -= deltaTime;
                damageBuffer.Clear(); 
                continue;
            }

            if (damageBuffer.Length > 0)
            {
                float finalDamage = 0f;
                bool tookDamage = false;
                for (int i = 0; i < damageBuffer.Length; i++)
                {
                    if (RollDodge(entity, i, frameTick, playerData.ValueRO.DodgeChancePercent))
                    {
                        continue;
                    }

                    finalDamage += math.max(0f, damageBuffer[i].Damage - health.ValueRO.DamageReduction);
                    tookDamage = true;
                }

                health.ValueRW.CurrentHealth -= finalDamage;
                
                if (tookDamage)
                {
                    health.ValueRW.InvincibilityTimer = 0.5f; 
                }

                if (tookDamage && SystemAPI.HasComponent<VisualAnimationState>(entity))
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

    private static bool RollDodge(Entity entity, int hitIndex, uint frameTick, float dodgeChancePercent)
    {
        float dodgeChance = math.clamp(dodgeChancePercent * 0.01f, 0f, 0.95f);
        if (dodgeChance <= 0f)
        {
            return false;
        }

        uint hash = math.hash(new uint4((uint)entity.Index, (uint)entity.Version, (uint)hitIndex, frameTick));
        float random01 = (hash & 0x00FFFFFFu) / 16777216f;
        return random01 < dodgeChance;
    }
}

[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(HitBoxCollisionSystem))]
public partial struct ShadowHealthSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (health, damageBuffer, entity) in
                 SystemAPI.Query<RefRW<HealthData>, DynamicBuffer<DamageBufferElement>>()
                 .WithAll<ShadowCombatData>()
                 .WithNone<DeathTag>()
                 .WithEntityAccess())
        {
            if (health.ValueRO.InvincibilityTimer > 0f)
            {
                health.ValueRW.InvincibilityTimer -= deltaTime;
                damageBuffer.Clear();
                continue;
            }

            if (damageBuffer.Length > 0)
            {
                float finalDamage = 0f;
                for (int i = 0; i < damageBuffer.Length; i++)
                {
                    finalDamage += math.max(0f, damageBuffer[i].Damage - health.ValueRO.DamageReduction);
                }

                health.ValueRW.CurrentHealth -= finalDamage;
                health.ValueRW.InvincibilityTimer = 0.5f;

                if (SystemAPI.HasComponent<VisualAnimationState>(entity) && finalDamage > 0f)
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

[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(HitBoxCollisionSystem))]
public partial struct UnitHealthSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (health, damageBuffer, entity) in
                 SystemAPI.Query<RefRW<HealthData>, DynamicBuffer<DamageBufferElement>>()
                 .WithNone<PlayerData, ShadowCombatData, DeathTag>()
                 .WithEntityAccess())
        {
            if (damageBuffer.Length > 0)
            {
                float finalDamage = 0f;
                for (int i = 0; i < damageBuffer.Length; i++)
                {
                    finalDamage += math.max(0f, damageBuffer[i].Damage - health.ValueRO.DamageReduction);
                }

                health.ValueRW.CurrentHealth -= finalDamage;

                if (SystemAPI.HasComponent<VisualAnimationState>(entity) && finalDamage > 0f)
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
