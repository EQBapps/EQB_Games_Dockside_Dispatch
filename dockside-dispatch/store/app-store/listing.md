# Dockside Dispatch: App Store listing

Text and answers for the App Store Connect app record. The build steps are in [`../../BUILD_IOS.md`](../../BUILD_IOS.md).

## App information

| Field | Value |
|---|---|
| Name (30) | Dockside Dispatch |
| Subtitle (30) | Sort cargo, fill the ships |
| Bundle ID | com.eqbbusiness.docksidedispatch |
| SKU | dockside-dispatch-ios |
| Primary language | English (U.K.), to match the game's spelling |
| Primary category | Games › Puzzle |
| Secondary category | Games › Casual |
| Content rights | No third-party content |
| Copyright | 2026 EQB Business Solutions Private Limited |
| Privacy policy URL | https://eqbsolutions.com/game/privacy-policy |
| Support URL | https://games.eqbsolutions.com/#contact |
| Marketing URL | https://games.eqbsolutions.com/ |
| Price | Free |
| Availability | All countries and regions. The EU requires the trader declaration (below). |
| iPad | Not supported in this version (the build is iPhone only) |

## Version 0.7.4

**Promotional text** (170; can be changed without a new build):
> Sort colourful containers, fill the ships and trucks, and keep the harbour moving across 20 famous ports. Now with Undo!

**Keywords** (100, exactly 100 used; words in the name are already indexed):
> cargo,ship,container,sort,sorting,harbour,harbor,port,truck,crane,logic,brain,relaxing,offline,color

**Description** (4,000):

```
Welcome to the docks! Sort colourful containers, fill the waiting ships and trucks, and keep the harbour moving from sunrise to starry nights.

TWO WAYS TO PLAY
• LOAD: tap a container with a clear path to the quay to load it onto the ship of the same colour. Fill a ship and it sails away, making room for the next one.
• UNLOAD: a ship has come in full. Lift each container out of the hold onto the truck of its colour, and watch full trucks drive off down the dock road. Unlocks after level 5.
• No ship or truck for that colour yet? The container waits in the holding bay, but don't let the bay fill up!

A TWIST IN EVERY PORT
• Long containers fill two slots, so plan where they go.
• Locked containers open when the container carrying their key is sent off.
• Mystery containers show their colour once they have a clear path out.

HANDY BOOSTERS
• Extra Berth adds space to the holding bay.
• Crane Lift plucks a boxed-in container straight out of the stack.
• Undo takes back your last move, even the one that filled the bay.

SAIL AROUND THE WORLD
Visit 20 famous ports, a new one every 5 levels: Hong Kong, Singapore, Dubai, Mumbai, Amsterdam, Los Angeles, Sydney, Rotterdam, Shanghai, New York, London, Hamburg, Tokyo, San Francisco, Rio de Janeiro, Cape Town, Istanbul, Venice, Barcelona and Seattle. The harbour follows your clock, from sunrise to starry nights.

• 200 hand-tuned levels, 100 in each mode, that get trickier as you go
• 17 Game Center achievements to earn
• Colour-blind friendly: every colour has its own symbol
• Relaxing sounds and a cosy, colourful art style
• Play offline, anytime

Free to play, with optional ads.
```

**Screenshots:** 6.9" iPhone, 1320 × 2868 portrait: the five in [`screenshots/`](screenshots/), in upload order. They show Hong Kong (Load), Singapore (Unload), Amsterdam, Sydney and Shanghai, all captured from the 0.7.4 build with Undo. Apple accepts 1 to 10. iPad screenshots aren't needed while the build is iPhone only.

**App Review information**
- Contact: name, phone and email of whoever answers Apple's questions.
- Sign-in required: No.
- Notes:
  > No account or login. Ads are served through Unity LevelPlay; on first launch the game asks Apple's tracking question before ads start (players in the EU, EEA, UK and Switzerland first answer our personalised-ads question). Achievements use Game Center: Pause > Awards. Reminder notifications are opt-in, offered after an early win, and can be switched off under Pause. The Unload mode unlocks after Load level 5.

## App Privacy (nutrition labels)

The answers below match the Play Data safety form. Before submitting, check them against the privacy details that Unity, ironSource, Meta, Liftoff, Mintegral, InMobi and GameAnalytics publish for App Store apps.

