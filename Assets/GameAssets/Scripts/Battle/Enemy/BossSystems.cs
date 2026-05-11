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

        foreach (var (bossData, bossPrefabs, enemyData, targetingData, transform, velocity, entity) in
                 SystemAPI.Query<RefRW<BossCombatData>, RefRO<BossAttackPrefabs>, RefRW<CEnemyData>, RefRO<TargetingData>, RefRW<LocalTransform>, RefRW<Unity.Physics.PhysicsVelocity>>()
                 .WithAll<BossTag>()
                 .WithEntityAccess())
        {
            if (entity.Index < 0) continue;

            // --- 보스 사망 처리 ---
            if (SystemAPI.HasComponent<DeathTag>(entity))
            {
                if (enemyData.ValueRW.CurrentState != EnemyState.Move)
                {
                    enemyData.ValueRW.CurrentState = EnemyState.Move;
                    bossData.ValueRW.DashTimer = 0f;  // DeathTimer 용도
                    velocity.ValueRW.Linear = new float3(0, velocity.ValueRO.Linear.y, 0); // 멈춤
                }

                if (bossData.ValueRO.DashTimer >= 0f)
                {
                    bossData.ValueRW.DashTimer += deltaTime;
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
                continue; // 상태 머신 스킵
            }

            if (enemyData.ValueRO.CurrentState != EnemyState.Attack && enemyData.ValueRO.CurrentState != EnemyState.Move) continue;

            Entity target = targetingData.ValueRO.CurrentTarget;
            bool hasTarget = target != Entity.Null && _transformLookup.HasComponent(target);
            float3 myPos = transform.ValueRO.Position;
            float3 targetPos = hasTarget ? _transformLookup[target].Position : myPos;
            float distSq = hasTarget ? math.distancesq(myPos, targetPos) : float.MaxValue;
            float attackRangeSq = enemyData.ValueRO.AttackRange * enemyData.ValueRO.AttackRange;

            // FSM (상태 머신)
            switch (bossData.ValueRO.CurrentState)
            {
                case BossState.Chasing:
                    enemyData.ValueRW.IsAttacking = false; // 일반 이동 허용
                    
                    // 패턴 시전 조건: 사거리 내에 들어왔거나 휴식 타이머가 끝난 경우
                    bossData.ValueRW.StateTimer -= deltaTime;
                    if (hasTarget && (distSq <= attackRangeSq || bossData.ValueRO.StateTimer <= 0f))
                    {
                        // Prep 상태 강제 전환 (멍때리지 않고 패턴 시작)
                        bossData.ValueRW.CurrentState = BossState.Prep;
                        bossData.ValueRW.StateTimer = 6.0f; // 최대 Prep 대기 (페일세이프용)
                        
                        enemyData.ValueRW.IsAttacking = true; // 이동 멈춤
                        velocity.ValueRW.Linear = new float3(0, velocity.ValueRO.Linear.y, 0);

                        // 타겟 방향 바라보고 좌표 고정 (Telegraph와 히트박스 일치 용도)
                        float3 dir = math.normalize(targetPos - myPos);
                        dir.y = 0;
                        bossData.ValueRW.DashDirection = dir;
                        transform.ValueRW.Rotation = quaternion.LookRotationSafe(dir, math.up());

                        // 전방 사거리 앞 계산
                        bossData.ValueRW.AttackPosition = myPos + dir * (enemyData.ValueRO.AttackRange * 0.5f);
                        bossData.ValueRW.AttackRotation = transform.ValueRO.Rotation;

                        // 패턴 난수 결정
                        uint seed = (uint)(entity.Index + (int)(SystemAPI.Time.ElapsedTime * 1000));
                        Unity.Mathematics.Random rng = new Unity.Mathematics.Random(seed == 0 ? 1 : seed);
                        bossData.ValueRW.CurrentPattern = distSq <= attackRangeSq 
                            ? (rng.NextBool() ? BossAttackPattern.Melee : BossAttackPattern.Dash) 
                            : (rng.NextBool() ? BossAttackPattern.AxeThrow : BossAttackPattern.Dash);

                        // 애니메이션 트리거 (이 시점에 Telegraph가 그려짐)
                        if (_animStateLookup.HasComponent(entity))
                        {
                            var anim = _animStateLookup[entity];
                            anim.TriggerAttack = true;
                            anim.EventAttackHit = false;
                            anim.EventAttackEnd = false;
                            if (bossData.ValueRO.CurrentPattern == BossAttackPattern.Melee) anim.AttackIndex = 0;
                            else if (bossData.ValueRO.CurrentPattern == BossAttackPattern.Dash) anim.AttackIndex = 1;
                            else if (bossData.ValueRO.CurrentPattern == BossAttackPattern.AxeThrow) anim.AttackIndex = 2;
                            _animStateLookup[entity] = anim;
                        }
                    }
                    break;

                case BossState.Prep:
                    bossData.ValueRW.StateTimer -= deltaTime;
                    velocity.ValueRW.Linear = new float3(0, velocity.ValueRO.Linear.y, 0); // 멈춤 유지

                    if (_animStateLookup.HasComponent(entity))
                    {
                        var anim = _animStateLookup[entity];
                        // Prep 중 Hit 이벤트 받으면 바로 공격(Hitting)
                        if (anim.EventAttackHit)
                        {
                            anim.EventAttackHit = false;
                            _animStateLookup[entity] = anim;
                            
                            bossData.ValueRW.CurrentState = BossState.Hitting;
                            bossData.ValueRW.StateTimer = 5.0f; // 타격 유지 제한시간

                            // [실제 히트박스 생성, Prep때 정해둔 위치/회전 그대로 사용]
                            if (bossData.ValueRO.CurrentPattern == BossAttackPattern.Dash)
                            {
                                Entity dashHit = ecb.Instantiate(bossPrefabs.ValueRO.DashHitBoxPrefab);
                                float3 bPos = transform.ValueRO.Position; bPos.y = 0.5f;
                                ecb.SetComponent(dashHit, new LocalTransform { Position = bPos, Rotation = transform.ValueRO.Rotation, Scale = 1f });

                                if (SystemAPI.HasComponent<HitBoxData>(bossPrefabs.ValueRO.DashHitBoxPrefab))
                                {
                                    var hitboxData = SystemAPI.GetComponent<HitBoxData>(bossPrefabs.ValueRO.DashHitBoxPrefab);
                                    hitboxData.Damage = enemyData.ValueRO.AttackPower > 0f ? enemyData.ValueRO.AttackPower : hitboxData.Damage;
                                    hitboxData.TargetFaction = 1;
                                    hitboxData.IsPiercing = true;
                                    hitboxData.MaxPierceCount = 50;
                                    hitboxData.Duration = 10f; // 넉넉히
                                    ecb.SetComponent(dashHit, hitboxData);
                                }
                                ecb.AddComponent(dashHit, new Unity.Transforms.Parent { Value = entity });
                                ecb.AddComponent<BossDashHitBoxTag>(dashHit);
                                // 돌진 공격 이펙트 수동 바인딩
                                ecb.AddComponent(dashHit, new EffectVisualInfo { ID = 330100003 });
                            }
                            else if (bossData.ValueRO.CurrentPattern == BossAttackPattern.Melee)
                            {
                                Entity hitbox = ecb.Instantiate(bossPrefabs.ValueRO.MeleeHitBoxPrefab);
                                float3 meleePos = transform.ValueRO.Position; meleePos.y = 0.5f;
                                ecb.SetComponent(hitbox, new LocalTransform { Position = meleePos, Rotation = bossData.ValueRO.AttackRotation, Scale = 1f });
                                
                                if (SystemAPI.HasComponent<HitBoxData>(bossPrefabs.ValueRO.MeleeHitBoxPrefab))
                                {
                                    var hitboxData = SystemAPI.GetComponent<HitBoxData>(bossPrefabs.ValueRO.MeleeHitBoxPrefab);
                                    hitboxData.Damage = enemyData.ValueRO.AttackPower > 0f ? enemyData.ValueRO.AttackPower : hitboxData.Damage;
                                    hitboxData.TargetFaction = 1;
                                    ecb.SetComponent(hitbox, hitboxData);
                                }
                                // 기본 근접 공격 이펙트 수동 바인딩
                                ecb.AddComponent(hitbox, new EffectVisualInfo { ID = 330100001 });
                            }
                            else if (bossData.ValueRO.CurrentPattern == BossAttackPattern.AxeThrow)
                            {
                                Entity hitbox = ecb.Instantiate(bossPrefabs.ValueRO.AxeHitBoxPrefab);
                                float3 projPos = transform.ValueRO.Position; projPos.y = 1f;
                                ecb.SetComponent(hitbox, new LocalTransform { Position = projPos, Rotation = transform.ValueRO.Rotation, Scale = 1f });

                                if (SystemAPI.HasComponent<HitBoxData>(bossPrefabs.ValueRO.AxeHitBoxPrefab))
                                {
                                    var hitboxData = SystemAPI.GetComponent<HitBoxData>(bossPrefabs.ValueRO.AxeHitBoxPrefab);
                                    hitboxData.Damage = enemyData.ValueRO.AttackPower > 0f ? enemyData.ValueRO.AttackPower : hitboxData.Damage;
                                    hitboxData.TargetFaction = 1;
                                    ecb.SetComponent(hitbox, hitboxData);
                                }
                                
                                if (SystemAPI.HasComponent<ProjectileData>(bossPrefabs.ValueRO.AxeHitBoxPrefab))
                                {
                                    var projData = SystemAPI.GetComponent<ProjectileData>(bossPrefabs.ValueRO.AxeHitBoxPrefab);
                                    projData.Direction = bossData.ValueRO.DashDirection;
                                    projData.MaxDistance = enemyData.ValueRO.AttackRange * 5f; // 보스의 경우 사거리를 넉넉하게 스케일링하거나 그대로 사용
                                    ecb.SetComponent(hitbox, projData);
                                }
                                else
                                {
                                    ecb.AddComponent(hitbox, new ProjectileData { Direction = bossData.ValueRO.DashDirection, Speed = 10f, MaxDistance = enemyData.ValueRO.AttackRange * 5f });
                                }
                                // 도끼 던지기 이펙트 수동 바인딩
                                ecb.AddComponent(hitbox, new EffectVisualInfo { ID = 330100002 });
                            }
                            break;
                        }
                    }
                    
                    // 페일세이프 (애니메이션 꼬임 방지)
                    if (bossData.ValueRO.StateTimer <= 0f) bossData.ValueRW.CurrentState = BossState.Cooldown;
                    break;

                case BossState.Hitting:
                    bossData.ValueRW.StateTimer -= deltaTime;
                    
                    // 대시는 물리 이동 수행, 그 외엔 정지
                    if (bossData.ValueRO.CurrentPattern == BossAttackPattern.Dash)
                    {
                        velocity.ValueRW.Linear = new float3(
                            bossData.ValueRO.DashDirection.x * bossData.ValueRO.DashSpeed, 
                            velocity.ValueRO.Linear.y, 
                            bossData.ValueRO.DashDirection.z * bossData.ValueRO.DashSpeed
                        );
                    }
                    else
                    {
                        velocity.ValueRW.Linear = new float3(0, velocity.ValueRO.Linear.y, 0);
                    }

                    if (_animStateLookup.HasComponent(entity))
                    {
                        var anim = _animStateLookup[entity];
                        if (anim.EventAttackEnd || bossData.ValueRO.StateTimer <= 0f)
                        {
                            anim.EventAttackEnd = false; // 소비
                            _animStateLookup[entity] = anim;

                            // 쿨다운으로 전환
                            bossData.ValueRW.CurrentState = BossState.Cooldown;
                            bossData.ValueRW.StateTimer = 1.0f; // 공격 직후 짧은 쿨타임
                            enemyData.ValueRW.IsAttacking = false;
                        }
                    }
                    break;

                case BossState.Cooldown:
                    bossData.ValueRW.StateTimer -= deltaTime;
                    velocity.ValueRW.Linear = new float3(0, velocity.ValueRO.Linear.y, 0); // 잠깐 멈칫
                    
                    if (bossData.ValueRO.StateTimer <= 0.0f)
                    {
                        bossData.ValueRW.CurrentState = BossState.Chasing;
                        bossData.ValueRW.StateTimer = bossData.ValueRO.AttackCooldown; // 최대 멍때림 방지 타이머 재설정
                    }
                    break;
            }
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
