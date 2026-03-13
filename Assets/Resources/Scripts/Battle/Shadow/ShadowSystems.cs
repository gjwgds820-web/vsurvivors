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

        foreach (var (shadow, targetPos, transform) in SystemAPI.Query<RefRW<ShadowData>, RefRW<TargetPositionData>, RefRO<LocalTransform>>())
        {
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

            if (shadow.ValueRO.CurrentState == FormationState.Idle)
            {
                if (index < 8)
                {
                    float angle = (index / 8f) * math.PI * 2f;
                    desiredPos += new float3(math.cos(angle) * 2f, 0, math.sin(angle) * 2f);
                }
                else
                {
                    float angle = ((index - 8) / 12f) * math.PI * 2f;
                    desiredPos += new float3(math.cos(angle) * 4f, 0, math.sin(angle) * 4f);
                }
            }
            else
            {
                if (index < 8)
                {
                    float angle = (index / 8f) * math.PI * 2f;
                    desiredPos += new float3(math.cos(angle) * 1.5f, 0, math.sin(angle) * 1.5f);
                }
                else
                {
                    float3 right = math.cross(math.up(), playerForward);
                    int rIdx = index - 8;
                    int row = rIdx < 5 ? 0 : (rIdx < 9 ? 1 : 2);
                    int col = rIdx < 5 ? rIdx : (rIdx < 9 ? rIdx - 5 : rIdx - 9);
                    int colsInRow = row == 0 ? 5 : (row == 1 ? 4 : 3);

                    float xOffset = (col - (colsInRow - 1) / 2f) * 1.5f;
                    float zOffset = 3f + (row * 1.5f);
                    desiredPos += playerForward * zOffset + right * xOffset;
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

        // 물리 월드 접근 (Separation을 위해)
        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld)) return;
        var collisionWorld = physicsWorld.CollisionWorld;

        // 분리 설정 (그림자 간의 거리를 넉넉하게 유지하고 싶다면 Radius 조절)
        float separationRadius = 1.2f;
        float separationWeight = 1.5f;

        foreach (var (transform, physicsVelocity, physicsMass, targetPos, shadow, shadowCombatData) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<PhysicsMass>, RefRO<TargetPositionData>, RefRO<ShadowData>, RefRO<ShadowCombatData>>())
        {
            if (!shadowCombatData.ValueRO.IsAlive) continue;

            // 그림자도 회전 시 물리적으로 방해받지 않도록 관성 고정
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

                // --- [Separation 로직 추가] ---
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

                        // 그림자는 목적지가 플레이어 주변 진형이므로 배회(Orbit) 보다는 
                        // 목적지에 도달했을 때 서로 공간을 확보하며 자리를 잡도록 밀어내기만 적용합니다.
                        moveDir = math.normalize(lookDir + separationVec * separationWeight);
                    }
                }
                hits.Dispose();
                // -----------------------------

                // 위치 갱신 (Y축 속도 강제 0으로 조절하여 팝콘 방지)
                physicsVelocity.ValueRW.Linear = new float3(
                    moveDir.x * finalSpeed,
                    0f, // 수직 속도 차단
                    moveDir.z * finalSpeed
                );

                // Y축 위치 강제 고정 (지면에 맞춤, 예: 0.5f)
                float3 fixedPos = transform.ValueRO.Position;
                fixedPos.y = 0.5f; 
                transform.ValueRW.Position = fixedPos;

                // 회전 갱신 (순수 타겟 방향인 lookDir 사용)
                quaternion targetRot = quaternion.LookRotationSafe(lookDir, math.up());
                transform.ValueRW.Rotation = math.slerp(transform.ValueRO.Rotation, targetRot, deltaTime * 10f);
                transform.ValueRW.Rotation.value.x = 0;
                transform.ValueRW.Rotation.value.z = 0;
                transform.ValueRW.Rotation = math.normalize(transform.ValueRW.Rotation);
            }
            else
            {
                // 타겟에 도착했을 때도 위로 튀지 않게 설정
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

        // 빠른 타겟 검색을 위해 살아있는 모든 적의 위치를 메모리에 로드
        var enemyQuery = SystemAPI.QueryBuilder().WithAll<EnemyTag, EnemyData, LocalTransform>().Build();
        var enemyEntities = enemyQuery.ToEntityArray(Allocator.TempJob);
        var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

        foreach (var (combatData, transform) in SystemAPI.Query<RefRW<ShadowCombatData>, RefRO<LocalTransform>>())
        {
            if (!combatData.ValueRO.IsAlive) continue;
            combatData.ValueRW.ScanTimer -= deltaTime;
            combatData.ValueRW.CurrentCooldown -= deltaTime;

            // 타겟 유효성 검사
            bool needsNewTarget = combatData.ValueRO.CurrentTarget == Entity.Null || 
                                  !SystemAPI.Exists(combatData.ValueRO.CurrentTarget);

            // 거리 초과 여부 확인
            if (!needsNewTarget && _transformLookup.TryGetComponent(combatData.ValueRO.CurrentTarget, out var targetTransform))
            {
                if (math.distancesq(transform.ValueRO.Position, targetTransform.Position) > combatData.ValueRO.AttackRange * combatData.ValueRO.AttackRange)
                {
                    needsNewTarget = true; // 사거리 벗어난 경우 타겟 초기화
                    combatData.ValueRW.CurrentTarget = Entity.Null;
                }
            }

            // 새로운 타겟 탐색
            if (needsNewTarget && combatData.ValueRO.ScanTimer <= 0)
            {
                combatData.ValueRW.ScanTimer = 0.3f;
                float closestDistSq = float.MaxValue;
                Entity bestTarget = Entity.Null;

                for (int i = 0; i < enemyEntities.Length; i++)
                {
                    float distSq = math.distancesq(transform.ValueRO.Position, enemyTransforms[i].Position);
                    if (distSq < combatData.ValueRO.AttackRange * combatData.ValueRO.AttackRange && distSq < closestDistSq)
                    {
                        closestDistSq = distSq;
                        bestTarget = enemyEntities[i];
                    }
                }
                combatData.ValueRW.CurrentTarget = bestTarget;
                needsNewTarget = bestTarget == Entity.Null;
            }

            // 공격 실행
            if (!needsNewTarget && combatData.ValueRO.CurrentCooldown <= 0)
            {
                float3 targetPos = _transformLookup[combatData.ValueRO.CurrentTarget].Position;

                Entity hitbox = ecb.Instantiate(combatData.ValueRO.AttackPrefab);

                if (combatData.ValueRO.AttackType == AttackType.Melee)
                {
                    ecb.SetComponent(hitbox, new LocalTransform
                    {
                        Position = targetPos,
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
                        Rotation = quaternion.LookRotationSafe(targetPos - transform.ValueRO.Position, math.up())
                    });
                }
                ecb.AddComponent(hitbox, new HitBoxData
                    {
                        Damage = combatData.ValueRO.AttackPower,
                        Radius = 0.5f,
                        Duration = 0.5f,
                        TargetFaction = 0, 
                        IsPiercing = true
                    });
                combatData.ValueRW.CurrentCooldown = combatData.ValueRO.AttackCooldown;
            }
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        enemyEntities.Dispose();
        enemyTransforms.Dispose();
    }
}
#endregion