**Data used to track you** (shared with ad partners for ads in other companies' apps):
- Identifiers › Device ID (the advertising identifier, only when the player allows tracking)
- Usage Data › Advertising Data

**Data not linked to you** (the game has no accounts):

| Data type | Purposes |
|---|---|
| Identifiers › Device ID | Third-Party Advertising, Analytics |
| Location › Coarse Location (from the IP address, by ad partners) | Third-Party Advertising, Analytics |
| Usage Data › Product Interaction | Analytics, Third-Party Advertising |
| Usage Data › Advertising Data | Third-Party Advertising, Analytics |
| Diagnostics › Crash Data | Analytics, App Functionality |
| Diagnostics › Performance Data | Analytics |

## Age rating

Answer "None" for every content descriptor (no violence, gambling, mature themes, horror, medical or sexual content).
- **Advertising:** Yes.
- **Unrestricted web access, user-generated content, messaging:** No.
- **Contests and loot boxes:** No.

Expected rating: 4+.

## Other declarations
- **Export compliance:** the build declares `ITSAppUsesNonExemptEncryption = NO` (the game only uses HTTPS), so App Store Connect doesn't ask again for each build.
- **EU Digital Services Act:** declare EQB Business Solutions Private Limited as a **trader**. Its address, phone number and email are then shown on the EU App Store.
- **In-app purchases, subscriptions, in-app events:** none.

## Game Center achievements

In App Store Connect: the app › Services (or Features) › Game Center › Achievements. The IDs must match `GameServices.GameCenterIds` in the code exactly. For each one:
- Reference name: the same as the title.
- Achievable more than once: No.
- Language: English (U.K.).
- Image: the same 512 × 512 icon as on Play, in `../play-games/icons/`.

Points total 345 (Apple allows up to 100 per achievement and 1,000 per game). Night Shift is hidden until earned. On Game Center, incremental achievements (12 to 17) show as percent complete.

| # | Achievement ID | Title | Points | Hidden | Pre-earned description | Earned description | Image |
|---|---|---|---|---|---|---|---|
| 1 | dockside.first_dispatch | First Dispatch | 5 | No | Win your first level. | You won your first level. | a01_first_dispatch.png |
| 2 | dockside.harbour_hand | Harbour Hand | 10 | No | Complete Load level 10. | You completed Load level 10. | a02_harbour_hand.png |
| 3 | dockside.dock_master | Dock Master | 25 | No | Complete Load level 50. | You completed Load level 50. | a03_dock_master.png |
| 4 | dockside.harbour_legend | Harbour Legend | 50 | No | Complete all 100 Load levels. | You completed all 100 Load levels. | a04_harbour_legend.png |
| 5 | dockside.change_of_shift | Change of Shift | 10 | No | Win your first Unload level. | You won your first Unload level. | a05_change_of_shift.png |
| 6 | dockside.yard_boss | Yard Boss | 25 | No | Complete Unload level 50. | You completed Unload level 50. | a06_yard_boss.png |
| 7 | dockside.unload_legend | Unload Legend | 50 | No | Complete all 100 Unload levels. | You completed all 100 Unload levels. | a07_unload_legend.png |
| 8 | dockside.around_the_world | Around the World | 30 | No | Reach the 20th port (Seattle) in either mode. | You reached Seattle, the 20th port. | a08_around_the_world.png |
| 9 | dockside.clean_sheet | Clean Sheet | 15 | No | Win level 20 or later without using a booster. | You won a level without a single booster. | a09_clean_sheet.png |
| 10 | dockside.comeback_captain | Comeback Captain | 10 | No | Win a level after taking a second chance. | You won after taking a second chance. | a10_comeback_captain.png |
| 11 | dockside.night_shift | Night Shift | 10 | Yes | Win a level at night. | You won a level at night. | a11_night_shift.png |
| 12 | dockside.crane_operator | Crane Operator | 10 | No | Use Crane Lift 25 times. | You used Crane Lift 25 times. | a12_crane_operator.png |
| 13 | dockside.key_holder | Key Holder | 15 | No | Open 50 locked containers. | You opened 50 locked containers. | a13_key_holder.png |
| 14 | dockside.mystery_solved | Mystery Solved | 15 | No | Reveal 100 mystery containers. | You revealed 100 mystery containers. | a14_mystery_solved.png |
| 15 | dockside.long_haul | Long Haul | 15 | No | Dispatch 100 long containers. | You dispatched 100 long containers. | a15_long_haul.png |
| 16 | dockside.fleet_commander | Fleet Commander | 25 | No | Send off 500 ships. | You sent off 500 ships. | a16_fleet_commander.png |
| 17 | dockside.convoy_leader | Convoy Leader | 25 | No | Fill 500 trucks. | You filled 500 trucks. | a17_convoy_leader.png |
