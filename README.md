# EQB Games

Mobile games division of EQB Business Solutions, building hybrid-casual puzzle games for the App Store and Google Play.

This repository holds the market research, the art direction and the first game, **Dockside Dispatch** (v0.7.2, build 12), which is being prepared for release on Google Play.

<p align="center">
  <img src="branding/dockside-dispatch/DocksideDispatch_Logo.png" width="420" alt="Dockside Dispatch logo">
</p>
<p align="center">
  <img src="dockside-dispatch/Screenshots/v3_l14.png" width="260" alt="Dockside Dispatch, level 14">
  <img src="dockside-dispatch/Screenshots/v3_l14_midgame.png" width="260" alt="Dockside Dispatch, mid-game">
</p>

## Repository layout

| Path | What it is |
|---|---|
| `research/` | Top-charts market research (App Store and Google Play, free and paid), with game concepts and their game loops. Markdown and PDF. |
| `dockside-dispatch/art-direction/` | Four art directions for Dockside Dispatch. **Option A, Golden Hour Harbour**, was chosen. |
| `dockside-dispatch/DocksideDispatch/` | The Unity project. |
| `dockside-dispatch/Screenshots/` | Screenshots from automated play-tests. |
| `dockside-dispatch/store/` | Google Play listing (`listing.md`), store graphics (`play/`, including foldable sizes), raw captures (`raw/`), Play Games achievements and the Play Console import zip (`play-games/`), the privacy policy page (`privacy-policy.html`) and `app-ads.txt`. |
| `dockside-dispatch/Tools/` | Python scripts that render the store graphics, app icon, logo, notification icons and achievement icons. |
| `branding/` | EQB Games logo (original and transparent cut-out). `branding/dockside-dispatch/` has the game logo and app icon, with the HTML sources they were rendered from. |

## Dockside Dispatch

A container-sorting puzzle set in a harbour, with two game modes: **Load** (fill ships from the container yard) and **Unload** (empty a full ship onto trucks). Each mode has its own 100-level campaign; coins and boosters are shared.

### Load mode

1. Tap a container in the yard that has a clear path to the quay. Row 0 sits next to the quay; anything behind it must be cleared first.
2. If a ship at a berth wants that colour and has room, the container is craned onto it. Otherwise it waits in the **holding bay** (5 slots).
3. A full ship sails away and the next one docks. Containers waiting in the bay load automatically when a matching ship arrives.
4. **Win:** dispatch every ship. **Lose:** the holding bay fills up, or no legal move is left.

Additional mechanics:

- **Long containers** cover 2 cells in the yard and take 2 slots on a ship or in the bay. They clear more per tap but are harder to place.
- **Locked containers** can only be moved after the container carrying their key has been sent off.
- **Hidden containers** show their colour only once they become reachable.
- Every colour also has a **symbol** (circle, triangle, square, diamond, plus), so the game can be played without relying on colour.

### Unload mode

Unload unlocks after Load level 5 and is chosen from the mode menu. A full ship is moored alongside the quay: tap a container in its open hold to lift it onto the waiting truck of that colour. A truck carries two short containers or one long one, then drives off down the dock road and the next one pulls in. Containers with no matching truck go to the holding bay. Win and loss rules, special containers and boosters are the same as in Load.

### Boosters, coins and second chance

The bottom bar has three **boosters**:

- **Extra Berth:** two more holding-bay slots.
- **Crane Lift:** move any container, even a blocked one.
- **Shuffle:** shuffle the colours of yard containers that are the same length.

Each level won earns 20 **coins**. When a booster runs out, the player can get one more by watching a rewarded video or by paying coins (Shuffle 40, Extra Berth 60, Crane Lift 90).

When the bay fills, there is a one-time **second chance** per level: +2 bay slots for a rewarded video or 120 coins.

### Ads, services and privacy

