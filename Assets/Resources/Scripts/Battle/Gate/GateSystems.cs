using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct GateInteractionSystem : ISystem
{
    private EntityQuery _playerQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _playerQuery = SystemAPI.QueryBuilder().WithAll<PlayerData, LocalTransform>().Build();
        state.RequireForUpdate(_playerQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var playerEntity = _playerQuery.GetSingletonEntity();
        var playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        var playerDataInfo = SystemAPI.GetComponent<PlayerData>(playerEntity); // RefRO 대신 값을 가져옵니다
        // 플레이어의 살아있는 섀도우 버퍼를 가져옵니다.
        var shadowSlots = SystemAPI.GetBuffer<ShadowSlotElement>(playerEntity);

        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        
        uint fixedSeed = (uint)((SystemAPI.Time.ElapsedTime + 1f) * 1000f);
        var random = Unity.Mathematics.Random.CreateFromIndex(fixedSeed);

        foreach (var (gateData, gateTransform, entity) in SystemAPI.Query<RefRW<GateData>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            if (gateData.ValueRO.AbsorbedShadows >= gateData.ValueRO.RequiredShadows)
            {
                ecb.AddComponent<DestroyEntityTag>(entity);
                continue;
            }

            float distSq = math.distancesq(playerTransform.Position, gateTransform.ValueRO.Position);
            float radiusSq = gateData.ValueRO.InteractionRadius * gateData.ValueRO.InteractionRadius;

            // 플레이어가 게이트 반경 안에 들어왔을 때
            if (distSq <= radiusSq)
            {
                gateData.ValueRW.AbsorbtionTimer -= deltaTime;

                if (gateData.ValueRO.AbsorbtionTimer <= 0f)
                {
                    // 그리기 직전 현재 살아있는 그림자 찾기
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

                        // 즉시 파기 표시
                        tempSlot.IsAlive = false;
                        tempSlot.ShadowEntity = Entity.Null;
                        shadowSlots[shadowSlotIndex] = tempSlot;

                        gateData.ValueRW.AbsorbedShadows++;
                        gateData.ValueRW.AbsorbtionTimer = 0.5f; // 0.5초 간격으로 하나씩 흡수 (적절히 조절 가능)
                    }
                    aliveShadowIndices.Dispose();
                }
            }
            else
            {
                gateData.ValueRW.AbsorbtionTimer = 0f;
            }
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
