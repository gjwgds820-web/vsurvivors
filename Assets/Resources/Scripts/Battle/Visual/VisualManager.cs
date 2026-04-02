using UnityEngine;

public class VisualManager : MonoBehaviour
{
    public static VisualManager Instance;

    [Header("Visual Prefabs")]
    public GameObject GateVisualPrefab;
    public GameObject EnemyVisualPrefab;
    public GameObject BossVisualPrefab;
    public GameObject ShadowVisualPrefab;

    public GameObject ExpVisualPrefab;
    public GameObject GoldVisualPrefab;
    public GameObject MagnetVisualPrefab;
    public GameObject BombVisualPrefab;

    private void Awake()
    {
        Instance = this;
    }
}