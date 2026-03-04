using UnityEngine;
using UnityEngine.UI;

public class BottomMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraController cameraController;
    [SerializeField] private GameObject[] menuButtons;

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;

    private void Start()
    {
        // 버튼에 클릭 이벤트 등록
        for (int i = 0; i < menuButtons.Length; i++)
        {
            int index = i; // 클로저 문제 방지
            Button button = menuButtons[i].GetComponentInChildren<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() => OnButtonClicked(index));
            }
        }

        // 카메라 섹션 변경 이벤트 구독
        if (cameraController != null)
        {
            cameraController.OnSectionChanged += UpdateMenuButtonVisuals;
            // 초기 상태 업데이트
            UpdateMenuButtonVisuals(cameraController.GetCurrentSection());
        }
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제
        if (cameraController != null)
        {
            cameraController.OnSectionChanged -= UpdateMenuButtonVisuals;
        }
    }

    private void OnButtonClicked(int index)
    {
        // cameraController의 OnSectionButtonClick 메서드를 호출하여 카메라 이동
        if (cameraController != null)
        {
            cameraController.OnSectionButtonClick(index);
        }
    }

    private void UpdateMenuButtonVisuals(int selectedIndex)
    {
        // 모든 버튼의 색상을 초기화
        for (int i = 0; i < menuButtons.Length; i++)
        {
            Image buttonImage = menuButtons[i].GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = (i == selectedIndex) ? selectedColor : normalColor;
            }

            menuButtons[i].transform.localScale = (i == selectedIndex) ? Vector3.one * 1.2f : Vector3.one;
        }
    }
}