using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dockside
{
    public enum BoosterType { ExtraBerth, CraneLift, Undo }

    /// <summary>
    /// Top bar (pause, level, coins), booster bar, toasts and the win / lose / pause panels.
    /// Portrait: the bar across the top and the booster bar along the bottom. Landscape (tablets, Chromebooks, PC, TV):
    /// side columns, pause and level on the left, coins and the boosters on the right, so the board gets the full height.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        /// <summary>Share of the screen's height the HUD covers at the top; the board camera keeps the board below it.</summary>
        public static float TopMargin => ScreenInfo.Landscape ? 0.03f + ScreenInfo.OverscanY : 0.11f;
        /// <summary>Share of the screen's height the booster bar covers at the bottom.</summary>
        public static float BottomMargin => ScreenInfo.Landscape ? 0.03f + ScreenInfo.OverscanY : 0.13f;
        /// <summary>Share of the screen's width each side column covers in landscape (0 in portrait).</summary>
        public static float SideMargin => ScreenInfo.Landscape
            ? SideColumn * ScreenInfo.CanvasScale / Mathf.Max(1f, Screen.width) + ScreenInfo.OverscanX : 0f;
        private const float SideColumn = 340f;   // canvas units: 24 margin + a 300-wide level pill / 260-wide booster + room for badges

        public event Action PauseClicked;
        public event Action<BoosterType> BoosterClicked;

        private Palette _pal;
        private Text _level, _ships, _coins, _toast, _hint;
        private CanvasGroup _toastGroup;
        private readonly Dictionary<BoosterType, Text> _boosterCounts = new Dictionary<BoosterType, Text>();
        private readonly Dictionary<BoosterType, Image> _boosterBg = new Dictionary<BoosterType, Image>();
        private RectTransform _overlay;
        private Coroutine _toastRoutine;
        // Laid out again whenever the screen turns between portrait and landscape (ApplyLayout).
        private RectTransform _levelPill, _bar, _hintPanel, _toastPanel;
        private Image _barBg;
        private readonly List<RectTransform> _boosters = new List<RectTransform>();
        private readonly List<(RectTransform icon, Text label, RectTransform badge, Text count)> _boosterParts =
            new List<(RectTransform, Text, RectTransform, Text)>();
        private RectTransform _pauseButton, _coinPill;
        private GameObject _hardTag;
        private bool? _landscape;
        // Remote / gamepad / keyboard: the gold cursor over the board, and what Back does on the card that is open.
        private RectTransform _boardFocus;
        private Action _cancel;

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
            _pauseButton = prt;
            UiFactory.Icon("pause", pause.transform, pal.uiText, 70);

            var levelPill = UiFactory.Panel("LevelPill", top, pal.uiCream, pal.uiShadow);
            var lrt = (RectTransform)levelPill.transform;
            _levelPill = lrt;
            var lvlBox = UiFactory.Rect("LevelText", levelPill.transform, new Vector2(0, 0.42f), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _level = UiFactory.Label("Level", lvlBox, "LEVEL 1", 50, pal.uiText);
            var shipsBox = UiFactory.Rect("ShipsText", levelPill.transform, new Vector2(0, 0.06f), new Vector2(1, 0.46f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _ships = UiFactory.Label("Ships", shipsBox, "Ships 0 of 0", 32, pal.uiSubText, TextAnchor.MiddleCenter, FontStyle.Normal);
            // "HARD" tag astride the pill's top edge on every 5th level: red, with the word (not colour alone).
            var hardTag = UiFactory.Panel("HardTag", levelPill.transform, Palette.Hex("D94A3D"), Palette.Hex("2E2422"), 4f);
            var hrt0 = (RectTransform)hardTag.transform;
            hrt0.anchorMin = hrt0.anchorMax = new Vector2(0.5f, 1); hrt0.pivot = new Vector2(0.5f, 0.5f);
            hrt0.anchoredPosition = new Vector2(0, 2); hrt0.sizeDelta = new Vector2(124, 44);
            UiFactory.Label("Text", hardTag.transform, "HARD", 30, Color.white);
            _hardTag = hardTag.gameObject;
            _hardTag.SetActive(false);

            var coinPill = UiFactory.Panel("CoinPill", top, pal.uiCream, pal.uiShadow);
            var crt = (RectTransform)coinPill.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1, 0.5f); crt.pivot = new Vector2(1, 0.5f);
            crt.anchoredPosition = Vector2.zero; crt.sizeDelta = new Vector2(250, 104);
            _coinPill = crt;
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
            hrt.sizeDelta = new Vector2(760, 84);
            _hintPanel = hrt;
            _hint = UiFactory.Label("Hint", hintPanel.transform, $"Crane Lift: {NavInput.tap} any container", 38, Color.white);
            hintPanel.gameObject.SetActive(false);

            // ---- Booster bar ----
            var bar = UiFactory.Panel("BoosterBar", safe, pal.uiBar);
            _bar = (RectTransform)bar.transform;
            _barBg = bar;
            AddBooster(_bar, BoosterType.ExtraBerth, "berth", "Extra Berth");
            AddBooster(_bar, BoosterType.CraneLift, "crane", "Crane Lift");
            AddBooster(_bar, BoosterType.Undo, "undo", "Undo");

            // ---- Toast ----
            var toast = UiFactory.Panel("Toast", safe, new Color(0.18f, 0.13f, 0.12f, 0.9f));
            var trt = (RectTransform)toast.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0); trt.pivot = new Vector2(0.5f, 0);
            trt.sizeDelta = new Vector2(940, 150);
            _toastPanel = trt;
            _toast = UiFactory.Label("ToastText", toast.transform, "", 36, Color.white);
            ((RectTransform)_toast.transform).offsetMin = new Vector2(30, 8); ((RectTransform)_toast.transform).offsetMax = new Vector2(-30, -8);
            _toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0; _toastGroup.blocksRaycasts = false;

            // ---- Board cursor (remote, gamepad, keyboard): a gold ring over the container in focus, under any card ----
            _boardFocus = UiFactory.Rect("BoardFocus", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100, 100));
            var bf = _boardFocus.gameObject.AddComponent<Image>();
            bf.sprite = UiFactory.RoundedOutline; bf.type = Image.Type.Sliced; bf.color = UiFactory.FocusColor; bf.raycastTarget = false;
            _boardFocus.gameObject.SetActive(false);

            // ---- Modal overlay (win / lose / pause) ----
            _overlay = UiFactory.Rect("Overlay", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var dim = _overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0.12f, 0.07f, 0.06f, 0.55f);
            _overlay.gameObject.SetActive(false);

            ApplyLayout(ScreenInfo.Landscape);
        }

        private const float BoosterW = 260, BoosterH = 170;

        private void Update()
        {
            // Tablets, Chromebooks and PC windows can turn or be resized at any time.
            if (_landscape != ScreenInfo.Landscape) ApplyLayout(ScreenInfo.Landscape);

            // Remote / gamepad: a card that opens gets its top button selected, so OK always does something sensible.
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return;
            var sel = es.currentSelectedGameObject;
            if (IsModalOpen)
            {
                if (NavInput.NavMode && (sel == null || !sel.activeInHierarchy || !sel.transform.IsChildOf(_overlay)))
                {
                    foreach (var b in _overlay.GetComponentsInChildren<Button>())
                        if (b.IsInteractable() && b.GetComponent<FocusRing>()) { es.SetSelectedGameObject(b.gameObject); break; }
                }
            }
            else if (sel != null && (!sel.activeInHierarchy || sel.transform.IsChildOf(_overlay))) es.SetSelectedGameObject(null);
            if (_boardFocus.gameObject.activeSelf && !MotionPrefs.Reduced)
                _boardFocus.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.unscaledTime * 5f));
        }

        /// <summary>Back on the remote / gamepad / keyboard while a card is open: the card's own way out, if it has one.</summary>
        public void Cancel() => _cancel?.Invoke();

        /// <summary>The gold cursor over the board, around a screen rectangle in pixels (null hides it).</summary>
        public void SetBoardFocus(Rect? pixels)
        {
            _boardFocus.gameObject.SetActive(pixels.HasValue);
            if (!pixels.HasValue) return;
            var r = pixels.Value;
            float k = 1f / ScreenInfo.CanvasScale;
            const float Pad = 14f;
            _boardFocus.anchoredPosition = new Vector2((r.center.x - Screen.width * 0.5f) * k, (r.center.y - Screen.height * 0.5f) * k);
            _boardFocus.sizeDelta = new Vector2(r.width * k + 2 * Pad, r.height * k + 2 * Pad);
        }

        /// <summary>Index of the booster button the remote / gamepad has selected, or -1.</summary>
        public int SelectedBooster
        {
            get
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                var sel = es ? es.currentSelectedGameObject : null;
                for (int i = 0; i < _boosters.Count; i++) if (sel == _boosters[i].gameObject) return i;
                return -1;
            }
        }

        /// <summary>Moves the remote / gamepad selection to a booster (or, with -1, off the HUD and back to the board).</summary>
        public void SelectBooster(int index)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es) es.SetSelectedGameObject(index >= 0 && index < _boosters.Count ? _boosters[index].gameObject : null);
        }

        public int BoosterCount => _boosters.Count;

        /// <summary>Places the level pill, the boosters, the crane hint and toasts for a portrait or a landscape screen.</summary>
        private void ApplyLayout(bool landscape)
        {
            _landscape = landscape;
            if (landscape)
            {
                // A landscape screen is 1080 units tall whatever its size, so the corner pieces come down a little from
                // their portrait size; on a TV (viewed from the sofa) everything is a fifth larger again.
                float k = ScreenInfo.IsTv ? 1.2f : 1f;
                _pauseButton.localScale = Vector3.one * 0.85f * k;
                _coinPill.localScale = Vector3.one * 0.85f * k;
                // Left column: the level pill under the pause button.
                _levelPill.localScale = Vector3.one * 0.85f * k;
                _levelPill.anchorMin = _levelPill.anchorMax = new Vector2(0, 0.5f); _levelPill.pivot = new Vector2(0, 1);
                _levelPill.anchoredPosition = new Vector2(0, -66 * k); _levelPill.sizeDelta = new Vector2(300, 140);
                // Right column: a compact dark rail (the portrait booster bar, turned on its side) with square tiles,
                // centred on the right edge where a thumb rests on a tablet.
                const float Tile = 150, Gap = 12, Pad = 14;
                int n = _boosters.Count;
                _barBg.enabled = true;
                _bar.localScale = Vector3.one * k;
                _bar.anchorMin = _bar.anchorMax = _bar.pivot = new Vector2(1, 0.5f);
                _bar.anchoredPosition = new Vector2(-24, -30);
                _bar.sizeDelta = new Vector2(Tile + 2 * Pad, n * Tile + (n - 1) * Gap + 2 * Pad);
                for (int i = 0; i < n; i++)
                {
                    var b = _boosters[i];
                    b.sizeDelta = new Vector2(Tile, Tile);
                    b.anchorMin = b.anchorMax = b.pivot = new Vector2(0.5f, 1);
                    b.anchoredPosition = new Vector2(0, -Pad - i * (Tile + Gap));
                    var (icon, label, badge, count) = _boosterParts[i];
                    icon.anchoredPosition = new Vector2(0, 18); icon.localScale = Vector3.one * (60f / 70f);
                    label.fontSize = 26;
                    badge.anchoredPosition = new Vector2(-8, -8); badge.sizeDelta = new Vector2(50, 50);
                    count.fontSize = 28;
                }
                _hintPanel.anchoredPosition = new Vector2(0, -40);
                _toastPanel.anchoredPosition = new Vector2(0, 40);
            }
            else
            {
                _pauseButton.localScale = _coinPill.localScale = _levelPill.localScale = _bar.localScale = Vector3.one;
                _levelPill.anchorMin = _levelPill.anchorMax = _levelPill.pivot = new Vector2(0.5f, 0.5f);
                _levelPill.anchoredPosition = Vector2.zero; _levelPill.sizeDelta = new Vector2(330, 140);
                // The booster bar along the bottom, its three buttons in a row.
                const float Gap = 42;
                _barBg.enabled = true;
                _bar.anchorMin = new Vector2(0, 0); _bar.anchorMax = new Vector2(1, 0); _bar.pivot = new Vector2(0.5f, 0);
                _bar.anchoredPosition = new Vector2(0, -30); _bar.sizeDelta = new Vector2(60, 270);
                for (int i = 0; i < _boosters.Count; i++)
                {
                    var b = _boosters[i];
                    b.sizeDelta = new Vector2(BoosterW, BoosterH);
                    b.anchorMin = b.anchorMax = b.pivot = new Vector2(0.5f, 0.5f);
                    b.anchoredPosition = new Vector2((i - (_boosters.Count - 1) * 0.5f) * (BoosterW + Gap), 14);
                    var (icon, label, badge, count) = _boosterParts[i];
                    icon.anchoredPosition = new Vector2(0, 22); icon.localScale = Vector3.one;
                    label.fontSize = 32;
                    badge.anchoredPosition = new Vector2(-14, -10); badge.sizeDelta = new Vector2(66, 66);
                    count.fontSize = 36;
                }
                _hintPanel.anchoredPosition = new Vector2(0, -196);
                _toastPanel.anchoredPosition = new Vector2(0, 290);
            }
            // Remote / gamepad: step along the boosters (across in portrait, down the column in landscape). Stepping off
            // towards the board is handled by the game controller, which moves the cursor back onto the containers.
            for (int i = 0; i < _boosters.Count; i++)
            {
                var prev = i > 0 ? _boosters[i - 1].GetComponent<Button>() : null;
                var next = i < _boosters.Count - 1 ? _boosters[i + 1].GetComponent<Button>() : null;
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                if (landscape) { nav.selectOnUp = prev; nav.selectOnDown = next; }
                else { nav.selectOnLeft = prev; nav.selectOnRight = next; }
                _boosters[i].GetComponent<Button>().navigation = nav;
            }
        }

        private void AddBooster(Transform row, BoosterType type, string icon, string label)
        {
            var b = UiFactory.Button(label, row, _pal.uiCream, Palette.Hex("2E2422"), () => BoosterClicked?.Invoke(type), label);
            ((RectTransform)b.transform).sizeDelta = new Vector2(BoosterW, BoosterH);
            _boosters.Add((RectTransform)b.transform);
            var iconHolder = UiFactory.Rect("IconHolder", b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(70, 70));
            UiFactory.Icon(icon, iconHolder, _pal.uiText, 70);
            var lab = UiFactory.Rect("Label", b.transform, new Vector2(0, 0), new Vector2(1, 0.38f), new Vector2(0.5f, 0), new Vector2(0, 8), Vector2.zero);
            var labText = UiFactory.Label("Text", lab, label, 32, _pal.uiText);
            var badge = UiFactory.Panel("Badge", b.transform, _pal.uiAccent);
            var bart = (RectTransform)badge.transform;
            bart.anchorMin = bart.anchorMax = new Vector2(1, 1); bart.pivot = new Vector2(0.5f, 0.5f);
            bart.anchoredPosition = new Vector2(-14, -10); bart.sizeDelta = new Vector2(66, 66);
            _boosterCounts[type] = UiFactory.Label("Count", badge.transform, "0", 36, Color.white);
            _boosterBg[type] = b.GetComponent<Image>();
            _boosterParts.Add((iconHolder, labText, bart, _boosterCounts[type]));
        }

        // ------------------------------------------------------------------
        public bool IsModalOpen => _overlay != null && _overlay.gameObject.activeSelf;
        /// <summary>Hides the whole HUD (top bar, boosters, toasts) while the mode menu is open.</summary>
        public void SetVisible(bool visible) => GetComponent<Canvas>().enabled = visible;
        public void SetLevel(int n, bool hard = false)
        {
            _level.text = $"LEVEL {n}";
            _hardTag.SetActive(hard);
        }
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
            if (DemoStage.Instance) DemoStage.Instance.Stop();
        }

        public void ShowWin(int level, int coinsEarned, bool hard, Action next)
        {
            var card = Card(hard ? "Hard level cleared!" : "Level complete!", $"Level {level} cleared\n+{coinsEarned} coins", 1);
            AddCardButton(card, "Next level", _pal.uiGood, next, 0);
        }

        /// <summary>Lost: undo the last move (when the player has an Undo), a second chance (once), or retry.</summary>
        public void ShowLose(string reason, bool canContinue, Action secondChance, string coinsLabel, bool canAffordCoins,
                             Action continueWithCoins, Action retry, string undoLabel = null, Action undo = null)
        {
            // secondChance is null where there are no videos (TV): the coins option is then the only second chance.
            bool video = secondChance != null;
            var card = Card("Out of room!", reason, (canContinue ? (video ? 3 : 2) : 1) + (undo != null ? 1 : 0));
            int i = 0;
            if (undo != null) AddCardButton(card, undoLabel, Palette.Hex("2F7FE0"), undo, i++, "undo");
            if (canContinue)
            {
                if (video) AddCardButton(card, "+2 bay slots", _pal.uiGood, secondChance, i++, "video");
                AddCardButton(card, coinsLabel, canAffordCoins ? Palette.Hex("E0A526") : Palette.Hex("B9AFA4"), continueWithCoins, i++, "coin");
            }
            AddCardButton(card, "Retry level", _pal.uiAccent, retry, i, "restart");
        }

        /// <summary>After the win that unlocks Unload: a "new mode" card with the choice to try it now or keep loading.</summary>
        public void ShowUnlock(Action tryIt, Action keepLoading)
        {
            var card = ShowDemo(null, "New mode unlocked!", "UNLOAD: send each container to the truck of its colour.", DemoKind.Basics, Core.GameMode.Unload,
                ("Try Unload", Palette.Hex("EE8A2B"), tryIt), ("Keep loading", Palette.Hex("2F7FE0"), keepLoading));
            // A "NEW" tag on the card's corner, in the Unload colour.
            var tag = UiFactory.Panel("NewTag", card, Palette.Hex("EE8A2B"), Palette.Hex("2E2422"), 6f);
            var trt = (RectTransform)tag.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(-70, -14); trt.sizeDelta = new Vector2(150, 70);
            trt.localRotation = Quaternion.Euler(0, 0, -8f);
            UiFactory.Label("Text", tag.transform, "NEW", 40, Color.white);
        }

        /// <summary>Out of a booster: one more for a video or for coins.</summary>
        public void ShowBoosterOffer(string booster, int price, bool canAfford, Action watchVideo, Action buy, Action cancel)
        {
            // watchVideo is null where there are no videos (TV).
            int i = 0;
            var card = Card($"Out of {booster}", $"You have {SaveData.Coins} coins.", watchVideo != null ? 3 : 2);
            if (watchVideo != null) AddCardButton(card, "+1 free", _pal.uiGood, watchVideo, i++, "video");
            AddCardButton(card, $"+1 for {price}", canAfford ? Palette.Hex("E0A526") : Palette.Hex("B9AFA4"), buy, i++, "coin");
            AddCardButton(card, "Not now", Palette.Hex("8A8F98"), cancel, i);
            _cancel = cancel;
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
            // Under the top bar in portrait; in landscape the top centre is open sky.
            _banner.anchoredPosition = new Vector2(0, ScreenInfo.Landscape ? -40 - Screen.height * ScreenInfo.OverscanY / ScreenInfo.CanvasScale : -230);
            _banner.sizeDelta = new Vector2(820, 170);
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

        /// <summary>
        /// Pause menu: one big Resume, then icon tiles (two rows of three). Toggles show their state with the icon and
        /// an ON/OFF badge, not colour alone. Test builds get one more row that opens the test tools.
        /// </summary>
        public void ShowPause(string port, Action resume, Action modes, Action achievements, Action privacy, Action restart,
                              bool soundOn, Action toggleSound, bool remindersOn, Action toggleReminders, Action testTools = null,
                              string premiumLabel = null, Action premium = null)
        {
            const float TileH = 210, Gap = 26, GridW = 250 * 3 + Gap * 2;
            // Tiles whose action is null don't apply here (on a TV: Privacy, which is about ads, and Reminders).
            var tiles = new List<(string icon, string label, Color color, Action action, string badge)>
            {
                ("modes", "Modes", Palette.Hex("EE8A2B"), modes, null),
                ("trophy", "Awards", Palette.Hex("1B8C8A"), achievements, null),
                ("shield", "Privacy", Palette.Hex("9B5DE5"), privacy, null),
                (soundOn ? "sound_on" : "sound_off", "Sound", Palette.Hex("2F7FE0"), toggleSound, soundOn ? "ON" : "OFF"),
                (remindersOn ? "bell_on" : "bell_off", "Reminders", Palette.Hex("2F7FE0"), toggleReminders, remindersOn ? "ON" : "OFF"),
                ("restart", "Restart", _pal.uiAccent, restart, null),
            };
            tiles.RemoveAll(t => t.action == null);
            int cols = tiles.Count == 4 ? 2 : 3, rows = (tiles.Count + cols - 1) / cols;
            float TileW = (GridW - Gap * (cols - 1)) / cols;
            float gridTop = 420, gridH = TileH * rows + Gap * (rows - 1);
            var rt = CardFrame("Paused", port, 900, 0);
            gridTop += _headerExtra;
            float height = gridTop + gridH + (premium != null ? 170 : 0) + (testTools != null ? 170 : 0) + 70;

            var resumeBtn = UiFactory.Button("Resume", rt, _pal.uiGood, Palette.Darken(_pal.uiGood, 0.3f), resume, "Resume");
            var brt = (RectTransform)resumeBtn.transform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 1);
            brt.anchoredPosition = new Vector2(0, -250 - _headerExtra); brt.sizeDelta = new Vector2(GridW, 140);
            IconAndLabel(brt, "play", "Resume", 54, 64);

            for (int i = 0; i < tiles.Count; i++)
            {
                var (icon, label, color, action, badge) = tiles[i];
                var tile = UiFactory.Button(label, rt, color, Palette.Darken(color, 0.3f), action, label);
                var trt = (RectTransform)tile.transform;
                trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 1);
                trt.sizeDelta = new Vector2(TileW, TileH);
                trt.anchoredPosition = new Vector2((i % cols - (cols - 1) * 0.5f) * (TileW + Gap), -gridTop - (i / cols) * (TileH + Gap));
                var ih = UiFactory.Rect("IconHolder", trt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 26), new Vector2(92, 92));
                UiFactory.Icon(icon, ih, Color.white, 92);
                var lab = UiFactory.Rect("Label", trt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(0, 54));
                UiFactory.Label("Text", lab, label, 40, Color.white);
                if (badge != null)
                {
                    var b = UiFactory.Panel("Badge", trt, badge == "ON" ? Palette.Hex("2FB36B") : Palette.Hex("6F6A66"), Palette.Hex("2E2422"), 4f);
                    var bt = (RectTransform)b.transform;
                    bt.anchorMin = bt.anchorMax = new Vector2(1, 1); bt.pivot = new Vector2(0.5f, 0.5f);
                    bt.anchoredPosition = new Vector2(-30, -6); bt.sizeDelta = new Vector2(92, 52);
                    UiFactory.Label("Text", b.transform, badge, 32, Color.white);
                }
            }

            float rowTop = gridTop + gridH + 50;
            if (premium != null)
            {
                // Premium: "Remove ads" on phones and tablets, "Unlock the full game" on a TV. Gold, under the tiles.
                var gold = Palette.Hex("E0A526");
                var pb = UiFactory.Button("Premium", rt, gold, Palette.Darken(gold, 0.3f), premium, "Premium");
                var pt = (RectTransform)pb.transform;
                pt.anchorMin = pt.anchorMax = pt.pivot = new Vector2(0.5f, 1);
                pt.anchoredPosition = new Vector2(0, -rowTop); pt.sizeDelta = new Vector2(GridW, 110);
                IconAndLabel(pt, "star", premiumLabel, 44, 54);
                rowTop += 170;
            }
            if (testTools != null)
            {
                var tools = UiFactory.Button("Test tools", rt, Palette.Hex("8A8F98"), Palette.Darken(Palette.Hex("8A8F98"), 0.3f), testTools, "Test tools");
                var tt = (RectTransform)tools.transform;
                tt.anchorMin = tt.anchorMax = tt.pivot = new Vector2(0.5f, 1);
                tt.anchoredPosition = new Vector2(0, -rowTop); tt.sizeDelta = new Vector2(GridW, 110);
                UiFactory.Label("Text", tools.transform, "Test tools", 40, Color.white);
            }
            FinishCard(rt, height);
            _cancel = resume;
        }

        /// <summary>
        /// Premium: what it gives on this device, the store's price on the buy button, Restore (App Store only, where it is
        /// required), and a way out.
        /// </summary>
        public void ShowPremium(string title, string body, string buyLabel, Action buy, Action restore, string closeLabel, Action close)
        {
            int n = restore != null ? 3 : 2, i = 0;
            var card = Card(title, body, n);
            AddCardButton(card, buyLabel, Palette.Hex("E0A526"), buy, i++, "star");
            if (restore != null) AddCardButton(card, "Restore purchase", Palette.Hex("2F7FE0"), restore, i++);
            AddCardButton(card, closeLabel, Palette.Hex("8A8F98"), close, i);
            _cancel = close;
        }

        /// <summary>A plain list of buttons (e.g. the test tools), first one on top.</summary>
        public void ShowButtons(string title, string body, System.Collections.Generic.IList<(string label, Color color, Action action)> buttons)
        {
            var card = Card(title, body, buttons.Count);
            for (int i = 0; i < buttons.Count; i++)
            {
                AddCardButton(card, buttons[i].label, buttons[i].color, buttons[i].action, i);
                if (buttons[i].label == "Back" || buttons[i].label == "Close") _cancel = buttons[i].action;
            }
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
            _cancel = back;
        }

        /// <summary>Our own opt-in for reminders, shown before Android's permission dialog (never at first launch).</summary>
        public void ShowReminderOffer(Action yes, Action notNow)
        {
            var card = Card("Want a nudge?",
                "We'll nudge you when ships are waiting. At most once a day, never at night.", 2);
            AddCardButton(card, "Yes, remind me", _pal.uiGood, yes, 0);
            AddCardButton(card, "Not now", Palette.Hex("8A8F98"), notNow, 1);
            _cancel = notNow;
        }

        /// <summary>Reminders were switched on but Android won't show them: point the player to the app's notification settings.</summary>
        public void ShowRemindersBlocked(Action openSettings, Action back)
        {
            var card = Card("Notifications are off",
                "Notifications for this game are off in your phone's settings.", 2);
            AddCardButton(card, "Open settings", _pal.uiGood, openSettings, 0);
            AddCardButton(card, "Back", Palette.Hex("8A8F98"), back, 1);
            _cancel = back;
        }

        private int _cardButtons;
        // Long cards (the pause menu in test builds) pack their buttons tighter so the card still fits a 16:9 screen.
        private int ButtonStep => _cardButtons > 10 ? 125 : _cardButtons > 9 ? 135 : _cardButtons > 8 ? 150 : 170;

        private RectTransform Card(string title, string body, int buttons)
        {
            const float Width = 860, BodyMin = 180;
            var rt = CardFrame(title, null, Width, buttons);
            var b = UiFactory.Rect("Body", rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -190 - _headerExtra), new Vector2(-100, BodyMin));
            var text = UiFactory.Label("BodyText", b, body, 44, _pal.uiSubText, TextAnchor.MiddleCenter, FontStyle.Normal);
            // A long body (e.g. the privacy question) grows the card instead of running into the buttons.
            float bodyH = Mathf.Max(BodyMin, TextHeight(text, Width - 100) + 24);
            b.sizeDelta = new Vector2(-100, bodyH);
            FinishCard(rt, 420 + _headerExtra + (bodyH - BodyMin) + buttons * ButtonStep);
            return rt;
        }

        // How much taller the card's header is than a one-line title (an eyebrow tag, a two-line title).
        // Everything below the header moves down by this much.
        private float _headerExtra;

        /// <summary>
        /// The cream card on the dimmed overlay, with its title, an optional eyebrow tag above it ("NEW") and an optional
        /// one-line subtitle under it. The title is fitted to one line (76 px down to 60 px) and only wraps to two lines
        /// when it is still too long; the caller lays its content out <see cref="_headerExtra"/> lower and then calls
        /// <see cref="FinishCard"/> with the card's height.
        /// </summary>
        private RectTransform CardFrame(string title, string subtitle, float width, int buttons, string eyebrow = null)
        {
            const float TitleH = 110, TitleMax = 76, TitleMin = 60, EyebrowH = 54, EyebrowGap = 4;
            HidePanels();
            _cancel = null;
            _overlay.gameObject.SetActive(true);
            var card = UiFactory.Panel("Card", _overlay, _pal.uiCream, _pal.uiShadow, 16f);
            var rt = (RectTransform)card.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            _cardButtons = buttons;
            rt.sizeDelta = new Vector2(width, 0);
            float top = subtitle != null ? 40 : 60;
            _headerExtra = 0;

            if (!string.IsNullOrEmpty(eyebrow))
            {
                var chip = UiFactory.Panel("Eyebrow", rt, Palette.Hex("4E3F3B"));
                var crt = (RectTransform)chip.transform;
                crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 1);
                var ct = UiFactory.Label("EyebrowText", crt, eyebrow, 34, _pal.uiCream);
                crt.sizeDelta = new Vector2(TextWidth(ct) + 48, EyebrowH);
                crt.anchoredPosition = new Vector2(0, -top + 16);
                top += EyebrowH - 16 + EyebrowGap;
                _headerExtra += EyebrowH - 16 + EyebrowGap;
            }

            float titleW = width - 80;
            var t = UiFactory.Rect("Title", rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -top), new Vector2(-80, TitleH));
            var tt = UiFactory.Label("TitleText", t, title, (int)TitleMax, _pal.uiText);
            for (int size = (int)TitleMax; size > TitleMin && TextWidth(tt) > titleW; size -= 4) tt.fontSize = size - 4;
            float titleH = Mathf.Max(TitleH, TextHeight(tt, titleW) + 16);
            t.sizeDelta = new Vector2(-80, titleH);
            _headerExtra += titleH - TitleH;

            if (subtitle != null)
            {
                var st = UiFactory.Rect("Subtitle", rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -150 - _headerExtra), new Vector2(-80, 60));
                UiFactory.Label("SubtitleText", st, subtitle, 40, _pal.uiSubText, TextAnchor.MiddleCenter, FontStyle.Normal);
            }
            return rt;
        }

        /// <summary>
        /// Sizes the card, then scales it down (uniformly, so the layout holds) if it doesn't fit inside the safe area
        /// with a margin, centres it there, and animates it in.
        /// </summary>
        private void FinishCard(RectTransform rt, float height)
        {
            const float MarginX = 30, MarginY = 48;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            float scale = 1f;
            var area = _overlay.rect;
            if (area.width > 1 && area.height > 1 && Screen.width > 0 && Screen.height > 0)
            {
                var safe = ScreenInfo.SafeArea;
                float sx = area.width / Screen.width, sy = area.height / Screen.height;
                float availW = safe.width * sx - 2 * MarginX, availH = safe.height * sy - 2 * MarginY;
                scale = Mathf.Min(1f, availW / rt.sizeDelta.x, availH / height);
                rt.anchoredPosition = new Vector2((safe.center.x - Screen.width * 0.5f) * sx, (safe.center.y - Screen.height * 0.5f) * sy);
            }
            rt.localScale = Vector3.one * scale;
            AnimateIn(rt, scale);
        }

        /// <summary>Cards fade in with a slight scale (under 200 ms, no bounce); with reduced motion they just appear.</summary>
        private void AnimateIn(RectTransform rt, float scale)
        {
            if (MotionPrefs.Reduced) return;
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            StartCoroutine(Tween.Run(0.18f, e =>
            {
                if (!rt) return;
                group.alpha = e;
                rt.localScale = Vector3.one * (scale * Mathf.Lerp(0.95f, 1f, e));
            }, Tween.EaseOutCubic));
        }

        /// <summary>Width of the text on one line, in canvas units.</summary>
        private static float TextWidth(Text t) =>
            t.cachedTextGeneratorForLayout.GetPreferredWidth(t.text, t.GetGenerationSettings(Vector2.zero)) / t.pixelsPerUnit;

        /// <summary>Height of the text wrapped to <paramref name="width"/>, in canvas units.</summary>
        private static float TextHeight(Text t, float width) =>
            t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, t.GetGenerationSettings(new Vector2(width, 0))) / t.pixelsPerUnit;

        /// <summary>
        /// Tutorial card: title, a mini screen where a short scene shows the mechanic (DemoStage), a one- or two-line
        /// caption, and its buttons.
        /// </summary>
        public RectTransform ShowDemo(string eyebrow, string title, string caption, DemoKind kind, Core.GameMode mode, params (string label, Color color, Action action)[] buttons)
        {
            const float Width = 880, ScreenW = 780, ScreenH = ScreenW * DemoStage.TexHeight / DemoStage.TexWidth, CaptionMin = 120;
            int n = buttons.Length;
            var rt = CardFrame(title, null, Width, n, eyebrow);
            float top = 178 + _headerExtra;   // just under the title

            // Mini screen: a dark rounded frame, the demo texture inside it (rounded by a mask), and the tap overlay.
            var frame = UiFactory.Panel("ScreenFrame", rt, Palette.Hex("2E2422"));
            var frt = (RectTransform)frame.transform;
            frt.anchorMin = frt.anchorMax = frt.pivot = new Vector2(0.5f, 1);
            frt.anchoredPosition = new Vector2(0, -top); frt.sizeDelta = new Vector2(ScreenW + 20, ScreenH + 20);
            var maskImg = UiFactory.Panel("Mask", frt, Color.white);
            var mrt = (RectTransform)maskImg.transform;
            UiFactory.Stretch(mrt, 10f);
            maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var screen = UiFactory.Rect("Screen", mrt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var raw = screen.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            var stage = DemoStage.Instance;
            if (stage)
            {
                stage.Play(kind, mode);
                raw.texture = stage.Texture;
                DemoOverlay.Attach(screen, _pal);
            }

            var cap = UiFactory.Rect("Caption", rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -top - ScreenH - 40), new Vector2(-90, CaptionMin));
            var ct = UiFactory.Label("CaptionText", cap, caption, 42, _pal.uiSubText, TextAnchor.MiddleCenter, FontStyle.Normal);
            float capH = Mathf.Max(CaptionMin, TextHeight(ct, Width - 90) + 20);
            cap.sizeDelta = new Vector2(-90, capH);
            for (int i = 0; i < n; i++) AddCardButton(rt, buttons[i].label, buttons[i].color, buttons[i].action, i);
            if (n == 1) _cancel = buttons[0].action;   // a tutorial card's "Got it"
            FinishCard(rt, top + 5 + ScreenH + 150 + (capH - CaptionMin) + n * ButtonStep + 50);
            return rt;
        }

        /// <summary>An icon (white) and a label side by side, centred in a button.</summary>
        private static void IconAndLabel(RectTransform button, string icon, string label, int fontSize, float iconSize)
        {
            var row = UiFactory.Rect("Content", button, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, iconSize));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 22; layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fit = row.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var ih = UiFactory.Rect("IconHolder", row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iconSize, iconSize));
            if (icon == "coin")
            {
                var coin = UiFactory.Panel("Coin", ih, Palette.Hex("F7C431"), Palette.Hex("C9952A"), 5f);
                UiFactory.Stretch((RectTransform)coin.transform, iconSize * 0.08f);
            }
            else UiFactory.Icon(icon, ih, Color.white, iconSize);
            var t = UiFactory.Label("Text", row, label, fontSize, Color.white);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            trt.sizeDelta = new Vector2(t.preferredWidth + 4, Mathf.Max(iconSize, t.preferredHeight));
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
            _cancel = back;
        }

        private void AddCardButton(RectTransform card, string label, Color color, Action onClick, int index, string icon = null)
        {
            var b = UiFactory.Button(label, card, color, Palette.Darken(color, 0.3f), onClick, label);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(680, ButtonStep - 30);
            rt.anchoredPosition = new Vector2(0, 60 + (_cardButtons - 1 - index) * ButtonStep); // first button on top
            if (icon != null) IconAndLabel(rt, icon, label, 46, 60);
            else UiFactory.Label("Text", b.transform, label, 46, Color.white);
        }
    }

    /// <summary>Keeps HUD content inside the device safe area (notches, rounded corners).</summary>
    public class SafeArea : MonoBehaviour
    {
        private Rect _last;
        private void Update()
        {
            var safe = ScreenInfo.SafeArea;
            if (safe == _last || Screen.width == 0 || Screen.height == 0) return;
            _last = safe;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
