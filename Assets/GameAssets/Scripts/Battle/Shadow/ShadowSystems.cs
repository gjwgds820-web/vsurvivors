using System;
using System.Numerics;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Collections;
using NUnit.Framework;
using Unity.Entities.UniversalDelegates;
using UnityEngine.UIElements;

#region BehaviorSystem (Brain + Movement)
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TransformSystemGroup))]
[UpdateAfter(typeof(UnitSpatialSystem))]
[BurstCompile]
public partial struct ShadowBehaviorSystem : ISystem
{
    private EntityQuery _playerQuery;
    private ComponentLookup<LocalTransform> _transformLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _playerQuery = SystemAPI.QueryBuilder().WithAll<PlayerInput, LocalTransform>().Build();
        _transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (_playerQuery.IsEmpty) return;
        if (!SystemAPI.TryGetSingleton<SpatialGridData>(out var gridData)) return;
        
        var playerEntity = _playerQuery.GetSingletonEntity();
        var playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        var playerInput = SystemAPI.GetComponent<PlayerInput>(playerEntity);
        
        bool isPlayerMoving = math.length(playerInput.Move) > 0.01f;
        float3 playerForward = isPlayerMoving ? math.normalize(new float3(playerInput.Move.x, 0, playerInput.Move.y)) : math.forward(playerTransform.Rotation);
        
        _transformLookup.Update(ref state);

        var job = new ShadowBehaviorJob
        {
            PlayerPos = playerTransform.Position,
            PlayerForward = playerForward,
            IsPlayerMoving = isPlayerMoving,
            LeashDistSq = 20f * 20f,
            TransformLookup = _transformLookup,
            EnemyGrid = gridData.EnemyGrid,
            ShadowGrid = gridData.ShadowGrid,
            DeltaTime = SystemAPI.Time.DeltaTime
        };

        job.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct ShadowBehaviorJob : IJobEntity
{
    public float3 PlayerPos;
    public float3 PlayerForward;
    public bool IsPlayerMoving;
    public float LeashDistSq;
    [NativeDisableContainerSafetyRestriction]
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    [ReadOnly] public NativeParallelMultiHashMap<int2, Entity> EnemyGrid;
    [ReadOnly] public NativeParallelMultiHashMap<int2, Entity> ShadowGrid;
    public float DeltaTime;
    
    private void Execute(Entity entity, ref LocalTransform transform, ref CShadowData shadow, ref TargetPositionData targetPos, ref TargetingData targetingData, in ShadowCombatData shadowCombatData)
    {
        if (!shadowCombatData.IsAlive) return;

        if (!shadow.Initialized)
        {
            float3 right = math.cross(math.up(), PlayerForward);
            int index = shadow.Index;
            int ring = 0;
            float angle = 0f;
            float radius = 0f;
            if (index < 8) { ring = 0; radius = 2.5f; angle = (index / 8f) * math.PI * 2f; }
            else if (index < 20) { ring = 1; radius = 4.5f; angle = ((index - 8) / 12f) * math.PI * 2f; }
            else { ring = 2; radius = 6.5f; angle = ((index - 20) / 16f) * math.PI * 2f; }
            
            float3 formationOffset = right * math.cos(angle) * radius + PlayerForward * math.sin(angle) * radius;
            shadow.InitialOffset = formationOffset;
            shadow.Initialized = true;
        }

        float3 currentPos = transform.Position;
        float3 targetDest = currentPos;
        float distToPlayerSq = math.distancesq(new float3(PlayerPos.x, 0, PlayerPos.z), new float3(currentPos.x, 0, currentPos.z));

        float searchRadius = shadowCombatData.AttackRange * 1.5f; 
        if (searchRadius < 5f) searchRadius = 5f;
        float searchRadiusSq = searchRadius * searchRadius;

        bool hasValidTarget = shadow.TargetEnemy != Entity.Null && TransformLookup.HasComponent(shadow.TargetEnemy);
        if (hasValidTarget)
        {
            float dSq = math.distancesq(currentPos, TransformLookup[shadow.TargetEnemy].Position);
            if (dSq > searchRadiusSq * 1.5f) 
            {
                shadow.TargetEnemy = Entity.Null;
                hasValidTarget = false;
            }
        }

        if (!hasValidTarget)
        {
            Entity bestEnemy = Entity.Null;
            float bestEnemyDistSq = float.MaxValue;
            int2 cell = SpatialHashConfig.GetCell(currentPos);
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    int2 nCell = cell + new int2(dx, dy);
                    if (EnemyGrid.TryGetFirstValue(nCell, out Entity enEnt, out var enIter))
                    {
                        do
                        {
                            if (!TransformLookup.HasComponent(enEnt)) continue;
                            float dSq = math.distancesq(currentPos, TransformLookup[enEnt].Position);
                            if (dSq < searchRadiusSq && dSq < bestEnemyDistSq)
                            {
                                bestEnemyDistSq = dSq;
                                bestEnemy = enEnt;
                            }
                        } while (EnemyGrid.TryGetNextValue(out enEnt, ref enIter));
                    }
                }
            }
            if (bestEnemy != Entity.Null)
            {
                shadow.TargetEnemy = bestEnemy;
                hasValidTarget = true;
            }
        }

