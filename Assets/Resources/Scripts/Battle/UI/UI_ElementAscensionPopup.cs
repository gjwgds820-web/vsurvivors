using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class UI_ElementAscensionPopup : UI_Base
{
    enum Texts
    {
        DiamondText,
        RerollText,
        RerollCostText
    }

    enum Buttons
    {
        ElementButton1,
        ElementButton2,
        RerollButton
    }

    enum GameObjects
    {
        Slot1,
        Slot2,
        ShadowContainer1,
        ShadowContainer2,
        ShadowSlotFrame,
        PassiveSlotFrame,
    }

    enum Images
    {
        ElementIcon1,
        ElementIcon2,
    }

    private GameObject _shadowIconPrefab;
    private GameObject _ownedSkillSlotPrefab;

    private int _remainRerollCount = 3;
    private readonly int[] _rerollCosts = { 100, 200, 300 };
    private int _currentDiamond;
    private GameManager _gameManager;

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindText(typeof(Texts));
        BindButton(typeof(Buttons));
        BindObject(typeof(GameObjects));
        BindImage(typeof(Images));

        _ownedSkillSlotPrefab = Resources.Load<GameObject>("UI/SubItem/UI_OwnedSkillSlot");
        _gameManager = FindAnyObjectByType<GameManager>();

        GetButton((int)Buttons.RerollButton).onClick.AddListener(OnClickReroll);
        GetButton((int)Buttons.ElementButton1).onClick.AddListener(() => OnClickElement(0));
        GetButton((int)Buttons.ElementButton2).onClick.AddListener(() => OnClickElement(1));

        SetupPopup();

        return true;
    }

    public void SetupPopup()
    {
        _remainRerollCount = 3;
        UpdateRerollUI();
        UpdateDiamondUI();
        LoadOwnedSkillsBottom();

        StartCoroutine(SpinSlotsRoutine());
    }

    private IEnumerator SpinSlotsRoutine()
    {
        SetPanelsInteractable(false);

        // 슬롯이 돌아가는 애니메이션 재생 (예시로 2초간)
        
        yield return new WaitForSeconds(2f);

        int element1ID = Random.Range(0, 5); // 예시로 5개의 원소 중 랜덤 선택
        int element2ID = Random.Range(0, 5);

        UpdateElementPanel(0, element1ID);
        UpdateElementPanel(1, element2ID);

        SetPanelsInteractable(true);
    }

    private void UpdateElementPanel(int panelIndex, int elementID)
    {
        // 아이콘
        Image elementIcon = GetImage(panelIndex == 0 ? (int)Images.ElementIcon1 : (int)Images.ElementIcon2);
        elementIcon.sprite = ResourceManager.Instance.LoadSprite($"Icons/Elements/Element_{elementID}");

        // 초월 가능한 그림자 목록 초기화
        GameObject container = GetObject(panelIndex == 0 ? (int)GameObjects.ShadowContainer1 : (int)GameObjects.ShadowContainer2);
        foreach (Transform child in container.transform)
        {
            Destroy(child.gameObject);
        }

        // 그릠자 아이콘 세팅
        int[] temp = { 40000001, 40000002, 40000003, 40000004 }; // 예시로 원소별 초월 가능한 그림자 ID
        foreach (var shadowID in temp)
        {
            if (_shadowIconPrefab == null)
            {
                _shadowIconPrefab = Resources.Load<GameObject>($"UI/SubItem/UI_OwnedSkillSlot");
            }

            GameObject iconObj = Instantiate(_shadowIconPrefab, container.transform);
            iconObj.GetComponent<Image>().sprite = ResourceManager.Instance.LoadSprite($"Icons/Shadows/Shadow_{shadowID-40000000}");

            bool isOwned = _gameManager.CurrentShadows.Exists(s => s.ID == shadowID);
            if (!isOwned)
            {
                iconObj.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 1f);
            }
        }
    }

    private void OnClickReroll()
    {
        if (_remainRerollCount <= 0) return;

        int costIndex = 3 - _remainRerollCount;
        int cost = _rerollCosts[costIndex];

        if (_currentDiamond < cost)
        {
            Debug.Log("다이아가 부족합니다!");
            return;
        }

        _currentDiamond -= cost;
        _remainRerollCount--;

        UpdateDiamondUI();
        UpdateRerollUI();

        StartCoroutine(SpinSlotsRoutine());
    }

    private void OnClickElement(int panelIndex)
    {
        Debug.Log($"원소 {panelIndex + 1} 선택됨");

        Time.timeScale = 1f;
        UIManager.Instance.CloseTopPopup();
    }

    private void UpdateRerollUI()
    {
        GetText((int)Texts.RerollText).text = $"남은 리롤 : {_remainRerollCount}/3";

        if (_remainRerollCount > 0)
        {
            int nextCost = _rerollCosts[3 - _remainRerollCount];
            GetText((int)Texts.RerollCostText).text = $"리롤 비용 : {nextCost}";
            GetButton((int)Buttons.RerollButton).interactable = true;
        }
        else
        {
            GetText((int)Texts.RerollCostText).text = "리롤 불가";
            GetButton((int)Buttons.RerollButton).interactable = false;
        }
    }

    private void LoadOwnedSkillsBottom()
    {
        Transform shadowGroup = GetObject((int)GameObjects.ShadowSlotFrame).transform;
        Transform passiveGroup = GetObject((int)GameObjects.PassiveSlotFrame).transform;
        List<SkillData> currentShadows = _gameManager.CurrentShadows;
        List<SkillData> currentPassives = _gameManager.CurrentPassives;
        foreach (SkillData shadow in currentShadows)
        {
            UI_OwnedSkillSlot slot = Instantiate(_ownedSkillSlotPrefab, shadowGroup).GetComponent<UI_OwnedSkillSlot>();
            slot.SetSkill(shadow);
        }
        foreach (SkillData passive in currentPassives)
        {
            UI_OwnedSkillSlot slot = Instantiate(_ownedSkillSlotPrefab, passiveGroup).GetComponent<UI_OwnedSkillSlot>();
            slot.SetSkill(passive);
        }
    }

    private void UpdateDiamondUI()
    {
        GetText((int)Texts.DiamondText).text = _currentDiamond.ToString();
    }

    private void SetPanelsInteractable(bool interactable)
    {
        GetButton((int)Buttons.ElementButton1).interactable = interactable;
        GetButton((int)Buttons.ElementButton2).interactable = interactable;
        GetButton((int)Buttons.RerollButton).interactable = interactable && _remainRerollCount > 0;
    }
}