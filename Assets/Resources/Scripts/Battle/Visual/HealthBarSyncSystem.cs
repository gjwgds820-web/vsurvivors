using Unity.Entities;
using Unity.Burst;
using UnityEngine;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class HealthBarSyncSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (healthData, visualModel) in SystemAPI.Query<RefRO<HealthData>, SubSceneVisualModel>())
        {
            if (visualModel.Value != null)
            {
                var healthBar = visualModel.Value.GetComponentInChildren<HealthBarVisual>();
                if (healthBar != null)
                {
                    healthBar.UpdateHealth(healthData.ValueRO.CurrentHealth, healthData.ValueRO.MaxHealth);
                }
            }
        }
    }
}