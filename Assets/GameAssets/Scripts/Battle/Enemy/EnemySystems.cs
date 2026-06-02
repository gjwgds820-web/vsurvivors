using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

#region Movement System
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct EnemyMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        bool isIsolatedPhase = false;
        if (SystemAPI.TryGetSingleton<GameDirectorData>(out var director))
        {
            isIsolatedPhase = director.CurrentPhase == GamePhase.IsolatedBossFight;
        }

        foreach (var (transform, enemyData, targetData, entity) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<CEnemyData>, RefRO<TargetingData>>().WithEntityAccess())
        {
            if (isIsolatedPhase && !SystemAPI.HasComponent<IsolatedBossTag>(entity)) continue;

            float3 fixedPos = transform.ValueRO.Position;
            fixedPos.y = 0.5f;
            transform.ValueRW.Position = fixedPos;

            transform.ValueRW.Rotation.value.x = 0;
            transform.ValueRW.Rotation.value.z = 0;
            transform.ValueRW.Rotation = math.normalize(transform.ValueRW.Rotation);

            if (enemyData.ValueRO.IsAttacking) continue;

            if (targetData.ValueRO.CurrentTarget == Entity.Null || !SystemAPI.Exists(targetData.ValueRO.CurrentTarget)) continue;

            float3 currentPos = transform.ValueRO.Position;
            float3 targetPos = SystemAPI.GetComponent<LocalTransform>(targetData.ValueRO.CurrentTarget).Position;

            float3 toTarget = targetPos - currentPos;
            toTarget.y = 0; 
            float distance = math.length(toTarget);

            if (distance > enemyData.ValueRO.AttackRange)
            {
                enemyData.ValueRW.CurrentState = EnemyState.Move;
                float3 moveDir = toTarget / distance;

                transform.ValueRW.Position += moveDir * enemyData.ValueRO.MoveSpeed * deltaTime;

                quaternion targetRot = quaternion.LookRotationSafe(moveDir, math.up());
                transform.ValueRW.Rotation = math.slerp(transform.ValueRO.Rotation, targetRot, deltaTime * 10f);
            }
            else
            {
                enemyData.ValueRW.CurrentState = EnemyState.Attack;
            }
        }
    }
}
#endregion
#region Combat System
[BurstCompile]
public partial struct EnemyCombatSystem : ISystem
{
    private ComponentLookup<LocalTransform> _transformLookup;
    private ComponentLookup<HitBoxData> _hitBoxLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        _hitBoxLookup = state.GetComponentLookup<HitBoxData>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _transformLookup.Update(ref state);
        _hitBoxLookup.Update(ref state);
        float deltaTime = SystemAPI.Time.DeltaTime;

