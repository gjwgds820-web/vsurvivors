using Unity.Entities;
using Unity.Mathematics;

public struct PlayerInput : IComponentData
{
    public float2 Move;
}

public struct PlayerMovementData : IComponentData
{
    public float MoveSpeed;
    public float RotationSpeed;
}

public struct CameraTargetTag : IComponentData { }