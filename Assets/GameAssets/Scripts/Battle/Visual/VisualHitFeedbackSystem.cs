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
    private static readonly int _MaskTint = Shader.PropertyToID("_MaskTint");
    
    private static readonly int[] _PartColors = new int[] 
    {
        Shader.PropertyToID("_Color1"),
        Shader.PropertyToID("_Color2"),
        Shader.PropertyToID("_Color3"),
        Shader.PropertyToID("_Color4"),
        Shader.PropertyToID("_Color5"),
        Shader.PropertyToID("_Color6"),
        Shader.PropertyToID("_Color7"),
        Shader.PropertyToID("_Color8"),
        Shader.PropertyToID("_Color9_Skin"),
        Shader.PropertyToID("_EmissionColor")
    };

    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (health, animState, rendererModel, entity) in SystemAPI.Query<RefRO<HealthData>, RefRO<VisualAnimationState>, VisualRendererModel>().WithEntityAccess())
        {
            bool isUnderLevel6 = false;
            if (SystemAPI.HasComponent<ShadowInstanceData>(entity))
            {
                int currentLevel = SystemAPI.GetComponent<ShadowInstanceData>(entity).CurrentLevel;
                if (currentLevel < 6) isUnderLevel6 = true;
            }

            if (animState.ValueRO.TriggerHit)
            {
                rendererModel.IsFlashing = true;
                rendererModel.FlashTimer = 0.15f; 
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
                Color originalColor = rendererModel.OriginalColors[i];
                
                if (isUnderLevel6) 
                {
                    originalColor = Color.black;
                }

                if (rendererModel.IsFlashing)
                {
                    r.enabled = true;
                    float t = math.clamp(rendererModel.FlashTimer / 0.15f, 0f, 1f);
                    Color currentColor = Color.Lerp(originalColor, Color.red, t);

                    if (r.sharedMaterial != null)
                    {
                        if (r.sharedMaterial.HasProperty(_MaskTint))
                            block.SetColor(_MaskTint, currentColor);
                        else if (r.sharedMaterial.HasProperty(_BaseColor))
                            block.SetColor(_BaseColor, currentColor);
                        else if (r.sharedMaterial.HasProperty(_Color))
                            block.SetColor(_Color, currentColor);

                        foreach (var propID in _PartColors)
                        {
                            if (r.sharedMaterial.HasProperty(propID))
                            {
                                Color origPart = r.sharedMaterial.GetColor(propID);
                                Color targetPart = isUnderLevel6 ? Color.black : origPart;
                                block.SetColor(propID, Color.Lerp(targetPart, Color.red, t));
                            }
                        }
                    }
                        
                    r.SetPropertyBlock(block);
                }
                else
                {
                    if (r.sharedMaterial != null)
                    {
                        if (r.sharedMaterial.HasProperty(_MaskTint))
                            block.SetColor(_MaskTint, originalColor);
                        else if (r.sharedMaterial.HasProperty(_BaseColor))
                            block.SetColor(_BaseColor, originalColor);
                        else if (r.sharedMaterial.HasProperty(_Color))
                            block.SetColor(_Color, originalColor);

                        foreach (var propID in _PartColors)
                        {
                            if (r.sharedMaterial.HasProperty(propID))
                            {
                                Color origPart = r.sharedMaterial.GetColor(propID);
                                block.SetColor(propID, isUnderLevel6 ? Color.black : origPart);
                            }
                        }
                    }

                    r.SetPropertyBlock(block);

                    if (health.ValueRO.InvincibilityTimer > 0f)
                    {
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
