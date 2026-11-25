using System.Collections;
using System.Collections.Generic;
using Dockside.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Dockside
{
    /// <summary>
    /// Runs a level in either mode: builds the views, turns taps into GameSession moves, and plays the
    /// resulting events back as animations in order. Load sends yard containers to ships; Unload sends a ship's
    /// cargo to trucks. The rules are identical, so only the scenery and the vehicle views depend on the mode.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        public const float CameraPitch = 55f;
        private static int WinCoins => Monetization.WinCoins;
        /// <summary>Test tools (Go to level, Skip level, Sky preview): editor and test-tools builds only (see BuildFlags).</summary>
        private static bool DevTools => BuildFlags.TestTools;
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
        private VehicleView[] _ships;
        private ModeMenuView _menu;
        private bool _menuOpen;
        private GameMode _mode = GameMode.Load;
        public GameMode Mode => _mode;
        private string VehicleNoun => _mode == GameMode.Unload ? "Trucks" : "Ships";
        private readonly List<int> _viewBuffer = new List<int>();   // holding bay as the player currently sees it
        private readonly Queue<GameEvent> _pending = new Queue<GameEvent>();
        private bool _playing;
        // Unload: a container sent to a truck that is still driving in waits over its bay (on the crane's spreader) while
        // playback carries on, and that truck's departure waits for it. Counts per truck, and everything still running.
        private readonly Dictionary<VehicleView, int> _deliveries = new Dictionary<VehicleView, int>();
        private int _backgroundPlays;
        private const float HoverHeight = 1.15f;
        private int _levelNumber, _shipsDepartedShown;
        private bool _craneArmed, _usedSecondChance, _ended;
        private float _levelStartTime, _lastAspect;
        private int _boostersUsed;
        private bool _rewinding;   // Undo: the container taken back is flying home to the yard; taps wait for it

        public void Init(Palette palette, Camera cam)
        {
            _pal = palette;
            _cam = cam;
            _hud = HudView.Create(palette);
            _hud.PauseClicked += OnPause;
            _hud.BoosterClicked += OnBooster;
            _tutorial = TutorialView.Create(_hud, cam, palette);
            // The tutorial cards' mini screen films its own little harbour; the board camera never sees it.
            DemoStage.Create(palette);
            cam.cullingMask &= ~(1 << DemoStage.Layer);
            _hud.SetCoins(SaveData.Coins);
            foreach (BoosterType t in System.Enum.GetValues(typeof(BoosterType))) _hud.SetBooster(t, SaveData.Boosters(t));
            _menu = ModeMenuView.Create(palette);
            _menu.ModeChanged += m => PreviewMode(m);
            _menu.PlayClicked += PlayFromMenu;
            _mode = SaveData.Mode;
            // New players go straight into Load level 1; returning players start on the mode menu.
            // (The GDPR question lives on the HUD, so a player who still has to answer it skips the menu this time.)
            bool returning = SaveData.Level > 1 || SaveData.LevelFor(GameMode.Unload) > 1;
            if (returning && !Privacy.NeedsGdprAnswer) ShowModeMenu();
            else StartLevel(SaveData.LevelFor(_mode));
            // GDPR players are asked once before any ads load (LevelPlayInitializer waits for the answer).
            if (Privacy.NeedsGdprAnswer)
                _hud.ShowAdConsent(yes =>
                {
                    Privacy.GdprAnswer = yes ? 1 : 0;
                    Analytics.Event("privacy_consent", ("gdpr", yes));
                    _hud.HidePanels();
                });
            Notifications.Init();
            GameServices.Init();
            Premium.Init();
        }

        // =====================================================================
        // Reminders (local notifications): scheduled when the app goes to the background, cleared when it comes back
        // =====================================================================
        private const int ReminderOfferLevel = 3;   // first offer after this Load win; second (last) after the first Unload win

        private void OnApplicationPause(bool paused)
        {
            if (paused) Notifications.OnAppBackgrounded(SaveData.Level, SaveData.UnloadUnlocked && !SaveData.UnloadPlayed);
            else Notifications.OnAppResumed();
        }

        private void OnApplicationQuit() => Notifications.OnAppBackgrounded(SaveData.Level, SaveData.UnloadUnlocked && !SaveData.UnloadPlayed);

        private bool ReminderOfferDue =>
            !ScreenInfo.IsTv && Notifications.ShouldOffer &&
            ((_mode == GameMode.Load && _levelNumber == ReminderOfferLevel && Notifications.PromptsShown == 0) ||
             (_mode == GameMode.Unload && _levelNumber == 1));

        /// <summary>"Want a nudge?" card; "Yes" leads to Android's permission dialog. Either way the game carries on with <paramref name="next"/>.</summary>
        private void ShowReminderOffer(System.Action next)
        {
            Notifications.PromptsShown++;
            Analytics.Event("reminder_offer", ("count", Notifications.PromptsShown), ("mode", _mode), ("level", _levelNumber));
            _hud.ShowReminderOffer(
                yes: () =>
                {
                    _hud.HidePanels();
                    StartCoroutine(Notifications.RequestPermission(ok =>
                    {
                        Notifications.Enabled = ok;
                        Analytics.Event("reminder_choice", ("choice", ok ? "on" : "denied"));
                        next();
                    }));
                },
                notNow: () =>
                {
                    Analytics.Event("reminder_choice", ("choice", "not_now"));
                    next();
                });
        }

        /// <summary>Pause menu switch. Turning reminders on asks for the permission if needed, or points to Settings if it was refused.</summary>
        private void ToggleReminders()
        {
            _hud.HidePanels();
            if (Notifications.Active)
            {
                Notifications.Enabled = false;
                Analytics.Event("reminder_toggle", ("on", false));
                OnPause();
                return;
            }
            StartCoroutine(Notifications.RequestPermission(ok =>
            {
                Notifications.Enabled = true;   // the player wants them; they show once the device allows it
                Analytics.Event("reminder_toggle", ("on", true), ("permission", ok));
                if (ok) OnPause();
                else _hud.ShowRemindersBlocked(
                    openSettings: () => { _hud.HidePanels(); Notifications.OpenSettings(); },
                    back: () => { _hud.HidePanels(); OnPause(); });
            }));
        }

        // =====================================================================
        // Level setup
        // =====================================================================
        /// <summary>Switches mode (if needed) and starts a level of it.</summary>
        public void StartLevel(GameMode mode, int levelNumber)
        {
            _mode = mode;
            SaveData.Mode = mode;
            StartLevel(levelNumber);
        }

        public void StartLevel(int levelNumber) => StartLevel(levelNumber, preview: false);

        // =====================================================================
        // Mode menu: the selected mode's next level is built behind the menu as a live backdrop; Play starts it.
        // =====================================================================
        public void ShowModeMenu()
        {
            _menuOpen = true;
            _hud.HidePanels();
            _hud.SetVisible(false);
            _menu.Show(_mode);
            PreviewMode(_mode);
            Analytics.Event("mode_menu_open", ("mode", _mode));
        }

        private void PreviewMode(GameMode mode)
        {
            _mode = mode;
            bool locked = mode == GameMode.Unload && !SaveData.UnloadUnlocked;
            StartLevel(locked ? 1 : SaveData.LevelFor(mode), preview: true);
        }

        private void PlayFromMenu(GameMode mode)
        {
            _menu.Hide();
            _menuOpen = false;
            _hud.SetVisible(true);
            _mode = mode;
            SaveData.Mode = mode;
            Analytics.Event("mode_selected", ("mode", mode), ("level", SaveData.LevelFor(mode)));
            if (_session == null || _levelNumber != SaveData.LevelFor(mode) || _session.MovesMade > 0 || Monetization.NeedsPremium(SaveData.LevelFor(mode)))
                StartLevel(SaveData.LevelFor(mode));
            else BeginPlay();
        }

        private void StartLevel(int levelNumber, bool preview)
        {
            // TV: past the free levels, the level is shown behind the Premium card instead of starting.
            if (!preview && Monetization.NeedsPremium(levelNumber))
            {
                StartLevel(levelNumber, preview: true);
                ShowPremium(gated: true);
                return;
            }
            StopAllCoroutines();
            _pending.Clear();
            _playing = false;
            _deliveries.Clear();
            _hovering.Clear();
            _backgroundPlays = 0;
            _hud.HidePanels();
            if (_levelRoot) Destroy(_levelRoot.gameObject);
            _containers.Clear();
            _viewBuffer.Clear();
            _craneArmed = false;
            _hud.SetCraneHint(false);
            _usedSecondChance = false;
            _ended = false;
            _boostersUsed = 0;
            _rewinding = false;

            _levelNumber = levelNumber;
            _focus = -1;
            BoardLayout.QuayShift = _mode == GameMode.Unload ? BoardLayout.UnloadQuayShift : 0f;
            var level = LevelRepository.Get(levelNumber, _mode);
            _session = new GameSession(level) { KeepHistory = true };   // every move can be taken back with Undo
            _layout = new BoardLayout(level.width, level.height, level.berths);
            FitBoardCamera();
            _lastAspect = _cam.aspect;

            _levelRoot = new GameObject($"Level_{levelNumber}").transform;
            _envRoot = EnvironmentBuilder.Build(_levelRoot, _layout, _pal, _cam, levelNumber, _mode);

            _bufferSlots = new GameObject("HoldingBay").AddComponent<BufferSlotsView>();
            _bufferSlots.transform.SetParent(_levelRoot, false);
            _bufferSlots.Init(_pal);
            _bufferSlots.SetCapacity(_session.BufferCapacity);
            _bufferSlots.SetUsed(SlotOfViewItem(_viewBuffer.Count));

            _craneRig = CraneRig.Create(_levelRoot, _pal);
            _craneLifted = -1;

            _yardRoot = new GameObject("Yard").transform;
            _yardRoot.SetParent(_levelRoot, false);
            for (int i = 0; i < _session.ContainerCount; i++) CreateContainerView(i);

            _shipsRoot = new GameObject("Ships").transform;
            _shipsRoot.SetParent(_levelRoot, false);
            _ships = new VehicleView[_session.BerthCount];
            for (int b = 0; b < _session.BerthCount; b++)
            {
                int ship = _session.ShipAtBerth(b);
                // Unload: trucks come in along one lane, so the one going furthest (the rightmost bay) leads.
                float delay = _mode == GameMode.Unload ? 0.3f * (_session.BerthCount - 1 - b) : 0.15f * b;
                if (ship >= 0) StartCoroutine(DockShip(b, ship, delay));
            }

            _shipsDepartedShown = 0;
            _hud.SetLevel(levelNumber, LevelGenerator.IsHard(levelNumber));
            _hud.SetShips(0, _session.ShipsTotal, VehicleNoun);
            RefreshYard();
            if (!preview) BeginPlay();
        }

        /// <summary>The player starts on the level: port banner, analytics, first-time tips, timer.</summary>
        private void BeginPlay()
        {
            int levelNumber = _levelNumber;
            bool hard = LevelGenerator.IsHard(levelNumber);
            // A new port every few levels (the city skyline behind both modes): announce it.
            if (_mode == GameMode.Unload && !SaveData.UnloadPlayed) { SaveData.UnloadPlayed = true; SaveData.UnloadRemindAt = 0; }
            int port = PortSkylines.IndexForLevel(levelNumber);
            bool portBanner = levelNumber == 1 || port != PortSkylines.IndexForLevel(levelNumber - 1);
            // One-time nudge for a player who chose "Keep loading" (not on a level that already announces a new port).
            if (_mode == GameMode.Load && !portBanner && !hard && SaveData.UnloadRemindAt > 0 && levelNumber >= SaveData.UnloadRemindAt && !SaveData.UnloadPlayed)
            {
                SaveData.UnloadRemindAt = 0;
                _hud.Banner("Unload mode is waiting", "Pause > Game modes");
                Analytics.Event("unload_reminder", ("level", levelNumber));
            }
            if (portBanner)
            {
                int first = (levelNumber - 1) / PortSkylines.LevelsPerPort * PortSkylines.LevelsPerPort + 1;
                _hud.Banner(_mode == GameMode.Unload ? $"Unloading in {PortSkylines.Names[port]}" : $"Next stop: {PortSkylines.Names[port]}",
                    $"Levels {first}-{first + PortSkylines.LevelsPerPort - 1}");
                Analytics.Event("port_arrival", ("port", PortSkylines.Names[port]), ("level", levelNumber), ("mode", _mode));
            }
            // Every 5th level is a hard level (the last of each port): say so up front, so a loss feels fair and a win counts.
            if (hard) _hud.Banner("Hard level", "Plan ahead: park containers in the holding bay");
            _levelStartTime = Time.time;
            var level = _session.Level;
            Analytics.Event("level_start", ("level", levelNumber), ("seed", level.seed), ("containers", level.containers.Count),
                ("ships", level.ships.Count), ("mode", _mode), ("hard", hard));
            _tutorial.OnLevelStarted(levelNumber, _mode, _session,
                i => _containers.TryGetValue(i, out var v) && v && _session.InYard(i) ? v.transform.position + Vector3.up * 0.45f : (Vector3?)null,
                b => _ships != null && _ships[b] && Vector3.Distance(_ships[b].transform.position, BerthPosition(b)) < 0.3f);
        }

        /// <summary>A container's view, in its yard cell (the controller moves it from there).</summary>
        private ContainerView CreateContainerView(int index)
        {
            var spec = _session.Spec(index);
            var view = ContainerView.Create(_yardRoot, spec, _pal);
            view.transform.localScale = _layout.YardContainerScale(spec.length);
            view.transform.position = _layout.CellCenter(spec.x, spec.y, spec.length);
            _containers[index] = view;
            return view;
        }

        private VehicleView CreateVehicle(int shipIndex)
        {
            var spec = _session.Level.ships[shipIndex];
            return _mode == GameMode.Unload
                ? TruckView.Create(_shipsRoot, shipIndex, spec.color, spec.capacity, _pal, SkyTheme.Current(_pal))
                : ShipView.Create(_shipsRoot, shipIndex, spec.color, spec.capacity, _pal);
        }

        private IEnumerator DockShip(int berth, int shipIndex, float delay)
        {
            var ship = CreateVehicle(shipIndex);
            _ships[berth] = ship;
            ship.transform.position = BerthPosition(berth) + new Vector3(-12f, 0, 0);
            if (delay > 0) yield return new WaitForSeconds(delay);
            yield return ship.Arrive(BerthPosition(berth), _mode == GameMode.Unload ? 0.75f : 0.5f);
        }

        private Vector3 BerthPosition(int berth) => _mode == GameMode.Unload
            ? new Vector3(_layout.BerthX(berth), 0f, BoardLayout.TruckZ)
            : new Vector3(_layout.BerthX(berth), -0.05f, BoardLayout.ShipZ);

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
            // The screen turned, or a Chromebook / PC window was resized: frame the board again straight away, and rebuild
            // the scenery (sized to the old screen width) once the size has settled.
            if (_cam && !Mathf.Approximately(_cam.aspect, _lastAspect) && _layout != null)
            {
                _lastAspect = _cam.aspect;
                FitBoardCamera();
                _rebuildSceneryAt = Time.unscaledTime + 0.3f;
            }
            if (_rebuildSceneryAt > 0 && Time.unscaledTime >= _rebuildSceneryAt)
            {
                _rebuildSceneryAt = 0;
                FitBoardCamera();
                RebuildEnvironment();
            }
            UpdateNav();
            if (_session == null || _session.IsOver || _hud.IsModalOpen || _menuOpen || _rewinding) return;
            if (!TryGetPress(out Vector2 screenPos)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            var ray = _cam.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 200f)) return;
            var view = hit.collider.GetComponentInParent<ContainerView>();
            if (view != null) OnContainerTapped(view);
        }

        private float _rebuildSceneryAt;

        // =====================================================================
        // Remote, gamepad and keyboard: a gold cursor over the containers, the boosters below (or beside) them, and Back
        // =====================================================================
        private int _focus = -1;              // container under the cursor, -1 for none
        private Vector2 _focusScreen;         // where it was, so the cursor lands nearby when that container leaves
        private int _navBlockedUntil;         // a press that just closed a card or the menu doesn't also hit the board

        private void UpdateNav()
        {
            bool busy = _hud.IsModalOpen || _menuOpen;
            if (busy) _navBlockedUntil = Time.frameCount + 1;
            if (NavInput.Back) { OnBackPressed(); return; }
            if (_session == null || _session.IsOver || busy || !NavInput.NavMode)
            {
                _hud.SetBoardFocus(null);
                return;
            }
            KeyboardShortcuts();
            var move = NavInput.Move;
            if (_hud.SelectedBooster >= 0)
            {
                // The UI input module steps along the boosters and presses them; stepping towards the board leaves them.
                _hud.SetBoardFocus(null);
                if (ScreenInfo.Landscape ? move.x < 0 : move.y > 0) { _hud.SelectBooster(-1); EnsureFocus(); ShowFocus(); }
                return;
            }
            EnsureFocus();
            if (move != Vector2Int.zero) MoveFocus(move);
            if (NavInput.Submit && Time.frameCount > _navBlockedUntil && !_rewinding && _focus >= 0 && _containers.TryGetValue(_focus, out var view))
                OnContainerTapped(view);
            ShowFocus();
        }

        /// <summary>Back: closes the open card (where it has a way out), pauses a level, or leaves the game from the mode menu.</summary>
        private void OnBackPressed()
        {
            if (_hud.IsModalOpen) _hud.Cancel();
            else if (_menuOpen) LeaveToHome();
            else if (_session != null && !_session.IsOver) OnPause();
        }

        /// <summary>Back from the mode menu: send the game to the background, as Android expects of an app's first screen.</summary>
        private static void LeaveToHome()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call<bool>("moveTaskToBack", true);
            }
            catch (System.Exception e) { Debug.LogWarning($"[Dockside] moveTaskToBack failed: {e.Message}"); }
