using Unity.Collections;
using Unity.Entities;

public struct CPortalData : IComponentData
{
    public int PortalID; // from PortalDatabase
    public int RequiredShadows;  // aka DelPortal
    public int AbsorbedShadows;
    public float InteractionRadius;
    public float AbsorbtionTimer;
    public bool IsActive;

    public int State;

    // PortalData info
    public int SummonAmount;
    public FixedString32Bytes PortalType;
    public int Monster1;
    public int Monster2;
    public int Monster3;
    public int Monster4;
    public int Monster5;
}
