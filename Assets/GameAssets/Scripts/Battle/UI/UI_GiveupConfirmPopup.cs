using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UI_GiveupConfirmPopup : UI_Base
{
    enum Buttons
    {
        ConfirmButton,
        CancelButton,
    }

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindButton(typeof(Buttons));

        GetButton((int)Buttons.ConfirmButton).onClick.AddListener(OnConfirmButtonClicked);
        GetButton((int)Buttons.CancelButton).onClick.AddListener(OnCancelButtonClicked);

        return true;
    }

    private void OnConfirmButtonClicked()
    {
        // 정상적인 속도로 복구 후 로비 씬으로 이동
        Time.timeScale = 1f;
        UIManager.Instance.CloseAllPopups(); // 혹시 열려있는 다른 팝업 정리
        SceneManager.LoadScene("LobbyScene");
    }

    private void OnCancelButtonClicked()
    {
        // 닫기 (Pause 팝업은 그대로 남아있고 Time.timeScale=0 유지)
        UIManager.Instance.CloseTopPopup();
    }
}
