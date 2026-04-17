using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;

public class UI_StagePopup : UI_Base, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    public static event System.Action OnStageChanged;
    enum Texts
    {
        StageIndexText,
        StageNameText
    }

    enum Buttons
    {
        SelectButton,
        BackButton,
        NextStageButton,
        PreviousStageButton
    }

    enum Images
    {
        StageImage
    }

    private int _currentStageID;
    private List<int> _availableStages;
    private CameraController _cameraController;
    private Vector2 _dragStartPos;
    [SerializeField] private float _dragThreshold = 150f;

    public override bool Init()
    {
        if (base.Init() == false)
            return false;
        
        BindText(typeof(Texts));
        BindButton(typeof(Buttons));
        BindImage(typeof(Images));
        _cameraController = FindAnyObjectByType<CameraController>();

        GetButton((int)Buttons.SelectButton).onClick.AddListener(OnSelectButtonClicked);
        GetButton((int)Buttons.BackButton).onClick.AddListener(OnBackButtonClicked);
        GetButton((int)Buttons.NextStageButton).onClick.AddListener(OnNextButtonClicked);
        GetButton((int)Buttons.PreviousStageButton).onClick.AddListener(OnPrevButtonClicked);

        _availableStages = DataManager.Instance.currentUserData.UnlockedStages;
        _currentStageID = DataManager.Instance.currentUserData.CurrentStage;
        UpdateStageInfo();

        return true;
    }

    private void UpdateStageInfo()
    {
        if (DataManager.Instance.StageDict.TryGetValue(_currentStageID, out var stageData))
        {
            GetText((int)Texts.StageIndexText).text = $"Stage {stageData.Name}";
            GetText((int)Texts.StageNameText).text = $"{stageData.Name}";
        }
        else
        {
            GetText((int)Texts.StageIndexText).text = $"Stage {_currentStageID}";
            GetText((int)Texts.StageNameText).text = $"Stage Name {_currentStageID}";
        }

        GetImage((int)Images.StageImage).sprite = ResourceManager.Instance.LoadSprite($"UI/Stages/Thumbnails/{_currentStageID}");
        
        int currentIndex = _availableStages.IndexOf(_currentStageID);
        if (currentIndex <= 0)
        {
            GetButton((int)Buttons.PreviousStageButton).gameObject.SetActive(false);
        }
        else
        {
            GetButton((int)Buttons.PreviousStageButton).gameObject.SetActive(true);
        }
        if (currentIndex >= _availableStages.Count - 1)
        {
            GetButton((int)Buttons.NextStageButton).gameObject.SetActive(false);
        }
        else
        {
            GetButton((int)Buttons.NextStageButton).gameObject.SetActive(true);
        }
    }

    private void OnSelectButtonClicked()
    {
        _cameraController.BlockInput(false);
        DataManager.Instance.currentUserData.CurrentStage = _currentStageID;
        UpdateStageInfo();
        OnStageChanged?.Invoke();
        UIManager.Instance.CloseTopPopup();
        // DataManager.Instance.SaveGame();
    }

    private void OnBackButtonClicked()
    {
        _cameraController.BlockInput(false);
        UIManager.Instance.CloseTopPopup();
    }

    private void OnNextButtonClicked()
    {
        int currentIndex = _availableStages.IndexOf(_currentStageID);
        if (currentIndex < _availableStages.Count - 1)
        {
            _currentStageID = _availableStages[currentIndex + 1];
            UpdateStageInfo();
        }
    }

    private void OnPrevButtonClicked()
    {
        int currentIndex = _availableStages.IndexOf(_currentStageID);
        if (currentIndex > 0)
        {
            _currentStageID = _availableStages[currentIndex - 1];
            UpdateStageInfo();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragStartPos = eventData.position;
        Debug.Log($"Drag started at: {_dragStartPos}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log($"Drag ended at: {eventData.position}");
        Vector2 dragEndPos = eventData.position;
        float dragDistance = Vector2.Distance(_dragStartPos, dragEndPos);

        if (dragDistance > _dragThreshold)
        {
            if (dragEndPos.x < _dragStartPos.x)
            {
                OnNextButtonClicked();
            }
            else
            {
                OnPrevButtonClicked();
            }
        }
    }
}