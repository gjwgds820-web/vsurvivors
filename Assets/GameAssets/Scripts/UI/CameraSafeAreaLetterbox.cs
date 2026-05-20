using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraSafeAreaLetterbox : MonoBehaviour
{
    private Camera _camera;
    private Rect _lastSafeArea = new Rect(0, 0, 0, 0);

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        ApplySafeArea();
    }

    private void Update()
    {
        // 화면 전환이나 해상도 변경으로 Safe Area가 바뀌면 업데이트
        if (_lastSafeArea != Screen.safeArea)
        {
            ApplySafeArea();
        }
    }

    private void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        _lastSafeArea = safeArea;

        // 모바일 기기의 전체 화면 해상도 대비 Safe Area의 비율 계산
        float x = safeArea.x / Screen.width;
        float y = safeArea.y / Screen.height;
        float w = safeArea.width / Screen.width;
        float h = safeArea.height / Screen.height;

        // 카메라가 렌더링하는 뷰포트 영역(Rect)을 Safe Area 비율로 축소
        // 렌더링되지 않는 빈 공간(노치 등)은 레터박스(기본적으로 검은색 배경)처럼 남게 됩니다.
        _camera.rect = new Rect(x, y, w, h);
    }
}
