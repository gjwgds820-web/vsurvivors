using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct UnitTargetingSystem : ISystem
{
    private EntityQuery _targetingQuery;
    private EntityQuery _damagableQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _targetingQuery = SystemAPI.QueryBuilder()
            .WithAllRW<TargetingData>()
            .WithAll<LocalTransform>()
            .Build();

        _damagableQuery = SystemAPI.QueryBuilder()
            .WithAll<HealthData>()
            .WithAll<LocalTransform>()
            .WithNone<DeathTag>()
            .Build();

        state.RequireForUpdate(_targetingQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var entities = _damagableQuery.ToEntityArray(Allocator.TempJob);
        var transforms = _damagableQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var healths = _damagableQuery.ToComponentDataArray<HealthData>(Allocator.TempJob);
        
        // Factions
        var isPlayer = new NativeArray<bool>(entities.Length, Allocator.TempJob);
        var isEnemy = new NativeArray<bool>(entities.Length, Allocator.TempJob);
        var isShadow = new NativeArray<bool>(entities.Length, Allocator.TempJob);

        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            isPlayer[i] = SystemAPI.HasComponent<PlayerData>(entity);
            isEnemy[i] = SystemAPI.HasComponent<EnemyTag>(entity);
            isShadow[i] = SystemAPI.HasComponent<ShadowTag>(entity);
        }

        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (targeting, transform, selfEntity) in SystemAPI.Query<RefRW<TargetingData>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            targeting.ValueRW.ScanTimer -= deltaTime;

            // Check if current target is valid
            bool needNewTarget = targeting.ValueRO.CurrentTarget == Entity.Null;
            int currentTargetIndex = -1;
            
            if (!needNewTarget)
            {
                currentTargetIndex = entities.IndexOf(targeting.ValueRO.CurrentTarget);
                if (currentTargetIndex == -1 || healths[currentTargetIndex].CurrentHealth <= 0)
                {
                    targeting.ValueRW.CurrentTarget = Entity.Null;
                    targeting.ValueRW.ScanTimer = 0f; // 즉각적인 재탐색
                    needNewTarget = true;
                }
                else
                {
                    // Check range (2D distance to avoid height mismatch)
                    float3 myPos2D = new float3(transform.ValueRO.Position.x, 0, transform.ValueRO.Position.z);
                    float3 targetPos2D = new float3(transforms[currentTargetIndex].Position.x, 0, transforms[currentTargetIndex].Position.z);
                    float distSq = math.distancesq(myPos2D, targetPos2D);
                    if (distSq > targeting.ValueRO.MaxFollowRangeSq)
                    {
                        targeting.ValueRW.CurrentTarget = Entity.Null;
                        targeting.ValueRW.ScanTimer = 0f; // 사거리 이탈 시 즉시 재탐색
                        needNewTarget = true;
                    }
                }
            }

            // 조건 1. 현재 타겟이 유효하지 않아 새 타겟이 즉시 필요한 경우
            // 조건 2. 스캔 타이머가 만료되어 주변을 주기적으로 다시 살피는 경우 (가장 가까운 대상 갱신)
            if (needNewTarget || targeting.ValueRO.ScanTimer <= 0)
            {
                targeting.ValueRW.ScanTimer = targeting.ValueRO.ScanInterval;   
                Entity bestTarget = Entity.Null;
                float bestScore = float.MaxValue;

                for (int i = 0; i < entities.Length; i++)
                {
                    if (entities[i] == selfEntity) continue;
                    if (healths[i].CurrentHealth <= 0) continue;

                    bool isValidTarget = false;
                    if (targeting.ValueRO.Faction == TargetingFaction.Enemy)    
                    {
                        if (isPlayer[i]) isValidTarget = true;
                        else if (isShadow[i]) isValidTarget = true;
                    }
                    else if (targeting.ValueRO.Faction == TargetingFaction.Ally)
                    {
                        if (isEnemy[i]) isValidTarget = true;
                    }

                    if (!isValidTarget) continue;

                    float3 myPos2DScan = new float3(transform.ValueRO.Position.x, 0, transform.ValueRO.Position.z);
                    float3 targetPos2DScan = new float3(transforms[i].Position.x, 0, transforms[i].Position.z);
                    float distSq = math.distancesq(myPos2DScan, targetPos2DScan);
                    
                    float score = float.MaxValue;
                    float scanRangeSq = targeting.ValueRO.MaxSearchRangeSq;
                    
                    if (targeting.ValueRO.Faction == TargetingFaction.Enemy)
                    {
                        if (isShadow[i])
                        {
                            if (distSq <= scanRangeSq)
                                score = distSq - 2000000f; // 1순위: 인식 범위 내 그림자
                            else
                                continue; // 멀리 있는 그림자는 무시
                        }
                        else if (isPlayer[i])
                        {
                            if (distSq <= scanRangeSq)
                                score = distSq - 1000000f; // 2순위: 인식 범위 내 플레이어
                            else
                                score = distSq; // 3순위: 전체 맵 플레이어 (어디 있든 추적)
                        }
                    }
                    else
                    {
                        // 아군 타겟팅
                        if (distSq > scanRangeSq) continue;

                        switch (targeting.ValueRO.Priority)
                        {
                            case TargetingType.Nearest:
                                score = distSq;
                                break;
                            case TargetingType.LowestHP:
                                score = healths[i].CurrentHealth;
                                break;
                        }
                    }
                    
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestTarget = entities[i];
                    }
                }
                if (bestTarget != Entity.Null)
                {
                    targeting.ValueRW.CurrentTarget = bestTarget;
                }
            }
        }

        entities.Dispose();
        transforms.Dispose();
        healths.Dispose();
        isPlayer.Dispose();
        isEnemy.Dispose();
        isShadow.Dispose();
    }
}



