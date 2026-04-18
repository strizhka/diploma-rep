using UnityEngine;

public class MainMenuUI : Singleton<MainMenuUI>
{
    [Header("Канвас главного меню")]
    [SerializeField] private CanvasGroup _mainMenuCanvas;

    [Header("Панели")]
    [SerializeField] private CanvasGroup _buttonsPanel;
    [SerializeField] private CanvasGroup _settingsPanel;

    [Header("Камера")]
    [SerializeField] private MenuCameraTransition _cameraTransition;

    [Header("Настройки")]
    [SerializeField] private SettingsManager _settingsManager;

    [Header("Анимация")]
    [SerializeField] private float _fade = 0.4f;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        _mainMenuCanvas.ShowInstant();
        _buttonsPanel.ShowInstant();
        _settingsPanel.HideInstant();
    }

    public void Show()
    {
        _settingsPanel.HideInstant();
        _buttonsPanel.ShowInstant();
        _mainMenuCanvas.FadeIn(_fade);
    }

    public void OnClickStart()
    {
        _buttonsPanel.FadeOut(_fade, () =>
            _mainMenuCanvas.FadeOut(_fade, () =>
            {
                _mainMenuCanvas.gameObject.SetActive(false);
                LobbyUIManager.Instance.Show();
            }));
    }

    public void OnCameraTransitionDone()
    {
        _mainMenuCanvas.FadeOut(_fade, () =>
        {
            _mainMenuCanvas.gameObject.SetActive(false);
            LobbyUIManager.Instance.Show();
        });
    }

    public void OnClickSettings()
    {
        _buttonsPanel.SwitchTo(_settingsPanel, _fade);
        _settingsManager?.Open();
    }

    public void OnClickSettingsBack()
    {
        _settingsPanel.FadeOut(_fade, () =>
        {
            _settingsManager?.Close();
            _buttonsPanel.FadeIn(_fade);
        });
    }

    public void OnClickQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}