using UnityEngine;

public class TelegraphUI : MonoBehaviour
{
    public enum TelegraphShape { Cone, Box, Arrow }
    
    [Header("Shape Settings")]
    public TelegraphShape Shape;
    public float RadiusOrLength = 10f; // 부채꼴의 반지름, 사각형/화살표의 최대 거리
    public float AngleOrWidth = 90f;   // 부채꼴의 각도, 사각형/화살표의 폭

    [Header("Colors")]
    public Color BaseColor = new Color(1f, 0f, 0f, 0.15f); // 배경 (연한 붉은색)
    public Color FillColor = new Color(1f, 0f, 0f, 0.45f); // 차오르는 부분 (진한 붉은색)

    private float _duration;
    private float _timer;
    private Transform _attachTo;
    private Vector3 _positionOffset;
    private bool _isTracking;

    private GameObject _fillObj;

    public void Setup(Transform target, Vector3 posOffset, float duration, bool trackTarget)
    {
        _attachTo = target;
        _positionOffset = posOffset;
        _duration = duration;
        _isTracking = trackTarget;

        if (_attachTo != null)
        {
            transform.position = _attachTo.position + _positionOffset;
            transform.rotation = _attachTo.rotation;
        }

        GenerateMeshes();
        _timer = 0f; // 타이머 초기화
    }

    private void GenerateMeshes()
    {
        // 코드에서 폴리곤 꼭짓점을 계산해 즉석으로 도형을 만듭니다.
        Mesh mesh = BuildMesh();

        // 1. 기반 전체 범위를 표시할 연한색 객체
        GameObject baseObj = new GameObject("Base");
        baseObj.transform.SetParent(transform);
        baseObj.transform.localPosition = Vector3.zero;
        baseObj.transform.localRotation = Quaternion.identity;
        baseObj.AddComponent<MeshFilter>().mesh = mesh;
        var baseRenderer = baseObj.AddComponent<MeshRenderer>();
        // Sprites/Default 셰이더는 양면 렌더링에 투명도를 자체 지원하여 완벽합니다.
        baseRenderer.material = new Material(Shader.Find("Sprites/Default")) { color = BaseColor };

        // 2. 시간이 지남에 따라 차오를 진한색 객체
        _fillObj = new GameObject("Fill");
        _fillObj.transform.SetParent(transform);
        _fillObj.transform.localPosition = new Vector3(0, 0.05f, 0); // 겹침 깜빡임(Z-Fighting) 방지
        _fillObj.transform.localRotation = Quaternion.identity;
        _fillObj.AddComponent<MeshFilter>().mesh = mesh;
        var fillRenderer = _fillObj.AddComponent<MeshRenderer>();
        fillRenderer.material = new Material(Shader.Find("Sprites/Default")) { color = FillColor };
        
        // 처음엔 스케일 0 (안 보임)
        _fillObj.transform.localScale = Vector3.zero; 
    }

    private Mesh BuildMesh()
    {
        Mesh mesh = new Mesh();
        if (Shape == TelegraphShape.Cone)
        {
            // 부채꼴: 각도에 맞춰 원둘레 정점 생성
            int segments = 30; // 등분 수 (높을 수록 부드러운 곡선)
            Vector3[] verts = new Vector3[segments + 2];
            int[] tris = new int[segments * 3];

            verts[0] = Vector3.zero;
            float startAngle = -AngleOrWidth / 2f;
            float angleStep = AngleOrWidth / segments;

            for (int i = 0; i <= segments; i++)
            {
                float rad = (startAngle + angleStep * i) * Mathf.Deg2Rad;
                verts[i + 1] = new Vector3(Mathf.Sin(rad) * RadiusOrLength, 0, Mathf.Cos(rad) * RadiusOrLength);
            }

            for (int i = 0; i < segments; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
        }
        else if (Shape == TelegraphShape.Box)
        {
            // 도끼 투척 궤적: 직사각형
            Vector3[] verts = new Vector3[4];
            float halfW = AngleOrWidth / 2f;
            verts[0] = new Vector3(-halfW, 0, 0);
            verts[1] = new Vector3(-halfW, 0, RadiusOrLength);
            verts[2] = new Vector3(halfW, 0, RadiusOrLength);
            verts[3] = new Vector3(halfW, 0, 0);

            mesh.vertices = verts;
            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        }
        else if (Shape == TelegraphShape.Arrow)
        {
            // 돌진: 전방 화살표
            Vector3[] verts = new Vector3[7];
            float halfW = AngleOrWidth / 4f;
            float headStart = RadiusOrLength * 0.7f;
            float headW = AngleOrWidth / 1.5f;

            verts[0] = new Vector3(-halfW, 0, 0);
            verts[1] = new Vector3(-halfW, 0, headStart);
            verts[2] = new Vector3(-headW, 0, headStart);
            verts[3] = new Vector3(0, 0, RadiusOrLength);
            verts[4] = new Vector3(headW, 0, headStart);
            verts[5] = new Vector3(halfW, 0, headStart);
            verts[6] = new Vector3(halfW, 0, 0);

            mesh.vertices = verts;
            mesh.triangles = new int[] { 0, 1, 6, 6, 1, 5, 2, 3, 4 };
        }
        
        mesh.RecalculateNormals();
        return mesh;
    }

    private void Update()
    {
        if (_duration <= 0f) return;

        _timer += Time.deltaTime;
        float progress = Mathf.Clamp01(_timer / _duration);

        if (_fillObj != null)
        {
            if (Shape == TelegraphShape.Cone)
            {
                // 원뿔(근접)은 안쪽에서 바깥쪽으로 사방으로 커짐
                _fillObj.transform.localScale = new Vector3(progress, 1f, progress);
            }
            else
            {
                // 사각형(도끼)이나 화살표(돌진)는 보스 위치부터 전방(Z축)으로만 쭉 뻗어나감
                _fillObj.transform.localScale = new Vector3(1f, 1f, progress);
            }
        }

        if (_isTracking && _attachTo != null)
        {
            transform.position = _attachTo.position + _positionOffset;
            transform.rotation = _attachTo.rotation;
        }

        // 도달 시 삭제 (애니메이션 OnAttackHit 타이밍 일치)
        if (_timer >= _duration)
        {
            Destroy(gameObject);
        }
    }
}
