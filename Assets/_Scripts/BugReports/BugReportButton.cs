using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Кнопка-открывалка панели баг-репорта.
/// Вешается на любую UI-кнопку «Сообщить о баге» в любой сцене.
/// Если _panel не задан — ищет BugReportPanel в сцене автоматически.
/// </summary>
[RequireComponent(typeof(Button))]
public class BugReportButton : MonoBehaviour
{
    [SerializeField] private BugReportPanel _panel;

    private void Awake()
    {
        if (_panel == null)
            _panel = FindAnyObjectByType<BugReportPanel>(FindObjectsInactive.Include);

        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (_panel != null) _panel.Open();
        else Debug.LogWarning("[BugReportButton] BugReportPanel не найден в сцене.");
    }
}
