using System;
using System.Collections.Concurrent;
using UnityEngine;
using Unity.Services.LevelPlay;

namespace Dockside
{
    /// <summary>
    /// Unified LevelPlay ad manager (Option 2: one AdManager) for the Balanced strategy:
    ///  - Rewarded: player-chosen videos for the second chance and for refilling an empty booster.
    ///    Preloaded eagerly so one is normally ready when the player asks.
    ///  - Interstitial: shown opportunistically after a completed level, never blocking play,
    ///    and frequency-capped (none until level <see cref="InterstitialFromLevel"/> of the mode being played is
    ///    complete, so new players and players trying Unload for the first time get ad-free levels; at most one every
    ///    <see cref="InterstitialCooldownSeconds"/> seconds and every <see cref="InterstitialEveryLevels"/> levels).
    /// Ad objects are created only after LevelPlay has initialized. Impression-level revenue (ILRD, SDK 9.5.0+)
    /// is collected per ad instance on a background thread and forwarded to Analytics on the main thread.
    /// Lives on the persistent "Game" object next to LevelPlayInitializer; ad unit IDs and bid floors are set in the
    /// Inspector (Dockside > Setup Project fills them in).
    /// </summary>
    public class AdManager : MonoBehaviour
    {
        [Header("Ad unit IDs (LevelPlay dashboard > Ad units)")]
        [SerializeField] private string rewardedAdUnitId;
        [SerializeField] private string interstitialAdUnitId;

        [Header("Bid floors in USD (0 = none)")]
        [SerializeField] private double rewardedBidFloor;
        [SerializeField] private double interstitialBidFloor;

        public const int InterstitialFromLevel = 6;
        public const float InterstitialCooldownSeconds = 300f;
        public const int InterstitialEveryLevels = 2;

        public static AdManager Instance { get; private set; }

        private LevelPlayRewardedAd _rewarded;
        private LevelPlayInterstitialAd _interstitial;
        private Action _onReward;
        private string _rewardPlacement;
        private bool _rewardGranted;
        private float _lastInterstitialTime = -9999f;
        private int _levelsSinceInterstitial;
        private float _rewardedRetryDelay = 5f, _interstitialRetryDelay = 5f;
        private readonly ConcurrentQueue<LevelPlayImpressionData> _impressions = new ConcurrentQueue<LevelPlayImpressionData>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Start()
        {
            // Ads must not be created before the SDK has initialized.
            if (LevelPlayInitializer.IsInitialized) CreateAds();
            else LevelPlayInitializer.Initialized += CreateAds;
        }

        void OnDestroy()
        {
            LevelPlayInitializer.Initialized -= CreateAds;
            if (_rewarded != null)
            {
                _rewarded.OnAdLoaded -= OnRewardedLoaded;
                _rewarded.OnAdLoadFailed -= OnRewardedLoadFailed;
                _rewarded.OnAdDisplayed -= OnRewardedDisplayed;
                _rewarded.OnAdDisplayFailed -= OnRewardedDisplayFailed;
                _rewarded.OnAdRewarded -= OnRewardedRewarded;
                _rewarded.OnAdClosed -= OnRewardedClosed;
                _rewarded.OnAdClicked -= OnRewardedClicked;
                _rewarded.OnAdImpressionDataReady -= OnImpressionDataReady;
            }
            if (_interstitial != null)
            {
                _interstitial.OnAdLoaded -= OnInterstitialLoaded;
                _interstitial.OnAdLoadFailed -= OnInterstitialLoadFailed;
                _interstitial.OnAdDisplayed -= OnInterstitialDisplayed;
                _interstitial.OnAdDisplayFailed -= OnInterstitialDisplayFailed;
                _interstitial.OnAdClosed -= OnInterstitialClosed;
                _interstitial.OnAdClicked -= OnInterstitialClicked;
                _interstitial.OnAdImpressionDataReady -= OnImpressionDataReady;
                // Free the native ad and its resources when this manager is destroyed
                _interstitial.DestroyAd();
            }
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            // ILRD arrives on a background thread; forward it here on the main thread.
            while (_impressions.TryDequeue(out var data)) ForwardImpression(data);
        }

