using UnityEngine;
using Unity.Entities;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    private EntityManager _entityManager;
    private EntityQuery _levelUpQuery;
    private EntityQuery _goldLootQuery;

    private List<SkillData> currentShadows = new List<SkillData>();
    private List<SkillData> currentPassives = new List<SkillData>();
    private List<SkillData> availableSkills = new List<SkillData>();
    private const int MAX_SLOTS = 6;

    void Start()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _levelUpQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<LevelUpEventTag>());
        _goldLootQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GoldEventTag>());

        InitializeAvailableSkills();
    }

    private void InitializeAvailableSkills()
    {
        availableSkills.Clear();

        List<int> deckIDs = DataManager.Instance.currentUserData.SelectedShadows;

        foreach (int id in deckIDs)
        {
            SkillData skill = DataManager.Instance.GetSkillData(id);
            if (skill != null)
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
            Time.timeScale = 0f;

            ShowSkillSelectionPopup();
            _entityManager.RemoveComponent<LevelUpEventTag>(_levelUpQuery);
        }

        if (!_goldLootQuery.IsEmptyIgnoreFilter)
        {
            var goldEvents = _goldLootQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var goldEvent in goldEvents)
            {
                var goldAmount = _entityManager.GetComponentData<GoldEventTag>(goldEvent).amount;
                DataManager.Instance.currentUserData.AddGold(goldAmount);
            }
            _entityManager.RemoveComponent<GoldEventTag>(_goldLootQuery);
        }
    }

    void ShowSkillSelectionPopup()
    {
        UIManager.Instance.ShowPopup("UI_SkillSelectionPopup");
        GenerateSkillOptions();
    }

    public void GenerateSkillOptions()
    {
        // 조건에 맞는 Pool 가져오기
        List<SkillData> validPool = GetValidSkillPool();

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

        // 뽑힌 스킬이 없다면?
        if (selectedOptions.Count == 0)
        {
            Debug.Log("더 이상 습득할 스킬이 없습니다!");
            return;
        }

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
                    if (currentLevel < skill.MaxLevel) filteredPool.Add(skill);
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

        // 현재 화면에 표시중인 3개와 방금 버린 스킬은 풀에서 제외
        validPool.RemoveAll(s => currentlyDisplayedOptions.Exists(disp => disp.ID == s.ID) || s.ID == oldSkill.ID);

        // 남은 풀이 없다면 원래 스킬을 돌려줌
        if (validPool.Count == 0)
        {
            Debug.Log("대체할 스킬이 없습니다");
            return oldSkill;
        }

        // 남은 풀에서 1개 리턴
        int randomIndex = Random.Range(0, validPool.Count);
        return validPool[randomIndex];
    }
}