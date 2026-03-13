using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.Physics.GraphicsIntegration;
using Unity.Collections;

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

#region Health System
[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct PlayerHealthSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (playerData, damageBuffer) in SystemAPI.Query<RefRW<PlayerData>, DynamicBuffer<DamageBufferElement>>())
        {
            // 무적 타이머 감소
            if (playerData.ValueRO.InvincibilityTimer > 0f)
            {
                playerData.ValueRW.InvincibilityTimer -= deltaTime;
                damageBuffer.Clear(); // 무적 상태에서는 받은 피해 무시
                continue;
            }

            if (damageBuffer.Length > 0)
            {
                float finalDamage = math.max(0f, damageBuffer[0].Damage - playerData.ValueRO.DamageReduction);
                playerData.ValueRW.CurrentHealth -= finalDamage;
                playerData.ValueRW.InvincibilityTimer = 0.5f; // 피해를 받은 후 0.5초간 무적

                damageBuffer.Clear();

                // 사망 처리
                if (playerData.ValueRO.CurrentHealth <= 0f)
                {
                    playerData.ValueRW.CurrentHealth = 0f;
                }
            }
        }
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
                if (slot.ShadowEntity != Entity.Null)
                {
                    // 상태 체크
                    if (SystemAPI.HasComponent<ShadowCombatData>(slot.ShadowEntity))
                    {
                        slot.IsAlive = SystemAPI.GetComponent<ShadowCombatData>(slot.ShadowEntity).IsAlive;
                        shadowSlots[i] = slot; // 버퍼 업데이트
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

                    for (int i = 0; i < shadowSlots.Length; i++)
                    {
                        if (i >= playerData.ValueRO.MaxShadow) break; // 최대 소환 수 초과 방지

                        if (!shadowSlots[i].IsAlive)
                        {
                            targetIndex = i;
                            targetShadow = shadowSlots[i].ShadowEntity;
                            break;
                        }
                    }

                    // 빈자리가 없으면 새로 생성
                    if (targetIndex == -1 && shadowSlots.Length < playerData.ValueRO.MaxShadow)
                    {
                        targetIndex = shadowSlots.Length;
                        needInstantiate = true;
                    }

                    // 생성 및 부활
                    if (targetIndex != -1)
                    {
                        if (needInstantiate || targetShadow == Entity.Null)
                        {
                            // 신규 생성
                            targetShadow = ecb.Instantiate(spawnerData.ValueRO.ShadowPrefab);

                            // 진형 내 인덱스 및 초기 데이터 주입
                            ecb.SetComponent(targetShadow, new LocalTransform { Position = playerTransform.ValueRO.Position, Scale = 1f, Rotation = quaternion.identity }); 
                            ecb.SetComponent(targetShadow, new ShadowData { Index = targetIndex, CurrentState = FormationState.Idle, StateChangeTimer = 0f });

                            if (needInstantiate)
                            {
                                shadowSlots.Add(new ShadowSlotElement { ShadowEntity = targetShadow, IsAlive = true });
                            }
                            else
                            {
                                shadowSlots[targetIndex] = new ShadowSlotElement { ShadowEntity = targetShadow, IsAlive = true };
                            }
                        }
                        else
                        {
                            // 부활
                            var combatData = SystemAPI.GetComponent<ShadowCombatData>(targetShadow);
                            combatData.CurrentHealth = combatData.MaxHealth;
                            combatData.CurrentTarget = Entity.Null;
                            ecb.SetComponent(targetShadow, combatData);

                            // 위치 초기화
                            var tr = SystemAPI.GetComponent<LocalTransform>(targetShadow);
                            tr.Position = playerTransform.ValueRO.Position;
                            ecb.SetComponent(targetShadow, tr);

                            var updatedSlot = shadowSlots[targetIndex];
                            updatedSlot.IsAlive = true;
                            shadowSlots[targetIndex] = updatedSlot;
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

        foreach (var (itemData, transform, entity) in
                SystemAPI.Query<RefRW<DroppedItemData>, RefRW<LocalTransform>>().WithEntityAccess())
        {
            float distSq = math.distancesq(transform.ValueRO.Position, playerTransform.Position);

            // 자석 범위 안에 들어오면 끌려가기 시작
            if (distSq <= magnetismRadiusSq || itemData.ValueRO.IsAttracted)
            {
                itemData.ValueRW.IsAttracted = true;

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
                        ecb.AddComponent<MagnetEventTag>(entity);
                        break;
                    case DropItemType.Bomb:
                        ecb.AddComponent<BombEventTag>(entity);
                        break;
                }

                ecb.DestroyEntity(entity);
            }
        }

        ecb.SetComponent(playerEntity, playerData);

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
            float requiredExp = 100f * playerData.ValueRO.Level;

            if (playerData.ValueRO.EXP >= requiredExp)
            {
                playerData.ValueRW.EXP -= requiredExp;
                playerData.ValueRW.Level++;

                // 레벨업 이벤트 태그 추가
                ecb.AddComponent<LevelUpEventTag>(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion