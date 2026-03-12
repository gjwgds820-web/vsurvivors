using System;
using System.Numerics;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Collections;

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

        foreach (var (transform, physicsVelocity, physicsMass, targetPos, shadow) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<PhysicsMass>, RefRO<TargetPositionData>, RefRO<ShadowData>>())
        {
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
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        
        // var enemyQuery = SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyData>>().WithEntityAccess();

        foreach (var (combatData, transform) in SystemAPI.Query<RefRW<ShadowCombatData>, RefRO<LocalTransform>>())
        {
            combatData.ValueRW.ScanTimer -= deltaTime;
            combatData.ValueRW.CurrentCooldown -= deltaTime;

            // 타겟 유효성 검사
            bool needsNewTarget = combatData.ValueRO.CurrentTarget == Entity.Null;

            // 거리 초과 여부 확인
            if (!needsNewTarget && SystemAPI.Exists(combatData.ValueRO.CurrentTarget))
            {
                float3 targetPos = SystemAPI.GetComponent<LocalTransform>(combatData.ValueRO.CurrentTarget).Position;
                if (math.distance(transform.ValueRO.Position, targetPos) > combatData.ValueRO.AttackRange)
                {
                    needsNewTarget = true; // 사거리 벗어난 경우 타겟 초기화
                }
            }
            else if (!needsNewTarget && !SystemAPI.Exists(combatData.ValueRO.CurrentTarget)) // 타겟이 사망/삭제됨
            {
                needsNewTarget = true;
            }

            // 새로운 타겟 탐색
            if (needsNewTarget && combatData.ValueRO.ScanTimer <= 0)
            {
                combatData.ValueRW.ScanTimer = 0.3f;
                // TODO : 타겟 탐색 로직 (쿼리 최적화 필요)
            }

            // 공격 실행
            if (!needsNewTarget && combatData.ValueRO.CurrentCooldown <= 0)
            {
                if (combatData.ValueRO.AttackType == AttackType.Melee)
                {
                    // TODO : 근접 공격 로직 (충돌체 활성화 등)
                }
                else
                {
                    // TODO : 원거리 공격 로직 (투사체 생성 등)
                }
                combatData.ValueRW.CurrentCooldown = combatData.ValueRO.AttackCooldown;
            }
        }
    }
}
#endregion