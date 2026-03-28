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
    public float EnemySpawnTimer;
    public float WaveTimer;
    public int CurrentWave;
    public GamePhase CurrentPhase;
    public float GlobalTimer;
    public float BossTimer;
}

public struct SpawnBossEventTag : IComponentData
{
    public int BossID;
}

public struct ClearNormalEnemiesEventTag : IComponentData {}