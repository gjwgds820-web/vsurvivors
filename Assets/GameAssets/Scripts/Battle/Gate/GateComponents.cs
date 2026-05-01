using Unity.Entities;

public struct GateData : IComponentData
{
    public int RequiredShadows;
    public int AbsorbedShadows;
    public float InteractionRadius;
    public float AbsorbtionTimer;
    public bool IsActive;

    // 추가: 활성화 상태 등에 사용
    // 0 = 닫히는중 아님, 1 = 닫히는 중
    public int State;
}