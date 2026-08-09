using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using System.Linq;
using Unity.Mathematics;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class DataManagerBridgeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (SystemAPI.HasSingleton<CurrentStageConfig>()) return;

        // 추가: 전역 비동기 매니저(DataManager 등)의 데이터 로딩이 끝날 때까지 ECS 로직 보류
        if (!VSurvivors.Managers.AppManager.IsInitialized) return;

        // 추가: 로비 씬 등에서 게임을 시작하기도 전에 Config가 구워지는 것을 방지.
        // 배틀씬에 진입했을 때만 초기화되도록 씬을 검사합니다.
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "BattleScene") return;

        var entity = EntityManager.CreateEntity();
        var config = new CurrentStageConfig();
        var upgradeModifiers = new BattleUpgradeModifiers();
        var buffer = EntityManager.AddBuffer<PortalConfigElement>(entity);

        bool hasData = DataManager.Instance != null && DataManager.Instance.StageDict != null && DataManager.Instance.StageDict.Count > 0;

        if (hasData)
        {
            int currentStageId = DataManager.Instance.currentUserData.CurrentStage;
            if (!DataManager.Instance.StageDict.ContainsKey(currentStageId))
            {
                currentStageId = DataManager.Instance.StageDict.Keys.First();
                Debug.LogWarning($"[DataManagerBridge] Invalid or missing StageID. Defaulting to first stage: {currentStageId}");
            }

            var stageData = DataManager.Instance.StageDict[currentStageId];
            config = new CurrentStageConfig
            {
                StageID = stageData.ID,
                Timer = stageData.Timer,
                Portal1 = stageData.Portal1, Chance1 = stageData.Chance1,
                Portal2 = stageData.Portal2, Chance2 = stageData.Chance2,
                Portal3 = stageData.Portal3, Chance3 = stageData.Chance3,
            };

            foreach (var kvp in DataManager.Instance.PortalDict)
            {
                buffer.Add(new PortalConfigElement
                {
                    ID = kvp.Value.ID,
                    SummonAmount = kvp.Value.SummonAmount,
                    DelPortal = kvp.Value.DelPortal,
                    Monster1 = kvp.Value.Monster1
                });
            }

            upgradeModifiers = BuildBattleUpgradeModifiers(DataManager.Instance.currentUserData);
        }
        else
        {
            Debug.LogWarning("[DataManagerBridge] DataManager.Instance is null or empty! Using hardcoded fallback StageConfig.");
            config = new CurrentStageConfig
            {
                StageID = 101000, Timer = 600,
                Portal1 = 42010101, Chance1 = 100,
                Portal2 = 42010002, Chance2 = 0,
                Portal3 = 42010003, Chance3 = 0,
            };
            buffer.Add(new PortalConfigElement { ID = 42010101, SummonAmount = 300, DelPortal = 0, Monster1 = 31101011 });
        }

        EntityManager.AddComponentData(entity, config);
        EntityManager.AddComponentData(entity, upgradeModifiers);
    }

    private static BattleUpgradeModifiers BuildBattleUpgradeModifiers(UserData userData)
    {
        var modifiers = new BattleUpgradeModifiers();
        if (userData == null || DataManager.Instance == null)
        {
            return modifiers;
        }

        var totals = userData.GetTotalUpgradeEffectsByType(DataManager.Instance.UpgradeGroupDict);
        foreach (var total in totals)
        {
            ApplyUpgradeEffect(ref modifiers, total.Key, total.Value);
        }

        return modifiers;
    }

    private static void ApplyUpgradeEffect(ref BattleUpgradeModifiers modifiers, string effectType, float value)
    {
        if (string.IsNullOrWhiteSpace(effectType))
        {
            return;
        }

        string normalized = effectType.Replace(" ", string.Empty).Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "최대체력":
            case "maxhealth":
            case "health":
                modifiers.MaxHealthBonus += value;
                break;
            case "체력재생":
            case "healthregen":
            case "regen":
                modifiers.HealthRegenPerSecondBonus += value;
                break;
            case "회피확률":
            case "dodgechance":
                modifiers.DodgeChanceBonus += value;
                break;
            case "이동속도":
            case "movespeed":
            case "speed":
                modifiers.MoveSpeedBonus += value;
                break;
            case "아이템획득범위증가":
            case "collectradius":
            case "itemrange":
                modifiers.ItemPickupRangeBonus += value;
                break;
            case "경험치획득량증가":
            case "expgain":
                modifiers.ExpGainPercentBonus += value;
                break;
            case "시작그림자수":
                modifiers.StartShadowCountBonus += value;
                break;
            case "최대그림자":
                modifiers.MaxShadowBonus += value;
                break;
            case "그림자소환쿨타임":
                modifiers.ShadowRegenCooldownReduction += value;
                break;
            case "그림자공격력증가":
                modifiers.ShadowAttackPowerBonus += value;
                break;
            case "그림자공격속도증가":
                modifiers.ShadowAttackSpeedBonus += value;
                break;
            case "그림자치명타확률증가":
                modifiers.ShadowCriticalChanceBonus += value;
                break;
            case "그림자치명타데미지증가":
                modifiers.ShadowCriticalDamageBonus += value;
                break;
        }
    }
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(DataManagerBridgeSystem))]
public partial class PlayerUpgradeApplySystem : SystemBase
{
    private EntityQuery _playerApplyQuery;