        private void CreateAds()
        {
            LevelPlayInitializer.Initialized -= CreateAds;

            #region Rewarded
            if (string.IsNullOrEmpty(rewardedAdUnitId))
                Debug.LogWarning("[Ads] No rewarded ad unit ID set; rewarded ads are disabled.");
            else
            {
                _rewarded = rewardedBidFloor > 0
                    ? new LevelPlayRewardedAd(rewardedAdUnitId, new LevelPlayRewardedAd.Config.Builder().SetBidFloor(rewardedBidFloor).Build())
                    : new LevelPlayRewardedAd(rewardedAdUnitId);
                _rewarded.OnAdImpressionDataReady += OnImpressionDataReady;   // ILRD (SDK 9.5.0+)
                _rewarded.OnAdLoaded += OnRewardedLoaded;
                _rewarded.OnAdLoadFailed += OnRewardedLoadFailed;
                _rewarded.OnAdDisplayed += OnRewardedDisplayed;
                _rewarded.OnAdDisplayFailed += OnRewardedDisplayFailed;
                _rewarded.OnAdRewarded += OnRewardedRewarded;
                _rewarded.OnAdClosed += OnRewardedClosed;
                _rewarded.OnAdClicked += OnRewardedClicked;
                LoadRewarded();
            }
            #endregion

            #region Interstitial
            if (string.IsNullOrEmpty(interstitialAdUnitId))
                Debug.LogWarning("[Ads] No interstitial ad unit ID set; interstitials are disabled.");
            else
            {
                _interstitial = interstitialBidFloor > 0
                    ? new LevelPlayInterstitialAd(interstitialAdUnitId, new LevelPlayInterstitialAd.Config.Builder().SetBidFloor(interstitialBidFloor).Build())
                    : new LevelPlayInterstitialAd(interstitialAdUnitId);
                _interstitial.OnAdImpressionDataReady += OnImpressionDataReady;   // ILRD (SDK 9.5.0+)
                _interstitial.OnAdLoaded += OnInterstitialLoaded;
                _interstitial.OnAdLoadFailed += OnInterstitialLoadFailed;
                _interstitial.OnAdDisplayed += OnInterstitialDisplayed;
                _interstitial.OnAdDisplayFailed += OnInterstitialDisplayFailed;
                _interstitial.OnAdClosed += OnInterstitialClosed;
                _interstitial.OnAdClicked += OnInterstitialClicked;
                LoadInterstitial();
            }
            #endregion
        }

        // =====================================================================
        #region Rewarded
        public bool IsRewardedReady() => _rewarded != null && _rewarded.IsAdReady();

        /// <summary>
        /// Shows a rewarded video. Returns false (and shows nothing) if no video is ready, so the caller can tell the player.
        /// onReward runs once, on the main thread, when the player has earned the reward.
        /// </summary>
        public bool ShowRewarded(string placement, Action onReward)
        {
            if (!IsRewardedReady())
            {
                Debug.LogWarning("[Ads] Rewarded ad is not ready yet");
                if (_rewarded != null) LoadRewarded();
                return false;
            }
            _onReward = onReward;
            _rewardPlacement = placement;
            _rewardGranted = false;
            Debug.Log($"[Ads] Showing rewarded ad ({placement})");
            Analytics.Event("ad_rewarded_show", ("placement", placement));
            _rewarded.ShowAd();
            return true;
        }

        public void LoadRewarded()
        {
            Debug.Log("[Ads] Loading rewarded ad...");
            _rewarded?.LoadAd();
        }

        private void OnRewardedLoaded(LevelPlayAdInfo adInfo)
        {
            Debug.Log($"[Ads] Rewarded ad loaded ({adInfo.AdNetwork})");
            _rewardedRetryDelay = 5f;
        }

        private void OnRewardedLoadFailed(LevelPlayAdError error)
        {
            Debug.LogWarning($"[Ads] Rewarded ad failed to load: {error.ErrorMessage}");
            // Retry with exponential backoff (5s, 10s, 20s ... up to 2 min)
            Invoke(nameof(LoadRewarded), _rewardedRetryDelay);
            _rewardedRetryDelay = Mathf.Min(_rewardedRetryDelay * 2f, 120f);
        }

        private void OnRewardedDisplayed(LevelPlayAdInfo adInfo) => Debug.Log("[Ads] Rewarded ad displayed");

        private void OnRewardedDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            Debug.LogError($"[Ads] Rewarded ad failed to display: {error.ErrorMessage}");
            // The player asked for the video and it failed on our side: grant the reward anyway (generous fallback).
            GrantReward("display_failed");
            LoadRewarded();
        }

        private void OnRewardedRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
        {
            Debug.Log($"[Ads] User earned reward: {reward.Amount} {reward.Name}");
            GrantReward("rewarded");
        }

        private void OnRewardedClosed(LevelPlayAdInfo adInfo)
        {
            Debug.Log("[Ads] Rewarded ad closed");
            LoadRewarded();   // eager preload: keep one ready for the next offer
            // OnAdRewarded is not guaranteed to arrive before OnAdClosed (the SDK's own editor mock sends it after), so a
            // close without a reward yet waits briefly for it before counting the video as skipped.
            if (!_rewardGranted) StartCoroutine(AwaitLateReward(_rewardPlacement));
            else _onReward = null;
        }

