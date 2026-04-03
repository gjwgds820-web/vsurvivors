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

        foreach (var (transform, physicsVelocity, physicsMass, input, movement) in 
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<PhysicsMass>, RefRO<PlayerInput>, RefRO<PlayerMovementData>>())
        {
            // 역관성 0으로 강제 세팅 (넘어짐 방지)
            physicsMass.ValueRW.InverseInertia = new float3(0f, 0f, 0f);

            float2 inputMove = input.ValueRO.Move;
            float3 moveDirection = new float3(inputMove.x, 0f, inputMove.y);

            if (math.lengthsq(moveDirection) > 0.01f)
            {
                moveDirection = math.normalize(moveDirection);

                // 위치 이동 적용
                float3 currentVelocity = physicsVelocity.ValueRO.Linear;
                physicsVelocity.ValueRW.Linear = new float3(
                    moveDirection.x * movement.ValueRO.MoveSpeed,
                    currentVelocity.y, 
                    moveDirection.z * movement.ValueRO.MoveSpeed
                );
                
                // 💡 회전 제어 (이 부분에서 직접 Y축 방향을 LookRotation으로 보정합니다)
                // Y축 값이 완전히 0인 완벽한 평면 벡터를 생성
                float3 flatForward = new float3(moveDirection.x, 0f, moveDirection.z);
                
                if (math.lengthsq(flatForward) > 0.001f)
                {
                    flatForward = math.normalize(flatForward);
                    quaternion targetRotation = quaternion.LookRotationSafe(flatForward, math.up());
                    
                    // Slerp를 통해 부드럽게 회전
                    quaternion newRotation = math.slerp(transform.ValueRO.Rotation, targetRotation, movement.ValueRO.RotationSpeed * deltaTime);
                    

                    newRotation.value.x = 0f;
                    newRotation.value.z = 0f;
                    newRotation = math.normalize(newRotation);
                    
                    transform.ValueRW.Rotation = newRotation;
                }
            }
            else
            {
                // 입력이 없을 때
                float3 currentVelocity = physicsVelocity.ValueRO.Linear;
                physicsVelocity.ValueRW.Linear = new float3(0f, currentVelocity.y, 0f);
                physicsVelocity.ValueRW.Angular = float3.zero;

                // 멈춰있을 때도 현재 각도에서 X, Z의 기울어짐을 방지
                quaternion currentRot = transform.ValueRO.Rotation;
                currentRot.value.x = 0f;
                currentRot.value.z = 0f;
                transform.ValueRW.Rotation = math.normalize(currentRot);
            }
        }
    }
}
#endregion

