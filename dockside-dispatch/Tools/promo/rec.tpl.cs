// Promo-video recorder, run in Play mode with `unity command script eval` (see Tools/promo/rec.py).
// Records the Game view as a 30 fps JPEG sequence into __DIR__ and logs each sound effect with its frame
// (sfx.csv), so the video's audio can be rebuilt from the game's own sounds. Game time is locked to the frames
// (Time.captureFramerate), so the footage is smooth however slowly the Editor writes the files.
var gc = UnityEngine.Object.FindFirstObjectByType<Dockside.GameController>();
if (gc == null) return "no gc";
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var T = typeof(Dockside.GameController);
var hud = T.GetField("_hud", bf).GetValue(gc) as Dockside.HudView;
hud.HidePanels();
var mode = (Dockside.Core.GameMode)__MODE__;
string dir = @"__DIR__";
System.IO.Directory.CreateDirectory(dir);
foreach (var old in System.IO.Directory.GetFiles(dir)) System.IO.File.Delete(old);

var log = new System.Collections.Generic.List<string>();
UnityEngine.MonoBehaviour runner = null;
int frame = 0; bool rec = true; int maxFrames = __MAXSEC__ * 30;
System.Action<string, float> hook = (n, v) => log.Add(frame + "," + n + "," + v.ToString(System.Globalization.CultureInfo.InvariantCulture));
Dockside.Sfx.EditorPlayed += hook;
UnityEngine.Time.captureFramerate = 30;

System.Collections.IEnumerator Rec()
{
    while (rec && frame < maxFrames)
    {
        yield return new UnityEngine.WaitForEndOfFrame();
        var tex = UnityEngine.ScreenCapture.CaptureScreenshotAsTexture();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, frame.ToString("00000") + ".jpg"), tex.EncodeToJPG(92));
        UnityEngine.Object.Destroy(tex);
        frame++;
    }
    Dockside.Sfx.EditorPlayed -= hook;
    UnityEngine.Time.captureFramerate = 0;
    System.IO.File.WriteAllLines(System.IO.Path.Combine(dir, "sfx.csv"), log);
    System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "done.txt"), frame.ToString());
}

Dockside.Core.GameSession S() => T.GetField("_session", bf).GetValue(gc) as Dockside.Core.GameSession;
System.Collections.IDictionary Map() => (System.Collections.IDictionary)T.GetField("_containers", bf).GetValue(gc);
var tapM = T.GetMethod("OnContainerTapped", bf);
void Tap(int i) => tapM.Invoke(gc, new object[] { Map()[i] });
void Booster(Dockside.BoosterType b) => T.GetMethod("OnBooster", bf).Invoke(gc, new object[] { b });
bool Won() => S() != null && S().IsWon;

// Plays the solver's solution from move `from` for `count` moves, one every `pace` seconds.
System.Collections.IEnumerator Solve(int from, int count, float pace)
{
    var sol = Dockside.Core.Solver.Solve(S().Level).solution;
    for (int i = from; sol != null && i < sol.Count && i < from + count; i++)
    {
        Tap(sol[i]);
        yield return new UnityEngine.WaitForSeconds(pace);
    }
}

System.Collections.IEnumerator Plan()
{
__PLAN__
    rec = false;
    UnityEngine.Object.Destroy(runner.gameObject, 1f);
}

if ((bool)T.GetField("_menuOpen", bf).GetValue(gc) && __MENU__ == 0) T.GetMethod("PlayFromMenu", bf).Invoke(gc, new object[] { mode });
if (__MENU__ == 1) gc.ShowModeMenu();
else gc.StartLevel(mode, __LEVEL__);
// The coroutines run on their own object: the game stops its own coroutines when it changes level or menu.
runner = new UnityEngine.GameObject("PromoRecorder").AddComponent<UnityEngine.EventSystems.EventTrigger>();
runner.StartCoroutine(Rec());
runner.StartCoroutine(Plan());
return "recording " + mode + " " + __LEVEL__;
