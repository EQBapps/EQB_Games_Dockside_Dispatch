# Dockside Dispatch: Play Games achievements

Import `DocksideDispatch_achievements.zip` in Play Console under **Grow users → Play Games Services → Setup and management → Achievements → Import achievements**. It holds the metadata, the 17 icons (512 × 512, also in `icons/`) and the icon mapping. Regenerate it with `python Tools/make_achievements.py`.

Point values follow Play's rules: multiples of 5, at most 200 per achievement, at most 1,000 for the whole game. This set totals **345**, which leaves room for future achievements.

| # | Name | Description | Type | Steps | Points | Initial state |
|---|---|---|---|---|---|---|
| 1 | First Dispatch | Win your first level. | Standard | | 5 | Revealed |
| 2 | Harbour Hand | Complete Load level 10. | Standard | | 10 | Revealed |
| 3 | Dock Master | Complete Load level 50. | Standard | | 25 | Revealed |
| 4 | Harbour Legend | Complete all 100 Load levels. | Standard | | 50 | Revealed |
| 5 | Change of Shift | Win your first Unload level. | Standard | | 10 | Revealed |
| 6 | Yard Boss | Complete Unload level 50. | Standard | | 25 | Revealed |
| 7 | Unload Legend | Complete all 100 Unload levels. | Standard | | 50 | Revealed |
| 8 | Around the World | Reach the 20th port (Seattle) in either mode. | Standard | | 30 | Revealed |
| 9 | Clean Sheet | Win level 20 or later without using a booster. | Standard | | 15 | Revealed |
| 10 | Comeback Captain | Win a level after taking a second chance. | Standard | | 10 | Revealed |
| 11 | Night Shift | Win a level at night. | Standard | | 10 | Hidden |
| 12 | Crane Operator | Use Crane Lift 25 times. | Incremental | 25 | 10 | Revealed |
| 13 | Key Holder | Open 50 locked containers. | Incremental | 50 | 15 | Revealed |
| 14 | Mystery Solved | Reveal 100 mystery containers. | Incremental | 100 | 15 | Revealed |
| 15 | Long Haul | Dispatch 100 long containers. | Incremental | 100 | 15 | Revealed |
| 16 | Fleet Commander | Send off 500 ships. | Incremental | 500 | 25 | Revealed |
| 17 | Convoy Leader | Fill 500 trucks. | Incremental | 500 | 25 | Revealed |

**Notes**
- **Unlock rules:** "Complete level N" unlocks on winning level N in that mode. "Around the World" unlocks on reaching level 96 in either mode, since every 5 levels is a new port.
- **Existing players:** progress already made (levels and ports reached) counts the first time the player signs in, so they don't have to replay anything.
- **Achievement IDs:** after you save the achievements, Play Console shows an **ID** for each one, and a "Get resources" button that exports them all. Send me that export; the game uses those IDs.
