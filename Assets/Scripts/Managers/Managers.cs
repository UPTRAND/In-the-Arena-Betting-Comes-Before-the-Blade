using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Managers : MonoBehaviour
{
    static Managers s_instance; // 유일성이 보장된다
    static Managers Instance { get { Init(); return s_instance; } } // 유일한 매니저를 갖고온다

    #region Core

    DataManager _data = new DataManager();
    InputManager _input = new InputManager();
    PoolManager _pool = new PoolManager();
    ResourceManager _resource = new ResourceManager();
    SceneManagerEx _scene = new SceneManagerEx();
    UIManager _ui = new UIManager();
    SoundManager _sound;

    public static DataManager Data { get { return Instance._data; } }
    public static InputManager Input { get { return Instance._input; } }
    public static PoolManager Pool { get { return Instance._pool; } }
    public static ResourceManager Resource { get { return Instance._resource; } }
    public static SceneManagerEx Scene { get { return Instance._scene; } }
    public static UIManager UI { get { return Instance._ui; } }
    public static SoundManager Sound { get { return Instance._sound; } }

    #endregion

    public static PlayerData nowPlayerData;

    void Start()
    {
        Init();
	}

    void Update()
    {
        _input.OnUpdate();
    }

    static void Init()
    {
        if (s_instance == null)
        {
            GameObject go = GameObject.Find("@Managers");
            GameObject sound = GameObject.Find("@Sound");

            if (go == null)
            {
                go = new GameObject { name = "@Managers" };
                sound = new GameObject { name = "@Sound" };
                sound.transform.SetParent(go.transform);
            }

            DontDestroyOnLoad(go);

            s_instance = go.GetOrAddComponent<Managers>();

            s_instance._sound = sound.GetOrAddComponent<SoundManager>();

            s_instance._data.Init();
            s_instance._pool.Init();
            s_instance._sound.Init();

            nowPlayerData = new PlayerData();

            s_instance._data.LoadData();
        }
    }

    public static void Clear()
    {
        Input.Clear();
        Sound.Clear();
        Scene.Clear();
        UI.Clear();
        Pool.Clear();
    }
}
