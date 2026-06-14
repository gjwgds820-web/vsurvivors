using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Unity.Collections.LowLevel.Unsafe;

#region Movement System
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TransformSystemGroup))]
[UpdateAfter(typeof(UnitSpatialSystem))]
[BurstCompile]
public partial struct EnemyMovementSystem : ISystem
{
    private ComponentLookup<LocalTransform> _transformLookup;
    private ComponentLookup<IsolatedBossTag> _isolatedBossLookup;
    private ComponentLookup<DeathTag> _deathLookup;
    private EntityQuery _playerQuery;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        _isolatedBossLookup = state.GetComponentLookup<IsolatedBossTag>(true);
        _deathLookup = state.GetComponentLookup<DeathTag>(true);
        _playerQuery = SystemAPI.QueryBuilder().WithAll<PlayerData, LocalTransform>().Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<SpatialGridData>(out var gridData)) return;

        bool isIsolatedPhase = false;
        if (SystemAPI.TryGetSingleton<GameDirectorData>(out var director))
        {
            isIsolatedPhase = director.CurrentPhase == GamePhase.IsolatedBossFight;
        }

        _transformLookup.Update(ref state);
        _isolatedBossLookup.Update(ref state);
        _deathLookup.Update(ref state);

        Entity playerEntity = Entity.Null;
        if (!_playerQuery.IsEmpty)
        {
            playerEntity = _playerQuery.GetSingletonEntity();
        }

        var job = new EnemyMovementJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            IsIsolatedPhase = isIsolatedPhase,
            EnemyGrid = gridData.EnemyGrid,
            ShadowGrid = gridData.ShadowGrid,
            PlayerEntity = playerEntity,
            TransformLookup = _transformLookup,
            IsolatedBossLookup = _isolatedBossLookup,
            DeathLookup = _deathLookup
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct EnemyMovementJob : IJobEntity
{
    public float DeltaTime;
    public bool IsIsolatedPhase;
    [ReadOnly] public NativeParallelMultiHashMap<int2, Entity> EnemyGrid;
    [ReadOnly] public NativeParallelMultiHashMap<int2, Entity> ShadowGrid;
    public Entity PlayerEntity;
    [NativeDisableContainerSafetyRestriction]
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    [ReadOnly] public ComponentLookup<IsolatedBossTag> IsolatedBossLookup;
    [ReadOnly] public ComponentLookup<DeathTag> DeathLookup;

    private bool IsTargetInvalid(Entity target)
    {
        return target == Entity.Null || !TransformLookup.HasComponent(target) || DeathLookup.HasComponent(target);
    }

    private Entity FindBestShadowTarget(float3 currentPos, float searchRadiusSq, int2 cell)
    {
        Entity bestShadow = Entity.Null;
        float bestShadowDistSq = float.MaxValue;

        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                int2 nCell = cell + new int2(dx, dy);
                if (ShadowGrid.TryGetFirstValue(nCell, out Entity shEnt, out var shIter))
                {
                    do
                    {
                        if (IsTargetInvalid(shEnt)) continue;

                        float dSq = math.distancesq(currentPos, TransformLookup[shEnt].Position);
                        if (dSq < searchRadiusSq && dSq < bestShadowDistSq)
                        {
                            bestShadowDistSq = dSq;
                            bestShadow = shEnt;
                        }
                    } while (ShadowGrid.TryGetNextValue(out shEnt, ref shIter));
                }
            }
        }

        return bestShadow;
    }

    private void Execute(Entity entity, ref LocalTransform transform, ref CEnemyData enemyData, ref TargetingData targetData)
    {
        const float blockedDetectionDistance = 0.5f;
        const float blockedDetectionMoveThresholdSq = 0.0025f;
        const float blockedDetectionTime = 0.35f;
        const float blockedRecoverDelay = 0.2f;

        if (IsIsolatedPhase && !IsolatedBossLookup.HasComponent(entity)) return;

        float3 currentPos = transform.Position;
        currentPos.y = 0.5f;

        // 1. Universal Separation Logic (Avoid overlap regardless of state)
        float3 positionResolution = float3.zero;
        float3 separation = float3.zero;
        int separationCount = 0;

        float enemySeparationRadius = 1.8f;
        float enemySeparationRadiusSq = enemySeparationRadius * enemySeparationRadius;
        float enemyHardRadius = 1.0f;

        float shadowSeparationRadius = 1.4f;
        float shadowSeparationRadiusSq = shadowSeparationRadius * shadowSeparationRadius;
        float shadowHardRadius = 0.8f;
        float shadowSeparationWeight = 0.7f;

        float playerSeparationRadius = 1.6f;
        float playerSeparationRadiusSq = playerSeparationRadius * playerSeparationRadius;
        float playerHardRadius = 1.0f;
        float playerSeparationWeight = 0.45f;

        int2 cell = SpatialHashConfig.GetCell(currentPos);
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int2 neighborCell = cell + new int2(dx, dy);
                if (EnemyGrid.TryGetFirstValue(neighborCell, out Entity otherEntity, out var iterator))
                {
                    do
                    {
                        if (otherEntity == entity) continue;
                        if (!TransformLookup.HasComponent(otherEntity)) continue;

                        float3 otherPos = TransformLookup[otherEntity].Position;
                        float3 diff = currentPos - otherPos;
                        diff.y = 0;
                        float distSq = math.lengthsq(diff);

                        if (distSq > 0.001f && distSq < enemySeparationRadiusSq)
                        {
                            float dist = math.sqrt(distSq);
                            if (dist < enemyHardRadius)
                            {
                                float overlap = enemyHardRadius - dist;
                                positionResolution += (diff / dist) * (overlap * 0.5f);
                            }

                            float pushForce = (enemySeparationRadius - dist) / enemySeparationRadius;
                            separation += (diff / dist) * pushForce;
                            separationCount++;
                        }
                    } while (EnemyGrid.TryGetNextValue(out otherEntity, ref iterator));
                }

                if (ShadowGrid.TryGetFirstValue(neighborCell, out Entity otherShadow, out var shadowIterator))
                {
                    do
                    {
                        if (!TransformLookup.HasComponent(otherShadow)) continue;

                        float3 otherPos = TransformLookup[otherShadow].Position;
                        float3 diff = currentPos - otherPos;
                        diff.y = 0;
                        float distSq = math.lengthsq(diff);

                        if (distSq > 0.001f && distSq < shadowSeparationRadiusSq)
                        {
                            float dist = math.sqrt(distSq);
                            if (dist < shadowHardRadius)
                            {
                                float overlap = shadowHardRadius - dist;
                                positionResolution += (diff / dist) * (overlap * 0.5f * shadowSeparationWeight);
                            }

                            float pushForce = (shadowSeparationRadius - dist) / shadowSeparationRadius;
                            separation += (diff / dist) * (pushForce * shadowSeparationWeight);
                            separationCount++;
                        }
                    } while (ShadowGrid.TryGetNextValue(out otherShadow, ref shadowIterator));
                }
            }
        }

        if (PlayerEntity != Entity.Null && TransformLookup.HasComponent(PlayerEntity))
        {
            float3 playerPos = TransformLookup[PlayerEntity].Position;
            float3 playerDiff = currentPos - playerPos;
            playerDiff.y = 0f;
            float playerDistSq = math.lengthsq(playerDiff);

            if (playerDistSq > 0.001f && playerDistSq < playerSeparationRadiusSq)
            {
                float playerDist = math.sqrt(playerDistSq);
                if (playerDist < playerHardRadius)
                {
                    float overlap = playerHardRadius - playerDist;
                    positionResolution += (playerDiff / playerDist) * (overlap * 0.5f * playerSeparationWeight);
                }

                float pushForce = (playerSeparationRadius - playerDist) / playerSeparationRadius;
                separation += (playerDiff / playerDist) * (pushForce * playerSeparationWeight);
                separationCount++;
            }
        }

        float resLength = math.length(positionResolution);
        if (resLength > 0.1f) positionResolution = (positionResolution / resLength) * 0.1f;
        currentPos += positionResolution;
        transform.Position = currentPos;

        if (enemyData.IsAttacking) 
        {
            transform.Rotation = math.normalize(new quaternion(0, transform.Rotation.value.y, 0, transform.Rotation.value.w));
            return;
        }

        // 2. State Machine (FSM)
        switch (enemyData.CurrentState)
        {
            case EnemyState.Blocked:
            {
                enemyData.BlockedTimer -= DeltaTime;
                if (enemyData.BlockedTimer > 0f)
                    break;

                enemyData.CurrentState = EnemyState.Scan;
                break;
            }

            case EnemyState.Scan: // Re-Scan or Idle checking
            {
                enemyData.BlockedTimer = 0f;

                if (IsTargetInvalid(targetData.CurrentTarget))
                    targetData.CurrentTarget = Entity.Null;

                float scanRadius = enemyData.AttackRange * 3.0f;
                if (scanRadius < 5.0f) scanRadius = 5.0f;
                float scanRadiusSq = scanRadius * scanRadius;

                Entity bestShadow = FindBestShadowTarget(currentPos, scanRadiusSq, cell);
                bool hasValidPlayer = !IsTargetInvalid(PlayerEntity);

                if (bestShadow != Entity.Null)
                    targetData.CurrentTarget = bestShadow;
                else if (hasValidPlayer)
                    targetData.CurrentTarget = PlayerEntity;
                else
                    targetData.CurrentTarget = Entity.Null;

                if (targetData.CurrentTarget != Entity.Null)
                    enemyData.CurrentState = EnemyState.Chase;

                break;
            }

            case EnemyState.Chase:
            {
                enemyData.BlockedTimer = 0f;

                float searchRadius = enemyData.AttackRange * 3.0f;
                if (searchRadius < 5.0f) searchRadius = 5.0f;
                float searchRadiusSq = searchRadius * searchRadius;
                bool hasValidPlayer = !IsTargetInvalid(PlayerEntity);

                if (IsTargetInvalid(targetData.CurrentTarget))
                {
                    targetData.CurrentTarget = hasValidPlayer ? PlayerEntity : Entity.Null;
                    if (targetData.CurrentTarget == Entity.Null)
                    {
                        enemyData.CurrentState = EnemyState.Scan;
                        break;
                    }
                }

                if (targetData.CurrentTarget != PlayerEntity)
                {
                    float currentTargetDistSq = math.distancesq(currentPos, TransformLookup[targetData.CurrentTarget].Position);
                    if (currentTargetDistSq > searchRadiusSq * 1.5f || DeathLookup.HasComponent(targetData.CurrentTarget))
                    {
                        targetData.CurrentTarget = hasValidPlayer ? PlayerEntity : Entity.Null;
                    }
                }

                Entity nearbyShadow = FindBestShadowTarget(currentPos, searchRadiusSq, cell);
                if (nearbyShadow != Entity.Null)
                {
                    targetData.CurrentTarget = nearbyShadow;
                }
                else if (targetData.CurrentTarget == Entity.Null && hasValidPlayer)
                {
                    targetData.CurrentTarget = PlayerEntity;
                }

                if (targetData.CurrentTarget == Entity.Null)
                {
                    enemyData.CurrentState = EnemyState.Scan;
                    break;
                }

                float3 targetPos = TransformLookup[targetData.CurrentTarget].Position;
                float3 toTarget = targetPos - currentPos;
                toTarget.y = 0;
                float distance = math.length(toTarget);

                // Adding a slightly larger check so they don't get stuck just outside strict hitboxes
                if (distance <= enemyData.AttackRange + 0.3f)
                {
                    enemyData.CurrentState = EnemyState.Attack;
                    break; // Stop movement, switch state immediately
                }

                float3 moveDir = distance > 0.001f ? toTarget / distance : math.forward();
                
                if (separationCount > 0)
                {
                    separation /= separationCount;
                    float separationAlongMove = math.dot(separation, moveDir);
                    if (separationAlongMove < 0f)
                    {
                        separation -= moveDir * separationAlongMove;
                    }

                    float3 combinedDir = moveDir + separation * 1.1f;
                    if (math.dot(combinedDir, moveDir) < 0.25f)
                    {
                        combinedDir = moveDir;
                    }

                    if (math.lengthsq(combinedDir) > 0.001f) moveDir = math.normalize(combinedDir);
                }

                if (math.lengthsq(moveDir) > 0.001f) 
                {
                    transform.Position += moveDir * enemyData.MoveSpeed * DeltaTime;
                    transform.Rotation = math.slerp(transform.Rotation, quaternion.LookRotationSafe(moveDir, math.up()), DeltaTime * 10f);
                }

                float moveDistSq = math.distancesq(transform.Position, enemyData.PreviousPosition);
                bool shouldDetectBlocked = distance > enemyData.AttackRange + blockedDetectionDistance;
                if (shouldDetectBlocked)
                {
                    if (moveDistSq < blockedDetectionMoveThresholdSq)
                    {
                        enemyData.BlockedTimer += DeltaTime;
                        if (enemyData.BlockedTimer >= blockedDetectionTime)
                        {
                            enemyData.CurrentState = EnemyState.Blocked;
                            enemyData.BlockedTimer = blockedRecoverDelay;
                            targetData.CurrentTarget = Entity.Null;
                        }
                    }
                    else
                    {
                        enemyData.BlockedTimer = 0f;
                    }
                }
                else
                {
                    enemyData.BlockedTimer = 0f;
                }

                break;
            }

            case EnemyState.Attack:
            {
                enemyData.BlockedTimer = 0f;

                if (targetData.CurrentTarget == Entity.Null || !TransformLookup.HasComponent(targetData.CurrentTarget))
                {
                    enemyData.CurrentState = EnemyState.Scan;
                    break;
                }

                float3 targetPos = TransformLookup[targetData.CurrentTarget].Position;
                float3 toTarget = targetPos - currentPos;
                toTarget.y = 0;
                float distance = math.length(toTarget);

                if (distance > enemyData.AttackRange + 0.6f) // Hysteresis buffer
                {
                    enemyData.CurrentState = EnemyState.Chase;
                }
                else
                {
                    if (distance > 0.001f)
                    {
                        float3 lookDir = toTarget / distance;
                        transform.Rotation = math.slerp(transform.Rotation, quaternion.LookRotationSafe(lookDir, math.up()), DeltaTime * 15f);
                    }
                }
                break;
            }
        }

        enemyData.PreviousPosition = transform.Position;
        transform.Rotation = math.normalize(new quaternion(0, transform.Rotation.value.y, 0, transform.Rotation.value.w));
    }
}
#endregion

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(EnemyMovementSystem))]
public partial class EnemyMovementDebugSystem : SystemBase
{
    private struct EnemyDebugSnapshot
    {
        public float3 Position;
        public float PlayerDistance;
    }

