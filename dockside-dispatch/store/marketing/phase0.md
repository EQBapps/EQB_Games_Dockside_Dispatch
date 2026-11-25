# Dockside Dispatch: Phase 0 store listing work

Positioning (decided 2026-11-24): **parent-facing**. The store listing, ads and posts speak to adults and families ("play
together", "rewards planning"). Nothing is written for, or aimed at, children, and the app stays outside the Play
Families program for now. Avoid in every listing, ad and post:
- Words aimed at kids ("kids", "children", "for ages 7+", "learning game", "educational").
- Claims of learning outcomes ("boosts brain development", "improves IQ"). "Rewards planning" and "a thinking game" are fine.
- Superlatives and calls to action that Play's metadata policy rejects ("best", "#1", "download now"), and emoji in titles.

## 1. Listing text (`store/play/listings/`)

| File | Use for | Short description (80 max) |
|---|---|---|
| `main_en.txt` | Default listing, English (en-US, plus en-GB and en-IN if you add them) | A calm harbour puzzle that rewards planning. Play on phone, tablet or TV. |
| `main_hi.txt` | Hindi (hi-IN) translation of the default listing | योजना बनाकर खेलें: एक शांत बंदरगाह पहेली, फ़ोन, टैबलेट या TV पर। |
| `families_en.txt` | Custom store listing "Families" | A calm thinking puzzle to play together at home, even on your TV. |
| `puzzlers_en.txt` | Custom store listing "Puzzlers" | Easy to start, every 5th level fights back. Can you plan your way through? |

App name stays **Dockside Dispatch**. Paste the plain text; Play shows `**` literally.

## 2. Graphics (`store/play/`)

Made by `python Tools/make_store.py` from the captures in `store/raw/`.
- Phone screenshots 1–8 (1080 × 1920). They replace the old set, which still showed the removed Shuffle booster.
- Tablet screenshots (landscape).
- Android TV screenshots (1920 × 1080), plus the TV banner `tv_banner_1280x720.png`.

## 3. Play Console setup (in this order)

1. **Main listing.** Grow users → Store presence → Main store listing:
   - Paste `main_en.txt` and its short description.
   - Upload the new phone screenshots, tablet screenshots, and the Android TV screenshots and banner.
2. **Hindi.** Same page → Manage translations → Add your own translations → Hindi. Paste `main_hi.txt` and its short
   description. The graphics can stay the same.
3. **Custom store listings.** Store presence → Custom store listings → Create:
   - "Families" (`families_en.txt`) and "Puzzlers" (`puzzlers_en.txt`), each with its own screenshot order:
     - Families: TV shot first, then "Plan ahead" and "No timers".
     - Puzzlers: the Hard-level shot first.
   - Target each by **URL**. Play gives each listing a link with a `listing=` parameter.
   - Use the Families link in parent-facing posts and creator bios, and the Puzzlers link in puzzle communities and challenge posts.
   - Later, target a listing at a Google Ads campaign as well.
4. **Store listing experiment** (A/B test). Store presence → Store listing experiments → Default graphics experiment:
   - Test screenshot 1 "Plan ahead" (control) against the TV / family screenshot first.
   - 50/50 split, run until Play declares a result (usually 1–2 weeks of traffic).
   - Change one thing per experiment.
5. **Promo codes for Premium.** Monetize with Play → Promo codes → Create promotion → One-time product `premium_unlock`.
   - Start with 100 codes. Play allows a limited number per quarter, so hand them out deliberately.
   - Use them for creators, reviewers, giveaways and community moderators. Keep a sheet of who got which code.
6. **Promotional content (LiveOps).** Grow users → Promotional content.
   - Create a "Family game night on the big screen" event card for **Dec 15 – Jan 5**. Submit it at least
     2 weeks ahead, since Play reviews these.
7. **Featuring nomination.** Submit Play's featuring nomination form (Play Console Help → "Get your app featured on
   Google Play"). Lead with the story:
   - an independent Indian studio;
   - a calm planning puzzle for families;
   - full Android TV support with the remote, no ads on TV;
   - colour-blind friendly symbols;
   - 200 levels across 20 world ports.

## 4. Promo video (30 s, for the listing and for Reels/Shorts)

YouTube, unlisted or public, linked in the main listing. Landscape 16:9 for the listing; a 9:16 cut for Reels and Shorts.

**Made (2026-11-25), in store/video:**
- `dockside_promo_vertical_1080x1920.mp4`: 31 s, for Shorts, Reels and TikTok.
- `dockside_promo_landscape_1920x1080.mp4`: 51 s, for YouTube and the Play listing.

Both are real gameplay with captions, the game's own sounds and an original music bed, at -16 LUFS. The end card carries the Play listing QR code (store/marketing/qr), held still for scanning; in the vertical cut it sits clear of the Reels/Shorts caption bar. The landscape cut adds the TV and mode-menu scenes.

To rebuild them:
1. `python Tools/promo/rec.py <dir> all` records the clips from the open Editor. It changes the Editor's PlayerPrefs, so restore them afterwards.
2. `python Tools/promo/make_video.py <dir> all`. Edit the captions in the VERTICAL and LANDSCAPE lists at the top of that file.

The table below was the original storyboard. The filmed living-room TV shot is not in these cuts yet.

| Time | Shot | On-screen text |
|---|---|---|
| 0–3 s | A full ship sails off at sunset (Hong Kong) | Dockside Dispatch |
| 3–9 s | Tapping containers onto ships, one parked in the holding bay, then sent off | Think one move ahead |
| 9–15 s | Hard level 10: HARD tag, a tricky clear, "Hard level cleared!" | Every 5th level is a Hard level |
| 15–21 s | On a living-room TV, a family in the background, remote in hand (filmed) | Play together on the big screen |
| 21–26 s | Night port, Unload trucks driving off, colour-blind symbols visible | 200 levels · 20 world ports · No timers |
| 26–30 s | Logo plus Google Play badge | Free on Google Play |

## 5. Press kit (games.eqbsolutions.com/dockside/press)

To build next:
- Fact sheet: studio, release date, platforms, price model, contact.
- The 30-second video.
- 8 screenshots, the logo and icon.
- A short "for parents" section: no timers, no ads on TV, one optional purchase, colour-blind friendly, plays offline.

## 6. What to measure (Play Console → Statistics and Store performance)

- Store listing conversion rate (visitors → installs), per custom listing and per country.
- Rating and new reviews. The in-app review prompt arrives in 0.7.7, after the 6th cleared level.
- In GameAnalytics: `review_prompt` (asks), and D1/D7 retention per acquisition source.
