using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

public class ItemBombAuthoring : MonoBehaviour
{
    class Baker : Baker<ItemBombAuthoring>
    {
        public override void Bake(ItemBombAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new DroppedItemData
            {
                Type = DropItemType.Bomb,
                Amount = 0,
                MoveSpeed = 1,
                IsAttracted = false
            });
        }
    }
}