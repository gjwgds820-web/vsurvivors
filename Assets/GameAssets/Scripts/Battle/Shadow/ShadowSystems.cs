using System;
using System.Numerics;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Collections;
using NUnit.Framework;
using Unity.Entities.UniversalDelegates;
using UnityEngine.UIElements;

#region BrainSystem
[BurstCompile]
public partial struct ShadowBrainSystem : ISystem
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
        
        var playerEntity = _playerQuery.GetSingletonEntity();
        var playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        var playerInput = SystemAPI.GetComponent<PlayerInput>(playerEntity);
        
        bool isPlayerMoving = math.length(playerInput.Move) > 0.01f;
        float3 playerForward = isPlayerMoving ? math.normalize(new float3(playerInput.Move.x, 0, playerInput.Move.y)) : math.forward(playerTransform.Rotation);
        
        _transformLookup.Update(ref state);

        var job = new ShadowBrainJob
        {
            PlayerPos = playerTransform.Position,
            PlayerForward = playerForward,
            IsPlayerMoving = isPlayerMoving,
            LeashDistSq = 15f * 15f,
            ReturnDistSq = 1.5f * 1.5f,
            TransformLookup = _transformLookup
        };

        job.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct ShadowBrainJob : IJobEntity
{
    public float3 PlayerPos;
    public float3 PlayerForward;
    public bool IsPlayerMoving;
    public float LeashDistSq;
    public float ReturnDistSq;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

    private void Execute(ref CShadowData shadow, ref TargetPositionData targetPos, in LocalTransform transform, in TargetingData targetingData)
    {
        float3 currentPos = transform.Position;
        
        // 동적 Formation 편대 오프셋 위치 계산
        float3 right = math.cross(math.up(), PlayerForward);
        float distance = 3f;
        float3 offsetPos = PlayerPos;
        int index = shadow.Index;
        
        if (!IsPlayerMoving)
        {
            if (index < 8)
            {
                float angle = (index / 8f) * math.PI * 2f;
                offsetPos += right * math.cos(angle) * distance + PlayerForward * math.sin(angle) * distance + new float3(0, 1f, 0);
            }
            else
            {
                float angle = ((index - 8) / 12f) * math.PI * 2f;
                offsetPos += right * math.cos(angle) * distance * 2f + PlayerForward * math.sin(angle) * distance * 2f + new float3(0, 1f, 0);
            }
        }
        else
        {
            if (index < 8)
            {
                float angle = (index / 8f) * math.PI * 2f;
                offsetPos += right * math.cos(angle) * distance + PlayerForward * math.sin(angle) * distance + new float3(0, 1f, 0);
            }
            else
            {
                int rIdx = index - 8;
                int row = rIdx < 5 ? 0 : (rIdx < 9 ? 1 : 2);
                int col = rIdx < 5 ? rIdx : (rIdx < 9 ? rIdx - 5 : rIdx - 9);
                int colsInRow = row == 0 ? 5 : (row == 1 ? 4 : 3);

                float xOffset = (col - (colsInRow - 1) / 2f) * distance * 1.5f;
                float zOffset = distance * 1.5f + (row * distance * 1.5f);
                // 이동 중일 땐 뒤로 배열되도록 보정
                offsetPos += PlayerForward * zOffset + right * xOffset + new float3(0, 1f, 0);
            }
        }

        float distToPlayerSq = math.lengthsq(PlayerPos - currentPos);

        // 강제 복귀 (리쉬 거리 이탈)
        if (distToPlayerSq > LeashDistSq)
        {
            shadow.CurrentState = ShadowAIState.ReturnToPlayer;
        }

        if (shadow.CurrentState == ShadowAIState.ReturnToPlayer)
        {
            // 오프셋에 도달하면 대기로 전환
            float distToOffsetSq = math.lengthsq(offsetPos - currentPos);
            if (distToOffsetSq < ReturnDistSq)
            {
                shadow.CurrentState = ShadowAIState.Idle;
            }
            targetPos.Value = offsetPos;
        }
        else
        {
            if (targetingData.CurrentTarget != Entity.Null && TransformLookup.HasComponent(targetingData.CurrentTarget))
            {
                shadow.CurrentState = ShadowAIState.Engage;
                targetPos.Value = TransformLookup[targetingData.CurrentTarget].Position;
                shadow.TargetEnemy = targetingData.CurrentTarget;
            }
            else
            {
                shadow.CurrentState = ShadowAIState.Idle;
                targetPos.Value = offsetPos;
                shadow.TargetEnemy = Entity.Null;
            }
        }
    }
}
#endregion
#region MovementSystem
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
[BurstCompile]
public partial struct ShadowMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        // 주석 복구됨
        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld)) return;
        var collisionWorld = physicsWorld.CollisionWorld;

        // 주석 복구됨
        float separationRadius = 0.8f;
        float separationWeight = 1.0f;

        foreach (var (transform, physicsVelocity, physicsMass, targetPos, shadow, shadowCombatData, entity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<PhysicsMass>, RefRO<TargetPositionData>, RefRO<CShadowData>, RefRO<ShadowCombatData>>().WithEntityAccess())
        {
            if (entity.Index < 0) continue; // 주석 복구됨
            if (!shadowCombatData.ValueRO.IsAlive) continue;

            // 회전 및 물리 충돌로 방해받지 않도록 고정
            physicsMass.ValueRW.InverseInertia = new float3(0, 0, 0);

            float3 currentPos = transform.ValueRO.Position;
            float3 toTarget = targetPos.ValueRO.Value - currentPos;
            toTarget.y = 0;
            float distance = math.length(toTarget);

            bool shouldStopForAttack = false;
            // 공격 사거리에 0.5f 여유를 두어 사거리 끝에서 버벅이며 계속 다가가는 현상 방지
            // 원거리/근거리 상관 없이 사거리 내에 들어오면 정지
            float effectiveRange = shadowCombatData.ValueRO.AttackRange + 0.5f;
            if (shadow.ValueRO.CurrentState == ShadowAIState.Engage)
            {
                if (distance <= effectiveRange)
                {
                    shouldStopForAttack = true;
                }
            }

            if (distance > 0.1f && !shouldStopForAttack)
            {
                float3 moveDir = toTarget / distance;
                float3 lookDir = moveDir; // 회전 처리를 위한 순수 방향

                float speedMultiplier = math.clamp(distance, 1f, 3f);
                float finalSpeed = shadow.ValueRO.MoveSpeed * speedMultiplier;

                // --- [Separation 로직 추�?] ---
                float3 separationVec = float3.zero;
                CollisionFilter filter = CollisionFilter.Default;
                
                NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.Temp);

                if (collisionWorld.CalculateDistance(new PointDistanceInput()
                {
                    Position = currentPos,
                    MaxDistance = separationRadius,
                    Filter = filter
                }, ref hits))
                {
                    int neighborCount = 0;
                    for (int i = 0; i < hits.Length; i++)
                    {
                        float3 neighborPos = hits[i].Position;
                        float3 diff = currentPos - neighborPos;
                        diff.y = 0;
                        float sqrMag = math.lengthsq(diff);

                        if (sqrMag > 0.001f && sqrMag < separationRadius * separationRadius)
                        {
                            float dist = math.sqrt(sqrMag);
                            separationVec += (diff / dist) * (1.0f - (dist / separationRadius));
                            neighborCount++;
                        }
                    }

                    if (neighborCount > 0)
                    {
                        separationVec /= neighborCount;

                        // 주석 복구됨
                        // 주석 복구됨
                        moveDir = math.normalize(lookDir + separationVec * separationWeight);
                    }
                }
                hits.Dispose();
                // -----------------------------

                // 주석 복구됨
                physicsVelocity.ValueRW.Linear = new float3(
                    moveDir.x * finalSpeed,
                    physicsVelocity.ValueRO.Linear.y, // 기존 Y 중력 유지 (하단에서 강제 고정)
                    moveDir.z * finalSpeed
                );

                // 회전 처리를 위한 순수 방향
                quaternion targetRot = quaternion.LookRotationSafe(lookDir, math.up());
                transform.ValueRW.Rotation = math.slerp(transform.ValueRO.Rotation, targetRot, deltaTime * 10f);
            }
            else
            {
                // 타겟에 도착했을 때도 회전을 유지하게 설정
                physicsVelocity.ValueRW.Linear = new float3(0, physicsVelocity.ValueRO.Linear.y, 0);
                
                if (distance > 0.001f)
                {
                    float3 lookDir = math.normalize(toTarget);
                    quaternion targetRot = quaternion.LookRotationSafe(lookDir, math.up());
                    transform.ValueRW.Rotation = math.slerp(transform.ValueRO.Rotation, targetRot, deltaTime * 10f);
                }
            }

            // [수정] 상태와 무관하게 모든 그림자가 절대 땅 속으로 들어가거나 솟아오르지 않게 강제 고정
            float3 fixedPos = transform.ValueRO.Position;
            fixedPos.y = 1f; // 그림자 기본 Y 높이
            transform.ValueRW.Position = fixedPos;

            transform.ValueRW.Rotation.value.x = 0;
            transform.ValueRW.Rotation.value.z = 0;
            transform.ValueRW.Rotation = math.normalize(transform.ValueRW.Rotation);
        }
    }
}
#endregion
#region CombatSystem
[BurstCompile]
public partial struct ShadowCombatSystem : ISystem
{
    private ComponentLookup<LocalTransform> _transformLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _transformLookup = state.GetComponentLookup<LocalTransform>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _transformLookup.Update(ref state);
        float deltaTime = SystemAPI.Time.DeltaTime;

        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (combatData, targetingData, transform, entity) in SystemAPI.Query<RefRW<ShadowCombatData>, RefRO<TargetingData>, RefRO<LocalTransform>>().WithEntityAccess())
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
                        // 원거리 투사체 스폰
                        ecb.SetComponent(hitbox, new LocalTransform
                        {
                            Position = transform.ValueRO.Position + new float3(0, 0.5f, 0), // 그림자 살짝 위에서 발사
                            Scale = 1f,
                            Rotation = quaternion.LookRotationSafe(tPosForAttack - transform.ValueRO.Position, math.up())
                        });

                        if (SystemAPI.HasComponent<ProjectileData>(combatData.ValueRO.AttackPrefab))
                        {
                            var projData = SystemAPI.GetComponent<ProjectileData>(combatData.ValueRO.AttackPrefab);
                            projData.Direction = math.normalize(tPosForAttack - transform.ValueRO.Position);
                            projData.Direction.y = 0;
                            projData.MaxDistance = combatData.ValueRO.AttackRange;
                            ecb.SetComponent(hitbox, projData);
                        }
                        else
                        {
                            // 프리팹에 ProjectileData가 누락된 경우 동적 추가
                            float3 direction = math.normalize(tPosForAttack - transform.ValueRO.Position);
                            direction.y = 0;
                            ecb.AddComponent(hitbox, new ProjectileData { Direction = direction, Speed = 15f, MaxDistance = combatData.ValueRO.AttackRange });
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

        foreach (var (combatData, transform, velocity, entity) in
                 SystemAPI.Query<RefRW<ShadowCombatData>, RefRW<LocalTransform>, RefRW<PhysicsVelocity>>()
                 .WithAll<DeathTag>()
                 .WithEntityAccess())
        {
            if (entity.Index < 0) continue;

            if (combatData.ValueRO.IsAlive)
            {
                combatData.ValueRW.IsAlive = false;
                ecb.RemoveComponent<Unity.Physics.PhysicsCollider>(entity); // 충돌 끄기
            }

            // 충돌체가 없어져서 PhysicsVelocity가 작동하지 않을 수 있으므로 수동으로도 y를 내립니다.
            float3 pos = transform.ValueRO.Position;
            pos.y -= 10f * SystemAPI.Time.DeltaTime;
            transform.ValueRW.Position = pos;

            if (pos.y > -10f)
            {
                velocity.ValueRW.Linear = new float3(0, -10f, 0); // 아래로 빠르게 떨어뜨려 시각적으로 사라지게 함
            }
            else
            {
                velocity.ValueRW.Linear = new float3(0, 0, 0);
                ecb.AddComponent<DestroyEntityTag>(entity); // 완전 사망 처리 (CleanupSystem에서 Visual 처리 후 Entity 자동 파괴됨)
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion











