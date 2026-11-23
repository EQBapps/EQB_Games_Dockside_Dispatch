# Building Dockside Dispatch for the App Store

How to go from this repository to a build in TestFlight and App Store review, on a Mac. The listing text, privacy answers and Game Center achievements are in [`store/app-store/listing.md`](store/app-store/listing.md).

## What the project already does for iOS

- **Ads.** LevelPlay with a separate iOS app key and ad units, which are empty until you fill them in, so ads stay off on iOS. Before ads start, Apple's tracking question is asked. It comes after the personalised-ads question for EU, EEA, UK and Swiss players, and isn't asked at all for players who already said no. LevelPlay adds the ad networks' SKAdNetwork IDs to `Info.plist` at build time.
- **Achievements.** Game Center, with the same 17 achievements as Play Games. The IDs are in `GameServices.GameCenterIds`.
- **Reminders.** iOS local notifications, opt-in through the same card as on Android.
- **Reduce Motion** (Settings › Accessibility › Motion) makes the tutorial demos hold still, as Android's "Remove animations" does.
- **Build settings:** iPhone only, iOS 15+, portrait, and the home indicator needs a second swipe so the booster bar isn't swiped away. The build number follows the Android version code.
- **Xcode project:** the post-build step (`IosPostProcess`) sets:
  - the tracking prompt text and `ITSAppUsesNonExemptEncryption = NO`;
  - the Game Center capability;
  - links `AppTrackingTransparency.framework`.

## One-time setup on the Mac

1. **Xcode:** install it from the Mac App Store and open it once to install its components. Then in Xcode › Settings › Accounts, add the Apple ID that belongs to the EQB Business Solutions developer team.
2. **Unity:** install Unity Hub, then **Unity 6000.3.28f1**, the exact version this project uses, with the **iOS Build Support** module.
3. **CocoaPods**, which the ad SDKs install through: `brew install cocoapods`, or `sudo gem install cocoapods` without Homebrew. Check it with `pod --version`.
4. **The project:** clone this repository and open `dockside-dispatch/DocksideDispatch` in Unity Hub.
5. **Your iPhone:** connect it by cable, trust the Mac, and turn on Settings › Privacy & Security › **Developer Mode**. The phone restarts.

## One-time setup in Apple's and the ad dashboards

1. **App Store Connect › Apps › +:** create the app.
   - Platform: iOS.
   - Name: Dockside Dispatch.
   - Primary language: English (U.K.).
   - Bundle ID: `com.eqbbusiness.docksidedispatch`. If it isn't in the list, Xcode registers it on the first signed build, or add it under Certificates, Identifiers & Profiles › Identifiers with **Game Center** ticked.
   - SKU: `dockside-dispatch-ios`.
2. **Game Center achievements:** create the 17 from the table in `store/app-store/listing.md`. The IDs must match exactly.
3. **LevelPlay:** add an **iOS** app for Dockside Dispatch, with its own app key and its own Rewarded and Interstitial ad units. Set it up in each network's dashboard too (Unity Ads, Meta, Liftoff, Mintegral, InMobi).
4. **GameAnalytics:** add an **iOS** platform to the game and copy its game key and secret key.
5. **Paste the keys** into the constants at the top of `Assets/_Game/Scripts/Editor/ProjectSetup.cs`:
   - `LevelPlayIosAppKey`
   - `IosRewardedAdUnitId`, `IosInterstitialAdUnitId`
   - `GameAnalyticsIosGameKey`, `GameAnalyticsIosSecretKey`
   - `AppleTeamId`, optional: the 10-character team ID from developer.apple.com › Membership
6. **Write them into the project:** in Unity, run **Dockside › Apply Ad Keys** and **Dockside › Configure GameAnalytics**. Commit the result.

## Building

1. In Unity, run **Dockside › Build iOS (Xcode project)**. The first time, it switches the project to iOS and asks you to run it again once compiling finishes. That's needed because the iOS build steps only load on the iOS platform.
2. The Xcode project is written to `Builds/iOS/DocksideDispatch-<version>-<build>/`. Unity runs `pod install` itself when CocoaPods is installed. If `Unity-iPhone.xcworkspace` isn't there afterwards, run this in that folder:
   ```
   pod install --repo-update
   ```
3. Open **`Unity-iPhone.xcworkspace`**, not the `.xcodeproj`, or the ad SDKs won't link.
4. Select the **Unity-iPhone** target › Signing & Capabilities:
   - Team: EQB Business Solutions Private Limited.
   - **Automatically manage signing:** on.
   - **Game Center** should be in the list.

## Testing on your iPhone

Choose your iPhone as the run destination and press Run (⌘R). Check:

- **First launch:** players in the EU, EEA, UK or Switzerland first see the personalised-ads card. Then iOS asks *"Allow Dockside Dispatch to track your activity…"* with our text. Ads load after you answer.
- **Game Center:** a "Welcome back" banner appears, or Game Center's sign-in sheet. Pause › **Awards** opens the achievements, and winning level 1 earns *First Dispatch*.
- **Reminders:** after an early win, accept the reminders card, and iOS asks for permission. Then close the game: the first reminder is due a day later, between 11:00 and 20:00.
- **Reduce Motion** on: the tutorial demos play once and hold.
- **Ads:** rewarded videos (out of a booster, or the second chance on the lose card) and an interstitial after level 6. Don't tap live ads on your own phone: use each network's test mode while testing.

The tracking question only appears once per install. To see it again, delete the app and reinstall it.

## Uploading to TestFlight and App Review

1. In Xcode, set the run destination to **Any iOS Device (arm64)**, then **Product › Archive**.
2. In the Organizer: **Distribute App › App Store Connect › Upload**. Processing takes 10 to 30 minutes, after which App Store Connect emails you.
3. **TestFlight › Internal Testing:** add yourself and install the build through the TestFlight app on your iPhone.
4. When it's good, fill in the version page from `store/app-store/listing.md` (screenshots, text, privacy answers, age rating), choose the build and **Add for Review**.

## Each new version

- Raise `bundleVersion` (e.g. 0.7.5) and the Android version code in Player Settings, as for Play. The iOS build number follows the version code, so both stores carry the same number.
- Builds you upload for the same version need a higher build number each time.

## If something goes wrong

| Problem | Fix |
|---|---|
| "No such module" or missing ironSource symbols | Open the `.xcworkspace`, not the `.xcodeproj`, and run `pod install` in the build folder. |
| "Signing requires a development team" | Pick the team under Signing & Capabilities, or set `AppleTeamId` in `ProjectSetup.cs`. |
| Email about a missing purpose string (ITMS-90683) | An SDK uses an API that needs an `NS…UsageDescription`. Add it in `IosPostProcess.cs`, next to the tracking text. |
| Game Center says the game isn't recognised | The bundle ID in App Store Connect must have Game Center ticked, and the achievement IDs must match `GameServices.GameCenterIds`. |
| Achievements never appear | They only report while signed in to Game Center (Settings › Game Center). Progress is kept on the device and sent at the next sign-in. |
