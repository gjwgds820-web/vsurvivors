using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public enum GamePhase
{
    NormalWave,
    BossFight,
    EventPaused
}

public struct GameDirectorData : IComponentData
{
    public Entity GatePrefab;
    public Entity EnemyPrefab;
    public Entity BossPrefab;
    
    public float EnemySpawnTimer;
    public float EnemySpawnInterval;
    
    public float WaveTimer;
    public int CurrentWave;
    public GamePhase CurrentPhase;
    public float GlobalTimer;
    
    public float BossTimer;
    public float BossSpawnInterval;
    
    public int KilledEnemyCount;
    
    public float ExpRequirementBase;
}

public struct SpawnBossEventTag : IComponentData
{
    public int BossID;
}

public struct ClearNormalEnemiesEventTag : IComponentData {}

public struct GameClearEventTag : IComponentData
{
    public int ClearanceLevel;
}
