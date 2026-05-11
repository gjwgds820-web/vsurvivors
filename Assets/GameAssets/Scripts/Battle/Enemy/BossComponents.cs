using Unity.Entities;
using Unity.Mathematics;

public enum BossAttackPattern
{
    Melee,
    AxeThrow,
    Dash
}

public enum BossState
{
    Chasing,
    Prep,
    Hitting,
    Cooldown
}

public struct BossCombatData : IComponentData
{
    public BossState CurrentState;
    public float StateTimer;
    public BossAttackPattern CurrentPattern;
    
    // 공격 진행 상태
    public float3 AttackPosition;
    public quaternion AttackRotation;
    public float3 DashDirection;
    public float DashSpeed;
    public float DashTimer; // Death 이벤트에서도 사용
    
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
