using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

#region Movement System
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
[BurstCompile]
public partial struct EnemyMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld)) return;

        var collisionWorld = physicsWorld.CollisionWorld;

        float separationRadius = 1.5f;
        float separationWeight = 2.0f;

        foreach (var (transform, velocity, physicsMass, enemyData, targetData) in
                SystemAPI.Query<RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<PhysicsMass>, RefRW<CEnemyData>, RefRW<TargetingData>>())
        {
            physicsMass.ValueRW.InverseInertia = float3.zero;

            // [수정] 솟아오르는 것 및 땅으로 꺼지는 현상 방지 (상태와 무관하게 항상 Y, X, Z 회전 축 고정)
            float3 fixedPos = transform.ValueRO.Position;
            fixedPos.y = 0f;
            transform.ValueRW.Position = fixedPos;

            transform.ValueRW.Rotation.value.x = 0;
            transform.ValueRW.Rotation.value.z = 0;
            transform.ValueRW.Rotation = math.normalize(transform.ValueRW.Rotation);

            if (enemyData.ValueRO.IsAttacking) continue; // 보스 패턴 등 공격 중이면 일반 추적 정지

            if (targetData.ValueRO.CurrentTarget == Entity.Null || !SystemAPI.Exists(targetData.ValueRO.CurrentTarget))
            {
                velocity.ValueRW.Linear = new float3(0, velocity.ValueRO.Linear.y, 0);
                continue;
            }

            float3 currentPos = transform.ValueRO.Position;
            float3 targetPos = SystemAPI.GetComponent<LocalTransform>(targetData.ValueRO.CurrentTarget).Position;

            float3 toTarget = targetPos - currentPos;
            toTarget.y = 0; 
            float distance = math.length(toTarget);

            // 보스는 항상 유저를 추적하며, 자신의 공격 쿨타임과 사거리는 BossCombatSystem에서 자체적으로 계산합니다.
            if (enemyData.ValueRO.IsBoss || distance > enemyData.ValueRO.AttackRange)
            {
                enemyData.ValueRW.CurrentState = EnemyState.Move;
                float3 moveDir = toTarget / distance;
                float3 lookDir = moveDir;

                // Separation 계산
                float3 separationVec = float3.zero;
                CollisionFilter filter = CollisionFilter.Default;
                NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.Temp);
                if (collisionWorld.CalculateDistance(new PointDistanceInput 
                { 
                    Position = currentPos, 
                    MaxDistance = separationRadius, 
                    Filter = filter 
                }, ref hits))
                {
                    int neighborCount = 0;
                    for (int i = 0; i < hits.Length; i++)
                    {
                        float3 neighborPos = hits[i].Position;
                        float3 diff = currentPos - neighborPos;
                        diff.y = 0;
                        float sqrMag = math.lengthsq(diff);

                        // 아주 미세한 거리 방어
                        if (sqrMag > 0.001f && sqrMag < separationRadius * separationRadius)
                        {
                            float dist = math.sqrt(sqrMag);
                            separationVec += (diff / dist) * (1.0f - (dist / separationRadius));
                            neighborCount++;
                        }
                    }
                    if (neighborCount > 0)
                    {
                        // 평균 분리 벡터
                        separationVec /= neighborCount;
                        float pushBackFactor = math.dot(lookDir, separationVec);

                        if (pushBackFactor < -0.2f)
                        {
                            enemyData.ValueRW.BlockedTimer += deltaTime;
                            if (enemyData.ValueRO.BlockedTimer > 1.0f)
                            {
                                targetData.ValueRW.CurrentTarget = Entity.Null;
                                targetData.ValueRW.ScanTimer = 0f;
                                enemyData.ValueRW.BlockedTimer = 0f;
                                continue;
                            }

                            // 회전 처리를 위한 순수 방향
                            float3 rightDir = math.cross(math.up(), lookDir);   
                            float dirSign = (currentPos.x * currentPos.z) % 2f > 0 ? 1f : -1f;

                            // 배회 벡터
                            float3 orbitVec = rightDir * dirSign * 2.0f;        

                            // 회전 처리를 위한 순수 방향
                            moveDir = math.normalize(lookDir + separationVec * separationWeight + orbitVec);
                        }
                        else
                        {
                            enemyData.ValueRW.BlockedTimer = 0f;
                            moveDir = math.normalize(moveDir + separationVec * separationWeight);
                        }
                    }
                    else
                    {
                        moveDir = lookDir; 
                    }
                }
                hits.Dispose();

                velocity.ValueRW.Linear = new float3(
                    moveDir.x * enemyData.ValueRO.MoveSpeed,
                    velocity.ValueRO.Linear.y, // 기존 Y 축 중력을 존중 (이후 고정)
                    moveDir.z * enemyData.ValueRO.MoveSpeed
                );

                quaternion targetRot = quaternion.LookRotationSafe(lookDir, math.up());
                transform.ValueRW.Rotation = math.slerp(transform.ValueRO.Rotation, targetRot, deltaTime * 10f);
            }
            else
            {
                enemyData.ValueRW.CurrentState = EnemyState.Attack;
                velocity.ValueRW.Linear = new float3(0, velocity.ValueRO.Linear.y, 0);
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

        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        foreach (var (enemyData, targetData, transform) in
                 SystemAPI.Query<RefRW<CEnemyData>, RefRO<TargetingData>, RefRO<LocalTransform>>())
        {
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
                    float3 targetPos = targetTransform.Position;
                    Entity hitbox = ecb.Instantiate(enemyData.ValueRO.AttackPrefab);

                    if (_hitBoxLookup.TryGetComponent(enemyData.ValueRO.AttackPrefab, out var prefabHitBox))
                    {
                        prefabHitBox.Damage = enemyData.ValueRO.AttackPower;
                        prefabHitBox.TargetFaction = 1;
                        ecb.SetComponent(hitbox, prefabHitBox);
                    }

                    ecb.SetComponent(hitbox, new LocalTransform
                    {
                        Position = transform.ValueRO.Position,
                        Scale = 1f,
                        Rotation = quaternion.LookRotationSafe(targetPos - transform.ValueRO.Position, math.up())
                    });

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
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitHealthSystem))]
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






