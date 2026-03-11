using Unity.Entities;
using Unity.Transforms;
using Unity.Physics.GraphicsIntegration;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class PlayerVisualSyncSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (interpolatedTransform, visualModel) in SystemAPI.Query<RefRO<PhysicsGraphicalInterpolationBuffer>, SubSceneVisualModel>())
        {
            if (visualModel.Value != null)
            {
                visualModel.Value.position = interpolatedTransform.ValueRO.PreviousTransform.pos;
                visualModel.Value.rotation = interpolatedTransform.ValueRO.PreviousTransform.rot;
            }
        }
    }
}