- **Ads:** Unity LevelPlay (ironSource Ads and Unity Ads), managed by `AdManager`. Rewarded videos are always the player's choice. Interstitials appear only after a completed level, from level 6 of each mode, at most once every 2 levels and every 5 minutes.
- **Privacy:** players in the EU/EEA, UK or Switzerland are asked for ad-personalisation consent on first launch, before ads start. A US "Do not sell or share my info" opt-out is in the pause menu (`Privacy`). The privacy policy is in `store/privacy-policy.html`.
- **Analytics:** GameAnalytics progression, ad, ad-revenue and design events (`Analytics`). Every event is also logged to the console as `[Analytics] ...`.
- **Google Play Games:** automatic sign-in and 17 achievements (`GameServices`). Progress is kept on the device and synced when the player is signed in, so the game works fully without sign-in.
- **Reminders:** local notifications only (`Notifications`). The player opts in after an early win; reminders are sent 1, 3 and 7 days after the app is closed, between 11:00 and 20:00 local time, and can be turned off in the pause menu.
- **Tutorial:** first-tap pointers and cards that introduce each new mechanic (`TutorialView`).
- **Audio:** sound effects for taps, the crane, landings, horns, unlocks, wins and losses (`Sfx`, `Resources/Audio`). There is no music yet.

### Levels

Each mode has 100 levels: Load levels are baked to `Assets/_Game/Resources/Levels/level_NN.json` and Unload levels to `Resources/Levels/Unload/`. After level 100, levels 81–100 repeat. The port skyline in the background changes every 5 levels, so each campaign visits 20 different ports: Hong Kong, Singapore, Dubai, Mumbai, Amsterdam, Los Angeles, Sydney, Rotterdam, Shanghai, New York, London, Hamburg, Tokyo, San Francisco, Rio de Janeiro, Cape Town, Istanbul, Venice, Barcelona and Seattle. The sky follows the player's local time of day, from sunrise to starry night, and birds fly past.

- **Generation:** levels are generated deterministically from a seed (`LevelGenerator`).
- **Solvability:** an exhaustive solver checks that every level can be won (`Solver.Solve`).
- **Difficulty:** each level is scored by a simulated casual player's win rate, which must fall in a target band for that level number. Difficulty ramps up as a sawtooth, with a spike every 5th level and a breather after it. Unload uses the same curve.

### Tech

- **Engine:** Unity **6000.3.28f1** (Unity 6.3 LTS) with URP and the Input System. Portrait. Android uses IL2CPP and ARM64.
- **Art:** built in code from generated meshes and procedural textures, so there are no imported 3D models yet:
  - most objects use the `Dockside/Flat` shader, which shows exact palette colours and darkens only the camera-facing sides, matching the flat 2D art direction (no lighting or shadow maps);
  - containers are rounded blocks with a generated corrugation texture;
  - the water is the custom `Dockside/Water` shader;
  - the sky and port silhouette are flat generated meshes.
