# Dockside Dispatch on the App Store: build and submission steps

The steps from this repository to a game in App Store review, for **iPhone and iPad**, done on the Mac. Work through
them in order: each part says what it needs from the one before.

- The listing text, privacy answers, age rating and Game Center achievements are in [`store/app-store/listing.md`](store/app-store/listing.md).
- The screenshots are in [`store/app-store/screenshots/`](store/app-store/screenshots/): one iPhone set and one iPad set.

## What the project already does for iOS

| Area | What it does |
|---|---|
| **Devices** | iPhone and iPad, iOS 15 or later. |
| **iPhone screen** | Portrait only, like Android phones, on every size from the 4" iPhone SE to the 6.9" Pro Max. The HUD keeps clear of the notch, Dynamic Island and home indicator. The home indicator needs a second swipe, so a swipe at the booster bar doesn't leave the game. |
| **iPad screen** | Either way round. Held sideways it uses the landscape layout from Android tablets: side columns and cards side by side. Its window can also be resized (Split View, Slide Over, Stage Manager, iPadOS 26 windows) and the game lays itself out again. |
| **Ads** | LevelPlay with its own iOS app key and ad units, already set in `ProjectSetup.cs`. Apple's tracking question comes before ads start: after the personalised-ads card for players in the EU, EEA, UK and Switzerland, and never for players who already said no. |
| **Premium** | One-time purchase `premium_unlock` that removes the ads between levels, with the **Restore purchase** button Apple requires. |
| **Achievements** | Game Center, the same 17 as on Play. |
| **Reminders** | Opt-in local notifications. |
| **Reduce Motion** | Settings › Accessibility › Motion makes the tutorial demos hold still. |
| **Analytics** | GameAnalytics with its own iOS keys. |
| **Xcode project** | `IosPostProcess` adds the tracking text, `ITSAppUsesNonExemptEncryption = NO`, the iPhone and iPad orientations, the Game Center capability and `AppTrackingTransparency.framework`. |
| **Version** | The build number follows the Android version code: 0.7.7 (17) on both stores. |

## Part 1. Accounts and agreements (once; some take days)

1. **Apple Developer Program, as the organisation.** Enrol EQB Business Solutions Private Limited at
   developer.apple.com/programs/enroll:
   - It needs the company's **D-U-N-S number** (free from Dun & Bradstreet; allow up to 2 weeks if you don't have one) and a website on the company's domain.
   - It costs US$99 a year.
   - The person enrolling must be able to sign for the company.

   Enrolling as an organisation shows the company, not a person, as the seller on the App Store.
