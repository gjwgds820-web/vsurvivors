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
    public float MaxHealth;
    public float CurrentHealth;
    public float HealthRegenPerSecond;
    public float DamageReduction;
    public float MaxShadow;
    public float CurrentShadow;
    public float ShadowRegenCooldown;
    public float ShadowRegenTimer;
    public float InvincibilityTimer;
    public float MagnetismRadius;
    public float CollectRadius;
    public bool IsAlive;
}

public struct ShadowSpawnData : IComponentData
{
    public Entity ShadowPrefab;
}

public struct ShadowSlotElement : IBufferElementData
{
    public Entity ShadowEntity;
    public bool IsAlive;
}

public struct CameraTargetTag : IComponentData { }

public struct LevelUpEventTag : IComponentData {}