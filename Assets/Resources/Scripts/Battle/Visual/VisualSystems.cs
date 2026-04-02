using Unity.Entities;
using Unity.Transforms;
using Unity.Physics.GraphicsIntegration;
using UnityEngine;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

#region VisualSync
[UpdateInGroup(typeof(PresentationSystemGroup))] 
public partial class VisualSyncSystem : SystemBase
{
    private EntityQuery _enemyMissingVisualQuery;
    private EntityQuery _bossMissingVisualQuery;
    private EntityQuery _gateMissingVisualQuery;
    private EntityQuery _shadowMissingVisualQuery;
    private EntityQuery _itemMissingVisualQuery;

    protected override void OnCreate()
    {
        _enemyMissingVisualQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, EnemyTag>().WithNone<SubSceneVisualModel, Prefab, BossTag>().Build();
        _bossMissingVisualQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, EnemyTag, BossTag>().WithNone<SubSceneVisualModel, Prefab>().Build();
        _gateMissingVisualQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, GateData>().WithNone<SubSceneVisualModel, Prefab>().Build();
        _shadowMissingVisualQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, CShadowData>().WithNone<SubSceneVisualModel, Prefab>().Build();
        _itemMissingVisualQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, DroppedItemData>().WithNone<SubSceneVisualModel, Prefab>().Build();
    }

    protected override void OnUpdate()
    {
        if (VisualManager.Instance == null) return;
        float deltaTime = SystemAPI.Time.DeltaTime;

        if (!_enemyMissingVisualQuery.IsEmpty)
        {
            var entities = _enemyMissingVisualQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var pos = EntityManager.GetComponentData<LocalTransform>(entity).Position;
                var rot = EntityManager.GetComponentData<LocalTransform>(entity).Rotation;
                var go = Object.Instantiate(VisualManager.Instance.EnemyVisualPrefab, pos, rot);
                EntityManager.AddComponentObject(entity, new SubSceneVisualModel { Value = go.transform });
            }
            entities.Dispose();
        }

        if (!_bossMissingVisualQuery.IsEmpty)
        {
            var entities = _bossMissingVisualQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var pos = EntityManager.GetComponentData<LocalTransform>(entity).Position;
                var rot = EntityManager.GetComponentData<LocalTransform>(entity).Rotation;
                var go = Object.Instantiate(VisualManager.Instance.BossVisualPrefab, pos, rot);
                EntityManager.AddComponentObject(entity, new SubSceneVisualModel { Value = go.transform });
            }
            entities.Dispose();
        }

        if (!_gateMissingVisualQuery.IsEmpty)
        {
            var entities = _gateMissingVisualQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var pos = EntityManager.GetComponentData<LocalTransform>(entity).Position;
                var rot = EntityManager.GetComponentData<LocalTransform>(entity).Rotation;
                var go = Object.Instantiate(VisualManager.Instance.GateVisualPrefab, pos, rot);
                EntityManager.AddComponentObject(entity, new SubSceneVisualModel { Value = go.transform });
            }
            entities.Dispose();
        }

        if (!_shadowMissingVisualQuery.IsEmpty)
        {
            var entities = _shadowMissingVisualQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var pos = EntityManager.GetComponentData<LocalTransform>(entity).Position;
                var rot = EntityManager.GetComponentData<LocalTransform>(entity).Rotation;
                var go = Object.Instantiate(VisualManager.Instance.ShadowVisualPrefab, pos, rot);
                EntityManager.AddComponentObject(entity, new SubSceneVisualModel { Value = go.transform });
            }
            entities.Dispose();
        }

        if (!_itemMissingVisualQuery.IsEmpty)
        {
            var entities = _itemMissingVisualQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var pos = EntityManager.GetComponentData<LocalTransform>(entity).Position;
                var rot = EntityManager.GetComponentData<LocalTransform>(entity).Rotation;
                var itemData = EntityManager.GetComponentData<DroppedItemData>(entity);
                GameObject prefab = null;
                switch (itemData.Type)
                {
                    case DropItemType.Exp:
                        prefab = VisualManager.Instance.ExpVisualPrefab;
                        break;
                    case DropItemType.Gold:
                        prefab = VisualManager.Instance.GoldVisualPrefab;
                        break;
                    case DropItemType.Magnet:
                        prefab = VisualManager.Instance.MagnetVisualPrefab;
                        break;
                    case DropItemType.Bomb:
                        prefab = VisualManager.Instance.BombVisualPrefab;
                        break;
                }
                if (prefab != null)
                {
                    var go = Object.Instantiate(prefab, pos, rot);
                    EntityManager.AddComponentObject(entity, new SubSceneVisualModel { Value = go.transform });
                }
            }
            entities.Dispose();
        }

        foreach (var (transform, visualModel) in SystemAPI.Query<RefRO<LocalTransform>, SubSceneVisualModel>())
        {
            if (visualModel != null && visualModel.Value != null)
            {
                // 실제 ECS Transform 위치와 비주얼 오브젝트의 위치 간 거리 확인
                float distanceSq = (visualModel.Value.position - (Vector3)transform.ValueRO.Position).sqrMagnitude;

                // 거리가 너무 멀면 (예: 부활, 텔레포트) Lerp 없이 즉각 이동하여 잔상 방지
                if (distanceSq > 25f) 
                {
                    visualModel.Value.position = transform.ValueRO.Position;
                    visualModel.Value.rotation = transform.ValueRO.Rotation;
                }
                else
                {
                    visualModel.Value.position = Vector3.Lerp(visualModel.Value.position, transform.ValueRO.Position, deltaTime * 20f);
                    visualModel.Value.rotation = Quaternion.Slerp(visualModel.Value.rotation, transform.ValueRO.Rotation, deltaTime * 20f);
                }
            }
        }
    }
}
#endregion

#region ItemVisual
[UpdateInGroup(typeof(PresentationSystemGroup))]
[BurstCompile]
public partial struct ItemVisualSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float time = (float)SystemAPI.Time.ElapsedTime;

        // 드랍된 아이템들만 찾아서 돌리기
        foreach (var (transform, itemData) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRO<DroppedItemData>>())
        {
            transform.ValueRW.Rotation = quaternion.RotateY(time * 2f);
            transform.ValueRW.Position.y = 0.5f + math.sin(time * 5f) * 0.1f;
        }
    }
}
#endregion

#region VisualCleanup
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateBefore(typeof(CleanupDestroyedEntitySystem))]
public partial class VisualCleanupSystem : SystemBase
{
    private EntityQuery _cleanupQuery;

    protected override void OnCreate()
    {
        _cleanupQuery = SystemAPI.QueryBuilder().WithAll<SubSceneVisualModel, DestroyEntityTag>().Build();
    }

    protected override void OnUpdate()
    {
        if (_cleanupQuery.IsEmpty) return;

        var entities = _cleanupQuery.ToEntityArray(Allocator.Temp);

        foreach (var entity in entities)
        {
            var visualModel = EntityManager.GetComponentData<SubSceneVisualModel>(entity);

            if (visualModel != null && visualModel.Value != null)
            {
                UnityEngine.Object.Destroy(visualModel.Value.gameObject);
            }
            EntityManager.RemoveComponent<SubSceneVisualModel>(entity);
        }
        entities.Dispose();
    }
}
#endregion