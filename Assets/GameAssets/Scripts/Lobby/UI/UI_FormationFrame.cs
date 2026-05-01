using UnityEngine;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public class UI_FormationFrame : UI_Base
{
    private const int MAX_SHADOW_SLOTS = 10;
    enum GameObjects
    {
        Shadows,
    }

    public override bool Init()
    {
        if (!base.Init())
            return false;
        Bind<GameObject>(typeof(GameObjects));
        
        return true;
    }

    public void RefreshView(int formationIndex)
    {
        UserData userData = DataManager.Instance.currentUserData;
        Transform shadowContent = GetObject((int)GameObjects.Shadows).transform;

        // 기존 슬롯 초기화
        foreach (Transform child in shadowContent)
        {
            Destroy(child.gameObject);
        }

        // 선택된 편성이 있는지 확인
        List<int> formationData = userData.FormationData.ContainsKey(formationIndex) ? userData.FormationData[formationIndex] : new List<int>();

        // 슬롯 생성
        for (int i = 0; i < MAX_SHADOW_SLOTS; i++)
        {
            GameObject go = Instantiate(ResourceManager.Instance.LoadPrefab("UI/Inventory/UI_InventorySlot"), shadowContent);
            UI_InventorySlot slot = go.GetComponent<UI_InventorySlot>();
            
            if (i < formationData.Count && formationData[i] != 0)
            {
                int shadowID = formationData[i];
                if (DataManager.Instance.ShadowDict.TryGetValue(shadowID, out ShadowData shadowData))
                {
                    slot.SetData(shadowID, shadowData.Icon, 1, false, OnEquippedShadowClicked);
                }
                else
                {
                    Debug.LogWarning($"[UI_FormationFrame] 저장된 그림자 ID({shadowID})를 데이터베이스에서 찾을 수 없습니다. 슬롯을 초기화합니다.");
                    slot.SetData(0, null, 0, false, OnEmptyShadowSlotClicked); // 빈 슬롯
                }
            }
            else
            {
                slot.SetData(0, null, 0, false, OnEmptyShadowSlotClicked); // 빈 슬롯
            }
        }
    }

    private void OnEquippedShadowClicked(int itemID)
    {
        // 장착된 그림자 슬롯 클릭 시 행동 (예: 상세 정보 팝업)
    }

    private void OnEmptyShadowSlotClicked(int placeHolder)
    {
        // 빈 슬롯 클릭 시 행동 (예: 슬롯 선택 팝업)
    }
}