    private float _logTimer;
    private EntityQuery _playerQuery;
    private readonly System.Collections.Generic.Dictionary<int, EnemyDebugSnapshot> _previousSnapshots = new();

    protected override void OnCreate()
    {
        _logTimer = 0f;
        _playerQuery = SystemAPI.QueryBuilder().WithAll<PlayerData, LocalTransform>().Build();
        Enabled = false;
    }

    protected override void OnUpdate()
    {
        _logTimer -= SystemAPI.Time.DeltaTime;
        if (_logTimer > 0f) return;
        _logTimer = 0.5f;

        if (_playerQuery.IsEmpty) return;
        if (!SystemAPI.TryGetSingleton<SpatialGridData>(out var gridData)) return;

        Entity playerEntity = _playerQuery.GetSingletonEntity();
        LocalTransform playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        float3 playerPos = playerTransform.Position;

        int totalEnemies = 0;
        int scanCount = 0;
        int chaseCount = 0;
        int attackCount = 0;
        int blockedCount = 0;
        int noTargetCount = 0;
        int playerTargetCount = 0;
        int shadowTargetCount = 0;
        int suspiciousCount = 0;
        int stalledCount = 0;
        int movingAwayCount = 0;
        int physicsBodyCount = 0;
        int blockedRecoveringCount = 0;
        int targetInvalidCount = 0;
        int separationStallCount = 0;
        int attackTargetLostCount = 0;

        int loggedSuspicious = 0;
        const int maxSuspiciousLogs = 3;
        int loggedSamples = 0;
        const int maxSampleLogs = 2;

        foreach (var (enemyData, targetData, transform, entity) in SystemAPI.Query<RefRO<CEnemyData>, RefRO<TargetingData>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            totalEnemies++;

            switch (enemyData.ValueRO.CurrentState)
            {
                case EnemyState.Scan:
                    scanCount++;
                    break;
                case EnemyState.Chase:
                    chaseCount++;
                    break;
                case EnemyState.Attack:
                    attackCount++;
                    break;
                case EnemyState.Blocked:
                    blockedCount++;
                    break;
            }

            Entity currentTarget = targetData.ValueRO.CurrentTarget;
            bool targetEntityExists = currentTarget != Entity.Null && EntityManager.Exists(currentTarget);
            bool targetHasTransform = targetEntityExists && SystemAPI.HasComponent<LocalTransform>(currentTarget);
            bool targetIsDead = targetEntityExists && SystemAPI.HasComponent<DeathTag>(currentTarget);
            bool hasTarget = currentTarget != Entity.Null && targetEntityExists && targetHasTransform && !targetIsDead;
            bool hasDynamicPhysicsBody = SystemAPI.HasComponent<PhysicsVelocity>(entity) || SystemAPI.HasComponent<PhysicsMass>(entity) || SystemAPI.HasComponent<PhysicsDamping>(entity) || SystemAPI.HasComponent<PhysicsGravityFactor>(entity);
            bool hasPhysicsCollider = SystemAPI.HasComponent<PhysicsCollider>(entity);
            if (hasDynamicPhysicsBody)
            {
                physicsBodyCount++;
            }

            if (!hasTarget)
            {
                noTargetCount++;
            }
            else if (currentTarget == playerEntity)
            {
                playerTargetCount++;
            }
            else if (SystemAPI.HasComponent<CShadowData>(currentTarget))
            {
                shadowTargetCount++;
            }

            float3 currentPos = transform.ValueRO.Position;
            float playerDistance = math.distance(new float3(currentPos.x, 0f, currentPos.z), new float3(playerPos.x, 0f, playerPos.z));
            float3 targetPos = hasTarget ? SystemAPI.GetComponent<LocalTransform>(currentTarget).Position : playerPos;
            float3 toTarget = targetPos - currentPos;
            toTarget.y = 0f;
            float distanceToTarget = math.length(toTarget);
            float3 moveDir = distanceToTarget > 0.001f ? toTarget / distanceToTarget : float3.zero;

            float actualMoveDistance = 0f;
            float playerDistanceDelta = 0f;
            bool hasPreviousSnapshot = _previousSnapshots.TryGetValue(entity.Index, out var previousSnapshot);
            if (hasPreviousSnapshot)
            {
                float3 actualDelta = currentPos - previousSnapshot.Position;
                actualDelta.y = 0f;
                actualMoveDistance = math.length(actualDelta);
                playerDistanceDelta = playerDistance - previousSnapshot.PlayerDistance;
            }

            _previousSnapshots[entity.Index] = new EnemyDebugSnapshot
            {
                Position = currentPos,
                PlayerDistance = playerDistance
            };

            float searchRadius = enemyData.ValueRO.AttackRange * 3.0f;
            if (searchRadius < 5.0f) searchRadius = 5.0f;
            float searchRadiusSq = searchRadius * searchRadius;

            Entity bestShadow = Entity.Null;
            float bestShadowDistSq = float.MaxValue;
            float3 separation = float3.zero;
            int separationCount = 0;
            int nearbyEnemyCount = 0;
            float separationRadius = 1.0f;
            float separationRadiusSq = separationRadius * separationRadius;

            int2 cell = SpatialHashConfig.GetCell(currentPos);
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    int2 neighborCell = cell + new int2(dx, dy);

                    if (gridData.ShadowGrid.TryGetFirstValue(neighborCell, out Entity shadowEntity, out var shadowIterator))
                    {
                        do
                        {
                            if (!EntityManager.Exists(shadowEntity) || !SystemAPI.HasComponent<LocalTransform>(shadowEntity) || SystemAPI.HasComponent<DeathTag>(shadowEntity))
                                continue;

                            float shadowDistSq = math.distancesq(currentPos, SystemAPI.GetComponent<LocalTransform>(shadowEntity).Position);
                            if (shadowDistSq < searchRadiusSq && shadowDistSq < bestShadowDistSq)
                            {
                                bestShadowDistSq = shadowDistSq;
                                bestShadow = shadowEntity;
                            }
                        } while (gridData.ShadowGrid.TryGetNextValue(out shadowEntity, ref shadowIterator));
                    }

                    if (dx < -1 || dx > 1 || dy < -1 || dy > 1)
                        continue;

                    if (gridData.EnemyGrid.TryGetFirstValue(neighborCell, out Entity otherEnemy, out var enemyIterator))
                    {
                        do
                        {
                            if (otherEnemy == entity || !EntityManager.Exists(otherEnemy) || !SystemAPI.HasComponent<LocalTransform>(otherEnemy))
                                continue;

                            float3 otherPos = SystemAPI.GetComponent<LocalTransform>(otherEnemy).Position;
                            float3 diff = currentPos - otherPos;
                            diff.y = 0f;
                            float distSq = math.lengthsq(diff);
                            if (distSq > 0.001f && distSq < separationRadiusSq)
                            {
                                nearbyEnemyCount++;
                                float dist = math.sqrt(distSq);
                                float pushForce = (separationRadius - dist) / separationRadius;
                                separation += (diff / dist) * pushForce;
                                separationCount++;
                            }
                        } while (gridData.EnemyGrid.TryGetNextValue(out otherEnemy, ref enemyIterator));
                    }
                }
            }

