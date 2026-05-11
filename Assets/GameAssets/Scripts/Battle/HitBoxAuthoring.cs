using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// 공용 히트박스(HitBox) 생성용 Authoring
public class HitBoxAuthoring : MonoBehaviour
{
    [Header("HitBox Base Settings")]
    public HitBoxShape Shape = HitBoxShape.Circle;
    [Tooltip("스폰 시 발사자의 공격력으로 덮어씌워질 기본 데미지입니다.")]
    public float BaseDamage = 0f;
    public float Radius = 1f;
    public float Duration = 5f; // 0이면 영구 지속되므로 수동 파괴 필요

    [Header("Cone & Box Settings (Optional)")]
    public float Angle = 0f;
    public float3 BoxExtents = new float3(1f, 1f, 1f);

    [Header("Piercing & Multi-Hit Settings")]
    public bool IsPiercing = false;
    [Tooltip("IsPiercing이 true일 경우 몇 번 관통하고 파괴될지 설정합니다. 0이면 무한 관통입니다.")]
    public int MaxPierceCount = 0;
    [Tooltip("장판 틱데미지 주기입니다. 0이면 한 번만 타격합니다.")]     
    public float TickRate = 0f;
    
    [Header("Visual Binding")]
    [Tooltip("어드레서블로 시각화 효과를 연결할 ID입니다. (IDAttackVisual). -1이면 시각화하지 않습니다.")]
    public int VisualID = -1;
    
    class Baker : Baker<HitBoxAuthoring>
    {
        public override void Bake(HitBoxAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // TargetFaction(적군/아군) 및 최종 Damage는 발사하는 쪽에서 덮어씌웁니다.
            AddComponent(entity, new HitBoxData
            {
                Shape = authoring.Shape,
                Damage = authoring.BaseDamage,
                Radius = authoring.Radius,
                Angle = authoring.Angle,
                BoxExtents = authoring.BoxExtents,
                Duration = authoring.Duration,
                IsPiercing = authoring.IsPiercing,
                MaxPierceCount = authoring.MaxPierceCount,
                CurrentPierceCount = 0,
                TickRate = authoring.TickRate,
                TargetFaction = -1 // 스포너가 덮어쓸 값입니다. (0: 플레이어가 타격, 1: 적이 타격)
            });

            int finalVisualID = authoring.VisualID;
            if (finalVisualID == -1)
            {
                // 이름 앞부분의 숫자(ID) 추출을 시도합니다.
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

            // 히트 기록용 버퍼 자동 추가
            AddBuffer<HitRecordElement>(entity);
        }
    }
}
