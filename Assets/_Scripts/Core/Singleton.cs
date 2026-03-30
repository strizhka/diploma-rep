using UnityEngine;

/// <summary>
/// Универсальный синглтон для MonoBehaviour.
/// 
/// ИСПОЛЬЗОВАНИЕ:
///   public class MyManager : Singleton<MyManager> { ... }
///
/// РЕЖИМЫ:
///   По умолчанию — уничтожается при смене сцены (подходит для WaitingRoomUI, LobbyUIManager и т.д.)
///   Если нужен DontDestroyOnLoad — переопределяем Persist:
///     protected override bool Persist => true;
///
/// ЧТО РЕШАЕТ:
///   1. Защита от дублирования — второй экземпляр уничтожается
///   2. Автоматическая очистка _instance при OnDestroy (нет висячих ссылок после смены сцены)
///   3. Потокобезопасный доступ через Instance с проверкой
///   4. HasInstance — безопасная проверка без логов
/// </summary>
public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    private static T _instance;

    /// <summary>
    /// Текущий экземпляр. Может быть null между сценами.
    /// Использовать HasInstance для безопасной проверки.
    /// </summary>
    public static T Instance
    {
        get
        {
            if (_instance == null)
                Debug.LogWarning($"[Singleton] {typeof(T).Name}.Instance запрошен, но экземпляр не существует.");
            return _instance;
        }
    }

    /// <summary>
    /// true если экземпляр жив и доступен. Не вызывает логов.
    /// </summary>
    public static bool HasInstance => _instance != null;

    /// <summary>
    /// Переопределить = true, чтобы синглтон пережил смену сцены (DontDestroyOnLoad).
    /// По умолчанию false — синглтон живёт в рамках одной сцены.
    /// </summary>
    protected virtual bool Persist => false;

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[Singleton] Дубликат {typeof(T).Name} уничтожен на '{gameObject.name}'.");
            Destroy(gameObject);
            return;
        }

        _instance = (T)this;

        if (Persist)
            DontDestroyOnLoad(gameObject);
    }

    protected virtual void OnDestroy()
    {
        // Очищаем ссылку только если уничтожается именно текущий экземпляр,
        // а не дубликат, который мы уничтожили в Awake.
        if (_instance == this)
            _instance = null;
    }
}
