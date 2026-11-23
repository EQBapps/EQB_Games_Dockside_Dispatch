using System.Collections;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Apple's App Tracking Transparency prompt (iOS only). Asked once, after our own GDPR question and before ads
    /// initialize, so ad partners only see the advertising identifier (IDFA) when the player allows it.
    /// Players who already said no to personalised ads (GDPR card or "Do not sell or share") are not asked:
    /// they have answered the question, and the identifier stays unavailable.
    /// Elsewhere this is a no-op.
    /// </summary>
    public static class Tracking
    {
        public const int NotDetermined = 0, Restricted = 1, Denied = 2, Authorized = 3;

        /// <summary>Asks if the player hasn't been asked yet; finishes once they have answered (or straight away).</summary>
        public static IEnumerator RequestIfNeeded()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (IosNative.TrackingStatus() != NotDetermined) yield break;
            if (Privacy.GdprAnswer == 0 || Privacy.CcpaOptOut) yield break;
            // iOS ignores the request while the app is still becoming active (e.g. during the launch animation).
            yield return new WaitForSecondsRealtime(0.5f);
            IosNative.RequestTracking();
            float until = Time.realtimeSinceStartup + 120f;   // never hold ads back forever
            while (IosNative.TrackingResult() < 0 && Time.realtimeSinceStartup < until) yield return null;
            Analytics.Event("att", ("status", Describe(IosNative.TrackingResult())));
#else
            yield break;
#endif
        }

        public static string Describe(int status) => status switch
        {
            NotDetermined => "not_determined",
            Restricted => "restricted",
            Denied => "denied",
            Authorized => "authorized",
            _ => "pending",
        };
    }
}
