using UnityEngine;

public class VisualManager : MonoBehaviour
{
    public static VisualManager Instance;

    [Header("Visual Prefabs")]
    public GameObject GateVisualPrefab;
    public GameObject EnemyVisualPrefab;
    public GameObject ShadowVisualPrefab;

    private void Awake()
    {
        Instance = this;
    }
}