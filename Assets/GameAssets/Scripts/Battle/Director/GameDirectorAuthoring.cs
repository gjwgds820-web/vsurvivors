using Unity.Entities;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class GameDirectorAuthoring : MonoBehaviour
{
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject bossPrefab;

    [Header("Testing & Balance")]
    public float enemySpawnInterval = 5f;
    public float bossSpawnInterval = 300f;
    public float expRequirementBase = 100f;

    public class GameDirectorBaker : Baker<GameDirectorAuthoring>
    {
        public override void Bake(GameDirectorAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            
            AddComponent(entity, new GameDirectorData
            {
                PortalPrefab = GetEntity(authoring.portalPrefab, TransformUsageFlags.Dynamic),
                EnemyPrefab = GetEntity(authoring.enemyPrefab, TransformUsageFlags.Dynamic),
                BossPrefab = GetEntity(authoring.bossPrefab, TransformUsageFlags.Dynamic),
                WaveTimer = 5f,
                EnemySpawnTimer = 1f,
                EnemySpawnInterval = authoring.enemySpawnInterval,
                CurrentWave = 1,
                CurrentPhase = GamePhase.NormalWave,
                GlobalTimer = 0f,
                BossTimer = 180f,
                BossSpawnInterval = authoring.bossSpawnInterval,
                ExpRequirementBase = authoring.expRequirementBase
            });
            AddComponent<SubSceneVisualModel>(entity);

            // Parse const.csv
            var constData = new ConstConfigData();
            string path = Application.dataPath + "/Resources/Data/const.csv";
            if (File.Exists(path))
            {
                string[] lines = File.ReadAllLines(path, System.Text.Encoding.UTF8);
                var dict = new Dictionary<string, float>();
                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    string[] row = lines[i].Split(',');
                    if (row.Length >= 3 && float.TryParse(row[2], out float val))
                    {
                        dict[row[0].Trim()] = val;
                    }
                }
                
                if (dict.TryGetValue("portal_create_phase1", out float v1)) constData.PortalCreatePhase1 = v1;
                if (dict.TryGetValue("portal_max_phase1", out float v2)) constData.PortalMaxPhase1 = (int)v2;
                if (dict.TryGetValue("portal_summon_phase1", out float v3)) constData.PortalSummonPhase1 = v3;
                if (dict.TryGetValue("phase_time_1", out float v4)) constData.PhaseTime1 = v4;

                if (dict.TryGetValue("portal_create_phase2", out float v5)) constData.PortalCreatePhase2 = v5;
                if (dict.TryGetValue("portal_max_phase2", out float v6)) constData.PortalMaxPhase2 = (int)v6;
                if (dict.TryGetValue("portal_summon_phase2", out float v7)) constData.PortalSummonPhase2 = v7;
                if (dict.TryGetValue("phase_time_2", out float v8)) constData.PhaseTime2 = v8;

                if (dict.TryGetValue("portal_create_phase3", out float v9)) constData.PortalCreatePhase3 = v9;
                if (dict.TryGetValue("portal_max_phase3", out float v10)) constData.PortalMaxPhase3 = (int)v10;
                if (dict.TryGetValue("portal_summon_phase3", out float v11)) constData.PortalSummonPhase3 = v11;
                if (dict.TryGetValue("phase_time_3", out float v12)) constData.PhaseTime3 = v12;

                if (dict.TryGetValue("portal_destroy_time_per_shadow", out float v13)) constData.PortalDestroyTimePerShadow = v13;
                if (dict.TryGetValue("portal_boss_timer", out float v14)) constData.PortalBossTimer = v14;
            }
            AddComponent(entity, constData);
        }
    }
}