        float3 idleDest = PlayerPos + shadow.InitialOffset;
        idleDest.y = 1f;

        if (distToPlayerSq > LeashDistSq)
        {
            shadow.CurrentState = ShadowAIState.ReturnToPlayer;
            shadow.TargetEnemy = Entity.Null;
            targetDest = idleDest;
        }
        else if (hasValidTarget)
        {
            shadow.CurrentState = ShadowAIState.Engage;
            targetDest = TransformLookup[shadow.TargetEnemy].Position;
        }
        else
        {
            shadow.CurrentState = ShadowAIState.Idle;
            targetDest = idleDest;
        }

        targetingData.CurrentTarget = shadow.TargetEnemy;

        targetPos.Value = targetDest;

        float3 toTarget = targetDest - currentPos;
        toTarget.y = 0; 
        float distance = math.length(toTarget);

        float3 positionResolution = float3.zero;
        float3 separation = float3.zero;
        int separationCount = 0;
        float separationRadius = 1.3f;
        float separationRadiusSq = separationRadius * separationRadius;
        float hardRadius = 0.5f;
        float enemySeparationRadius = 1.1f;
        float enemySeparationRadiusSq = enemySeparationRadius * enemySeparationRadius;
        float enemyHardRadius = 0.45f;
        float enemySeparationWeight = 1.0f;
        
        int2 myCell = SpatialHashConfig.GetCell(currentPos);
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int2 neighborCell = myCell + new int2(dx, dy);
                if (ShadowGrid.TryGetFirstValue(neighborCell, out Entity otherSh, out var shIter))
                {
                    do
                    {
                        if (otherSh == entity) continue;
                        if (!TransformLookup.HasComponent(otherSh)) continue;

                        float3 otherPos = TransformLookup[otherSh].Position;
                        float3 diff = currentPos - otherPos;
                        diff.y = 0;
                        float distSq = math.lengthsq(diff);

                        if (distSq > 0.001f && distSq < separationRadiusSq)
                        {
                            float dist = math.sqrt(distSq);
                            if (dist < hardRadius)
                            {
                                float overlap = hardRadius - dist;
                                positionResolution += (diff / dist) * (overlap * 0.5f);
                            }

                            float pushForce = (separationRadius - dist) / separationRadius;
                            separation += (diff / dist) * pushForce;
                            separationCount++;
                        }
                    } while (ShadowGrid.TryGetNextValue(out otherSh, ref shIter));
                }

