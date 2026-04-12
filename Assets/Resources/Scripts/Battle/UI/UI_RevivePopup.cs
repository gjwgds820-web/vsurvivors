using Unity.Entities;
using UnityEngine;

public class UI_RevivePopup : UI_Base
{
    enum Buttons
    {
        AdReviveButton,
        DiamondReviveButton,
        GiveupButton,
    }

    enum Texts
    {
        DiamondText,
    }

    private EntityManager _entityManager;

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        BindButton(typeof(Buttons));
        BindText(typeof(Texts));

        GetButton((int)Buttons.AdReviveButton).onClick.AddListener(OnAdReviveClicked);
        GetButton((int)Buttons.DiamondReviveButton).onClick.AddListener(OnDiamondReviveClicked);
        GetButton((int)Buttons.GiveupButton).onClick.AddListener(OnGiveupClicked);
        
        RefreshDiamondText();

        return true;
    }

    private void RefreshDiamondText()
    {
        if (DataManager.Instance != null && DataManager.Instance.currentUserData != null)
        {
            int diamondCount = DataManager.Instance.currentUserData.Diamond;
            GetText((int)Texts.DiamondText).text = diamondCount.ToString();
        }
    }

    private void OnAdReviveClicked()
    {
        // TODO: 광고 시청 로직 연동
        Debug.Log("광고 시청 후 부활!");
        RevivePlayer();
    }

    private void OnDiamondReviveClicked()
    {
        int cost = 50; // TODO: 기획에 맞게 필요한 다이아 개수 수정
        if (DataManager.Instance != null && DataManager.Instance.currentUserData.Diamond >= cost)
        {
            DataManager.Instance.currentUserData.Diamond -= cost;
            // TODO: 서버 혹은 로컬 저장 로직 (예: DataManager.Instance.SaveGameData();)
            
            Debug.Log($"다이아 {cost}개 사용 후 부활! (남은 다이아: {DataManager.Instance.currentUserData.Diamond})");
            RevivePlayer();
        }
        else
        {
            Debug.LogWarning("다이아가 부족합니다!");
            // TODO: 다이아 부족 안내 팝업 등 처리
        }
    }

    private void OnGiveupClicked()
    {
        UIManager.Instance.CloseTopPopup();
        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null) gm.ShowResultPopup(false);
    }

    private void RevivePlayer()
    {
        var entityQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerData>());
        if (entityQuery.HasSingleton<PlayerData>())
        {
            var pEntity = entityQuery.GetSingletonEntity();
            
            // HealthData 복구
            if (_entityManager.HasComponent<HealthData>(pEntity))
            {
                var healthData = _entityManager.GetComponentData<HealthData>(pEntity);
                healthData.CurrentHealth = healthData.MaxHealth;
                healthData.InvincibilityTimer = 3.0f; // 부활 후 무적 3초
                _entityManager.SetComponentData(pEntity, healthData);
            }

            // VisualAnimationState 부활 처리
            if (_entityManager.HasComponent<VisualAnimationState>(pEntity))
            {
                var animState = _entityManager.GetComponentData<VisualAnimationState>(pEntity);
                animState.IsDead = false;
                animState.TriggerSummon = true; // 부활 연출용 (선택)
                _entityManager.SetComponentData(pEntity, animState);
            }
            
            // State Tags 처리
            if (_entityManager.HasComponent<DeathProcessedTag>(pEntity))
            {
                _entityManager.RemoveComponent<DeathProcessedTag>(pEntity);
            }
            if (_entityManager.HasComponent<DeathTag>(pEntity))
            {
                _entityManager.RemoveComponent<DeathTag>(pEntity);
            }

            // DeathTimer 초기화
            var playerData = _entityManager.GetComponentData<PlayerData>(pEntity);
            playerData.DeathTimer = 0f;
            _entityManager.SetComponentData(pEntity, playerData);
        }
        
        UIManager.Instance.CloseTopPopup();
        Time.timeScale = 1f;
    }
}
