using Unity.Entities;
using Unity.Transforms;
using Unity.Physics.GraphicsIntegration;
using UnityEngine;
using Unity.Collections;

[UpdateInGroup(typeof(SimulationSystemGroup))] 
public partial class VisualSyncSystem : SystemBase
{
    private EntityQuery _enemyQuery;
    private EntityQuery _gateQuery;

    protected override void OnCreate()
    {
        // 엔티티를 추적하기위한 쿼리 생성
        _enemyQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, EnemyTag>().WithNone<SubSceneVisualModel>().Build();
        _gateQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, GateData>().WithNone<SubSceneVisualModel>().Build();
    }
    protected override void OnUpdate()
    {
        // =========================================================
        // 1. 런타임 생성 엔티티 껍데기 세팅 (초기화)
        // =========================================================
        if (VisualManager.Instance != null)
        {
            // 에너미
            if (!_enemyQuery.IsEmpty)
            {
                var entities = _enemyQuery.ToEntityArray(Allocator.Temp);

                foreach (var entity in entities)
                {
                    var pos = EntityManager.GetComponentData<LocalTransform>(entity).Position;
                    var rot = EntityManager.GetComponentData<LocalTransform>(entity).Rotation;

                    var go = Object.Instantiate(VisualManager.Instance.EnemyVisualPrefab, pos, rot);
                    EntityManager.AddComponentObject(entity, new SubSceneVisualModel { Value = go.transform });
                }
                entities.Dispose();
            }
            // 게이트
            if (!_gateQuery.IsEmpty)
            {
                var entities = _gateQuery.ToEntityArray(Allocator.Temp);

                foreach (var entity in entities)
                {
                    var pos = EntityManager.GetComponentData<LocalTransform>(entity).Position;
                    var rot = EntityManager.GetComponentData<LocalTransform>(entity).Rotation;

                    var go = Object.Instantiate(VisualManager.Instance.GateVisualPrefab, pos, rot);
                    EntityManager.AddComponentObject(entity, new SubSceneVisualModel { Value = go.transform });
                }
                entities.Dispose();
            }
        }

        // =========================================================
        // 2. 통합 동기화 시작 (플레이어, 에너미, 게이트 등 모두 포함)
        // =========================================================

        // [A] 물리 보간(PhysicsGraphicalInterpolationBuffer)이 있는 엔티티 (플레이어, 이동하는 에너미 등)
        // 떨림(Jitter) 현상 방지를 위해 이전/현재 프레임을 보간한 Transforms 사용
        foreach (var (interpolatedTransform, visualModel) in SystemAPI.Query<RefRO<PhysicsGraphicalInterpolationBuffer>, SubSceneVisualModel>())
        {
            if (visualModel != null && visualModel.Value != null)
            {
                visualModel.Value.position = interpolatedTransform.ValueRO.PreviousTransform.pos;
                visualModel.Value.rotation = interpolatedTransform.ValueRO.PreviousTransform.rot;
            }
        }

        // [B] 물리 보간 버퍼가 없는 정적 엔티티 (게이트 등)
        // 단순히 LocalTransform 위치를 바로 복사
        foreach (var (transform, visualModel) in SystemAPI.Query<RefRO<LocalTransform>, SubSceneVisualModel>()
                     .WithNone<PhysicsGraphicalInterpolationBuffer>()) // A와 중복 실행을 막기 위해 제외
        {
            if (visualModel != null && visualModel.Value != null)
            {
                visualModel.Value.position = transform.ValueRO.Position;
                visualModel.Value.rotation = transform.ValueRO.Rotation;
            }
        }
    }
}