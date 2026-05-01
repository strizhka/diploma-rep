using System;
using System.IO;
using System.Text;
using System.Threading;
using Mirror;
using UnityEngine;

public static class FileLogger
{
    private static StreamWriter _writer;
    private static readonly object _lock = new();
    private static string _filePath;
    private static bool _initialized;
    private static int _mainThreadId;

    public static string FilePath => _filePath;
    public static bool IsActive => _writer != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;

            string dir = Path.Combine(Application.persistentDataPath, "Logs");
            Directory.CreateDirectory(dir);

            string time = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            _filePath = Path.Combine(dir, $"diploma_{time}_pid{pid}.log");

            // Чистим старые логи (старше 7 дней) — чтобы папка не пухла
            TryCleanupOldLogs(dir);

            _writer = new StreamWriter(_filePath, false, Encoding.UTF8) { AutoFlush = true };

            WriteHeader();

            Application.logMessageReceivedThreaded += OnLogMessage;
            Application.quitting += OnQuit;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;

            // Видно в Development Console — путь к файлу
            Debug.Log($"[FileLogger] >>> Логи: {_filePath}");
        }
        catch (Exception e)
        {
            // Если логгер не поднялся — пишем в стандартную консоль и работаем дальше
            try { Debug.LogError($"[FileLogger] init failed: {e}"); } catch { }
            _writer = null;
        }
    }

    private static void WriteHeader()
    {
        Write($"=== Лог запущен: {DateTime.Now:O} ===");
        Write($"Unity:        {Application.unityVersion}");
        Write($"Платформа:    {Application.platform}");
        Write($"PID:          {System.Diagnostics.Process.GetCurrentProcess().Id}");
        Write($"Машина:       {Environment.MachineName}");
        Write($"User:         {Environment.UserName}");
        Write($"Persistent:   {Application.persistentDataPath}");
        Write($"DataPath:     {Application.dataPath}");
        Write("");
    }

    private static void TryCleanupOldLogs(string dir)
    {
        try
        {
            DateTime threshold = DateTime.Now.AddDays(-7);
            foreach (var path in Directory.GetFiles(dir, "diploma_*.log"))
            {
                var info = new FileInfo(path);
                if (info.LastWriteTime < threshold)
                {
                    try { info.Delete(); } catch { }
                }
            }
        }
        catch { /* не критично */ }
    }

    // ──────────────────────── ОБРАБОТЧИКИ ────────────────────────

    private static void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        if (_writer == null) return;

        lock (_lock)
        {
            try
            {
                string ts = DateTime.Now.ToString("HH:mm:ss.fff");
                int tid = Thread.CurrentThread.ManagedThreadId;
                string thread = tid == _mainThreadId ? "main" : $"T{tid}";
                string lvl = LevelTag(type);

                _writer.WriteLine($"[{ts}] [{thread,4}] [{lvl}] {condition}");

                // Стек только для warning/error/exception, чтобы лог не разносило
                bool needStack =
                    type == LogType.Error ||
                    type == LogType.Exception ||
                    type == LogType.Assert ||
                    type == LogType.Warning;

                if (needStack && !string.IsNullOrEmpty(stackTrace))
                {
                    foreach (var line in stackTrace.Split('\n'))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        _writer.WriteLine($"    {line.TrimEnd()}");
                    }
                }
            }
            catch
            {
                // Молчим — иначе бесконечный цикл логирования при ошибке записи
            }
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            var ex = e.ExceptionObject as Exception;
            Write($"!!! UNHANDLED EXCEPTION (terminating={e.IsTerminating}): {ex}");
        }
        catch { }
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Write($"--- Сцена загружена: '{s.name}' (mode={mode}) ---");
    }

    private static void OnSceneUnloaded(UnityEngine.SceneManagement.Scene s)
    {
        Write($"--- Сцена выгружена: '{s.name}' ---");
    }

    private static void OnQuit()
    {
        lock (_lock)
        {
            try
            {
                _writer?.WriteLine($"=== Лог закрыт: {DateTime.Now:O} ===");
                _writer?.Flush();
                _writer?.Close();
                _writer = null;
            }
            catch { }
        }
    }

    // ──────────────────────── ПУБЛИЧНОЕ API ────────────────────────

    /// <summary>Прямая запись с тегом (минует Debug.Log — никаких фильтров и стеков).</summary>
    public static void Write(string message)
    {
        if (_writer == null) return;
        lock (_lock)
        {
            try
            {
                string ts = DateTime.Now.ToString("HH:mm:ss.fff");
                _writer.WriteLine($"[{ts}] [main] [INFO] {message}");
            }
            catch { }
        }
    }

    /// <summary>Принудительно сбросить буфер на диск.</summary>
    public static void Flush()
    {
        lock (_lock)
        {
            try { _writer?.Flush(); } catch { }
        }
    }

    private static string LevelTag(LogType type) => type switch
    {
        LogType.Error => "ERR ",
        LogType.Assert => "ASRT",
        LogType.Warning => "WARN",
        LogType.Exception => "EXC ",
        _ => "INFO",
    };
}
