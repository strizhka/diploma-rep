using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    private static T _instance;

    public static T Instance => _instance;

    public static bool HasInstance => _instance != null;

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
        if (_instance == this)
            _instance = null;
    }
}