            float3 combinedDir = moveDir;
            if (separationCount > 0 && math.lengthsq(moveDir) > 0.001f)
            {
                separation /= separationCount;
                float separationAlongMove = math.dot(separation, moveDir);
                if (separationAlongMove < 0f)
                {
                    separation -= moveDir * separationAlongMove;
                }

                combinedDir = moveDir + separation * 1.1f;
                if (math.dot(combinedDir, moveDir) < 0.25f)
                {
                    combinedDir = moveDir;
                }

                if (math.lengthsq(combinedDir) > 0.001f)
                {
                    combinedDir = math.normalize(combinedDir);
                }
            }

            float directionDot = math.lengthsq(moveDir) > 0.001f && math.lengthsq(combinedDir) > 0.001f
                ? math.dot(math.normalize(moveDir), math.normalize(combinedDir))
                : 1f;

            bool shouldBeChasingPlayer = bestShadow == Entity.Null;
            bool suspicious = !hasTarget
                || (shouldBeChasingPlayer && currentTarget != playerEntity)
                || (enemyData.ValueRO.CurrentState == EnemyState.Scan && hasTarget)
                || (enemyData.ValueRO.CurrentState == EnemyState.Chase && distanceToTarget > enemyData.ValueRO.AttackRange + 0.5f && directionDot < 0.55f)
                || (enemyData.ValueRO.CurrentState == EnemyState.Chase && distanceToTarget > searchRadius && currentTarget != playerEntity && bestShadow == Entity.Null)
                || (hasPreviousSnapshot && enemyData.ValueRO.CurrentState == EnemyState.Chase && distanceToTarget > enemyData.ValueRO.AttackRange + 0.5f && actualMoveDistance < 0.05f)
                || (hasPreviousSnapshot && enemyData.ValueRO.CurrentState == EnemyState.Chase && currentTarget == playerEntity && distanceToTarget > enemyData.ValueRO.AttackRange + 0.5f && playerDistanceDelta > 0.05f);

