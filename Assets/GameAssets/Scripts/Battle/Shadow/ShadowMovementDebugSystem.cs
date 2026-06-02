using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ShadowTargetDebugSystem))]
public partial class ShadowMovementDebugSystem : SystemBase
{
    private float _logTimer;
    protected override void OnCreate()
    {
        _logTimer = 0f;
    }

    protected override void OnUpdate()
    {
        _logTimer -= SystemAPI.Time.DeltaTime;
        if (_logTimer > 0f) return;
        _logTimer = 1.0f;

        bool hasPhysicsWorld = false;
        
        int queryMatchCount = 0;
        foreach (var entity in SystemAPI.Query<Unity.Transforms.LocalTransform>().WithAll<CShadowData>())
        {
            queryMatchCount++;
        }

        Debug.Log($"<color=red>[Shadow Movement Check]</color> Has PhysicsWorld: {hasPhysicsWorld} | Shadows with LocalTransform: {queryMatchCount}");
    }
}

