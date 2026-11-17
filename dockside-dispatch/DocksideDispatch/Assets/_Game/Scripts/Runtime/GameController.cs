using System.Collections;
using System.Collections.Generic;
using Dockside.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Dockside
{
    /// <summary>
    /// Runs a level: builds the views, turns taps into GameSession moves, and plays the
    /// resulting events back as animations in order.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        public const float CameraPitch = 55f;
        private const int WinCoins = 20;
        /// <summary>Coin prices: boosters when you run out, and a continue after losing (the alternative to a video).</summary>
        public const int ContinuePrice = 120;
        public static int BoosterPrice(BoosterType t) => t switch { BoosterType.ExtraBerth => 60, BoosterType.CraneLift => 90, _ => 40 };
        private const int MaxBufferSlots = 7;

        private Palette _pal;
        private Camera _cam;
        private HudView _hud;
        private TutorialView _tutorial;
        private CraneRig _craneRig;
        private int _craneLifted = -1;   // container being carried by the Crane Lift animation

        private GameSession _session;
        private BoardLayout _layout;
        private Transform _levelRoot, _yardRoot, _shipsRoot, _envRoot;
        private BufferSlotsView _bufferSlots;
        private readonly Dictionary<int, ContainerView> _containers = new Dictionary<int, ContainerView>();
        private ShipView[] _ships;
        private readonly List<int> _viewBuffer = new List<int>();   // holding bay as the player currently sees it
        private readonly Queue<GameEvent> _pending = new Queue<GameEvent>();
        private bool _playing;
        private int _levelNumber, _shipsDepartedShown;
        private bool _craneArmed, _usedSecondChance, _ended;
        private float _levelStartTime, _lastAspect;
        private int _boostersUsed;
        private readonly System.Random _rng = new System.Random();

        public void Init(Palette palette, Camera cam)
        {
            _pal = palette;
            _cam = cam;
            _hud = HudView.Create(palette);
            _hud.PauseClicked += OnPause;
            _hud.BoosterClicked += OnBooster;
            _tutorial = TutorialView.Create(_hud, cam, palette);
            _hud.SetCoins(SaveData.Coins);
            foreach (BoosterType t in System.Enum.GetValues(typeof(BoosterType))) _hud.SetBooster(t, SaveData.Boosters(t));
            StartLevel(SaveData.Level);
            // GDPR players are asked once before any ads load (LevelPlayInitializer waits for the answer).
            if (Privacy.NeedsGdprAnswer)
                _hud.ShowAdConsent(yes =>
                {
                    Privacy.GdprAnswer = yes ? 1 : 0;
                    Analytics.Event("privacy_consent", ("gdpr", yes));
                    _hud.HidePanels();
                });
        }

        // =====================================================================
        // Level setup
        // =====================================================================
        public void StartLevel(int levelNumber)
        {
            StopAllCoroutines();
            _pending.Clear();
            _playing = false;
            _hud.HidePanels();
            if (_levelRoot) Destroy(_levelRoot.gameObject);
            _containers.Clear();
            _viewBuffer.Clear();
            _craneArmed = false;
            _hud.SetCraneHint(false);
            _usedSecondChance = false;
            _ended = false;
            _boostersUsed = 0;

            _levelNumber = levelNumber;
            var level = LevelRepository.Get(levelNumber);
            _session = new GameSession(level);
            _layout = new BoardLayout(level.width, level.height, level.berths);
            _layout.FitCamera(_cam, CameraPitch, HudView.TopMargin, HudView.BottomMargin);
            _lastAspect = _cam.aspect;

            _levelRoot = new GameObject($"Level_{levelNumber}").transform;
            _envRoot = EnvironmentBuilder.Build(_levelRoot, _layout, _pal, _cam, levelNumber);
            // A new port every few levels: announce it.
            int port = PortSkylines.IndexForLevel(levelNumber);
            if (levelNumber == 1 || port != PortSkylines.IndexForLevel(levelNumber - 1))
            {
                int first = (levelNumber - 1) / PortSkylines.LevelsPerPort * PortSkylines.LevelsPerPort + 1;
                _hud.Banner($"Next stop: {PortSkylines.Names[port]}", $"Levels {first}-{first + PortSkylines.LevelsPerPort - 1}");
                Analytics.Event("port_arrival", ("port", PortSkylines.Names[port]), ("level", levelNumber));
            }

            _bufferSlots = new GameObject("HoldingBay").AddComponent<BufferSlotsView>();
            _bufferSlots.transform.SetParent(_levelRoot, false);
            _bufferSlots.Init(_pal);
            _bufferSlots.SetCapacity(_session.BufferCapacity);
            _bufferSlots.SetUsed(SlotOfViewItem(_viewBuffer.Count));

            _craneRig = CraneRig.Create(_levelRoot, _pal);
            _craneLifted = -1;

            _yardRoot = new GameObject("Yard").transform;
            _yardRoot.SetParent(_levelRoot, false);
            for (int i = 0; i < _session.ContainerCount; i++)
            {
                var spec = _session.Spec(i);
                var view = ContainerView.Create(_yardRoot, spec, _pal);
                view.transform.localScale = _layout.YardContainerScale(spec.length);
                view.transform.position = _layout.CellCenter(spec.x, spec.y, spec.length);
                _containers[i] = view;
            }

            _shipsRoot = new GameObject("Ships").transform;
            _shipsRoot.SetParent(_levelRoot, false);
            _ships = new ShipView[_session.BerthCount];
            for (int b = 0; b < _session.BerthCount; b++)
            {
                int ship = _session.ShipAtBerth(b);
                if (ship >= 0) StartCoroutine(DockShip(b, ship, 0.15f * b));
            }

            _shipsDepartedShown = 0;
            _hud.SetLevel(levelNumber);
            _hud.SetShips(0, _session.ShipsTotal);
            RefreshYard();
            _levelStartTime = Time.time;
            Analytics.Event("level_start", ("level", levelNumber), ("seed", level.seed), ("containers", level.containers.Count),
                ("ships", level.ships.Count));
            _tutorial.OnLevelStarted(levelNumber, _session,
                i => _containers.TryGetValue(i, out var v) && v && _session.InYard(i) ? v.transform.position + Vector3.up * 0.45f : (Vector3?)null,
                b => _ships != null && _ships[b] && Vector3.Distance(_ships[b].transform.position, BerthPosition(b)) < 0.3f);
        }

        private IEnumerator DockShip(int berth, int shipIndex, float delay)
        {
            var spec = _session.Level.ships[shipIndex];
            var ship = ShipView.Create(_shipsRoot, shipIndex, spec.color, spec.capacity, _pal);
            _ships[berth] = ship;
            ship.transform.position = BerthPosition(berth) + new Vector3(-12f, 0, 0);
            if (delay > 0) yield return new WaitForSeconds(delay);
            yield return ship.SailIn(BerthPosition(berth), 0.5f);
        }

        private Vector3 BerthPosition(int berth) => new Vector3(_layout.BerthX(berth), -0.05f, BoardLayout.ShipZ);

        private void RefreshYard()
        {
            foreach (var kv in _containers)
            {
                if (!_session.InYard(kv.Key)) continue;
                kv.Value.SetLocked(_session.IsLocked(kv.Key));
                kv.Value.SetDim(!_session.IsReachable(kv.Key) || _session.IsLocked(kv.Key));
            }
        }

        // =====================================================================
        // Input
        // =====================================================================
        private void Update()
        {
            if (_cam && !Mathf.Approximately(_cam.aspect, _lastAspect) && _layout != null)
            {
                _lastAspect = _cam.aspect;
                _layout.FitCamera(_cam, CameraPitch, HudView.TopMargin, HudView.BottomMargin);
            }
            if (_session == null || _session.IsOver || _hud.IsModalOpen) return;
            if (!TryGetPress(out Vector2 screenPos)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            var ray = _cam.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 200f)) return;
            var view = hit.collider.GetComponentInParent<ContainerView>();
            if (view != null) OnContainerTapped(view);
        }

        private static bool TryGetPress(out Vector2 pos)
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                pos = pointer.position.ReadValue();
                return true;
            }
