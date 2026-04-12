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

public struct VisualAnimationState : IComponentData
{
    public float Speed;
    public bool TriggerSummon;
    public bool TriggerHit;
    public bool TriggerAttack; // 섀도우, 적 등 공격 트리거용
    public int AttackIndex;    // 공격 종류 인덱스 (0: 기본, 1: 돌진, 2: 투척 등)
    public bool IsDead;
    
    // 유니티 애니메이션 이벤트를 위한 동기화 필드
    public bool EventAttackHit; // 공격 타격 타이밍
    public bool EventAttackEnd; // 공격 종료 타이밍
}