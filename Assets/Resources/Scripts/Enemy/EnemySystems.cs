using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

#region Targeting System
[BurstCompile]
public partial struct EnemyTargetingSystem : ISystem
{
    private EntityQuery _playerQuery;
    private EntityQuery _shadowQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _playerQuery = SystemAPI.QueryBuilder().WithAll<PlayerInput, LocalTransform>().Build();
        _shadowQuery = SystemAPI.QueryBuilder().WithAll<ShadowData, LocalTransform>().Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (_playerQuery.IsEmpty) return;

        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld)) return;

        Entity playerEntity = _playerQuery.GetSingletonEntity();
        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

        var shadowEntities = _shadowQuery.ToEntityArray(Allocator.TempJob);
        var shadowTransforms = _shadowQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

        var job = new EnemyTargetingJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            PlayerEntity = playerEntity,
            PlayerPos = playerPos,
            ShadowEntities = shadowEntities,
            ShadowTransforms = shadowTransforms,
            CollisionWorld = physicsWorld.CollisionWorld
        };

        job.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct EnemyTargetingJob : IJobEntity
{
    public float DeltaTime;
    public Entity PlayerEntity;
    public float3 PlayerPos;

    [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<Entity> ShadowEntities;
    [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<LocalTransform> ShadowTransforms;
    [ReadOnly] public CollisionWorld CollisionWorld;

    private void Execute(ref EnemyData enemyData, ref EnemyTargetData targetData, in LocalTransform transform)
    {
        enemyData.SearchTimer -= DeltaTime;
        if (enemyData.SearchTimer > 0) return;

        enemyData.SearchTimer = 0.3f;

        float closestDistSq = float.MaxValue;
        Entity closestShadow = Entity.Null;

        // 인식 거리
        float detectionRangeSq = (enemyData.AttackRange * 2.0f) * (enemyData.AttackRange * 2.0f);

        // 타겟을 둘러쌀 수 있는 최대 허용 개체 수
        int maxAttackersAllowed = 5;

        // 섀도우 탐색
        for (int i = 0; i < ShadowEntities.Length; i++)
        {
            float3 shadowPos = ShadowTransforms[i].Position;
            float distSq = math.distancesq(transform.Position, ShadowTransforms[i].Position);

            if (distSq <= detectionRangeSq && distSq < closestDistSq)
            {
                // 혼잡도 체크
                NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.Temp);
                int crowdCount = 0;

                // 타겟 위치 기준으로 주변 접근 가능 거리 탐색
                if (CollisionWorld.CalculateDistance(new PointDistanceInput()
                {
                    Position = shadowPos,
                    MaxDistance = enemyData.AttackRange * 1.5f,
                    Filter = CollisionFilter.Default
                }, ref hits))
                {
                    crowdCount = hits.Length;
                }
                hits.Dispose();

                // 주변 개체(적군+아군)가 제한 수치 이하일 때만 타겟으로 선정
                if (crowdCount <= maxAttackersAllowed)
                {
                    closestDistSq = distSq;
                    closestShadow = ShadowEntities[i];
                }
            }
        }

        // 사거리 내에 접근 가능한 그림자가 있는 경우 그림자 타겟, 그렇지 않으면 플레이어 타겟
        if (closestShadow != Entity.Null)
        {
            targetData.CurrentTarget = closestShadow;
            targetData.IsTargetingShadow = true;
        }
        else
        {
            targetData.CurrentTarget = PlayerEntity;
            targetData.IsTargetingShadow = false;
        }
    }
}
#endregion

#region Movement System
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
[BurstCompile]
public partial struct EnemyMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        // 주변 공간 검색을 위한 PhysicsWorld 접근
        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld)) return;

        var collisionWorld = physicsWorld.CollisionWorld;

        // 반발력을 계산할 반경과 가중치
        float separationRadius = 1.5f;
        float separationWeight = 2.0f;

        foreach (var (transform, velocity, physicsMass, enemyData, targetData) in 
                SystemAPI.Query<RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<PhysicsMass>, RefRW<EnemyData>, RefRO<EnemyTargetData>>())
        {
            physicsMass.ValueRW.InverseInertia = new float3(0, 0, 0);
            if (targetData.ValueRO.CurrentTarget == Entity.Null || !SystemAPI.Exists(targetData.ValueRO.CurrentTarget))
            {
                velocity.ValueRW.Linear = new float3(0, velocity.ValueRO.Linear.y, 0);
                continue;
            }

            float3 currentPos = transform.ValueRO.Position;
            float3 targetPos = SystemAPI.GetComponent<LocalTransform>(targetData.ValueRO.CurrentTarget).Position;

            float3 toTarget = targetPos - currentPos;
            toTarget.y = 0; // 수평 이동만 고려
            float distance = math.length(toTarget);

            if (distance > enemyData.ValueRO.AttackRange)
            {
                enemyData.ValueRW.CurrentState = EnemyState.Move;
                float3 moveDir = toTarget / distance;
                float3 lookDir = moveDir;

                // Separation 계산
                float3 separationVec = float3.zero;

                // 공간 검색 필터
                CollisionFilter filter = CollisionFilter.Default;

                // 목표 반경(Radius) 내에 있는 물리 객체 검색
                NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.Temp);

                // OverlapAabb 대신 점(Point) 기반 거리 측정(CalculateDistance) 사용
                if (collisionWorld.CalculateDistance(new PointDistanceInput 
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

                        // 아주 미세한 거리 방어 (자기 자신 또는 완전히 겹친 상태 방지)
                        if (sqrMag > 0.001f && sqrMag < separationRadius * separationRadius)
                        {
                            float dist = math.sqrt(sqrMag);
                            // 거리가 가까울수록 강한 밀어내기 힘 적용
                            separationVec += (diff / dist) * (1.0f - (dist / separationRadius));
                            neighborCount++;
                        }
                    }
                    if (neighborCount > 0)
                    {
                        // 평균 분리 벡터
                        separationVec /= neighborCount;

                        // 가고자 하는 방향과 밀어내는 방향의 내적
                        float pushBackFactor = math.dot(lookDir, separationVec);

                        // 만약 꽉 막혔다면
                        if (pushBackFactor < -0.2f)
                        {
                            // 타겟 방향의 측면 벡터
                            float3 rightDir = math.cross(math.up(), lookDir);
                            float dirSign = (currentPos.x * currentPos.z) % 2f > 0 ? 1f : -1f;

                            // 배회 벡터
                            float3 orbitVec = rightDir * dirSign * 2.0f;

                            // 이동 방향에 배회 벡터와 분리 벡터를 섞음
                            moveDir = math.normalize(lookDir + separationVec * separationWeight + orbitVec);
                        }
                        else
                        {
                            // 막힌게 아니면 타겟방향 + 분리벡터
                            moveDir = math.normalize(moveDir + separationVec * separationWeight);
                        }
                    }
                    else
                    {
                        moveDir = lookDir; // 주변에 아무도 없다면 직진
                    }
                }
                hits.Dispose();

                // 목표를 향해 이동
                velocity.ValueRW.Linear = new float3(
                    moveDir.x * enemyData.ValueRO.MoveSpeed,
                    0f,
                    moveDir.z * enemyData.ValueRO.MoveSpeed
                );

                // 튀어오르는 것 방지
                float3 fixedPos = transform.ValueRO.Position;
                fixedPos.y = 0.5f;
                transform.ValueRW.Position = fixedPos;

                // 회전 (Y축 기준)
                quaternion targetRot = quaternion.LookRotationSafe(lookDir, math.up());
                transform.ValueRW.Rotation = math.slerp(transform.ValueRO.Rotation, targetRot, deltaTime * 10f);
                transform.ValueRW.Rotation.value.x = 0;
                transform.ValueRW.Rotation.value.z = 0;
                transform.ValueRW.Rotation = math.normalize(transform.ValueRW.Rotation);
            }
            else
            {
                // 사거리 내에 들어왔을 때 공격 상태로 전환
                enemyData.ValueRW.CurrentState = EnemyState.Attack;
                velocity.ValueRW.Linear = new float3(0, velocity.ValueRO.Linear.y, 0);
            }
        }
    }
}
#endregion