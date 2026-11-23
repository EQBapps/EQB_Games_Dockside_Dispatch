#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Dockside
{
    /// <summary>
    /// The iOS bridge in Assets/Plugins/iOS/DocksideNative.mm. Everywhere else these calls return safe defaults,
    /// so callers don't need their own platform checks.
    /// </summary>
    public static class IosNative
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int Dockside_TrackingStatus();
        [DllImport("__Internal")] private static extern void Dockside_RequestTracking();
        [DllImport("__Internal")] private static extern int Dockside_TrackingResult();
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.I1)] private static extern bool Dockside_ReduceMotion();

        public static int TrackingStatus() => Dockside_TrackingStatus();
        public static void RequestTracking() => Dockside_RequestTracking();
        public static int TrackingResult() => Dockside_TrackingResult();
        public static bool ReduceMotion() => Dockside_ReduceMotion();
#else
        public static int TrackingStatus() => Tracking.Authorized;
        public static void RequestTracking() { }
        public static int TrackingResult() => Tracking.Authorized;
        public static bool ReduceMotion() => false;
#endif
    }
}