#region HealthSystem
[BurstCompile]
public partial struct ShadowHealthSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (combatData, damageBuffer) in SystemAPI.Query<RefRW<ShadowCombatData>, DynamicBuffer<DamageBufferElement>>())
        {
            if (!combatData.ValueRO.IsAlive)
            {
                damageBuffer.Clear(); // 이미 사망한 경우 피해 무시
                continue;
            }

            // 무적 타이머 감소
            if (combatData.ValueRO.InvincibilityTimer > 0f)
            {
                combatData.ValueRW.InvincibilityTimer -= deltaTime;
                damageBuffer.Clear(); // 무적 상태에서는 받은 피해 무시
                continue;
            }

            if (damageBuffer.Length > 0)
            {
                float finalDamage = math.max(0f, damageBuffer[0].Damage - 0f); // 방어력 등 추가 계산 가능
                combatData.ValueRW.CurrentHealth -= finalDamage;
                combatData.ValueRW.InvincibilityTimer = 0.5f; // 피해를 받은 후 0.5초간 무적

                damageBuffer.Clear();

                // 사망 처리
                if (combatData.ValueRO.CurrentHealth <= 0f)
                {
                    combatData.ValueRW.CurrentHealth = 0f;
                    combatData.ValueRW.IsAlive = false;
                }
            }
        }
    }
}
#endregion