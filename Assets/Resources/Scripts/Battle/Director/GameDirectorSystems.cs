using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics.GraphicsIntegration;
using UnityEngine;
using Unity.Collections;

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
        float deltaTime = SystemAPI.Time.DeltaTime;

        // 커맨드 버퍼 생성
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // 페이즈별 로직 분기
        switch (directorData.ValueRO.CurrentPhase)
        {
            case GamePhase.NormalWave:
                ProcessNormalWave(ref state, directorData, deltaTime, ref ecb);
                break;
            case GamePhase.BossFight:
                ProcessBossFight(directorData, deltaTime, ref ecb);
                break;
            case GamePhase.EventPaused:
                // 이벤트 진행 중에는 아무 것도 하지 않음
                break;
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    private void ProcessNormalWave(ref SystemState state, RefRW<GameDirectorData> data, float deltaTime, ref EntityCommandBuffer ecb)
    {
        // 전역 타이머 업데이트
        data.ValueRW.GlobalTimer += deltaTime;

        // 300초 주기 확인
        float bossSpawnInterval = 300f;
        int expectedWave = (int)(data.ValueRO.GlobalTimer / bossSpawnInterval) + 1;

        if (expectedWave > data.ValueRO.CurrentWave)
        {
            // 보스 페이즈 진입
            data.ValueRW.CurrentWave = expectedWave;
            data.ValueRW.CurrentPhase = GamePhase.BossFight;
            data.ValueRW.BossTimer = 180f;

            // 맵 상의 일반 몹 초기화 이벤트
            var clearEventEntity = ecb.CreateEntity();
            ecb.AddComponent<ClearNormalEnemiesEventTag>(clearEventEntity);

            // 보스 스폰 이벤트
            var bossEventEntity = ecb.CreateEntity();
            ecb.AddComponent(bossEventEntity, new SpawnBossEventTag { BossID = data.ValueRO.CurrentWave });
            return;
        }

        // 일반 몬스터, 게이트 스폰 타이머
        data.ValueRW.WaveTimer -= deltaTime;
        if (data.ValueRO.WaveTimer <= 0f)
        {
            data.ValueRW.WaveTimer = 60f; // 60초마다 스폰
            SpawnGate(ref state, data.ValueRO, ref ecb);
        }

        if (!SystemAPI.TryGetSingleton<EnemyDatabaseComponent>(out var enemyDB)) return;

        // 일반 몬스터 스폰 타이머
        data.ValueRW.EnemySpawnTimer -= deltaTime;
        if (data.ValueRO.EnemySpawnTimer <= 0f)
        {
            data.ValueRW.EnemySpawnTimer = 1f; // 1초마다 스폰 시도

            // 현재 맵에 존재하는 일반 몬스터 수 확인
            var enemyQuery = SystemAPI.QueryBuilder().WithAll<CEnemyData>().Build();
            int currentEnemyCount = enemyQuery.CalculateEntityCount();

            // 200마리 미만일때만 스폰
            if (currentEnemyCount < 200)
            {
                int enemyIndexToSpawn = 0;
                if (enemyIndexToSpawn >= enemyDB.DatabaseRef.Value.Enemies.Length) return;

                ref var enemyDef = ref enemyDB.DatabaseRef.Value.Enemies[enemyIndexToSpawn];

                var baseEnemyData = SystemAPI.GetComponent<CEnemyData>(data.ValueRO.EnemyPrefab);
                // 현재 활성화된 게이트 위치를 확인
                foreach (var (gateTransform, gateData) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<GateData>>())
                {
                    // 최대 마릿수를 넘지 않도록 방어코드 추가
                    if (currentEnemyCount >= 200) break;

                    // 게이트 주변에 몬스터 스폰
                    var enemyEntity = ecb.Instantiate(data.ValueRO.EnemyPrefab);
                    float2 randomOffset = _random.NextFloat2Direction() * _random.NextFloat(0.5f, 2f);
                    float3 spawnPos = gateTransform.ValueRO.Position + new float3(randomOffset.x, -0.5f, randomOffset.y);

                    ecb.SetComponent(enemyEntity, new LocalTransform { Position = spawnPos, Rotation = quaternion.identity, Scale = 1f });
                    if (SystemAPI.HasComponent<PhysicsGraphicalInterpolationBuffer>(data.ValueRO.EnemyPrefab))
                    {
                        var initTransform = new RigidTransform(quaternion.identity, spawnPos);
                        ecb.SetComponent(enemyEntity, new PhysicsGraphicalInterpolationBuffer
                        {
                            PreviousTransform = initTransform
                        });
                    }
                    var newEnemyData = new CEnemyData
                    {
                        ID = enemyDef.ID,
                        Type = enemyDef.Type,
                        CurrentState = baseEnemyData.CurrentState,
                        AttackPrefab = baseEnemyData.AttackPrefab,
                        MaxHealth = enemyDef.MaxHealth,
                        CurrentHealth = enemyDef.MaxHealth,
                        AttackPower = enemyDef.AttackPower,
                        AttackRange = enemyDef.AttackRange,
                        AttackCooldown = enemyDef.AttackCooldown,
                        CurrentCooldown = 0f,
                        MoveSpeed = enemyDef.MoveSpeed,
                        SearchTimer = 0f,
                        HitBoxShape = enemyDef.HitBoxShape,
                        HitboxRadius = enemyDef.HitboxRadius,
                        HitboxDuration = enemyDef.HitboxDuration,
                        IsPiercing = enemyDef.IsPiercing,
                        IsBoss = enemyDef.IsBoss,
                        IsAlive = true
                    };
                    ecb.SetComponent(enemyEntity, newEnemyData);
                    ecb.AddComponent<EnemyTag>(enemyEntity);
                    currentEnemyCount++;
                }
            }
        }
    }

    private void ProcessBossFight(RefRW<GameDirectorData> data, float deltaTime, ref EntityCommandBuffer ecb)
    {
        // 보스전 타이머
        if (data.ValueRO.BossTimer > 0f)
        {
            data.ValueRW.BossTimer -= deltaTime;

            if (data.ValueRO.BossTimer <= 0f)
            {
                // 플레이어 사망 이벤트
                var playerDeathEvent = ecb.CreateEntity();
                ecb.AddComponent<PlayerDeathEventTag>(playerDeathEvent);
            }
        }
    }

    private void SpawnGate(ref SystemState state, GameDirectorData directorData, ref EntityCommandBuffer ecb)
    {
        if (directorData.GatePrefab == Entity.Null) return;

        var playerEntity = SystemAPI.GetSingletonEntity<PlayerInput>();
        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

        int gatesToSpawn = math.min(3, directorData.CurrentWave); // 최대 3개의 게이트
        float offScreenRadius = 30f;
        for (int i = 0; i < gatesToSpawn; i++)
        {
            float angle = _random.NextFloat(0, math.PI * 2);
            float3 spawnPos = playerPos + new float3(math.cos(angle) * offScreenRadius, 1, math.sin(angle) * offScreenRadius);

            var gateEntity = ecb.Instantiate(directorData.GatePrefab);
            if (SystemAPI.HasComponent<Parent>(directorData.GatePrefab))
            {
                ecb.RemoveComponent<Parent>(gateEntity);
            }
            ecb.SetComponent(gateEntity, new LocalTransform { Position = spawnPos, Rotation = quaternion.identity, Scale = 1f });
            ecb.AddComponent(gateEntity, new GateData { AbsorbedShadows = 0, IsActive = true });
        }
    }
}