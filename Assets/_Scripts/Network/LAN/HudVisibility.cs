using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Скрывает NetworkManagerHUD на указанных сценах (например, Tutorial).
/// Прицепить на тот же GameObject, на котором висит NetworkManagerHUD.
/// </summary>
[RequireComponent(typeof(NetworkManagerHUD))]
public class HudVisibility : MonoBehaviour
{
    [Tooltip("Сцены, на которых HUD должен быть скрыт.")]
    [SerializeField] private string[] _hideOnScenes = { "Tutorial" };

    private NetworkManagerHUD _hud;

    private void Awake()
    {
        _hud = GetComponent<NetworkManagerHUD>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        UpdateVisibility(SceneManager.GetActiveScene().name);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateVisibility(scene.name);
    }

    private void UpdateVisibility(string sceneName)
    {
        bool shouldHide = false;
        foreach (var s in _hideOnScenes)
        {
            if (s == sceneName)
            {
                shouldHide = true;
                break;
            }
        }

        _hud.enabled = !shouldHide;
    }
}
