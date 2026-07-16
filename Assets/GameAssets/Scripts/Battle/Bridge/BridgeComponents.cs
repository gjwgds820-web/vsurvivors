using Unity.Entities;

public struct CurrentStageConfig : IComponentData
{
    public int StageID;
    public float Timer;
    public int Portal1, Chance1;
    public int Portal2, Chance2;
    public int Portal3, Chance3;
}

[InternalBufferCapacity(16)]
public struct PortalConfigElement : IBufferElementData
{
    public int ID;
    public int SummonAmount;
    public int DelPortal;
    public int Monster1;
}

public struct BattleUpgradeModifiers : IComponentData
{
    public float MaxHealthBonus;
    public float HealthRegenPerSecondBonus;
    public float DodgeChanceBonus;

    public float MoveSpeedBonus;
    public float ItemPickupRangeBonus;
    public float ExpGainPercentBonus;

    public float StartShadowCountBonus;
    public float MaxShadowBonus;
    public float ShadowRegenCooldownReduction;

    public float ShadowAttackPowerBonus;
    public float ShadowAttackSpeedBonus;
    public float ShadowCriticalChanceBonus;
    public float ShadowCriticalDamageBonus;
}

public struct BattleUpgradeAppliedTag : IComponentData
{
}
