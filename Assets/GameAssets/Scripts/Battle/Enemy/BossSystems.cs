using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitTargetingSystem))]
public partial struct BossCombatSystem : ISystem
{
    private ComponentLookup<LocalTransform> _transformLookup;
    private ComponentLookup<VisualAnimationState> _animStateLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        _animStateLookup = state.GetComponentLookup<VisualAnimationState>(false);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _transformLookup.Update(ref state);
        _animStateLookup.Update(ref state);
        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        // 보스의 CEnemyData, BossCombatData, BossAttackPrefabs, LocalTransform, TargetingData 모두 보유한다고 가정
        foreach (var (bossData, bossPrefabs, enemyData, targetingData, transform, velocity, entity) in
                 SystemAPI.Query<RefRW<BossCombatData>, RefRO<BossAttackPrefabs>, RefRW<CEnemyData>, RefRO<TargetingData>, RefRW<LocalTransform>, RefRW<Unity.Physics.PhysicsVelocity>>()
                 .WithAll<BossTag>()
                 .WithEntityAccess())
        {
            if (entity.Index < 0) continue;

            // --- 보스 사망 처리 및 속성 초월 이벤트 ---
            if (SystemAPI.HasComponent<DeathTag>(entity))
            {
                if (enemyData.ValueRW.CurrentState != EnemyState.Move)
                {
                    enemyData.ValueRW.CurrentState = EnemyState.Move;
                    bossData.ValueRW.StateTimer = 0f;
                    bossData.ValueRW.IsAttacking = false;
                    bossData.ValueRW.DashTimer = 0f;  // DeathTimer 용도로 재사용
                    
                    bossData.ValueRW.IsDashingPhase = false;
                    velocity.ValueRW.Linear = new float3(0, velocity.ValueRO.Linear.y, 0); // 보스 이동 완전 정지
                }

                if (bossData.ValueRO.DashTimer >= 0f)
                {
                    bossData.ValueRW.DashTimer += deltaTime;
                    // 보스 사망 애니메이션 종료(2초) 후 초월 이벤트 발생
                    if (bossData.ValueRO.DashTimer >= 2.0f)
                    {
                        bossData.ValueRW.DashTimer = -1f; // 이벤트 중복 방지
                        
                        if (!SystemAPI.HasComponent<IsolatedBossTag>(entity))
                        {
                            Entity ascTag = ecb.CreateEntity();
                            ecb.AddComponent<ElementAscensionEventTag>(ascTag);
                        }
                    }
                }
                continue; // 아래 어택 로직 스킵
            }

            if (enemyData.ValueRO.CurrentState != EnemyState.Attack && enemyData.ValueRO.CurrentState != EnemyState.Move) continue;

            if (!bossData.ValueRO.IsAttacking)
            {
                bossData.ValueRW.StateTimer -= deltaTime;
            }

            // 3초에 한 번씩 패턴 랜덤 설정 (공격 중이 아닐 때만 전환)
            if (bossData.ValueRO.StateTimer <= 0f && !bossData.ValueRO.IsAttacking)
            {
                bossData.ValueRW.StateTimer = 3.0f; // 3초 타이머

                Entity target = targetingData.ValueRO.CurrentTarget;
                if (target == Entity.Null || !_transformLookup.HasComponent(target)) continue;

                float3 myPos = transform.ValueRO.Position;
                float3 targetPos = _transformLookup[target].Position;
                float distSq = math.distancesq(myPos, targetPos);

                // 패턴 결정을 위한 간단한 난수 생성
                uint seed = (uint)(entity.Index + (int)(SystemAPI.Time.ElapsedTime * 1000));
                Unity.Mathematics.Random rng = new Unity.Mathematics.Random(seed == 0 ? 1 : seed);

                // 패턴 결정
                BossAttackPattern nextPattern;
                float attackRangeSq = enemyData.ValueRO.AttackRange * enemyData.ValueRO.AttackRange;

                if (distSq <= attackRangeSq)
                {
                    nextPattern = rng.NextBool() ? BossAttackPattern.Melee : BossAttackPattern.Dash;
                }
                else
                {
                    nextPattern = rng.NextBool() ? BossAttackPattern.AxeThrow : BossAttackPattern.Dash;
                }

                bossData.ValueRW.CurrentPattern = nextPattern;
                bossData.ValueRW.IsAttacking = true;
                bossData.ValueRW.AttackDelayTimer = 0f;
                bossData.ValueRW.PendingTargetPosition = targetPos;
                bossData.ValueRW.DashDirection = math.normalize(targetPos - myPos);

                // 회전 (타겟 바라보기)
                if (math.lengthsq(bossData.ValueRO.DashDirection) > 0.001f)
                    transform.ValueRW.Rotation = quaternion.LookRotationSafe(bossData.ValueRO.DashDirection, math.up());

                // EnemyData의 IsAttacking에 연동하여 기존 Move 통제. 이동 정지.
                enemyData.ValueRW.IsAttacking = true;
                
                // 돌진, 도끼 투척, 근접 등 공격으로 결정된 시점에서 바로 이동을 멈춥니다.
                velocity.ValueRW.Linear = new float3(0, velocity.ValueRO.Linear.y, 0); 

                // 애니메이션 연동 (트리거 발생 -> VisualManager에서 Telegraph 생성을 자동으로 유발함)
                if (_animStateLookup.HasComponent(entity))
                {
                    var anim = _animStateLookup[entity];
                    anim.TriggerAttack = true;
                    anim.EventAttackHit = false; // 잔여 프레임의 이벤트 소비
                    anim.EventAttackEnd = false; // 잔여 프레임의 이벤트 소비
                    if (nextPattern == BossAttackPattern.Melee) anim.AttackIndex = 0;
                    else if (nextPattern == BossAttackPattern.Dash) anim.AttackIndex = 1;
                    else if (nextPattern == BossAttackPattern.AxeThrow) anim.AttackIndex = 2;
                    _animStateLookup[entity] = anim;
                }
            }

            // 선딜레이 및 공격 진행
            if (bossData.ValueRO.IsAttacking)
            {
                bossData.ValueRW.AttackDelayTimer += deltaTime;
                bool isAttackHitTriggered = false;

                if (_animStateLookup.HasComponent(entity))
                {
                    var anim = _animStateLookup[entity];
                    if (anim.EventAttackHit)
                    {
                        isAttackHitTriggered = true;
                        anim.EventAttackHit = false; // 소비
                    }
                    if (anim.EventAttackEnd)
                    {
                        bossData.ValueRW.IsAttacking = false;
                        enemyData.ValueRW.IsAttacking = false;
                        anim.EventAttackEnd = false; // 소비

                        // 돌진 히트박스 파괴 및 정지 (OnAttackEnd 시점)
                        bossData.ValueRW.IsDashingPhase = false;

                        // 보스 이동 완전 정지 (돌진 끝)
                        if (bossData.ValueRO.CurrentPattern == BossAttackPattern.Dash)
                        {
                            velocity.ValueRW.Linear = new float3(0, velocity.ValueRO.Linear.y, 0);
                        }
                    }
                    _animStateLookup[entity] = anim;
                }

                // 애니메이션 누락 혹은 프리징 대비 페일세이프 (6초 뒤 자동 해제)
                if (bossData.ValueRO.AttackDelayTimer >= 6.0f)
                {
                    bossData.ValueRW.IsAttacking = false;
                    enemyData.ValueRW.IsAttacking = false;

                    bossData.ValueRW.IsDashingPhase = false;
                    velocity.ValueRW.Linear = new float3(0, velocity.ValueRO.Linear.y, 0);
                }

                // 도끼 투척, 근접 패턴 중에는 이동 완전 잠금 (돌진 중일 때는 제외)
                if (bossData.ValueRO.CurrentPattern != BossAttackPattern.Dash || !bossData.ValueRO.IsDashingPhase)
                {
                    velocity.ValueRW.Linear = new float3(0, velocity.ValueRO.Linear.y, 0);
                }

                // 이벤트 발생 (혹은 애니메이션 이벤트가 누락되었을 경우 0.8초 경과 시 강제 돌입)
                bool forceDash = (bossData.ValueRO.CurrentPattern == BossAttackPattern.Dash && bossData.ValueRO.AttackDelayTimer > 0.8f && !bossData.ValueRO.IsDashingPhase);

                if (isAttackHitTriggered || bossData.ValueRO.IsDashingPhase || forceDash)
                {
                    if (bossData.ValueRO.CurrentPattern == BossAttackPattern.Dash)
                    {
                        // 달리기 시작 시점에 히트박스 생성 (OnAttackHit에 생성, OnAttackEnd까지 유지)
                        if ((isAttackHitTriggered || forceDash) && !bossData.ValueRO.IsDashingPhase)
                        {
                            Entity dashHit = ecb.Instantiate(bossPrefabs.ValueRO.DashHitBoxPrefab);
                            float3 bPos = transform.ValueRO.Position;
                            bPos.y = 0.5f;

                            ecb.SetComponent(dashHit, new LocalTransform
                            {
                                Position = bPos,
                                Rotation = transform.ValueRO.Rotation,
                                Scale = 1f
                            });

                            if (SystemAPI.HasComponent<HitBoxData>(bossPrefabs.ValueRO.DashHitBoxPrefab))
                            {
                                var hitboxData = SystemAPI.GetComponent<HitBoxData>(bossPrefabs.ValueRO.DashHitBoxPrefab);
                                hitboxData.Damage = enemyData.ValueRO.AttackPower > 0f ? enemyData.ValueRO.AttackPower : hitboxData.Damage;
                                hitboxData.TargetFaction = 1;
                                hitboxData.IsPiercing = true; // 통과
                                hitboxData.MaxPierceCount = 50; // 다수 타격 가능
                                hitboxData.TickRate = 0f; // 한 대상 1회 타격
                                hitboxData.Duration = 10f; // 넉넉히 주고 OnAttackEnd에서 수동 파괴
                                ecb.SetComponent(dashHit, hitboxData);
                            }
                            bossData.ValueRW.IsDashingPhase = true;
                            ecb.AddComponent(dashHit, new Unity.Transforms.Parent { Value = entity });
                            ecb.AddComponent<BossDashHitBoxTag>(dashHit);
                        }

                        // OnAttackHit 이후 돌진 이동 재개 (Telegraph는 VisualManager에서 파괴됨)
                        velocity.ValueRW.Linear = new float3(
                            bossData.ValueRO.DashDirection.x * bossData.ValueRO.DashSpeed, 
                            velocity.ValueRO.Linear.y, 
                            bossData.ValueRO.DashDirection.z * bossData.ValueRO.DashSpeed
                        );
                    }
                    else if (bossData.ValueRO.CurrentPattern == BossAttackPattern.Melee && isAttackHitTriggered)
                    {
                        // 1회성 근접 타격
                        Entity hitbox = ecb.Instantiate(bossPrefabs.ValueRO.MeleeHitBoxPrefab);
                        float3 meleePos = bossData.ValueRO.PendingTargetPosition;
                        meleePos.y = 0.5f;

                        ecb.SetComponent(hitbox, new LocalTransform
                        {
                            Position = meleePos,
                            Scale = 1f,
                            Rotation = quaternion.identity
                        });

                        if (SystemAPI.HasComponent<HitBoxData>(bossPrefabs.ValueRO.MeleeHitBoxPrefab))
                        {
                            var hitboxData = SystemAPI.GetComponent<HitBoxData>(bossPrefabs.ValueRO.MeleeHitBoxPrefab);
                            hitboxData.Damage = enemyData.ValueRO.AttackPower > 0f ? enemyData.ValueRO.AttackPower : hitboxData.Damage;
                            hitboxData.TargetFaction = 1; // 플레이어 공격 목표로 설정
                            ecb.SetComponent(hitbox, hitboxData);
                        }
                    }
                    else if (bossData.ValueRO.CurrentPattern == BossAttackPattern.AxeThrow && isAttackHitTriggered)
                    {
                        // 도끼 투척 - 타겟 방향
                        Entity projectile = ecb.Instantiate(bossPrefabs.ValueRO.AxeHitBoxPrefab);
                        float3 axeStartPos = transform.ValueRO.Position;
                        axeStartPos.y = 0.5f;

                        ecb.SetComponent(projectile, new LocalTransform
                        {
                            Position = axeStartPos,
                            Scale = 1f,
                            Rotation = math.mul(quaternion.LookRotationSafe(bossData.ValueRO.DashDirection, math.up()), quaternion.Euler(0f, 0f, math.radians(90f)))
                        });

                        ecb.SetComponent(projectile, new ProjectileData
                        {
                            Direction = bossData.ValueRO.DashDirection,
                            Speed = 15f,
                            MaxDistance = 20f,
                            TravelledDistance = 0f
                        });

                        if (SystemAPI.HasComponent<HitBoxData>(bossPrefabs.ValueRO.AxeHitBoxPrefab))
                        {
                            var hitboxData = SystemAPI.GetComponent<HitBoxData>(bossPrefabs.ValueRO.AxeHitBoxPrefab);
                            hitboxData.Damage = enemyData.ValueRO.AttackPower > 0f ? enemyData.ValueRO.AttackPower : hitboxData.Damage;
                            hitboxData.TargetFaction = 1; // 플레이어 공격 목표로 설정
                            ecb.SetComponent(projectile, hitboxData);
                        }
                        
                        // 도끼 회전 데이터 추가 (X축, Y축 등을 혼합하여 누워서 도는 대신 똑바로 팽이치듯 회전하도록)
                        ecb.AddComponent(projectile, new SpinningProjectileData {
                            SpinSpeed = 15f,
                            SpinAxis = new float3(1, 0, 0) // 투척 무기처럼 앞으로 회전 (Pitch)
                        });

                        ecb.AddComponent<Unity.Rendering.DisableRendering>(projectile);
                    }
                }
            }
        }
        
        // DashHitBoxCleanup
        foreach (var (parent, e) in SystemAPI.Query<RefRO<Unity.Transforms.Parent>>().WithAll<BossDashHitBoxTag>().WithEntityAccess())
        {
            if (SystemAPI.HasComponent<BossCombatData>(parent.ValueRO.Value))
            {
                var bd = SystemAPI.GetComponent<BossCombatData>(parent.ValueRO.Value);
                if (!bd.IsAttacking || bd.CurrentPattern != BossAttackPattern.Dash || !bd.IsDashingPhase)
                {
                    ecb.AddComponent<DestroyEntityTag>(e);
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
