using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;

[InternalBufferCapacity(8)]
public struct DamageBufferElement : IBufferElementData
{
    public float Damage;
}

[InternalBufferCapacity(8)]
public struct HitRecordElement : IBufferElementData
{
    public Entity Target;
    public double LastHitTime;
}

public enum HitBoxShape
{
    Circle,
    Box,
    Cone,
}

public struct HitBoxData : IComponentData
{
    public HitBoxShape Shape;
    public float Damage;
    public float Radius;
    public float Angle;
    public float3 BoxExtents;
    public float Duration;
    public int TargetFaction;
    public bool IsPiercing;
    public int MaxPierceCount; // 0?대㈃ 臾댄븳 愿??(IsPiercing = true????
    public int CurrentPierceCount;
    public float TickRate;
}

public struct ProjectileData : IComponentData
{
    public float3 Direction;
    public float Speed;
    public float MaxDistance;
    public float TravelledDistance;
}

public struct DeathTag : IComponentData { }

public struct EffectVisualInfo : IComponentData
{
    public int ID;
}

public struct SpinningProjectileData : IComponentData
{
    public float SpinSpeed;
    public float3 SpinAxis;
}

public struct ProjectileVisualInfo : IComponentData
{
    public int ID;
}

public struct DestroyEntityTag : IComponentData { }

