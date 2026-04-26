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
        // 鍮뚮낫????븷: 移대찓?쇰? 諛붾씪蹂닿쾶 ?ㅼ젙
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }
    }
}

