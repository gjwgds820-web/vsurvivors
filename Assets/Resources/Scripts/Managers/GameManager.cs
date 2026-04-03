using UnityEngine;
using Unity.Entities;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    private EntityManager _entityManager;
    private EntityQuery _levelUpQuery;
    private EntityQuery _goldLootQuery;
    private EntityQuery _elementAscensionQuery;

    private int _pendingLevelUps = 0;
    private EntityQuery _playerDeathQuery;
    private EntityQuery _gameClearQuery;

    private List<SkillData> currentShadows = new List<SkillData>();
    public List<SkillData> CurrentShadows => currentShadows;
    private List<SkillData> currentPassives = new List<SkillData>();
    public List<SkillData> CurrentPassives => currentPassives;
    private List<SkillData> availableSkills = new List<SkillData>();
    private const int MAX_SLOTS = 6;

    // 현재 보유 중인 원소 (최대 2개)
    public List<int> SelectedElements { get; private set; } = new List<int>();

    void Start()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _levelUpQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<LevelUpEventTag>());
        _goldLootQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GoldEventTag>());
        _elementAscensionQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<ElementAscensionEventTag>());
        _playerDeathQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerDeathEventTag>());
        _gameClearQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameClearEventTag>());

        InitializeAvailableSkills();

        // 씬 내의 다른 Manager/System들까지 Start()가 끝날 시간을 보장하기 위해 한 프레임 대기 후 로딩을 종료할 수 있도록 Coroutine 사용
        StartCoroutine(FinishLoadingCoroutine());
    }

    private System.Collections.IEnumerator FinishLoadingCoroutine()
    {
        // 1. 최소 1프레임은 명시적으로 양보
        yield return null;

        // 2. ECS 세상에 PlayerData(플레이어 엔티티)가 로드될 때까지 대기
        EntityQuery playerQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerData>());
        
        while (playerQuery.CalculateEntityCount() == 0)
        {
            yield return null;
        }
        
        // 3. 플레이어가 스폰되고 시스템들이 첫 업데이트를 돌 수 있도록 약간의 유예(1~2 프레임 분량)
        yield return new WaitForSeconds(0.1f);
        
        if (VSurvivors.Managers.LoadingManager.Instance != null)
        {
            VSurvivors.Managers.LoadingManager.Instance.FinishLoading();
        }
    }

    private void InitializeAvailableSkills()
    {
        availableSkills.Clear();

        List<int> deckIDs = DataManager.Instance.currentUserData.SelectedShadowsID;

        foreach (int id in deckIDs)
        {
            if (DataManager.Instance.SkillDict.TryGetValue(id, out SkillData skill))
            {
                availableSkills.Add(skill);
            }
        }
        availableSkills.AddRange(DataManager.Instance.GetAllPassiveSkills());
    }

    void Update()
    {
        if (!_levelUpQuery.IsEmptyIgnoreFilter)
        {
            var levelUpEvents = _levelUpQuery.ToComponentDataArray<LevelUpEventTag>(Unity.Collections.Allocator.Temp);
            foreach (var ev in levelUpEvents)
            {
                _pendingLevelUps += ev.Count;
            }
            levelUpEvents.Dispose();
            _entityManager.RemoveComponent<LevelUpEventTag>(_levelUpQuery);
        }

        // 버퍼에 대기중인 레벨업이 있고, 팝업이 안열려있다면(시간이 흐르고 있다면) 시작
        if (_pendingLevelUps > 0 && Time.timeScale > 0f)
        {
            _pendingLevelUps--;
            Time.timeScale = 0f;
            ShowSkillSelectionPopup();
        }

        if (!_goldLootQuery.IsEmptyIgnoreFilter)
        {
            var goldEvents = _goldLootQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var goldEvent in goldEvents)
            {
                var goldAmount = _entityManager.GetComponentData<GoldEventTag>(goldEvent).amount;
                DataManager.Instance.currentUserData.AddGold(goldAmount);
            }
            goldEvents.Dispose();
            _entityManager.RemoveComponent<GoldEventTag>(_goldLootQuery);
        }

        if (!_elementAscensionQuery.IsEmptyIgnoreFilter)
        {
            Time.timeScale = 0f;

            ShowElementAscensionPopup();
            _entityManager.RemoveComponent<ElementAscensionEventTag>(_elementAscensionQuery);
        }

        if (!_playerDeathQuery.IsEmptyIgnoreFilter)
        {
            Time.timeScale = 0f;
            ShowResultPopup(false);
            _entityManager.RemoveComponent<PlayerDeathEventTag>(_playerDeathQuery);
        }

        if (!_gameClearQuery.IsEmptyIgnoreFilter)
        {
            Time.timeScale = 0f;
            ShowResultPopup(true);
            _entityManager.RemoveComponent<GameClearEventTag>(_gameClearQuery);
        }
    }

    void ShowSkillSelectionPopup()
    {
        // Debug.Log("[GameManager] ShowSkillSelectionPopup Started.");
        GenerateSkillOptions();
        // Debug.Log("[GameManager] Calling UIManager.Instance.ShowPopup");
        UIManager.Instance.ShowPopup("UI_SkillSelectionPopup");
        // Debug.Log("[GameManager] ShowSkillSelectionPopup Ended.");
    }

    void ShowElementAscensionPopup()
    {
        UIManager.Instance.ShowPopup("UI_ElementAscensionPopup");
    }

    void ShowResultPopup(bool isVictory)
    {
        UIManager.Instance.ShowPopup("UI_ResultPopup");
        
        // Find the instantiated UI_ResultPopup to set up its content
        var popup = FindAnyObjectByType<UI_ResultPopup>();
        if (popup != null)
        {
            popup.Setup(isVictory);
        }
        else
        {
            Debug.LogError("UI_ResultPopup not found after calling ShowPopup!");
        }
    }

    public void GenerateSkillOptions()
    {
        // 조건에 맞는 Pool 가져오기
        List<SkillData> validPool = GetValidSkillPool();
        // Debug.Log($"[GameManager] GenerateSkillOptions: validPool.Count = {validPool.Count}");

        // 풀에서 중복 없이 3개 뽑기
        List<SkillData> selectedOptions = new List<SkillData>();

        // 풀이 비어있지 않다면 최대 3개까지 추출
        while (selectedOptions.Count < 3 && validPool.Count > 0)
        {
            int randomIndex = Random.Range(0, validPool.Count);
            SkillData pick = validPool[randomIndex];

            selectedOptions.Add(pick);
            validPool.RemoveAt(randomIndex);
        }

        // 뽑힌 스킬 반환
        if (selectedOptions.Count == 0) { Debug.Log("No skills"); }

        DataManager.Instance.SelectedOptions = selectedOptions;
    }
    
    public List<SkillData> GetValidSkillPool()
    {
        List<SkillData> filteredPool = new List<SkillData>();

        foreach (SkillData skill in availableSkills)
        {
            if (skill.Type == SkillType.Shadow)
            {
                bool isOwned = currentShadows.Exists(s => s.ID == skill.ID);

                if (isOwned)
                {
                    int currentLevel = GetCurrentSkillLevel(skill.ID, currentShadows);
                    
                    // 기본 최대 레벨은 5로 제한
                    int limitLevel = 5;
                    
                    // 만약 보유중인 원소 리스트에 이 스킬이 요구하는 원소가 있다면 MaxLevel(6)로 해방
                    // ShadowDict에서 해당하는 원소를 찾아옵니다.
                    bool hasMatchingElement = false;
                    if (DataManager.Instance.ShadowDict.TryGetValue(skill.ID, out ShadowData shadowData))
                    {
                        hasMatchingElement = SelectedElements.Contains((int)shadowData.Element);
                    }
                    
                    if (skill.MaxLevel > 5 && hasMatchingElement)
                    {
                        limitLevel = skill.MaxLevel;
                    }

                    if (currentLevel < limitLevel) filteredPool.Add(skill);
                }
                else
                {
                    if (currentShadows.Count < MAX_SLOTS) filteredPool.Add(skill);
                }
            }
            else
            {
                bool isOwned = currentPassives.Exists(s => s.ID == skill.ID);

                if (isOwned)
                {
                    int currentLevel = GetCurrentSkillLevel(skill.ID, currentPassives);
                    if (currentLevel < skill.MaxLevel) filteredPool.Add(skill);
                }
                else
                {
                    if (currentPassives.Count < MAX_SLOTS) filteredPool.Add(skill);
                }
            }
        }
        return filteredPool;
    }

    private int GetCurrentSkillLevel(int skillID, List<SkillData> skillList)
    {
        var exist = skillList.Find(s => s.ID == skillID);
        return exist != null ? exist.CurrentLevel : 0;
    }

    public SkillData RerollSingleOption(SkillData oldSkill, List<SkillData> currentlyDisplayedOptions)
    {
        List<SkillData> validPool = GetValidSkillPool();

        // 현재 화면에 표시중인 3개 중 방금 버린 스킬을 제외
        validPool.RemoveAll(s => currentlyDisplayedOptions.Exists(disp => disp.ID == s.ID) || s.ID == oldSkill.ID);

        // 주석 복구됨
        if (validPool.Count == 0)
        {
            Debug.Log("대체할 스킬이 없습니다");
            return oldSkill;
        }

        // (주석 복구됨)
        int randomIndex = Random.Range(0, validPool.Count);
        return validPool[randomIndex];
    }

    public SkillData LevelUp(SkillData selectedSkill)
    {
        SkillData ownedSkill = null;
        if (selectedSkill.Type == SkillType.Shadow)
        {
            ownedSkill = currentShadows.Find(s => s.ID == selectedSkill.ID);
            if (ownedSkill != null) 
            {
                ownedSkill.CurrentLevel++;
            }
            else 
            {
                selectedSkill.CurrentLevel = 1; // 1레벨로 초기화
                currentShadows.Add(selectedSkill);
                ownedSkill = selectedSkill;
            }
        }
        else
        {
            ownedSkill = currentPassives.Find(s => s.ID == selectedSkill.ID);   
            if (ownedSkill != null) 
            {
                ownedSkill.CurrentLevel++;
            }
            else 
            {
                selectedSkill.CurrentLevel = 1; // 1레벨로 초기화
                currentPassives.Add(selectedSkill);
                ownedSkill = selectedSkill;
            }
        }

        return ownedSkill;
    }

    public void OnSkillSelectionComplete(SkillData selectedSkill)
    {
        LevelUp(selectedSkill);
        UIManager.Instance.CloseTopPopup();

        if (_pendingLevelUps > 0)
        {
            // 바로 다음 팝업을 띄우기 위해 펜딩 횟수 감소 후 재생성
            _pendingLevelUps--;
            ShowSkillSelectionPopup();
        }
        else
        {
            Time.timeScale = 1f;
        }
    }
}






