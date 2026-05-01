using Unity.Entities;
using UnityEngine;

public class BossAuthoring : MonoBehaviour
{
    [Header("Boss Attack Prefabs")]
    public GameObject MeleeHitBoxPrefab;
    public GameObject AxeHitBoxPrefab;
    public GameObject DashHitBoxPrefab;

    [Header("Boss Config")]
    public float DashSpeed = 15f; // Inspector에서 조절 가능

    public class BossBaker : Baker<BossAuthoring>
    {
        public override void Bake(BossAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // BossTag is likely already added or needed here if not in EnemyAuthoring. Let's add it to be safe, or just ensure it exists.
            AddComponent<BossTag>(entity);
            AddComponent(entity, new BossCombatData { DashSpeed = authoring.DashSpeed });

            AddComponent(entity, new BossAttackPrefabs
            {
                MeleeHitBoxPrefab = GetEntity(authoring.MeleeHitBoxPrefab, TransformUsageFlags.Dynamic),
                AxeHitBoxPrefab = GetEntity(authoring.AxeHitBoxPrefab, TransformUsageFlags.Dynamic),
                DashHitBoxPrefab = GetEntity(authoring.DashHitBoxPrefab, TransformUsageFlags.Dynamic)
            });
        }
    }
}
