using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Honours Android's "Remove animations" accessibility setting (animator duration scale 0): tutorial demos then
    /// play once and hold their last frame, and cards appear without a scale-in. Test override: PlayerPrefs
    /// "dd.debug.reducedMotion" = 1.
    /// </summary>
    public static class MotionPrefs
    {
        private static bool _reduced;
        private static float _checkedAt = float.NegativeInfinity;

        public static bool Reduced
        {
            get
            {
                // The player can change the setting while the game is in the background: re-read it now and then.
                if (Time.unscaledTime - _checkedAt > 5f)
                {
                    _checkedAt = Time.unscaledTime;
                    _reduced = PlayerPrefs.GetInt("dd.debug.reducedMotion", 0) == 1 || SystemSaysReduce();
                }
                return _reduced;
            }
        }

        private static bool SystemSaysReduce()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
                using (var global = new AndroidJavaClass("android.provider.Settings$Global"))
                    return global.CallStatic<float>("getFloat", resolver, "animator_duration_scale", 1f) == 0f;
            }
            catch (System.Exception)
            {
                return false;
            }
#else
            return false;
#endif
        }
    }
}
