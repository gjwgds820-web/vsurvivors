using UnityEngine;

// Unity 에디터에서 Asset > Create > Character 메뉴를 통해 생성할 수 있습니다.
[CreateAssetMenu(fileName = "Character", menuName = "Game/Character")]
public class Character : ScriptableObject
{
    private int _id;
    private string _name;
    private int _maxHealth;
    private int _healthRegen;
    private float _moveSpeed;
    private float _damageReduction;
    private int _attackPoint;
    private float _attackSpeed;
    private float _criticalChance;
    private float _criticalDamage;
    private int _maxShadow;
    private float _shadowSummonCooltime;

    public int Id => _id;
    public string Name => _name;
    public int MaxHealth => _maxHealth;
    public int HealthRegen => _healthRegen;
    public float MoveSpeed => _moveSpeed;
    public float DamageReduction => _damageReduction;
    public int AttackPoint => _attackPoint;
    public float AttackSpeed => _attackSpeed;
    public float CriticalChance => _criticalChance;
    public float CriticalDamage => _criticalDamage;
    public int MaxShadow => _maxShadow;
    public float ShadowSummonCooltime => _shadowSummonCooltime;
}
