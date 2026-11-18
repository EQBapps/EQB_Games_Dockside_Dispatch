using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dockside
{
    public enum BoosterType { ExtraBerth, CraneLift, Shuffle }

    /// <summary>Top bar (pause, level, coins), booster bar, toasts and the win / lose / pause panels.</summary>
    public class HudView : MonoBehaviour
    {
        public const float TopMargin = 0.11f;     // share of screen height the top bar covers
        public const float BottomMargin = 0.13f;  // share of screen height the booster bar covers

        public event Action PauseClicked;
        public event Action<BoosterType> BoosterClicked;

        private Palette _pal;
        private Text _level, _ships, _coins, _toast, _hint;
        private CanvasGroup _toastGroup;
        private readonly Dictionary<BoosterType, Text> _boosterCounts = new Dictionary<BoosterType, Text>();
        private readonly Dictionary<BoosterType, Image> _boosterBg = new Dictionary<BoosterType, Image>();
        private RectTransform _overlay;
        private Coroutine _toastRoutine;

        public static HudView Create(Palette pal)
        {
            var canvas = UiFactory.CreateCanvas("HUD");
            var hud = canvas.gameObject.AddComponent<HudView>();
            hud.Build(pal, canvas.transform);
            return hud;
        }

        private void Build(Palette pal, Transform root)
        {
            _pal = pal;
            var safe = UiFactory.Rect("SafeArea", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeArea>();

            // ---- Top bar ----
            var top = UiFactory.Rect("TopBar", safe, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(-48, 150));

            var pause = UiFactory.Button("Pause", top, pal.uiCream, pal.uiShadow, () => PauseClicked?.Invoke(), "Pause");
            var prt = (RectTransform)pause.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0, 0.5f); prt.pivot = new Vector2(0, 0.5f);
            prt.anchoredPosition = Vector2.zero; prt.sizeDelta = new Vector2(120, 120);
            UiFactory.Icon("pause", pause.transform, pal.uiText, 70);

            var levelPill = UiFactory.Panel("LevelPill", top, pal.uiCream, pal.uiShadow);
            var lrt = (RectTransform)levelPill.transform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f); lrt.sizeDelta = new Vector2(330, 140);
            var lvlBox = UiFactory.Rect("LevelText", levelPill.transform, new Vector2(0, 0.42f), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _level = UiFactory.Label("Level", lvlBox, "LEVEL 1", 50, pal.uiText);
            var shipsBox = UiFactory.Rect("ShipsText", levelPill.transform, new Vector2(0, 0.06f), new Vector2(1, 0.46f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _ships = UiFactory.Label("Ships", shipsBox, "Ships 0 of 0", 32, pal.uiSubText, TextAnchor.MiddleCenter, FontStyle.Normal);

            var coinPill = UiFactory.Panel("CoinPill", top, pal.uiCream, pal.uiShadow);
            var crt = (RectTransform)coinPill.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1, 0.5f); crt.pivot = new Vector2(1, 0.5f);
            crt.anchoredPosition = Vector2.zero; crt.sizeDelta = new Vector2(250, 104);
            var coin = UiFactory.Panel("Coin", coinPill.transform, Palette.Hex("F7C431"), Palette.Hex("C9952A"), 6f);
            var cort = (RectTransform)coin.transform;
            cort.anchorMin = cort.anchorMax = new Vector2(0, 0.5f); cort.pivot = new Vector2(0, 0.5f);
            cort.anchoredPosition = new Vector2(18, 3); cort.sizeDelta = new Vector2(66, 66);
            var coinText = UiFactory.Rect("CoinText", coinPill.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            coinText.offsetMin = new Vector2(92, 0); coinText.offsetMax = new Vector2(-16, 0);
            _coins = UiFactory.Label("Coins", coinText, "0", 44, pal.uiText, TextAnchor.MiddleCenter);

            // ---- Crane-lift hint (shown under the top bar while the booster is armed) ----
            var hintPanel = UiFactory.Panel("CraneHint", safe, pal.uiAccent, Palette.Darken(pal.uiAccent, 0.3f), 6f);
            var hrt = (RectTransform)hintPanel.transform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1); hrt.pivot = new Vector2(0.5f, 1);
            hrt.anchoredPosition = new Vector2(0, -196); hrt.sizeDelta = new Vector2(760, 84);
            _hint = UiFactory.Label("Hint", hintPanel.transform, "Crane Lift: tap any container", 38, Color.white);
            hintPanel.gameObject.SetActive(false);

            // ---- Booster bar ----
            var bar = UiFactory.Panel("BoosterBar", safe, pal.uiBar);
            var brt = (RectTransform)bar.transform;
            brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 0); brt.pivot = new Vector2(0.5f, 0);
            brt.anchoredPosition = new Vector2(0, -30); brt.sizeDelta = new Vector2(60, 270);
            var row = UiFactory.Rect("Row", bar.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(900, 170));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 42; layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            AddBooster(row, BoosterType.ExtraBerth, "berth", "Extra Berth");
            AddBooster(row, BoosterType.CraneLift, "crane", "Crane Lift");
            AddBooster(row, BoosterType.Shuffle, "shuffle", "Shuffle");

            // ---- Toast ----
            var toast = UiFactory.Panel("Toast", safe, new Color(0.18f, 0.13f, 0.12f, 0.9f));
            var trt = (RectTransform)toast.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0); trt.pivot = new Vector2(0.5f, 0);
            trt.anchoredPosition = new Vector2(0, 290); trt.sizeDelta = new Vector2(940, 150);
            _toast = UiFactory.Label("ToastText", toast.transform, "", 36, Color.white);
            ((RectTransform)_toast.transform).offsetMin = new Vector2(30, 8); ((RectTransform)_toast.transform).offsetMax = new Vector2(-30, -8);
            _toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0; _toastGroup.blocksRaycasts = false;

            // ---- Modal overlay (win / lose / pause) ----
            _overlay = UiFactory.Rect("Overlay", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var dim = _overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0.12f, 0.07f, 0.06f, 0.55f);
            _overlay.gameObject.SetActive(false);
        }

        private void AddBooster(Transform row, BoosterType type, string icon, string label)
        {
            var b = UiFactory.Button(label, row, _pal.uiCream, Palette.Hex("2E2422"), () => BoosterClicked?.Invoke(type), label);
            ((RectTransform)b.transform).sizeDelta = new Vector2(260, 170);
            var iconHolder = UiFactory.Rect("IconHolder", b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(70, 70));
            UiFactory.Icon(icon, iconHolder, _pal.uiText, 70);
            var lab = UiFactory.Rect("Label", b.transform, new Vector2(0, 0), new Vector2(1, 0.38f), new Vector2(0.5f, 0), new Vector2(0, 8), Vector2.zero);
            UiFactory.Label("Text", lab, label, 32, _pal.uiText);
            var badge = UiFactory.Panel("Badge", b.transform, _pal.uiAccent);
            var bart = (RectTransform)badge.transform;
            bart.anchorMin = bart.anchorMax = new Vector2(1, 1); bart.pivot = new Vector2(0.5f, 0.5f);
            bart.anchoredPosition = new Vector2(-14, -10); bart.sizeDelta = new Vector2(66, 66);
            _boosterCounts[type] = UiFactory.Label("Count", badge.transform, "0", 36, Color.white);
            _boosterBg[type] = b.GetComponent<Image>();
        }

        // ------------------------------------------------------------------
        public bool IsModalOpen => _overlay != null && _overlay.gameObject.activeSelf;
        /// <summary>Hides the whole HUD (top bar, boosters, toasts) while the mode menu is open.</summary>
        public void SetVisible(bool visible) => GetComponent<Canvas>().enabled = visible;
        public void SetLevel(int n) => _level.text = $"LEVEL {n}";
        /// <summary>Vehicles sent off so far ("Ships 3 of 10", "Trucks 5 of 18").</summary>
        public void SetShips(int departed, int total, string noun = "Ships") => _ships.text = $"{noun} {departed} of {total}";
        public void SetCoins(int coins) => _coins.text = coins.ToString("N0");

        public void SetBooster(BoosterType type, int count, bool armed = false)
        {
            _boosterCounts[type].text = count > 0 ? count.ToString() : "+";
            _boosterBg[type].color = armed ? Palette.Hex("FFD98A") : _pal.uiCream;
        }

        public void SetCraneHint(bool on) => _hint.transform.parent.gameObject.SetActive(on);

        public void Toast(string message, float seconds = 1.8f)
        {
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            // Draw toasts above everything, including a dimmed card (e.g. "no video available" on the lose card).
            _toastGroup.transform.SetParent(transform, true);
            _toastGroup.transform.SetAsLastSibling();
            _toastRoutine = StartCoroutine(ToastRoutine(message, seconds));
        }

        private IEnumerator ToastRoutine(string message, float seconds)
        {
            _toast.text = message;
            yield return Tween.Run(0.15f, e => _toastGroup.alpha = e);
            yield return new WaitForSeconds(seconds);
            yield return Tween.Run(0.25f, e => _toastGroup.alpha = 1 - e);
        }

        // ------------------------------------------------------------------
        public void HidePanels()
        {
            foreach (Transform child in _overlay) Destroy(child.gameObject);
            _overlay.gameObject.SetActive(false);
        }

        public void ShowWin(int level, int coinsEarned, Action next)
        {
            var card = Card("Level complete!", $"Level {level} cleared\n+{coinsEarned} coins", 1);
            AddCardButton(card, "Next level", _pal.uiGood, next, 0);
        }

        public void ShowLose(string reason, bool canContinue, Action secondChance, string coinsLabel, bool canAffordCoins,
                             Action continueWithCoins, Action retry)
        {
            var card = Card("Out of room!", reason, canContinue ? 3 : 1);
            int i = 0;
            if (canContinue)
            {
                AddCardButton(card, "Watch a video: +2 bay slots", _pal.uiGood, secondChance, i++);
                AddCardButton(card, coinsLabel, canAffordCoins ? Palette.Hex("E0A526") : Palette.Hex("B9AFA4"), continueWithCoins, i++);
            }
            AddCardButton(card, "Retry level", _pal.uiAccent, retry, i);
        }

        /// <summary>After the win that unlocks Unload: a "new mode" card with the choice to try it now or keep loading.</summary>
        public void ShowUnlock(Action tryIt, Action keepLoading)
        {
            var card = Card("New mode unlocked!", "UNLOAD: a ship has come in full.\nSend each container to the truck of its colour.", 2);
            // A "NEW" tag on the card's corner, in the Unload colour.
            var tag = UiFactory.Panel("NewTag", card, Palette.Hex("EE8A2B"), Palette.Hex("2E2422"), 6f);
            var trt = (RectTransform)tag.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(-70, -14); trt.sizeDelta = new Vector2(150, 70);
            trt.localRotation = Quaternion.Euler(0, 0, -8f);
            UiFactory.Label("Text", tag.transform, "NEW", 40, Color.white);
            AddCardButton(card, "Try Unload", Palette.Hex("EE8A2B"), tryIt, 0);
            AddCardButton(card, "Keep loading", Palette.Hex("2F7FE0"), keepLoading, 1);
        }

        /// <summary>Out of a booster: one more for a video or for coins.</summary>
        public void ShowBoosterOffer(string booster, int price, bool canAfford, Action watchVideo, Action buy, Action cancel)
        {
            var card = Card($"Out of {booster}", $"Get one more. You have {SaveData.Coins} coins.", 3);
            AddCardButton(card, "Watch a video: +1", _pal.uiGood, watchVideo, 0);
            AddCardButton(card, $"Buy 1 for {price} coins", canAfford ? Palette.Hex("E0A526") : Palette.Hex("B9AFA4"), buy, 1);
            AddCardButton(card, "Not now", Palette.Hex("8A8F98"), cancel, 2);
        }

        private RectTransform _banner;

        /// <summary>Non-blocking banner under the top bar (e.g. "Next stop: Singapore").</summary>
        public void Banner(string title, string subtitle, float seconds = 2.8f)
        {
            if (_banner) Destroy(_banner.gameObject);
            var panel = UiFactory.Panel("Banner", transform, Palette.Hex("4E3F3B"), Palette.Hex("2E2422"), 8f);
            panel.raycastTarget = false;
            _banner = (RectTransform)panel.transform;
            _banner.anchorMin = _banner.anchorMax = new Vector2(0.5f, 1); _banner.pivot = new Vector2(0.5f, 1);
            _banner.anchoredPosition = new Vector2(0, -230); _banner.sizeDelta = new Vector2(820, 170);
            var t = UiFactory.Rect("Title", _banner, new Vector2(0, 0.42f), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UiFactory.Label("TitleText", t, title, 60, _pal.uiCream);
            var st = UiFactory.Rect("Sub", _banner, new Vector2(0, 0.08f), new Vector2(1, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UiFactory.Label("SubText", st, subtitle, 36, Palette.Hex("E8C25A"), TextAnchor.MiddleCenter, FontStyle.Normal);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = 0f;
            StartCoroutine(BannerRoutine(_banner, group, seconds));
        }

        private IEnumerator BannerRoutine(RectTransform rt, CanvasGroup group, float seconds)
        {
            yield return new WaitForSeconds(0.5f);   // let the ships sail in first
            yield return Tween.Run(0.35f, e => { if (rt) { group.alpha = e; rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, e); } }, Tween.EaseOutBack);
            yield return new WaitForSeconds(seconds);
            yield return Tween.Run(0.4f, e => { if (rt) group.alpha = 1f - e; });
            if (rt) Destroy(rt.gameObject);
        }

        public void ShowPause(Action resume, Action modes, Action restart, Action skip, string soundLabel, Action toggleSound, Action privacy,
                              string skyLabel, Action cycleSky, string port, Action goToLevel = null)
        {
            // Sky preview and Skip level are test tools: the caller passes null for them in release builds.
            var buttons = new System.Collections.Generic.List<(string label, Color color, Action action)>
            {
                ("Resume", _pal.uiGood, resume),
                ("Game modes", Palette.Hex("EE8A2B"), modes),
                (soundLabel, Palette.Hex("2F7FE0"), toggleSound),
            };
            if (cycleSky != null) buttons.Add((skyLabel, Palette.Hex("2F7FE0"), cycleSky));
            buttons.Add(("Privacy", Palette.Hex("9B5DE5"), privacy));
            buttons.Add(("Restart level", _pal.uiAccent, restart));
            if (goToLevel != null) buttons.Add(("Go to level... (test)", Palette.Hex("8A8F98"), goToLevel));
            if (skip != null) buttons.Add(("Skip level (test)", Palette.Hex("8A8F98"), skip));
            var card = Card("Paused", $"Port: {port}", buttons.Count);
            for (int i = 0; i < buttons.Count; i++) AddCardButton(card, buttons[i].label, buttons[i].color, buttons[i].action, i);
        }

        /// <summary>First-launch GDPR question (players in the EU/EEA, UK and Switzerland).</summary>
        public void ShowAdConsent(Action<bool> answer)
        {
            var card = Card("Your privacy",
                "Ads keep Dockside Dispatch free. Can our ad partners use data from this device to show ads that suit you? " +
                "You can change this any time under Pause > Privacy.", 2);
            AddCardButton(card, "Yes, personalise ads", _pal.uiGood, () => answer(true), 0);
            AddCardButton(card, "No, keep ads general", Palette.Hex("8A8F98"), () => answer(false), 1);
        }

        /// <summary>Privacy settings: personalised ads (GDPR) and the "do not sell or share" opt-out (CCPA/CPRA).</summary>
        public void ShowPrivacy(string gdprLabel, Action toggleGdpr, string ccpaLabel, Action toggleCcpa, Action back)
        {
            var card = Card("Privacy", "Choose how ad partners may use data from this device.", 3);
            AddCardButton(card, gdprLabel, Palette.Hex("2F7FE0"), toggleGdpr, 0);
            AddCardButton(card, ccpaLabel, Palette.Hex("2F7FE0"), toggleCcpa, 1);
            AddCardButton(card, "Back", Palette.Hex("8A8F98"), back, 2);
        }

        /// <summary>One-off explainer card (tutorial), closed with a single button.</summary>
        public void ShowTip(string title, string body, Action close)
        {
            var card = Card(title, body, 1);
            AddCardButton(card, "Got it", _pal.uiGood, close, 0);
        }

        private int _cardButtons;

        private RectTransform Card(string title, string body, int buttons)
        {
            HidePanels();
            _overlay.gameObject.SetActive(true);
            var card = UiFactory.Panel("Card", _overlay, _pal.uiCream, _pal.uiShadow, 16f);
            var rt = (RectTransform)card.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            _cardButtons = buttons;
            rt.sizeDelta = new Vector2(860, 420 + buttons * 170);
            var t = UiFactory.Rect("Title", rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(-80, 110));
            UiFactory.Label("TitleText", t, title, 76, _pal.uiText);
            var b = UiFactory.Rect("Body", rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -190), new Vector2(-100, 180));
            UiFactory.Label("BodyText", b, body, 44, _pal.uiSubText, TextAnchor.MiddleCenter, FontStyle.Normal);
            StartCoroutine(Tween.Run(0.3f, e => { if (rt) rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.8f, 1f, e); }, Tween.EaseOutBack));
            return rt;
        }

        /// <summary>Test tool: step through levels with -5/-1/+1/+5 (5 = one port) and play the chosen one.</summary>
        public void ShowLevelPicker(int start, int max, Func<int, string> describe, Action<int> go, Action back)
        {
            int level = Mathf.Clamp(start, 1, max);
            var card = Card("Go to level", describe(level), 3);
            var body = card.Find("Body/BodyText").GetComponent<Text>();
            body.fontSize = 56;
            body.font = UiFactory.FontBold;
            body.color = _pal.uiText;
            var row = UiFactory.Rect("Steps", card, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 400), new Vector2(680, 140));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20; layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            foreach (int step in new[] { -5, -1, 1, 5 })
            {
                int d = step;
                var b = UiFactory.Button(step > 0 ? $"+{step}" : step.ToString(), row, Palette.Hex("2F7FE0"), Palette.Darken(Palette.Hex("2F7FE0"), 0.3f),
                    () => { level = Mathf.Clamp(level + d, 1, max); body.text = describe(level); }, $"step{step}");
                ((RectTransform)b.transform).sizeDelta = new Vector2(155, 130);
                UiFactory.Label("Text", b.transform, step > 0 ? $"+{step}" : step.ToString(), 50, Color.white);
            }
            AddCardButton(card, "Play this level", _pal.uiGood, () => go(level), 1);
            AddCardButton(card, "Back", Palette.Hex("8A8F98"), back, 2);
        }

        private void AddCardButton(RectTransform card, string label, Color color, Action onClick, int index)
        {
            var b = UiFactory.Button(label, card, color, Palette.Darken(color, 0.3f), onClick, label);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(680, 140);
            rt.anchoredPosition = new Vector2(0, 60 + (_cardButtons - 1 - index) * 170); // first button on top
            UiFactory.Label("Text", b.transform, label, 46, Color.white);
        }
    }

    /// <summary>Keeps HUD content inside the device safe area (notches, rounded corners).</summary>
    public class SafeArea : MonoBehaviour
    {
        private Rect _last;
        private void Update()
        {
            var safe = Screen.safeArea;
            if (safe == _last || Screen.width == 0 || Screen.height == 0) return;
            _last = safe;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
