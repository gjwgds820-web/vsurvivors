using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

public class ItemMagnetAuthoring : MonoBehaviour
{
    class Baker : Baker<ItemMagnetAuthoring>
    {
        public override void Bake(ItemMagnetAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new DroppedItemData
            {
                Type = DropItemType.Magnet,
                Amount = 0,
                MoveSpeed = 1,
                IsAttracted = false
            });
        }
    }
}