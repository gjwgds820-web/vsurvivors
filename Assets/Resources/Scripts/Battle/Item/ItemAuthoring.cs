using Unity.Entities;
using UnityEngine;

public class ItemAuthoring : MonoBehaviour
{
    public DropItemType ItemType;
    public float Amount = 10f;
    public float MoveSpeed = 1f;

    public class ItemBaker : Baker<ItemAuthoring>
    {
        public override void Bake(ItemAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new DroppedItemData
            {
                Type = authoring.ItemType,
                Amount = authoring.Amount,
                MoveSpeed = authoring.MoveSpeed,
                IsAttracted = false
            });
        }
    }
}
