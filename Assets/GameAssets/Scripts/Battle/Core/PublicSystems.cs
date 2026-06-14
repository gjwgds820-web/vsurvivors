using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Collections;

#region ProjectileMovement
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(HitBoxCollisionSystem))]
[BurstCompile]
public partial struct ProjectileMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (transform, projData, entity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<ProjectileData>>().WithNone<Prefab>().WithEntityAccess())
        {
            float3 dir = math.lengthsq(projData.ValueRO.Direction) > 0f ? math.normalize(projData.ValueRO.Direction) : math.forward(transform.ValueRO.Rotation);
            
            float moveDist = projData.ValueRO.Speed * dt;
            transform.ValueRW.Position += dir * moveDist;
            
            transform.ValueRW.Position.y = 0.5f;

            projData.ValueRW.TravelledDistance += moveDist;

            if (SystemAPI.HasComponent<SpinningProjectileData>(entity))
            {
                var spinData = SystemAPI.GetComponent<SpinningProjectileData>(entity);
                transform.ValueRW.Rotation = math.mul(transform.ValueRW.Rotation, quaternion.AxisAngle(spinData.SpinAxis, spinData.SpinSpeed * dt));
            }

            if (projData.ValueRO.MaxDistance > 0f && projData.ValueRO.TravelledDistance >= projData.ValueRO.MaxDistance)
            {
                ecb.AddComponent(entity, default(DestroyEntityTag));
            }
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion

#region HitBoxCollision
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct HitBoxCollisionSystem : ISystem
{
    private EntityQuery _targetQuery;
    private ComponentLookup<LocalToWorld> _transformLookup;
    private ComponentLookup<CEnemyData> _enemyLookup;
    private ComponentLookup<PlayerData> _playerLookup;
    private ComponentLookup<ShadowCombatData> _shadowLookup;
    private ComponentLookup<ShadowTag> _shadowTagLookup;
    private BufferLookup<DamageBufferElement> _damageBufferLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _targetQuery = SystemAPI.QueryBuilder().WithAll<HealthData, LocalToWorld>().WithNone<DeathTag>().Build();
        _transformLookup = state.GetComponentLookup<LocalToWorld>(true);
        _enemyLookup = state.GetComponentLookup<CEnemyData>(true);
        _playerLookup = state.GetComponentLookup<PlayerData>(true);
        _shadowLookup = state.GetComponentLookup<ShadowCombatData>(true);
        _shadowTagLookup = state.GetComponentLookup<ShadowTag>(true);
        _damageBufferLookup = state.GetBufferLookup<DamageBufferElement>(false);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        double currentTime = SystemAPI.Time.ElapsedTime;
        
        _transformLookup.Update(ref state);
        _enemyLookup.Update(ref state);
        _playerLookup.Update(ref state);
        _shadowLookup.Update(ref state);
        _shadowTagLookup.Update(ref state);
        _damageBufferLookup.Update(ref state);

        var targets = _targetQuery.ToEntityArray(Allocator.TempJob);
        
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        
        var job = new HitBoxCollisionJob
        {
            CurrentTime = currentTime,
            Dt = dt,
            Transforms = _transformLookup,
            Enemies = _enemyLookup,
            Players = _playerLookup,
            Shadows = _shadowLookup,
            ShadowTags = _shadowTagLookup,
            DamageBuffers = _damageBufferLookup,
            Targets = targets,
            Ecb = ecb.AsParallelWriter()
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
        state.Dependency.Complete();
        state.Dependency = targets.Dispose(state.Dependency);
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

[BurstCompile]
public partial struct HitBoxCollisionJob : IJobEntity
{
    public double CurrentTime;
    public float Dt;
    [ReadOnly] public ComponentLookup<LocalToWorld> Transforms;
    [ReadOnly] public ComponentLookup<CEnemyData> Enemies;
    [ReadOnly] public ComponentLookup<PlayerData> Players;
    [ReadOnly] public ComponentLookup<ShadowCombatData> Shadows;
    [ReadOnly] public ComponentLookup<ShadowTag> ShadowTags;
    [ReadOnly] public BufferLookup<DamageBufferElement> DamageBuffers;
    [ReadOnly] public NativeArray<Entity> Targets;
    public EntityCommandBuffer.ParallelWriter Ecb;

    private void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, ref HitBoxData hitbox, ref LocalTransform transform, ref DynamicBuffer<HitRecordElement> hitBuffer)
    {
        float3 currentPos = transform.Position;
        if (math.abs(currentPos.y - 0.5f) > 0.01f)
        {
            currentPos.y = 0.5f;
            transform.Position = currentPos;
        }

        hitbox.Duration -= Dt;
        if (hitbox.Duration <= 0f)
        {
            Ecb.AddComponent(chunkIndex, entity, default(DestroyEntityTag));
            return;
        }

        float3 myPos2D = new float3(transform.Position.x, 0, transform.Position.z);
        float3 hitboxForward = math.forward(transform.Rotation);
        hitboxForward.y = 0;
        if (math.lengthsq(hitboxForward) > 0.01f) hitboxForward = math.normalize(hitboxForward);

        float dotThreshold = (hitbox.Shape == HitBoxShape.Cone) ? math.cos(math.radians(hitbox.Angle * 0.5f)) : -1f;

        for (int i = 0; i < Targets.Length; i++)
        {
            if (Targets[i] == Entity.Null) continue;
            Entity targetEnt = Targets[i];
            
            bool isEnemy = Enemies.HasComponent(targetEnt);
            bool isAlly = Players.HasComponent(targetEnt) || Shadows.HasComponent(targetEnt) || ShadowTags.HasComponent(targetEnt);
            
            if (hitbox.TargetFaction == 0 && !isEnemy) continue;
            if (hitbox.TargetFaction == 1 && !isAlly) continue;

            if (!Transforms.HasComponent(targetEnt)) continue;
            
            float3 targetPos = Transforms[targetEnt].Position;
            float3 targetPos2D = new float3(targetPos.x, 0, targetPos.z);
            
            bool hit = false;
            
            if (hitbox.Shape == HitBoxShape.Circle || hitbox.Shape == HitBoxShape.Cone)
            {
                float distSq = math.distancesq(myPos2D, targetPos2D);
                if (distSq <= hitbox.Radius * hitbox.Radius)
                {
                    if (hitbox.Shape == HitBoxShape.Cone)
                    {
                        float3 toTarget = targetPos2D - myPos2D;
                        if (math.lengthsq(toTarget) > 0.001f)
                        {
                            toTarget = math.normalize(toTarget);
                            if (math.dot(hitboxForward, toTarget) >= dotThreshold) hit = true;
                        }
                        else hit = true; 
                    }
                    else hit = true;
                }
            }
            else if (hitbox.Shape == HitBoxShape.Box)
            {
                float3 toTarget = targetPos2D - myPos2D;
                quaternion invRot = math.inverse(transform.Rotation);
                float3 localToTarget = math.rotate(invRot, toTarget);
                if (math.abs(localToTarget.x) <= hitbox.BoxExtents.x && 
                    math.abs(localToTarget.z) <= hitbox.BoxExtents.z)
                {
                    hit = true;
                }
            }

            if (!hit) continue;

            bool canHit = true;
            int bufferIndex = -1;

            for (int b = 0; b < hitBuffer.Length; b++)
            {
                if (hitBuffer[b].Target == targetEnt)
                {
                    bufferIndex = b;
                    if (hitbox.TickRate <= 0.001f) canHit = false;
                    else if (CurrentTime < hitBuffer[b].LastHitTime + hitbox.TickRate) canHit = false;
                    break;
                }
            }

            if (!canHit) continue;
            if (!DamageBuffers.HasBuffer(targetEnt)) continue;

            Ecb.AppendToBuffer(chunkIndex, targetEnt, new DamageBufferElement { Damage = hitbox.Damage });

            if (bufferIndex == -1) hitBuffer.Add(new HitRecordElement { Target = targetEnt, LastHitTime = CurrentTime });
            else
            {
                var record = hitBuffer[bufferIndex];
                record.LastHitTime = CurrentTime;
                hitBuffer[bufferIndex] = record;
            }

            if (!hitbox.IsPiercing)
            {
                Ecb.AddComponent(chunkIndex, entity, default(DestroyEntityTag));
                return;
            }
            else if (hitbox.MaxPierceCount > 0)
            {
                hitbox.CurrentPierceCount++;
                if (hitbox.CurrentPierceCount >= hitbox.MaxPierceCount)
                {
                    Ecb.AddComponent(chunkIndex, entity, default(DestroyEntityTag));
                    return;
                }
            }
        }
    }
}
#endregion

#region Cleanup
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(VisualCleanupSystem))]
[BurstCompile]
public partial struct CleanupDestroyedEntitySystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        foreach (var (tag, entity) in SystemAPI.Query<RefRO<DestroyEntityTag>>().WithEntityAccess())
        {
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion
