using Unity.Entities;
using Unity.Mathematics;

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
    public float AttackRange;
    public float AttackCooldown;
    public float CurrentCooldown;
    public TargetingType TargetPriority;
    public AttackType AttackType;
    public Entity CurrentTarget;
    public float ScanTimer;
}