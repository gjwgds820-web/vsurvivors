using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.Physics.GraphicsIntegration;
using Unity.Collections;
using System.ComponentModel;
using VSurvivors.Battle.Physics;

#region Movement System
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[BurstCompile]
public partial struct PlayerMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (transform, input, movement) in 
                 SystemAPI.Query<RefRW<LocalTransform>, RefRO<PlayerInput>, RefRO<PlayerMovementData>>())
        {
            float2 inputMove = input.ValueRO.Move;
            float3 moveDirection = new float3(inputMove.x, 0f, inputMove.y);

            if (math.lengthsq(moveDirection) > 0.01f)
            {
                moveDirection = math.normalize(moveDirection);

                float3 currentPos = transform.ValueRO.Position;
                currentPos += moveDirection * movement.ValueRO.MoveSpeed * deltaTime;
                currentPos.y = 0.5f; // 바닥 유지
                transform.ValueRW.Position = currentPos;
                
                float3 flatForward = new float3(moveDirection.x, 0f, moveDirection.z);
                
                if (math.lengthsq(flatForward) > 0.001f)
                {
                    flatForward = math.normalize(flatForward);
                    quaternion targetRotation = quaternion.LookRotationSafe(flatForward, math.up());
                    
                    quaternion newRotation = math.slerp(transform.ValueRO.Rotation, targetRotation, movement.ValueRO.RotationSpeed * deltaTime);
                    
                    newRotation.value.x = 0f;
                    newRotation.value.z = 0f;
                    newRotation = math.normalize(newRotation);
                    
                    transform.ValueRW.Rotation = newRotation;
                }
            }
            else
            {
                quaternion currentRot = transform.ValueRO.Rotation;
                currentRot.value.x = 0f;
                currentRot.value.z = 0f;
                transform.ValueRW.Rotation = math.normalize(currentRot);
            }
        }
    }
}
#endregion

#region BossArenaBoundaryClampSystem
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[BurstCompile]
public partial struct BossArenaBoundaryClampSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<GameDirectorData>(out var director)) return;
        if (!director.BossArenaActive || director.BossArenaRadius <= 0.1f) return;

        bool isBossPhase = director.CurrentPhase == GamePhase.BossFight || director.CurrentPhase == GamePhase.IsolatedBossFight;
        if (!isBossPhase) return;

        float2 center2D = new float2(director.BossArenaCenter.x, director.BossArenaCenter.z);
        float clampRadius = math.max(1f, director.BossArenaRadius - 0.6f);

        foreach (var transform in SystemAPI.Query<RefRW<LocalTransform>>().WithAll<PlayerData>().WithNone<DeathTag>())
        {
            float3 pos = transform.ValueRO.Position;
            float2 pos2D = new float2(pos.x, pos.z);
            float2 delta = pos2D - center2D;
            float distSq = math.lengthsq(delta);
            float radiusSq = clampRadius * clampRadius;

            if (distSq > radiusSq)
            {
                float2 dir = math.normalizesafe(delta, new float2(1f, 0f));
                float2 clamped2D = center2D + dir * clampRadius;
                transform.ValueRW.Position = new float3(clamped2D.x, pos.y, clamped2D.y);
            }
        }

        foreach (var transform in SystemAPI.Query<RefRW<LocalTransform>>().WithAll<CEnemyData>().WithNone<DeathTag>())
        {
            float3 pos = transform.ValueRO.Position;
            float2 pos2D = new float2(pos.x, pos.z);
            float2 delta = pos2D - center2D;
            float distSq = math.lengthsq(delta);
            float radiusSq = clampRadius * clampRadius;

            if (distSq > radiusSq)
            {
                float2 dir = math.normalizesafe(delta, new float2(1f, 0f));
                float2 clamped2D = center2D + dir * clampRadius;
                transform.ValueRW.Position = new float3(clamped2D.x, pos.y, clamped2D.y);
            }
        }

        foreach (var transform in SystemAPI.Query<RefRW<LocalTransform>>().WithAll<CShadowData>().WithNone<DeathTag>())
        {
            float3 pos = transform.ValueRO.Position;
            float2 pos2D = new float2(pos.x, pos.z);
            float2 delta = pos2D - center2D;
            float distSq = math.lengthsq(delta);
            float radiusSq = clampRadius * clampRadius;

            if (distSq > radiusSq)
            {
                float2 dir = math.normalizesafe(delta, new float2(1f, 0f));
                float2 clamped2D = center2D + dir * clampRadius;
                transform.ValueRW.Position = new float3(clamped2D.x, pos.y, clamped2D.y);
            }
        }
    }
}
#endregion

