using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
public partial struct BossCombatSystem : ISystem
{
    private EntityQuery _bossQuery;
    private EntityQuery _databaseQuery;
    private EntityQuery _playerQuery;
    private ComponentLookup<LocalTransform> _transformLookup;
    private ComponentLookup<VisualAnimationState> _animationLookup;
    private ComponentLookup<DeathTag> _deathLookup;
    private ComponentLookup<IsolatedBossTag> _isolatedBossLookup;
    private ComponentLookup<HitBoxData> _hitBoxLookup;
    private ComponentLookup<ProjectileData> _projectileLookup;
    private BufferLookup<BossSkillPrefabElement> _skillPrefabLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _bossQuery = SystemAPI.QueryBuilder()
            .WithAllRW<BossCombatData>()
            .WithAllRW<CEnemyData>()
            .WithAllRW<LocalTransform>()
            .WithAll<HealthData, TargetingData, BossAuthoringConfig, BossTag>()
            .Build();
        _databaseQuery = SystemAPI.QueryBuilder().WithAll<BossPatternDatabaseComponent>().Build();
        _playerQuery = SystemAPI.QueryBuilder().WithAll<PlayerData, LocalTransform>().Build();
        _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        _animationLookup = state.GetComponentLookup<VisualAnimationState>(false);
        _deathLookup = state.GetComponentLookup<DeathTag>(true);
        _isolatedBossLookup = state.GetComponentLookup<IsolatedBossTag>(true);
        _hitBoxLookup = state.GetComponentLookup<HitBoxData>(true);
        _projectileLookup = state.GetComponentLookup<ProjectileData>(true);
        _skillPrefabLookup = state.GetBufferLookup<BossSkillPrefabElement>(true);
        state.RequireForUpdate(_databaseQuery);
        state.RequireForUpdate(_bossQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.EntityManager.CompleteDependencyBeforeRW<LocalTransform>();

        _transformLookup.Update(ref state);
        _animationLookup.Update(ref state);
        _deathLookup.Update(ref state);
        _isolatedBossLookup.Update(ref state);
        _hitBoxLookup.Update(ref state);
        _projectileLookup.Update(ref state);
        _skillPrefabLookup.Update(ref state);

        float deltaTime = SystemAPI.Time.DeltaTime;
        BossPatternDatabaseComponent database = _databaseQuery.GetSingleton<BossPatternDatabaseComponent>();
        var entities = _bossQuery.ToEntityArray(Allocator.Temp);
        var bossLookup = SystemAPI.GetComponentLookup<BossCombatData>();
        var enemyLookup = SystemAPI.GetComponentLookup<CEnemyData>();
        var healthLookup = SystemAPI.GetComponentLookup<HealthData>(true);
        var targetingLookup = SystemAPI.GetComponentLookup<TargetingData>(true);
        var configLookup = SystemAPI.GetComponentLookup<BossAuthoringConfig>(true);
        var transformWriteLookup = SystemAPI.GetComponentLookup<LocalTransform>();
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        float3 playerPosition = float3.zero;
        bool hasPlayer = TryGetPlayerPosition(out playerPosition);

        foreach (Entity entity in entities)
        {
            BossCombatData boss = bossLookup[entity];
            CEnemyData enemy = enemyLookup[entity];
            LocalTransform transform = transformWriteLookup[entity];
            HealthData health = healthLookup[entity];
            TargetingData targeting = targetingLookup[entity];
            BossAuthoringConfig config = configLookup[entity];

            if (HandleDeath(entity, deltaTime, ref boss, ref enemy, ref ecb))
            {
                bossLookup[entity] = boss;
                enemyLookup[entity] = enemy;
                continue;
            }

            if (boss.BaseAttackPower <= 0f)
            {
                boss.BaseAttackPower = enemy.AttackPower;
                boss.BaseMoveSpeed = enemy.MoveSpeed;
            }

            TickCooldowns(deltaTime, ref boss);
            ref BossPatternDatabaseBlob root = ref database.DatabaseRef.Value;
            BossPatternDefBlob pattern = FindCurrentPattern(ref root, enemy.ID, health);

            if (pattern.Phase != 0 && pattern.Phase != boss.CurrentPhase)
            {
                boss.CurrentPhase = pattern.Phase;
                boss.PendingStartSkillID = pattern.StartSkillID;
            }

            if (boss.PendingStartSkillID != 0)
            {
                ApplyPassiveSkill(ref root, boss.PendingStartSkillID, ref boss, ref enemy);
                boss.PendingStartSkillID = 0;
                boss.CurrentState = BossState.Enraging;
                boss.StateTimer = config.EnrageDuration;
                enemy.IsAttacking = true;
                TriggerEnrage(entity);
            }

            if (boss.CurrentState == BossState.Enraging)
            {
                boss.StateTimer -= deltaTime;
                if (boss.StateTimer <= 0f)
                {
                    boss.CurrentState = BossState.Chasing;
                    enemy.IsAttacking = false;
                }

                bossLookup[entity] = boss;
                enemyLookup[entity] = enemy;
                continue;
            }

            Entity targetEntity = targeting.CurrentTarget;
            bool hasTarget = targetEntity != Entity.Null && _transformLookup.HasComponent(targetEntity);
            float3 targetPosition = hasTarget ? _transformLookup[targetEntity].Position : transform.Position;

            ProcessState(entity, ref root, pattern, config, hasPlayer, playerPosition, hasTarget,
                targetPosition, deltaTime, ref boss, ref enemy, ref transform, ref ecb);

            bossLookup[entity] = boss;
            enemyLookup[entity] = enemy;
            transformWriteLookup[entity] = transform;
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        entities.Dispose();
    }

    private bool TryGetPlayerPosition(out float3 position)
    {
        var players = _playerQuery.ToEntityArray(Allocator.Temp);
        bool found = players.Length > 0 && _transformLookup.HasComponent(players[0]);
        position = found ? _transformLookup[players[0]].Position : float3.zero;
        players.Dispose();
        return found;
    }

    private bool HandleDeath(Entity entity, float deltaTime, ref BossCombatData boss, ref CEnemyData enemy, ref EntityCommandBuffer ecb)
    {
        if (!_deathLookup.HasComponent(entity)) return false;

        boss.CurrentState = BossState.Cooldown;
        boss.DashRemainingDistance = 0f;

        if (enemy.CurrentState != EnemyState.Chase)
        {
            enemy.CurrentState = EnemyState.Chase;
            boss.DashTimer = 0f;
        }

        if (boss.DashTimer >= 0f)
        {
            boss.DashTimer += deltaTime;
            if (boss.DashTimer >= 2f)
            {
                boss.DashTimer = -1f;
                if (!_isolatedBossLookup.HasComponent(entity))
                {
                    Entity eventEntity = ecb.CreateEntity();
                    ecb.AddComponent<ElementAscensionEventTag>(eventEntity);
                }
            }
        }

        return true;
    }

    private void ProcessState(Entity entity, ref BossPatternDatabaseBlob root, in BossPatternDefBlob pattern,
        in BossAuthoringConfig config, bool hasPlayer, float3 playerPosition, bool hasTarget,
        float3 targetPosition, float deltaTime, ref BossCombatData boss, ref CEnemyData enemy,
        ref LocalTransform transform, ref EntityCommandBuffer ecb)
    {
        switch (boss.CurrentState)
        {
            case BossState.Chasing:
                enemy.IsAttacking = false;
                if (!hasTarget || pattern.Phase == 0) return;

                if (TrySelectSkill(ref root, pattern, config, transform.Position, targetPosition, ref boss, out BossActiveSkillDefBlob skill))
                {
                    BossSkillExecutionType executionType = FindExecutionType(entity, skill.SkillID);
                    float3 selectedTarget = executionType == BossSkillExecutionType.Charge
                        ? targetPosition
                        : skill.Target == BossSkillTarget.Player && hasPlayer ? playerPosition : targetPosition;
                    BeginSkill(entity, skill, config, selectedTarget, ref boss, ref enemy, ref transform);
                }
                break;

            case BossState.Prep:
                boss.StateTimer -= deltaTime;
                if (TryConsumeAttackHit(entity) || boss.StateTimer <= 0f)
                {
                    if (TryFindActiveSkill(ref root, boss.CurrentSkillID, out BossActiveSkillDefBlob activeSkill))
                    {
                        SpawnSkill(entity, activeSkill, config, enemy, boss, transform, ref ecb);
                        SetCooldown(activeSkill, ref boss);

                        BossSkillExecutionType executionType = FindExecutionType(entity, activeSkill.SkillID);
                        if (executionType == BossSkillExecutionType.Charge)
                        {
                            boss.CurrentState = BossState.Charging;
                            boss.StateTimer = boss.DashSpeed > 0f
                                ? (boss.DashRemainingDistance / boss.DashSpeed) + 0.5f
                                : 0.5f;
                        }
                        else
                        {
                            boss.CurrentState = BossState.Hitting;
                            boss.StateTimer = math.max(0.1f, activeSkill.GroggyDuration);
                        }
                    }
                    else FinishSkill(ref boss, ref enemy);
                }
                break;

            case BossState.Hitting:
                boss.StateTimer -= deltaTime;
                if (TryConsumeAttackEnd(entity) || boss.StateTimer <= 0f)
                {
                    boss.CurrentState = BossState.Cooldown;
                    boss.StateTimer = 0.2f;
                    enemy.IsAttacking = false;
                }
                break;

            case BossState.Charging:
                boss.StateTimer -= deltaTime;
                float moveDistance = math.min(boss.DashSpeed * deltaTime, boss.DashRemainingDistance);
                transform.Position += boss.DashDirection * moveDistance;
                boss.DashRemainingDistance -= moveDistance;

                if (boss.DashRemainingDistance <= 0f || boss.StateTimer <= 0f)
                {
                    boss.CurrentState = BossState.Cooldown;
                    boss.StateTimer = TryFindActiveSkill(ref root, boss.CurrentSkillID, out BossActiveSkillDefBlob chargeSkill)
                        ? math.max(0.1f, chargeSkill.GroggyDuration)
                        : 0.2f;
                    enemy.IsAttacking = true;
                }
                break;

            case BossState.Cooldown:
                boss.StateTimer -= deltaTime;
                if (boss.StateTimer <= 0f) FinishSkill(ref boss, ref enemy);
                break;
        }
    }

    private void BeginSkill(Entity entity, in BossActiveSkillDefBlob skill, in BossAuthoringConfig config,
        float3 targetPosition, ref BossCombatData boss, ref CEnemyData enemy, ref LocalTransform transform)
    {
        float3 direction = targetPosition - transform.Position;
        direction.y = 0f;
        direction = math.normalizesafe(direction, math.forward());

        boss.CurrentSkillID = skill.SkillID;
        boss.CurrentState = BossState.Prep;
        boss.StateTimer = 6f;
        boss.DashDirection = direction;
        BossSkillExecutionType executionType = FindExecutionType(entity, skill.SkillID);
        boss.DashRemainingDistance = executionType == BossSkillExecutionType.Charge
            ? config.SizeReference * skill.RangeRate
            : 0f;
        boss.AttackPosition = transform.Position;
        boss.AttackRotation = quaternion.LookRotationSafe(direction, math.up());
        boss.CurrentPattern = executionType == BossSkillExecutionType.Charge
            ? BossAttackPattern.Dash
            : skill.Shape == HitBoxShape.Cone ? BossAttackPattern.Melee : BossAttackPattern.AxeThrow;
        enemy.IsAttacking = true;
        transform.Rotation = boss.AttackRotation;

        if (_animationLookup.HasComponent(entity))
        {
            VisualAnimationState animation = _animationLookup[entity];
            animation.TriggerAttack = true;
            animation.EventAttackHit = false;
            animation.EventAttackEnd = false;
            animation.AttackIndex = FindAnimationIndex(entity, skill.SkillID);
            animation.TelegraphShape = skill.Shape;
            animation.TelegraphRange = config.SizeReference * skill.RangeRate;
            animation.TelegraphWidth = config.SizeReference * config.BoxWidthRate;
            animation.TelegraphAngle = config.ConeAngle;
            animation.TelegraphRotation = boss.AttackRotation;
            _animationLookup[entity] = animation;
        }
    }

    private void SpawnSkill(Entity owner, in BossActiveSkillDefBlob skill, in BossAuthoringConfig config,
        in CEnemyData enemy, in BossCombatData boss, in LocalTransform transform, ref EntityCommandBuffer ecb)
    {
        if (!TryFindSkillBinding(owner, skill.SkillID, out BossSkillPrefabElement binding) || binding.Prefab == Entity.Null) return;

        Entity hitbox = ecb.Instantiate(binding.Prefab);
        float range = config.SizeReference * skill.RangeRate;
        float3 spawnPosition = transform.Position;
        bool isProjectile = binding.ExecutionType == BossSkillExecutionType.Projectile;
        bool isCharge = binding.ExecutionType == BossSkillExecutionType.Charge;
        spawnPosition.y = isProjectile ? 1f : 0.5f;
        if (skill.Shape == HitBoxShape.Box && !isCharge) spawnPosition += boss.DashDirection * (range * 0.5f);

        ecb.SetComponent(hitbox, LocalTransform.FromPositionRotationScale(spawnPosition, boss.AttackRotation, 1f));

        HitBoxData hitboxData = _hitBoxLookup.HasComponent(binding.Prefab)
            ? _hitBoxLookup[binding.Prefab]
            : default;
        hitboxData.Shape = isCharge ? HitBoxShape.Circle : skill.Shape;
        hitboxData.Damage = enemy.AttackPower * skill.AttackRate;
        hitboxData.TargetFaction = 1;
        hitboxData.Radius = isCharge ? config.BodyRadius : range;
        hitboxData.Angle = config.ConeAngle;
        hitboxData.BoxExtents = new float3(config.SizeReference * config.BoxWidthRate * 0.5f, 1f, range * 0.5f);
        if (isCharge)
        {
            hitboxData.Duration = boss.DashSpeed > 0f ? (range / boss.DashSpeed) + 1f : 1f;
            hitboxData.IsPiercing = true;
            hitboxData.MaxPierceCount = 0;
            hitboxData.CurrentPierceCount = 0;
            hitboxData.TickRate = 0f;
        }
        ecb.SetComponent(hitbox, hitboxData);

        if (isProjectile)
        {
            bool hasProjectile = _projectileLookup.HasComponent(binding.Prefab);
            ProjectileData projectile = hasProjectile
                ? _projectileLookup[binding.Prefab]
                : new ProjectileData { Speed = 10f };
            projectile.Direction = boss.DashDirection;
            projectile.MaxDistance = range;
            projectile.TravelledDistance = 0f;
            if (hasProjectile) ecb.SetComponent(hitbox, projectile);
            else ecb.AddComponent(hitbox, projectile);
        }
        else if (isCharge)
        {
            ecb.AddComponent(hitbox, new BossDashHitBoxTag { Owner = owner });
        }
    }

    private static BossPatternDefBlob FindCurrentPattern(ref BossPatternDatabaseBlob root, int bossID, in HealthData health)
    {
        BossPatternDefBlob selected = default;
        float healthRate = health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth * 100f : 0f;
        for (int i = 0; i < root.Patterns.Length; i++)
        {
            ref BossPatternDefBlob candidate = ref root.Patterns[i];
            if (candidate.BossID != bossID || healthRate > candidate.HealthRate) continue;
            if (candidate.Phase > selected.Phase) selected = candidate;
        }
        return selected;
    }

    private static bool TrySelectSkill(ref BossPatternDatabaseBlob root, in BossPatternDefBlob pattern,
        in BossAuthoringConfig config, float3 bossPosition, float3 targetPosition,
        ref BossCombatData boss, out BossActiveSkillDefBlob selected)
    {
        selected = default;
        float distance = math.distance(new float2(bossPosition.x, bossPosition.z), new float2(targetPosition.x, targetPosition.z));
        float meleeRange = 0f;

        for (int slot = 0; slot < 4; slot++)
        {
            int skillID = GetPatternSkill(pattern, slot);
            if (skillID == 0 || !TryFindActiveSkill(ref root, skillID, out BossActiveSkillDefBlob skill)) continue;
            EnsureCooldown(skillID, ref boss);
            if (skill.Shape == HitBoxShape.Cone) meleeRange = math.max(meleeRange, config.SizeReference * skill.RangeRate);
            if (skill.IsForced && IsCooldownReady(skillID, boss))
            {
                selected = skill;
                return true;
            }
        }

        for (int slot = 0; slot < 4; slot++)
        {
            int skillID = GetPatternSkill(pattern, slot);
            if (skillID == 0 || !TryFindActiveSkill(ref root, skillID, out BossActiveSkillDefBlob skill)) continue;
            if (skill.IsForced || !IsCooldownReady(skillID, boss)) continue;
            float range = config.SizeReference * skill.RangeRate;
            if (skill.Shape == HitBoxShape.Cone && distance <= range)
            {
                selected = skill;
                return true;
            }
            if (skill.Shape != HitBoxShape.Cone && distance > meleeRange)
            {
                selected = skill;
                return true;
            }
        }
        return false;
    }

    private static int GetPatternSkill(in BossPatternDefBlob pattern, int index)
    {
        switch (index)
        {
            case 0: return pattern.Skill1;
            case 1: return pattern.Skill2;
            case 2: return pattern.Skill3;
            default: return pattern.Skill4;
        }
    }

    private static bool TryFindActiveSkill(ref BossPatternDatabaseBlob root, int skillID, out BossActiveSkillDefBlob skill)
    {
        for (int i = 0; i < root.ActiveSkills.Length; i++)
        {
            if (root.ActiveSkills[i].SkillID != skillID) continue;
            skill = root.ActiveSkills[i];
            return true;
        }
        skill = default;
        return false;
    }

    private static void ApplyPassiveSkill(ref BossPatternDatabaseBlob root, int skillID,
        ref BossCombatData boss, ref CEnemyData enemy)
    {
        for (int i = 0; i < root.PassiveEffects.Length; i++)
        {
            ref BossPassiveEffectDefBlob effect = ref root.PassiveEffects[i];
            if (effect.SkillID != skillID) continue;
            if (effect.Stat == BossPassiveStat.Attack) enemy.AttackPower = boss.BaseAttackPower * effect.BuffValue;
            else if (effect.Stat == BossPassiveStat.MoveSpeed) enemy.MoveSpeed = boss.BaseMoveSpeed * effect.BuffValue;
        }
        boss.IsEnraged = true;
    }

    private static void TickCooldowns(float deltaTime, ref BossCombatData boss)
    {
        for (int i = 0; i < boss.SkillCooldowns.Length; i++)
        {
            BossSkillCooldownState cooldown = boss.SkillCooldowns[i];
            cooldown.Remaining = math.max(0f, cooldown.Remaining - deltaTime);
            boss.SkillCooldowns[i] = cooldown;
        }
    }

    private static void EnsureCooldown(int skillID, ref BossCombatData boss)
    {
        for (int i = 0; i < boss.SkillCooldowns.Length; i++)
        {
            if (boss.SkillCooldowns[i].SkillID == skillID) return;
        }
        if (boss.SkillCooldowns.Length < boss.SkillCooldowns.Capacity)
            boss.SkillCooldowns.Add(new BossSkillCooldownState { SkillID = skillID, Remaining = 0f });
    }

    private static bool IsCooldownReady(int skillID, in BossCombatData boss)
    {
        for (int i = 0; i < boss.SkillCooldowns.Length; i++)
        {
            if (boss.SkillCooldowns[i].SkillID == skillID) return boss.SkillCooldowns[i].Remaining <= 0f;
        }
        return true;
    }

    private static void SetCooldown(in BossActiveSkillDefBlob skill, ref BossCombatData boss)
    {
        EnsureCooldown(skill.SkillID, ref boss);
        for (int i = 0; i < boss.SkillCooldowns.Length; i++)
        {
            BossSkillCooldownState cooldown = boss.SkillCooldowns[i];
            if (cooldown.SkillID != skill.SkillID) continue;
            cooldown.Remaining = skill.Cooldown;
            boss.SkillCooldowns[i] = cooldown;
            return;
        }
    }

    private bool TryFindSkillBinding(Entity entity, int skillID, out BossSkillPrefabElement binding)
    {
        if (_skillPrefabLookup.HasBuffer(entity))
        {
            DynamicBuffer<BossSkillPrefabElement> bindings = _skillPrefabLookup[entity];
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].SkillID != skillID) continue;
                binding = bindings[i];
                return true;
            }
        }
        binding = default;
        return false;
    }

    private int FindAnimationIndex(Entity entity, int skillID)
    {
        return TryFindSkillBinding(entity, skillID, out BossSkillPrefabElement binding) ? binding.AnimationIndex : 0;
    }

    private BossSkillExecutionType FindExecutionType(Entity entity, int skillID)
    {
        return TryFindSkillBinding(entity, skillID, out BossSkillPrefabElement binding)
            ? binding.ExecutionType
            : BossSkillExecutionType.HitBox;
    }

    private bool TryConsumeAttackHit(Entity entity)
    {
        if (!_animationLookup.HasComponent(entity)) return false;
        VisualAnimationState animation = _animationLookup[entity];
        if (!animation.EventAttackHit) return false;
        animation.EventAttackHit = false;
        _animationLookup[entity] = animation;
        return true;
    }

    private bool TryConsumeAttackEnd(Entity entity)
    {
        if (!_animationLookup.HasComponent(entity)) return false;
        VisualAnimationState animation = _animationLookup[entity];
        if (!animation.EventAttackEnd) return false;
        animation.EventAttackEnd = false;
        _animationLookup[entity] = animation;
        return true;
    }

    private void TriggerEnrage(Entity entity)
    {
        if (!_animationLookup.HasComponent(entity)) return;
        VisualAnimationState animation = _animationLookup[entity];
        animation.TriggerEnrage = true;
        animation.TriggerAttack = false;
        _animationLookup[entity] = animation;
    }

    private static void FinishSkill(ref BossCombatData boss, ref CEnemyData enemy)
    {
        boss.CurrentState = BossState.Chasing;
        boss.CurrentSkillID = 0;
        boss.DashRemainingDistance = 0f;
        enemy.IsAttacking = false;
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }
}

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(HitBoxCollisionSystem))]
public partial struct BossChargeHitBoxFollowSystem : ISystem
{
    private EntityQuery _chargeHitBoxQuery;
    private ComponentLookup<LocalToWorld> _ownerTransformLookup;
    private ComponentLookup<BossCombatData> _bossLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _chargeHitBoxQuery = SystemAPI.QueryBuilder()
            .WithAllRW<LocalTransform>()
            .WithAll<BossDashHitBoxTag>()
            .Build();
        _ownerTransformLookup = state.GetComponentLookup<LocalToWorld>(true);
        _bossLookup = state.GetComponentLookup<BossCombatData>(true);
        state.RequireForUpdate(_chargeHitBoxQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _ownerTransformLookup.Update(ref state);
        _bossLookup.Update(ref state);

        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        var job = new FollowChargeHitBoxJob
        {
            OwnerTransforms = _ownerTransformLookup,
            Bosses = _bossLookup,
            Ecb = ecb.AsParallelWriter()
        };

        state.Dependency = job.ScheduleParallel(_chargeHitBoxQuery, state.Dependency);
        state.Dependency.Complete();
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }
}

[BurstCompile]
public partial struct FollowChargeHitBoxJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<LocalToWorld> OwnerTransforms;
    [ReadOnly] public ComponentLookup<BossCombatData> Bosses;
    public EntityCommandBuffer.ParallelWriter Ecb;

    private void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex,
        ref LocalTransform transform, in BossDashHitBoxTag chargeHitBox)
    {
        if (!OwnerTransforms.HasComponent(chargeHitBox.Owner) ||
            !Bosses.HasComponent(chargeHitBox.Owner) ||
            Bosses[chargeHitBox.Owner].CurrentState != BossState.Charging)
        {
            Ecb.AddComponent(chunkIndex, entity, default(DestroyEntityTag));
            return;
        }

        LocalToWorld ownerTransform = OwnerTransforms[chargeHitBox.Owner];
        transform.Position = ownerTransform.Position;
        transform.Rotation = ownerTransform.Rotation;
        transform.Scale = 1f;
    }
}