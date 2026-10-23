using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Starts the game from any scene: if no GameManager exists after the first scene loads, one is created.
    /// The whole game (camera, lights, UI, levels) is built in code, so an empty scene is enough.
    /// Add a <see cref="DisableAutoBoot"/> component to a scene to opt out (e.g. art test scenes).
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (GameManager.Instance != null) return;
#if UNITY_2023_1_OR_NEWER
            if (Object.FindAnyObjectByType<DisableAutoBoot>() != null) return;
            if (Object.FindAnyObjectByType<GameManager>() != null) return;
#else
            if (Object.FindObjectOfType<DisableAutoBoot>() != null) return;
            if (Object.FindObjectOfType<GameManager>() != null) return;
#endif
            new GameObject("ShapeEscapeGame").AddComponent<GameManager>();
        }
    }
}
