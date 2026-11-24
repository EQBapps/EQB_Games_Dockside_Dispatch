using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Dockside
{
    /// <summary>
    /// TV remote, gamepad and keyboard, read in one place so the board, the HUD and the menus agree on what a press means.
    /// <list type="bullet">
    /// <item>Move: arrow keys, the D-pad (remote or gamepad), a joystick's hat or a gamepad's left stick, repeating while held.</item>
    /// <item>Submit: OK / D-pad centre, Enter, Space or the gamepad's A.</item>
    /// <item>Back: the Back button (Android reports it as Escape), Backspace, or the gamepad's B or Start.</item>
    /// </list>
    /// Every connected device is read, not just the "current" one: a Bluetooth remote often appears as several devices
    /// (keys, an air mouse or motion sensor, voice), and whichever of them last sent anything becomes current. Moves come
    /// from presses, so a device that reports a direction all the time (a drifting stick, a motion sensor, a key whose
    /// release was lost) can't block the D-pad: each new press always moves. Sticks count only as they cross into a
    /// direction. Buttons on cards are driven by the EventSystem's UI input module; this class is for the board's focus
    /// cursor, the mode menu and the Back button.
    /// </summary>
    public static class NavInput
    {
        /// <summary>
        /// True once the player has used the arrows, D-pad or a gamepad, until they next touch or click. Focus rings and
        /// the board cursor only show in this mode. Always true on a TV.
        /// </summary>
        public static bool NavMode => ScreenInfo.IsTv || _navMode;

        /// <summary>"Tap" on touch screens, "Select" on a TV (for instructions such as "Tap a container").</summary>
        public static string Tap => ScreenInfo.IsTv ? "Select" : "Tap";
        public static string tap => ScreenInfo.IsTv ? "select" : "tap";

        public static Vector2Int Move { get { Poll(); return _move; } }
        public static bool Submit { get { Poll(); return _submit; } }
        public static bool Back { get { Poll(); return _back; } }

        /// <summary>Test builds: the last few keys or buttons seen and the devices they came from, newest first.</summary>
        public static string LastKey => string.Join("\n", _recent);
        private static readonly List<string> _recent = new List<string>();

        private static void Log(string press)
        {
            _recent.Insert(0, press);
            if (_recent.Count > 5) _recent.RemoveAt(5);
        }

        private const float RepeatDelay = 0.4f, RepeatRate = 0.14f;
        private const int MaxRepeats = 12;   // enough to cross any yard; stops a key whose release was lost from scrolling forever
        private const float StickOn = 0.6f, StickOff = 0.35f;
        private static bool _navMode, _submit, _back;
        private static Vector2Int _move;
        private static float _nextRepeat;
        private static int _repeats;
        private static int _frame = -1;

#if ENABLE_INPUT_SYSTEM
        // The press being repeated while held: a button (key, D-pad, hat) or a gamepad's stick (by device id).
        private static ButtonControl _repeatButton;
        private static int _repeatStick = -1;
        private static Vector2Int _repeatDir;
        // Each gamepad's stick direction last frame, so only a change of direction counts as a press.
        private static readonly Dictionary<int, Vector2Int> _stickDir = new Dictionary<int, Vector2Int>();
        private static bool _hooked;

        private static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            InputSystem.onDeviceChange += (device, change) =>
            {
                if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed || change == InputDeviceChange.Reconnected
                    || change == InputDeviceChange.Disconnected)
                    Debug.Log($"[Dockside] Input device {change}: {device.displayName ?? device.name} ({device.layout})");
                if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected) ResetHeld();
            };
            Application.focusChanged += _ => ResetHeld();   // a key released while another app had focus is never seen
        }

        private static void ResetHeld()
        {
            _repeatButton = null;
            _repeatStick = -1;
            _stickDir.Clear();
        }

        /// <summary>Connected input devices, one per line (test tools).</summary>
        public static string DeviceList()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var d in InputSystem.devices)
                if (d is Keyboard || d is Gamepad || d is Joystick)
                    sb.Append(d.displayName ?? d.name).Append(" (").Append(d.layout).Append(d.enabled ? ")" : ", off)").Append('\n');
            return sb.ToString().TrimEnd();
        }
#else
        public static string DeviceList() => "";
#endif

        private static void Poll()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            _move = default;
            _submit = _back = false;
