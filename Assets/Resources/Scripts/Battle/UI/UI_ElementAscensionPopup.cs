using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using DG.Tweening;

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
        Slot1Reel,
        Slot2Reel,
        ShadowContainer1,
        ShadowContainer2,
    }

    enum Images
    {
        ElementIcon1,
        ElementIcon2,
    }

    private GameObject _shadowIconPrefab;

    private int _remainRerollCount = 3;
    private readonly int[] _rerollCosts = { 100, 200, 300 };
    private int _currentDiamond;
    private GameManager _gameManager;

    private int _element1ID = -1;
    private int _element2ID = -1;

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindText(typeof(Texts));
        BindButton(typeof(Buttons));
        BindObject(typeof(GameObjects));
        BindImage(typeof(Images));

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

        SpinSlots();
    }

    private void SpinSlots()
    {
        SetPanelsInteractable(false);

        // 슬롯 릴(Reel)의 RectTransform 가져오기
        RectTransform slot1Reel = GetObject((int)GameObjects.Slot1Reel).GetComponent<RectTransform>();
        RectTransform slot2Reel = GetObject((int)GameObjects.Slot2Reel).GetComponent<RectTransform>();

        // 시작 전 초기 위치 0으로 셋팅 (Y축을 원래 자리로 돌려놓음)
        slot1Reel.anchoredPosition = new Vector2(slot1Reel.anchoredPosition.x, 0);
        slot2Reel.anchoredPosition = new Vector2(slot2Reel.anchoredPosition.x, 0);

        // 결과로 선택될 원소 ID (클래스 변수에 저장하여 OnClickElement에서 접근 가능하게)
        _element1ID = Random.Range(0, 5); 
        _element2ID = Random.Range(0, 5);

        // --- 슬롯 애니메이션 설정 ---
        // 슬롯 높이 1칸의 픽셀값 (실제 에디터 상의 간격에 맞게 조정하세요)
        float iconHeight = 250f; 

        // 슬롯이 몇 칸이나 돌아갈지 결정 (슬롯2가 조금 더 돌게)
        int spinCount1 = Random.Range(15, 20);
        int spinCount2 = Random.Range(20, 25);

        // 아래쪽으로 띠가 내려가는 효과라면 목표 Y값은 보통 양수입니다. (반대라면 마이너스 처리)
        float targetY1 = spinCount1 * iconHeight;
        float targetY2 = spinCount2 * iconHeight;

        float duration = 1.5f;

        // 슬롯 1번 돌리기
        // Ease.OutBack: 목표지점을 살짝 지나친 후 뒤로 튕기는(브레이크 걸리는) 연출
        slot1Reel.DOAnchorPosY(targetY1, duration).SetEase(Ease.OutBack);

        // 슬롯 2번 돌리기 & 끝난 후(OnComplete) 결과 반영
        slot2Reel.DOAnchorPosY(targetY2, duration + 0.3f).SetEase(Ease.OutBack).OnComplete(() =>
        {
            // 애니메이션이 끝나면 원래 만들어두신 UpdateElementPanel 함수 실행!
            UpdateElementPanel(0, _element1ID);
            UpdateElementPanel(1, _element2ID);

            SetPanelsInteractable(true);
        });
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

        SpinSlots();
    }

    private void OnClickElement(int panelIndex)
    {
        Debug.Log($"원소 {panelIndex + 1} 선택됨");

        int selectedElement = panelIndex == 0 ? _element1ID : _element2ID;
        
        // GameManager의 선택된 원소 리스트에 추가
        if (!_gameManager.SelectedElements.Contains(selectedElement))
        {
            _gameManager.SelectedElements.Add(selectedElement);
        }

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
        UI_OwnedSkillsPanel panel = GetComponentInChildren<UI_OwnedSkillsPanel>();
        if (panel != null)
        {
            panel.RefreshUI();
        }
        else
        {
            Debug.LogWarning("[UI_ElementAscensionPopup] UI_OwnedSkillsPanel을 찾을 수 없습니다.");
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