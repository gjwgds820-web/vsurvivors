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
    [Tooltip("어드레서블로 시각화 효과를 연결할 ID입니다. (IDAttackVisual). -1이면 시각화하지 않습니다.")]
    public int VisualID = -1;

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

            int finalVisualID = authoring.VisualID;
            if (finalVisualID == -1)
            {
                // 이름 앞부분의 숫자(ID) 추출을 시도합니다. 예: "21020201Attack" -> 21020201
                string objName = authoring.gameObject.name;
                string numStr = "";
                foreach (char c in objName)
                {
                    if (char.IsDigit(c)) numStr += c;
                    else break;
                }

                if (!string.IsNullOrEmpty(numStr))
                {
                    int.TryParse(numStr, out finalVisualID);
                }
            }

            if (finalVisualID >= 0)
            {
                AddComponent(entity, new EffectVisualInfo { ID = finalVisualID });
            }
        }
    }
}
