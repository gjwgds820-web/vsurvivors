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

        // 주석 복구됨
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // 보스 진입시 모든 잡몹 클리어 이벤트 처리
        foreach (var (clearTag, entity) in SystemAPI.Query<RefRO<ClearNormalEnemiesEventTag>>().WithEntityAccess())
        {
            ecb.DestroyEntity(entity);
            foreach (var (health, enemyEntity) in SystemAPI.Query<RefRW<HealthData>>().WithAll<EnemyTag, CEnemyData>().WithNone<BossTag>().WithEntityAccess())
            {
                health.ValueRW.CurrentHealth = 0f;
            }
        }

        // 페이즈별 로직 분기
        switch (directorData.ValueRO.CurrentPhase)
        {
            case GamePhase.NormalWave:
                ProcessNormalWave(ref state, directorData, deltaTime, ref ecb);
                break;
            case GamePhase.BossFight:
                ProcessBossFight(ref state, directorData, deltaTime, ref ecb);
                break;
            case GamePhase.EventPaused:
                // 주석 복구됨
                break;
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    private void ProcessNormalWave(ref SystemState state, RefRW<GameDirectorData> data, float deltaTime, ref EntityCommandBuffer ecb)
    {
        // (주석 복구됨)
        data.ValueRW.GlobalTimer += deltaTime;

        // 300�?주기 ?�인
        float bossSpawnInterval = 300f;
        int expectedWave = (int)(data.ValueRO.GlobalTimer / bossSpawnInterval) + 1;

        if (expectedWave > data.ValueRO.CurrentWave)
        {
            // 보스 ?�이�?진입
            data.ValueRW.CurrentWave = expectedWave;
            data.ValueRW.CurrentPhase = GamePhase.BossFight;
            data.ValueRW.BossTimer = 180f;

            // 주석 복구됨
            var clearEventEntity = ecb.CreateEntity();
            ecb.AddComponent<ClearNormalEnemiesEventTag>(clearEventEntity);

            // 주석 복구됨
            var bossEventEntity = ecb.CreateEntity();
            ecb.AddComponent(bossEventEntity, new SpawnBossEventTag { BossID = data.ValueRO.CurrentWave });
            return;
        }

        // 주석 복구됨
        data.ValueRW.WaveTimer -= deltaTime;
        if (data.ValueRO.WaveTimer <= 0f)
        {
            data.ValueRW.WaveTimer = 60f; // 주석 복구됨
            SpawnGate(ref state, data.ValueRO, ref ecb);
        }

        if (!SystemAPI.TryGetSingleton<EnemyDatabaseComponent>(out var enemyDB)) return;

        // 주석 복구됨
        data.ValueRW.EnemySpawnTimer -= deltaTime;
        if (data.ValueRO.EnemySpawnTimer <= 0f)
        {
            data.ValueRW.EnemySpawnTimer = 5f; // 주석 복구됨

            // 현재 맵에 존재하는 몬스터 수 확인
            var enemyQuery = SystemAPI.QueryBuilder().WithAll<CEnemyData>().Build();
            int currentEnemyCount = enemyQuery.CalculateEntityCount();

            // 주석 복구됨
            if (currentEnemyCount < 200)
            {
                int enemyIndexToSpawn = 0;
                if (enemyIndexToSpawn >= enemyDB.DatabaseRef.Value.Enemies.Length) return;

                ref var enemyDef = ref enemyDB.DatabaseRef.Value.Enemies[enemyIndexToSpawn];

                var baseEnemyData = SystemAPI.GetComponent<CEnemyData>(data.ValueRO.EnemyPrefab);
                // 현재 생성된 몹의 위치 확인
                foreach (var (gateTransform, gateData) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<GateData>>())
                {
                    // 최�? 마릿?��? ?��? ?�도�?방어코드 추�?
                    if (currentEnemyCount >= 200) break;

                    // 주석 복구됨
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
                        AttackPower = enemyDef.AttackPower,
                        AttackRange = enemyDef.AttackRange,
                        AttackCooldown = enemyDef.AttackCooldown,
                        CurrentCooldown = 0f,
                        MoveSpeed = enemyDef.MoveSpeed,
                        HitBoxShape = enemyDef.HitBoxShape,
                        HitboxRadius = enemyDef.HitboxRadius,
                        HitboxDuration = enemyDef.HitboxDuration,
                        IsPiercing = enemyDef.IsPiercing,
                        IsBoss = enemyDef.IsBoss,
                        IsAlive = true
                    };
                    ecb.SetComponent(enemyEntity, newEnemyData);
                    
                    var baseHealthData = SystemAPI.GetComponent<HealthData>(data.ValueRO.EnemyPrefab);
                    ecb.SetComponent(enemyEntity, new HealthData
                    {
                        MaxHealth = enemyDef.MaxHealth,
                        CurrentHealth = enemyDef.MaxHealth,
                        DamageReduction = baseHealthData.DamageReduction,
                        InvincibilityTimer = baseHealthData.InvincibilityTimer
                    });
                    
                    ecb.AddComponent<EnemyTag>(enemyEntity);
                    currentEnemyCount++;
                }
            }
        }
    }

    private void ProcessBossFight(ref SystemState state, RefRW<GameDirectorData> data, float deltaTime, ref EntityCommandBuffer ecb)
    {
        // 주석 복구됨
        foreach (var (bossEvent, entity) in SystemAPI.Query<RefRO<SpawnBossEventTag>>().WithEntityAccess())
        {
            ecb.DestroyEntity(entity);

            if (!SystemAPI.TryGetSingleton<EnemyDatabaseComponent>(out var enemyDB)) continue;
            
            // 주석 복구됨
            int bossIndexToSpawn = -1;
            for (int i = 0; i < enemyDB.DatabaseRef.Value.Enemies.Length; i++)
            {
                if (enemyDB.DatabaseRef.Value.Enemies[i].IsBoss)
                {
                    // 현재 조건에 따른 보스 선택
                    // n번째 보스의 ID를 기반으로 설정
                    bossIndexToSpawn = i;
                    // 주석 복구됨
                }
            }

            if (bossIndexToSpawn != -1)
            {
                ref var bossDef = ref enemyDB.DatabaseRef.Value.Enemies[bossIndexToSpawn];
                var bossEntity = ecb.Instantiate(data.ValueRO.BossPrefab);

                // (주석 복구됨)
                float3 spawnPos = float3.zero;
                foreach (var (playerTrans, playerData) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerData>>())
                {
                    spawnPos = playerTrans.ValueRO.Position;
                    break;
                }

                // (주석 복구됨)
                                  float2 randomOffset = _random.NextFloat2Direction() * 20f;
                  spawnPos += new float3(randomOffset.x, 0, randomOffset.y);
                  spawnPos.y = 1.0f; // 보스가 바닥에 끼여 하늘로 솟구치지 않도록 높이 보정

                ecb.SetComponent(bossEntity, new LocalTransform { Position = spawnPos, Rotation = quaternion.identity, Scale = 1f });
                if (SystemAPI.HasComponent<PhysicsGraphicalInterpolationBuffer>(data.ValueRO.BossPrefab))
                {
                    var initTransform = new RigidTransform(quaternion.identity, spawnPos);
                    ecb.SetComponent(bossEntity, new PhysicsGraphicalInterpolationBuffer
                    {
                        PreviousTransform = initTransform
                    });
                }

                var baseEnemyData = SystemAPI.GetComponent<CEnemyData>(data.ValueRO.BossPrefab);
                var newBossData = new CEnemyData
                {
                    ID = bossDef.ID,
                    Type = bossDef.Type,
                    CurrentState = baseEnemyData.CurrentState,
                    AttackPrefab = baseEnemyData.AttackPrefab,
                    AttackPower = bossDef.AttackPower,
                    AttackRange = bossDef.AttackRange,
                    AttackCooldown = bossDef.AttackCooldown,
                    CurrentCooldown = 0f,
                    MoveSpeed = bossDef.MoveSpeed,
                    HitBoxShape = bossDef.HitBoxShape,
                    HitboxRadius = bossDef.HitboxRadius,
                    HitboxDuration = bossDef.HitboxDuration,
                    IsPiercing = bossDef.IsPiercing,
                    IsBoss = true,
                    IsAlive = true
                };

                ecb.SetComponent(bossEntity, newBossData);

                var baseBossHealth = SystemAPI.GetComponent<HealthData>(data.ValueRO.BossPrefab);
                ecb.SetComponent(bossEntity, new HealthData
                {
                    MaxHealth = bossDef.MaxHealth,
                    CurrentHealth = bossDef.MaxHealth,
                    DamageReduction = baseBossHealth.DamageReduction,
                    InvincibilityTimer = baseBossHealth.InvincibilityTimer
                });

                ecb.AddComponent<EnemyTag>(bossEntity);
                ecb.AddComponent<BossTag>(bossEntity);
            }
        }

        // 팝업 종료 후 NormalWave 복귀
        // UI가 Element Ascension이나 Victory 이벤트 팝업을 처리하거나 종료되면,
        // (IsEventPaused 상태 기반 처리 완료 신호를 바탕으로) 복귀 로직은 여기서 추가
        // 팝업 종료 후 NormalWave 복귀

        // 보스 제한 타이머
        if (data.ValueRO.BossTimer > 0f)
        {
            data.ValueRW.BossTimer -= deltaTime;

            if (data.ValueRO.BossTimer <= 0f)
            {
                // 주석 복구됨
                var playerDeathEvent = ecb.CreateEntity();
                ecb.AddComponent<PlayerDeathEventTag>(playerDeathEvent);
            }
        }

        // 보스가 처리되었고 추가 스폰 예정이 없으면 NormalWave로 복귀
        bool hasBossesAlive = false;
        foreach (var enemyData in SystemAPI.Query<RefRO<CEnemyData>>())
        {
            if (enemyData.ValueRO.IsBoss && enemyData.ValueRO.IsAlive)
            {
                hasBossesAlive = true;
                break;
            }
        }
        
        bool hasSpawnEventPending = !SystemAPI.QueryBuilder().WithAll<SpawnBossEventTag>().Build().IsEmpty;

        if (!hasBossesAlive && !hasSpawnEventPending)
        {
            data.ValueRW.CurrentPhase = GamePhase.NormalWave;
            // 일반 몬스터 스폰 타이머 초기 보정 로직 (필요시 추가)
        }
    }

    private void SpawnGate(ref SystemState state, GameDirectorData directorData, ref EntityCommandBuffer ecb)
    {
        if (directorData.GatePrefab == Entity.Null) return;

        var playerEntity = SystemAPI.GetSingletonEntity<PlayerInput>();
        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

        int gatesToSpawn = math.min(3, directorData.CurrentWave); // 주석 복구됨
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




