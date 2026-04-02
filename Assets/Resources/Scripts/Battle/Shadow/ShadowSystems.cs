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

#region FormationSystem
[BurstCompile]
public partial struct ShadowFormationSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var playerQuery = SystemAPI.QueryBuilder().WithAll<PlayerInput, LocalTransform>().Build();
        if(playerQuery.IsEmpty)
            return;
        
        var playerEntity = playerQuery.GetSingletonEntity();
        var playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        var playerInput = SystemAPI.GetComponent<PlayerInput>(playerEntity);

        bool isPlayerMoving = math.length(playerInput.Move) > 0.01f;
        float3 playerForward = isPlayerMoving ? math.normalize(new float3(playerInput.Move.x, 0, playerInput.Move.y)) : math.forward(playerTransform.Rotation);

        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (shadow, targetPos, transform, entity) in SystemAPI.Query<RefRW<CShadowData>, RefRW<TargetPositionData>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            if (entity.Index < 0) continue; // 주석 복구됨

            FormationState targetState = isPlayerMoving ? FormationState.Moveing : FormationState.Idle;
            if (shadow.ValueRO.CurrentState != targetState)
            {
                shadow.ValueRW.StateChangeTimer -= deltaTime;
                if (shadow.ValueRW.StateChangeTimer <= 0)
                {
                    shadow.ValueRW.CurrentState = targetState;
                }
            }
            else
            {
                shadow.ValueRW.StateChangeTimer = 0.3f;
            }

            int index = shadow.ValueRO.Index;
            float3 desiredPos = playerTransform.Position;

            float3 right = math.cross(math.up(), playerForward);
            float distance = 3f;

            if (shadow.ValueRO.CurrentState == FormationState.Idle)
            {
                if (index < 8)
                {
                    float angle = (index / 8f) * math.PI * 2f;
                    desiredPos += right * math.cos(angle) * distance + playerForward * math.sin(angle) * distance + new float3(0, 1f, 0);
                }
                else
                {
                    float angle = ((index - 8) / 12f) * math.PI * 2f;
                    desiredPos += right * math.cos(angle) * distance * 2f + playerForward * math.sin(angle) * distance * 2f + new float3(0, 1f, 0);
                }
            }
            else
            {
                if (index < 8)
                {
                    float angle = (index / 8f) * math.PI * 2f;
                    desiredPos += right * math.cos(angle) * distance + playerForward * math.sin(angle) * distance + new float3(0, 1f, 0);
                }
                else
                {
                    int rIdx = index - 8;
                    int row = rIdx < 5 ? 0 : (rIdx < 9 ? 1 : 2);
                    int col = rIdx < 5 ? rIdx : (rIdx < 9 ? rIdx - 5 : rIdx - 9);
                    int colsInRow = row == 0 ? 5 : (row == 1 ? 4 : 3);

                    float xOffset = (col - (colsInRow - 1) / 2f) * distance * 1.5f;
                    float zOffset = distance * 1.5f + (row * distance * 1.5f);
                    desiredPos += playerForward * zOffset + right * xOffset + new float3(0, 1f, 0);
                }
            }
            targetPos.ValueRW.Value = desiredPos;
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

            if (distance > 0.1f)
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
                    0f, // 수직 이동 차단
                    moveDir.z * finalSpeed
                );

                // 솟아오르는 것 방지
                float3 fixedPos = transform.ValueRO.Position;
                fixedPos.y = 1f;
                transform.ValueRW.Position = fixedPos;

                // 회전 처리를 위한 순수 방향
                quaternion targetRot = quaternion.LookRotationSafe(lookDir, math.up());
                transform.ValueRW.Rotation = math.slerp(transform.ValueRO.Rotation, targetRot, deltaTime * 10f);
                transform.ValueRW.Rotation.value.x = 0;
                transform.ValueRW.Rotation.value.z = 0;
                transform.ValueRW.Rotation = math.normalize(transform.ValueRW.Rotation);
            }
            else
            {
                // 타겟에 도착했을 때도 회전을 유지하게 설정
                physicsVelocity.ValueRW.Linear = new float3(0, 0f, 0);
            }
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

        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        foreach (var (combatData, targetingData, transform, entity) in SystemAPI.Query<RefRW<ShadowCombatData>, RefRO<TargetingData>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            if (entity.Index < 0) continue;
            if (!combatData.ValueRO.IsAlive) continue;

            combatData.ValueRW.CurrentCooldown -= deltaTime;
            
            if (targetingData.ValueRO.CurrentTarget == Entity.Null || !_transformLookup.HasComponent(targetingData.ValueRO.CurrentTarget))
                continue;

            if (combatData.ValueRO.CurrentCooldown <= 0)
            {
                float3 myPosForAttack = transform.ValueRO.Position;
                myPosForAttack.y = 0;
                float3 tPosForAttack = _transformLookup[targetingData.ValueRO.CurrentTarget].Position;
                float finalTPosY = tPosForAttack.y;
                tPosForAttack.y = 0;

                // 공격 사거리에 약간의 보정값(0.5f)을 더해, 적이 아슬아슬하게 걸쳐서 때릴 때 그림자가 바보가 되는 현상 방지
                float effectiveRange = combatData.ValueRO.AttackRange + 0.5f;
                if (math.distancesq(myPosForAttack, tPosForAttack) <= effectiveRange * effectiveRange)
                {
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
                        ecb.SetComponent(hitbox, new LocalTransform
                        {
                            Position = transform.ValueRO.Position,
                            Scale = 1f,
                            Rotation = quaternion.LookRotationSafe(tPosForAttack - transform.ValueRO.Position, math.up())
                        });
                    }
                    ecb.AddComponent(hitbox, new HitBoxData
                    {
                        Damage = combatData.ValueRO.AttackPower,
                        Radius = 3f,
                        Duration = 0.5f,
                        TargetFaction = 0,
                        IsPiercing = true
                    });
                    
                    combatData.ValueRW.CurrentCooldown = combatData.ValueRO.AttackCooldown;
                }
            }
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion

#region ShadowDeathSystem
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitHealthSystem))]
[BurstCompile]
public partial struct ShadowDeathSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

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

            if (transform.ValueRO.Position.y > -10f)
            {
                velocity.ValueRW.Linear = new float3(0, -10f, 0); // 아래로 빠르게 떨어뜨려 시각적으로 사라지게 함
            }
            else
            {
                velocity.ValueRW.Linear = new float3(0, 0, 0);
                ecb.RemoveComponent<DeathTag>(entity); // 사망 처리 완료
            }
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion



