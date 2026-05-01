using Unity.Entities;
using UnityEngine;
using TMPro;

public class UI_TopUI : UI_Base
{
    enum Texts
    {
        CounterText,
        TimerText,
        ShadowCounterText,
    }

    enum Buttons
    {
        PauseButton,
    }

    private EntityManager _entityManager;
    private EntityQuery _enemyQuery;
    private EntityQuery _shadowQuery;
    private EntityQuery _gameDirectorQuery;

    private float _updateTimer = 0f;
    private const float UPDATE_INTERVAL = 0.1f;

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindText(typeof(Texts));
        BindButton(typeof(Buttons));

        // ECS 환경 세팅
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        
        // 쿼리 생성 (DeathTag가 없는 살아있는 개체만 카운트)
        _enemyQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc 
        {
            All = new ComponentType[] { typeof(EnemyTag) },
            None = new ComponentType[] { typeof(DeathTag) }
        });
        
        _shadowQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc 
        {
            All = new ComponentType[] { typeof(CShadowData) },
            None = new ComponentType[] { typeof(DeathTag) }
        });
        
        _gameDirectorQuery = _entityManager.CreateEntityQuery(typeof(GameDirectorData));

        // 버튼 이벤트 등록
        GetButton((int)Buttons.PauseButton).onClick.AddListener(OnPauseButtonClicked);

        return true;
    }

    private void Start()
    {
        Init();
    }

    private void Update()
    {
        if (!_init) return;

        // 0.1초마다 UI 업데이트 갱신 (성능 최적화)
        _updateTimer += Time.unscaledDeltaTime;
        if (_updateTimer >= UPDATE_INTERVAL)
        {
            _updateTimer = 0f;
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        // 적 개수 업데이트
        int enemyCount = _enemyQuery.CalculateEntityCount();
        GetText((int)Texts.CounterText).text = enemyCount.ToString();

        // 그림자 개수 업데이트
        int shadowCount = _shadowQuery.CalculateEntityCount();
        GetText((int)Texts.ShadowCounterText).text = $"Shadow : {shadowCount}";

        // 타임 업데이트
        if (_gameDirectorQuery.HasSingleton<GameDirectorData>())
        {
            var dirData = _gameDirectorQuery.GetSingleton<GameDirectorData>(); float timeToShow = dirData.CurrentPhase == GamePhase.BossFight ? dirData.BossTimer : dirData.GlobalTimer;
            int minutes = Mathf.FloorToInt(timeToShow / 60f);
            int seconds = Mathf.FloorToInt(timeToShow % 60f); GetText((int)Texts.TimerText).color = dirData.CurrentPhase == GamePhase.BossFight ? Color.red : Color.white;
            GetText((int)Texts.TimerText).text = $"{minutes:00}:{seconds:00}";
        }
    }

    private void OnPauseButtonClicked()
    {
        if (UIManager.Instance == null)
        {
            Debug.LogWarning("UIManager.Instance가 존재하지 않습니다. 정상적인 루트(로비 씬)를 통해 게임을 실행했는지 확인해주세요.");
            return;
        }

        UIManager.Instance.ShowPopup("UI_PausePopup");
    }
}
