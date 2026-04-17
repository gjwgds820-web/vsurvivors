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
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

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
            SpawnPortal(ref state, data.ValueRO, ref ecb);
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
                foreach (var (portalTransform, CPortalData) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<CPortalData>>())
                {
                    // 최�? 마릿?��? ?��? ?�도�?방어코드 추�?
                    if (currentEnemyCount >= 200) break;

                    // 주석 복구됨
                    var enemyEntity = ecb.Instantiate(data.ValueRO.EnemyPrefab);
                    float2 randomOffset = _random.NextFloat2Direction() * _random.NextFloat(0.5f, 2f);
                    float3 spawnPos = portalTransform.ValueRO.Position + new float3(randomOffset.x, 0.5f, randomOffset.y); // 높이를 0.5f로 스폰

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
                  spawnPos.y = 0.5f; // 비주얼은 모두 y=0이 되므로 논리판정용 y좌표 0.5f로 통일

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

        // 보스가 처리되었고 (완전히 파괴되었고) 추가 스폰 예정이 없으면 NormalWave로 복귀
        bool hasBossEntity = false;
        bool isBossAlive = false;
        foreach (var enemyData in SystemAPI.Query<RefRO<CEnemyData>>())
        {
            if (enemyData.ValueRO.IsBoss)
            {
                hasBossEntity = true;
                if (enemyData.ValueRO.IsAlive)
                {
                    isBossAlive = true;
                }
                break;
            }
        }
        
        bool hasSpawnEventPending = !SystemAPI.QueryBuilder().WithAll<SpawnBossEventTag>().Build().IsEmpty;

        if (!hasBossEntity && !hasSpawnEventPending)
        {
            data.ValueRW.CurrentPhase = GamePhase.NormalWave;
            // 일반 몬스터 스폰 타이머 초기 보정 로직 (필요시 추가)
        }

        // 보스 제한 타이머 (살아있을 때만 감소)
        if (data.ValueRO.BossTimer > 0f && isBossAlive)
        {
            data.ValueRW.BossTimer -= deltaTime;

            if (data.ValueRO.BossTimer <= 0f)
            {
                // 주석 복구됨
                var playerDeathEvent = ecb.CreateEntity();
                ecb.AddComponent<PlayerDeathEventTag>(playerDeathEvent);
            }
        }
    }

    private void SpawnPortal(ref SystemState state, GameDirectorData directorData, ref EntityCommandBuffer ecb)
    {
        if (directorData.PortalPrefab == Entity.Null) return;
        if (!SystemAPI.HasSingleton<CurrentStageConfig>()) return;
        
        var stageConfig = SystemAPI.GetSingleton<CurrentStageConfig>();
        var portalBuffer = SystemAPI.GetSingletonBuffer<PortalConfigElement>();

        var playerEntity = SystemAPI.GetSingletonEntity<PlayerInput>();
        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

        int portalsToSpawn = math.min(3, directorData.CurrentWave);
        float offScreenRadius = 30f;
        for (int i = 0; i < portalsToSpawn; i++)
        {
            float angle = _random.NextFloat(0, math.PI * 2);
            float3 spawnPos = playerPos + new float3(math.cos(angle) * offScreenRadius, 1, math.sin(angle) * offScreenRadius);

            var portalEntity = ecb.Instantiate(directorData.PortalPrefab);
            if (SystemAPI.HasComponent<Parent>(directorData.PortalPrefab))
            {
                ecb.RemoveComponent<Parent>(portalEntity);
            }
            ecb.SetComponent(portalEntity, new LocalTransform { Position = spawnPos, Rotation = quaternion.identity, Scale = 1f });

            // Random portal pickup
            int totalChance = stageConfig.Chance1 + stageConfig.Chance2 + stageConfig.Chance3;
            int chosenId = stageConfig.Portal1;
            if (totalChance > 0)
            {
                int r = _random.NextInt(totalChance);
                if (r < stageConfig.Chance1) chosenId = stageConfig.Portal1;
                else if (r < stageConfig.Chance1 + stageConfig.Chance2) chosenId = stageConfig.Portal2;
                else chosenId = stageConfig.Portal3;
            }
            
            PortalConfigElement chosenConfig = new PortalConfigElement { DelPortal = 3, SummonAmount = 1, Monster1 = 310100001 };
            for (int p = 0; p < portalBuffer.Length; p++)
            {
                if (portalBuffer[p].ID == chosenId)
                {
                    chosenConfig = portalBuffer[p];
                    break;
                }
            }

            ecb.AddComponent(portalEntity, new CPortalData {
                PortalID = chosenConfig.ID,
                RequiredShadows = chosenConfig.DelPortal,
                SummonAmount = chosenConfig.SummonAmount,
                Monster1 = chosenConfig.Monster1,
                AbsorbedShadows = 0,
                InteractionRadius = 5.0f,
                AbsorbtionTimer = 0f,
                IsActive = true,
                State = 0
            });
        }
    }
}













