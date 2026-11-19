# Dockside Dispatch: Google Play listing

Text and graphics for the Play Console store listing. The graphics are in `play/`; the raw captures they were made from are in `raw/`.

## Graphics

Regenerate with `python Tools/make_store.py` (from the `dockside-dispatch` folder) after replacing the captures in `raw/`.

| Asset | File | Play requirement |
|---|---|---|
| App icon | `play/icon_512.png` | 512 × 512 PNG |
| Feature graphic | `play/feature_graphic.png` | 1024 × 500, no transparency |
| Phone screenshots | `play/screenshot_1.png` … `screenshot_6.png` | 1080 × 1920 (9:16), 2–8 required |

The screenshots show:
1. Load, Hong Kong at sunset. Caption: "Sort the cargo, fill the ships!"
2. Unload, Singapore at midday. Caption: "Unload the ship onto the trucks!"
3. The mode menu with Unload selected. Caption: "Two ways to play"
4. Load, Amsterdam at night. Caption: "From sunrise to starry nights"
5. Unload, Sydney in the morning. Caption: "Sail to famous ports worldwide"
6. Load, Shanghai in the afternoon. Caption: "200 levels of harbour puzzles"

### Foldable screenshots (`play/foldable/`)

Real game-view captures at each aspect ratio (the layout adapts to the screen), no phone frame:

| Play slot | File | Size |
|---|---|---|
| Unfolded 1:1 | `unfolded_1x1_load_amsterdam_night.png` | 1600 × 1600 |
| Unfolded 4:3 | `unfolded_3x4_unload_singapore_midday.png` (portrait, matches the portrait-only game) | 1536 × 2048 |
| Unfolded 4:3, fallback | `unfolded_4x3_landscape_unload_singapore_midday.png`, only if Play rejects the portrait one | 2048 × 1536 |
| Folded portrait 10:16 | `folded_10x16_load_hongkong_sunset.png` | 1200 × 1920 |

## Text

**App name** (30 characters max): Dockside Dispatch

**Short description** (80 characters max):
> Load ships, unload to trucks: a relaxing container-sorting harbour puzzle!

**Full description** (4,000 characters max; paste the plain-text copy in `play/full_description.txt`, since Play shows `**` literally):

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
> • Shuffle mixes the colours.
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
