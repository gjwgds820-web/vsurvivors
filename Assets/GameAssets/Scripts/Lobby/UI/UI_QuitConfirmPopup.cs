using UnityEngine;
using UnityEngine.UI;

public class UI_QuitConfirmPopup : UI_Base
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
        // 어플리케이션 종료
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnCancelButtonClicked()
    {
        UIManager.Instance.CloseTopPopup();
    }
}
