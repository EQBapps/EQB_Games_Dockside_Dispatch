# Dockside Dispatch: Google Play listing

Text and graphics for the Play Console store listing. The graphics are in `play/`; the raw captures they were made from are in `raw/`.

## Graphics

Regenerate with `python Tools/make_store.py` (from the `dockside-dispatch` folder) after replacing the captures in `raw/`.
The captures are real game views from the Editor in Play mode at each resolution, using the current HUD. TV shots are
in TV mode (remote cursor, overscan margins) and at levels 15 or below, which are free on TV.

| Play Console slot | Files | Size |
|---|---|---|
| App icon | `play/icon_512.png` | 512 × 512 |
| Feature graphic | `play/feature_graphic.png` | 1024 × 500 |
| Phone screenshots | `play/screenshot_1.png` … `screenshot_8.png` | 1080 × 1920 (9:16) |
| 7-inch tablet screenshots | `play/tablet/tablet_1.png` … `tablet_6.png` | 2560 × 1440 (16:9) |
| 10-inch tablet screenshots | the same `play/tablet/` set | 2560 × 1440 |
| Chromebook screenshots | the same `play/tablet/` set | 2560 × 1440 |
| Android TV screenshots | `play/tv/tv_1.png` … `tv_4.png` (no captions, the real TV screen) | 1920 × 1080 |
| Android TV banner | `play/tv_banner_1280x720.png` | 1280 × 720 |
| Foldables (if asked) | `play/foldable/` (unfolded 1:1, unfolded 4:3 landscape, unfolded 3:4 portrait, folded 10:16) | as named |
| Premium product image | `play/iap/premium_icon_512.png`, `premium_icon_512x1080.png` | 512 × 512, 512 × 1080 |

Phone screenshots, in listing order:
1. "Think one move ahead" (Load, Singapore, sunset)
2. "Every 5th level is a Hard level" (level 10, HARD tag)
3. "Play together on your TV" (the TV screen on a TV set)
4. "Plan ahead with the holding bay" (two containers parked)
5. "Unload the ship onto the trucks"
6. "No timers, no rush" (Amsterdam at night)
7. "200 levels in 20 world ports" (Sydney)
8. "Two ways to play" (mode menu)

Tablet and Chromebook captions: Think one move ahead · Every 5th level is a Hard level · Unload the ship onto the trucks ·
No timers, no rush · Made for big screens · Two ways to play.

Listing text for each audience and language is in `play/listings/`; the setup steps are in `marketing/phase0.md`.

## Text

**App name** (30 characters max): Dockside Dispatch

**Short description** (80 characters max):
> A calm harbour puzzle that rewards planning. Play on phone, tablet or TV.

(Earlier: "Load ships, unload to trucks: a relaxing container-sorting harbour puzzle!")

**Full description** (4,000 characters max). The current text is `play/listings/main_en.txt` (from 2026-11-24, parent-facing, with Hard levels, TV and Premium). The copy below is the earlier version, kept for reference:

> Welcome to the docks! Sort colourful containers, fill the waiting ships and trucks, and keep the harbour moving from sunrise to starry nights.
>
> **TWO WAYS TO PLAY**
> • LOAD: tap a container with a clear path to the quay to load it onto the ship of the same colour. Fill a ship and it sails away, making room for the next one.
> • UNLOAD: a ship has come in full. Lift each container out of the hold onto the truck of its colour, and watch full trucks drive off down the dock road. Unlocks after level 5.
> • No ship or truck for that colour yet? The container waits in the holding bay, but don't let the bay fill up!
>
> **A TWIST IN EVERY PORT**
> • Long containers fill two slots, so plan where they go.
> • Locked containers open when the container carrying their key is sent off.
> • Mystery containers show their colour once they have a clear path out.
>
> **HANDY BOOSTERS**
> • Extra Berth adds space to the holding bay.
> • Crane Lift plucks a boxed-in container straight out of the stack.
> • Undo takes back your last move, even the one that filled the bay.
>
> **SAIL AROUND THE WORLD**
> Visit 20 famous ports, a new one every 5 levels: Hong Kong, Singapore, Dubai, Mumbai, Amsterdam, Los Angeles, Sydney, Rotterdam, Shanghai, New York, London, Hamburg, Tokyo, San Francisco, Rio de Janeiro, Cape Town, Istanbul, Venice, Barcelona and Seattle. The harbour follows your clock, from sunrise to starry nights.
>
> • 200 hand-tuned levels, 100 in each mode, that get trickier as you go
> • Colour-blind friendly: every colour has its own symbol
> • Relaxing sounds and a cosy, colourful art style
> • Play offline, anytime
>
> Free to play, with optional ads.

**Category:** Game › Puzzle
**Tags:** Puzzle, Casual, Sorting, Relaxing, Offline
**Contact email:** contact@eqbsolutions.com
**Developer:** EQB Business Solutions Private Limited, Hyderabad, India (eqbsolutions.com)
**Privacy policy URL:** https://eqbsolutions.com/game/privacy-policy (source: `privacy-policy.html`)
