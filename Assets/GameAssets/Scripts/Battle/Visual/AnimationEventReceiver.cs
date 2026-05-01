using UnityEngine;

public class AnimationEventReceiver : MonoBehaviour
{
    [Header("Optional References")]
    [Tooltip("애니메이션 이벤트로 켜고 끌 무기 모델 (예: 보스의 도끼)")]
    public GameObject WeaponObject;

    public bool EventAttackHit;
    public bool EventAttackEnd;

    // 유니티 애니메이션 창에서 무기 비활성화를 원할 때 추가하세요.
    public void HideWeapon()
    {
        if (WeaponObject != null) WeaponObject.SetActive(false);
    }

    // 유니티 애니메이션 창에서 무기 활성화를 원할 때 추가하세요.
    public void ShowWeapon()
    {
        if (WeaponObject != null) WeaponObject.SetActive(true);
    }

    // 유니티 애니메이션 창에서 공격 판정 타이밍에 이 이벤트를 추가하세요.
    public void OnAttackHit() { EventAttackHit = true; }
    
    
    

    // 유니티 애니메이션 창에서 공격 모션이 끝날 때 이 이벤트를 추가하세요.
    public void OnAttackEnd() { EventAttackEnd = true; }
    
    
    

    /// <summary>
    /// 재생 예정인(혹은 재생 중인) 애니메이션 클립을 분석하여 
    /// OnAttackHit() 이벤트가 몇 초 뒤에 발생하는지 자동 계산합니댜.
    /// </summary>
    public float GetTimeToHitEvent()
    {
        var animator = GetComponent<Animator>();
        if (animator == null) return 1.0f;

        // SetTrigger나 Paramenter가 방금 변경된 직후이기 때문에, 
        // Animator의 상태 변화(Transition) 연산을 현재 프레임에서 강제로 1회 갱신시킵니다.
        // 이 코드가 있어야 파라미터 변경 즉시 '다음 클립'을 정확히 인식할 수 있습니다.
        animator.Update(0f);

        // 1순위: 바로 다음에 전환될 예정인 클립 배열 (블렌드 트리 포함 여러 개일 수 있음)
        var nextClips = animator.GetNextAnimatorClipInfo(0);
        if (nextClips.Length > 0)
        {
            float time = FindHitEventTime(nextClips);
            if (time > 0f) return time;
        }

        // 2순위: 전환 없이 즉시 실행 중인 현재 클립 배열
        var currentClips = animator.GetCurrentAnimatorClipInfo(0);
        if (currentClips.Length > 0)
        {
            float time = FindHitEventTime(currentClips);
            if (time > 0f) return time;
        }

        return 1.0f; // 찾지 못한 경우 기본 1초 (안전 장치)
    }

    private float FindHitEventTime(AnimatorClipInfo[] clips)
    {
        foreach (var clipInfo in clips)
        {
            if (clipInfo.clip != null)
            {
                foreach (var ev in clipInfo.clip.events)
                {
                    if (ev.functionName == "OnAttackHit") 
                    {
                        return ev.time;
                    }
                }
            }
        }
        return -1f; // 해당 클립 배열 안에서 이벤트를 찾지 못함
    }
}
