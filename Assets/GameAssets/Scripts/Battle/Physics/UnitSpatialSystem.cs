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
    private NativeParallelMultiHashMap<int2, Entity> _enemyGrid;
    private NativeParallelMultiHashMap<int2, Entity> _shadowGrid;
    private NativeParallelMultiHashMap<int2, Entity> _playerGrid;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _enemyQuery = SystemAPI.QueryBuilder().WithAll<CEnemyData, LocalTransform>().Build();
        _shadowQuery = SystemAPI.QueryBuilder().WithAny<CShadowData, ShadowTag>().WithAll<LocalTransform>().Build();
        _playerQuery = SystemAPI.QueryBuilder().WithAll<PlayerData, LocalTransform>().Build();

        _enemyGrid = new NativeParallelMultiHashMap<int2, Entity>(0, Allocator.Persistent);
        _shadowGrid = new NativeParallelMultiHashMap<int2, Entity>(0, Allocator.Persistent);
        _playerGrid = new NativeParallelMultiHashMap<int2, Entity>(0, Allocator.Persistent);

        var gridData = new SpatialGridData
        {
            EnemyGrid = _enemyGrid,
            ShadowGrid = _shadowGrid,
            PlayerGrid = _playerGrid
        };
        state.EntityManager.AddComponentData(state.EntityManager.CreateEntity(), gridData);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        state.Dependency.Complete();
        if (_enemyGrid.IsCreated) _enemyGrid.Dispose();
        if (_shadowGrid.IsCreated) _shadowGrid.Dispose();
        if (_playerGrid.IsCreated) _playerGrid.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonRW<SpatialGridData>(out var gridDataRW)) return;

        int enemyCount = _enemyQuery.CalculateEntityCount();
        int shadowCount = _shadowQuery.CalculateEntityCount();
        int playerCount = _playerQuery.CalculateEntityCount();

        if (_enemyGrid.Capacity < enemyCount * 2 + 256)
        {
            state.Dependency.Complete();
            if (_enemyGrid.IsCreated) _enemyGrid.Dispose();
            _enemyGrid = new NativeParallelMultiHashMap<int2, Entity>(enemyCount * 2 + 512, Allocator.Persistent);
        }
        _enemyGrid.Clear();
        
        if (_shadowGrid.Capacity < shadowCount * 2 + 256)
        {
            state.Dependency.Complete();
            if (_shadowGrid.IsCreated) _shadowGrid.Dispose();
            _shadowGrid = new NativeParallelMultiHashMap<int2, Entity>(shadowCount * 2 + 512, Allocator.Persistent);
        }
        _shadowGrid.Clear();

        if (_playerGrid.Capacity < playerCount * 2 + 256)
        {
            state.Dependency.Complete();
            if (_playerGrid.IsCreated) _playerGrid.Dispose();
            _playerGrid = new NativeParallelMultiHashMap<int2, Entity>(playerCount * 2 + 512, Allocator.Persistent);
        }
        _playerGrid.Clear();

        gridDataRW.ValueRW.EnemyGrid = _enemyGrid;
        gridDataRW.ValueRW.ShadowGrid = _shadowGrid;
        gridDataRW.ValueRW.PlayerGrid = _playerGrid;

        var buildEnemyJob = new BuildGridJob { Grid = _enemyGrid.AsParallelWriter() };
        var buildShadowJob = new BuildGridJob { Grid = _shadowGrid.AsParallelWriter() };
        var buildPlayerJob = new BuildGridJob { Grid = _playerGrid.AsParallelWriter() };

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





