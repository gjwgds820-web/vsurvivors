using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

public struct SpatialHashConfig
{
    public const float CellSize = 2.0f;
    public static int2 GetCell(float3 position) => new int2((int)math.floor(position.x / CellSize), (int)math.floor(position.z / CellSize));
}

public struct SpatialGridData : IComponentData
{
    public NativeParallelMultiHashMap<int2, Entity> EnemyGrid;
    public NativeParallelMultiHashMap<int2, Entity> ShadowGrid;
    public NativeParallelMultiHashMap<int2, Entity> PlayerGrid;
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct UnitSpatialSystem : ISystem
{
    private EntityQuery _enemyQuery;
    private EntityQuery _shadowQuery;
    private EntityQuery _playerQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _enemyQuery = SystemAPI.QueryBuilder().WithAll<CEnemyData, LocalTransform>().Build();
        _shadowQuery = SystemAPI.QueryBuilder().WithAny<CShadowData, ShadowTag>().WithAll<LocalTransform>().Build();
        _playerQuery = SystemAPI.QueryBuilder().WithAll<PlayerData, LocalTransform>().Build();

        var gridData = new SpatialGridData
        {
            EnemyGrid = new NativeParallelMultiHashMap<int2, Entity>(0, Allocator.Persistent),
            ShadowGrid = new NativeParallelMultiHashMap<int2, Entity>(0, Allocator.Persistent),
            PlayerGrid = new NativeParallelMultiHashMap<int2, Entity>(0, Allocator.Persistent)
        };
        state.EntityManager.AddComponentData(state.EntityManager.CreateEntity(), gridData);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (SystemAPI.TryGetSingleton<SpatialGridData>(out var gridData))
        {
            if (gridData.EnemyGrid.IsCreated) gridData.EnemyGrid.Dispose();
            if (gridData.ShadowGrid.IsCreated) gridData.ShadowGrid.Dispose();
            if (gridData.PlayerGrid.IsCreated) gridData.PlayerGrid.Dispose();
        }
    }

        [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonRW<SpatialGridData>(out var gridDataRW)) return;
        var enemyGrid = gridDataRW.ValueRW.EnemyGrid;
        var shadowGrid = gridDataRW.ValueRW.ShadowGrid;
        var playerGrid = gridDataRW.ValueRW.PlayerGrid;

        int enemyCount = _enemyQuery.CalculateEntityCount();
        int shadowCount = _shadowQuery.CalculateEntityCount();
        int playerCount = _playerQuery.CalculateEntityCount();

        if (enemyGrid.Capacity < enemyCount * 2 + 256) 
        {
            if (enemyGrid.IsCreated) enemyGrid.Dispose();
            enemyGrid = new NativeParallelMultiHashMap<int2, Entity>(enemyCount * 2 + 512, Allocator.Persistent);
        }
        enemyGrid.Clear();
        
        if (shadowGrid.Capacity < shadowCount * 2 + 256) 
        {
            if (shadowGrid.IsCreated) shadowGrid.Dispose();
            shadowGrid = new NativeParallelMultiHashMap<int2, Entity>(shadowCount * 2 + 512, Allocator.Persistent);
        }
        shadowGrid.Clear();

        if (playerGrid.Capacity < playerCount * 2 + 256) 
        {
            if (playerGrid.IsCreated) playerGrid.Dispose();
            playerGrid = new NativeParallelMultiHashMap<int2, Entity>(playerCount * 2 + 512, Allocator.Persistent);
        }
        playerGrid.Clear();

        gridDataRW.ValueRW.EnemyGrid = enemyGrid;
        gridDataRW.ValueRW.ShadowGrid = shadowGrid;
        gridDataRW.ValueRW.PlayerGrid = playerGrid;

        var buildEnemyJob = new BuildGridJob { Grid = enemyGrid.AsParallelWriter() };
        var buildShadowJob = new BuildGridJob { Grid = shadowGrid.AsParallelWriter() };
        var buildPlayerJob = new BuildGridJob { Grid = playerGrid.AsParallelWriter() };

        var j1 = buildEnemyJob.ScheduleParallel(_enemyQuery, state.Dependency);
        var j2 = buildShadowJob.ScheduleParallel(_shadowQuery, state.Dependency);
        var j3 = buildPlayerJob.ScheduleParallel(_playerQuery, state.Dependency);

        state.Dependency = JobHandle.CombineDependencies(j1, j2, j3);
    }
}

[BurstCompile]
public partial struct BuildGridJob : IJobEntity
{
    public NativeParallelMultiHashMap<int2, Entity>.ParallelWriter Grid;

    private void Execute(Entity entity, in LocalTransform transform)
    {
        Grid.Add(SpatialHashConfig.GetCell(transform.Position), entity);
    }
}





