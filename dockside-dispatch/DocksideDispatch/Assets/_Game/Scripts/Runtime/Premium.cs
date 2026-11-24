using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if DOCKSIDE_IAP
using UnityEngine.Purchasing;
#endif

namespace Dockside
{
    /// <summary>
    /// Dockside Premium: one purchase, kept forever on the player's store account (Google Play / App Store).
    /// <list type="bullet">
    /// <item>TV: unlocks the game after the free levels (<see cref="Monetization.TvFreeLevels"/> per mode).</item>
    /// <item>Phones, tablets, Chromebooks and PC: optional. It removes the ads between levels; the game is never locked
    /// there, and the optional videos for rewards stay available for players who choose them.</item>
    /// </list>
    /// The purchase is remembered on the device and checked against the store at each launch, so it carries over to a new
    /// device (or the TV) signed in to the same account, and a refunded purchase is taken back.
    /// Built on Unity IAP (DOCKSIDE_IAP is defined while the com.unity.purchasing package is installed; without it the
    /// store is simply unavailable).
    /// </summary>
    public static class Premium
    {
        /// <summary>The one-time product in Play Console (Monetise > In-app products) and App Store Connect.</summary>
        public const string ProductId = "premium_unlock";
        private const string OwnedKey = "premium_owned";

        public static bool Owned => PlayerPrefs.GetInt(OwnedKey, 0) == 1;

        /// <summary>The store's price in the player's currency ("₹299.00", "$3.99"), or null until the store has answered.</summary>
        public static string Price { get; private set; }

        /// <summary>The store is connected and has returned the product: Buy can start a purchase.</summary>
#if DOCKSIDE_IAP
        public static bool Ready => _store != null && _product != null;
#else
        public static bool Ready => false;
#endif

        /// <summary>Owned changed (bought, restored, or refunded).</summary>
        public static event Action Changed;

#if DOCKSIDE_IAP
        private static StoreController _store;
        private static Product _product;
        private static Action<bool, string> _pending;   // the Buy / Restore call waiting for its answer
#endif

        private static void SetOwned(bool owned)
        {
            if (owned == Owned) return;
            PlayerPrefs.SetInt(OwnedKey, owned ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

#if DOCKSIDE_IAP
        /// <summary>Connects to the store; called once at startup.</summary>
        public static async void Init()
        {
            if (_store != null) return;
            try
            {
                _store = UnityIAPServices.StoreController();
                _store.OnStoreConnected += OnConnected;
                _store.OnStoreDisconnected += d => Debug.LogWarning($"[Dockside] Store disconnected: {d.Message}");
                _store.OnProductsFetched += OnProductsFetched;
                _store.OnProductsFetchFailed += f => Debug.LogWarning($"[Dockside] Store products failed: {f.FailureReason}");
                _store.OnPurchasesFetched += OnPurchasesFetched;
                _store.OnPurchasesFetchFailed += f => Debug.LogWarning($"[Dockside] Store purchases failed: {f.Message}");
                _store.OnPurchasePending += OnPurchasePending;
                _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
                _store.OnPurchaseFailed += OnPurchaseFailed;
                _store.OnPurchaseDeferred += _ => Finish(false, "Your purchase is waiting for approval.");
                _store.OnAuthAccountChanged += OnConnected;   // another account: fetch everything again
                await _store.Connect();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Dockside] Store unavailable: {e.Message}");
            }
        }

        private static void OnConnected()
        {
            _store.FetchProducts(new List<ProductDefinition> { new ProductDefinition(ProductId, ProductType.NonConsumable) });
        }

        private static void OnProductsFetched(List<Product> products)
        {
            _product = products.FirstOrDefault(p => p.definition.id == ProductId);
            Price = _product?.metadata?.localizedPriceString;
            Changed?.Invoke();   // the price is known now
            _store.FetchPurchases();
        }

        private static bool Contains(Order order) =>
            order?.CartOrdered?.Items().Any(i => i?.Product?.definition?.id == ProductId) == true;

        private static void OnPurchasesFetched(Orders orders)
        {
            // What the store says this account owns is the truth: it restores the purchase on a new device, and takes
            // back one that was refunded.
            bool owned = orders.ConfirmedOrders.Any(Contains) || orders.PendingOrders.Any(Contains);
            SetOwned(owned);
            foreach (var pending in orders.PendingOrders.Where(Contains)) _store.ConfirmPurchase(pending);
        }

        private static void OnPurchasePending(PendingOrder order)
        {
            if (!Contains(order)) return;
            SetOwned(true);
            _store.ConfirmPurchase(order);   // acknowledges it with the store (Play refunds unacknowledged purchases)
        }

        private static void OnPurchaseConfirmed(Order order)
        {
            if (!Contains(order)) return;
            if (order is FailedOrder failed) Debug.LogWarning($"[Dockside] Premium confirmation failed: {failed.FailureReason} {failed.Details}");
            Analytics.Event("premium_purchase", ("tv", ScreenInfo.IsTv), ("price", Price ?? ""));
            Finish(true, null);
        }

        private static void OnPurchaseFailed(FailedOrder order)
        {
            if (!Contains(order)) return;
            bool cancelled = order.FailureReason == PurchaseFailureReason.UserCancelled;
            Analytics.Event("premium_failed", ("reason", order.FailureReason.ToString()));
            Finish(false, cancelled ? null : "The purchase didn't go through. You haven't been charged.");
        }

        private static void Finish(bool ok, string message)
        {
            var done = _pending;
            _pending = null;
            done?.Invoke(ok, message);
        }

        /// <summary>
        /// Starts the store's purchase sheet. <paramref name="done"/> gets (bought, message for the player or null).
        /// </summary>
        public static void Buy(Action<bool, string> done)
        {
            if (Owned) { done?.Invoke(true, null); return; }
            if (!Ready) { done?.Invoke(false, "The store isn't available right now. Check your connection and try again."); return; }
            _pending = done;
            Analytics.Event("premium_start", ("tv", ScreenInfo.IsTv));
            _store.PurchaseProduct(_product);
        }

        /// <summary>Asks the store again what this account owns (the App Store requires a Restore button).</summary>
        public static void Restore(Action<bool, string> done)
        {
            if (_store == null) { done?.Invoke(false, "The store isn't available right now."); return; }
            _store.RestoreTransactions((ok, error) =>
            {
                _store.FetchPurchases();
                done?.Invoke(ok, ok ? null : "Couldn't reach the store. Try again later.");
            });
        }
#else
        public static void Init() { }

        public static void Buy(Action<bool, string> done) =>
            done?.Invoke(Owned, Owned ? null : "The store isn't available in this build.");

        public static void Restore(Action<bool, string> done) => done?.Invoke(Owned, Owned ? null : "The store isn't available in this build.");

#endif

        /// <summary>Test tools only: pretend Premium is owned or not.</summary>
        public static void DevSetOwned(bool owned) => SetOwned(owned);
    }
}
