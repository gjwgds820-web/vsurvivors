using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Collections;

#region ProjectileMovement
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(HitBoxCollisionSystem))]
[BurstCompile]
public partial struct ProjectileMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        // ?ъ궗泥??대룞 泥섎━
        foreach (var (transform, projData, entity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<ProjectileData>>().WithNone<Prefab>().WithEntityAccess())
        {
            // 諛⑺뼢???ㅼ젙?섏뼱 ?덈떎硫?Direction ?ъ슜, ?꾨땲硫?transform??forward (?명솚??
            float3 dir = math.lengthsq(projData.ValueRO.Direction) > 0f ? math.normalize(projData.ValueRO.Direction) : math.forward(transform.ValueRO.Rotation);
            
            float moveDist = projData.ValueRO.Speed * dt;
            transform.ValueRW.Position += dir * moveDist;
            
            // 특수: 투사체는 중력을 무시하고 지정된 높이(0.5f)를 항상 유지하도록 강제
            transform.ValueRW.Position.y = 0.5f;

            projData.ValueRW.TravelledDistance += moveDist;

            if (SystemAPI.HasComponent<SpinningProjectileData>(entity))
            {
                var spinData = SystemAPI.GetComponent<SpinningProjectileData>(entity);
                transform.ValueRW.Rotation = math.mul(transform.ValueRW.Rotation, quaternion.AxisAngle(spinData.SpinAxis, spinData.SpinSpeed * dt));
            }

            // 최대 거리를 초과하면 파괴
            if (projData.ValueRO.MaxDistance > 0f && projData.ValueRO.TravelledDistance >= projData.ValueRO.MaxDistance)
            {
                ecb.AddComponent(entity, default(DestroyEntityTag));
            }
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion

#region HitBoxCollision
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct HitBoxCollisionSystem : ISystem
{
    public unsafe void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        double currentTime = SystemAPI.Time.ElapsedTime;
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld))
        {
            return;  
        } 

        var collisionWorld = physicsWorld.CollisionWorld;

        // 대미지 조회용 Lookup
        var damageBufferLookup = SystemAPI.GetBufferLookup<DamageBufferElement>(false);

        foreach (var (hitbox, transform, entity) in
                 SystemAPI.Query<RefRW<HitBoxData>, RefRW<LocalTransform>>().WithAll<HitRecordElement>().WithNone<Prefab>().WithEntityAccess())
        {
            float3 currentPos = transform.ValueRO.Position;
            if (math.abs(currentPos.y - 0.5f) > 0.01f)
            {
                currentPos.y = 0.5f;
                transform.ValueRW.Position = currentPos;
            }

            var hitBuffer = SystemAPI.GetBuffer<HitRecordElement>(entity);
            // 수명 감소 및 자동 파괴
            hitbox.ValueRW.Duration -= dt;
            if (hitbox.ValueRO.Duration <= 0f)
            {
                ecb.AddComponent(entity, default(DestroyEntityTag));
                continue;
            }

            // 검출 결과를 담을 리스트
            NativeList<Entity> tempHitTargets = new NativeList<Entity>(Allocator.Temp);

            // Shape별 물리 검출
            switch (hitbox.ValueRO.Shape)
            {
                case HitBoxShape.Circle:
                case HitBoxShape.Cone:
                {
                    // 원형 및 부채꼴은 반경 기준으로 검색 (y축은 지면 0으로 투영)
                    NativeList<DistanceHit> distHits = new NativeList<DistanceHit>(Allocator.Temp);
                    float3 testPos = new float3(transform.ValueRO.Position.x, 0.0f, transform.ValueRO.Position.z);
                    if (collisionWorld.CalculateDistance(new PointDistanceInput
                    {
                        Position = testPos,
                        MaxDistance = hitbox.ValueRO.Radius,
                        Filter = CollisionFilter.Default
                    }, ref distHits))
                    {
                        // UnityEngine.Debug.Log($"[HitBoxCollision Debug] distHits count: {distHits.Length}, Radius: {hitbox.ValueRO.Radius}, testPos: {testPos}");
                        float3 hitboxForward = math.forward(transform.ValueRO.Rotation);

                        // 설정된 각도의 절반 코사인 값
                        float dotThreshold = (hitbox.ValueRO.Shape == HitBoxShape.Cone) ? math.cos(math.radians(hitbox.ValueRO.Angle * 0.5f)) : -1f;

                        for (int i = 0; i < distHits.Length; i++)
                        {
                            Entity targetEnt = distHits[i].Entity;

                            // 부채꼴일 경우, 타겟이 공격 방향 내에 있는지 추가 확인
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
                    else 
                    {
                        // UnityEngine.Debug.Log($"[HitBoxCollision Debug] No distance hits. Radius: {hitbox.ValueRO.Radius}, Pos: {testPos}");
                    }
                    distHits.Dispose();
                    break;
                }

                case HitBoxShape.Box:
                {
                    // 직사각형은 실제 박스 콜라이더의 모양을 인메모리로 생성하여 overlap 검출
                    var boxGeometry = new BoxGeometry
                    {
                        Center = float3.zero,
                        Orientation = quaternion.identity,
                        Size = hitbox.ValueRO.BoxExtents * 2f,
                        BevelRadius = 0f
                    };
                    BlobAssetReference<Collider> boxCollider = BoxCollider.Create(boxGeometry);

                    // 공간만 차지할 투사체 생성 (y는 0으로)
                    float3 boxTestPos = new float3(transform.ValueRO.Position.x, 0.0f, transform.ValueRO.Position.z);
                    var overlapInput = new ColliderCastInput()
                    {
                        Collider = (Collider*)boxCollider.GetUnsafePtr(),
                        Orientation = transform.ValueRO.Rotation,
                        Start = boxTestPos,
                        End = boxTestPos, // 박스 콜라이더 자체가 위치에 고정되어 있으므로 Start와 End가 동일
                    };

                    NativeList<ColliderCastHit> boxHits = new NativeList<ColliderCastHit>(Allocator.Temp);
                    if (collisionWorld.CastCollider(overlapInput, ref boxHits))
                    {
                        // UnityEngine.Debug.Log($"[HitBoxCollision Debug] Box Hits count: {boxHits.Length}");
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
                Entity rawTarget = tempHitTargets[i];
                Entity targetEnt = rawTarget;
                if (targetEnt.Index < 0 || !SystemAPI.Exists(targetEnt))
                {
                    continue;
                }

                // 자식 콜라이더인 경우 부모(루트) 엔티티를 찾기
                while (SystemAPI.HasComponent<Parent>(targetEnt))
                {
                    targetEnt = SystemAPI.GetComponent<Parent>(targetEnt).Value;
                }

                // UnityEngine.Debug.Log($"[HitBoxCollision Debug] Processing targetEnt: {targetEnt.Index} (from raw: {rawTarget.Index})");

                bool canHit = true;
                int bufferIndex = -1;

                for (int b = 0; b < hitBuffer.Length; b++)
                {
                    if (hitBuffer[b].Target == targetEnt)
                    {
                        bufferIndex = b;

                        // TickRate가 0이거나 소수면 단발성 공격
                        if (hitbox.ValueRO.TickRate <= 0.001f)
                        {
                            canHit = false;
                        }
                        // 장판 공격이면 현재 시간이 쿨타임을 지났는지 비교
                        else if (currentTime < hitBuffer[b].LastHitTime + hitbox.ValueRO.TickRate)
                        {
                            canHit = false;
                        }
                        break;
                    }
                }

                if (!canHit)
                {
                    // UnityEngine.Debug.Log($"[HitBoxCollision Debug] Skipped {targetEnt.Index} because canHit is false. TickRate: {hitbox.ValueRO.TickRate}");
                    continue;
                }

                // 타겟이 적인지 확인 (TargetFaction이 0이면 적을 타겟, 1이면 아군을 타겟으로 간주)
                bool isValidTarget = false;
                if (hitbox.ValueRO.TargetFaction == 0 && SystemAPI.HasComponent<CEnemyData>(targetEnt))
                {
                    isValidTarget = true;
                }
                else if (hitbox.ValueRO.TargetFaction == 1 && (SystemAPI.HasComponent<PlayerData>(targetEnt) || SystemAPI.HasComponent<ShadowCombatData>(targetEnt) || SystemAPI.HasComponent<ShadowTag>(targetEnt)))
                {
                    isValidTarget = true;
                }

                if (!isValidTarget)
                {
                    bool hasEnemyData = SystemAPI.HasComponent<CEnemyData>(targetEnt);
                    bool hasBuffer = damageBufferLookup.HasBuffer(targetEnt);
                    // UnityEngine.Debug.Log($"[HitBoxCollision Debug] Invalid Target Faction. targetEnt: {targetEnt.Index}, Expected: {hitbox.ValueRO.TargetFaction}, HasCEnemyData: {hasEnemyData}, HasBuffer: {hasBuffer}");
                }
                else
                {
                    if (!damageBufferLookup.HasBuffer(targetEnt))
                    {
                        // UnityEngine.Debug.Log($"[HitBoxCollision Debug] CRITICAL ERROR! Valid Target {targetEnt.Index} DOES NOT have DamageBufferElement! HasCEnemyData: true");
                    }
                }

                // 대미지 적용
                if (isValidTarget && damageBufferLookup.HasBuffer(targetEnt))
                {
                    // UnityEngine.Debug.Log($"[HitBoxCollision Debug] APPLYING DAMAGE: {hitbox.ValueRO.Damage} to Enity: {targetEnt.Index}");

                    // 타겟의 대미지 버퍼에 대미지 추가
                    damageBufferLookup[targetEnt].Add(new DamageBufferElement { Damage = hitbox.ValueRO.Damage });

                    // 타격 기록 업데이트
                    if (bufferIndex == -1)
                    {
                        // 새로운 타겟이므로 버퍼에 추가
                        hitBuffer.Add(new HitRecordElement { Target = targetEnt, LastHitTime = currentTime });
                    }
                    else
                    {
                        // 기존 타겟이므로 마지막 타격 시간 업데이트
                        var record = hitBuffer[bufferIndex];
                        record.LastHitTime = currentTime;
                        hitBuffer[bufferIndex] = record;
                    }

                    // 광역/관통기가 아니면 1명만 맞추고 파괴
                    if (!hitbox.ValueRO.IsPiercing)
                    {
                        ecb.AddComponent(entity, default(DestroyEntityTag));
                        break;
                    }
                    else if (hitbox.ValueRO.MaxPierceCount > 0)
                    {
                        // 관통기 관통수 제한이 있을 경우
                        hitbox.ValueRW.CurrentPierceCount++;
                        if (hitbox.ValueRO.CurrentPierceCount >= hitbox.ValueRO.MaxPierceCount)
                        {
                            ecb.AddComponent(entity, default(DestroyEntityTag));
                            break;
                        }
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

#region Cleanup
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(VisualCleanupSystem))]
[BurstCompile]
public partial struct CleanupDestroyedEntitySystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        foreach (var (tag, entity) in SystemAPI.Query<RefRO<DestroyEntityTag>>().WithEntityAccess())
        {
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
#endregion







