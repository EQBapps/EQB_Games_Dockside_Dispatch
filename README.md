# EQB Games

Mobile games division of EQB Business Solutions, building hybrid-casual puzzle games for the App Store and Google Play.

This repository holds the market research, the art direction and the first game prototype, **Dockside Dispatch**.

<p align="center">
  <img src="dockside-dispatch/Screenshots/art_pass_l14_wide_yard.png" width="280" alt="Dockside Dispatch, level 14">
  <img src="dockside-dispatch/Screenshots/art_pass_l5_midgame.png" width="280" alt="Dockside Dispatch, mid-game">
</p>

## Repository layout

| Path | What it is |
|---|---|
| `research/` | Top-charts market research (App Store and Google Play, free and paid), with game concepts and their game loops. Markdown and PDF. |
| `dockside-dispatch/art-direction/` | Four art directions for Dockside Dispatch. **Option A, Golden Hour Harbour**, was chosen. |
| `dockside-dispatch/DocksideDispatch/` | The Unity project. |
| `dockside-dispatch/Screenshots/` | Screenshots from automated play-tests. |
| `branding/` | EQB Games logo: the original, plus a transparent cut-out used for the splash screen. |

## Dockside Dispatch

A container-sorting puzzle set in a harbour at sunset.

### Core loop

1. Tap a container in the yard that has a clear path to the quay. Row 0 sits next to the quay; anything behind it must be cleared first.
2. If a ship at a berth wants that colour and has room, the container is craned onto it. Otherwise it waits in the **holding bay** (5 slots).
3. A full ship sails away and the next one docks. Containers waiting in the bay load automatically when a matching ship arrives.
4. **Win:** dispatch every ship. **Lose:** the holding bay fills up, or no legal move is left.

Additional mechanics:

- **Long containers** cover 2 cells in the yard and take 2 slots on a ship or in the bay. They clear more per tap but are harder to place.
- **Locked containers** can only be moved after the container carrying their key has been sent off.
- **Hidden containers** show their colour only once they become reachable.
- Every colour also has a **symbol** (circle, triangle, square, diamond, plus), so the game can be played without relying on colour.

The bottom bar has three **boosters**:

- **Extra Berth:** two more holding-bay slots.
- **Crane Lift:** move any container, even a blocked one.
- **Shuffle:** shuffle the colours of yard containers that are the same length.

After a loss there is a one-time **second chance**. Rewarded ads are placeholders for now.

### Levels

There are 30 levels, baked to `Assets/_Game/Resources/Levels/level_NN.json`. After level 30, levels 21–30 repeat.

- **Generation:** levels are generated deterministically from a seed (`LevelGenerator`).
- **Solvability:** an exhaustive solver checks that every level can be won (`Solver.Solve`).
- **Difficulty:** each level is scored by a simulated casual player's win rate, which must fall in a target band for that level number. Difficulty ramps up, with a spike every 5th level.

### Tech

- **Engine:** Unity **6000.3.28f1** (Unity 6.3 LTS) with URP and the Input System. Portrait. Android uses IL2CPP and ARM64.
- **Art:** built in code from primitives, generated meshes and procedural textures, so there are no imported art assets yet:
  - containers use a generated detail texture;
  - the water is the custom `Dockside/Water` shader;
  - the sky and port silhouette are flat generated meshes.
- **UI:** uGUI, built at runtime (`HudView`, `UiFactory`).
- **Data:** progress, coins and booster counts are stored in PlayerPrefs (`SaveData`). Analytics are a stub that logs `[Analytics]` events.

```
Assets/_Game/
  Scripts/Core/      Pure C# game rules, no UnityEngine: LevelData, GameSession, Solver, LevelGenerator
  Scripts/Runtime/   Presentation: GameController, views (containers, ships, environment), HUD, palette, tweens
  Scripts/Editor/    ProjectSetup (scene, materials, palette, splash, mobile rendering), BuildTools (Android APK)
  Shaders/           Water.shader
  Tests/EditMode/    Rules and level-generation tests
  Resources/Levels/  Baked level JSON
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
4. Press **Play**. The pause menu has a "Skip level (test)" button.

The **Dockside** menu in the editor has these commands:

| Menu item | Purpose |
|---|---|
| Setup Project | Recreates the materials, palette and Main scene, and sets player settings, splash and mobile rendering |
| Bake Levels | Regenerates `level_01..30.json` |
| Report Levels | Logs size and solver statistics for every level |
| Build Android APK | Builds `Builds/Android/DocksideDispatch-<version>-<code>.apk` |

**Run the tests:** in the editor, use Window → General → Test Runner → EditMode. From the command line, use Unity's `-runTests -testPlatform EditMode`.

**Build for Android from the command line**, with the editor closed:

```
Unity -batchmode -projectPath dockside-dispatch/DocksideDispatch -buildTarget Android ^
      -executeMethod Dockside.EditorTools.BuildTools.BuildAndroidApk -logFile build.log
```

The APK is signed with Unity's debug key, so it is for sideloading onto test phones only. Store releases will need an upload keystore and an Android App Bundle (AAB).

### Status and next steps

The prototype is playable from start to finish. The art is still placeholder, built from primitives, and there is no audio yet.

Next steps:

1. Play-test on real devices and tune difficulty from the results.
2. Integrate an analytics SDK and ad mediation.
3. Set up Google Play and Apple developer accounts, and an internal testing track.
4. Final art and audio.

## Conventions

- **Build output:** `Library/`, `Temp/`, `Logs/` and `Builds/` are ignored, along with APK and AAB files.
- **Binary assets** (png, jpg, psd, fbx, audio, fonts, pdf) go through Git LFS.
- **Unity scene and asset files** are stored as text YAML, merged with Unity's `UnityYAMLMerge` tool.
