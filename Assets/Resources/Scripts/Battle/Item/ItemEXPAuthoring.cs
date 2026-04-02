using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

public class ItemEXPAuthoring : MonoBehaviour
{
    class Baker : Baker<ItemEXPAuthoring>
    {
        public override void Bake(ItemEXPAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new DroppedItemData
            {
                Type = DropItemType.Exp,
                Amount = 10,
                MoveSpeed = 1,
                IsAttracted = false
            });
        }
    }
}