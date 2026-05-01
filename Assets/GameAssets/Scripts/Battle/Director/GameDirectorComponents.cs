using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public enum GamePhase
{
    NormalWave,
    BossFight,
    EventPaused,
    IsolatedBossFight
}

public struct GameDirectorData : IComponentData
{
    public Entity PortalPrefab;
    public Entity EnemyPrefab;
    public Entity BossPrefab;
    
    public float EnemySpawnTimer;
    public float EnemySpawnInterval;
    
    public float WaveTimer;
    public int CurrentWave;
    public GamePhase CurrentPhase;
    public GamePhase PreviousPhase;
    public float GlobalTimer;
    
    public float BossTimer;
    public float BossSpawnInterval;
    
    public int KilledEnemyCount;
    
    public float ExpRequirementBase;
    
    // For Isolated Boss Fight
    public float3 SavedPlayerPosition;
    public Entity ActiveIsolatedPortal;
}

public struct SpawnBossEventTag : IComponentData
{
    public int BossID;
    public bool IsIsolatedBoss;
}

public struct ClearNormalEnemiesEventTag : IComponentData {}

public struct GameClearEventTag : IComponentData
{
    public int ClearanceLevel;
}


public struct ConstConfigData : IComponentData
{
    public float PortalCreatePhase1;
    public int PortalMaxPhase1;
    public float PortalSummonPhase1;
    public float PhaseTime1;
    public float PortalCreatePhase2;
    public int PortalMaxPhase2;
    public float PortalSummonPhase2;
    public float PhaseTime2;
    public float PortalCreatePhase3;
    public int PortalMaxPhase3;
    public float PortalSummonPhase3;
    public float PhaseTime3;
    public float PortalDestroyTimePerShadow;
    public float PortalBossTimer;
}