#region Death System
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitHealthSystem))]
[BurstCompile]
public partial struct PlayerDeathSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        foreach (var (playerData, entity) in
                 SystemAPI.Query<RefRO<PlayerData>>()
                 .WithAll<DeathTag>()
                 .WithEntityAccess())
        {
            // 사망 처리 (예: 게임 오버 이벤트 생성)
            var gameOverEvent = ecb.CreateEntity();
            ecb.AddComponent<PlayerDeathEventTag>(gameOverEvent);

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
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<ShadowDatabaseComponent>(out var shadowDB)) return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (playerData, spawnerData, playerTransform, entity) in
                 SystemAPI.Query<RefRW<PlayerData>, RefRO<ShadowSpawnData>,  RefRO<LocalTransform>>().WithEntityAccess())
        {
            var shadowSlots = SystemAPI.GetBuffer<ShadowSlotElement>(entity);
            // 현재 살아있는 그림자 수 확인 및 죽은 그림자 동기화
            int aliveCount = 0;
            for (int i = 0; i < shadowSlots.Length; i++)
            {
                var slot = shadowSlots[i];
                if (slot.ShadowEntity != Entity.Null && slot.ShadowEntity.Index >= 0)
                {
                    // 상태 체크
                    if (SystemAPI.HasComponent<ShadowCombatData>(slot.ShadowEntity))
                    {
                        slot.IsAlive = SystemAPI.GetComponent<ShadowCombatData>(slot.ShadowEntity).IsAlive;
                        shadowSlots[i] = slot; // 버퍼 업데이트
                    }
                    else if (!SystemAPI.Exists(slot.ShadowEntity))
                    {
                        // 그림자 엔티티가 파괴(DeathSystem에서 처리)된 경우 상태 초기화
                        slot.IsAlive = false;
                        slot.ShadowEntity = Entity.Null;
                        shadowSlots[i] = slot;
                    }
                }
                if (slot.IsAlive) aliveCount++;
            }
            playerData.ValueRW.CurrentShadow = aliveCount;

            // 살아있는 그림자가 최대치보다 적을때 타이머 감소
            if (playerData.ValueRO.CurrentShadow < playerData.ValueRO.MaxShadow)
            {
                playerData.ValueRW.ShadowRegenTimer -= deltaTime;

                if (playerData.ValueRO.ShadowRegenTimer <= 0f)
                {
                    // 타이머 초기화
                    playerData.ValueRW.ShadowRegenTimer = playerData.ValueRO.ShadowRegenCooldown;

                    // 빈자리 찾기
                    int targetIndex = -1;
                    Entity targetShadow = Entity.Null;
                    bool needInstantiate = false;

                    // 먼저 게임 매니저나 실제 플레이어가 보유한 그림자 목록에 맞게 ID를 동적으로 가져올 수 있게 확장 고려
                    // 현재는 기본 ID(40000001)로 고정
                    int shadowID = 40000001; 
                    
                    for (int i = 0; i < shadowSlots.Length; i++)
                    {
                        if (i >= playerData.ValueRO.MaxShadow) break; 

                        if (!shadowSlots[i].IsAlive)
                        {
                            targetIndex = i;
                            // 죽은 슬롯은 덮어씌기 처리
                            break;
                        }
                    }

                    if (targetIndex == -1 && shadowSlots.Length < playerData.ValueRO.MaxShadow)
                    {
                        targetIndex = shadowSlots.Length;
                        needInstantiate = true;
                    }

                    if (targetIndex != -1)
                    {
                        // 1. 보유중인 섀도우 데이터를 GameManager 혹은 PlayerData 버퍼에서 가져오는 것이 맞음
                        // 만약 별도의 버퍼(ActiveShadows)가 있다면 해당 스킬 ID를 참조해야 함
                        // 지금은 유저가 "보유중인 그림자 수가 줄어들어야 하는데"라고 표현했으므로
                        // 특정 슬롯 인덱스별로 ID가 다를 수 있음
                        
                        // 현재 테스트 시나리오에 맞춰서 스폰 로직 그대로 유지
                        int currentLevel = 1;
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

                        if (dbIndex == -1) continue;

                        // 레벨 스탯 가져오기
                        ref var shadowDef = ref shadows[dbIndex];
                        ref var statBlob = ref shadowDef.LevelStats[currentLevel - 1];

                        // 스폰 위치 계산
                        float3 playerPos = playerTransform.ValueRO.Position;
                        float3 playerForward = math.forward(playerTransform.ValueRO.Rotation);
                        float3 right = math.cross(math.up(), playerForward);
                        float distance = 3f;
                        float3 spawnPos = playerPos;

                        // Idle 기준 위치 계산
                        if (targetIndex < 8)
                        {
                            float angle = (targetIndex / 8f) * math.PI * 2f;
                            spawnPos += right * math.cos(angle) * distance + playerForward * math.sin(angle) * distance + new float3(0, 1f, 0);
                        }
                        else
                        {
                            float angle = ((targetIndex - 8) / 12f) * math.PI * 2f;
                            spawnPos += right * math.cos(angle) * distance * 2f + playerForward * math.sin(angle) * distance * 2f + new float3(0, 1f, 0);
                        }

                        // 스폰 위치 로직 끝

                        // 공통적으로 사용할 LocalTransform 위치
                        var newTr = new LocalTransform { Position = spawnPos, Scale = 1f, Rotation = quaternion.identity };

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
                        baseShadowData.CurrentState = FormationState.Idle;
                        baseShadowData.StateChangeTimer = 0f;
                        ecb.SetComponent(targetShadow, baseShadowData);
                        // 스탯 주입
                        var combatData = SystemAPI.GetComponent<ShadowCombatData>(spawnerData.ValueRO.ShadowPrefab);
                        combatData.AttackPower = statBlob.AttackPower;
                        combatData.AttackRange = statBlob.AttackRange;
                        combatData.AttackCooldown = statBlob.AttackCooldown;
                        combatData.AttackType = (AttackType)shadowDef.AttackType;
                        combatData.IsAlive = true;
                        ecb.SetComponent(targetShadow, combatData);
                        
                        var targetingData = SystemAPI.GetComponent<TargetingData>(spawnerData.ValueRO.ShadowPrefab);
                        targetingData.Priority = (TargetingType)shadowDef.TargetPriority;
                        ecb.SetComponent(targetShadow, targetingData);

                        var healthData = SystemAPI.GetComponent<HealthData>(spawnerData.ValueRO.ShadowPrefab);
                        healthData.MaxHealth = statBlob.MaxHealth;
                        healthData.CurrentHealth = statBlob.MaxHealth;
                        ecb.SetComponent(targetShadow, healthData);

                        if (needInstantiate)
                        {
                            ecb.AppendToBuffer(entity, new ShadowSlotElement { ShadowEntity = targetShadow, IsAlive = true });
                        }
                        else
                        {
                            // 동적 버퍼에 덮어쓸 경우, ECB를 통해 기록해야 임시(Negative) Entity가 실제 Entity로 치환됩니다.
                            // 즉시 덮어쓰지 않고 defer 처리
                            var bufferHandle = ecb.SetBuffer<ShadowSlotElement>(entity);
                            var currentSlots = shadowSlots.AsNativeArray();
                            for (int i = 0; i < currentSlots.Length; i++)
                            {
                                if (i == targetIndex)
                                    bufferHandle.Add(new ShadowSlotElement { ShadowEntity = targetShadow, IsAlive = true });
                                else
                                    bufferHandle.Add(currentSlots[i]);
                            }
                        }
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
                        playerData.EXP += itemData.ValueRO.Amount;
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
            // GameDirectorData 싱글톤에서 레벨업 필요 경험치 기준량 가져오기 (없으면 100f)
            float expBase = 100f;
            if (SystemAPI.TryGetSingleton<GameDirectorData>(out var directorData))
            {
                expBase = directorData.ExpRequirementBase;
            }

            float requiredExp = expBase * playerData.ValueRO.Level;
            int levelUpsThisFrame = 0;

            while (playerData.ValueRO.EXP >= requiredExp)
            {
                playerData.ValueRW.EXP -= requiredExp;
                playerData.ValueRW.Level++;
                levelUpsThisFrame++;
                
                // 다음 레벨업 요구치 갱신
                requiredExp = expBase * playerData.ValueRO.Level;
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
