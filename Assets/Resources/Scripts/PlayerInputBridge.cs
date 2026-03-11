using TMPro;
using Unity.Entities;
using UnityEngine;

public class SubSceneVisualModel : IComponentData
{
    public Transform Value;
}
public class PlayerInputBridge : MonoBehaviour
{
    [SerializeField] private FloatingJoystick joystick;
    [SerializeField] private GameObject visualModel;
    private EntityManager _em;
    private EntityQuery _playerInputQuery;
    private bool _isLinked = false;

    private void Start()
    {
        _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        _playerInputQuery = _em.CreateEntityQuery(typeof(PlayerInput));
        if (visualModel == null)
        {
            visualModel = GameObject.FindWithTag("Player");
        }
    }

    private void Update()
    {
        if (joystick == null) return;

        // Debug.Log($"조이스틱 입력: {joystick.InputVector}"); 

        if (_playerInputQuery.HasSingleton<PlayerInput>())
        {
            Entity playerEntity = _playerInputQuery.GetSingletonEntity();
            _em.SetComponentData(playerEntity, new PlayerInput
            {
                Move = new Unity.Mathematics.float2(joystick.InputVector.x, joystick.InputVector.y)
            });
            if (!_isLinked && visualModel != null)
            {
                _em.AddComponentData(playerEntity, new SubSceneVisualModel { Value = visualModel.transform });
                _isLinked = true;
            }
            // Debug.Log("ECS로 데이터 전송 중..."); 
        }
        else
        {
            Debug.LogWarning("ECS에 PlayerInput을 가진 엔티티가 없습니다!"); 
        }
    }
}