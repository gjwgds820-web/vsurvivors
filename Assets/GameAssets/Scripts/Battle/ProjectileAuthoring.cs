using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// 공용 투사체(Projectile) 생성용 Authoring
public class ProjectileAuthoring : MonoBehaviour
{
    [Header("Projectile Base Settings")]
    public float Speed = 10f;
    public float MaxDistance = 20f;

    [Header("Visual Binding")]
    [Tooltip("VisualManager의 EffectVisualPrefabs 배열 인덱스입니다. -1이면 시각화하지 않습니다.")]
    public int VisualPrefabID = -1;

    class Baker : Baker<ProjectileAuthoring>
    {
        public override void Bake(ProjectileAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // 방향(Direction)은 발사하는 쪽에서 덮어씌웁니다.
            AddComponent(entity, new ProjectileData
            {
                Speed = authoring.Speed,
                MaxDistance = authoring.MaxDistance,
                Direction = float3.zero,
                TravelledDistance = 0f
            });

            if (authoring.VisualPrefabID >= 0)
            {
                AddComponent(entity, new EffectVisualInfo { PrefabID = authoring.VisualPrefabID });
            }
        }
    }
}
