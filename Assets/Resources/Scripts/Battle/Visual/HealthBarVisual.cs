using UnityEngine;
using UnityEngine.UI;

public class HealthBarVisual : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Canvas canvas;

    private Transform _mainCameraTransform;

    private void Awake()
    {
        if (canvas == null)
            canvas = GetComponent<Canvas>();
            
        if (Camera.main != null)
            _mainCameraTransform = Camera.main.transform;

        if (canvas != null && _mainCameraTransform != null)
            canvas.worldCamera = Camera.main;
    }

    public void UpdateHealth(float currentHealth, float maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;

            // 체력이 0 이하면 체력바 숨김 처리
            healthSlider.gameObject.SetActive(currentHealth > 0);
        }
    }

    private void LateUpdate()
    {
        // 빌보딩 (항상 카메라 방향을 바라보도록 회전)
        if (_mainCameraTransform != null)
        {
            transform.forward = _mainCameraTransform.forward;
        }
        else if (Camera.main != null)
        {
            _mainCameraTransform = Camera.main.transform;
        }
    }
}
