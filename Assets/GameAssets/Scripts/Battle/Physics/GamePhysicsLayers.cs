using Unity.Physics;

namespace VSurvivors.Battle.Physics
{
    public static class GamePhysicsLayers
    {
        // Category Bits (BelongsTo) - 각 객체의 고유 비트값
        public const uint None       = 0;
        public const uint Player     = 1 << 0; // 1
        public const uint Enemy      = 1 << 1; // 2
        public const uint Shadow     = 1 << 2; // 4
        public const uint Structure  = 1 << 3; // 8
        public const uint Hitbox     = 1 << 4; // 16
        public const uint Item       = 1 << 5; // 32
        public const uint All        = ~0u;    // 0xFFFFFFFF

        // Collision Masks (CollidesWith) - 충돌할 대상 비트값의 합

        // 적, 그림자는 Structure와 Hitbox랑만 충돌
        public const uint EnemyMask  = Structure | Hitbox;
        
        // 플레이어는 추가적으로 아이템과도 충돌 (획득)
        public const uint PlayerMask = Structure | Hitbox | Item;

        // 아이템은 플레이어 및 구조물(바닥/벽)과 충돌 (평상시)
        public const uint ItemMask   = Player | Structure;

        // 아이템이 자석에 끌려가거나 습득 중일 때 (플이어를 밀어내지 않도록 물리 충돌 완전 무시)
        public const uint ItemMagnetizedMask = None;

        // 구조물(Structure)은 물리 월드 내 거의 모든 것과 충돌
        public const uint StructureMask = Player | Enemy | Shadow | Hitbox | Structure | Item;

        // 공격 판정 (Hitbox)은 움직이는 객체들과 구조물 양쪽과 다 충돌
        public const uint HitboxMask = Player | Enemy | Shadow | Structure;
    }
}