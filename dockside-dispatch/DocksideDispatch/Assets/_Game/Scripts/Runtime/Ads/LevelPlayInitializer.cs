using System;
using System.Collections;
using UnityEngine;
using Unity.Services.LevelPlay;

namespace Dockside
{
    /// <summary>
    /// Initializes the LevelPlay SDK (LevelPlay integration, Option 1: dedicated initializer script).
    /// Sits on the "Game" object in the Main scene; the App Key is set in the Inspector (Dockside > Setup Project fills it in).
    /// Initialization waits until the player's privacy choices are known (see Privacy), because GDPR/CCPA
    /// settings must reach LevelPlay before Init().
    /// Impression-level revenue (ILRD) is wired per ad instance (SDK 9.5.0+), not here.
    /// </summary>
    public class LevelPlayInitializer : MonoBehaviour
    {
        [SerializeField] private string appKey;

        public static bool IsInitialized { get; private set; }
        public static event Action Initialized;

        void Awake()
        {
            // Persist across scene loads so ads stay initialized
            DontDestroyOnLoad(gameObject);
        }

        IEnumerator Start()
        {
            // Register initialization callbacks
            LevelPlay.OnInitSuccess += OnInitSuccess;
            LevelPlay.OnInitFailed += OnInitFailed;

            // Privacy settings must be applied BEFORE LevelPlay.Init()
            while (!Privacy.Ready) yield return null;
            Privacy.ApplyToLevelPlay();

            // Initialize the SDK with your App Key
            LevelPlay.Init(appKey);
        }

        private void OnInitSuccess(LevelPlayConfiguration config)
        {
            Debug.Log("LevelPlay SDK initialized successfully");
            // SDK is now ready to load ads
            IsInitialized = true;
            Initialized?.Invoke();
        }

        private void OnInitFailed(LevelPlayInitError error)
        {
            Debug.LogError($"LevelPlay initialization failed: {error.ErrorMessage}");
        }

        void OnDestroy()
        {
            LevelPlay.OnInitSuccess -= OnInitSuccess;
            LevelPlay.OnInitFailed -= OnInitFailed;
        }
    }
}
