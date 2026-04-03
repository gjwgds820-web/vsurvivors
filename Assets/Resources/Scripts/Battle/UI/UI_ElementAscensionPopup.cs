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
        // 버티컬 레이아웃의 자식 요소 하나의 높이(250) + 간격(Spacing 40) = 290
        float iconHeight = 290f; 

        // 슬롯이 몇 칸이나 돌아갈지 결정
        // 릴의 이미지들이 위쪽으로 0, 1, 2, 3, 4 순서로 배치되어 반복된다고 가정했을 때,
        // 애니메이션이 무작위 바퀴수(5의 배수)를 돌고 정확히 해당 원소 ID의 위치에서 멈추도록 강제합니다.
        int baseSpins1 = Random.Range(3, 5) * 5; // 15 or 20칸 (3~4바퀴)
        int baseSpins2 = Random.Range(4, 6) * 5; // 20 or 25칸 (4~5바퀴)

        int spinCount1 = baseSpins1 + _element1ID;
        int spinCount2 = baseSpins2 + _element2ID;

        // 아래쪽으로 띠가 내려가는 효과라면 목표 Y값은 보통 양수입니다. (반대라면 마이너스 처리)
        float targetY1 = spinCount1 * iconHeight;
        float targetY2 = spinCount2 * iconHeight;

        float duration = 1.5f;

        // 슬롯 1번 돌리기
        // Ease.OutBack: 목표지점을 살짝 지나친 후 뒤로 튕기는(브레이크 걸리는) 연출
        // 팝업이 뜰 때 Time.timeScale = 0 상태일 수 있으므로 SetUpdate(true)를 추가해 타임스케일을 무시하도록 설정합니다.
        slot1Reel.DOAnchorPosY(targetY1, duration).SetEase(Ease.OutBack).SetUpdate(true);

        // 슬롯 2번 돌리기 & 끝난 후(OnComplete) 결과 반영
        slot2Reel.DOAnchorPosY(targetY2, duration + 0.3f).SetEase(Ease.OutBack).SetUpdate(true).OnComplete(() =>
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
            
            // 프리팹이 UI에 비해 너무 크다면 localScale을 조절하여 크기를 맞춰줍니다. (필요 시 0.6f 값을 변경)
            iconObj.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            
            UI_OwnedSkillSlot slotUI = iconObj.GetComponent<UI_OwnedSkillSlot>();
            
            if (_gameManager == null) _gameManager = FindAnyObjectByType<GameManager>();

            SkillData ownedShadow = null;
            if (_gameManager != null && _gameManager.CurrentShadows != null)
            {
                ownedShadow = _gameManager.CurrentShadows.Find(s => s.ID == shadowID);
            }

            if (slotUI != null && ownedShadow != null)
            {
                // 보유 시: 기존 스킬 슬롯 UI 로직(레벨별 별 모양 포함) 그대로 사용
                slotUI.SetSkill(ownedShadow);
                // 추가적으로 아이콘 딤(Dim) 해제
                Transform skillIconTransform = iconObj.transform.Find("SkillIconImage");
                if (skillIconTransform != null) skillIconTransform.GetComponent<Image>().color = Color.white;
            }
            else
            {
                // 미보유 시: 별 아이콘이나 빈 슬롯 이미지 끄고, 아이콘만 어둡게 세팅
                Transform emptyObj = iconObj.transform.Find("EmptyObject");
                if (emptyObj != null) emptyObj.gameObject.SetActive(false);

                // 별만 모여있는 부모를 끄던가, UI_OwnedSkillSlot 방식에 맞춰 별 이미지를 모두 끔
                for (int i = 1; i <= 5; i++)
                {
                    Transform starObj = iconObj.transform.Find($"Star{i}");
                    if (starObj != null) starObj.gameObject.SetActive(false);
                }
                
                // 불필요한 Stars 부모 찾기 (있다면)
                Transform starsObj = iconObj.transform.Find("Stars");
                if (starsObj != null) starsObj.gameObject.SetActive(false);

                Transform skillIconTransform = iconObj.transform.Find("SkillIconImage");
                Image imgComponent = skillIconTransform != null ? skillIconTransform.GetComponent<Image>() : iconObj.GetComponentInChildren<Image>();
                
                if (imgComponent != null)
                {
                    imgComponent.sprite = ResourceManager.Instance.LoadSprite($"Icons/Shadows/Shadow_{shadowID-40000000}");
                    imgComponent.gameObject.SetActive(true);
                    imgComponent.color = new Color(0.3f, 0.3f, 0.3f, 1f); // 미보유 어둡게 처리
                }
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