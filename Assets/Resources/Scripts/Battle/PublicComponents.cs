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
    public float TickRate;
}

public struct ProjectileData : IComponentData
{
    public float Speed;
    // public Entity Target;
}