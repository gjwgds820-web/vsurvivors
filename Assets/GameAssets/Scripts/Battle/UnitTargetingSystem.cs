using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct UnitTargetingSystem : ISystem
{
    private ComponentLookup<LocalToWorld> _transformLookup;
    private ComponentLookup<HealthData> _healthLookup;
    private ComponentLookup<PlayerData> _playerLookup;
    private ComponentLookup<CEnemyData> _enemyLookup;
    private ComponentLookup<EnemyTag> _enemyTagLookup;
    private ComponentLookup<ShadowCombatData> _shadowLookup;
    private ComponentLookup<ShadowTag> _shadowTagLookup;

    private EntityQuery _targetableQuery;
    private EntityQuery _targetingQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _transformLookup = state.GetComponentLookup<LocalToWorld>(true);
        _healthLookup = state.GetComponentLookup<HealthData>(true);
        _playerLookup = state.GetComponentLookup<PlayerData>(true);
        _enemyLookup = state.GetComponentLookup<CEnemyData>(true);
        _enemyTagLookup = state.GetComponentLookup<EnemyTag>(true);
        _shadowLookup = state.GetComponentLookup<ShadowCombatData>(true);
        _shadowTagLookup = state.GetComponentLookup<ShadowTag>(true);

        _targetableQuery = SystemAPI.QueryBuilder()
            .WithAll<HealthData, LocalToWorld>()
            .WithNone<DeathTag>()
            .Build();

        _targetingQuery = SystemAPI.QueryBuilder().WithAllRW<TargetingData>().WithAll<LocalToWorld>().Build();
        state.RequireForUpdate(_targetingQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _transformLookup.Update(ref state);
        _healthLookup.Update(ref state);
        _playerLookup.Update(ref state);
        _enemyLookup.Update(ref state);
        _enemyTagLookup.Update(ref state);
        _shadowLookup.Update(ref state);
        _shadowTagLookup.Update(ref state);

        var targetableEntities = _targetableQuery.ToEntityArray(Allocator.TempJob);

        var job = new TargetingJob
        {
            Targetables = targetableEntities,
            TransformLookup = _transformLookup,
            HealthLookup = _healthLookup,
            PlayerLookup = _playerLookup,
            EnemyLookup = _enemyLookup,
            EnemyTagLookup = _enemyTagLookup,
            ShadowLookup = _shadowLookup,
            ShadowTagLookup = _shadowTagLookup,
            DeltaTime = SystemAPI.Time.DeltaTime
        };
        
        state.Dependency = job.ScheduleParallel(_targetingQuery, state.Dependency);
        
        state.Dependency = targetableEntities.Dispose(state.Dependency);
    }
}

[BurstCompile]
public partial struct TargetingJob : IJobEntity
{
    [ReadOnly] public NativeArray<Entity> Targetables;
    [ReadOnly] public ComponentLookup<LocalToWorld> TransformLookup;
    [ReadOnly] public ComponentLookup<HealthData> HealthLookup;
    [ReadOnly] public ComponentLookup<PlayerData> PlayerLookup;
    [ReadOnly] public ComponentLookup<CEnemyData> EnemyLookup;
    [ReadOnly] public ComponentLookup<EnemyTag> EnemyTagLookup;
    [ReadOnly] public ComponentLookup<ShadowCombatData> ShadowLookup;
    [ReadOnly] public ComponentLookup<ShadowTag> ShadowTagLookup;
    public float DeltaTime;

    private void Execute(Entity selfEntity, ref TargetingData targeting, in LocalToWorld transform)
    {
        targeting.ScanTimer -= DeltaTime;

        bool needNewTarget = targeting.CurrentTarget == Entity.Null;
        
        if (!needNewTarget)
        {
            if (!HealthLookup.HasComponent(targeting.CurrentTarget) || HealthLookup[targeting.CurrentTarget].CurrentHealth <= 0)
            {
                targeting.CurrentTarget = Entity.Null;
                targeting.ScanTimer = 0f; 
                needNewTarget = true;
            }
            else if (TransformLookup.HasComponent(targeting.CurrentTarget))
            {
                float3 myPos2D = new float3(transform.Position.x, 0, transform.Position.z);
                float3 targetPos2D = new float3(TransformLookup[targeting.CurrentTarget].Position.x, 0, TransformLookup[targeting.CurrentTarget].Position.z);
                float distSq = math.distancesq(myPos2D, targetPos2D);
                
                float maxFollowSq = targeting.MaxFollowRangeSq;
                
                if (distSq > maxFollowSq)
                {
                    targeting.CurrentTarget = Entity.Null;
                    targeting.ScanTimer = 0f; 
                    needNewTarget = true;
                }
            }
        }

        if (needNewTarget || targeting.ScanTimer <= 0)
        {
            targeting.ScanTimer = targeting.ScanInterval;   
            Entity bestTarget = Entity.Null;
            float bestScore = float.MaxValue;

            float3 myPos2DScan = new float3(transform.Position.x, 0, transform.Position.z);
            
            float scanRangeSq = targeting.MaxSearchRangeSq;
            TargetingFaction faction = targeting.Faction;
            TargetingType priority = targeting.Priority;

            for (int i = 0; i < Targetables.Length; i++)
            {
                Entity checkEnt = Targetables[i];
                if (checkEnt == selfEntity) continue;
                if (!HealthLookup.HasComponent(checkEnt) || HealthLookup[checkEnt].CurrentHealth <= 0) continue;

                bool isValidTarget = false;
                
                bool isPl = PlayerLookup.HasComponent(checkEnt);
                bool isSh = ShadowLookup.HasComponent(checkEnt) || ShadowTagLookup.HasComponent(checkEnt);
                bool isEn = EnemyTagLookup.HasComponent(checkEnt) || EnemyLookup.HasComponent(checkEnt);

                if (faction == TargetingFaction.Enemy)    
                {
                    if (isPl || isSh) isValidTarget = true;
                }
                else if (faction == TargetingFaction.Ally)
                {
                    if (isEn) isValidTarget = true;
                }

                if (!isValidTarget) continue;
                if (!TransformLookup.HasComponent(checkEnt)) continue;

                float3 targetPos2DScan = new float3(TransformLookup[checkEnt].Position.x, 0, TransformLookup[checkEnt].Position.z);
                float distSq = math.distancesq(myPos2DScan, targetPos2DScan);
                
                float score = float.MaxValue;
                
                if (faction == TargetingFaction.Enemy)
                {
                    if (isSh)
                    {
                        if (distSq <= scanRangeSq) score = distSq - 2000000f; 
                        else continue; 
                    }
                    else if (isPl)
                    {
                        if (distSq <= scanRangeSq) score = distSq - 1000000f; 
                        else score = distSq; 
                    }
                }
                else
                {
                    if (distSq > scanRangeSq) continue;

                    if (priority == TargetingType.LowestHP)
                    {
                        score = HealthLookup[checkEnt].CurrentHealth;
                    }
                    else
                    {
                        score = distSq; // Fallback to Nearest
                    }
                }
                
                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = checkEnt;
                }
            }
            
            // Regardless of whether we found one or not, if needNewTarget evaluates to true, force update
            if (needNewTarget || bestTarget != Entity.Null)
            {
                targeting.CurrentTarget = bestTarget;
            }
        }
    }
}