#else
            if (Input.GetMouseButtonDown(0)) { pos = Input.mousePosition; return true; }
#endif
            pos = default;
            return false;
        }

        private void OnContainerTapped(ContainerView view)
        {
            int index = -1;
            foreach (var kv in _containers) if (kv.Value == view) { index = kv.Key; break; }
            if (index < 0 || !_session.InYard(index)) return;

            var events = new List<GameEvent>();
            // Crane Lift is only spent when it's needed: a container that can already move just moves (booster stays armed).
            bool useCrane = _craneArmed && !_session.CanTap(index);
            var result = _session.Tap(index, events, ignorePath: useCrane);
            switch (result)
            {
                case TapResult.Accepted:
                    _tutorial.OnTapAccepted();
                    Sfx.Play("tap", 0.8f);
                    if (useCrane)
                    {
                        _craneLifted = index;   // played back by the crane rig, not the usual arc
                        ConsumeBooster(BoosterType.CraneLift);
                        _craneArmed = false;
                        _hud.SetCraneHint(false);
                        _hud.SetBooster(BoosterType.CraneLift, SaveData.Boosters(BoosterType.CraneLift));
                    }
                    Enqueue(events);
                    break;
                case TapResult.Blocked:
                    StartCoroutine(Tween.Shake(view.transform));
                    Sfx.Play("blocked", 0.7f);
                    _hud.Toast("Blocked: clear a path to the quay first");
                    break;
                case TapResult.Locked:
                    StartCoroutine(Tween.Shake(view.transform));
                    Sfx.Play("blocked", 0.7f);
                    _hud.Toast("Locked: send off the container with the key, or lift it out with Crane Lift");
                    break;
                case TapResult.NoRoom:
                    StartCoroutine(Tween.Shake(view.transform));
                    Sfx.Play("blocked", 0.7f);
                    _hud.Toast("No room for that in the holding bay");
                    break;
            }
        }

        // =====================================================================
        // Event playback
        // =====================================================================
        private void Enqueue(List<GameEvent> events)
        {
            foreach (var e in events) _pending.Enqueue(e);
            if (!_playing) StartCoroutine(PlayEvents());
        }

        private IEnumerator PlayEvents()
        {
            _playing = true;
            while (_pending.Count > 0)
            {
                var e = _pending.Dequeue();
                yield return PlayEvent(e);
            }
            _playing = false;
        }

        private IEnumerator PlayEvent(GameEvent e)
        {
            switch (e.type)
            {
                case GameEventType.ContainerToShip:
                case GameEventType.BufferToShip:
                {
                    var view = _containers[e.containerId];
                    var ship = _ships[e.berth];
                    if (e.type == GameEventType.BufferToShip) _viewBuffer.Remove(e.containerId);
                    view.Detach();
                    // Ship may still be sailing in: wait until it is near its berth.
                    while (ship && Vector3.Distance(ship.transform.position, BerthPosition(e.berth)) > 0.3f) yield return null;
                    int len = view.Length;
                    view.SetRevealed(true);
                    if (e.containerId == _craneLifted)
                    {
                        _craneLifted = -1;
                        yield return _craneRig.Carry(view.transform, () => ship.SlotWorldPosition(e.slot, len),
                            BoardLayout.ContainerScale(len) * BoardLayout.ShipScale, ship.transform.rotation);
                    }
                    else
                    {
                        var target = ship.SlotWorldPosition(e.slot, len);
                        Sfx.Play("lift", 0.6f);
                        yield return Tween.Arc(view.transform, target, 1.6f, 0.3f, BoardLayout.ContainerScale(len) * BoardLayout.ShipScale, ship.transform.rotation);
                        Sfx.Play("land");
                    }
                    view.transform.SetParent(ship.transform, true);
                    view.transform.localPosition = ship.SlotLocalPosition(e.slot, len);
                    view.transform.localRotation = Quaternion.identity;
                    ship.SetLoaded(ship.Loaded + len);
                    StartCoroutine(Tween.Pop(view.transform, 0.25f, 0.18f));
                    if (e.type == GameEventType.BufferToShip) yield return RelayoutBuffer(0.12f);
                    break;
                }
                case GameEventType.ContainerToBuffer:
                {
                    var view = _containers[e.containerId];
                    view.Detach();
                    _viewBuffer.Add(e.containerId);
                    int cap = _session.BufferCapacity;
                    var target = BoardLayout.BufferPosition(SlotOfViewItem(_viewBuffer.Count - 1), view.Length, cap);
                    view.SetRevealed(true);
                    if (e.containerId == _craneLifted)
                    {
                        _craneLifted = -1;
                        yield return _craneRig.Carry(view.transform, () => target, BoardLayout.BufferScale(view.Length, cap), Quaternion.identity);
                    }
                    else
                    {
                        Sfx.Play("lift", 0.6f);
                        yield return Tween.Arc(view.transform, target, 1.4f, 0.26f, BoardLayout.BufferScale(view.Length, cap), Quaternion.identity);
                    }
                    Sfx.Play("bay");
                    _bufferSlots.SetUsed(SlotOfViewItem(_viewBuffer.Count));
                    _tutorial.OnContainerToBay();
                    StartCoroutine(Tween.Pop(view.transform, 0.2f, 0.16f));
                    break;
                }
                case GameEventType.BufferCompacted:
                    yield return RelayoutBuffer(0.15f);
                    break;
                case GameEventType.ShipDeparted:
                {
                    var ship = _ships[e.berth];
                    _ships[e.berth] = null;
                    _shipsDepartedShown++;
                    _hud.SetShips(_shipsDepartedShown, _session.ShipsTotal);
                    if (ship)
                    {
                        StartCoroutine(Tween.Pop(ship.transform, 0.12f, 0.25f));
                        yield return new WaitForSeconds(0.12f);
                        Sfx.Play("horn", 0.8f);
                        StartCoroutine(SailAway(ship));
                        yield return new WaitForSeconds(0.25f);
                    }
                    break;
                }
                case GameEventType.ShipArrived:
                    yield return DockShip(e.berth, e.shipIndex, 0f);
                    break;
                case GameEventType.ContainerRevealed:
                    if (_containers.TryGetValue(e.containerId, out var rv))
                    {
                        rv.SetRevealed(true);
                        Sfx.Play("reveal", 0.7f);
                        StartCoroutine(Tween.Pop(rv.transform, 0.2f, 0.25f));
                    }
                    break;
                case GameEventType.ContainerUnlocked:
                    if (_containers.TryGetValue(e.containerId, out var uv))
                    {
                        uv.SetLocked(false);
                        Sfx.Play("unlock");
                        StartCoroutine(Tween.Pop(uv.transform, 0.25f, 0.3f));
                    }
                    RefreshYard();
                    break;
                case GameEventType.ReachabilityChanged:
                    RefreshYard();
                    break;
                case GameEventType.Won:
                    yield return new WaitForSeconds(0.6f);
                    OnWon();
                    break;
                case GameEventType.Lost:
                    yield return new WaitForSeconds(0.4f);
                    OnLost();
                    break;
            }
        }

        private IEnumerator SailAway(ShipView ship)
        {
            yield return ship.SailOut(0.9f);
            if (ship) Destroy(ship.gameObject);
        }

        private int SlotOfViewItem(int position)
        {
            int s = 0;
            for (int i = 0; i < position; i++) s += _containers[_viewBuffer[i]].Length;
            return s;
        }

        private IEnumerator RelayoutBuffer(float duration)
        {
            int cap = _session.BufferCapacity;
            for (int p = 0; p < _viewBuffer.Count; p++)
            {
                var v = _containers[_viewBuffer[p]];
                var to = BoardLayout.BufferPosition(SlotOfViewItem(p), v.Length, cap);
                StartCoroutine(Tween.Move(v.transform, to, duration));
                StartCoroutine(Tween.ScaleTo(v.transform, BoardLayout.BufferScale(v.Length, cap), duration));
            }
            _bufferSlots.SetUsed(SlotOfViewItem(_viewBuffer.Count));
            yield return new WaitForSeconds(duration);
        }

        // =====================================================================
        // End of level
        // =====================================================================
        private void OnWon()
        {
            if (_ended) return;
            _ended = true;
            SaveData.Coins += WinCoins;
            _hud.SetCoins(SaveData.Coins);
            Analytics.Coins(true, WinCoins, "level_reward", $"level_{_levelNumber:000}");
            SaveData.Level = _levelNumber + 1;
            Analytics.Event("level_complete", ("level", _levelNumber), ("moves", _session.MovesMade),
                ("seconds", Mathf.RoundToInt(Time.time - _levelStartTime)), ("boosters", _boostersUsed), ("second_chance", _usedSecondChance));
            Sfx.Play("win");
            int completed = _levelNumber;
            _hud.ShowWin(_levelNumber, WinCoins, () =>
            {
                // A capped, opportunistic interstitial between levels; the game never waits for an ad.
                var ads = AdManager.Instance;
                if (ads == null || !ads.TryShowInterstitialAfterLevel(completed, () => StartLevel(SaveData.Level)))
                    StartLevel(SaveData.Level);
            });
        }

        private void OnLost()
        {
            if (!_session.IsLost) return; // a second chance may already have cleared it
            string reason = _session.LoseReason == LoseReason.NoMoves
                ? "No container can move right now."
                : "The holding bay is full.";
            Analytics.Event("level_fail", ("level", _levelNumber), ("reason", _session.LoseReason), ("moves", _session.MovesMade),
                ("seconds", Mathf.RoundToInt(Time.time - _levelStartTime)), ("second_chance", _usedSecondChance));
            Sfx.Play("lose");
            _hud.ShowLose(reason, !_usedSecondChance, SecondChance,
                coinsLabel: $"Use {ContinuePrice} coins: +2 bay slots", canAffordCoins: SaveData.Coins >= ContinuePrice,
                continueWithCoins: ContinueWithCoins, retry: () => StartLevel(_levelNumber));
        }

        private void SecondChance()
        {
            // Rewarded video: the reward (2 extra bay slots) is granted when the player has earned it.
            var ads = AdManager.Instance;
            if (ads == null || !ads.ShowRewarded("second_chance", GrantSecondChance))
                _hud.Toast("No video is available right now. Try again in a moment.");
        }

        private void ContinueWithCoins()
        {
            if (!TrySpend(ContinuePrice, "continue", "second_chance")) return;
            GrantSecondChance();
        }

        /// <summary>Spends coins if the player has enough; otherwise says how many more they need.</summary>
        private bool TrySpend(int price, string itemType, string itemId)
        {
            if (SaveData.Coins < price)
            {
                _hud.Toast($"You need {price - SaveData.Coins} more coins. Each level you finish pays {WinCoins}.");
                return false;
            }
            SaveData.Coins -= price;
            _hud.SetCoins(SaveData.Coins);
            Sfx.Play("unlock", 0.7f);
            Analytics.Coins(false, price, itemType, itemId);
            return true;
        }

        private void GrantSecondChance()
        {
            if (_usedSecondChance || _session == null || !_session.IsLost) return;
            _usedSecondChance = true;
            _hud.HidePanels();
            var events = new List<GameEvent>();
            _session.AddBufferSlots(2, events);
            _bufferSlots.SetCapacity(_session.BufferCapacity);
            StartCoroutine(RelayoutBuffer(0.2f));
            Enqueue(events);
        }

        // =====================================================================
        // Boosters and pause
        // =====================================================================
        private void OnBooster(BoosterType type)
        {
            if (_session == null || _session.IsOver || _hud.IsModalOpen) return;
            int count = SaveData.Boosters(type);
            if (count <= 0)
            {
                // Out of this booster: one more for a video, or buy it with coins.
                void Give()
                {
                    SaveData.SetBoosters(type, SaveData.Boosters(type) + 1);
                    _hud.SetBooster(type, SaveData.Boosters(type));
                    _hud.Toast($"+1 {Label(type)}");
                }
                int price = BoosterPrice(type);
                _hud.ShowBoosterOffer(Label(type), price, SaveData.Coins >= price,
                    watchVideo: () =>
                    {
                        _hud.HidePanels();
                        var ads = AdManager.Instance;
                        if (ads == null || !ads.ShowRewarded("booster_" + type, Give))
                            _hud.Toast("No video is available right now. Try again in a moment.");
                    },
                    buy: () => { _hud.HidePanels(); if (TrySpend(price, "booster", type.ToString())) Give(); },
                    cancel: () => _hud.HidePanels());
                return;
            }
            switch (type)
            {
                case BoosterType.ExtraBerth:
                    if (_session.BufferCapacity >= MaxBufferSlots) { _hud.Toast("The holding bay can't grow any bigger"); return; }
                    var ev = new List<GameEvent>();
                    _session.AddBufferSlots(1, ev);
                    _bufferSlots.SetCapacity(_session.BufferCapacity);
                    Sfx.Play("berth");
                    StartCoroutine(RelayoutBuffer(0.2f));
                    Enqueue(ev);
                    ConsumeBooster(type);
                    break;
                case BoosterType.CraneLift:
                    _craneArmed = !_craneArmed;
                    if (_craneArmed) Sfx.Play("crane", 0.8f);
                    _hud.SetCraneHint(_craneArmed);
                    _hud.SetBooster(type, count, _craneArmed);
                    break;
                case BoosterType.Shuffle:
                    var sev = new List<GameEvent>();
                    _session.ShuffleYard(_rng, sev);
                    Sfx.Play("shuffle");
                    foreach (var kv in _containers)
                    {
                        if (!_session.InYard(kv.Key)) continue;
                        kv.Value.SetColor(_session.Spec(kv.Key).color);
                        StartCoroutine(Tween.Pop(kv.Value.transform, 0.15f, 0.25f));
                    }
                    Enqueue(sev);
                    ConsumeBooster(type);
                    break;
            }
        }

        private void ConsumeBooster(BoosterType type)
        {
            int n = Mathf.Max(0, SaveData.Boosters(type) - 1);
            SaveData.SetBoosters(type, n);
            _hud.SetBooster(type, n);
            _boostersUsed++;
            Analytics.Event("booster_used", ("booster", type), ("level", _levelNumber));
        }

        private static string Label(BoosterType t) => t switch
        {
            BoosterType.ExtraBerth => "Extra Berth",
            BoosterType.CraneLift => "Crane Lift",
            _ => "Shuffle",
        };

        private void OnPause()
        {
            if (_hud.IsModalOpen) return;
            _hud.ShowPause(
                resume: () => _hud.HidePanels(),
                restart: () => StartLevel(_levelNumber),
                skip: () => { SaveData.Level = _levelNumber + 1; StartLevel(SaveData.Level); },
                soundLabel: Sfx.Enabled ? "Sound: On" : "Sound: Off",
                toggleSound: () => { Sfx.Enabled = !Sfx.Enabled; _hud.HidePanels(); OnPause(); },
                privacy: ShowPrivacy,
                skyLabel: SkyTheme.Override < 0 ? $"Sky: Auto ({SkyTheme.Label(SkyTheme.CurrentPhase)})" : $"Sky: {SkyTheme.Label(SkyTheme.CurrentPhase)}",
                cycleSky: () => { SkyTheme.Override = SkyTheme.Override >= 4 ? -1 : SkyTheme.Override + 1; RebuildEnvironment(); _hud.HidePanels(); OnPause(); },
                port: PortSkylines.NameForLevel(_levelNumber));
        }

        /// <summary>Rebuilds only the backdrop and harbour (e.g. after changing the sky preview), keeping the level in progress.</summary>
        private void RebuildEnvironment()
        {
            if (_envRoot) Destroy(_envRoot.gameObject);
            _envRoot = EnvironmentBuilder.Build(_levelRoot, _layout, _pal, _cam, _levelNumber);
        }

        private void ShowPrivacy()
        {
            _hud.ShowPrivacy(
                gdprLabel: Privacy.GdprAnswer == 1 ? "Personalised ads: On" : "Personalised ads: Off",
                toggleGdpr: () => { Privacy.GdprAnswer = Privacy.GdprAnswer == 1 ? 0 : 1; ApplyPrivacy(); ShowPrivacy(); },
                ccpaLabel: Privacy.CcpaOptOut ? "Do not sell/share my info: On" : "Do not sell/share my info: Off",
                toggleCcpa: () => { Privacy.CcpaOptOut = !Privacy.CcpaOptOut; ApplyPrivacy(); ShowPrivacy(); },
                back: () => { _hud.HidePanels(); OnPause(); });
        }

        private static void ApplyPrivacy()
        {
            // Changes apply to future ad requests (LevelPlay privacy settings may be updated after Init).
            if (LevelPlayInitializer.IsInitialized) Privacy.ApplyToLevelPlay();
        }
    }
}
