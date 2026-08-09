using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using UnityEngine;
using Unity.Collections;
using VSurvivors.Battle.Physics;

[BurstCompile]
public partial struct GameDirectorSystem : ISystem
{
    private Unity.Mathematics.Random _random;

    private static void CreateBossArenaFence(float3 playerPos, float3 bossPos, ref EntityCommandBuffer ecb)
    {
        float3 center = (playerPos + bossPos) * 0.5f;
        center.y = 1.5f;

        float horizontalDistance = math.distance(new float2(playerPos.x, playerPos.z), new float2(bossPos.x, bossPos.z));
        float radius = math.max(12f, (horizontalDistance * 0.5f) + 6f);

        const int segmentCount = 20;
        const float segmentThickness = 0.8f;
        const float segmentHeight = 3f;
        float arcLength = (2f * math.PI * radius) / segmentCount;

        for (int i = 0; i < segmentCount; i++)
        {
            float angle = (math.PI * 2f * i) / segmentCount;
            float3 outward = new float3(math.cos(angle), 0f, math.sin(angle));
            float3 segPos = center + outward * radius;

            quaternion segRot = math.mul(
                quaternion.LookRotationSafe(outward, math.up()),
                quaternion.RotateY(math.radians(90f)));
            float3 halfExtents = new float3(segmentThickness * 0.5f, segmentHeight * 0.5f, arcLength * 0.5f);

            var collider = Unity.Physics.BoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = halfExtents * 2f,
                BevelRadius = 0f
            });