            string stallReason = "None";
            if (enemyData.ValueRO.CurrentState == EnemyState.Blocked)
            {
                blockedRecoveringCount++;
                stallReason = $"BlockedRecover(timer={enemyData.ValueRO.BlockedTimer:F2})";
            }
            else if (!hasTarget)
            {
                targetInvalidCount++;
                if (currentTarget == Entity.Null)
                    stallReason = "NoTarget(Null)";
                else if (!targetEntityExists)
                    stallReason = "NoTarget(EntityMissing)";
                else if (!targetHasTransform)
                    stallReason = "NoTarget(NoTransform)";
                else if (targetIsDead)
                    stallReason = "NoTarget(DeadTarget)";
                else
                    stallReason = "NoTarget(Unknown)";
            }
            else if (enemyData.ValueRO.CurrentState == EnemyState.Chase)
            {
                bool isFarFromAttack = distanceToTarget > enemyData.ValueRO.AttackRange + 0.5f;
                bool isLowMotion = hasPreviousSnapshot && actualMoveDistance < 0.05f;
                if (isFarFromAttack && isLowMotion)
                {
                    if (hasDynamicPhysicsBody)
                    {
                        stallReason = "ChaseStall(PhysicsBody)";
                    }
                    else if (separationCount >= 3 && math.lengthsq(separation) > 0.04f)
                    {
                        separationStallCount++;
                        stallReason = "ChaseStall(SeparationDense)";
                    }
                    else if (directionDot < 0.35f)
                    {
                        stallReason = "ChaseStall(DirectionCollapsed)";
                    }
                    else if (enemyData.ValueRO.BlockedTimer > 0f)
                    {
                        stallReason = $"ChaseStall(BlockedDetecting={enemyData.ValueRO.BlockedTimer:F2})";
                    }
                    else
                    {
                        stallReason = "ChaseStall(LowMotion)";
                    }
                }
            }
            else if (enemyData.ValueRO.CurrentState == EnemyState.Attack && enemyData.ValueRO.IsAttacking && !hasTarget)
            {
                attackTargetLostCount++;
                stallReason = "AttackStall(TargetLostWhileAttacking)";
            }