        bool isIsolatedPhase = false;
        if (SystemAPI.TryGetSingleton<GameDirectorData>(out var director))
        {
            isIsolatedPhase = director.CurrentPhase == GamePhase.IsolatedBossFight;
        }

        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        foreach (var (enemyData, targetData, transform) in
                 SystemAPI.Query<RefRW<CEnemyData>, RefRO<TargetingData>, RefRO<LocalTransform>>())
        {
            if (isIsolatedPhase) continue;
            if (enemyData.ValueRO.IsBoss) continue; // 보스는 전용 Combat 로직(BossSystems) 사용

            enemyData.ValueRW.CurrentCooldown -= deltaTime;
            Entity currentTarget = targetData.ValueRO.CurrentTarget;
            if (enemyData.ValueRO.CurrentState != EnemyState.Attack) continue;
            if (currentTarget == Entity.Null || !SystemAPI.Exists(currentTarget)) continue;

            if (enemyData.ValueRO.CurrentCooldown <= 0)
            {
                
                if (currentTarget == Entity.Null || currentTarget.Index < 0) continue; // (주석 복구됨)
                if (_transformLookup.TryGetComponent(currentTarget, out var targetTransform))
                {
                    if (enemyData.ValueRO.AttackPrefab == Entity.Null)
                    {
                        UnityEngine.Debug.LogError("Attack Prefab is not assigned in EnemyData!");
                        continue;
                    }
                    float3 myPos = transform.ValueRO.Position;
                    float3 targetPos = targetTransform.Position;
                    
                    float3 dir2D = targetPos - myPos;
                    dir2D.y = 0;
                    if (math.lengthsq(dir2D) > 0.001f) dir2D = math.normalize(dir2D);
                    else dir2D = math.forward();

                    Entity hitbox = ecb.Instantiate(enemyData.ValueRO.AttackPrefab);

                    if (_hitBoxLookup.TryGetComponent(enemyData.ValueRO.AttackPrefab, out var prefabHitBox))
                    {
                        prefabHitBox.Damage = enemyData.ValueRO.AttackPower;
                        prefabHitBox.TargetFaction = 1;
                        
                        // 물리 반경 확대 복원
                        if (prefabHitBox.Shape == HitBoxShape.Circle || prefabHitBox.Shape == HitBoxShape.Cone)
                        {
                            prefabHitBox.Radius = math.max(prefabHitBox.Radius, enemyData.ValueRO.AttackRange + 0.2f);
                        }
                        
                        ecb.SetComponent(hitbox, prefabHitBox);
                    }

                    // [FIX] 물리 판정 도달 실패 문제를 해결하기 위해, 적중 중심 좌표를 위치시킴 (Melee 타격은 투사체 연산 거리를 약간 전진)
                    float3 spawnPos = myPos + (dir2D * (enemyData.ValueRO.AttackRange * 0.5f));
                    spawnPos.y = myPos.y + 0.5f;

                    ecb.SetComponent(hitbox, new LocalTransform
                    {
                        Position = spawnPos,
                        Scale = 1f,
                        Rotation = quaternion.LookRotationSafe(dir2D, math.up())
                    });

                    if (enemyData.ValueRO.Type == EnemyType.Ranged)
                    {
                        if (SystemAPI.HasComponent<ProjectileData>(enemyData.ValueRO.AttackPrefab))
                        {
                            var projData = SystemAPI.GetComponent<ProjectileData>(enemyData.ValueRO.AttackPrefab);
                            projData.Direction = dir2D;
                            projData.MaxDistance = enemyData.ValueRO.AttackRange;
                            ecb.SetComponent(hitbox, projData);
                        }
                        else
                        {
                            ecb.AddComponent(hitbox, new ProjectileData { Direction = dir2D, Speed = 10f, MaxDistance = enemyData.ValueRO.AttackRange });
                        }
                    }

                    enemyData.ValueRW.CurrentCooldown = enemyData.ValueRO.AttackCooldown;
                }
            }
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion

#region Death & Drop System
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(UnitHealthSystem))]
[UpdateBefore(typeof(VisualCleanupSystem))]
[BurstCompile]
public partial struct EnemyDeathSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        float time = (float)SystemAPI.Time.ElapsedTime;
        int killIncrement = 0;

        bool hasDropBank = SystemAPI.TryGetSingleton<DropBankData>(out var dropBank);

        foreach (var (enemyData, transform, entity) in
                 SystemAPI.Query<RefRO<CEnemyData>, RefRO<LocalTransform>>()
                 .WithAll<DeathTag>()
                 .WithNone<DestroyEntityTag>()
                 .WithEntityAccess())
        {
            if (enemyData.ValueRO.IsBoss)
            {
                if (!SystemAPI.HasComponent<IsolatedBossTag>(entity))
                {
                    // 현재 조건에 따른 보스 선택
                    int currentBossWave = 1;
                    if (SystemAPI.TryGetSingleton<GameDirectorData>(out var directorData))
                    {
                        currentBossWave = directorData.CurrentWave;
                    }

                    var eventEntity = ecb.CreateEntity();
                    if (currentBossWave >= 3)
                    {
                        ecb.AddComponent(eventEntity, new GameClearEventTag { ClearanceLevel = currentBossWave });
                    }
                    else
                    {
                        ecb.AddComponent(eventEntity, new ElementAscensionEventTag { BossLevel = currentBossWave });
                    }
                }
            }
            else
            {
                killIncrement++;
            }

            uint seed = (uint)(entity.Index + time * 100000f);
            if (seed == 0) seed = 1; // 0이 되면 Unity.Mathematics.Random 생성 시 예외 발생
            var random = Unity.Mathematics.Random.CreateFromIndex(seed);

            if (hasDropBank)
            {
                if (dropBank.ExpPrefab != Entity.Null)
                {
                    Entity expEntity = ecb.Instantiate(dropBank.ExpPrefab);
                    ecb.SetComponent(expEntity, new LocalTransform
                    {
                        Position = transform.ValueRO.Position,
                        Rotation = quaternion.identity,
                        Scale = 1f
                    });
                    ecb.AddComponent(expEntity, new DroppedItemData
                    {
                        Type = DropItemType.Exp,
                        Amount = 10f,
                        MoveSpeed = 15f,
                    });
                }

                float dropChance = random.NextFloat();
                if (dropChance <= 0.15f && dropBank.GoldPrefab != Entity.Null)
                {
                    Entity goldEntity = ecb.Instantiate(dropBank.GoldPrefab);
                    ecb.SetComponent(goldEntity, new LocalTransform
                    {
                        Position = transform.ValueRO.Position + new float3(0.5f, 0, 0),
                        Rotation = quaternion.identity,
                        Scale = 1f
                    });
                    ecb.AddComponent(goldEntity, new DroppedItemData
                    {
                        Type = DropItemType.Gold,
                        Amount = random.NextInt(100, 501),
                        MoveSpeed = 15f,
                    });
                }
                else if (dropChance > 0.15f && dropChance <= 0.35f && dropBank.MagnetPrefab != Entity.Null) // 20%
                {
                    Entity magnetEntity = ecb.Instantiate(dropBank.MagnetPrefab);
                    ecb.SetComponent(magnetEntity, new LocalTransform
                    {
                        Position = transform.ValueRO.Position + new float3(-0.5f, 0, 0),
                        Rotation = quaternion.identity,
                        Scale = 1f
                    });
                    ecb.AddComponent(magnetEntity, new DroppedItemData
                    {
                        Type = DropItemType.Magnet,
                        Amount = 1f,
                        MoveSpeed = 15f,
                    });
                }
                else if (dropChance > 0.35f && dropChance <= 0.60f && dropBank.BombPrefab != Entity.Null) // 25%
                {
                    Entity bombEntity = ecb.Instantiate(dropBank.BombPrefab);
                    ecb.SetComponent(bombEntity, new LocalTransform
                    {
                        Position = transform.ValueRO.Position + new float3(0, 0, 0.5f),
                        Rotation = quaternion.identity,
                        Scale = 1f
                    });
                    ecb.AddComponent(bombEntity, new DroppedItemData
                    {
                        Type = DropItemType.Bomb,
                        Amount = 1f,
                        MoveSpeed = 15f,
                    });
                }
            }

            ecb.AddComponent<DestroyEntityTag>(entity); // 사망 처리 완료
        }

        if (killIncrement > 0 && SystemAPI.TryGetSingletonRW<GameDirectorData>(out var dirDataRW))
        {
            dirDataRW.ValueRW.KilledEnemyCount += killIncrement;
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion












