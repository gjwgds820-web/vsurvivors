using UnityEngine;
using TMPro;

public class PortalUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textCount;
    private int currentRequired = 3;
    private int currentAbsorbed = 0;

    public void Setup(int requiredCount)
    {
        currentRequired = requiredCount;
        currentAbsorbed = 0;
        UpdateUI();
    }

    public void UpdateAbsorbed(int absorbedAmount)
    {
        if (currentAbsorbed != absorbedAmount)
        {
            currentAbsorbed = absorbedAmount;
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (currentRequired == 0)
        {
            if (textCount != null) textCount.gameObject.SetActive(false);
            return;
        }
        else
        {
            if (textCount != null) textCount.gameObject.SetActive(true);
        }

        if (textCount != null)
        {
            textCount.text = $"{currentAbsorbed}/{currentRequired}";
        }
    }

    private void Update()
    {
        // 빌보드 역할: 카메라를 바라보게 설정 (비주얼 루트 전체가 아닌 텍스트 객체만 회전)
        if (Camera.main != null && textCount != null)
        {
            if(textCount.transform.parent != null)
                textCount.transform.parent.rotation = Camera.main.transform.rotation;
            else
                textCount.transform.rotation = Camera.main.transform.rotation;
        }
    }
}

