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
    /// iOS uses its own LevelPlay app (a separate app key) and asks Apple's tracking question (Tracking) first.
    /// </summary>
    public class LevelPlayInitializer : MonoBehaviour
    {
        [Tooltip("LevelPlay dashboard > Apps > Dockside Dispatch (Android)")]
        [SerializeField] private string appKey;
        [Tooltip("LevelPlay dashboard > Apps > Dockside Dispatch (iOS). Ads stay off on iOS while this is empty.")]
        [SerializeField] private string iosAppKey;

#if UNITY_IOS
        private string PlatformAppKey => iosAppKey;
#else
        private string PlatformAppKey => appKey;
#endif

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

            // No ads on a TV (see Monetization): the ad SDK never starts there.
            if (!Monetization.AdsAllowed)
            {
                Debug.Log("[Dockside] TV: ads are off.");
                yield break;
            }

            // Privacy settings must be applied BEFORE LevelPlay.Init()
            while (!Privacy.Ready) yield return null;
            // iOS: Apple's tracking prompt, after our own privacy question and before any ad SDK starts.
            yield return Tracking.RequestIfNeeded();
            Privacy.ApplyToLevelPlay();

            if (string.IsNullOrEmpty(PlatformAppKey))
            {
                Debug.LogWarning("[Dockside] LevelPlay: no app key for this platform, ads are off.");
                yield break;
            }

            // Enable Test Suite: Development Builds only (Dockside > Build Android APK (ad test suite)),
            // so it can never reach a release build.
            if (Debug.isDebugBuild && !Application.isEditor) LevelPlay.SetMetaData("is_test_suite", "enable");

            // Initialize the SDK with your App Key
            LevelPlay.Init(PlatformAppKey);
        }

        private void OnInitSuccess(LevelPlayConfiguration config)
        {
            Debug.Log("LevelPlay SDK initialized successfully");
            // SDK is now ready to load ads
            IsInitialized = true;
            Initialized?.Invoke();
            // Launch Test Suite: Development Builds only (validates real ads on a device)
            if (Debug.isDebugBuild && !Application.isEditor) LevelPlay.LaunchTestSuite();
        }

        private void OnInitFailed(LevelPlayInitError error)
        {
            Debug.LogError($"LevelPlay initialization failed: {error.ErrorMessage}");
            Analytics.Error(true, "ads", $"LevelPlay init failed ({error.ErrorCode}): {error.ErrorMessage}");
        }

        void OnDestroy()
        {
            LevelPlay.OnInitSuccess -= OnInitSuccess;
            LevelPlay.OnInitFailed -= OnInitFailed;
        }
    }
}
