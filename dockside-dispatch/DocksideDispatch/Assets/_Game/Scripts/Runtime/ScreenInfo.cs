using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// What kind of screen the game is on, and which way round it may be played.
    /// Phones are portrait only. Large screens (smallest width of 600 dp or more: tablets, Chromebooks, laptops,
    /// Google Play Games on PC; on iOS, iPads) and TVs turn freely, and the HUD, mode menu and camera lay themselves out for
    /// whichever shape the screen has (see <see cref="Landscape"/>).
    /// The Android manifest allows every orientation (Player Settings: Auto Rotation) so big screens open the way
    /// they are held; phones are locked to portrait here, before the splash screen.
    /// </summary>
    public static class ScreenInfo
    {
        /// <summary>A TV (Android TV / Google TV): played with a remote, from the sofa.</summary>
        public static bool IsTv { get; private set; }

        /// <summary>Tablet, Chromebook, laptop, PC or TV: may be played in landscape.</summary>
        public static bool IsLargeScreen { get; private set; }

        /// <summary>The screen (or window) is wider than it is tall: side-column HUD, cards side by side.</summary>
        public static bool Landscape => Screen.width > Screen.height;

        /// <summary>
        /// Share of the screen kept clear at each edge on a TV, which may crop the picture's edges (overscan).
        /// Android TV's guidance is 48 x 27 dp of a 960 x 540 dp screen: 5% at every edge.
        /// </summary>
        public static float OverscanX => IsTv ? 0.05f : 0f;
        public static float OverscanY => IsTv ? 0.05f : 0f;

        /// <summary>
        /// Where the HUD and cards may draw, in pixels: the device safe area (notches, rounded corners), and on a TV
        /// also inside the overscan margins.
        /// </summary>
        public static Rect SafeArea
        {
            get
            {
                var safe = Screen.safeArea;
                if (!IsTv) return safe;
                float dx = Screen.width * OverscanX, dy = Screen.height * OverscanY;
                return Rect.MinMaxRect(Mathf.Max(safe.xMin, dx), Mathf.Max(safe.yMin, dy),
                                       Mathf.Min(safe.xMax, Screen.width - dx), Mathf.Min(safe.yMax, Screen.height - dy));
            }
        }

        /// <summary>
        /// Pixels per canvas unit (UiFactory.OrientationScaler): landscape canvases are 1080 units tall; portrait ones
        /// match a 1080 x 1920 design 50/50 on width and height.
        /// </summary>
        public static float CanvasScale => Landscape
            ? Mathf.Max(1f, Screen.height) / 1080f
            : Mathf.Sqrt(Mathf.Max(1f, Screen.width) * Mathf.Max(1f, Screen.height) / (1080f * 1920f));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Init()
        {
            Detect();
            if (IsLargeScreen)
            {
                Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = true;
                Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
                Screen.orientation = ScreenOrientation.AutoRotation;
            }
            else Screen.orientation = ScreenOrientation.Portrait;
        }

        private static void Detect()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var resources = activity.Call<AndroidJavaObject>("getResources");
                using var config = resources.Call<AndroidJavaObject>("getConfiguration");
                using var pm = activity.Call<AndroidJavaObject>("getPackageManager");
                const int UiModeTypeMask = 0x0f, UiModeTypeTelevision = 4;
                IsTv = (config.Get<int>("uiMode") & UiModeTypeMask) == UiModeTypeTelevision
                       || pm.Call<bool>("hasSystemFeature", "android.software.leanback");
                IsLargeScreen = IsTv || config.Get<int>("smallestScreenWidthDp") >= 600;
                // Test-tools builds: Test tools > TV mode shows the TV version on a phone or tablet.
                if (BuildFlags.TestTools && PlayerPrefs.GetInt("dev_tv", 0) == 1) IsTv = IsLargeScreen = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Dockside] Couldn't read the screen class, treating this as a phone: {e.Message}");
                IsTv = IsLargeScreen = false;
            }
#elif UNITY_IOS && !UNITY_EDITOR
            // iPad: either way round, like an Android tablet. iPhone: portrait only (its Info.plist allows only portrait).
            IsTv = false;
            IsLargeScreen = IosNative.IsPad();
#else
            // Editor and desktop: follow the Game view / window, and let a test pretend to be a TV.
            IsTv = PlayerPrefs.GetInt("dev_tv", 0) == 1;
            IsLargeScreen = true;
#endif
        }
    }

    /// <summary>
    /// Portrait: a 1080 x 1920 design, width and height matched 50/50 (as the game has always been laid out).
    /// Landscape: a 1920 x 1080 design matched on height, so every landscape screen is 1080 units tall and only the
    /// width varies (1440 at 4:3, 1920 at 16:9, 2520 at 21:9). See <see cref="ScreenInfo.CanvasScale"/>.
    /// </summary>
    public class OrientationScaler : MonoBehaviour
    {
        private bool? _landscape;
        private void Update() { if (_landscape != ScreenInfo.Landscape) Apply(); }

        public void Apply()
        {
            _landscape = ScreenInfo.Landscape;
            var scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.referenceResolution = _landscape.Value ? new Vector2(1920, 1080) : new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = _landscape.Value ? 1f : 0.5f;
        }
    }
}
