namespace Dockside
{
    /// <summary>
    /// How the game earns on each kind of device.
    /// <list type="bullet">
    /// <item>Phones, tablets, Chromebooks and PC: free with ads (an interstitial between some levels, optional rewarded
    /// videos for boosters and a second chance).</item>
    /// <item>TV: no ads at all (ad networks don't serve TVs, and an ad must be closable with the remote). Boosters and
    /// second chances cost coins instead, and wins pay a little more to make up for the missing free videos. The first
    /// <see cref="TvFreeLevels"/> levels of each mode are free; <see cref="Premium"/> unlocks the rest.</item>
    /// <item>Premium (one purchase) also removes the interstitials everywhere. It never locks anything off a TV.</item>
    /// </list>
    /// </summary>
    public static class Monetization
    {
        /// <summary>The ad SDK starts, and the ad-privacy question is asked (where the law requires it).</summary>
        public static bool AdsAllowed => !ScreenInfo.IsTv;

        /// <summary>"Watch a video" offers: +1 booster, +2 bay slots on the lose card.</summary>
        public static bool RewardedVideos => AdsAllowed;

        /// <summary>The occasional full-screen ad between levels.</summary>
        public static bool Interstitials => AdsAllowed && !Premium.Owned;

        /// <summary>Levels of each mode a TV player can play before Premium (3 ports).</summary>
        public const int TvFreeLevels = 15;

        /// <summary>TV only: this level is past the free ones and Premium isn't owned.</summary>
        public static bool NeedsPremium(int level) => ScreenInfo.IsTv && !Premium.Owned && level > TvFreeLevels;

        /// <summary>Coins for a win.</summary>
        public static int WinCoins => ScreenInfo.IsTv ? 30 : 20;
    }
}
