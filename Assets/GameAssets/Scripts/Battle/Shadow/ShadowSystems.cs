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

#region BehaviorSystem (Brain + Movement)
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct ShadowBehaviorSystem : ISystem
{
    private EntityQuery _playerQuery;
    private ComponentLookup<LocalToWorld> _transformLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _playerQuery = SystemAPI.QueryBuilder().WithAll<PlayerInput, LocalToWorld>().Build();
        _transformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (_playerQuery.IsEmpty) return;
        
        var playerEntity = _playerQuery.GetSingletonEntity();
        var playerTransform = SystemAPI.GetComponent<LocalToWorld>(playerEntity);
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
    [ReadOnly] public ComponentLookup<LocalToWorld> TransformLookup;
    public float DeltaTime;

    private void Execute(Entity entity, ref LocalTransform transform, ref CShadowData shadow, ref TargetPositionData targetPos, in TargetingData targetingData, in ShadowCombatData shadowCombatData)
    {
        if (!shadowCombatData.IsAlive) return;

        float3 currentPos = transform.Position;
        float3 targetDest = currentPos;
        
        bool hasValidTarget = targetingData.CurrentTarget != Entity.Null && TransformLookup.HasComponent(targetingData.CurrentTarget);
        float distToPlayerSq = math.distancesq(new float3(PlayerPos.x, 0, PlayerPos.z), new float3(currentPos.x, 0, currentPos.z));

        // 기획 의도: 다중 원형 진형 (Multi-Ring Formation) 계산
        int index = shadow.Index;
        float3 right = math.cross(math.up(), PlayerForward);
        
        int ring = 0;
        float angle = 0f;
        float radius = 0f;
        
        if (index < 8) { ring = 0; radius = 2.5f; angle = (index / 8f) * math.PI * 2f; }
        else if (index < 20) { ring = 1; radius = 4.5f; angle = ((index - 8) / 12f) * math.PI * 2f; }
        else { ring = 2; radius = 6.5f; angle = ((index - 20) / 16f) * math.PI * 2f; }
        
        float3 formationOffset = right * math.cos(angle) * radius + PlayerForward * math.sin(angle) * radius;
        
        // 이동 중일 땐 그림자들이 플레이어 뒤쪽으로 약간 쏠리는 시각적 효과 (Trailing)
        if (IsPlayerMoving)
        {
            formationOffset -= PlayerForward * (1.5f + ring * 1.5f);
        }
        formationOffset += new float3(0, 1f, 0); // Y축 기본 오프셋
        
        float3 idleDest = PlayerPos + formationOffset;

        // 즉각 상태 결정
        if (distToPlayerSq > LeashDistSq)
        {
            shadow.CurrentState = ShadowAIState.ReturnToPlayer;
            shadow.TargetEnemy = Entity.Null;
            targetDest = idleDest;
        }
        else if (hasValidTarget)
        {
            shadow.CurrentState = ShadowAIState.Engage;
            shadow.TargetEnemy = targetingData.CurrentTarget;
            targetDest = TransformLookup[targetingData.CurrentTarget].Position;
        }
        else
        {
            shadow.CurrentState = ShadowAIState.Idle;
            shadow.TargetEnemy = Entity.Null;
            targetDest = idleDest;
        }

        // 시각 및 다른 시스템 디버깅을 위해 보존
        targetPos.Value = targetDest;

        // 즉각 이동 로직
        float3 toTarget = targetDest - currentPos;
        toTarget.y = 0; 
        float distance = math.length(toTarget);

        bool shouldStop = false;
        if (shadow.CurrentState == ShadowAIState.Engage)
        {
            // 공격 사거리 90% 이내면 정지 후 공격 대기
            if (distance <= shadowCombatData.AttackRange * 0.9f)
                shouldStop = true;
        }
        else if (shadow.CurrentState == ShadowAIState.Idle || shadow.CurrentState == ShadowAIState.ReturnToPlayer)
        {
            if (distance < 0.5f) // 진형 도착 시 미세 떨림 방지 (Deadzone)
                shouldStop = true;
        }

        if (!shouldStop && distance > 0.05f)
        {
            float3 moveDir = toTarget / distance;
            
            // 추격 혹은 복귀 장거리 이동일수록 빨라짐
            float speedMultiplier = math.clamp(distance, 1f, shadow.CurrentState == ShadowAIState.ReturnToPlayer ? 4f : 2.5f);
            float finalSpeed = shadow.MoveSpeed * speedMultiplier;

            currentPos += moveDir * finalSpeed * DeltaTime;

            quaternion targetRot = quaternion.LookRotationSafe(moveDir, math.up());
            transform.Rotation = math.slerp(transform.Rotation, targetRot, DeltaTime * 15f);
        }
        else if (distance > 0.001f) // 멈췄더라도 목표 방향 스무스하게 응시
        {
            float3 lookDir = shadow.CurrentState == ShadowAIState.Idle && !IsPlayerMoving ? PlayerForward : math.normalize(toTarget);
            quaternion targetRot = quaternion.LookRotationSafe(lookDir, math.up());
            transform.Rotation = math.slerp(transform.Rotation, targetRot, DeltaTime * 10f);
        }

        currentPos.y = 1f; // 고정된 Y 높이 유지
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
                 .WithEntityAccess())
        {
            if (entity.Index < 0) continue;

            if (combatData.ValueRO.IsAlive)
            {
                combatData.ValueRW.IsAlive = false;
                 // 충돌 끄기
            }

            // 충돌체가 없어져서 PhysicsVelocity가 작동하지 않을 수 있으므로 수동으로도 y를 내립니다.
            float3 pos = transform.ValueRO.Position;
            pos.y -= 10f * SystemAPI.Time.DeltaTime;
            transform.ValueRW.Position = pos;

            if (pos.y > -10f)
            {
                
            }
            else
            {
                
                ecb.AddComponent<DestroyEntityTag>(entity); // 완전 사망 처리 (CleanupSystem에서 Visual 처리 후 Entity 자동 파괴됨)
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion













