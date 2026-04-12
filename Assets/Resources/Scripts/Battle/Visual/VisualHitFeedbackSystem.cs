using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateBefore(typeof(VisualAnimationSyncSystem))]
public partial class VisualHitFeedbackSystem : SystemBase
{
    private static readonly int _BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int _Color = Shader.PropertyToID("_Color");

    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (health, animState, rendererModel) in SystemAPI.Query<RefRO<HealthData>, RefRO<VisualAnimationState>, VisualRendererModel>())
        {
            // 피격 시 빨간색 점멸 로직 
            if (animState.ValueRO.TriggerHit)
            {
                rendererModel.IsFlashing = true;
                rendererModel.FlashTimer = 0.15f; // 0.15초간 빨간색 유지
            }

            if (rendererModel.IsFlashing)
            {
                rendererModel.FlashTimer -= dt;
                if (rendererModel.FlashTimer <= 0f)
                {
                    rendererModel.IsFlashing = false;
                }
            }

            if (rendererModel.Renderers == null || rendererModel.OriginalColors == null || rendererModel.PropertyBlocks == null)
                continue;

            for (int i = 0; i < rendererModel.Renderers.Length; i++)
            {
                var r = rendererModel.Renderers[i];
                if (r == null || i >= rendererModel.PropertyBlocks.Length) continue;
                
                var block = rendererModel.PropertyBlocks[i];

                if (rendererModel.IsFlashing)
                {
                    r.enabled = true;
                    Color originalColor = rendererModel.OriginalColors[i];
                    // FlashTimer를 통해 Color.red에서 원래 색으로 부드럽게 돌아갑니다.
                    float t = math.clamp(rendererModel.FlashTimer / 0.15f, 0f, 1f);
                    Color currentColor = Color.Lerp(originalColor, Color.red, t);

                    if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(_BaseColor))
                        block.SetColor(_BaseColor, currentColor);
                    else if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(_Color))
                        block.SetColor(_Color, currentColor);
                        
                    r.SetPropertyBlock(block);
                }
                else
                {
                    Color originalColor = rendererModel.OriginalColors[i];

                    if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(_BaseColor))
                        block.SetColor(_BaseColor, originalColor);
                    else if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(_Color))
                        block.SetColor(_Color, originalColor);

                    r.SetPropertyBlock(block);

                    // 무적 시간일 때 렌더러 On/Off 점멸 (깜빡임 이펙트)
                    if (health.ValueRO.InvincibilityTimer > 0f)
                    {
                        // 사인파를 이용해 시간 단위로 true/false 토글 (주파수=30)
                        float wave = math.sin(health.ValueRO.InvincibilityTimer * 30f);
                        r.enabled = wave > 0f;
                    }
                    else
                    {
                        r.enabled = true;
                    }
                }
            }
        }
    }
}