#elif !UNITY_EDITOR && !UNITY_IOS
            Application.Quit();
#endif
        }

        /// <summary>PC and Chromebook keyboards: 1, 2, 3 for the boosters, Z (or Ctrl+Z) to undo.</summary>
        private void KeyboardShortcuts()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) OnBooster(BoosterType.ExtraBerth);
            else if (kb.digit2Key.wasPressedThisFrame) OnBooster(BoosterType.CraneLift);
            else if (kb.digit3Key.wasPressedThisFrame || kb.zKey.wasPressedThisFrame) OnBooster(BoosterType.Undo);
#endif
        }

        private Vector2 ContainerScreen(int index) =>
            _containers.TryGetValue(index, out var v) && v ? (Vector2)_cam.WorldToScreenPoint(v.transform.position) : _focusScreen;

        /// <summary>Keeps the cursor on a container in the yard: the nearest one to where it was, or else one that can move now.</summary>
        private void EnsureFocus()
        {
            if (_focus >= 0 && _session.InYard(_focus) && _containers.ContainsKey(_focus)) return;
            int best = -1;
            float bestScore = float.MaxValue;
            bool hadFocus = _focus >= 0;
            foreach (var kv in _containers)
            {
                if (!_session.InYard(kv.Key) || !kv.Value) continue;
                float score;
                if (hadFocus) score = (ContainerScreen(kv.Key) - _focusScreen).sqrMagnitude;
                else
                {
                    // First time: a container that can go now, near the quay and the middle of the yard.
                    var spec = _session.Spec(kv.Key);
                    bool free = _session.IsReachable(kv.Key) && !_session.IsLocked(kv.Key);
                    score = (free ? 0 : 1000) + spec.y * 10 + Mathf.Abs(spec.x + (spec.length - 1) * 0.5f - (_layout.Width - 1) * 0.5f);
                }
                if (score < bestScore) { bestScore = score; best = kv.Key; }
            }
            _focus = best;
            if (best >= 0) _focusScreen = ContainerScreen(best);
        }

        /// <summary>
        /// Steps the cursor to the nearest container in that direction on screen. Off the bottom of the yard (portrait) or
        /// its right side (landscape) it moves onto the boosters.
        /// </summary>
        private void MoveFocus(Vector2Int dir)
        {
            if (_focus < 0) return;
            Vector2 from = ContainerScreen(_focus), d = new Vector2(dir.x, dir.y);
            int best = -1;
            float bestScore = float.MaxValue;
            foreach (var kv in _containers)
            {
                if (kv.Key == _focus || !_session.InYard(kv.Key) || !kv.Value) continue;
                Vector2 v = ContainerScreen(kv.Key) - from;
                float along = Vector2.Dot(v, d);
                if (along < 8f) continue;
                float across = Mathf.Abs(v.x * d.y - v.y * d.x);
                if (across > along * 2.5f) continue;
                float score = along + across * 2f;
                if (score < bestScore) { bestScore = score; best = kv.Key; }
            }
            if (best >= 0)
            {
                _focus = best;
                _focusScreen = ContainerScreen(best);
                Sfx.Play("click", 0.25f, 0f);
            }
            else if (ScreenInfo.Landscape ? dir.x > 0 : dir.y < 0)
            {
                _hud.SetBoardFocus(null);
                _hud.SelectBooster(_hud.BoosterCount / 2);
            }
        }

        /// <summary>Draws the gold cursor around the focused container's outline on screen.</summary>
        private void ShowFocus()
        {
            if (_focus < 0 || !_containers.TryGetValue(_focus, out var view) || !view) { _hud.SetBoardFocus(null); return; }
            var renderers = view.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { _hud.SetBoardFocus(null); return; }
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                Vector2 p = _cam.WorldToScreenPoint(corner);
                min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            _focusScreen = (min + max) * 0.5f;
            _hud.SetBoardFocus(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
        }

        private void FitBoardCamera() =>
            _layout.FitCamera(_cam, CameraPitch, HudView.TopMargin, HudView.BottomMargin, HudView.SideMargin);

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
                        GameServices.AddStat("cranelift");
                        _craneArmed = false;
                        _hud.SetCraneHint(false);
                        _hud.SetBooster(BoosterType.CraneLift, SaveData.Boosters(BoosterType.CraneLift));
                    }
                    Enqueue(events);
                    break;
                case TapResult.Blocked:
                    StartCoroutine(Tween.Shake(view.transform));
                    Sfx.Play("blocked", 0.7f);
                    _hud.Toast("Blocked: clear a path first");
                    break;
                case TapResult.Locked:
                    StartCoroutine(Tween.Shake(view.transform));
                    Sfx.Play("blocked", 0.7f);
                    _hud.Toast(_craneArmed ? "The crane can't lift a locked container" : "Locked: send off its gold key first");
                    break;
                case TapResult.NoRoom:
                    StartCoroutine(Tween.Shake(view.transform));
                    Sfx.Play("blocked", 0.7f);
                    _hud.Toast("No room in the holding bay");
                    break;
            }
        }

        // =====================================================================
        // Event playback
        // =====================================================================
        private void Enqueue(List<GameEvent> events)
        {
            foreach (var e in events)
            {
                _pending.Enqueue(e);
                CountForAchievements(e);
            }
            if (!_playing) StartCoroutine(PlayEvents());
        }

        /// <summary>Feeds the incremental Play Games achievements (counted on the device, synced when signed in).</summary>
        private void CountForAchievements(GameEvent e)
        {
            switch (e.type)
            {
                case GameEventType.ContainerToShip:
                case GameEventType.BufferToShip:
                    if (_containers.TryGetValue(e.containerId, out var v) && v.Length > 1) GameServices.AddStat("long");
                    break;
                case GameEventType.ShipDeparted: GameServices.AddStat(_mode == GameMode.Unload ? "trucks" : "ships"); break;
                case GameEventType.ContainerRevealed: GameServices.AddStat("revealed"); break;
                case GameEventType.ContainerUnlocked: GameServices.AddStat("unlocked"); break;
            }
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
                    int len = view.Length;
                    if (_mode == GameMode.Unload && ship && !Docked(ship, e.berth) && e.containerId != _craneLifted)
                    {
                        // The truck is still on its way: lift the container over its bay and play on; it drops in when the
                        // truck has parked (DeliverWhenDocked), so later taps never queue behind lane traffic.
                        view.SetRevealed(true);
                        Sfx.Play("lift", 0.6f);
                        yield return Tween.Arc(view.transform, HoverPoint(ship, e.berth, e.slot, len), 0.6f, 0.26f,
                            BoardLayout.ContainerScale(len) * ship.StowScale, RestRotation);
                        _deliveries[ship] = (_deliveries.TryGetValue(ship, out var n) ? n : 0) + 1;
                        _hovering[ship] = e.berth;
                        _backgroundPlays++;
                        StartCoroutine(DeliverWhenDocked(view, ship, e.berth, e.slot, len));
                        if (e.type == GameEventType.BufferToShip) yield return RelayoutBuffer(0.12f);
                        break;
                    }
                    // Ship may still be sailing in: wait until it is near its berth.
                    while (ship && !Docked(ship, e.berth)) yield return null;
                    view.SetRevealed(true);
                    if (e.containerId == _craneLifted)
                    {
                        _craneLifted = -1;
                        yield return _craneRig.Carry(view.transform, () => ship.SlotWorldPosition(e.slot, len),
                            BoardLayout.ContainerScale(len) * ship.StowScale, ship.transform.rotation);
                    }
                    else
                    {
                        var target = ship.SlotWorldPosition(e.slot, len);
                        Sfx.Play("lift", 0.6f);
                        yield return Tween.Arc(view.transform, target, 1.6f, 0.3f, BoardLayout.ContainerScale(len) * ship.StowScale, ship.transform.rotation);
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
                    if (ship && _deliveries.TryGetValue(ship, out var waiting) && waiting > 0)
                    {
                        // Its last containers are still waiting for it to park: it leaves once they are on board.
                        _backgroundPlays++;
                        StartCoroutine(DepartWhenLoaded(ship));
                        break;
                    }
                    yield return SendOff(ship);
                    break;
                }
                case GameEventType.ShipArrived:
                    // A truck may have to queue in the lane before it can turn in; containers for it already wait until
                    // it reaches its bay, so play on rather than hold every other animation up.
                    if (_mode == GameMode.Unload) { StartCoroutine(DockShip(e.berth, e.shipIndex, 0f)); yield return new WaitForSeconds(0.1f); }
                    else yield return DockShip(e.berth, e.shipIndex, 0f);
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
                    while (_backgroundPlays > 0) yield return null;   // the last trucks are loaded and away
                    yield return new WaitForSeconds(0.6f);
                    OnWon();
                    break;
                case GameEventType.Lost:
                    while (_backgroundPlays > 0) yield return null;
                    yield return new WaitForSeconds(0.4f);
                    OnLost();
                    break;
            }
        }

        private bool Docked(VehicleView ship, int berth) => Vector3.Distance(ship.transform.position, BerthPosition(berth)) <= 0.3f;

        /// <summary>A truck's resting pose (it leans towards the camera); its slots are placed relative to it.</summary>
        private static Quaternion RestRotation => Quaternion.Euler(TruckView.Tilt, 0, 0);

        /// <summary>
        /// Where a container waits for its truck: over its slot on the parked truck, raised (higher still when an earlier
        /// truck for the same bay has containers waiting there too).
        /// </summary>
        private Vector3 HoverPoint(VehicleView ship, int berth, int slot, int len)
        {
            int earlier = 0;
            foreach (var kv in _hovering)
                if (kv.Key && kv.Key != ship && kv.Value == berth && _deliveries.TryGetValue(kv.Key, out var n) && n > 0) earlier++;
            return BerthPosition(berth) + RestRotation * ship.SlotLocalPosition(slot, len) + Vector3.up * (HoverHeight + 0.8f * earlier);
        }

        private readonly Dictionary<VehicleView, int> _hovering = new Dictionary<VehicleView, int>();   // truck -> its bay, while containers wait for it

        private IEnumerator DeliverWhenDocked(ContainerView view, VehicleView ship, int berth, int slot, int len)
        {
            float phase = Random.value * 6f;
            Vector3 hover = view ? view.transform.position : Vector3.zero;
            while (ship && view && !Docked(ship, berth))
            {
                view.transform.position = hover + Vector3.up * (Mathf.Sin((Time.time + phase) * 3f) * 0.04f);
                yield return null;
            }
            if (ship && view)
            {
                yield return Tween.Arc(view.transform, ship.SlotWorldPosition(slot, len), 0.12f, 0.22f,
                    BoardLayout.ContainerScale(len) * ship.StowScale, ship.transform.rotation);
                Sfx.Play("land");
                view.transform.SetParent(ship.transform, true);
                view.transform.localPosition = ship.SlotLocalPosition(slot, len);
                view.transform.localRotation = Quaternion.identity;
                ship.SetLoaded(ship.Loaded + len);
                StartCoroutine(Tween.Pop(view.transform, 0.25f, 0.18f));
            }
            if (ship && _deliveries.TryGetValue(ship, out var n)) _deliveries[ship] = n - 1;
            _backgroundPlays--;
        }

        private IEnumerator DepartWhenLoaded(VehicleView ship)
        {
            while (ship && _deliveries.TryGetValue(ship, out var n) && n > 0) yield return null;
            yield return new WaitForSeconds(0.15f);   // let the last container settle on the deck
            yield return SendOff(ship);
            _backgroundPlays--;
        }

        /// <summary>A full vehicle leaves: the counter goes up, a little pop and the horn, then it drives or sails away.</summary>
        private IEnumerator SendOff(VehicleView ship)
        {
            _shipsDepartedShown++;
            _hud.SetShips(_shipsDepartedShown, _session.ShipsTotal, VehicleNoun);
            if (!ship) yield break;
            _deliveries.Remove(ship);
            _hovering.Remove(ship);
            StartCoroutine(Tween.Pop(ship.transform, 0.12f, 0.25f));
            yield return new WaitForSeconds(0.12f);
            Sfx.Play(ship.DepartSound, 0.8f);
            StartCoroutine(SailAway(ship));
            yield return new WaitForSeconds(0.25f);
        }

        private IEnumerator SailAway(VehicleView ship)
        {
            yield return ship.Depart(0.9f);
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
            bool hard = LevelGenerator.IsHard(_levelNumber);
            int coins = hard ? WinCoins * 3 / 2 : WinCoins;   // hard levels pay half as much again
            SaveData.Coins += coins;
            _hud.SetCoins(SaveData.Coins);
            Analytics.Coins(true, coins, "level_reward", $"{(_mode == GameMode.Unload ? "unload_" : "")}level_{_levelNumber:000}");
            bool wasLocked = !SaveData.UnloadUnlocked;
            SaveData.SetLevelFor(_mode, _levelNumber + 1);
            if (wasLocked && SaveData.UnloadUnlocked) Analytics.Event("mode_unlocked", ("mode", GameMode.Unload), ("level", _levelNumber));
            Analytics.Event("level_complete", ("level", _levelNumber), ("moves", _session.MovesMade),
                ("seconds", Mathf.RoundToInt(Time.time - _levelStartTime)), ("boosters", _boostersUsed), ("second_chance", _usedSecondChance),
                ("mode", _mode), ("hard", hard));
            Sfx.Play("win");
            GameServices.OnLevelWon(_mode, _levelNumber, _boostersUsed, _usedSecondChance, SkyTheme.CurrentPhase == DayPhase.Night);
            // Interstitials start only after level AdManager.InterstitialFromLevel of this mode: the first levels of each
            // mode (where players learn it, and decide whether to stay) are always ad-free.
            bool unlockedNow = wasLocked && SaveData.UnloadUnlocked;
            bool offerReminders = ReminderOfferDue;
            // A store rating card after the 6th cleared level, then now and then (ReviewPrompt); it takes this win's
            // ad slot, and waits for any unlock card or reminder question to have had their turn.
            bool askReview = !unlockedNow && !offerReminders && ReviewPrompt.Due;
            System.Action next = () =>
            {
                // The win that unlocks Unload ends with the unlock card (no ad on top of the celebration).
                if (unlockedNow) { ShowUnloadUnlocked(); return; }
                // No ad straight after the reminder question either.
                if (offerReminders) { StartLevel(SaveData.LevelFor(_mode)); return; }
                if (askReview) { StartCoroutine(ReviewPrompt.Ask("win", () => StartLevel(SaveData.LevelFor(_mode)))); return; }
                // A capped, opportunistic interstitial between levels; the game never waits for an ad.
                var ads = AdManager.Instance;
                if (!Monetization.Interstitials || ads == null || !ads.TryShowInterstitialAfterLevel(_levelNumber, () => StartLevel(SaveData.LevelFor(_mode))))
                    StartLevel(SaveData.LevelFor(_mode));
            };
            _hud.ShowWin(_levelNumber, coins, hard, () =>
            {
                if (offerReminders) ShowReminderOffer(next);
                else next();
            });
        }

        /// <summary>"New mode unlocked" card after the win that opens Unload: try it now, or keep loading (reminded later).</summary>
        private void ShowUnloadUnlocked()
        {
            Sfx.Play("unlock");
            _hud.ShowUnlock(
                tryIt: () =>
                {
                    Analytics.Event("unlock_choice", ("choice", "try_unload"));
                    StartLevel(GameMode.Unload, SaveData.LevelFor(GameMode.Unload));
                },
                keepLoading: () =>
                {
                    Analytics.Event("unlock_choice", ("choice", "keep_loading"));
                    SaveData.UnloadRemindAt = SaveData.Level + 2;
                    StartLevel(SaveData.LevelFor(GameMode.Load));
                });
        }

        private void OnLost()
        {
            if (!_session.IsLost) return; // a second chance may already have cleared it
            string reason = _session.LoseReason == LoseReason.NoMoves
                ? "No container can move right now."
                : "The holding bay is full.";
            Analytics.Event("level_fail", ("level", _levelNumber), ("reason", _session.LoseReason), ("moves", _session.MovesMade),
                ("seconds", Mathf.RoundToInt(Time.time - _levelStartTime)), ("second_chance", _usedSecondChance), ("mode", _mode));
            Sfx.Play("lose");
            int undos = SaveData.Boosters(BoosterType.Undo);
            _hud.ShowLose(reason, !_usedSecondChance, Monetization.RewardedVideos ? SecondChance : (System.Action)null,
                coinsLabel: $"+2 bay slots for {ContinuePrice}", canAffordCoins: SaveData.Coins >= ContinuePrice,
                continueWithCoins: ContinueWithCoins, retry: () => StartLevel(_levelNumber),
                undoLabel: $"Undo move ({undos} left)",
                undo: undos > 0 && _session.CanUndo ? UndoFromLose : (System.Action)null);
        }

        /// <summary>The lose card's Undo: the move that filled the bay is taken back and the level carries on.</summary>
        private void UndoFromLose()
        {
            _hud.HidePanels();
            UndoMove(fromLose: true);
            ConsumeBooster(BoosterType.Undo);
        }

        private void SecondChance()
        {
            // Rewarded video: the reward (2 extra bay slots) is granted when the player has earned it.
            var ads = AdManager.Instance;
            if (ads == null || !ads.ShowRewarded("second_chance", GrantSecondChance))
                _hud.Toast("No video right now. Try again soon.");
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
                _hud.Toast($"Need {price - SaveData.Coins} more coins (+{WinCoins} a level)");
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
            if (_session == null || _session.IsWon || _ended || _hud.IsModalOpen || _menuOpen || _rewinding) return;
            // Once a level is lost, only Undo still helps (until the lose card offers it, a second chance or a retry).
            if (_session.IsLost && type != BoosterType.Undo) return;
            if (_tutorial.TryShowBoosterIntro(type, () => OnBooster(type))) return;   // first tap: what it does, then it acts
            if (type == BoosterType.Undo && !_session.CanUndo) { _hud.Toast("Nothing to undo yet"); return; }
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
                    watchVideo: !Monetization.RewardedVideos ? null : () =>
                    {
                        _hud.HidePanels();
                        var ads = AdManager.Instance;
                        if (ads == null || !ads.ShowRewarded("booster_" + type, Give))
                            _hud.Toast("No video right now. Try again soon.");
                    },
                    buy: () => { _hud.HidePanels(); if (TrySpend(price, "booster", type.ToString())) Give(); },
                    cancel: () => _hud.HidePanels());
                return;
            }
            switch (type)
            {
                case BoosterType.ExtraBerth:
                    if (_session.BufferCapacity >= MaxBufferSlots) { _hud.Toast("The bay can't grow any bigger"); return; }
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
                    if (_craneArmed && _hud.SelectedBooster >= 0) _hud.SelectBooster(-1);   // remote: pick the container next
                    break;
                case BoosterType.Undo:
                    UndoMove(fromLose: false);
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
            Analytics.Event("booster_used", ("booster", type), ("level", _levelNumber), ("mode", _mode));
        }

        private static string Label(BoosterType t) => t switch
        {
            BoosterType.ExtraBerth => "Extra Berth",
            BoosterType.CraneLift => "Crane Lift",
            _ => "Undo",
        };

        // =====================================================================
        // Undo
        // =====================================================================
        /// <summary>
        /// Takes back the last tap. The rules restore the state before it; the scene is rebuilt from that state (any
        /// animation still running is cut short, so a ship that had just left is back at its berth), and the container
        /// that was tapped flies home to its yard cell from wherever it is now.
        /// </summary>
        private void UndoMove(bool fromLose)
        {
            bool wasLost = _session.IsLost;
            int id = _session.Undo();
            if (id < 0) return;
            bool hadView = _containers.TryGetValue(id, out var old) && old;
            Vector3 fromPos = hadView ? old.transform.position : Vector3.zero, fromScale = hadView ? old.transform.lossyScale : Vector3.one;
            Quaternion fromRot = hadView ? old.transform.rotation : Quaternion.identity;

            RebuildFromState();

            var view = _containers[id];
            Vector3 cell = view.transform.position, scale = view.transform.localScale;
            Quaternion rest = view.transform.rotation;
            if (!hadView) { fromPos = cell + Vector3.up * 3f; fromRot = rest; fromScale = scale; }   // it left on a ship: drop it in
            view.transform.SetPositionAndRotation(fromPos, fromRot);
            view.transform.localScale = fromScale;
            StartCoroutine(FlyHome(view, cell, scale, rest));
            Analytics.Event("undo", ("level", _levelNumber), ("mode", _mode), ("from_lose", fromLose), ("was_lost", wasLost),
                ("moves", _session.MovesMade));
        }

        private IEnumerator FlyHome(ContainerView view, Vector3 cell, Vector3 scale, Quaternion rest)
        {
            _rewinding = true;
            Sfx.Play("lift", 0.6f);
            yield return Tween.Arc(view.transform, cell, 1.2f, 0.3f, scale, rest);
            if (view)
            {
                view.transform.SetPositionAndRotation(cell, rest);
                view.transform.localScale = scale;
                Sfx.Play("land");
                StartCoroutine(Tween.Pop(view.transform, 0.15f, 0.18f));
            }
            _rewinding = false;
        }

        /// <summary>
        /// Rebuilds containers, vehicles and the holding bay from the session's state, with nothing in motion: yard
        /// containers in their cells, the vehicles at their berths with their loads, the bay in order.
        /// </summary>
        private void RebuildFromState()
        {
            StopAllCoroutines();
            _pending.Clear();
            _playing = false;
            _deliveries.Clear();
            _hovering.Clear();
            _backgroundPlays = 0;
            _rewinding = false;
            if (_craneRig) Destroy(_craneRig.gameObject);   // it may have been cut off mid-lift
            _craneRig = CraneRig.Create(_levelRoot, _pal);
            _craneLifted = -1;

            foreach (var v in _containers.Values) if (v) Destroy(v.gameObject);
            _containers.Clear();
            _viewBuffer.Clear();
            // Every vehicle, including one still driving or sailing away (its containers go with it).
            for (int i = _shipsRoot.childCount - 1; i >= 0; i--) Destroy(_shipsRoot.GetChild(i).gameObject);

            _ships = new VehicleView[_session.BerthCount];
            for (int b = 0; b < _session.BerthCount; b++)
            {
                int shipIndex = _session.ShipAtBerth(b);
                if (shipIndex < 0) continue;
                var ship = CreateVehicle(shipIndex);
                ship.Park(BerthPosition(b));
                _ships[b] = ship;
                int slot = 0;
                foreach (int ci in _session.LoadAtBerth(b))
                {
                    var view = CreateContainerView(ci);
                    int len = view.Length;
                    view.Detach();
                    view.SetLocked(false);
                    view.SetRevealed(true);
                    view.transform.SetPositionAndRotation(ship.SlotWorldPosition(slot, len), ship.transform.rotation);
                    view.transform.localScale = BoardLayout.ContainerScale(len) * ship.StowScale;
                    view.transform.SetParent(ship.transform, true);
                    view.transform.localPosition = ship.SlotLocalPosition(slot, len);
                    view.transform.localRotation = Quaternion.identity;
                    slot += len;
                }
                ship.SetLoaded(slot);
            }

            int cap = _session.BufferCapacity;
            foreach (int ci in _session.Buffer)
            {
                var view = CreateContainerView(ci);
                view.Detach();
                view.SetLocked(false);
                view.SetRevealed(true);
                _viewBuffer.Add(ci);
                view.transform.position = BoardLayout.BufferPosition(SlotOfViewItem(_viewBuffer.Count - 1), view.Length, cap);
                view.transform.localScale = BoardLayout.BufferScale(view.Length, cap);
            }
            _bufferSlots.SetCapacity(cap);
            _bufferSlots.SetUsed(SlotOfViewItem(_viewBuffer.Count));

            for (int i = 0; i < _session.ContainerCount; i++)
            {
                if (!_session.InYard(i)) continue;
                var view = CreateContainerView(i);
                view.SetRevealed(_session.IsRevealed(i));
            }

            _shipsDepartedShown = _session.ShipsDeparted;
            _hud.SetShips(_shipsDepartedShown, _session.ShipsTotal, VehicleNoun);
            RefreshYard();
        }

        private void OnPause()
        {
            if (_hud.IsModalOpen || _menuOpen) return;
            _hud.ShowPause(PortSkylines.NameForLevel(_levelNumber),
                resume: () => _hud.HidePanels(),
                modes: () => { _hud.HidePanels(); ShowModeMenu(); },
                achievements: () =>
                {
                    _hud.HidePanels();
                    GameServices.ShowAchievements(failed => _hud.Banner($"Couldn't sign in to {GameServices.ServiceName}", GameServices.SignInHint));
                },
                privacy: Monetization.AdsAllowed ? ShowPrivacy : (System.Action)null,
                restart: () => StartLevel(_levelNumber),
                soundOn: Sfx.Enabled,
                toggleSound: () => { Sfx.Enabled = !Sfx.Enabled; _hud.HidePanels(); OnPause(); },
                remindersOn: Notifications.Active,
                toggleReminders: ScreenInfo.IsTv ? null : (System.Action)ToggleReminders,
                testTools: DevTools ? ShowTestTools : (System.Action)null,
                premiumLabel: ScreenInfo.IsTv ? "Unlock the full game" : "Remove ads",
                premium: Premium.Owned ? null : (System.Action)(() => ShowPremium(gated: false)));
        }

        /// <summary>
        /// The Premium card. Gated (TV, past the free levels): the only ways on are buying, or back to the mode menu.
        /// Otherwise it was opened from the pause menu and goes back there.
        /// </summary>
        private void ShowPremium(bool gated)
        {
            bool tv = ScreenInfo.IsTv;
            string price = Premium.Price;
            string title = tv ? "Unlock the whole harbour" : "Remove ads";
            string body = tv
                ? $"The first {Monetization.TvFreeLevels} levels of each mode are free. One payment unlocks all 20 ports and 200 levels, " +
                  "and removes the ads between levels on your phone or tablet too."
                : "One payment, and no more ads between levels on any device with your account. Videos for free boosters " +
                  "stay, only when you choose them. It also unlocks the full game on Android TV.";
            string buyLabel = price != null ? (tv ? $"Unlock for {price}" : $"Remove ads for {price}") : (tv ? "Unlock" : "Remove ads");
            System.Action close = gated ? () => { _hud.HidePanels(); ShowModeMenu(); } : (System.Action)(() => { _hud.HidePanels(); OnPause(); });

            void Done(bool ok, string message)
            {
                if (ok || Premium.Owned)
                {
                    _hud.HidePanels();
                    Sfx.Play("unlock");
                    _hud.Banner("Thank you!", tv ? "The whole harbour is open" : "No more ads between levels");
                    if (gated) StartLevel(_levelNumber);
                }
                else if (message != null) _hud.Toast(message, 3f);
            }
#if UNITY_IOS
            System.Action restore = () => Premium.Restore((ok, msg) => Done(Premium.Owned, Premium.Owned ? null : msg ?? "No purchase found for this Apple ID."));
#else
            System.Action restore = null;   // Google Play restores purchases by itself at each launch
#endif
            Analytics.Event("premium_offer", ("tv", tv), ("gated", gated), ("level", _levelNumber), ("mode", _mode));
            _hud.ShowPremium(title, body, buyLabel, () => Premium.Buy(Done), restore, gated ? "Not now" : "Back", close);
        }

        /// <summary>Test builds: what the remote / gamepad looks like to the game (press a key, then Refresh to see it).</summary>
        private void ShowInputDevices()
        {
            string last = string.IsNullOrEmpty(NavInput.LastKey) ? "none yet" : NavInput.LastKey;
            _hud.ShowButtons("Input devices", $"{NavInput.DeviceList()}\n\nRecent presses:\n{last}", new List<(string, Color, System.Action)>
            {
                ("Refresh", Palette.Hex("2F7FE0"), () => { _hud.HidePanels(); ShowInputDevices(); }),
                ("Back", Palette.Hex("8A8F98"), () => { _hud.HidePanels(); ShowTestTools(); }),
            });
        }

        /// <summary>Test builds only: the tools that used to crowd the pause menu.</summary>
        private void ShowTestTools()
        {
            var grey = Palette.Hex("8A8F98");
            var blue = Palette.Hex("2F7FE0");
            _hud.ShowButtons("Test tools", $"Level {_levelNumber} · {PortSkylines.NameForLevel(_levelNumber)}", new List<(string, Color, System.Action)>
            {
                (SkyTheme.Override < 0 ? $"Sky: Auto ({SkyTheme.Label(SkyTheme.CurrentPhase)})" : $"Sky: {SkyTheme.Label(SkyTheme.CurrentPhase)}", blue,
                    () => { SkyTheme.Override = SkyTheme.Override >= 4 ? -1 : SkyTheme.Override + 1; RebuildEnvironment(); _hud.HidePanels(); ShowTestTools(); }),
                ("Go to level...", blue, ShowLevelPicker),
                ("Skip level", blue, () => { SaveData.SetLevelFor(_mode, _levelNumber + 1); StartLevel(_levelNumber + 1); }),
                ("Reminder in 1 min", blue, () =>
                {
                    _hud.HidePanels();
                    StartCoroutine(Notifications.RequestPermission(ok =>
                    {
                        if (ok) Notifications.SendTest(SaveData.Level, SaveData.UnloadUnlocked && !SaveData.UnloadPlayed);
                        _hud.Banner(ok ? "Reminder in 1 minute" : "Notifications are blocked", ok ? "Leave the game to see it" : "Check the phone's settings");
                    }));
                }),
                ("Replay tutorials", blue, () =>
                {
                    foreach (var k in new[] { TutorialView.TapKey, TutorialView.BayKey, TutorialView.LongKey, TutorialView.LockKey, TutorialView.HiddenKey,
                                              TutorialView.LoadIntroKey, TutorialView.UnloadIntroKey, "unload.tap", "unload.bay",
                                              "booster.ExtraBerth", "booster.CraneLift", "booster.Undo" })
                        PlayerPrefs.DeleteKey("dd.tut." + k);
                    StartLevel(_levelNumber);
                }),
                (Premium.Owned ? "Premium: owned (tap to clear)" : "Premium: not owned (tap to own)", blue,
                    () => { Premium.DevSetOwned(!Premium.Owned); _hud.HidePanels(); ShowTestTools(); }),
                (PlayerPrefs.GetInt("dev_tv", 0) == 1 ? "TV mode: on (restart the game)" : "TV mode: off (restart the game)", blue,
                    () => { PlayerPrefs.SetInt("dev_tv", PlayerPrefs.GetInt("dev_tv", 0) == 1 ? 0 : 1); PlayerPrefs.Save(); _hud.HidePanels(); ShowTestTools(); }),
                ("Input devices", blue, ShowInputDevices),
                ($"Rating card now (asked {ReviewPrompt.Asks}x)", blue, () => { _hud.HidePanels(); StartCoroutine(ReviewPrompt.Ask("test", OnPause)); }),
                ("Reset rating schedule", blue, () => { ReviewPrompt.DevReset(); _hud.HidePanels(); ShowTestTools(); }),
                ("Back", grey, () => { _hud.HidePanels(); OnPause(); }),
            });
        }

        /// <summary>Test tool: pick any level (and so any port) and play it straight away.</summary>
        private void ShowLevelPicker()
        {
            _hud.ShowLevelPicker(_levelNumber, LevelGenerator.AuthoredLevelCount,
                n => $"Level {n}\n{PortSkylines.NameForLevel(n)}",
                go: n => { SaveData.SetLevelFor(_mode, n); _hud.HidePanels(); StartLevel(n); },
                back: () => { _hud.HidePanels(); ShowTestTools(); });
        }

        /// <summary>Rebuilds only the backdrop and harbour (e.g. after changing the sky preview), keeping the level in progress.</summary>
        private void RebuildEnvironment()
        {
            if (_envRoot) Destroy(_envRoot.gameObject);
            _envRoot = EnvironmentBuilder.Build(_levelRoot, _layout, _pal, _cam, _levelNumber, _mode);
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