            if (hasPreviousSnapshot && enemyData.ValueRO.CurrentState == EnemyState.Chase && distanceToTarget > enemyData.ValueRO.AttackRange + 0.5f)
            {
                if (actualMoveDistance < 0.05f)
                {
                    stalledCount++;
                }

                if (currentTarget == playerEntity && playerDistanceDelta > 0.05f)
                {
                    movingAwayCount++;
                }
            }

            if (suspicious)
            {
                suspiciousCount++;
            }

            bool shouldLogDetails = false;
            if (suspicious && loggedSuspicious < maxSuspiciousLogs)
            {
                loggedSuspicious++;
                shouldLogDetails = true;
            }
            else if (!suspicious && loggedSuspicious == 0 && loggedSamples < maxSampleLogs)
            {
                loggedSamples++;
                shouldLogDetails = true;
            }

            if (!shouldLogDetails)
                continue;

            string targetKind = !hasTarget ? "None" : currentTarget == playerEntity ? "Player" : SystemAPI.HasComponent<CShadowData>(currentTarget) ? "Shadow" : "Other";
            string bestShadowInfo = bestShadow == Entity.Null ? "None" : $"{bestShadow.Index} ({math.sqrt(bestShadowDistSq):F2}m)";

            UnityEngine.Debug.Log(
                $"<color=red>[Enemy Move Debug]</color> Enemy={entity.Index} State={enemyData.ValueRO.CurrentState} Pos={currentPos} PlayerDist={playerDistance:F2} PlayerDelta={playerDistanceDelta:F2} ActualMove={actualMoveDistance:F2} " +
                $"Target={targetKind}:{currentTarget.Index} TargetDist={distanceToTarget:F2} BestShadow={bestShadowInfo} SearchRadius={searchRadius:F2} DynamicBody={hasDynamicPhysicsBody} Collider={hasPhysicsCollider} Suspicious={suspicious} BlockedTimer={enemyData.ValueRO.BlockedTimer:F2} IsAttacking={enemyData.ValueRO.IsAttacking}");

