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
    Scan,
    Chase,
    Attack,
    Blocked
}

public enum EnemyRank : byte
{
    Normal,
    Elite,
    Boss
}
#endregion

#region EnemyData
public struct IsolatedBossTag : IComponentData {}

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

    // 공격 애니메이션 딜레이 처리
    public bool IsAttacking;
    public float AttackDelayTimer;
    public float3 PendingTargetPosition;

    public float MoveSpeed;
    public bool IsAlive;
    public float DeathTimer; // 사망 후 삭제 딜레이

    public bool IsBoss;
    public float BlockedTimer;
    public float3 PreviousPosition;
    public float TargetOutOfRangeTimer;
}

public struct EnemyDefBlob
{
    public int ID;
    public EnemyType Type;
    public EnemyRank Rank;
    public float MaxHealth;
    public float AttackPower;
    public float Defence;
    public float AttackRange;
    public float AttackCooldown;
    public float MoveSpeed;
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

