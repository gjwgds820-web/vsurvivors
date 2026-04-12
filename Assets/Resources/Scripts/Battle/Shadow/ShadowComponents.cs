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

    // 추가: 딜레이 공격을 위한 내부 상태 저장 관리
    public bool IsAttacking; // 0.5초 선딜레이 중인지
    public float AttackDelayTimer; // 지나간 딜레이 시간 (0.5s 이상이면 발사)
    public Unity.Mathematics.float3 PendingTargetPosition; // 타겟이 사라질 수도 있으므로 치려는 좌표 캐싱

    public AttackType AttackType;
    public Entity AttackPrefab;
    public bool IsAlive;
    public float DeathTimer; // 추가: 2초 후 제거
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