            UnityEngine.Debug.Log(
                $"<color=yellow>[Enemy Move Vector]</color> Enemy={entity.Index} ToTargetDir={moveDir} Separation={separation} SeparationCount={separationCount} NearbyEnemies={nearbyEnemyCount} CombinedDir={combinedDir} Dot={directionDot:F2} " +
                $"ShouldChasePlayer={shouldBeChasingPlayer} HasTarget={hasTarget}\n---------------------------------");

            UnityEngine.Debug.Log(
                $"<color=magenta>[Enemy Move StallDetail]</color> Enemy={entity.Index} Reason={stallReason} TargetExists={targetEntityExists} TargetHasTransform={targetHasTransform} TargetDead={targetIsDead} " +
                $"CurrentTarget={currentTarget.Index} Dist={distanceToTarget:F2} ActualMove={actualMoveDistance:F3} BlockedTimer={enemyData.ValueRO.BlockedTimer:F2} PrevPos={enemyData.ValueRO.PreviousPosition}");
        }

        UnityEngine.Debug.Log(
            $"<color=cyan>[Enemy Move Summary]</color> Total={totalEnemies} Scan={scanCount} Chase={chaseCount} Attack={attackCount} Blocked={blockedCount} NoTarget={noTargetCount} PlayerTarget={playerTargetCount} ShadowTarget={shadowTargetCount} Suspicious={suspiciousCount} Stalled={stalledCount} MovingAway={movingAwayCount} PhysicsBody={physicsBodyCount} BlockedRecovering={blockedRecoveringCount} TargetInvalid={targetInvalidCount} SeparationStall={separationStallCount} AttackTargetLost={attackTargetLostCount}");
    }
}

