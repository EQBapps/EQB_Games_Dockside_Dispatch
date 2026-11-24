using System;
using System.Collections;
using Dockside.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Dockside
{
    /// <summary>
    /// First-time user experience. Each tip is shown once per player (PlayerPrefs "dd.tut.*"), on a card whose mini
    /// screen plays the mechanic with the game's own pieces (DemoStage):
    ///  - level 1 of each mode: how to play, then a pulsing tap marker on a container that can go straight onto its vehicle;
    ///  - the first time a container has to wait in the holding bay: what the bay is for;
    ///  - the first level with long, locked or hidden containers: a "New" card for that mechanic;
    ///  - the first tap on each booster: what it does (then it goes ahead).
    /// </summary>
    public class TutorialView : MonoBehaviour
    {
        private const string Prefix = "dd.tut.";
        public const string TapKey = "tap", BayKey = "bay", LongKey = "long", LockKey = "lock", HiddenKey = "hidden", LoadIntroKey = "load.intro";

        private Camera _cam;
        private HudView _hud;
        private Palette _pal;
        private RectTransform _canvas, _marker, _ringA, _ringB, _tip;
        private Text _tipText;
        private Func<Vector3?> _target;

        public static bool Seen(string key) => PlayerPrefs.GetInt(Prefix + key, 0) == 1;
        public static void MarkSeen(string key) { PlayerPrefs.SetInt(Prefix + key, 1); PlayerPrefs.Save(); }

        public static TutorialView Create(HudView hud, Camera cam, Palette pal)
        {
            var t = hud.gameObject.AddComponent<TutorialView>();
            t.Build(hud, cam, pal);
            return t;
        }

        private void Build(HudView hud, Camera cam, Palette pal)
        {
            _hud = hud; _cam = cam; _pal = pal;
            _canvas = (RectTransform)hud.transform;

            // Pulsing double ring that sits on the container to tap.
            _marker = UiFactory.Rect("TapMarker", _canvas, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170, 170));
            _ringA = Ring("RingA", 170, 14f);
            _ringB = Ring("RingB", 110, 18f);
            var dot = UiFactory.Rect("Dot", _marker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));
            var di = dot.gameObject.AddComponent<Image>();
            di.sprite = UiFactory.Disc; di.color = Color.white; di.raycastTarget = false;
            _marker.gameObject.SetActive(false);

            // Tip banner under the top bar.
            var tip = UiFactory.Panel("TutorialTip", _canvas.Find("SafeArea") ?? _canvas, pal.uiCream, pal.uiShadow, 8f);
            _tip = (RectTransform)tip.transform;
            _tip.anchorMin = _tip.anchorMax = new Vector2(0.5f, 1); _tip.pivot = new Vector2(0.5f, 1);
            _tip.anchoredPosition = new Vector2(0, -200); _tip.sizeDelta = new Vector2(820, 104);
            _tipText = UiFactory.Label("Text", _tip, "", 42, pal.uiText);
            ((RectTransform)_tipText.transform).offsetMin = new Vector2(36, 10);
            ((RectTransform)_tipText.transform).offsetMax = new Vector2(-36, -10);
            _tip.gameObject.SetActive(false);
        }

        private RectTransform Ring(string name, float size, float thickness)
        {
            var r = UiFactory.Rect(name, _marker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = UiFactory.RingSprite(thickness / size);
            img.color = Color.white;
            img.raycastTarget = false;
            return r;
        }

        // ------------------------------------------------------------------ level hooks
        /// <summary>Called when a level starts; shows any first-time intros once the ships have docked.</summary>
        public void OnLevelStarted(int levelNumber, GameMode mode, GameSession session, Func<int, Vector3?> containerPosition, Func<int, bool> shipReady)
        {
            HidePointer();
            StopAllCoroutines();
            _mode = mode;
            StartCoroutine(Intro(levelNumber, session, containerPosition, shipReady));
        }

        private GameMode _mode;
        private bool Unload => _mode == GameMode.Unload;
        // Unload has its own first-tap pointer and holding-bay tip (the vehicles differ), the mechanic cards are shared.
        private string TapKeyFor => Unload ? "unload.tap" : TapKey;
        private string BayKeyFor => Unload ? "unload.bay" : BayKey;
        public const string UnloadIntroKey = "unload.intro";

        private IEnumerator Intro(int levelNumber, GameSession session, Func<int, Vector3?> containerPosition, Func<int, bool> shipReady)
        {
            yield return new WaitForSeconds(1.2f);   // let the ships sail in first
            while (_hud.IsModalOpen) yield return null;   // e.g. the privacy question on first launch
            bool hasLong = false, hasLock = false, hasHidden = false;
            foreach (var c in session.Level.containers)
            {
                hasLong |= c.length > 1;
                hasLock |= c.kind == ContainerKind.Locked;
                hasHidden |= c.kind == ContainerKind.Hidden;
            }
            if (!Unload && levelNumber == 1 && !Seen(LoadIntroKey))
                yield return Card(LoadIntroKey, null, "How to play", $"{NavInput.Tap} a container to load the ship of its colour. Full ships sail away.", DemoKind.Basics);
            if (Unload && levelNumber == 1 && !Seen(UnloadIntroKey))
                yield return Card(UnloadIntroKey, "NEW MODE", "Unload", "Send each container to the truck of its colour. Full trucks drive off.", DemoKind.Basics);
            if (hasLong && !Seen(LongKey))
                yield return Card(LongKey, "NEW", "Long containers", $"They take 2 slots, on a {Vehicle} or in the holding bay.", DemoKind.Long);
            if (hasLock && !Seen(LockKey))
                yield return Card(LockKey, "NEW", "Locked containers", "Send off the container with the gold key to unlock it.", DemoKind.Locked);
            if (hasHidden && !Seen(HiddenKey))
                yield return Card(HiddenKey, "NEW", "Mystery containers", "A \"?\" shows its colour once it has a clear path.", DemoKind.Hidden);

            if (levelNumber == 1 && !Seen(TapKeyFor))
            {
                // Point at a container that can go straight onto a docked ship of its colour.
                // Prefer the front row (next to the quay): the clearest example for a new player.
                int best = -1;
                for (int i = 0; i < session.ContainerCount; i++)
                {
                    if (!session.CanTap(i) || session.Spec(i).kind != ContainerKind.Normal) continue;
                    if (best >= 0 && session.Spec(i).y >= session.Spec(best).y) continue;
                    for (int b = 0; b < session.BerthCount; b++)
                    {
                        int ship = session.ShipAtBerth(b);
                        if (ship >= 0 && session.Level.ships[ship].color == session.Spec(i).color && shipReady(b)) { best = i; break; }
                    }
                }
                if (best >= 0)
                    ShowPointer(() => containerPosition(best), $"{NavInput.Tap} to load the same-colour {Vehicle}");
            }
        }

        private string Vehicle => Unload ? "truck" : "ship";

        /// <summary>A tutorial card with its demo, shown once the screen is free; done when closed (or replaced).</summary>
        private IEnumerator Card(string key, string eyebrow, string title, string caption, DemoKind demo)
        {
            while (_hud.IsModalOpen) yield return null;
            bool closed = false;
            _hud.ShowDemo(eyebrow, title, caption, demo, _mode, ("Got it", _pal.uiGood, () => { closed = true; _hud.HidePanels(); }));
            MarkSeen(key);
            while (!closed && _hud.IsModalOpen) yield return null;
            yield return new WaitForSeconds(0.2f);
        }

        /// <summary>
        /// The first tap on a booster explains it (with its demo) before it acts: <paramref name="then"/> runs when the
        /// card is closed. False when it has been explained already.
        /// </summary>
        public bool TryShowBoosterIntro(BoosterType type, Action then)
        {
            string key = "booster." + type;
            if (Seen(key)) return false;
            MarkSeen(key);
            var (title, caption, demo) = type switch
            {
                BoosterType.ExtraBerth => ("Extra Berth", "Adds a holding-bay slot for the rest of the level.", DemoKind.ExtraBerth),
                BoosterType.CraneLift => ("Crane Lift", $"{NavInput.Tap} Crane Lift, then a boxed-in container to lift it out.", DemoKind.CraneLift),
                _ => ("Undo", "Takes back your last move, even the one that filled the bay.", DemoKind.Undo),
            };
            Analytics.Event("booster_intro", ("booster", type), ("mode", _mode));
            _hud.ShowDemo("BOOSTER", title, caption, demo, _mode, ("Got it", _pal.uiGood, () => { _hud.HidePanels(); then?.Invoke(); }));
            return true;
        }

        /// <summary>Called after every accepted tap.</summary>
        public void OnTapAccepted()
        {
            if (_target == null) return;
            HidePointer();
            MarkSeen(TapKeyFor);
        }

        /// <summary>Called when a container lands in the holding bay.</summary>
        public void OnContainerToBay()
        {
            if (Seen(BayKeyFor)) return;
            MarkSeen(BayKeyFor);   // now, so a second container reaching the bay before the card shows doesn't queue another
            StartCoroutine(Card(BayKeyFor, "TIP", "The holding bay", $"No {Vehicle} for that colour yet? It waits here. Don't fill the bay!", DemoKind.Bay));
        }

        // ------------------------------------------------------------------ pointer
        private void ShowPointer(Func<Vector3?> worldTarget, string tip)
        {
            _target = worldTarget;
            _tipText.text = tip;
            _tip.gameObject.SetActive(true);
            _marker.gameObject.SetActive(true);
            _marker.SetAsLastSibling();
        }

        public void HidePointer()
        {
            _target = null;
            if (_marker) _marker.gameObject.SetActive(false);
            if (_tip) _tip.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            // Cards (pause, tutorials, win/lose) cover the board: the marker and its tip wait underneath them.
            bool covered = _hud.IsModalOpen;
            _marker.gameObject.SetActive(!covered);
            _tip.gameObject.SetActive(!covered);
            if (covered) return;
            var world = _target();
            if (world == null) { HidePointer(); return; }
            Vector2 screen = _cam.WorldToScreenPoint(world.Value);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out var local);
            _marker.anchorMin = _marker.anchorMax = new Vector2(0.5f, 0.5f);
            _marker.anchoredPosition = local;
            float t = Time.unscaledTime;
            float a = (t * 1.2f) % 1f;
            _ringA.localScale = Vector3.one * Mathf.Lerp(0.55f, 1.15f, a);
            _ringA.GetComponent<Image>().color = new Color(1, 1, 1, 1f - a);
            _ringB.localScale = Vector3.one * (0.92f + 0.08f * Mathf.Sin(t * 6f));
        }
    }
}