#if ENABLE_INPUT_SYSTEM
            Hook();
            float now = Time.unscaledTime;
            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                if (!device.enabled) continue;
                switch (device)
                {
                    case Keyboard kb:
                        Button(kb.upArrowKey, Vector2Int.up, now);
                        Button(kb.downArrowKey, Vector2Int.down, now);
                        Button(kb.leftArrowKey, Vector2Int.left, now);
                        Button(kb.rightArrowKey, Vector2Int.right, now);
                        _submit |= kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                        _back |= kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame;
                        if (BuildFlags.TestTools && kb.anyKey.wasPressedThisFrame)
                            foreach (var k in kb.allKeys)
                                if (k != null && k.wasPressedThisFrame) { Log($"Key {k.keyCode} from {kb.displayName}"); break; }
                        break;
                    case Gamepad gp:
                        Button(gp.dpad.up, Vector2Int.up, now);
                        Button(gp.dpad.down, Vector2Int.down, now);
                        Button(gp.dpad.left, Vector2Int.left, now);
                        Button(gp.dpad.right, Vector2Int.right, now);
                        Stick(gp.deviceId, gp.leftStick.ReadValue(), now);
                        _submit |= gp.buttonSouth.wasPressedThisFrame;
                        _back |= gp.buttonEast.wasPressedThisFrame || gp.startButton.wasPressedThisFrame;
                        if (BuildFlags.TestTools) TestLog(gp);
                        break;
                    case Joystick js:
                        if (js.hatswitch is DpadControl hat)
                        {
                            Button(hat.up, Vector2Int.up, now);
                            Button(hat.down, Vector2Int.down, now);
                            Button(hat.left, Vector2Int.left, now);
                            Button(hat.right, Vector2Int.right, now);
                        }
                        if (BuildFlags.TestTools) TestLog(js);
                        break;
                }
            }

            // Holding repeats the last press, after a short delay, while it stays down.
            if (_move == Vector2Int.zero && (_repeatButton != null || _repeatStick >= 0))
            {
                bool stillDown = _repeatButton != null ? _repeatButton.isPressed
                    : _stickDir.TryGetValue(_repeatStick, out var sd) && sd == _repeatDir;
                if (!stillDown || _repeats >= MaxRepeats) ResetRepeat();
                else if (now >= _nextRepeat) { _move = _repeatDir; _nextRepeat = now + RepeatRate; _repeats++; }
            }

            if (_move != Vector2Int.zero || _submit) _navMode = true;
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame && !(pointer is Touchscreen && ScreenInfo.IsTv)) _navMode = false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static void Button(ButtonControl b, Vector2Int dir, float now)
        {
            if (b == null || !b.wasPressedThisFrame || _move != Vector2Int.zero) return;   // first press this frame wins
            _move = dir;
            _repeatButton = b;
            _repeatStick = -1;
            StartRepeat(dir, now);
        }

        private static void Stick(int deviceId, Vector2 v, float now)
        {
            _stickDir.TryGetValue(deviceId, out var was);
            // Hysteresis: a direction holds until the stick comes well back towards the centre.
            float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y);
            Vector2Int dir = was;
            if (was != Vector2Int.zero && Vector2.Dot(v, was) < StickOff) dir = Vector2Int.zero;
            if (dir == Vector2Int.zero && Mathf.Max(ax, ay) > StickOn)
                dir = ay >= ax ? new Vector2Int(0, v.y > 0 ? 1 : -1) : new Vector2Int(v.x > 0 ? 1 : -1, 0);
            _stickDir[deviceId] = dir;
            if (dir == was || dir == Vector2Int.zero || _move != Vector2Int.zero) return;
            _move = dir;
            _repeatButton = null;
            _repeatStick = deviceId;
            StartRepeat(dir, now);
        }

        private static void StartRepeat(Vector2Int dir, float now)
        {
            _repeatDir = dir;
            _nextRepeat = now + RepeatDelay;
            _repeats = 0;
        }

        private static void ResetRepeat()
        {
            _repeatButton = null;
            _repeatStick = -1;
        }

        private static void TestLog(InputDevice d)
        {
            foreach (var c in d.allControls)
                if (c is ButtonControl b && !c.synthetic && b.wasPressedThisFrame) { Log($"{c.path} from {d.displayName}"); break; }
        }
#endif
    }
}
