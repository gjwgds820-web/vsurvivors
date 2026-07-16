using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct PlayerInput : IComponentData
{
    public float2 Move;
}

public struct PlayerMovementData : IComponentData
{
    public float MoveSpeed;
    public float RotationSpeed;
}

public struct PlayerData : IComponentData
{
    public int Level;
    public float EXP;
    public float ExpGainMultiplier;
    public float DodgeChancePercent;

    public float HealthRegenPerSecond;

    public float ShadowAttackPowerPercentBonus;
    public float ShadowAttackSpeedPercentBonus;
    public float ShadowCriticalChancePercent;
    public float ShadowCriticalDamagePercent;

    public float MaxShadow;
    public float CurrentShadow;
    public float ShadowRegenCooldown;
    public float ShadowRegenTimer;
    public float SummonAnimationDelay;
    public int InitialShadowSpawnCount;

    public float MagnetismRadius;
    public float CollectRadius;
    public bool IsAlive;
    public bool InitialShadowsSpawned;

    public int DeathCount;
    public float DeathTimer;
}

public struct ShadowSpawnData : IComponentData
{
    public Entity ShadowPrefab;
    public bool UsePlayerRotationBasis;
}

public struct ShadowSlotElement : IBufferElementData
{
    public Entity ShadowEntity;
    public bool IsAlive;
}

public struct ActiveShadowSkillElement : IBufferElementData
{
    public int ShadowID;
}

public struct CameraTargetTag : IComponentData { }

public struct LevelUpEventTag : IComponentData 
{
    public int Count;
}

public struct ElementAscensionEventTag : IComponentData
{
    public int BossLevel;
}

public struct PlayerDeathEventTag : IComponentData {}
public struct DeathProcessedTag : IComponentData {}