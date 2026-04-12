using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class UI_EquipmentFrame : UI_Base
{
    [SerializeField] private Transform[] _characterSpawnPoints;
    [SerializeField] private Transform[] _relicSlots;

    private GameObject _currentCharacter;

    public override bool Init() { return base.Init(); }

    public void RefreshView()
    {
        UserData userData = DataManager.Instance.currentUserData;

        // 캐릭터 모델 갱신
        foreach (Transform spawnPoint in _characterSpawnPoints)
        {
            foreach (Transform child in spawnPoint) Destroy(child.gameObject);
        }

        int charID = userData.SelectedCharacterID;
        CharacterData charData = DataManager.Instance.CharacterDict[charID];

        GameObject charPrefab = ResourceManager.Instance.LoadPrefab($"Prefabs/VisualPrefabs/{charData.Name}(Lobby)");
        if (charPrefab != null)
        {
            foreach (Transform spawnPoint in _characterSpawnPoints)
            {
                _currentCharacter = Instantiate(charPrefab, spawnPoint);
                _currentCharacter.name = charData.Name;
                _currentCharacter.transform.localPosition = new Vector3(0, 0, 60f);
                _currentCharacter.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                _currentCharacter.transform.localScale = Vector3.one * 300f;
            }
        }

        // 유물 모델 갱신
        for (int i = 0; i < _relicSlots.Length; i++)
        {
            foreach (Transform child in _relicSlots[i]) Destroy(child.gameObject);

            GameObject go = Instantiate(ResourceManager.Instance.LoadPrefab("UI/Inventory/UI_InventorySlot"), _relicSlots[i]);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;

            UI_InventorySlot slot = go.GetComponent<UI_InventorySlot>();
            int relicID = (i < userData.EquippedRelicsID.Count) ? userData.EquippedRelicsID[i] : 0;
            if (relicID != 0)            {
                RelicData relicData = DataManager.Instance.RelicDict[relicID];
                slot.SetData(relicID, relicData.Icon, 1, false, OnEquippedRelicClicked);
            }
            else
            {
                slot.SetData(0, null, 0, false, OnEmptyRelicSlotClicked); // 빈 슬롯
            }
        }
    }

    private void OnEquippedRelicClicked(int itemID)
    {
        // 장착된 유물 클릭 시 상세 정보 팝업 띄우기
    }

    private void OnEmptyRelicSlotClicked(int placeHolder)
    {
        // 빈 슬롯 클릭 시 유물 선택 팝업 띄우기
    }
}