            Entity segEntity = ecb.CreateEntity();
            ecb.AddComponent(segEntity, LocalTransform.FromPositionRotationScale(segPos, segRot, 1f));
            ecb.AddComponent(segEntity, new PhysicsCollider { Value = collider });
            ecb.AddComponent(segEntity, new CustomCollisionFilter
            {
                Value = new CollisionFilter
                {
                    BelongsTo = GamePhysicsLayers.Structure,
                    CollidesWith = GamePhysicsLayers.StructureMask,
                    GroupIndex = 0
                }
            });
            ecb.AddComponent<BossArenaFenceTag>(segEntity);
        }
    }

    private static void RemoveDynamicPhysicsBody(ref EntityCommandBuffer ecb, Entity entity, Entity prefab, ref SystemState state)
    {
        if (state.EntityManager.HasComponent<PhysicsVelocity>(prefab))
        {
            ecb.RemoveComponent<PhysicsVelocity>(entity);
        }

        if (state.EntityManager.HasComponent<PhysicsMass>(prefab))
        {
            ecb.RemoveComponent<PhysicsMass>(entity);
        }

        if (state.EntityManager.HasComponent<PhysicsDamping>(prefab))
        {
            ecb.RemoveComponent<PhysicsDamping>(entity);
        }

        if (state.EntityManager.HasComponent<PhysicsGravityFactor>(prefab))
        {
            ecb.RemoveComponent<PhysicsGravityFactor>(entity);
        }
    }

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameDirectorData>();
        state.RequireForUpdate<PlayerInput>();
        state.RequireForUpdate<ConstConfigData>();

        uint seed = (uint)System.DateTime.Now.Ticks;
        if (seed == 0) seed = 1;
        _random = new Unity.Mathematics.Random(seed);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var directorEntity = SystemAPI.GetSingletonEntity<GameDirectorData>();
        var directorData = SystemAPI.GetComponentRW<GameDirectorData>(directorEntity);
        var constData = SystemAPI.GetSingleton<ConstConfigData>();
        float deltaTime = SystemAPI.Time.DeltaTime;

        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        var newPortalPositions = new NativeList<float3>(16, Allocator.Temp);

        foreach (var (clearTag, entity) in SystemAPI.Query<RefRO<ClearNormalEnemiesEventTag>>().WithEntityAccess())
        {
            ecb.DestroyEntity(entity);
            foreach (var (health, enemyEntity) in SystemAPI.Query<RefRW<HealthData>>().WithAll<EnemyTag, CEnemyData>().WithNone<BossTag>().WithEntityAccess())
            {
                ecb.AddComponent<NoDropOnDeathTag>(enemyEntity);
                health.ValueRW.CurrentHealth = 0f;
                ecb.AddComponent<DestroyEntityTag>(enemyEntity);
            }

            foreach (var (_, portalEntity) in SystemAPI.Query<RefRO<CPortalData>>().WithEntityAccess())
            {
                ecb.AddComponent<DestroyEntityTag>(portalEntity);
            }
        }

        switch (directorData.ValueRO.CurrentPhase)
        {
            case GamePhase.NormalWave:
                ProcessNormalWave(ref state, directorData, constData, deltaTime, ref ecb, ref newPortalPositions);
                break;
            case GamePhase.BossFight:
                ProcessBossFight(ref state, directorData, constData, deltaTime, ref ecb);
                break;
            case GamePhase.EventPaused:
                break;
            case GamePhase.IsolatedBossFight:
                ProcessIsolatedBossFight(ref state, directorData, constData, deltaTime, ref ecb);
                break;
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        newPortalPositions.Dispose();
    }

    private void ProcessNormalWave(ref SystemState state, RefRW<GameDirectorData> data, ConstConfigData constData, float deltaTime, ref EntityCommandBuffer ecb, ref NativeList<float3> newPortalPositions)
    {
        data.ValueRW.GlobalTimer += deltaTime;
        float globalMinutes = data.ValueRO.GlobalTimer / 60f;

        float bossSpawnInterval = 300f;
        int expectedWave = (int)(data.ValueRO.GlobalTimer / bossSpawnInterval) + 1;

        if (expectedWave > data.ValueRO.CurrentWave)
        {
            data.ValueRW.CurrentWave = expectedWave;
            data.ValueRW.CurrentPhase = GamePhase.BossFight;
            data.ValueRW.BossTimer = constData.PortalBossTimer > 0f ? constData.PortalBossTimer : 180f;

            // 보스 페이즈 진입 시점에 일반 적/포탈을 즉시 정리합니다.
            foreach (var (health, enemyEntity) in SystemAPI.Query<RefRW<HealthData>>().WithAll<EnemyTag, CEnemyData>().WithNone<BossTag>().WithEntityAccess())
            {
                ecb.AddComponent<NoDropOnDeathTag>(enemyEntity);
                health.ValueRW.CurrentHealth = 0f;
                ecb.AddComponent<DestroyEntityTag>(enemyEntity);
            }

            foreach (var (_, portalEntity) in SystemAPI.Query<RefRO<CPortalData>>().WithEntityAccess())
            {
                ecb.AddComponent<DestroyEntityTag>(portalEntity);
            }

            var clearEventEntity = ecb.CreateEntity();
            ecb.AddComponent<ClearNormalEnemiesEventTag>(clearEventEntity);

            var bossEventEntity = ecb.CreateEntity();
            ecb.AddComponent(bossEventEntity, new SpawnBossEventTag { BossID = 31201011 });
            return;
        }

        int currentPortalCount = SystemAPI.QueryBuilder().WithAll<CPortalData>().Build().CalculateEntityCount();

        float createPhase = constData.PortalCreatePhase3;
        int maxPhase = constData.PortalMaxPhase3;
        if (globalMinutes < constData.PhaseTime1) { createPhase = constData.PortalCreatePhase1; maxPhase = constData.PortalMaxPhase1; }
        else if (globalMinutes < constData.PhaseTime1 + constData.PhaseTime2) { createPhase = constData.PortalCreatePhase2; maxPhase = constData.PortalMaxPhase2; }

        if (currentPortalCount < 3)
        {
            for (int i = currentPortalCount; i < 3; i++)
            {
                SpawnPortal(ref state, data.ValueRO, ref ecb, ref newPortalPositions);
            }
            currentPortalCount = 3;
            data.ValueRW.WaveTimer = createPhase;
        }

        data.ValueRW.WaveTimer -= deltaTime;
        if (data.ValueRO.WaveTimer <= 0f)
        {
            data.ValueRW.WaveTimer = createPhase > 0 ? createPhase : 60f;
            if (currentPortalCount < maxPhase)
            {
                SpawnPortal(ref state, data.ValueRO, ref ecb, ref newPortalPositions);
            }
        }

        if (!SystemAPI.TryGetSingleton<EnemyDatabaseComponent>(out var enemyDB)) return;

        data.ValueRW.EnemySpawnTimer -= deltaTime;
        if (data.ValueRO.EnemySpawnTimer <= 0f)
        {
            float summonPhase = constData.PortalSummonPhase3;
            if (globalMinutes < constData.PhaseTime1) summonPhase = constData.PortalSummonPhase1;
            else if (globalMinutes < constData.PhaseTime1 + constData.PhaseTime2) summonPhase = constData.PortalSummonPhase2;
            
            data.ValueRW.EnemySpawnTimer = summonPhase > 0 ? summonPhase : 5f;

            var enemyQuery = SystemAPI.QueryBuilder().WithAll<CEnemyData>().Build();
            int currentEnemyCount = enemyQuery.CalculateEntityCount();

            if (currentEnemyCount < 200)
            {
                int enemyIndexToSpawn = 0;
                if (enemyIndexToSpawn >= enemyDB.DatabaseRef.Value.Enemies.Length) return;

                ref var enemyDef = ref enemyDB.DatabaseRef.Value.Enemies[enemyIndexToSpawn];
                var baseEnemyData = SystemAPI.GetComponent<CEnemyData>(data.ValueRO.EnemyPrefab);
                
                float timeMultiplier = 1f + (float)math.floor(SystemAPI.Time.ElapsedTime / 60f) * 0.2f;

                foreach (var (portalTransform, cPortalData) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<CPortalData>>())
                {
                    if (currentEnemyCount >= 200) break;

                    var enemyEntity = ecb.Instantiate(data.ValueRO.EnemyPrefab);
                    RemoveDynamicPhysicsBody(ref ecb, enemyEntity, data.ValueRO.EnemyPrefab, ref state);
                    float2 randomOffset = _random.NextFloat2Direction() * _random.NextFloat(0.5f, 2f);
                    float3 spawnPos = portalTransform.ValueRO.Position + new float3(randomOffset.x, 0.5f, randomOffset.y);

                    ecb.SetComponent(enemyEntity, new LocalTransform { Position = spawnPos, Rotation = quaternion.identity, Scale = 1f });
                    if (SystemAPI.HasComponent<PhysicsGraphicalInterpolationBuffer>(data.ValueRO.EnemyPrefab))
                    {
                        var initTransform = new RigidTransform(quaternion.identity, spawnPos);
                        ecb.SetComponent(enemyEntity, new PhysicsGraphicalInterpolationBuffer { PreviousTransform = initTransform });
                    }
                    var newEnemyData = new CEnemyData
                    {
                        ID = enemyDef.ID, Type = enemyDef.Type, CurrentState = baseEnemyData.CurrentState,
                        AttackPrefab = baseEnemyData.AttackPrefab, AttackPower = enemyDef.AttackPower * timeMultiplier,
                        AttackRange = enemyDef.AttackRange, AttackCooldown = enemyDef.AttackCooldown,
                        CurrentCooldown = 0f, MoveSpeed = enemyDef.MoveSpeed, IsBoss = enemyDef.IsBoss, IsAlive = true
                    };
                    ecb.SetComponent(enemyEntity, newEnemyData);
                    
                    var baseHealthData = SystemAPI.GetComponent<HealthData>(data.ValueRO.EnemyPrefab);
                    ecb.SetComponent(enemyEntity, new HealthData
                    {
                        MaxHealth = enemyDef.MaxHealth * timeMultiplier, CurrentHealth = enemyDef.MaxHealth * timeMultiplier,
                        DamageReduction = enemyDef.Defence, InvincibilityTimer = baseHealthData.InvincibilityTimer
                    });
                    
                    ecb.AddComponent<EnemyTag>(enemyEntity);
                    currentEnemyCount++;
                }
            }
        }
    }

    private void ProcessBossFight(ref SystemState state, RefRW<GameDirectorData> data, ConstConfigData constData, float deltaTime, ref EntityCommandBuffer ecb)
    {
        foreach (var (bossEvent, entity) in SystemAPI.Query<RefRO<SpawnBossEventTag>>().WithEntityAccess())
        {
            if (bossEvent.ValueRO.IsIsolatedBoss) continue;

            ecb.DestroyEntity(entity);
            if (!SystemAPI.TryGetSingleton<EnemyDatabaseComponent>(out var enemyDB)) continue;
            
            int bossIndexToSpawn = -1;
            for (int i = 0; i < enemyDB.DatabaseRef.Value.Enemies.Length; i++)
            {
                ref var candidate = ref enemyDB.DatabaseRef.Value.Enemies[i];
                if (!candidate.IsBoss) continue;

                if (candidate.ID == bossEvent.ValueRO.BossID)
                {
                    bossIndexToSpawn = i;
                    break;
                }

                if (bossIndexToSpawn == -1)
                {
                    bossIndexToSpawn = i; // fallback: first boss in DB
                }
            }

            if (bossIndexToSpawn != -1)
            {
                ref var bossDef = ref enemyDB.DatabaseRef.Value.Enemies[bossIndexToSpawn];
                var bossEntity = ecb.Instantiate(data.ValueRO.BossPrefab);
                RemoveDynamicPhysicsBody(ref ecb, bossEntity, data.ValueRO.BossPrefab, ref state);

                float3 spawnPos = float3.zero;
                float3 playerPosForBoss = float3.zero;
                foreach (var (playerTrans, playerData) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerData>>())
                {
                    spawnPos = playerTrans.ValueRO.Position;
                    playerPosForBoss = playerTrans.ValueRO.Position;
                    break;
                }

                float2 randomOffset = _random.NextFloat2Direction() * 20f;
                spawnPos += new float3(randomOffset.x, 0, randomOffset.y);
                spawnPos.y = 0.5f;

                float timeMultiplier = 1f + (float)math.floor(SystemAPI.Time.ElapsedTime / 60f) * 0.2f;

                ecb.SetComponent(bossEntity, new LocalTransform { Position = spawnPos, Rotation = quaternion.identity, Scale = 3f });
                if (SystemAPI.HasComponent<PhysicsGraphicalInterpolationBuffer>(data.ValueRO.BossPrefab))
                {
                    var initTransform = new RigidTransform(quaternion.identity, spawnPos);
                    ecb.SetComponent(bossEntity, new PhysicsGraphicalInterpolationBuffer { PreviousTransform = initTransform });
                }

                var baseEnemyData = SystemAPI.GetComponent<CEnemyData>(data.ValueRO.BossPrefab);
                var newBossData = new CEnemyData
                {
                    ID = bossDef.ID, Type = bossDef.Type, CurrentState = baseEnemyData.CurrentState,
                    AttackPrefab = baseEnemyData.AttackPrefab, AttackPower = bossDef.AttackPower * timeMultiplier,
                    AttackRange = bossDef.AttackRange, AttackCooldown = bossDef.AttackCooldown,
                    CurrentCooldown = 0f, MoveSpeed = bossDef.MoveSpeed, IsBoss = true, IsAlive = true
                };

                ecb.SetComponent(bossEntity, newBossData);

                var baseBossHealth = SystemAPI.GetComponent<HealthData>(data.ValueRO.BossPrefab);
                ecb.SetComponent(bossEntity, new HealthData
                {
                    MaxHealth = bossDef.MaxHealth * timeMultiplier, CurrentHealth = bossDef.MaxHealth * timeMultiplier,
                    DamageReduction = bossDef.Defence, InvincibilityTimer = baseBossHealth.InvincibilityTimer
                });

                ecb.AddComponent<EnemyTag>(bossEntity);
                ecb.AddComponent<BossTag>(bossEntity);

                foreach (var (_, fenceEntity) in SystemAPI.Query<RefRO<BossArenaFenceTag>>().WithEntityAccess())
                {
                    ecb.AddComponent<DestroyEntityTag>(fenceEntity);
                }
                CreateBossArenaFence(playerPosForBoss, spawnPos, ref ecb);

                float horizontalDistance = math.distance(new float2(playerPosForBoss.x, playerPosForBoss.z), new float2(spawnPos.x, spawnPos.z));
                data.ValueRW.BossArenaCenter = (playerPosForBoss + spawnPos) * 0.5f;
                data.ValueRW.BossArenaCenter.y = 0f;
                data.ValueRW.BossArenaRadius = math.max(12f, (horizontalDistance * 0.5f) + 6f);
                data.ValueRW.BossArenaActive = true;
            }
        }

        bool hasAliveBossEntity = false;
        foreach (var (enemyData, entity) in SystemAPI.Query<RefRO<CEnemyData>>().WithEntityAccess())
        {
            if (enemyData.ValueRO.IsBoss)
            {
                bool isDeadTagged = SystemAPI.HasComponent<DeathTag>(entity);
                if (enemyData.ValueRO.IsAlive && !isDeadTagged)
                {
                    hasAliveBossEntity = true;
                    break;
                }
            }
        }
        
        bool hasSpawnEventPending = false;
        foreach (var evt in SystemAPI.Query<RefRO<SpawnBossEventTag>>())
        {
            if (!evt.ValueRO.IsIsolatedBoss) { hasSpawnEventPending = true; break; }
        }

        if (!hasAliveBossEntity && !hasSpawnEventPending)
        {
            foreach (var (_, fenceEntity) in SystemAPI.Query<RefRO<BossArenaFenceTag>>().WithEntityAccess())
            {
                ecb.AddComponent<DestroyEntityTag>(fenceEntity);
            }
            data.ValueRW.BossArenaActive = false;
            data.ValueRW.BossArenaRadius = 0f;
            data.ValueRW.CurrentPhase = GamePhase.NormalWave;
        }

        if (data.ValueRO.BossTimer > 0f && hasAliveBossEntity)
        {
            data.ValueRW.BossTimer -= deltaTime;
            if (data.ValueRO.BossTimer <= 0f)
            {
                var playerDeathEvent = ecb.CreateEntity();
                ecb.AddComponent<PlayerDeathEventTag>(playerDeathEvent);
            }
        }
        
        // 보스전 중에도 포탈에서 몬스터 스폰은 계속 허용 (요청사항)
        if (!SystemAPI.TryGetSingleton<EnemyDatabaseComponent>(out var enemyDB2)) return;

        data.ValueRW.EnemySpawnTimer -= deltaTime;
        if (data.ValueRO.EnemySpawnTimer <= 0f)
        {
            float summonPhase = constData.PortalSummonPhase3; 
            // 보스 페이즈는 일반적으로 마지막 페이즈 취급 혹은 5초 유지
            data.ValueRW.EnemySpawnTimer = summonPhase > 0 ? summonPhase : 5f;

            var enemyQuery = SystemAPI.QueryBuilder().WithAll<CEnemyData>().Build();
            int currentEnemyCount = enemyQuery.CalculateEntityCount();

            if (currentEnemyCount < 200)
            {
                int enemyIndexToSpawn = 0;
                if (enemyIndexToSpawn >= enemyDB2.DatabaseRef.Value.Enemies.Length) return;

                ref var enemyDef = ref enemyDB2.DatabaseRef.Value.Enemies[enemyIndexToSpawn];
                var baseEnemyData = SystemAPI.GetComponent<CEnemyData>(data.ValueRO.EnemyPrefab);
                
                float timeMultiplier = 1f + (float)math.floor(SystemAPI.Time.ElapsedTime / 60f) * 0.2f;

                foreach (var (portalTransform, cPortalData) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<CPortalData>>().WithNone<HiddenIsolatedPortalTag>())
                {
                    if (currentEnemyCount >= 200) break;

                    var enemyEntity = ecb.Instantiate(data.ValueRO.EnemyPrefab);
                    RemoveDynamicPhysicsBody(ref ecb, enemyEntity, data.ValueRO.EnemyPrefab, ref state);
                    float2 randomOffset = _random.NextFloat2Direction() * _random.NextFloat(0.5f, 2f);
                    float3 spawnPos = portalTransform.ValueRO.Position + new float3(randomOffset.x, 0.5f, randomOffset.y);

                    ecb.SetComponent(enemyEntity, new LocalTransform { Position = spawnPos, Rotation = quaternion.identity, Scale = 1f });
                    if (SystemAPI.HasComponent<PhysicsGraphicalInterpolationBuffer>(data.ValueRO.EnemyPrefab))
                    {
                        var initTransform = new RigidTransform(quaternion.identity, spawnPos);
                        ecb.SetComponent(enemyEntity, new PhysicsGraphicalInterpolationBuffer { PreviousTransform = initTransform });
                    }
                    var newEnemyData = new CEnemyData
                    {
                        ID = enemyDef.ID, Type = enemyDef.Type, CurrentState = baseEnemyData.CurrentState,
                        AttackPrefab = baseEnemyData.AttackPrefab, AttackPower = enemyDef.AttackPower * timeMultiplier,
                        AttackRange = enemyDef.AttackRange, AttackCooldown = enemyDef.AttackCooldown,
                        CurrentCooldown = 0f, MoveSpeed = enemyDef.MoveSpeed, IsBoss = enemyDef.IsBoss, IsAlive = true
                    };
                    ecb.SetComponent(enemyEntity, newEnemyData);
                    
                    var baseHealthData = SystemAPI.GetComponent<HealthData>(data.ValueRO.EnemyPrefab);
                    ecb.SetComponent(enemyEntity, new HealthData
                    {
                        MaxHealth = enemyDef.MaxHealth * timeMultiplier, CurrentHealth = enemyDef.MaxHealth * timeMultiplier,
                        DamageReduction = enemyDef.Defence, InvincibilityTimer = baseHealthData.InvincibilityTimer
                    });
                    
                    ecb.AddComponent<EnemyTag>(enemyEntity);
                    currentEnemyCount++;
                }
            }
        }
    }

    private void ProcessIsolatedBossFight(ref SystemState state, RefRW<GameDirectorData> data, ConstConfigData constData, float deltaTime, ref EntityCommandBuffer ecb)
    {
        foreach (var (bossEvent, entity) in SystemAPI.Query<RefRO<SpawnBossEventTag>>().WithEntityAccess())
        {
            if (!bossEvent.ValueRO.IsIsolatedBoss) continue;

            ecb.DestroyEntity(entity);
            if (!SystemAPI.TryGetSingleton<EnemyDatabaseComponent>(out var enemyDB)) continue;
            
            int bossIndexToSpawn = -1;
            for (int i = 0; i < enemyDB.DatabaseRef.Value.Enemies.Length; i++)
            {
                ref var candidate = ref enemyDB.DatabaseRef.Value.Enemies[i];
                if (!candidate.IsBoss) continue;

                if (candidate.ID == bossEvent.ValueRO.BossID)
                {
                    bossIndexToSpawn = i;
                    break;
                }

                if (bossIndexToSpawn == -1)
                {
                    bossIndexToSpawn = i; // fallback: first boss in DB
                }
            }

            if (bossIndexToSpawn != -1)
            {
                ref var bossDef = ref enemyDB.DatabaseRef.Value.Enemies[bossIndexToSpawn];
                var bossEntity = ecb.Instantiate(data.ValueRO.BossPrefab);
                RemoveDynamicPhysicsBody(ref ecb, bossEntity, data.ValueRO.BossPrefab, ref state);

                float3 spawnPos = new float3(10000, 0.5f, 10000);

                float2 randomOffset = _random.NextFloat2Direction() * 10f;
                spawnPos += new float3(randomOffset.x, 0, randomOffset.y);
                spawnPos.y = 0.5f;

                float timeMultiplier = 1f + (float)math.floor(SystemAPI.Time.ElapsedTime / 60f) * 0.2f;

                ecb.SetComponent(bossEntity, new LocalTransform { Position = spawnPos, Rotation = quaternion.identity, Scale = 3f });
                if (SystemAPI.HasComponent<PhysicsGraphicalInterpolationBuffer>(data.ValueRO.BossPrefab))
                {
                    var initTransform = new RigidTransform(quaternion.identity, spawnPos);
                    ecb.SetComponent(bossEntity, new PhysicsGraphicalInterpolationBuffer { PreviousTransform = initTransform });
                }

                var baseEnemyData = SystemAPI.GetComponent<CEnemyData>(data.ValueRO.BossPrefab);
                var newBossData = new CEnemyData
                {
                    ID = bossDef.ID, Type = bossDef.Type, CurrentState = baseEnemyData.CurrentState,
                    AttackPrefab = baseEnemyData.AttackPrefab, AttackPower = bossDef.AttackPower * timeMultiplier,
                    AttackRange = bossDef.AttackRange, AttackCooldown = bossDef.AttackCooldown,
                    CurrentCooldown = 0f, MoveSpeed = bossDef.MoveSpeed, IsBoss = true, IsAlive = true
                };

                ecb.SetComponent(bossEntity, newBossData);

                var baseBossHealth = SystemAPI.GetComponent<HealthData>(data.ValueRO.BossPrefab);
                ecb.SetComponent(bossEntity, new HealthData
                {
                    MaxHealth = bossDef.MaxHealth * timeMultiplier, CurrentHealth = bossDef.MaxHealth * timeMultiplier,
                    DamageReduction = bossDef.Defence, InvincibilityTimer = baseBossHealth.InvincibilityTimer
                });

                ecb.AddComponent<EnemyTag>(bossEntity);
                ecb.AddComponent<BossTag>(bossEntity);
                ecb.AddComponent<IsolatedBossTag>(bossEntity);

                float3 isolatedPlayerPos = new float3(10000, 1f, 10000);
                foreach (var (_, fenceEntity) in SystemAPI.Query<RefRO<BossArenaFenceTag>>().WithEntityAccess())
                {
                    ecb.AddComponent<DestroyEntityTag>(fenceEntity);
                }
                CreateBossArenaFence(isolatedPlayerPos, spawnPos, ref ecb);

                float horizontalDistance = math.distance(new float2(isolatedPlayerPos.x, isolatedPlayerPos.z), new float2(spawnPos.x, spawnPos.z));
                data.ValueRW.BossArenaCenter = (isolatedPlayerPos + spawnPos) * 0.5f;
                data.ValueRW.BossArenaCenter.y = 0f;
                data.ValueRW.BossArenaRadius = math.max(12f, (horizontalDistance * 0.5f) + 6f);
                data.ValueRW.BossArenaActive = true;
            }
        }

        bool hasAliveBossEntity = false;
        foreach (var (enemyData, entity) in SystemAPI.Query<RefRO<CEnemyData>>().WithAll<IsolatedBossTag>().WithEntityAccess())
        {
            if (enemyData.ValueRO.IsBoss)
            {
                bool isDeadTagged = SystemAPI.HasComponent<DeathTag>(entity);
                if (enemyData.ValueRO.IsAlive && !isDeadTagged)
                {
                    hasAliveBossEntity = true;
                    break;
                }
            }
        }
        
        bool hasSpawnEventPending = false;
        foreach (var evt in SystemAPI.Query<RefRO<SpawnBossEventTag>>())
        {
            if (evt.ValueRO.IsIsolatedBoss) { hasSpawnEventPending = true; break; }
        }

        if (!hasAliveBossEntity && !hasSpawnEventPending)
        {
            // Boss died! Teleport player back and destroy portal
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerInput>();
            ecb.SetComponent(playerEntity, new LocalTransform { Position = data.ValueRO.SavedPlayerPosition, Rotation = quaternion.identity, Scale = 1f });

            foreach (var (_, fenceEntity) in SystemAPI.Query<RefRO<BossArenaFenceTag>>().WithEntityAccess())
            {
                ecb.AddComponent<DestroyEntityTag>(fenceEntity);
            }
            data.ValueRW.BossArenaActive = false;
            data.ValueRW.BossArenaRadius = 0f;

            if (data.ValueRO.ActiveIsolatedPortal != Entity.Null && SystemAPI.Exists(data.ValueRO.ActiveIsolatedPortal))
            {
                var portalData = SystemAPI.GetComponent<CPortalData>(data.ValueRO.ActiveIsolatedPortal);
                portalData.AbsorbedShadows = portalData.RequiredShadows;
                ecb.SetComponent(data.ValueRO.ActiveIsolatedPortal, portalData);
                ecb.RemoveComponent<HiddenIsolatedPortalTag>(data.ValueRO.ActiveIsolatedPortal);
                
                // Allow portal interactions to destroy it
                ecb.AddComponent<DestroyEntityTag>(data.ValueRO.ActiveIsolatedPortal);
            }

            data.ValueRW.CurrentPhase = data.ValueRO.PreviousPhase;
        }

        if (data.ValueRO.BossTimer > 0f && hasAliveBossEntity)
        {
            data.ValueRW.BossTimer -= deltaTime;
            if (data.ValueRO.BossTimer <= 0f)
            {
                var playerDeathEvent = ecb.CreateEntity();
                ecb.AddComponent<PlayerDeathEventTag>(playerDeathEvent);
            }
        }
        
        // During Isolated Boss Fight, DO NOT spawn regular enemies from portals
    }

    private void SpawnPortal(ref SystemState state, GameDirectorData directorData, ref EntityCommandBuffer ecb, ref NativeList<float3> newPortalPositions)
    {
        if (directorData.PortalPrefab == Entity.Null) return;
        if (!SystemAPI.HasSingleton<CurrentStageConfig>()) return;
        
        var stageConfig = SystemAPI.GetSingleton<CurrentStageConfig>();
        var portalBuffer = SystemAPI.GetSingletonBuffer<PortalConfigElement>();

        var playerEntity = SystemAPI.GetSingletonEntity<PlayerInput>();
        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

        float offScreenRadius = 30f;
        float3 spawnPos = float3.zero;
        bool validPosFound = false;
        int maxAttempts = 10;

        for (int i = 0; i < maxAttempts; i++)
        {
            float angle = _random.NextFloat(0, math.PI * 2);
            spawnPos = playerPos + new float3(math.cos(angle) * offScreenRadius, 1, math.sin(angle) * offScreenRadius);
            
            bool tooClose = false;
            foreach (var (portalTransform, _) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<CPortalData>>())
            {
                if (math.distancesq(spawnPos, portalTransform.ValueRO.Position) < 225f) // min 15 distance
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                for (int j = 0; j < newPortalPositions.Length; j++)
                {
                    if (math.distancesq(spawnPos, newPortalPositions[j]) < 225f)
                    {
                        tooClose = true;
                        break;
                    }
                }
            }

            if (!tooClose)
            {
                validPosFound = true;
                newPortalPositions.Add(spawnPos);
                break;
            }
        }

        var portalEntity = ecb.Instantiate(directorData.PortalPrefab);
        if (SystemAPI.HasComponent<Parent>(directorData.PortalPrefab))
        {
            ecb.RemoveComponent<Parent>(portalEntity);
        }
        ecb.SetComponent(portalEntity, new LocalTransform { Position = spawnPos, Rotation = quaternion.identity, Scale = 1f });

        int totalChance = stageConfig.Chance1 + stageConfig.Chance2 + stageConfig.Chance3;
        int chosenId = stageConfig.Portal1;
        if (totalChance > 0)
        {
            int r = _random.NextInt(totalChance);
            if (r < stageConfig.Chance1) chosenId = stageConfig.Portal1;
            else if (r < stageConfig.Chance1 + stageConfig.Chance2) chosenId = stageConfig.Portal2;
            else chosenId = stageConfig.Portal3;
        }
        
        PortalConfigElement chosenConfig = new PortalConfigElement { DelPortal = 3, SummonAmount = 1, Monster1 = 31101011 };
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
            MaxHoldTime = 3.0f,
            IsActive = true,
            State = 0
        });
    }
}

