using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>
/// Панель отчёта о баге. Три кнопки:
///   1. Открыть форму — открывает Google Form в браузере с предзаполненными
///      полями «Сцена», «Версия», «Время». Все данные вводятся в самой форме.
///   2. Открыть папку с логами — открывает Проводник на папку с FileLogger-логами,
///      чтобы пользователь мог вручную приложить файлы к форме.
///   3. Закрыть — скрывает панель.
///
/// НАСТРОЙКА:
/// 1. Создай Canvas → Panel «BugReportPanel» (изначально выключенную)
/// 2. Внутри: три Button-ы.
/// 3. На корне панели — этот скрипт, привязать поля _panel и три кнопки.
/// 4. _formUrlTemplate — prefilled-ссылка Google Form, в которой плейсхолдеры
///    __SCENE__, __VERSION__, __TIME__ заменятся на реальные значения.
/// 5. Сохрани панель как префаб BugReportPanel.prefab, кидай в каждую сцену.
/// </summary>
public class BugReportPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _openFormButton;
    [SerializeField] private Button _openLogsFolderButton;
    [SerializeField] private Button _closeButton;

    [Header("Google Form")]
    [Tooltip("Prefilled-ссылка из Google Forms. Плейсхолдеры __SCENE__, __VERSION__, __TIME__ " +
             "будут заменены на реальные значения при открытии.")]
    [TextArea(3, 6)]
    [SerializeField] private string _formUrlTemplate;

    private void Awake()
    {
        if (_panel != null) _panel.SetActive(false);

        if (_openFormButton != null)        _openFormButton.onClick.AddListener(OnOpenForm);
        if (_openLogsFolderButton != null)  _openLogsFolderButton.onClick.AddListener(OnOpenLogsFolder);
        if (_closeButton != null)           _closeButton.onClick.AddListener(Close);
    }

    // ──────────────────────── ОТКРЫТИЕ / ЗАКРЫТИЕ ────────────────────────

    public void Open()
    {
        if (_panel != null) _panel.SetActive(true);
    }

    public void Close()
    {
        if (_panel != null) _panel.SetActive(false);
    }

    // ──────────────────────── КНОПКИ ────────────────────────

    private void OnOpenForm()
    {
        // Сбрасываем буфер логов на диск — чтобы если пользователь пойдёт прикладывать
        // файл, тот был свежим
        FileLogger.Flush();

        string url = BuildFormUrl();
        Application.OpenURL(url);

        FileLogger.Write($"[BugReport] Открыта форма: scene={SceneManager.GetActiveScene().name}, " +
                         $"version={Application.version}");
    }

    private void OnOpenLogsFolder()
    {
        string folder = Path.Combine(Application.persistentDataPath, "Logs");

        if (!Directory.Exists(folder))
        {
            Debug.LogWarning($"[BugReport] Папка с логами не найдена: {folder}");
            return;
        }

        FileLogger.Flush();
        OpenInFileExplorer(folder);
    }

    // ──────────────────────── HELPERS ────────────────────────

    private string BuildFormUrl()
    {
        string scene = UnityWebRequest.EscapeURL(SceneManager.GetActiveScene().name);
        string version = UnityWebRequest.EscapeURL(Application.version);
        string time = UnityWebRequest.EscapeURL(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        string cpu = UnityWebRequest.EscapeURL($"{SystemInfo.processorType} ({SystemInfo.processorCount} cores)");
        string ram = UnityWebRequest.EscapeURL($"{SystemInfo.systemMemorySize} MB");
        string gpu = UnityWebRequest.EscapeURL($"{SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB)");
        string network = UnityWebRequest.EscapeURL(Application.internetReachability.ToString());

        return _formUrlTemplate
            .Replace("__SCENE__", scene)
            .Replace("__VERSION__", version)
            .Replace("__TIME__", time)
            .Replace("__CPU__", cpu)
            .Replace("__RAM__", ram)
            .Replace("__GPU__", gpu)
            .Replace("__NETWORK__", network);
    }

    /// <summary>Открывает указанную папку в файловом проводнике системы.</summary>
    private static void OpenInFileExplorer(string path)
    {
        try
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            Process.Start("open", $"\"{path}\"");
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            Process.Start("xdg-open", $"\"{path}\"");
#else
            Application.OpenURL("file://" + path);
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BugReport] Не удалось открыть папку: {e.Message}");
        }
    }
}
