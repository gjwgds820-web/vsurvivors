using Unity.Entities;
using Unity.Collections;
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
    Charging,
    Cooldown,
    Enraging
}

public enum BossSkillExecutionType : byte
{
    HitBox,
    Projectile,
    Charge
}

public enum BossSkillTarget : byte
{
    Nearest,
    Player
}

public enum BossPassiveStat : byte
{
    Attack,
    MoveSpeed
}

public struct BossSkillCooldownState
{
    public int SkillID;
    public float Remaining;
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
    public float DashRemainingDistance;
    public float DashTimer; // Death 이벤트에서도 사용
    
    public float AttackCooldown;
    public int CurrentPhase;
    public int CurrentSkillID;
    public int PendingStartSkillID;
    public float BaseAttackPower;
    public float BaseMoveSpeed;
    public bool IsEnraged;
    public FixedList128Bytes<BossSkillCooldownState> SkillCooldowns;
}

// 돌진 중 보스 위치를 따라가는 접촉 히트박스입니다.
public struct BossDashHitBoxTag : IComponentData
{
    public Entity Owner;
}

public struct BossAttackPrefabs : IComponentData
{
    public Entity MeleeHitBoxPrefab;
    public Entity AxeHitBoxPrefab;
    public Entity DashHitBoxPrefab;
}

public struct BossAuthoringConfig : IComponentData
{
    public float SizeReference;
    public float BodyRadius;
    public float ConeAngle;
    public float BoxWidthRate;
    public float EnrageDuration;
}

[InternalBufferCapacity(4)]
public struct BossSkillPrefabElement : IBufferElementData
{
    public int SkillID;
    public Entity Prefab;
    public int AnimationIndex;
    public BossSkillExecutionType ExecutionType;
}

public struct BossPatternDefBlob
{
    public int BossID;
    public int Phase;
    public float HealthRate;
    public int StartSkillID;
    public int Skill1;
    public int Skill2;
    public int Skill3;
    public int Skill4;
}

public struct BossActiveSkillDefBlob
{
    public int SkillID;
    public BossSkillTarget Target;
    public float AttackRate;
    public float Cooldown;
    public bool IsForced;
    public float GroggyDuration;
    public float RangeRate;
    public HitBoxShape Shape;
}

public struct BossPassiveEffectDefBlob
{
    public int SkillID;
    public BossPassiveStat Stat;
    public float BuffValue;
    public float Duration;
}

public struct BossPatternDatabaseBlob
{
    public BlobArray<BossPatternDefBlob> Patterns;
    public BlobArray<BossActiveSkillDefBlob> ActiveSkills;
    public BlobArray<BossPassiveEffectDefBlob> PassiveEffects;
}

public struct BossPatternDatabaseComponent : IComponentData
{
    public BlobAssetReference<BossPatternDatabaseBlob> DatabaseRef;
}