                if (EnemyGrid.TryGetFirstValue(neighborCell, out Entity otherEnemy, out var enemyIter))
                {
                    do
                    {
                        if (!TransformLookup.HasComponent(otherEnemy)) continue;

                        float3 otherPos = TransformLookup[otherEnemy].Position;
                        float3 diff = currentPos - otherPos;
                        diff.y = 0;
                        float distSq = math.lengthsq(diff);

                        if (distSq > 0.001f && distSq < enemySeparationRadiusSq)
                        {
                            float dist = math.sqrt(distSq);
                            if (dist < enemyHardRadius)
                            {
                                float overlap = enemyHardRadius - dist;
                                positionResolution += (diff / dist) * (overlap * 0.5f * enemySeparationWeight);
                            }

                            float pushForce = (enemySeparationRadius - dist) / enemySeparationRadius;
                            separation += (diff / dist) * (pushForce * enemySeparationWeight);
                            separationCount++;
                        }
                    } while (EnemyGrid.TryGetNextValue(out otherEnemy, ref enemyIter));
                }
            }
        }

        float resLength = math.length(positionResolution);
        if (resLength > 0.1f) positionResolution = (positionResolution / resLength) * 0.1f;

        transform.Position += positionResolution;
        currentPos = transform.Position;

        bool shouldStop = false;
        if (shadow.CurrentState == ShadowAIState.Engage)
        {
            if (distance <= shadowCombatData.AttackRange * 0.9f) shouldStop = true;
        }
        else if (shadow.CurrentState == ShadowAIState.Idle || shadow.CurrentState == ShadowAIState.ReturnToPlayer)
        {
            if (distance < 0.5f) shouldStop = true;
        }

        if (!shouldStop && distance > 0.05f)
        {
            float3 moveDir = toTarget / distance;
            
            if (separationCount > 0)
            {
                separation /= separationCount;
                float3 combinedDir = moveDir + separation * 2.5f;
                if (math.lengthsq(combinedDir) > 0.001f) moveDir = math.normalize(combinedDir); 
                else moveDir = math.forward();
            }
            
            float speedMultiplier = math.clamp(distance, 1f, shadow.CurrentState == ShadowAIState.ReturnToPlayer ? 4f : 2.5f);
            float finalSpeed = shadow.MoveSpeed * speedMultiplier;

            currentPos += moveDir * finalSpeed * DeltaTime;

            quaternion targetRot = quaternion.LookRotationSafe(moveDir, math.up());
            transform.Rotation = math.slerp(transform.Rotation, targetRot, DeltaTime * 15f);
        }
        else if (distance > 0.001f)
        {
            float3 lookDir = shadow.CurrentState == ShadowAIState.Idle && !IsPlayerMoving ? PlayerForward : math.normalize(toTarget);
            quaternion targetRot = quaternion.LookRotationSafe(lookDir, math.up());
            transform.Rotation = math.slerp(transform.Rotation, targetRot, DeltaTime * 10f);
        }

        currentPos.y = 1f; 
        transform.Position = currentPos;
    }
}
#endregion
#region CombatSystem
[BurstCompile]
public partial struct ShadowCombatSystem : ISystem
{
    private ComponentLookup<LocalToWorld> _transformLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _transformLookup = state.GetComponentLookup<LocalToWorld>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _transformLookup.Update(ref state);
        float deltaTime = SystemAPI.Time.DeltaTime;

        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (combatData, targetingData, transform, entity) in SystemAPI.Query<RefRW<ShadowCombatData>, RefRO<TargetingData>, RefRO<LocalToWorld>>().WithEntityAccess())
        {
            if (entity.Index < 0) continue;
            if (!combatData.ValueRO.IsAlive) continue;

            combatData.ValueRW.CurrentCooldown -= deltaTime;
            
            bool hasTarget = targetingData.ValueRO.CurrentTarget != Entity.Null && _transformLookup.HasComponent(targetingData.ValueRO.CurrentTarget);

            if (hasTarget && combatData.ValueRO.CurrentCooldown <= 0)
            {
                float3 myPosForAttack = transform.ValueRO.Position;
                myPosForAttack.y = 0;
                float3 tPosForAttack = _transformLookup[targetingData.ValueRO.CurrentTarget].Position;
                tPosForAttack.y = 0;

                // 공격 사거리에 약간의 보정값을 더해, 적이 아슬아슬하게 걸쳐서 때릴 때 그림자가 머뭇거리는 현상 방지
                float effectiveRange = combatData.ValueRO.AttackRange + 0.5f;
                if (math.distancesq(myPosForAttack, tPosForAttack) <= effectiveRange * effectiveRange)
                {
                    // 애니메이터 트리거
                    if (SystemAPI.HasComponent<VisualAnimationState>(entity))
                    {
                        var animState = SystemAPI.GetComponent<VisualAnimationState>(entity);
                        animState.TriggerAttack = true;
                        animState.EventAttackHit = false;
                        SystemAPI.SetComponent(entity, animState);
                    }
                    combatData.ValueRW.CurrentCooldown = combatData.ValueRO.AttackCooldown;
                }
            }

            // OnAttackHit 발생 시 실제 투사체/히트박스 스폰
            if (SystemAPI.HasComponent<VisualAnimationState>(entity))
            {
                var animState = SystemAPI.GetComponent<VisualAnimationState>(entity);
                if (animState.EventAttackHit)
                {
                    animState.EventAttackHit = false; // 소비
                    SystemAPI.SetComponent(entity, animState);

                    float3 tPosForAttack = transform.ValueRO.Position + math.forward(transform.ValueRO.Rotation) * 5f; // 기본 전방
                    float finalTPosY = transform.ValueRO.Position.y;

                    int shadowID = 1;
                    if (SystemAPI.HasComponent<ShadowInstanceData>(entity))
                    {
                        shadowID = SystemAPI.GetComponent<ShadowInstanceData>(entity).ShadowID;
                    }

                    if (hasTarget)
                    {
                        tPosForAttack = _transformLookup[targetingData.ValueRO.CurrentTarget].Position;
                        finalTPosY = tPosForAttack.y;
                    }
                    
                    Entity hitbox = ecb.Instantiate(combatData.ValueRO.AttackPrefab);
                    ecb.AddBuffer<HitRecordElement>(hitbox);

                    if (combatData.ValueRO.AttackType == AttackType.Melee)
                    {
                        ecb.SetComponent(hitbox, new LocalTransform
                        {
                            Position = new float3(tPosForAttack.x, finalTPosY, tPosForAttack.z),
                            Scale = 1f,
                            Rotation = quaternion.identity
                        });
                    }
                    else
                    {
                        // 원거리 발사 시 높이 차이로 엉뚱한 방향(위/아래)을 바라보지 않도록 2D 평면 보정
                        float3 myPos2D = transform.ValueRO.Position;
                        myPos2D.y = 0;
                        float3 targetPos2D = tPosForAttack;
                        targetPos2D.y = 0;
                        float3 dir2D = targetPos2D - myPos2D;
                        if (math.lengthsq(dir2D) > 0.001f) dir2D = math.normalize(dir2D);
                        else dir2D = math.forward();

                        // 원거리 투사체 스폰
                        ecb.SetComponent(hitbox, new LocalTransform
                        {
                            Position = transform.ValueRO.Position + new float3(0, 0.5f, 0), // 그림자 살짝 위에서 발사
                            Scale = 1f,
                            Rotation = quaternion.LookRotationSafe(dir2D, math.up())
                        });

                        if (SystemAPI.HasComponent<ProjectileData>(combatData.ValueRO.AttackPrefab))
                        {
                            var projData = SystemAPI.GetComponent<ProjectileData>(combatData.ValueRO.AttackPrefab);
                            projData.Direction = dir2D;
                            projData.MaxDistance = combatData.ValueRO.AttackRange;
                            ecb.SetComponent(hitbox, projData);
                        }
                        else
                        {
                            // 프리팹에 ProjectileData가 누락된 경우 동적 추가
                            ecb.AddComponent(hitbox, new ProjectileData { Direction = dir2D, Speed = 15f, MaxDistance = combatData.ValueRO.AttackRange });
                        }
                    }

                    // 투사체/히트박스의 Visual 연결을 위한 키 제공 (어드레서블 항상 1레벨 기준 IDAttack 용도)
                    int level1ShadowID = (shadowID / 100) * 100 + 1;
                    ecb.AddComponent(hitbox, new ProjectileVisualInfo { ID = level1ShadowID });

                    if (SystemAPI.HasComponent<HitBoxData>(combatData.ValueRO.AttackPrefab))
                    {
                        var dynamicHitbox = SystemAPI.GetComponent<HitBoxData>(combatData.ValueRO.AttackPrefab);
                        dynamicHitbox.Damage = combatData.ValueRO.AttackPower;
                        dynamicHitbox.TargetFaction = 0; // 0이 적(몬스터)
                        ecb.SetComponent(hitbox, dynamicHitbox);
                    }
                    else
                    {
                        // Fallback 추가
                        ecb.AddComponent(hitbox, new HitBoxData
                        {
                            Shape = HitBoxShape.Circle,
                            Damage = combatData.ValueRO.AttackPower,
                            Radius = 3f,
                            Duration = combatData.ValueRO.AttackType == AttackType.Melee ? 0.5f : 10f,
                            TargetFaction = 0,
                            IsPiercing = combatData.ValueRO.AttackType != AttackType.Ranged
                        });
                    }
                }
            }
        } // end foreach

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion

#region ShadowDeathSystem
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(UnitHealthSystem))]
[UpdateBefore(typeof(VisualCleanupSystem))]
[BurstCompile]
public partial struct ShadowDeathSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (combatData, transform, entity) in
                 SystemAPI.Query<RefRW<ShadowCombatData>, RefRW<LocalTransform>>()
                 .WithAll<DeathTag>()
                 .WithNone<DestroyEntityTag>()
                 .WithEntityAccess())
        {
            if (entity.Index < 0) continue;

            if (combatData.ValueRO.IsAlive)
            {
                combatData.ValueRW.IsAlive = false;
            }

            ecb.AddComponent<DestroyEntityTag>(entity); 
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion
















