using Unity.Entities;

public enum TargetingFaction
{
    Enemy = 0,
    Ally = 1
}

public enum TargetingType
{
    Nearest,
    LowestHP,
    Random,
}

public struct TargetingData : IComponentData
{
    public Entity CurrentTarget;
    public TargetingFaction Faction;
    public TargetingType Priority;
    
    public float ScanTimer;
    public float ScanInterval;
    public float MaxSearchRangeSq;
    public float MaxFollowRangeSq;
    
    public bool UseCrowdControl;
}
