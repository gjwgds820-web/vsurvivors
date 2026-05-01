using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class UI_BottomUI : UI_Base
{
    enum Texts
    {
        LevelText,
    }

    enum Sliders
    {
        ExpSlider,
        GhostSlider,
    }

    private EntityManager _entityManager;
    private EntityQuery _playerQuery;

    // 슬라이더 보간을 위한 변수
    private float _targetExpRatio = 0f;
    private float _currentExpRatio = 0f;
    private float _currentGhostRatio = 0f;

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindText(typeof(Texts));
        BindSlider(typeof(Sliders));

        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _playerQuery = _entityManager.CreateEntityQuery(typeof(PlayerData));

        // 슬라이더 초기값 0 세팅
        GetSlider((int)Sliders.ExpSlider).value = 0f;
        GetSlider((int)Sliders.GhostSlider).value = 0f;

        return true;
    }

    private void Start()
    {
        Init();
    }

    private void Update()
    {
        if (!_init) return;

        UpdateExpData();
        AnimateSliders();
    }

    private void UpdateExpData()
    {
        if (_playerQuery.HasSingleton<PlayerData>())
        {
            var playerData = _playerQuery.GetSingleton<PlayerData>();

            // 레벨 텍스트 적용
            GetText((int)Texts.LevelText).text = $"Level : {playerData.Level}";

            // 다음 레벨업 요구 경험치 (ECS 산식과 일치시킴)
            float requiredExp = PlayerLevelUpSystem.GetRequiredExpForNextLevel(playerData.Level);

            // 현재 경험치 %
            _targetExpRatio = requiredExp > 0f ? playerData.EXP / requiredExp : 0f;

            // 레벨업 직후 EXP가 0(또는 초과치)으로 돌아가며 %가 크게 떨어진 경우 슬라이더 값 강제 동기화 
            // (레벨업 시 경험치 바가 뒤로 천천히 되감기는 것을 방지)
            if (_targetExpRatio < _currentGhostRatio - 0.1f)
            {
                _currentExpRatio = _targetExpRatio;
                _currentGhostRatio = _targetExpRatio;
            }
        }
    }

    private void AnimateSliders()
    {
        float deltaTime = Time.unscaledDeltaTime; // 멈춘 상태(레벨업 팝업)에서도 부드럽게 원한다면 unscaled 사용하거나 Time.deltaTime 사용 설정

        // 1. 고스트 슬라이더가 목표(Target)를 향해 빠르게 먼저 이동
        _currentGhostRatio = Mathf.Lerp(_currentGhostRatio, _targetExpRatio, deltaTime * 15f);

        // 2. 실제 경험치 바(ExpSlider)는 고스트를 쫓아 천천히 움직임
        _currentExpRatio = Mathf.Lerp(_currentExpRatio, _currentGhostRatio, deltaTime * 5f);
        
        // 아주 미세한 차이는 스냅 처리
        if (Mathf.Abs(_targetExpRatio - _currentGhostRatio) < 0.001f)
            _currentGhostRatio = _targetExpRatio;
        if (Mathf.Abs(_currentGhostRatio - _currentExpRatio) < 0.001f)
            _currentExpRatio = _currentGhostRatio;

        // UI 적용
        GetSlider((int)Sliders.GhostSlider).value = _currentGhostRatio;
        GetSlider((int)Sliders.ExpSlider).value = _currentExpRatio;
    }
}