    protected override void OnCreate()
    {
        _playerApplyQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadWrite<PlayerData>(),
                ComponentType.ReadWrite<PlayerMovementData>(),
                ComponentType.ReadWrite<HealthData>()
            },
            None = new ComponentType[]
            {
                ComponentType.ReadOnly<BattleUpgradeAppliedTag>()
            }
        });
    }

    protected override void OnUpdate()
    {
        if (!SystemAPI.HasSingleton<BattleUpgradeModifiers>())
        {
            return;
        }

        if (_playerApplyQuery.IsEmptyIgnoreFilter)
        {
            return;
        }

        BattleUpgradeModifiers modifiers = SystemAPI.GetSingleton<BattleUpgradeModifiers>();

        using var entities = _playerApplyQuery.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            Entity entity = entities[i];

            PlayerData playerData = EntityManager.GetComponentData<PlayerData>(entity);
            PlayerMovementData movementData = EntityManager.GetComponentData<PlayerMovementData>(entity);
            HealthData healthData = EntityManager.GetComponentData<HealthData>(entity);

            healthData.MaxHealth += modifiers.MaxHealthBonus;
            healthData.CurrentHealth = math.min(healthData.CurrentHealth + modifiers.MaxHealthBonus, healthData.MaxHealth);

            playerData.HealthRegenPerSecond += modifiers.HealthRegenPerSecondBonus;
            movementData.MoveSpeed += modifiers.MoveSpeedBonus;

            playerData.MagnetismRadius += modifiers.ItemPickupRangeBonus;
            playerData.CollectRadius += modifiers.ItemPickupRangeBonus;

            playerData.ExpGainMultiplier = math.max(0f, playerData.ExpGainMultiplier * (1f + modifiers.ExpGainPercentBonus * 0.01f));
            playerData.DodgeChancePercent = math.clamp(playerData.DodgeChancePercent + modifiers.DodgeChanceBonus, 0f, 95f);

            playerData.ShadowAttackPowerPercentBonus += modifiers.ShadowAttackPowerBonus;
            playerData.ShadowAttackSpeedPercentBonus += modifiers.ShadowAttackSpeedBonus;
            playerData.ShadowCriticalChancePercent = math.clamp(playerData.ShadowCriticalChancePercent + modifiers.ShadowCriticalChanceBonus, 0f, 95f);
            playerData.ShadowCriticalDamagePercent += modifiers.ShadowCriticalDamageBonus;

            int startShadowBonus = (int)math.round(modifiers.StartShadowCountBonus);
            playerData.InitialShadowSpawnCount = math.max(1, playerData.InitialShadowSpawnCount + startShadowBonus);

            playerData.MaxShadow += modifiers.MaxShadowBonus;
            playerData.CurrentShadow = math.clamp(playerData.CurrentShadow, 0f, playerData.MaxShadow);

            playerData.ShadowRegenCooldown = math.max(0.1f, playerData.ShadowRegenCooldown - modifiers.ShadowRegenCooldownReduction);
            playerData.ShadowRegenTimer = math.min(playerData.ShadowRegenTimer, playerData.ShadowRegenCooldown);

            EntityManager.SetComponentData(entity, playerData);
            EntityManager.SetComponentData(entity, movementData);
            EntityManager.SetComponentData(entity, healthData);
            EntityManager.AddComponent<BattleUpgradeAppliedTag>(entity);
        }

        if (entities.Length > 0)
        {
            Enabled = false;
        }
    }
}