        private System.Collections.IEnumerator AwaitLateReward(string placement)
        {
            float until = Time.realtimeSinceStartup + 2f;
            while (!_rewardGranted && Time.realtimeSinceStartup < until) yield return null;
            if (!_rewardGranted && _rewardPlacement == placement)
            {
                Analytics.Event("ad_rewarded_skipped", ("placement", placement));
                _onReward = null;
            }
        }

        private void OnRewardedClicked(LevelPlayAdInfo adInfo) => Debug.Log("[Ads] Rewarded ad clicked");

        private void GrantReward(string how)
        {
            if (_rewardGranted) return;
            _rewardGranted = true;
            Analytics.Event("ad_rewarded_granted", ("placement", _rewardPlacement), ("how", how));
            var cb = _onReward;
            _onReward = null;
            cb?.Invoke();
        }
        #endregion

        // =====================================================================
        #region Interstitial
        /// <summary>
        /// Called when a level is completed. Shows an interstitial only if the frequency cap allows and one is ready;
        /// never waits for an ad. Returns true if an ad is being shown (onClosed runs when it closes).
        /// levelCompleted: the level just completed, in the mode being played (each mode starts ad-free).
        /// </summary>
        public bool TryShowInterstitialAfterLevel(int levelCompleted, Action onClosed)
        {
            _levelsSinceInterstitial++;
            if (levelCompleted < InterstitialFromLevel) return false;
            if (_levelsSinceInterstitial < InterstitialEveryLevels) return false;
            if (Time.realtimeSinceStartup - _lastInterstitialTime < InterstitialCooldownSeconds) return false;
            if (_interstitial == null || !_interstitial.IsAdReady())
            {
                Debug.LogWarning("[Ads] Interstitial ad is not ready yet");
                return false;
            }
            _lastInterstitialTime = Time.realtimeSinceStartup;
            _levelsSinceInterstitial = 0;
            _interstitialClosed = onClosed;
            Debug.Log("[Ads] Showing interstitial ad");
            Analytics.Event("ad_interstitial_show", ("level", levelCompleted));
            _interstitial.ShowAd();
            return true;
        }

        private Action _interstitialClosed;

        public void LoadInterstitial()
        {
            Debug.Log("[Ads] Loading interstitial ad...");
            _interstitial?.LoadAd();
        }

        private void OnInterstitialLoaded(LevelPlayAdInfo adInfo)
        {
            Debug.Log($"[Ads] Interstitial ad loaded ({adInfo.AdNetwork})");
            _interstitialRetryDelay = 5f;
        }

        private void OnInterstitialLoadFailed(LevelPlayAdError error)
        {
            Debug.LogWarning($"[Ads] Interstitial ad failed to load: {error.ErrorMessage}");
            Invoke(nameof(LoadInterstitial), _interstitialRetryDelay);
            _interstitialRetryDelay = Mathf.Min(_interstitialRetryDelay * 2f, 120f);
        }

        private void OnInterstitialDisplayed(LevelPlayAdInfo adInfo) => Debug.Log("[Ads] Interstitial ad displayed");

        private void OnInterstitialDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            Debug.LogError($"[Ads] Interstitial ad failed to display: {error.ErrorMessage}");
            FinishInterstitial();
        }

        private void OnInterstitialClosed(LevelPlayAdInfo adInfo)
        {
            Debug.Log("[Ads] Interstitial ad closed");
            FinishInterstitial();
        }

        private void FinishInterstitial()
        {
            var cb = _interstitialClosed;
            _interstitialClosed = null;
            cb?.Invoke();       // continue the game flow
            LoadInterstitial(); // have the next one ready
        }

        private void OnInterstitialClicked(LevelPlayAdInfo adInfo) => Debug.Log("[Ads] Interstitial ad clicked");
        #endregion

        // =====================================================================
        #region Impression-level revenue (ILRD)
        private void OnImpressionDataReady(LevelPlayImpressionData impressionData)
        {
            // Runs on a BACKGROUND thread: no Unity APIs here, just hand it to Update().
            if (impressionData != null) _impressions.Enqueue(impressionData);
        }

        private void ForwardImpression(LevelPlayImpressionData data)
        {
            double revenue = data.Revenue ?? 0;
            Debug.Log($"ILRD: {data.AdNetwork} / {data.AdFormat} / ${revenue}");
            Analytics.AdImpression(data.AdNetwork ?? "unknown", data.AdFormat ?? "unknown", data.InstanceName ?? "", revenue,
                data.AllData ?? "");
        }
        #endregion
    }
}
