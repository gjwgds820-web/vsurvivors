using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

public class ItemGoldAuthoring : MonoBehaviour
{
    class Baker : Baker<ItemGoldAuthoring>
    {
        public override void Bake(ItemGoldAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new DroppedItemData
            {
                Type = DropItemType.Gold,
                Amount = 10,
                MoveSpeed = 1,
                IsAttracted = false
            });
        }
    }
}