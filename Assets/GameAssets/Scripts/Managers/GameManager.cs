using UnityEngine;
using Unity.Entities;
using System.Collections.Generic;

using Cysharp.Threading.Tasks;

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

    private float _baseMaxHealth = -1f;
    private float _baseMoveSpeed = -1f;
    private float _baseMaxShadow = -1f;

    void Start()
    {
        _baseMaxHealth = -1f;
        _baseMoveSpeed = -1f;
        _baseMaxShadow = -1f;

        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _levelUpQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<LevelUpEventTag>());
        _goldLootQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GoldEventTag>());
        _elementAscensionQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<ElementAscensionEventTag>());
        _playerDeathQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerDeathEventTag>());
        _gameClearQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameClearEventTag>());

        InitializeAvailableSkills();

        // 씬 내의 다른 Manager/System들까지 Start()가 끝날 시간을 보장하기 위해 대기 후 로딩 종료
        FinishLoadingAsync().Forget();
    }

    private async UniTaskVoid FinishLoadingAsync()
    {
        // 1. 최소 1프레임은 명시적으로 양보
        await UniTask.Yield();

        // 2. ECS 세상에 PlayerData(플레이어 엔티티)가 로드될 때까지 대기
        EntityQuery playerQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerData>());
        
        await UniTask.WaitUntil(() => playerQuery.CalculateEntityCount() > 0);
        
        // 3. 플레이어가 스폰되고 시스템들이 첫 업데이트를 돌 수 있도록 약간의 유예
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.1f));
        
        if (VSurvivors.Managers.LoadingManager.Instance != null)
        {
            VSurvivors.Managers.LoadingManager.Instance.FinishLoading();
        }
        
        SyncShadowSkillsToPlayer();
    }

    public void SyncShadowSkillsToPlayer()
    {
        EntityQuery playerQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerData>());
        if (playerQuery.CalculateEntityCount() == 0) return;

        Entity playerEntity = playerQuery.GetSingletonEntity();
        
        if (_entityManager.HasBuffer<ActiveShadowSkillElement>(playerEntity))
        {
            DynamicBuffer<ActiveShadowSkillElement> buffer = _entityManager.GetBuffer<ActiveShadowSkillElement>(playerEntity);
            buffer.Clear();
            
            foreach (var shadow in currentShadows)
            {
                if (int.TryParse(shadow.Value, out int shadowID))
                {
                    buffer.Add(new ActiveShadowSkillElement { ShadowID = shadowID });
                }
            }
        }
    }

    public void SyncPassiveSkillsToPlayer()
    {
        EntityQuery playerQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadWrite<PlayerData>(), 
            ComponentType.ReadWrite<PlayerMovementData>(), 
            ComponentType.ReadWrite<HealthData>());
            
        if (playerQuery.CalculateEntityCount() == 0) return;

        Entity playerEntity = playerQuery.GetSingletonEntity();
        var playerData = _entityManager.GetComponentData<PlayerData>(playerEntity);
        var moveData = _entityManager.GetComponentData<PlayerMovementData>(playerEntity);
        var healthData = _entityManager.GetComponentData<HealthData>(playerEntity);

        if (_baseMaxHealth < 0f)
        {
            _baseMaxHealth = healthData.MaxHealth;
            _baseMoveSpeed = moveData.MoveSpeed;
            _baseMaxShadow = playerData.MaxShadow; // QA: 인스펙터 베이킹 값을 초기값으로 보존
        }

        float hpAdd = 0f;
        float speedMult = 0f;
        float shadowAdd = 0f;

        foreach (var passive in currentPassives)
        {
            if (passive.Stats == "hp" && float.TryParse(passive.Value, out float hpVal)) hpAdd += hpVal;
            if (passive.Stats == "speed" && float.TryParse(passive.Value, out float spdVal)) speedMult += spdVal;
            if (passive.Stats == "shadow_max" && float.TryParse(passive.Value, out float shdVal)) shadowAdd += shdVal;
        }

        playerData.MaxShadow = _baseMaxShadow + shadowAdd;
        moveData.MoveSpeed = _baseMoveSpeed * (1f + speedMult);
        
        float oldMax = healthData.MaxHealth;
        healthData.MaxHealth = _baseMaxHealth + hpAdd;
        if (healthData.MaxHealth > oldMax) 
        {
            healthData.CurrentHealth += (healthData.MaxHealth - oldMax);
        }
        else if (healthData.CurrentHealth > healthData.MaxHealth)
        {
            healthData.CurrentHealth = healthData.MaxHealth;
        }

        _entityManager.SetComponentData(playerEntity, playerData);
        _entityManager.SetComponentData(playerEntity, moveData);
        _entityManager.SetComponentData(playerEntity, healthData);
    }

    private void InitializeAvailableSkills()
    {
        availableSkills.Clear();
        currentShadows.Clear();
        currentPassives.Clear();

        List<int> formationIDs = DataManager.Instance.currentUserData.FormationData.ContainsKey(0) 
                                 ? DataManager.Instance.currentUserData.FormationData[0] 
                                 : new List<int>();

        List<int> deckGroupIDs = new List<int>();

        // 편성된 그림자 ID(21010101 등)를 이용해 덱에 포함시킬 스킬 그룹 ID 매핑
        foreach (int shadowID in formationIDs)
        {
            if (shadowID == 0) continue;

            int targetVal = shadowID == 21010101 ? 21010102 : shadowID;

            foreach (var kvp in DataManager.Instance.SkillDict)
            {
                if (kvp.Value.Value == targetVal.ToString())
                {
                    if (!deckGroupIDs.Contains(kvp.Value.GroupID))
                    {
                        deckGroupIDs.Add(kvp.Value.GroupID);

                        if (shadowID == 21010101)
                        {
                            // 기본 그림자는 1레벨(혹은 그와 동등한 장착상태)로 시작하므로 가짜 SkillData나 1레벨 취급으로 Add
                            // 여기서는 targetVal(2렙 데이터)을 바탕으로 1레벨 형태의 더미를 만들거나
                            // 아니면 2레벨의 데이터를 참고해 GroupID만 맞추고 레벨 1이라고 명시한 카피를 넣을 수도 있음.
                            // 가장 쉬운 것은, 레벨을 1로 임시 지정한 클론 객체를 만들어 currentShadows에 넣는 것입니다.
                            SkillData dummyLv1 = new SkillData
                            {
                                ID = kvp.Value.ID - 1,
                                GroupID = kvp.Value.GroupID,
                                Level = 1,
                                MaxLevel = kvp.Value.MaxLevel,
                                Type = kvp.Value.Type,
                                Value = "21010101",
                                Name = "기본 그림자(1)",
                                Description = "기본 그림자 LV1"
                            };
                            currentShadows.Add(dummyLv1);
                            
                            if (DataManager.Instance.ShadowDict.TryGetValue(shadowID, out ShadowData sData))
                            {
                                int element = (int)sData.Element;
                                if (element != 0 && !SelectedElements.Contains(element)) 
                                {
                                    SelectedElements.Add(element);
                                }
                            }
                        }
                    }
                    break;
                }
            }
        }
        
        foreach (var kvp in DataManager.Instance.SkillDict)
        {
            SkillData skill = kvp.Value;
            if (skill.Type == SkillType.Shadow)
            {
                if (deckGroupIDs.Contains(skill.GroupID) || deckGroupIDs.Contains(skill.ID))
                {
                    availableSkills.Add(skill);
                }
            }
            else
            {
                availableSkills.Add(skill); // Passive etc.
            }
        }
    }

    void Update()
    {
        if (World.DefaultGameObjectInjectionWorld == null || !World.DefaultGameObjectInjectionWorld.IsCreated) return;

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

            var playerQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerData>());
            if (playerQuery.HasSingleton<PlayerData>())
            {
                var playerEntity = playerQuery.GetSingletonEntity();
                var playerData = _entityManager.GetComponentData<PlayerData>(playerEntity);

                if (playerData.DeathCount == 0)
                {
                    playerData.DeathCount++;
                    _entityManager.SetComponentData(playerEntity, playerData);

                    // 첫 사망 시 부활 팝업 띄우기
                    UIManager.Instance.ShowPopup("UI_RevivePopup");
                }
                else
                {
                    // 두 번째 사망 시 결과창
                    ShowResultPopup(false);
                }
            }
            else
            {
                ShowResultPopup(false);
            }

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

    public void ShowResultPopup(bool isVictory)
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

        // 1순위: 속성 초월 우대. 6레벨 스킬이 풀에 있다면 첫 번째 슬롯에 확정 배치 (여러 개일 경우 난수 1개)
        List<SkillData> transcendenceSkills = validPool.FindAll(s => s.Level == 6);
        if (transcendenceSkills.Count > 0)
        {
            int randomIndex = Random.Range(0, transcendenceSkills.Count);
            SkillData pick = transcendenceSkills[randomIndex];
            selectedOptions.Add(pick);
            validPool.Remove(pick);
        }

        // 풀이 비어있지 않다면 최대 3개까지 나머지 자리 추출
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
        
        // Add only the next sequence levels for grouped skills. 
        // Group by GroupID to unique skills
        Dictionary<int, List<SkillData>> groupedSkills = new Dictionary<int, List<SkillData>>();
        foreach (var s in availableSkills)
        {
            if (!groupedSkills.ContainsKey(s.GroupID)) groupedSkills[s.GroupID] = new List<SkillData>();
            groupedSkills[s.GroupID].Add(s);
        }

        foreach (var kvp in groupedSkills)
        {
            int groupID = kvp.Key;
            List<SkillData> groupList = kvp.Value;
            
            // Sort by level just in case
            groupList.Sort((a, b) => a.Level.CompareTo(b.Level));
            
            SkillData firstSkill = groupList[0];
            bool isShadow = firstSkill.Type == SkillType.Shadow;
            List<SkillData> targetList = isShadow ? currentShadows : currentPassives;
            
            // Find existing skill
            SkillData existingSkill = targetList.Find(s => s.GroupID == groupID);
            
            if (existingSkill != null)
            {
                int currentLevel = existingSkill.Level; // or existingSkill.CurrentLevel
                int maxAllowedLevel = firstSkill.MaxLevel;
                
                // Shadow element check 
                if (isShadow && maxAllowedLevel > 5)
                {
                    bool hasMatchingElement = false;
                    if (DataManager.Instance.ShadowDict.TryGetValue(existingSkill.ID, out ShadowData shadowData))
                    {
                        hasMatchingElement = SelectedElements.Contains((int)shadowData.Element);
                    }
                    if (!hasMatchingElement) maxAllowedLevel = 5;
                }

                if (currentLevel < maxAllowedLevel)
                {
                    // Find next level skill data
                    SkillData nextSkill = groupList.Find(s => s.Level == currentLevel + 1);
                    if (nextSkill != null)
                    {
                        filteredPool.Add(nextSkill);
                    }
                }
            }
            else
            {
                // Not owned
                if (targetList.Count < MAX_SLOTS)
                {
                    filteredPool.Add(groupList[0]); // Lowest level
                }
            }
        }
        
        return filteredPool;
    }

    private int GetCurrentSkillLevel(int groupID, List<SkillData> skillList)
    {
        var exist = skillList.Find(s => s.GroupID == groupID);
        return exist != null ? exist.Level : 0;
    }

    public SkillData RerollSingleOption(SkillData oldSkill, List<SkillData> currentlyDisplayedOptions)
    {
        List<SkillData> validPool = GetValidSkillPool();

        // 현재 화면에 표시중인 3개 중 방금 버린 스킬을 제외
        validPool.RemoveAll(s => currentlyDisplayedOptions.Exists(disp => disp.GroupID == s.GroupID) || s.GroupID == oldSkill.GroupID);

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
        List<SkillData> targetList = selectedSkill.Type == SkillType.Shadow ? currentShadows : currentPassives;

        ownedSkill = targetList.Find(s => s.GroupID == selectedSkill.GroupID);
        if (ownedSkill != null) 
        {
            targetList.Remove(ownedSkill);
        }
        
        targetList.Add(selectedSkill);
        ownedSkill = selectedSkill;

        if (selectedSkill.Type == SkillType.Shadow)
        {
            SyncShadowSkillsToPlayer();
        }
        else if (selectedSkill.Type == SkillType.Passive)
        {
            SyncPassiveSkillsToPlayer();
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