- **Font:** [Fredoka](https://fonts.google.com/specimen/Fredoka) SemiBold and Bold, under the SIL Open Font License (`Assets/_Game/Resources/Fonts`). The UI icons are in `Resources/UI`.
- **UI:** uGUI, built at runtime (`HudView`, `UiFactory`).
- **Data:** progress per mode, coins, booster counts and settings are stored in PlayerPrefs (`SaveData`).
- **Packages:** LevelPlay 9.6.0, GameAnalytics 8.3.1, Mobile Notifications 2.5.0, Google Play Games plugin 2.2.1 (`Assets/GooglePlayGames`) and the Mobile Dependency Resolver.

```
Assets/_Game/
  Scripts/Core/      Pure C# game rules, no UnityEngine: LevelData, GameSession, Solver, LevelGenerator
  Scripts/Runtime/   Presentation: GameController, views (containers, ships, trucks, environment), HUD, mode menu,
                     tutorial, palette, tweens, plus Ads/, Backdrop/ (sky, skylines, birds), analytics, Play Games, notifications
  Scripts/Editor/    ProjectSetup (scene, materials, palette, splash, mobile rendering, SDK settings), BuildTools (APK and AAB)
  Shaders/           Flat.shader (flat palette shading), Water.shader
  Branding/          Splash logos and the app icon
  Tests/EditMode/    Rules, level-generation and reminder-schedule tests
  Resources/Levels/  Baked level JSON (Unload/ for Unload mode)
  Resources/Audio/   Sound effects
```

The game logic in `Core` emits events (`ContainerToShip`, `ShipDeparted`, and others). `GameController` plays those events back as animations. This keeps the rules testable without Unity.

### Getting started

**Requirements:**

- Unity Hub, with Unity 6000.3.28f1 and **Android Build Support** (OpenJDK, Android SDK and NDK).
- Git LFS. Images, fonts and PDFs are stored with LFS, so run this once:

```
git lfs install
git clone <this repo>
```

**Open and play:**

1. Open `dockside-dispatch/DocksideDispatch` in Unity Hub.
2. Open `Assets/_Game/Scenes/Main.unity`.
3. Set the Game view to a portrait phone resolution, such as 1080×2340.
4. Press **Play**.

Test tools (Go to level, Skip level, sky preview and a test reminder in the pause menu) are always available in the editor. On a device they are compiled in only when the `DOCKSIDE_TEST_TOOLS` scripting define is set, which the "test tools" build does.

The **Dockside** menu in the editor has these commands:

| Menu item | Purpose |
|---|---|
| Setup Project | Recreates the materials, palette and Main scene, and sets player settings, splash, app icon, mobile rendering, ad unit IDs and GameAnalytics keys |
| Configure Notifications / Splash and Icon / GameAnalytics / Mobile Rendering | Re-applies one part of Setup Project |
| Bake Levels | Regenerates the Load levels `level_01..100.json` |
| Bake Unload Levels | Regenerates the Unload levels in `Levels/Unload/` |
| Report Levels | Logs size and solver statistics for every level |
| Build Android APK | Debug-signed test APK: `Builds/Android/DocksideDispatch-<version>-<code>.apk` |
| Build Android APK (ad test suite) | Development build that opens the LevelPlay Test Suite to check the ad networks |
| Build Android APK (test tools) | Release-style APK with the test tools compiled in |
| Build Play Store AAB | App bundle signed with the upload key, for Google Play |

**Run the tests:** in the editor, use Window → General → Test Runner → EditMode. From the command line, use Unity's `-runTests -testPlatform EditMode`.

**Build for Android from the command line**, with the editor closed:

```
Unity -batchmode -projectPath dockside-dispatch/DocksideDispatch -buildTarget Android ^
      -executeMethod Dockside.EditorTools.BuildTools.BuildAndroidApk -logFile build.log
```

Test APKs are signed with Unity's debug key, so they are for sideloading onto test phones only.

**Play Store build:** use `-executeMethod Dockside.EditorTools.BuildTools.BuildPlayStoreAab`, or the menu item. It signs with the upload key described by a properties file (`storeFile`, `keyAlias`, `storePassword`, `keyPassword`) that is never committed. The file is read from `Documents/EQB_Keys/dockside-dispatch-upload.properties` in the user's home folder, or from the path in the `DOCKSIDE_SIGNING` environment variable. Play App Signing re-signs the bundle for players.

### Status and next steps

Version 0.7.2 (build 12) is the current Android release candidate. Both modes are playable from start to finish, with ads, analytics, Play Games achievements, reminders, privacy consent, a signed AAB build and a Play Store listing in place. The art is still generated in code.

Next steps:

1. Test on Google Play's testing tracks and tune difficulty and ad pacing from GameAnalytics funnel data.
2. Add the meta layer from the research: harbour rebuilding with port stars and daily shipping contracts.
3. Add in-app purchases: no-ads pack, starter bundle and Harbour Pass.
4. Music, final art, and an iOS build.

## Conventions

- **Build output:** `Library/`, `Temp/`, `Logs/` and `Builds/` are ignored, along with APK and AAB files.
- **Binary assets** (png, jpg, psd, fbx, audio, fonts, pdf) go through Git LFS.
- **Unity scene and asset files** are stored as text YAML, merged with Unity's `UnityYAMLMerge` tool.
