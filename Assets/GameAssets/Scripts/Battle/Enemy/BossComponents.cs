using Unity.Entities;
using Unity.Mathematics;

public enum BossAttackPattern
{
    Melee,
    AxeThrow,
    Dash
}

public struct BossCombatData : IComponentData
{
    public float StateTimer;
    public BossAttackPattern CurrentPattern;
    
    // 공격 진행 상태
    public bool IsAttacking;
    public float AttackDelayTimer;
    public float3 PendingTargetPosition;
    public float3 DashDirection;
    public float DashSpeed; // 돌진 속도 조절용
    public float DashTimer;
    public bool IsDashingPhase;
    
    public float AttackCooldown;
}

// 보스 대시 중 부착되는 히트박스 식별용 (단순 태그)
public struct BossDashHitBoxTag : IComponentData { }

public struct BossAttackPrefabs : IComponentData
{
    public Entity MeleeHitBoxPrefab;
    public Entity AxeHitBoxPrefab;
    public Entity DashHitBoxPrefab;
}
