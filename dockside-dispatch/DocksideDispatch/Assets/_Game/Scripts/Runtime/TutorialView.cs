using System;
using System.Collections;
using Dockside.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Dockside
{
    /// <summary>
    /// First-time user experience. Each tip is shown once per player (PlayerPrefs "dd.tut.*"):
    ///  - level 1: a pulsing tap marker on a container that can go straight onto its ship;
    ///  - the first time a container has to wait in the holding bay: what the bay is for;
    ///  - the first level with long, locked or hidden containers: a "New!" card for that mechanic.
    /// </summary>
    public class TutorialView : MonoBehaviour
    {
        private const string Prefix = "dd.tut.";
        public const string TapKey = "tap", BayKey = "bay", LongKey = "long", LockKey = "lock", HiddenKey = "hidden";

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
            _tip.anchoredPosition = new Vector2(0, -200); _tip.sizeDelta = new Vector2(900, 130);
            _tipText = UiFactory.Label("Text", _tip, "", 40, pal.uiText);
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
            if (Unload && levelNumber == 1 && !Seen(UnloadIntroKey))
                yield return Card(UnloadIntroKey, "New mode: Unload", "A ship has come in full. Put each container on the truck of its colour; the holding bay takes the rest. Don't let it fill up!");
            if (hasLong && !Seen(LongKey))
                yield return Card(LongKey, "New: long containers", "They fill 2 slots on a ship, a truck or in the holding bay. Plan where they go!");
            if (hasLock && !Seen(LockKey))
                yield return Card(LockKey, "New: locked containers", "Send off the container carrying the gold key to unlock it. Even Crane Lift can't move it until then.");
            if (hasHidden && !Seen(HiddenKey))
                yield return Card(HiddenKey, "New: mystery containers", "A \"?\" container shows its colour once it has a clear path to the quay.");

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
                    ShowPointer(() => containerPosition(best), Unload ? "Tap a container to put it on the truck of the same colour"
                                                                     : "Tap a container to load it onto the ship of the same colour");
            }
        }

        private IEnumerator Card(string key, string title, string body)
        {
            while (_hud.IsModalOpen) yield return null;
            bool closed = false;
            _hud.ShowTip(title, body, () => { closed = true; _hud.HidePanels(); });
            MarkSeen(key);
            while (!closed) yield return null;
            yield return new WaitForSeconds(0.2f);
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
            MarkSeen(BayKeyFor);
            _hud.Toast(Unload ? "No truck for that colour yet, so it waits in the holding bay. Don't let the bay fill up!"
                              : "No ship for that colour yet, so it waits in the holding bay. Don't let the bay fill up!", 4f);
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
