using Unity.Entities;
using UnityEngine;

public class VisualInstanceObject : IComponentData
{
    public GameObject Value;
    public Transform Transform;
}

public class SubSceneVisualModel : IComponentData
{
    public Transform Value;
}

public class AnimatorModel : IComponentData
{
    public Animator Value;
    public Animator[] Animators;
}

public class VisualRendererModel : IComponentData
{
    public Renderer[] Renderers;
    public Color[] OriginalColors;
    public MaterialPropertyBlock[] PropertyBlocks;
    public bool IsFlashing;
    public float FlashTimer;
}

public struct VisualAnimationState : IComponentData { public Unity.Mathematics.float3 PrevPosition;
    public float Speed;
    public bool TriggerSummon;
    public bool TriggerHit;
    public bool TriggerAttack; // ??꾩슦, ????怨듦꺽 ?몃━嫄곗슜
    public int AttackIndex;    // 怨듦꺽 醫낅쪟 ?몃뜳??(0: 湲곕낯, 1: ?뚯쭊, 2: ?ъ쿃 ??
    public bool IsDead;
    
    // ?좊땲???좊땲硫붿씠???대깽?몃? ?꾪븳 ?숆린???꾨뱶
    public bool EventAttackHit; // 怨듦꺽 ?寃???대컢
    public bool EventAttackEnd; // 怨듦꺽 醫낅즺 ??대컢
}
