using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;

public enum DropItemType
{
    Exp,
    Gold,
    Magnet,
    Bomb,
}

public struct DroppedItemData : IComponentData
{
    public DropItemType Type;
    public float Amount;
    public float MoveSpeed;
    public bool IsAttracted;
}

public struct DropBankData : IComponentData
{
    public Entity ExpPrefab;
    public Entity GoldPrefab;
    public Entity MagnetPrefab;
    public Entity BombPrefab;
}

public struct GoldEventTag : IComponentData { public int amount;}
public struct MagnetEventTag : IComponentData {}

public struct BombEventTag : IComponentData {}