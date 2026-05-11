#if UNITY_EDITOR
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEditor;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class RangeVisualizationSystem : SystemBase
{
    private Material _lineMaterial;

    protected override void OnCreate()
    {
        base.OnCreate();
        
        // Shader를 사용한 간단한 Unlit 라인용 매터리얼 생성. 
        // Game 뷰 컬러를 온전히 그리기 위해 ZTest를 끄거나 조정할 수도 있습니다.
        var shader = Shader.Find("Hidden/Internal-Colored");
        _lineMaterial = new Material(shader);
        _lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        // Turn on alpha blending
        _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        // Turn backface culling off
        _lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        // Turn off depth writes
        _lineMaterial.SetInt("_ZWrite", 0);
    }

    protected override void OnUpdate()
    {
        bool showPlayerLeash = EditorPrefs.GetBool("ShowPlayerLeash", false);
        bool showShadowRange = EditorPrefs.GetBool("ShowShadowRange", false);
        bool showEnemyRange = EditorPrefs.GetBool("ShowEnemyRange", false);

        if (!showPlayerLeash && !showShadowRange && !showEnemyRange) return;

        int segments = 36;
        float angleInc = math.PI * 2f / segments;

        // GL 좌표계로 선을 직접 그림 (OnRenderObject나 Camera 이벤트 대신 Update 단에서 GL을 쌓으려면 
        // Graphics.DrawMesh 등 다른 방법이 필요할 수 있으나, 보통 게임뷰 시각화를 위해서는 OnPostRender 등이 사용됨.
        // 현재는 편의상 OnUpdate 시점에 Debug.DrawLine 대신 시스템상 컴포넌트에 Mesh/LineRenderer를 직접 넣는 방식을 취하거나, 
        // 여기선 MonoBehaviour 헬퍼 훅을 써서 렌더링 파이프라인에 탑승합니다.)
        
        // => 가장 쉬운 방법: 기존 Entity의 SubSceneVisualModel 트랜스폼 하위에 LineRenderer를 동적 삽입/업데이트
        DoLineRendererLogic(showPlayerLeash, showShadowRange, showEnemyRange, segments, angleInc);
    }

    private void DoLineRendererLogic(bool showPlayerLeash, bool showShadowRange, bool showEnemyRange, int segments, float angleInc)
    {
        if (showPlayerLeash)
        {
            foreach (var (visualModel, entity) in SystemAPI.Query<SubSceneVisualModel>().WithAll<PlayerData>().WithEntityAccess())
            {
                UpdateLineRenderer(visualModel.Value.gameObject, "Gizmo_PlayerLeash", 15f, Color.green, segments, angleInc, true);
            }
        }
        else
        {
            foreach (var (visualModel, entity) in SystemAPI.Query<SubSceneVisualModel>().WithAll<PlayerData>().WithEntityAccess())
            {
                UpdateLineRenderer(visualModel.Value.gameObject, "Gizmo_PlayerLeash", 0, Color.black, segments, angleInc, false);
            }
        }

        if (showShadowRange)
        {
            foreach (var (visualModel, targeting, entity) in SystemAPI.Query<SubSceneVisualModel, RefRO<TargetingData>>().WithAll<ShadowTag>().WithEntityAccess())
            {
                float range = math.sqrt(targeting.ValueRO.MaxSearchRangeSq);
                UpdateLineRenderer(visualModel.Value.gameObject, "Gizmo_ShadowRange", range, Color.cyan, segments, angleInc, true);
            }
        }
        else
        {
            foreach (var (visualModel, entity) in SystemAPI.Query<SubSceneVisualModel>().WithAll<ShadowTag>().WithEntityAccess())
            {
                UpdateLineRenderer(visualModel.Value.gameObject, "Gizmo_ShadowRange", 0, Color.black, 36, 0, false);
            }
        }

        if (showEnemyRange)
        {
            foreach (var (visualModel, targeting, entity) in SystemAPI.Query<SubSceneVisualModel, RefRO<TargetingData>>().WithAll<EnemyTag>().WithEntityAccess())
            {
                float range = math.sqrt(targeting.ValueRO.MaxSearchRangeSq);
                UpdateLineRenderer(visualModel.Value.gameObject, "Gizmo_EnemyRange", range, Color.red, segments, angleInc, true);
            }
        }
        else
        {
            foreach (var (visualModel, entity) in SystemAPI.Query<SubSceneVisualModel>().WithAll<EnemyTag>().WithEntityAccess())
            {
                UpdateLineRenderer(visualModel.Value.gameObject, "Gizmo_EnemyRange", 0, Color.black, 36, 0, false);
            }
        }
    }

    private void UpdateLineRenderer(GameObject parentGo, string childName, float radius, Color color, int segments, float angleInc, bool isActive)
    {
        if (parentGo == null) return;

        Transform childTr = parentGo.transform.Find(childName);
        if (!isActive)
        {
            if (childTr != null) childTr.gameObject.SetActive(false);
            return;
        }

        LineRenderer lr;
        if (childTr == null)
        {
            GameObject newGo = new GameObject(childName);
            newGo.transform.SetParent(parentGo.transform);
            newGo.transform.localPosition = new Vector3(0, 0.2f, 0); // 살짝 띄움
            newGo.transform.localRotation = Quaternion.identity;
            newGo.SetActive(true);

            lr = newGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; // 부모 따라가도록
            lr.loop = true;
            lr.startWidth = 0.1f;
            lr.endWidth = 0.1f;
            lr.material = _lineMaterial;
            lr.positionCount = segments;
        }
        else
        {
            childTr.gameObject.SetActive(true);
            lr = childTr.GetComponent<LineRenderer>();
        }

        lr.startColor = color;
        lr.endColor = color;

        Vector3[] points = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleInc;
            points[i] = new Vector3(math.cos(angle) * radius, 0, math.sin(angle) * radius);
        }
        lr.SetPositions(points);
    }
}
#endif