using UnityEngine;

public class PersistentSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static object _lock = new object();

    private static bool applicationIsQuitting = false;

    public static T Instance
    {
        get
        {
            if (applicationIsQuitting)
                return null;

            lock (_lock)
            {
                if (_instance != null)
                    return _instance;

                _instance = (T)FindObjectOfType(typeof(T));

                if (FindObjectsOfType(typeof(T)).Length > 1)
                {
                    Debug.LogError("[Singleton] Something went really wrong " +
                                   " - there should never be more than 1 singleton!" +
                                   " Reopening the scene might fix it.");
                    return _instance;
                }

                if (_instance == null)
                {
                    GameObject singleton = new GameObject();
                    _instance = singleton.AddComponent<T>();
                    singleton.name = "(singleton) " + typeof(T).ToString();

                    DontDestroyOnLoad(singleton);

                    Debug.Log("[Singleton] An instance of " + typeof(T) +
                              " is needed in the scene, so '" + singleton +
                              "' was created with DontDestroyOnLoad.");
                }

                return _instance;
            }
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            GameObject singleton = this.gameObject;
            _instance = singleton.GetComponent<T>();
            singleton.name = "(singleton) " + typeof(T).ToString();

            DontDestroyOnLoad(singleton);

            Debug.Log("[Singleton] An instance of " + typeof(T) +
                      " is needed in the scene, so '" + singleton +
                      "' was created with DontDestroyOnLoad.");
        }
        else
        {
            Destroy(this.gameObject);
        }
    } 
}