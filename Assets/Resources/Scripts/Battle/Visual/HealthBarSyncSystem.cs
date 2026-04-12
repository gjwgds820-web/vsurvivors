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
                // 최적화 경고: 매 프레임 GetComponentInChildren는 무거울 수 있습니다.
                var healthBar = visualModel.Value.GetComponentInChildren<HealthBarVisual>(true);

                if (healthBar != null)
                {
                    healthBar.UpdateHealth(healthData.ValueRO.CurrentHealth, healthData.ValueRO.MaxHealth);
                }
            }
        }
    }
}