#region Combat System
[BurstCompile]
public partial struct EnemyCombatSystem : ISystem
{
    private ComponentLookup<LocalTransform> _transformLookup;
    private ComponentLookup<HitBoxData> _hitBoxLookup;
    private ComponentLookup<VisualAnimationState> _animStateLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        _hitBoxLookup = state.GetComponentLookup<HitBoxData>(true);
        _animStateLookup = state.GetComponentLookup<VisualAnimationState>(false);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _transformLookup.Update(ref state);
        _hitBoxLookup.Update(ref state);
        _animStateLookup.Update(ref state);
        float deltaTime = SystemAPI.Time.DeltaTime;

        bool isIsolatedPhase = false;
        if (SystemAPI.TryGetSingleton<GameDirectorData>(out var director))
        {
            isIsolatedPhase = director.CurrentPhase == GamePhase.IsolatedBossFight;
        }

        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        foreach (var (enemyData, targetData, transform, entity) in
             SystemAPI.Query<RefRW<CEnemyData>, RefRO<TargetingData>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            if (isIsolatedPhase) continue;
            if (enemyData.ValueRO.IsBoss) continue; // 보스는 전용 Combat 로직(BossSystems) 사용

            enemyData.ValueRW.CurrentCooldown -= deltaTime;
            Entity currentTarget = targetData.ValueRO.CurrentTarget;
            bool hasValidTarget = currentTarget != Entity.Null && SystemAPI.Exists(currentTarget) && _transformLookup.HasComponent(currentTarget);

            if (enemyData.ValueRO.IsAttacking)
            {
                enemyData.ValueRW.AttackDelayTimer += deltaTime;
            }

            if (enemyData.ValueRO.CurrentState == EnemyState.Attack && enemyData.ValueRO.IsAttacking && !hasValidTarget)
            {
                enemyData.ValueRW.IsAttacking = false;
                enemyData.ValueRW.AttackDelayTimer = 0f;
                enemyData.ValueRW.CurrentState = EnemyState.Scan;

                if (_animStateLookup.HasComponent(entity))
                {
                    var animState = _animStateLookup[entity];
                    animState.TriggerAttack = false;
                    animState.EventAttackHit = false;
                    animState.EventAttackEnd = false;
                    _animStateLookup[entity] = animState;
                }

                continue;
            }

            if (enemyData.ValueRO.CurrentState != EnemyState.Attack) continue;
            if (!hasValidTarget) continue;

            if (!enemyData.ValueRO.IsAttacking && enemyData.ValueRO.CurrentCooldown <= 0)
            {
                if (currentTarget == Entity.Null || currentTarget.Index < 0) continue;
                if (_transformLookup.TryGetComponent(currentTarget, out var targetTransform))
                {
                    enemyData.ValueRW.PendingTargetPosition = targetTransform.Position;
                    enemyData.ValueRW.IsAttacking = true;
                    enemyData.ValueRW.AttackDelayTimer = 0f;

                    if (_animStateLookup.HasComponent(entity))
                    {
                        var animState = _animStateLookup[entity];
                        animState.TriggerAttack = true;
                        animState.EventAttackHit = false;
                        animState.EventAttackEnd = false;
                        _animStateLookup[entity] = animState;
                    }
                }
            }

            if (!enemyData.ValueRO.IsAttacking) continue;

            bool hasAttackEvent = false;
            if (_animStateLookup.HasComponent(entity))
            {
                var animState = _animStateLookup[entity];
                if (animState.EventAttackHit)
                {
                    animState.EventAttackHit = false;
                    _animStateLookup[entity] = animState;
                    hasAttackEvent = true;
                }
            }

            if (!hasAttackEvent && enemyData.ValueRO.AttackDelayTimer < 0.35f) continue;

            if (enemyData.ValueRO.AttackPrefab == Entity.Null)
            {
                UnityEngine.Debug.LogError("Attack Prefab is not assigned in EnemyData!");
                enemyData.ValueRW.IsAttacking = false;
                enemyData.ValueRW.AttackDelayTimer = 0f;
                continue;
            }

            float3 myPos = transform.ValueRO.Position;
            float3 targetPos = enemyData.ValueRO.PendingTargetPosition;

            float3 dir2D = targetPos - myPos;
            dir2D.y = 0;
            if (math.lengthsq(dir2D) > 0.001f) dir2D = math.normalize(dir2D);
            else dir2D = math.forward();

            Entity hitbox = ecb.Instantiate(enemyData.ValueRO.AttackPrefab);

            if (_hitBoxLookup.TryGetComponent(enemyData.ValueRO.AttackPrefab, out var prefabHitBox))
            {
                prefabHitBox.Damage = enemyData.ValueRO.AttackPower;
                prefabHitBox.TargetFaction = 1;

                if (prefabHitBox.Shape == HitBoxShape.Circle || prefabHitBox.Shape == HitBoxShape.Cone)
                {
                    prefabHitBox.Radius = math.max(prefabHitBox.Radius, enemyData.ValueRO.AttackRange + 0.2f);
                }

                ecb.SetComponent(hitbox, prefabHitBox);
            }

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

            enemyData.ValueRW.IsAttacking = false;
            enemyData.ValueRW.AttackDelayTimer = 0f;
            enemyData.ValueRW.CurrentCooldown = enemyData.ValueRO.AttackCooldown;
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