#region Death System
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(UnitHealthSystem))]
[UpdateBefore(typeof(VisualCleanupSystem))]
[BurstCompile]
public partial struct PlayerDeathSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (playerData, entity) in
                 SystemAPI.Query<RefRO<PlayerData>>()
                 .WithAll<DeathTag>()
                 .WithNone<DeathProcessedTag>()
                 .WithEntityAccess())
        {
            // 사망 처리 (예: 게임 오버 이벤트 생성)
            var gameOverEvent = ecb.CreateEntity();
            ecb.AddComponent<PlayerDeathEventTag>(gameOverEvent);
            ecb.AddComponent<DeathProcessedTag>(entity);

            // 플레이어 엔티티는 삭제하지 않고, 필요한 경우 리스폰 시스템에서 재활용할 수 있도록 함
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion

#region ShadowSpawnSystem
[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct ShadowSpawnerSystem : ISystem
{
    private static void ApplyShadowUpgradeBonuses(in PlayerData playerData, in ShadowDefBlob shadowDef, ref ShadowCombatData combatData)
    {
        float attackPowerMultiplier = math.max(0f, 1f + playerData.ShadowAttackPowerPercentBonus * 0.01f);
        float attackSpeedMultiplier = math.max(0.1f, 1f + playerData.ShadowAttackSpeedPercentBonus * 0.01f);

        combatData.AttackPower = shadowDef.AttackPower * attackPowerMultiplier;
        combatData.AttackRange = shadowDef.AttackRange;
        combatData.AttackCooldown = math.max(0.05f, shadowDef.AttackCooldown / attackSpeedMultiplier);
        combatData.CurrentCooldown = math.min(combatData.CurrentCooldown, combatData.AttackCooldown);
        combatData.CriticalChancePercent = math.clamp(playerData.ShadowCriticalChancePercent, 0f, 95f);
        combatData.CriticalDamagePercent = math.max(0f, playerData.ShadowCriticalDamagePercent);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<ShadowDatabaseComponent>(out var shadowDB)) return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var random = new Unity.Mathematics.Random((uint)(SystemAPI.Time.ElapsedTime * 1000) + 1);

        foreach (var (playerData, spawnerData, playerTransform, entity) in
                 SystemAPI.Query<RefRW<PlayerData>, RefRO<ShadowSpawnData>,  RefRO<LocalTransform>>().WithEntityAccess())
        {
            var shadowSlots = SystemAPI.GetBuffer<ShadowSlotElement>(entity);
            var activeSkills = SystemAPI.GetBuffer<ActiveShadowSkillElement>(entity);
            int maxShadowSlots = math.min((int)playerData.ValueRO.MaxShadow, 20);
            float3 basisForwardForSlots = spawnerData.ValueRO.UsePlayerRotationBasis
                ? math.normalizesafe(new float3(math.forward(playerTransform.ValueRO.Rotation).x, 0f, math.forward(playerTransform.ValueRO.Rotation).z), new float3(0f, 0f, 1f))
                : new float3(0f, 0f, 1f);
            float3 basisRightForSlots = math.cross(math.up(), basisForwardForSlots);

            // 현재 살아있는 그림자 수 확인 및 죽은 그림자 동기화 (앞쪽 빈 슬롯 유지)
            int aliveCount = 0;
            for (int i = shadowSlots.Length - 1; i >= 0; i--)
            {
                var slot = shadowSlots[i];
                if (slot.ShadowEntity != Entity.Null && slot.ShadowEntity.Index >= 0)
                {
                    // 상태 체크
                    if (SystemAPI.HasComponent<ShadowCombatData>(slot.ShadowEntity))
                    {
                        slot.IsAlive = SystemAPI.GetComponent<ShadowCombatData>(slot.ShadowEntity).IsAlive;
                    }
                    else if (!SystemAPI.Exists(slot.ShadowEntity))
                    {
                        slot.IsAlive = false;
                        slot.ShadowEntity = Entity.Null;
                    }
                }
                
                if (slot.IsAlive) 
                {
                    aliveCount++;
                    shadowSlots[i] = slot;
                }
                else
                {
                    // 죽은 슬롯은 비워두고, 다음 스폰이 가장 앞 빈 슬롯을 우선 점유합니다.
                    slot.ShadowEntity = Entity.Null;
                    slot.IsAlive = false;
                    shadowSlots[i] = slot;
                }
            }

            // 최대치를 넘어가는 오래된 잉여 슬롯이 있다면 강제 제거
            while (shadowSlots.Length > maxShadowSlots)
            {
                if (SystemAPI.Exists(shadowSlots[shadowSlots.Length - 1].ShadowEntity))
                {
                    ecb.AddComponent<DestroyEntityTag>(shadowSlots[shadowSlots.Length - 1].ShadowEntity);
                }
                shadowSlots.RemoveAt(shadowSlots.Length - 1);
            }

            // 인덱스 동기화 및 기존 그림자 스탯/외형 업데이트
            for (int i = 0; i < shadowSlots.Length; i++)
            {
                var slot = shadowSlots[i];
                if (slot.ShadowEntity != Entity.Null && slot.ShadowEntity.Index >= 0)
                {
                    if (SystemAPI.HasComponent<CShadowData>(slot.ShadowEntity))
                    {
                        var cData = SystemAPI.GetComponent<CShadowData>(slot.ShadowEntity);
                        if (cData.Index != i || !cData.Initialized)
                        {
                            cData.Index = i;
                            const float innerRadius = 3f;
                            const float outerRadius = 6f;
                            float3 slotOffset = float3.zero;
                            if (i >= 0 && i < 20)
                            {
                                if (i < 8)
                                {
                                    float angleStep = (math.PI * 2f) / 8f;
                                    float angle = i * angleStep;
                                    float3 dir = math.normalizesafe(basisForwardForSlots * math.cos(angle) + basisRightForSlots * math.sin(angle), basisForwardForSlots);
                                    slotOffset = dir * innerRadius;
                                }
                                else
                                {
                                    int outerIndex = i - 8;
                                    float angleStep = (math.PI * 2f) / 12f;
                                    float angle = outerIndex * angleStep;
                                    float3 dir = math.normalizesafe(basisForwardForSlots * math.cos(angle) + basisRightForSlots * math.sin(angle), basisForwardForSlots);
                                    slotOffset = dir * outerRadius;
                                }
                            }
                            cData.InitialOffset = slotOffset;
                            cData.Initialized = true;
                            ecb.SetComponent(slot.ShadowEntity, cData);
                        }
                    }

                    // 보유한 해당 스킬의 최고 레벨 ID를 찾아 현재 그림자의 능력치 검사 (동기화)
                    if (SystemAPI.HasComponent<ShadowInstanceData>(slot.ShadowEntity) && SystemAPI.HasComponent<ShadowCombatData>(slot.ShadowEntity))
                    {
                        var instanceData = SystemAPI.GetComponent<ShadowInstanceData>(slot.ShadowEntity);
                        int baseTypeID = instanceData.ShadowID / 100; // 앞자리로 스킬 계열 구분
                        
                        int bestShadowID = -1;
                        for (int k = 0; k < activeSkills.Length; k++)
                        {
                            if (activeSkills[k].ShadowID / 100 == baseTypeID)
                            {
                                bestShadowID = activeSkills[k].ShadowID;
                                break;
                            }
                        }

                        // 최고 레벨의 ID가 있고, 현재 소환수의 ID보다 높다면
                        if (bestShadowID != -1 && bestShadowID > instanceData.ShadowID)
                        {
                            // 강제 파괴 없이 항상 스탯과 레벨만 즉시 덮어쓰기
                            ref var shadows = ref shadowDB.DatabaseRef.Value.Shadows;
                            int dbIndex = -1;
                            for (int k = 0; k < shadows.Length; k++)
                            {
                                if (shadows[k].ID == bestShadowID)
                                {
                                    dbIndex = k;
                                    break;
                                }
                            }

                            if (dbIndex != -1)
                            {
                                ref var shadowDef = ref shadows[dbIndex];
                                
                                var combatData = SystemAPI.GetComponent<ShadowCombatData>(slot.ShadowEntity);
                                ApplyShadowUpgradeBonuses(playerData.ValueRO, shadowDef, ref combatData);
                                
                                ecb.SetComponent(slot.ShadowEntity, combatData);

                                // 데이터도 새로운 렙으로 갱신
                                instanceData.ShadowID = bestShadowID;
                                instanceData.CurrentLevel = bestShadowID % 100;
                                ecb.SetComponent(slot.ShadowEntity, instanceData);
                            }
                        }
                    }
                }
            }

            playerData.ValueRW.CurrentShadow = aliveCount;

            if (!playerData.ValueRO.InitialShadowsSpawned)
            {
                if (activeSkills.Length == 0)
                {
                    // 스킬 버퍼 동기화 전에는 초기 소환 타이머를 소비하지 않는다.
                    continue;
                }

                playerData.ValueRW.InitialShadowsSpawned = true;
                int initialSpawnCount = math.clamp(playerData.ValueRO.InitialShadowSpawnCount, 1, maxShadowSlots);
                playerData.ValueRW.ShadowRegenTimer = -playerData.ValueRO.ShadowRegenCooldown * (initialSpawnCount - 1);
            }

            if (activeSkills.Length == 0)
            {
                continue;
            }

            // 살아있는 슬롯 기준으로 재소환 판단 (외부 CurrentShadow 덮어쓰기 영향 차단)
            if (aliveCount < maxShadowSlots)
            {
                playerData.ValueRW.ShadowRegenTimer -= deltaTime;

                if (playerData.ValueRW.ShadowRegenTimer <= 0f)
                {
                    // 타이머 연장 (연속 스폰 지원)
                    playerData.ValueRW.ShadowRegenTimer += playerData.ValueRO.ShadowRegenCooldown;

                    // 앞에서부터 첫 번째 빈 슬롯을 찾고, 없으면 뒤에 새 슬롯을 추가합니다.
                    int targetIndex = -1;
                    int slotSearchLength = math.min(shadowSlots.Length, maxShadowSlots);
                    for (int i = 0; i < slotSearchLength; i++)
                    {
                        var slot = shadowSlots[i];
                        if (!slot.IsAlive || slot.ShadowEntity == Entity.Null)
                        {
                            targetIndex = i;
                            break;
                        }
                    }

                    if (targetIndex == -1 && shadowSlots.Length < maxShadowSlots)
                    {
                        targetIndex = shadowSlots.Length;
                    }

                    Entity targetShadow = Entity.Null;

                    int skillIndex = random.NextInt(0, activeSkills.Length);
                    int shadowID = activeSkills[skillIndex].ShadowID;

                    if (targetIndex < maxShadowSlots)
                    {
                        // 실제 소환 대상 찾기
                        ref var shadows = ref shadowDB.DatabaseRef.Value.Shadows;
                        int dbIndex = -1;
                        for (int i = 0; i < shadows.Length; i++)
                        {
                            if (shadows[i].ID == shadowID)
                            {
                                dbIndex = i;
                                break;
                            }
                        }

                        if (dbIndex == -1 && shadows.Length > 0) { dbIndex = 0; shadowID = shadows[0].ID; } else if (dbIndex == -1) continue;

                        ref var shadowDef = ref shadows[dbIndex];
                        int currentLevel = shadowID % 100;

                        // 스폰 위치 계산
                        float3 playerPos = playerTransform.ValueRO.Position;
                        float3 basisForward = basisForwardForSlots;
                        float3 right = basisRightForSlots;
                        float3 spawnPos = playerPos;
                        const float innerRadius = 3f;
                        const float outerRadius = 6f;
                        float3 slotOffset = float3.zero;
                        if (targetIndex >= 0 && targetIndex < 20)
                        {
                            if (targetIndex < 8)
                            {
                                float angleStep = (math.PI * 2f) / 8f;
                                float angle = targetIndex * angleStep;
                                float3 dir = math.normalizesafe(basisForward * math.cos(angle) + right * math.sin(angle), basisForward);
                                slotOffset = dir * innerRadius;
                            }
                            else
                            {
                                int outerIndex = targetIndex - 8;
                                float angleStep = (math.PI * 2f) / 12f;
                                float angle = outerIndex * angleStep;
                                float3 dir = math.normalizesafe(basisForward * math.cos(angle) + right * math.sin(angle), basisForward);
                                slotOffset = dir * outerRadius;
                            }
                        }
                        spawnPos += slotOffset + new float3(0, 1f, 0);

                        // 스폰 위치 로직 끝

                        // 공통적으로 사용할 LocalTransform 위치
                        var newTr = new LocalTransform { Position = spawnPos, Scale = 0.7f, Rotation = quaternion.identity };

                        if (spawnerData.ValueRO.ShadowPrefab == Entity.Null)
                        {
                            UnityEngine.Debug.LogError("Shadow Prefab is not assigned in ShadowSpawnData!");
                            continue;
                        }
                        // 신규 생성
                        targetShadow = ecb.Instantiate(spawnerData.ValueRO.ShadowPrefab);
                        
                        // 진형 내 인덱스 및 초기 데이터 주입
                        ecb.SetComponent(targetShadow, newTr); 
                        ecb.SetComponent(targetShadow, new PhysicsGraphicalInterpolationBuffer
                        {
                            PreviousTransform = new RigidTransform(newTr.Rotation, newTr.Position)
                        });
                        ecb.SetComponent(targetShadow, new ShadowInstanceData { ShadowID = shadowID, CurrentLevel = currentLevel });
                        var baseShadowData = SystemAPI.GetComponent<CShadowData>(spawnerData.ValueRO.ShadowPrefab);
                        baseShadowData.Index = targetIndex;
                        baseShadowData.CurrentState = ShadowAIState.Idle;
                        baseShadowData.StateChangeTimer = 0f;
                        baseShadowData.InitialOffset = slotOffset;
                        baseShadowData.Initialized = true;
                        ecb.SetComponent(targetShadow, baseShadowData);
                        // 스탯 주입
                        var combatData = SystemAPI.GetComponent<ShadowCombatData>(spawnerData.ValueRO.ShadowPrefab);
                        ApplyShadowUpgradeBonuses(playerData.ValueRO, shadowDef, ref combatData);
                        combatData.AttackType = (AttackType)(shadowDef.AttackType - 1);
                        combatData.IsAlive = true;
                        ecb.SetComponent(targetShadow, combatData);
                        
                        var targetingData = SystemAPI.GetComponent<TargetingData>(spawnerData.ValueRO.ShadowPrefab);
                        targetingData.Priority = (TargetingType)shadowDef.TargetPriority;
                        float totalRange = shadowDef.Recognize + shadowDef.AttackRange;
                        targetingData.MaxSearchRangeSq = totalRange * totalRange;
                        if (targetingData.MaxSearchRangeSq <= 0.1f) targetingData.MaxSearchRangeSq = 144f; // fallback
                        targetingData.MaxFollowRangeSq = targetingData.MaxSearchRangeSq; // [FIX] 유지 반경을 탐색 반경과 일치시켜 추적 해제 방지
                        ecb.SetComponent(targetShadow, targetingData);

                        var healthData = SystemAPI.GetComponent<HealthData>(spawnerData.ValueRO.ShadowPrefab);
                        healthData.MaxHealth = shadowDef.MaxHealth;
                        healthData.CurrentHealth = shadowDef.MaxHealth;
                        ecb.SetComponent(targetShadow, healthData);

                        // 가장 앞 빈 슬롯을 채우거나, 빈 슬롯이 없으면 뒤에 추가합니다.
                        var updatedSlots = new NativeList<ShadowSlotElement>(math.max(shadowSlots.Length + 1, maxShadowSlots), Allocator.Temp);
                        for (int i = 0; i < shadowSlots.Length; i++)
                        {
                            updatedSlots.Add(shadowSlots[i]);
                        }

                        if (targetIndex < updatedSlots.Length)
                        {
                            updatedSlots[targetIndex] = new ShadowSlotElement { ShadowEntity = targetShadow, IsAlive = true };
                        }
                        else
                        {
                            updatedSlots.Add(new ShadowSlotElement { ShadowEntity = targetShadow, IsAlive = true });
                        }

                        var slotBuffer = ecb.SetBuffer<ShadowSlotElement>(entity);
                        for (int i = 0; i < updatedSlots.Length; i++)
                        {
                            slotBuffer.Add(updatedSlots[i]);
                        }
                        updatedSlots.Dispose();

                        // 스폰 완료에 따른 플레이어 데이터 업데이트
                        playerData.ValueRW.CurrentShadow++;
                    }
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion

#region ItemLootSystem
[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct ItemLootSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 플레이어 정보 획득
        var playerQuery = SystemAPI.QueryBuilder().WithAll<PlayerInput, PlayerData, LocalTransform>().Build();
        if (playerQuery.IsEmpty) return; // 플레이어가 존재하지 않으면 종료

        var playerEntity = playerQuery.GetSingletonEntity();
        var playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        var playerData = SystemAPI.GetComponent<PlayerData>(playerEntity);

        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // 플레이어의 아이템 습득/자석 반경
        float magnetismRadiusSq = playerData.MagnetismRadius * playerData.MagnetismRadius;
        float collectRadiusSq = playerData.CollectRadius * playerData.CollectRadius;

        foreach (var (itemData, transform, filter, entity) in
                SystemAPI.Query<RefRW<DroppedItemData>, RefRW<LocalTransform>, RefRW<CustomCollisionFilter>>().WithEntityAccess())
        {
            float distSq = math.distancesq(transform.ValueRO.Position, playerTransform.Position);

            // 자석 범위 안에 들어오면 끌려가기 시작
            if (distSq <= magnetismRadiusSq || itemData.ValueRO.IsAttracted)
            {
                itemData.ValueRW.IsAttracted = true;

                // 아이템이 플레이어에게 끌려가기 시작하면 물리 충돌을 완벽히 꺼버림
                if (filter.ValueRO.Value.CollidesWith != GamePhysicsLayers.ItemMagnetizedMask)
                {
                    filter.ValueRW.Value.CollidesWith = GamePhysicsLayers.ItemMagnetizedMask;
                }

                // 플레이어를 향해 이동
                float3 dir = math.normalize(playerTransform.Position - transform.ValueRO.Position);
                transform.ValueRW.Position += dir * itemData.ValueRO.MoveSpeed * deltaTime;

                // 가속도
                itemData.ValueRW.MoveSpeed += 15f * deltaTime;
            }

            // 획득 판정
            if (distSq <= collectRadiusSq)
            {
                // 타입별 처리
                switch (itemData.ValueRO.Type)
                {
                    case DropItemType.Exp:
                        playerData.EXP += itemData.ValueRO.Amount * playerData.ExpGainMultiplier;
                        break;
                    case DropItemType.Gold:
                        var goldEvent = ecb.CreateEntity();
                        ecb.AddComponent(goldEvent, new GoldEventTag { amount = (int)itemData.ValueRO.Amount });
                        break;
                    case DropItemType.Magnet:
                        var magnetEvent = ecb.CreateEntity();
                        ecb.AddComponent<MagnetEventTag>(magnetEvent);
                        break;
                    case DropItemType.Bomb:
                        var bombEvent = ecb.CreateEntity();
                        ecb.AddComponent<BombEventTag>(bombEvent);
                        break;
                }

                ecb.AddComponent(entity, new DestroyEntityTag());
            }
        }

        ecb.SetComponent(playerEntity, playerData);

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion

#region ItemEventSystem
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ItemLootSystem))]
[BurstCompile]
public partial struct ItemEventSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        bool triggerMagnet = false;
        bool triggerBomb = false;

        foreach (var (tag, entity) in SystemAPI.Query<RefRO<MagnetEventTag>>().WithEntityAccess())
        {
            triggerMagnet = true;
            ecb.DestroyEntity(entity);
        }
        foreach (var (tag, entity) in SystemAPI.Query<RefRO<BombEventTag>>().WithEntityAccess())
        {
            triggerBomb = true;
            ecb.DestroyEntity(entity);
        }

        if (triggerMagnet)
        {
            foreach (var itemData in SystemAPI.Query<RefRW<DroppedItemData>>())
            {
                itemData.ValueRW.IsAttracted = true;
            }
        }

        if (triggerBomb)
        {
            float3 playerPos = float3.zero;
            if (SystemAPI.TryGetSingleton<PlayerData>(out var playerData))
            {
                playerPos = SystemAPI.GetComponent<LocalTransform>(SystemAPI.GetSingletonEntity<PlayerData>()).Position;
            }

            float bombRadiusSq = 15f * 15f;
    
            foreach (var (healthData, enemyData, transform) in SystemAPI.Query<RefRW<HealthData>, RefRO<CEnemyData>, RefRO<LocalTransform>>())
            {
                if (math.distancesq(transform.ValueRO.Position, playerPos) <= bombRadiusSq)
                {
                    healthData.ValueRW.CurrentHealth = 0f;
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion

#region LevelUp System
[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct PlayerLevelUpSystem : ISystem
{
    [BurstCompile]
    public static float GetRequiredExpForNextLevel(int currentLevel)
    {
        if (currentLevel < 1) return 20f; // Start a bit harder
        
        // EXP Curve QA adjusting
        return 20f + 15f * (currentLevel - 1);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // 레벨업 대기 중이면 갱신 중지
        if (SystemAPI.HasSingleton<LevelUpEventTag>())
        {
            ecb.Dispose();
            return;
        }

        foreach (var (playerData, playerTransform, entity) in
                 SystemAPI.Query<RefRW<PlayerData>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            int levelUpsThisFrame = 0;

            while (true)
            {
                float requiredExp = GetRequiredExpForNextLevel(playerData.ValueRO.Level);

                if (playerData.ValueRO.EXP >= requiredExp)
                {
                    playerData.ValueRW.EXP -= requiredExp;
                    playerData.ValueRW.Level++;
                    levelUpsThisFrame++;
                }
                else
                {
                    break;
                }
            }

            if (levelUpsThisFrame > 0)
            {
                // 레벨업 이벤트 태그 추가 (버퍼 추가 구현)
                ecb.AddComponent(entity, new LevelUpEventTag { Count = levelUpsThisFrame });
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion



