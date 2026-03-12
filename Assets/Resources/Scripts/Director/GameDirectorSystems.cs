using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics.GraphicsIntegration;
using UnityEngine;

[BurstCompile]
public partial struct GameDirectorSystem : ISystem
{
    private Unity.Mathematics.Random _random;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameDirectorData>();
        state.RequireForUpdate<PlayerInput>();

        _random = new Unity.Mathematics.Random(1234);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var directorEntity = SystemAPI.GetSingletonEntity<GameDirectorData>();
        var directorData = SystemAPI.GetComponentRW<GameDirectorData>(directorEntity);

        directorData.ValueRW.WaveTimer -= SystemAPI.Time.DeltaTime;

        // 타이머가 0 이하가 되면 웨이브 트리거
        if (directorData.ValueRO.WaveTimer <= 0f)
        {
            directorData.ValueRW.WaveTimer = 5f;
            directorData.ValueRW.CurrentWave++;
            
            SpawnWave(ref state, directorData.ValueRO);
        }
    }

    private void SpawnWave(ref SystemState state, GameDirectorData directorData)
    {
        if (directorData.GatePrefab == Entity.Null) return;

        // 최적화를 위해 EntityCommandBuffer를 사용하여 엔티티 생성
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        var playerEntity = SystemAPI.GetSingletonEntity<PlayerInput>();
        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

        // 1분마다 게이트 3개 추가 생성
        int gatesToSpawn = 3;
        float offScreenRadius = 30f;

        for (int i = 0; i < gatesToSpawn; i++)
        {
            float randomAngle = _random.NextFloat(0, math.PI * 2);
            float3 gateSpawnPos = playerPos + new float3(math.cos(randomAngle) * offScreenRadius, 1, math.sin(randomAngle) * offScreenRadius);

            Entity newGate = ecb.Instantiate(directorData.GatePrefab);
            if (SystemAPI.HasComponent<Parent>(directorData.GatePrefab))
            {
                ecb.RemoveComponent<Parent>(newGate);
            }
            ecb.SetComponent(newGate, LocalTransform.FromPosition(gateSpawnPos));
            ecb.AddComponent(newGate, new GateData { AbsorbedShadows = 0, IsActive = true });

            // 임시
            int enemiesPerGate = 5;
            for (int e = 0; e < enemiesPerGate; e++)
            {
                Entity newEnemy = ecb.Instantiate(directorData.EnemyPrefab);

                float3 enemySpawnPos = gateSpawnPos + new float3(_random.NextFloat(-3f, 3f), 0.5f, _random.NextFloat(-3f, 3f));
                if (SystemAPI.HasComponent<Parent>(directorData.EnemyPrefab))
                {
                    ecb.RemoveComponent<Parent>(newEnemy);
                }
                ecb.SetComponent(newEnemy, LocalTransform.FromPosition(enemySpawnPos));
                ecb.SetComponent(newEnemy, new EnemyTargetData
                {
                    CurrentTarget = Entity.Null,
                    IsTargetingShadow = false
                });

                if (SystemAPI.HasComponent<PhysicsGraphicalInterpolationBuffer>(directorData.EnemyPrefab))
                {
                    ecb.SetComponent(newEnemy, new PhysicsGraphicalInterpolationBuffer
                    {
                        PreviousTransform = new RigidTransform(quaternion.identity, enemySpawnPos) 
                    });
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}