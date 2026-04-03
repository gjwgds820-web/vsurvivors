using UnityEngine;
using TMPro;

public class GateUI : MonoBehaviour
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
        if (textCount != null)
        {
            textCount.text = $"{currentAbsorbed}/{currentRequired}";
        }
    }

    private void Update()
    {
        // 빌보드 역할: 카메라를 바라보게 설정
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }
    }
}
