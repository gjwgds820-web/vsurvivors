using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public enum FormationState
{
    Idle,
    Moveing,
}

public enum AttackType
{
    Melee,
    Ranged,
}

public struct CShadowData : IComponentData
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
    public float AttackPower;
    public float AttackRange;
    public float AttackCooldown;
    public float CurrentCooldown;
    public AttackType AttackType;
    public Entity AttackPrefab;
    public bool IsAlive;
}

public struct ShadowLevelStatBlob
{
    public float MaxHealth;
    public float AttackPower;
    public float AttackRange;
    public float AttackCooldown;
}

public struct ShadowDefBlob
{
    public int ID;
    public BlobArray<ShadowLevelStatBlob> LevelStats;
    public int TargetPriority;
    public int AttackType;
}

public struct ShadowDatabaseBlob
{
    public BlobArray<ShadowDefBlob> Shadows;
}

public struct ShadowDatabaseComponent : IComponentData
{
    public BlobAssetReference<ShadowDatabaseBlob> DatabaseRef;
}

public struct ShadowInstanceData : IComponentData
{
    public int ShadowID;
    public int CurrentLevel;
    public ElementType Element;
}

public struct ShadowTag : IComponentData { }