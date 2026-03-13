using Unity.Entities;

public struct GameDirectorData : IComponentData
{
    public Entity GatePrefab;
    public Entity EnemyPrefab;
    public float WaveTimer;
    public int CurrentWave;
}