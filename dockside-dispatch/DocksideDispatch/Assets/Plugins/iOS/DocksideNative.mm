// Dockside Dispatch: small iOS bridge used from C# (see Assets/_Game/Scripts/Runtime/IosNative.cs).
//  - App Tracking Transparency: read the status and show Apple's tracking prompt.
//  - Reduce Motion: the accessibility setting, so tutorial demos can hold still.
// AppTrackingTransparency.framework is linked by Dockside.EditorTools.IosPostProcess.
#import <UIKit/UIKit.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>

// 0 not determined, 1 restricted, 2 denied, 3 authorized (ATTrackingManagerAuthorizationStatus values).
static volatile int s_trackingResult = -1;

extern "C" {

int Dockside_TrackingStatus(void)
{
    if (@available(iOS 14, *)) return (int)[ATTrackingManager trackingAuthorizationStatus];
    return 3;   // before iOS 14 there is no prompt and the advertising identifier is available
}

// Shows the prompt (iOS only shows it once per install). The answer is collected with Dockside_TrackingResult.
void Dockside_RequestTracking(void)
{
    s_trackingResult = -1;
    if (@available(iOS 14, *))
    {
        [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {
            s_trackingResult = (int)status;
        }];
    }
    else s_trackingResult = 3;
}

// -1 while the prompt is still open, otherwise the status the player chose.
int Dockside_TrackingResult(void)
{
    return s_trackingResult;
}

bool Dockside_ReduceMotion(void)
{
    return UIAccessibilityIsReduceMotionEnabled();
}

}
