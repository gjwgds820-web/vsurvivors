using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using System.Linq;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class DataManagerBridgeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (SystemAPI.HasSingleton<CurrentStageConfig>()) return;

        // 추가: 전역 비동기 매니저(DataManager 등)의 데이터 로딩이 끝날 때까지 ECS 로직 보류
        if (!VSurvivors.Managers.AppManager.IsInitialized) return;

        var entity = EntityManager.CreateEntity();
        var config = new CurrentStageConfig();
        var buffer = EntityManager.AddBuffer<PortalConfigElement>(entity);

        bool hasData = DataManager.Instance != null && DataManager.Instance.StageDict != null && DataManager.Instance.StageDict.Count > 0;

        if (hasData)
        {
            int currentStageId = DataManager.Instance.currentUserData.CurrentStage;
            if (!DataManager.Instance.StageDict.ContainsKey(currentStageId))
            {
                currentStageId = DataManager.Instance.StageDict.Keys.First();
                Debug.LogWarning($"[DataManagerBridge] Invalid or missing StageID. Defaulting to first stage: {currentStageId}");
            }

            var stageData = DataManager.Instance.StageDict[currentStageId];
            config = new CurrentStageConfig
            {
                StageID = stageData.ID,
                Timer = stageData.Timer,
                Portal1 = stageData.Portal1, Chance1 = stageData.Chance1,
                Portal2 = stageData.Portal2, Chance2 = stageData.Chance2,
                Portal3 = stageData.Portal3, Chance3 = stageData.Chance3,
            };

            foreach (var kvp in DataManager.Instance.PortalDict)
            {
                buffer.Add(new PortalConfigElement
                {
                    ID = kvp.Value.ID,
                    SummonAmount = kvp.Value.SummonAmount,
                    DelPortal = kvp.Value.DelPortal,
                    Monster1 = kvp.Value.Monster1
                });
            }
        }
        else
        {
            Debug.LogWarning("[DataManagerBridge] DataManager.Instance is null or empty! Using hardcoded fallback StageConfig.");
            config = new CurrentStageConfig
            {
                StageID = 101000, Timer = 600,
                Portal1 = 42010101, Chance1 = 100,
                Portal2 = 42010002, Chance2 = 0,
                Portal3 = 42010003, Chance3 = 0,
            };
            buffer.Add(new PortalConfigElement { ID = 42010101, SummonAmount = 300, DelPortal = 0, Monster1 = 301000 });
        }

        EntityManager.AddComponentData(entity, config);
    }
}
