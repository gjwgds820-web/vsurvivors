using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct PortalInteractionSystem : ISystem
{
    private EntityQuery _playerQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _playerQuery = SystemAPI.QueryBuilder().WithAll<PlayerData, LocalTransform>().Build();
        state.RequireForUpdate(_playerQuery);
        state.RequireForUpdate<ConstConfigData>();
        state.RequireForUpdate<GameDirectorData>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var playerEntity = _playerQuery.GetSingletonEntity();
        var playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        var playerDataInfo = SystemAPI.GetComponent<PlayerData>(playerEntity);
        var shadowSlots = SystemAPI.GetBuffer<ShadowSlotElement>(playerEntity);
        var constData = SystemAPI.GetSingleton<ConstConfigData>();

        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        
        uint fixedSeed = (uint)((SystemAPI.Time.ElapsedTime + 1f) * 1000f);
        var random = Unity.Mathematics.Random.CreateFromIndex(fixedSeed);

        // Fetch director for boss portal logic
        var directorEntity = SystemAPI.GetSingletonEntity<GameDirectorData>();
        var directorDataData = SystemAPI.GetComponentRW<GameDirectorData>(directorEntity);

        foreach (var (cPortalData, portalTransform, entity) in SystemAPI.Query<RefRW<CPortalData>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            if (SystemAPI.HasComponent<HiddenIsolatedPortalTag>(entity)) continue;

            int portalType = cPortalData.ValueRO.PortalID;

            // 42020101: 파괴 불가 (인터랙션 없음)
            if (portalType == 42020101)
            {
                cPortalData.ValueRW.AbsorbtionTimer = 0f;
                continue;
            }

            if (cPortalData.ValueRO.RequiredShadows <= 0) continue;

            if (cPortalData.ValueRO.AbsorbedShadows >= cPortalData.ValueRO.RequiredShadows)
            {
                ecb.AddComponent<DestroyEntityTag>(entity);
                continue;
            }

            float distSq = math.distancesq(playerTransform.Position, portalTransform.ValueRO.Position);
            float radiusSq = cPortalData.ValueRO.InteractionRadius * cPortalData.ValueRO.InteractionRadius;

            float requiredHoldTime = (portalType == 42020103) ? 3.0f : (constData.PortalDestroyTimePerShadow > 0 ? constData.PortalDestroyTimePerShadow : 3.0f);
            cPortalData.ValueRW.MaxHoldTime = requiredHoldTime; // For UI

            // 반경 안
            if (distSq <= radiusSq)
            {
                // 초기 진입시 카운트 다운 시작 세팅
                if (cPortalData.ValueRO.AbsorbtionTimer <= 0f)
                {
                    cPortalData.ValueRW.AbsorbtionTimer = requiredHoldTime;
                }

                cPortalData.ValueRW.AbsorbtionTimer -= deltaTime;

                if (cPortalData.ValueRO.AbsorbtionTimer <= 0f)
                {
                    // 42020103: 보스 포탈 (3초 대기 시 보스전 페이즈 즉시 진입)
                    if (portalType == 42020103)
                    {
                        directorDataData.ValueRW.CurrentWave++;
                        directorDataData.ValueRW.PreviousPhase = directorDataData.ValueRO.CurrentPhase;
                        directorDataData.ValueRW.CurrentPhase = GamePhase.IsolatedBossFight;
                        directorDataData.ValueRW.BossTimer = constData.PortalBossTimer > 0f ? constData.PortalBossTimer : 180f;

                        // Save player position and portal
                        directorDataData.ValueRW.SavedPlayerPosition = playerTransform.Position;
                        directorDataData.ValueRW.ActiveIsolatedPortal = entity;

                        // Teleport player
                        ecb.SetComponent(playerEntity, new LocalTransform { Position = new float3(10000, 1, 10000), Rotation = quaternion.identity, Scale = 1f });

                        var bossEventEntity = ecb.CreateEntity();
                        ecb.AddComponent(bossEventEntity, new SpawnBossEventTag 
                        { 
                            BossID = directorDataData.ValueRW.CurrentWave,
                            IsIsolatedBoss = true 
                        });

                        ecb.AddComponent<HiddenIsolatedPortalTag>(entity);
                        
                        // Hide the portal temporarily instead of destroying it
                        ecb.SetComponent(entity, new LocalTransform { Position = new float3(0, -100, 0), Rotation = portalTransform.ValueRO.Rotation, Scale = 0f });
                    }
                    // 42020102: 일반 그림자 헌납 포탈
                    else
                    {
                        NativeList<int> aliveShadowIndices = new NativeList<int>(Allocator.Temp);
                        for (int i = 0; i < shadowSlots.Length; i++)
                        {
                            if (shadowSlots[i].IsAlive && shadowSlots[i].ShadowEntity != Entity.Null && shadowSlots[i].ShadowEntity.Index >= 0)
                            {
                                if (!SystemAPI.HasComponent<DeathTag>(shadowSlots[i].ShadowEntity))
                                {
                                    aliveShadowIndices.Add(i);
                                }
                            }
                        }

                        if (aliveShadowIndices.Length > 0)
                        {
                            int randomIndex = random.NextInt(0, aliveShadowIndices.Length);
                            int shadowSlotIndex = aliveShadowIndices[randomIndex];
                            
                            var tempSlot = shadowSlots[shadowSlotIndex];
                            ecb.AddComponent<DeathTag>(tempSlot.ShadowEntity);

                            tempSlot.IsAlive = false;
                            tempSlot.ShadowEntity = Entity.Null;
                            shadowSlots[shadowSlotIndex] = tempSlot;

                            cPortalData.ValueRW.AbsorbedShadows++;
                            // 다음 그림자 흡수를 위해 초기화
                            cPortalData.ValueRW.AbsorbtionTimer = requiredHoldTime;
                        }
                        aliveShadowIndices.Dispose();
                    }
                }
            }
            else
            {
                // 벗어날 경우 소멸/활성화 진행도(timer) 리셋
                cPortalData.ValueRW.AbsorbtionTimer = requiredHoldTime;
            }
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
