using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Collections;

#region ProjectileMovement
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(HitBoxCollisionSystem))]
[BurstCompile]
public partial struct ProjectileMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        // 투사체 이동 처리
        foreach (var (transform, projData) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRO<ProjectileData>>())
        {
            float3 forward = math.forward(transform.ValueRO.Rotation);
            transform.ValueRW.Position += forward * projData.ValueRO.Speed * dt;
        }
    }
}
#endregion

#region HitBoxCollision
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
[BurstCompile]
public partial struct HitBoxCollisionSystem : ISystem
{
    [BurstCompile]
    public unsafe void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        double currentTime = SystemAPI.Time.ElapsedTime;
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld))
        {
            ecb.Dispose();
            return;  
        } 

        var collisionWorld = physicsWorld.CollisionWorld;

        // 적 조회용 Lookup
        var damageBufferLookup = SystemAPI.GetBufferLookup<DamageBufferElement>(false);

        foreach (var (hitbox, transform, entity) in
                 SystemAPI.Query<RefRW<HitBoxData>, RefRO<LocalTransform>>().WithAll<HitRecordElement>().WithEntityAccess())
        {
            var hitBuffer = SystemAPI.GetBuffer<HitRecordElement>(entity);
            // 수명 감소 및 자동 파괴
            hitbox.ValueRW.Duration -= dt;
            if (hitbox.ValueRO.Duration <= 0f)
            {
                ecb.DestroyEntity(entity);
                continue;
            }

            // 검색 결과를 담을 리스트
            NativeList<Entity> tempHitTargets = new NativeList<Entity>(Allocator.Temp);

            // Shape별 물리 검색
            switch (hitbox.ValueRO.Shape)
            {
                case HitBoxShape.Circle:
                case HitBoxShape.Cone:
                {
                    // 원형 및 부채꼴은 반경 기준으로 탐색
                    NativeList<DistanceHit> distHits = new NativeList<DistanceHit>(Allocator.Temp);
                    if (collisionWorld.CalculateDistance(new PointDistanceInput
                    {
                        Position = transform.ValueRO.Position,
                        MaxDistance = hitbox.ValueRO.Radius,
                        Filter = CollisionFilter.Default
                    }, ref distHits))
                    {
                        float3 hitboxForward = math.forward(transform.ValueRO.Rotation);

                        // 설정한 각도의 절반 코사인 값
                        float dotThreshold = (hitbox.ValueRO.Shape == HitBoxShape.Cone) ? math.cos(math.radians(hitbox.ValueRO.Angle * 0.5f)) : -1f;

                        for (int i = 0; i < distHits.Length; i++)
                        {
                            Entity targetEnt = distHits[i].Entity;

                            // 부채꼴인 경우, 타겟이 공격 방향 내에 있는지 추가 확인
                            if (hitbox.ValueRO.Shape == HitBoxShape.Cone)
                            {
                                float3 toTarget = math.normalize(distHits[i].Position - transform.ValueRO.Position);
                                float dot = math.dot(hitboxForward, toTarget);
                                if (dot < dotThreshold)
                                {
                                    continue; // 타겟이 부채꼴 범위 밖
                                }
                            }

                            tempHitTargets.Add(targetEnt);
                        }
                    }
                    distHits.Dispose();
                    break;
                }

                case HitBoxShape.Box:
                {
                    // 직사각형은 실제 박스 콜라이더의 모양을 허공에 생성하여 overlap 검사
                    var boxGeometry = new BoxGeometry
                    {
                        Center = float3.zero,
                        Orientation = quaternion.identity,
                        Size = hitbox.ValueRO.BoxExtents * 2f,
                        BevelRadius = 0f
                    };
                    BlobAssetReference<Collider> boxCollider = BoxCollider.Create(boxGeometry);

                    // 공간에 던질 투사체 생성
                    var overlapInput = new ColliderCastInput()
                    {
                        Collider = (Collider*)boxCollider.GetUnsafePtr(),
                        Orientation = transform.ValueRO.Rotation,
                        Start = transform.ValueRO.Position,
                        End = transform.ValueRO.Position, // 박스 콜라이더 자체가 위치에 고정되어 있으므로 Start와 End는 동일
                    };

                    NativeList<ColliderCastHit> boxHits = new NativeList<ColliderCastHit>(Allocator.Temp);
                    if (collisionWorld.CastCollider(overlapInput, ref boxHits))
                    {
                        for (int i = 0; i < boxHits.Length; i++)
                        {
                            Entity hitEnt = boxHits[i].Entity;
                            tempHitTargets.Add(hitEnt);
                        }
                    }
                    boxHits.Dispose();
                    boxCollider.Dispose();
                    break;
                }
            }

            // 결과 처리
            for (int i = 0; i < tempHitTargets.Length; i++)
            {
                Entity targetEnt = tempHitTargets[i];
                // 타겟이 이미 피격된 적인지 확인
                bool canHit = true;
                int bufferIndex = -1;

                for (int b = 0; b < hitBuffer.Length; b++)
                {
                    if (hitBuffer[b].Target == targetEnt)
                    {
                        bufferIndex = b;

                        // TickRate가 0이나 음수면 단발성 공격
                        if (hitbox.ValueRO.TickRate <= 0.001f)
                        {
                            canHit = false;
                        }
                        // 장판 공격이면 현재 시간이 쿨다운을 지났는지 비교
                        else if (currentTime < hitBuffer[b].LastHitTime + hitbox.ValueRO.TickRate)
                        {
                            canHit = false;
                        }
                        break;
                    }
                }

                if (!canHit) continue; // 이미 피격된 타겟이거나 아직 쿨다운이 지나지 않음

                // 타겟이 적인지 확인 (TargetFaction이 0이면 적을 타겟, 1이면 아군을 타겟으로 가정)
                bool isValidTarget = false;
                if (hitbox.ValueRO.TargetFaction == 0 && SystemAPI.HasComponent<EnemyData>(targetEnt))
                {
                    isValidTarget = true;
                }
                else if (hitbox.ValueRO.TargetFaction == 1 && (SystemAPI.HasComponent<PlayerData>(targetEnt) || SystemAPI.HasComponent<ShadowCombatData>(targetEnt)))
                {
                    isValidTarget = true;
                }

                // 데미지 적용
                if (isValidTarget && damageBufferLookup.HasBuffer(targetEnt))
                {
                    // 타겟의 데미지 버퍼에 데미지 추가
                    damageBufferLookup[targetEnt].Add(new DamageBufferElement { Damage = hitbox.ValueRO.Damage });

                    // 피격 기록 업데이트
                    if (bufferIndex == -1)
                    {
                        // 새로운 타겟이므로 버퍼에 추가
                        hitBuffer.Add(new HitRecordElement { Target = targetEnt, LastHitTime = currentTime });
                    }
                    else
                    {
                        // 기존 타겟이므로 마지막 피격 시간 업데이트
                        var record = hitBuffer[bufferIndex];
                        record.LastHitTime = currentTime;
                        hitBuffer[bufferIndex] = record;
                    }

                    // 피격효과 생성

                    // 광역/관통기가 아니라면 1명만 맞추고 파괴
                    if (!hitbox.ValueRO.IsPiercing)
                    {
                        ecb.DestroyEntity(entity);
                        break;
                    }
                }
            }
            tempHitTargets.Dispose();
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion