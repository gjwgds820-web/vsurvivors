using Unity.Entities;

public struct HealthData : IComponentData
{
    public float CurrentHealth;
    public float MaxHealth;
    public float InvincibilityTimer;
    public float DamageReduction;
}
