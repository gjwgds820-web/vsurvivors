using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public enum FormationState
{
    Idle,
    Moveing,
}

public enum TargetingType
{
    Nearest,
    LowestHP,
    Random,
}

public enum AttackType
{
    Melee,
    Ranged,
}

public struct ShadowData : IComponentData
{
    public int Index;
    public float MoveSpeed;
    public FormationState CurrentState;
    public float StateChangeTimer;
}

public struct TargetPositionData : IComponentData
{
    public float3 Value;
}

public struct ShadowCombatData : IComponentData
{
    public float MaxHealth;
    public float CurrentHealth;
    public float AttackPower;
    public float AttackRange;
    public float AttackCooldown;
    public float CurrentCooldown;
    public TargetingType TargetPriority;
    public AttackType AttackType;
    public Entity AttackPrefab;
    public Entity CurrentTarget;
    public float ScanTimer;
    public float InvincibilityTimer;
    public bool IsAlive;
}

public struct ShadowTag : IComponentData { }