2. **Agreements, tax and banking.** App Store Connect › Business:
   - Accept the **Paid Applications** agreement.
   - Add the bank account and the tax forms (for India, the W-8BEN-E with the company's details).

   The free agreement covers the app itself, but **in-app purchases don't work, even when testing, until the Paid Applications agreement is Active.**
3. **EU trader status.** App Store Connect › Business › the Digital Services Act section. Declare the company a **trader**. Its address, phone and email are then shown on the EU storefronts. Without it, the app can't be offered in the EU.
4. **Team ID** (optional). developer.apple.com › Account › Membership shows the 10-character Team ID. Put it in `AppleTeamId` in `Assets/_Game/Scripts/Editor/ProjectSetup.cs` so every build is signed for the right team. Otherwise you pick the team in Xcode each time.

## Part 2. Set up the Mac (once)

1. **macOS:** update to the latest version. The current Xcode needs a recent macOS.
2. **Xcode 26 or later**, from the Mac App Store.
   - Apple only accepts uploads built with Xcode 26 and the iOS 26 SDK, or later.
   - Open it once so it installs its components. In Settings › Components, also add an **iOS Simulator** runtime.
   - In Xcode › Settings › Accounts, add the Apple Account that's on the developer team.
3. **Homebrew** (from brew.sh), then:
   - **CocoaPods**, which installs the ad and analytics SDKs: `brew install cocoapods`. Check it with `pod --version`.
   - **Git LFS**, which stores the game's images, sounds and fonts: `brew install git-lfs`, then `git lfs install`. Without it the clone has placeholder files instead of art.
4. **Unity Hub**, then **Unity 6000.3.28f1**, exactly this version, with the **iOS Build Support** module. If the Mac has Apple silicon, use the Apple silicon editor.
5. **The project:**
   ```
   git clone https://github.com/EQBapps/EQB_Games_Dockside_Dispatch.git
   ```
   Sign in to GitHub as the EQBapps account when Git asks; the repository is private. Open `dockside-dispatch/DocksideDispatch` in Unity Hub. The first import takes 10 to 20 minutes.
6. **Test iPhone (and iPad, if you have one):**
   - Connect it by cable and choose Trust.
   - Turn on Settings › Privacy & Security › **Developer Mode**. The device restarts.

No signing keys or passwords come from the Windows PC: Xcode makes the Apple certificates itself, under the team account.

## Part 3. App Store Connect: the app record (once)

App Store Connect › **Apps › + › New App**:

| Field | Value |
|---|---|
| Platforms | iOS |
| Name | Dockside Dispatch |
| Primary language | English (U.K.) |
| Bundle ID | `com.eqbbusiness.docksidedispatch` |
| SKU | `dockside-dispatch-ios` |
| User access | Full access |

If the bundle ID isn't in the list:
- Register it at developer.apple.com › Certificates, Identifiers & Profiles › Identifiers › +, an App ID.
- Use the explicit ID `com.eqbbusiness.docksidedispatch`.
- Tick **Game Center** and **In-App Purchase**.

Then, on the new app:

1. **Game Center** (the app › Services › Game Center): create the 17 achievements from the table in `listing.md`. The IDs must match exactly. The images are in `store/play-games/icons/`.
2. **In-app purchase** (the app › Monetization › In-App Purchases › +):
   - Type: **Non-Consumable**.
   - Reference name: `Dockside Premium`. Product ID: `premium_unlock`, the same as on Play.
   - Price: the tier nearest the Play price (US$3.99).
   - Localisation:
     - Display name: "Remove ads".
     - Description: "No more ads between levels. Optional reward videos stay."
   - Review screenshot: a capture of the Premium card. Take it on the iPhone in Part 6 (Pause › Remove ads).
   - Status must reach **Ready to Submit**. It is submitted with the app version in Part 8.
3. **Sandbox tester** (Users and Access › Sandbox › +): make a test Apple Account with an email address that isn't already an Apple Account. Purchases with it are free.
4. **Pricing and Availability:**
   - Price: Free.
   - Countries: all.
   - **Untick** "Make this app available" on **Mac (Apple silicon)** and **Apple Vision Pro**. They would run the iPad version, and it hasn't been tested there.

## Part 4. Build the Xcode project in Unity

1. In Unity, run **Dockside › Build iOS (Xcode project)**.
   - The first time, it switches the project to iOS (5 to 15 minutes) and asks you to run it again; the iOS build steps only load on the iOS platform.
   - It then writes `Builds/iOS/DocksideDispatch-0.7.7-17/`.
2. Unity runs `pod install` itself. If `Unity-iPhone.xcworkspace` isn't in that folder afterwards, run in it:
   ```
   pod install --repo-update
   ```
3. Open **`Unity-iPhone.xcworkspace`**, never the `.xcodeproj`, or the ad SDKs won't link.
4. Select the **Unity-iPhone** target › **Signing & Capabilities**:
   - Team: EQB Business Solutions Private Limited.
   - Automatically manage signing: on.
   - **Game Center** and **In-App Purchase** should be listed. Add In-App Purchase with **+ Capability** if it's missing.
5. Under **General**, check:
   - Supported Destinations lists iPhone and iPad.
   - Minimum Deployments: iOS 15.0.
   - Version 0.7.7, Build 17.

## Part 5. Try every screen size in the Simulator

Apple reviews on both iPhone and iPad, so every size has to work. The Simulator covers the sizes you don't own.

1. In Unity, run **Dockside › Build iOS Simulator (Xcode project)**. It writes `Builds/iOS/DocksideDispatch-0.7.7-17-simulator/`. This copy is for testing only; it can't be uploaded.
2. Open its `Unity-iPhone.xcworkspace`. Choose each simulator below as the run destination and press Run (⌘R). Add missing ones under Window › Devices and Simulators › Simulators › +.

| Simulator | Screen | What to check |
|---|---|---|
| iPhone SE (3rd generation) | 4.7", 16:9, home button | The HUD and booster bar fit; nothing overlaps the board. |
| iPhone 16e or 17 | 6.1", notch or Dynamic Island | Level pill and pause button clear of the Dynamic Island. |
| iPhone 17 Pro Max | 6.9" | The screenshots' size; the board is centred and big. |
| iPad mini | 8.3" | Portrait and landscape (⌘← / ⌘→ rotate); both layouts readable. |
| iPad (A16) or iPad Air 11" | 11" | Rotate mid-level; the board and HUD re-lay out. |
| iPad Pro 13" | 13" | Landscape side columns; mode menu cards side by side. |

On one iPad, also check resizing:
- Open the game in a window: Settings › Multitasking & Gestures › **Windowed Apps** on iPadOS 26, or Split View on older iPadOS.
- Drag the window narrow and wide. The game should switch between the portrait and landscape layouts without cut-off buttons.

On every device, check these:
- **iPhones stay portrait** when turned sideways.
- **iPads turn** with the device.
- Play a level to the end: the win card, the next level, and Pause and its buttons all fit.

Ads, Game Center and purchases don't run in the Simulator. Part 6 tests them on a real device.

## Part 6. Test on a real iPhone (and iPad)

Back in the **device** workspace from Part 4, choose your iPhone as the destination and press Run. Check:

- **First launch:** in the EU, EEA, UK or Switzerland, the personalised-ads card first. Then iOS asks *"Allow Dockside Dispatch to track your activity…"* with our text. Ads load after you answer. To see the question again, delete the app and reinstall it.
- **Game Center:** a "Welcome back" banner, or the sign-in sheet. Pause › **Awards** opens the achievements. Winning level 1 earns *First Dispatch*.
- **Reminders:** after an early win, accept the reminders card and iOS asks for permission. The first reminder is due a day later, between 11:00 and 20:00.
- **Premium:**
  - Sign in to the sandbox account first: Settings › Developer › Sandbox Apple Account.
  - Pause › Remove ads shows the store price. Buy it: no more ads between levels.
  - Delete and reinstall, then use **Restore purchase**: Premium comes back.
  - Take the IAP review screenshot (Part 3) here.
- **Ads:** a rewarded video (from a booster, or the second chance on the lose card) and an interstitial after level 6. Don't tap live ads on your own device; use the networks' test modes.
- **Reduce Motion** on: the tutorial demos play once and hold.

If you have an iPad, run the same build on it: one Xcode build covers both.

## Part 7. Upload to TestFlight

1. In Xcode, set the destination to **Any iOS Device (arm64)**, then **Product › Archive**.
2. When the Organizer opens: **Distribute App › App Store Connect › Distribute**. Xcode signs and uploads it.
3. Processing takes 10 to 30 minutes. App Store Connect emails you when the build is ready, or about any problem (see the table at the end).
4. **TestFlight › Internal Testing:** make a group, add yourself (and anyone on the team), and install the build with the TestFlight app on iPhone and iPad.
5. Play it for a day: this is the exact build Apple will review.

## Part 8. Fill in the version and submit

On the app's **iOS App › 0.7.7** page (the version number must match the build), from `listing.md`:

1. **Screenshots:**
   - **iPhone 6.9" Display:** the 7 PNGs in `store/app-store/screenshots/iphone_6.9/`, in file order.
   - **iPad 13" Display:** the 6 PNGs in `store/app-store/screenshots/ipad_13/`.

   Apple scales these sets down for every smaller iPhone and iPad, so no other sizes are needed.
2. **Promotional text, description, keywords, support and marketing URLs.**
3. **Build:** choose 0.7.7 (17).
4. **In-App Purchases and Subscriptions:** add **Dockside Premium**, so it's reviewed with this version.
5. **Game Center:** turn it on for this version and add the 17 achievements.
6. **App Review Information:** contact details and the notes in `listing.md`. No sign-in is needed.
7. **App Privacy** (the app's App Privacy page): the answers in `listing.md`, then Publish.
8. **Age Rating:** answer the questionnaire as in `listing.md`. Expected: 4+.
9. **Version release:** "Manually release" is safest for the first version: approval doesn't publish it until you press Release.
10. **Add for Review**, then **Submit for Review**.

Review usually takes 1 to 3 days. If Apple rejects it, the reason is in App Store Connect › the app › App Review. Fix it, raise the build number (Part 9), upload, and reply in the same thread.

## Part 9. Each new version

- Raise `bundleVersion` (e.g. 0.7.8) and the Android version code in Player Settings, as for Play. The iOS build number follows the version code.
- Every upload needs a new build number, even for the same version.
- After pulling changes on the Mac: Part 4 (build), Part 6 (quick check), Part 7 (upload), then a new version in App Store Connect › + Version, and Part 8.
- Regenerate the screenshots when the game's look changes (below).

## Screenshots: how they're made

The App Store screenshots are made on the Windows PC from Editor captures, like the Play ones:
1. **Captures** go in `store/raw/ios/`:
   - `i*`: iPhone, 1320 × 2868 portrait.
   - `a*`: iPad, 2752 × 2064 landscape.

   They're taken in Play mode with the Game view set to those sizes.
2. **`python Tools/make_appstore.py`** adds the captions and frames. It writes `store/app-store/screenshots/iphone_6.9/` and `.../ipad_13/` and checks each file is exactly Apple's size, with no transparency.

## If something goes wrong

| Problem | Fix |
|---|---|
| Upload refused: "built with an older SDK" | Update Xcode to 26 or later, then rebuild from Unity (Part 4). |
| "No such module" or missing ironSource symbols | Open the `.xcworkspace`, not the `.xcodeproj`, and run `pod install` in the build folder. |
| `pod install` fails | `pod repo update`, then `pod install` again. On Apple silicon, if Ruby complains, `brew reinstall cocoapods`. |
| "Signing requires a development team" | Pick the team under Signing & Capabilities, or set `AppleTeamId` in `ProjectSetup.cs`. |
| Images or sounds are missing, or are tiny text files | Git LFS wasn't installed before cloning: `git lfs install`, then `git lfs pull`. |
| Simulator build fails to link an ad SDK | That SDK has no Simulator version. Test that size on a device or through TestFlight instead. The Simulator build is only for checking layouts. |
| Email ITMS-90683, missing purpose string | An SDK uses an API that needs an `NS…UsageDescription`. Add it in `IosPostProcess.cs`, next to the tracking text. |
| Email ITMS-91053, missing API declaration (privacy manifest) | An SDK uses a "required reason" API without declaring it. Update that SDK (Ads Mediation › Network Manager), or add a `PrivacyInfo.xcprivacy` in `Assets/Plugins/iOS/` with the reason the email names. |
| Premium shows no price, or "purchases unavailable" | The Paid Applications agreement isn't Active yet (Part 1), the product isn't Ready to Submit, or the device isn't signed in to the sandbox account. |
| Game Center says the game isn't recognised | The App ID must have Game Center ticked, and the achievement IDs must match `GameServices.GameCenterIds`. |
| Rejected under 2.1 (crashes or bugs) on iPad | Apple's note names the iPad model. Run that size in the Simulator (Part 5) to reproduce it. |
| Rejected under 4.0 (design) for the iPad layout | Attach a screen recording of the iPad playing in both orientations in your reply. |
