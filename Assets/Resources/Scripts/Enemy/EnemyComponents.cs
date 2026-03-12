using Unity.Entities;
using Unity.Mathematics;

#region EnemyEnums
public enum EnemyType
{
    Melee,
    Ranged,
}

public enum EnemyState
{
    Move,
    Attack,
}
#endregion
#region EnemyData
public struct EnemyData : IComponentData
{
    public EnemyType Type;
    public EnemyState CurrentState;

    public float MaxHealth;
    public float CurrentHealth;

    public float AttackPower;
    public float AttackRange;
    public float AttackCooldown;
    public float CurrentCooldown;

    public float MoveSpeed;
    public float SearchTimer;
}
#endregion
#region EnemyTargetData
public struct EnemyTargetData : IComponentData
{
    public Entity CurrentTarget;
    public bool IsTargetingShadow;
}
#endregion

public struct EnemyTag : IComponentData { }