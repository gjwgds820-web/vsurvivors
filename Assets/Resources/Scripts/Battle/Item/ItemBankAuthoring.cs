using Unity.Entities;
using UnityEngine;

public class ItemBankAuthoring : MonoBehaviour
{
    [Header("Drop Prefabs")]
    [SerializeField] private GameObject _expPrefab;
    [SerializeField] private GameObject _goldPrefab;
    [SerializeField] private GameObject _magnetPrefab;
    [SerializeField] private GameObject _bombPrefab;

    class Baker : Baker<ItemBankAuthoring>
    {
        public override void Bake(ItemBankAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new DropBankData
            {
                ExpPrefab = GetEntity(authoring._expPrefab, TransformUsageFlags.Dynamic),
                GoldPrefab = GetEntity(authoring._goldPrefab, TransformUsageFlags.Dynamic),
                MagnetPrefab = GetEntity(authoring._magnetPrefab, TransformUsageFlags.Dynamic),
                BombPrefab = GetEntity(authoring._bombPrefab, TransformUsageFlags.Dynamic)
            });
        }
    }
}