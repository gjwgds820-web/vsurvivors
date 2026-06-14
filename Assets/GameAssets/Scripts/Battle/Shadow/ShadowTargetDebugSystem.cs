using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ShadowTargetDebugSystem : SystemBase
{
    private float _logTimer;
    private EntityQuery _targetableQuery;

    protected override void OnCreate()
    {
        _logTimer = 0f;
        _targetableQuery = SystemAPI.QueryBuilder()
            .WithAll<HealthData, LocalToWorld>()
            .WithNone<DeathTag>()
            .Build();
    }

    protected override void OnUpdate()
    {
        _logTimer -= SystemAPI.Time.DeltaTime;
        if (_logTimer > 0f) return;
        _logTimer = 1.0f; // 1초마다 출력 (도배 방지)

        Entity firstShadow = Entity.Null;
        TargetingData shadowTargeting = default;
        LocalToWorld shadowTransform = default;

        foreach (var (targeting, transform, entity) in SystemAPI.Query<RefRO<TargetingData>, RefRO<LocalToWorld>>().WithAll<CShadowData>().WithEntityAccess())
        {
            firstShadow = entity;
            shadowTargeting = targeting.ValueRO;
            shadowTransform = transform.ValueRO;
            break; // 가장 첫 번째 그림자만 타겟팅 (1개체 전용 로그)
        }

        if (firstShadow == Entity.Null) return;

        var targetables = _targetableQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        int totalTargetables = targetables.Length;
        int enemiesInSearchRange = 0;
        int validEnemiesFound = 0;

        float3 myPos2D = new float3(shadowTransform.Position.x, 0, shadowTransform.Position.z);
        float searchRangeSq = shadowTargeting.MaxSearchRangeSq;
        float trueSearchRange = math.sqrt(searchRangeSq);

        foreach (var target in targetables)
        {
            if (target == firstShadow) continue;

            bool isEnemy = SystemAPI.HasComponent<EnemyTag>(target) || SystemAPI.HasComponent<CEnemyData>(target);
            if (isEnemy)
            {
                validEnemiesFound++;
                var targetTransform = SystemAPI.GetComponent<LocalToWorld>(target);
                float3 targetPos2D = new float3(targetTransform.Position.x, 0, targetTransform.Position.z);
                float distSq = math.distancesq(myPos2D, targetPos2D);

                if (distSq <= searchRangeSq)
                {
                    enemiesInSearchRange++;
                }
            }
        }

        string targetStr = shadowTargeting.CurrentTarget == Entity.Null ? "None(Null)" : "Entity_" + shadowTargeting.CurrentTarget.Index;

        Debug.Log($"<color=cyan>[Shadow Debug]</color> Shadow ID: {firstShadow.Index} | Pos: {shadowTransform.Position}");
        Debug.Log($"<color=yellow>[Shadow Target Data]</color> SearchRange: {trueSearchRange:F1}m (Sq: {searchRangeSq:F1}) | Tagged Faction: {shadowTargeting.Faction} | ScanTimer: {shadowTargeting.ScanTimer:F3}");
        Debug.Log($"<color=orange>[Shadow Scan Result]</color> Total Targetable Units: {totalTargetables} | Total Enemies Map: {validEnemiesFound} | Enemies in Range: {enemiesInSearchRange} | <b>Current Target: {targetStr}</b>\n---------------------------------");

        targetables.Dispose();
    }
}

