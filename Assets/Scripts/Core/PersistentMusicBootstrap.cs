using UnityEngine;
using UnityEngine.SceneManagement;

public static class PersistentMusicBootstrap
{
    private const string MusicPlayerObjectName = "Music Player";

    private static AudioSource persistentMusicSource;
    private static bool sceneHookRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        persistentMusicSource = null;
        sceneHookRegistered = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        EnsureSceneHook();
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void EnsureSceneHook()
    {
        if (sceneHookRegistered)
        {
            return;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
        sceneHookRegistered = true;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (persistentMusicSource == null)
        {
            TryClaimSceneMusicPlayer(scene);
            return;
        }

        RemoveDuplicateSceneMusicPlayers(scene);

        if (!persistentMusicSource.isPlaying)
        {
            persistentMusicSource.Play();
        }
    }

    private static void TryClaimSceneMusicPlayer(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null || root.name != MusicPlayerObjectName)
            {
                continue;
            }

            var source = root.GetComponent<AudioSource>();
            if (source == null)
            {
                continue;
            }

            persistentMusicSource = source;
            Object.DontDestroyOnLoad(root);
            persistentMusicSource.loop = true;

            if (!persistentMusicSource.isPlaying)
            {
                persistentMusicSource.Play();
            }

            break;
        }
    }

    private static void RemoveDuplicateSceneMusicPlayers(Scene scene)
    {
        if (persistentMusicSource == null)
        {
            return;
        }

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null || root.name != MusicPlayerObjectName)
            {
                continue;
            }

            if (root == persistentMusicSource.gameObject)
            {
                continue;
            }

            var duplicateSource = root.GetComponent<AudioSource>();
            if (duplicateSource == null)
            {
                continue;
            }

            Object.Destroy(root);
        }
    }
}

