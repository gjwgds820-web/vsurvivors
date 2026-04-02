using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

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
public struct CEnemyData : IComponentData
{
    public int ID;
    public EnemyType Type;
    public EnemyState CurrentState;
    public Entity AttackPrefab;

    public float AttackPower;
    public float AttackRange;
    public float AttackCooldown;
    public float CurrentCooldown;

    public float MoveSpeed;
    public HitBoxShape HitBoxShape;
    public float HitboxRadius;
    public float HitboxDuration;
    public bool IsPiercing;
    public bool IsAlive;

    public bool IsBoss;
    public float BlockedTimer;
}

public struct EnemyDefBlob
{
    public int ID;
    public EnemyType Type;
    public float MaxHealth;
    public float AttackPower;
    public float AttackRange;
    public float AttackCooldown;
    public float MoveSpeed;
    public HitBoxShape HitBoxShape;
    public float HitboxRadius;
    public float HitboxDuration;
    public bool IsPiercing;
    public bool IsBoss;
}

public struct EnemyDatabaseBlob
{
    public BlobArray<EnemyDefBlob> Enemies;
}

public struct EnemyDatabaseComponent : IComponentData
{
    public BlobAssetReference<EnemyDatabaseBlob> DatabaseRef;
}
#endregion

public struct EnemyTag : IComponentData { }
public struct BossTag : IComponentData { }