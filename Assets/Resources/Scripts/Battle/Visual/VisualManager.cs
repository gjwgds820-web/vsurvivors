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

    [Header("Telegraph Prefabs")]
    public GameObject TelegraphConePrefab;
    public GameObject TelegraphBoxPrefab;
    public GameObject TelegraphArrowPrefab;

    [Header("Effect & HitBox Prefabs")]
    [Tooltip("Add visual prefabs for hitboxes and projectiles here. Use the index as the PrefabID in EffectVisualInfo.")]
    public GameObject[] EffectVisualPrefabs;

    private void Awake()
    {
        Instance = this;
    }

    public void SpawnTelegraph(Transform bossTransform, int attackIndex, float duration)
    {
        GameObject prefabToSpawn = null;
        bool isTracking = false;

        switch (attackIndex)
        {
            case 0: // Melee (Cone)
                prefabToSpawn = TelegraphConePrefab;
                isTracking = false; // 부채꼴은 보스 위치 시점에 고정
                break;
            case 1: // Dash (Arrow)
                prefabToSpawn = TelegraphArrowPrefab;
                isTracking = true;  // 화살표는 보스 이동을 실시간 추적
                break;
            case 2: // AxeThrow (Box)
                prefabToSpawn = TelegraphBoxPrefab;
                isTracking = false; // 투척 투사체 경로 고정
                break;
        }

        if (prefabToSpawn != null && duration > 0f)
        {
            GameObject telegraphGo = Instantiate(prefabToSpawn, bossTransform.position, bossTransform.rotation);
            TelegraphUI telegraphUI = telegraphGo.GetComponent<TelegraphUI>();
            if (telegraphUI != null)
            {
                // 약간 바닥에서 위로 띄움 (Y 0.2)
                telegraphUI.Setup(bossTransform, new Vector3(0, 0.2f, 0), duration, isTracking);
            }
        }
    }
}
