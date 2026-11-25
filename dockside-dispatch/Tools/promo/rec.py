"""Records gameplay clips for the promo videos from the open Unity Editor (Play mode, Game view).

Usage:  python Tools/promo/rec.py <out_dir> <clip> [<clip> ...]      (or "all")
Each clip is recorded portrait (1080x1920, suffix _p) and/or landscape (1920x1080, suffix _l) as a 30 fps JPEG
sequence plus sfx.csv (frame,sound,volume). It changes the Editor's PlayerPrefs: save and restore them around it.
"""
import os, subprocess, sys, time

HERE = os.path.dirname(os.path.abspath(__file__))
TPL = open(os.path.join(HERE, "rec.tpl.cs"), encoding="utf-8").read()

# Plans run inside the recorder coroutine (see rec.tpl.cs: Solve, Tap, Booster, Won, S).
FULL = """    yield return new UnityEngine.WaitForSeconds(2.2f);
    yield return Solve(0, 999, {pace});
    float end = UnityEngine.Time.time + 12f;
    while (!Won() && UnityEngine.Time.time < end) yield return null;
    yield return new UnityEngine.WaitForSeconds({tail});"""

BOOSTERS = """    yield return new UnityEngine.WaitForSeconds(2.2f);
    yield return Solve(0, 3, 0.85f);
    yield return new UnityEngine.WaitForSeconds(0.6f);
    Booster(Dockside.BoosterType.ExtraBerth);
    yield return new UnityEngine.WaitForSeconds(1.6f);
    Booster(Dockside.BoosterType.CraneLift);
    yield return new UnityEngine.WaitForSeconds(0.9f);
    var s = S(); int pick = -1;
    for (int m = 0; m < s.ContainerCount; m++)   // a buried container that has a ship waiting for it
        if (s.InYard(m) && !s.IsReachable(m) && !s.IsLocked(m) && (pick < 0 || s.BerthFor(m) >= 0 && s.BerthFor(pick) < 0)) pick = m;
    if (pick >= 0) Tap(pick);
    yield return new UnityEngine.WaitForSeconds(2.2f);
    Booster(Dockside.BoosterType.Undo);
    yield return new UnityEngine.WaitForSeconds(2.0f);"""

MENU = """    yield return new UnityEngine.WaitForSeconds(2.6f);
    var menu = T.GetField("_menu", bf).GetValue(gc);
    menu.GetType().GetMethod("Select", bf).Invoke(menu, new object[] {{ 1, true }});   // swipe to Unload
    yield return new UnityEngine.WaitForSeconds({hold});"""

# name: (mode, level, sky phase, tv, menu, max seconds, plan, orientations)
# sky: 0 morning, 1 midday, 2 afternoon, 3 sunset, 4 night.  mode: 0 Load, 1 Unload.
CLIPS = {
    "load_hk_sunset":    (0, 3,  3, 0, 0, 40, FULL.format(pace="0.8f", tail="3.5f"), "pl"),
    "hard_sg_midday":    (0, 10, 1, 0, 0, 50, FULL.format(pace="0.8f", tail="3.5f"), "pl"),
    "special_mum_morn":  (0, 13, 0, 0, 0, 50, FULL.format(pace="0.85f", tail="3f"), "pl"),
    "bay_mum_afternoon": (0, 17, 2, 0, 0, 55, FULL.format(pace="0.8f", tail="3f"), "pl"),
    "unload_dxb_night":  (1, 8,  4, 0, 0, 50, FULL.format(pace="0.8f", tail="3.5f"), "pl"),
    "boosters_sunset":   (0, 22, 3, 0, 0, 25, BOOSTERS, "pl"),
    "menu":              (0, 26, 3, 0, 1, 7,  MENU.format(hold="3f"), "pl"),
    "tv_load_night":     (0, 7,  4, 1, 0, 45, FULL.format(pace="0.8f", tail="3f"), "l"),
    "tv_menu":           (0, 12, 2, 1, 1, 7,  MENU.format(hold="3f"), "l"),
}

PREFS = """foreach (var k in new[]{"tap","bay","long","lock","hidden","load.intro","unload.intro","unload.tap","unload.bay","booster.ExtraBerth","booster.CraneLift","booster.Undo"}) UnityEngine.PlayerPrefs.SetInt("dd.tut."+k,1);
UnityEngine.PlayerPrefs.SetInt("dd.mode", __MODE__);
UnityEngine.PlayerPrefs.SetInt("dd.level", __MODE__==1 ? 60 : __LEVEL__);
UnityEngine.PlayerPrefs.SetInt("dd.unload.level", __MODE__==1 ? __LEVEL__ : 30);
UnityEngine.PlayerPrefs.SetInt("dd.unload.played", 1);
UnityEngine.PlayerPrefs.SetInt("dd.unload.remind_at", 0);
UnityEngine.PlayerPrefs.SetInt("dd.notif.prompts", 9);
UnityEngine.PlayerPrefs.SetInt("dd.review.asks", 4);
UnityEngine.PlayerPrefs.SetInt("dd.coins", 460);
UnityEngine.PlayerPrefs.SetInt("dd.booster.ExtraBerth", 2); UnityEngine.PlayerPrefs.SetInt("dd.booster.CraneLift", 2); UnityEngine.PlayerPrefs.SetInt("dd.booster.Undo", 3);
UnityEngine.PlayerPrefs.SetInt("dd.sky", __PHASE__);
if (__TV__==1) { UnityEngine.PlayerPrefs.SetInt("dev_tv",1); UnityEngine.PlayerPrefs.SetInt("premium_owned",1); }
else UnityEngine.PlayerPrefs.DeleteKey("dev_tv");
UnityEngine.PlayerPrefs.Save();
return "prefs ok";"""


def unity(*args, timeout=60):
    r = subprocess.run(["unity", *args], capture_output=True, text=True, timeout=timeout + 30, shell=(os.name == "nt"))
    return (r.stdout + r.stderr).strip()


def eval_file(path, timeout=30):
    """Runs a C# file in the Editor; returns its result line (compiler warnings come back too and are skipped)."""
    out = unity("command", "script", "eval", "--", "--file", path, "--timeout", str(timeout), timeout=timeout)
    lines = [l for l in out.splitlines() if "warning" not in l and l.strip()]
    return lines[-1] if lines else out


def record(out_root, name, orient):
    mode, level, phase, tv, menu, maxsec, plan, _ = CLIPS[name]
    w, h = (1080, 1920) if orient == "p" else (1920, 1080)
    out = os.path.join(out_root, f"{name}_{orient}")
    tmp = os.path.join(out_root, "_tmp")
    os.makedirs(tmp, exist_ok=True)
    rep = {"__MODE__": mode, "__LEVEL__": level, "__PHASE__": phase, "__TV__": tv, "__MENU__": menu,
           "__MAXSEC__": maxsec, "__DIR__": out.replace("\\", "/")}

    def fill(s):
        for k, v in rep.items(): s = s.replace(k, str(v))
        return s

    unity("command", "editor.playmode", "stop"); time.sleep(2)
    p = os.path.join(tmp, "prefs.cs"); open(p, "w", encoding="utf-8").write(fill(PREFS))
    print(f"{name}_{orient}:", eval_file(p))
    unity("command", "script", "eval", "--", "--code",
          f'UnityEditor.PlayModeWindow.SetCustomRenderingResolution({w}, {h}, "S {w}x{h}"); return "res";', "--timeout", "30")
    unity("command", "editor.playmode", "start"); time.sleep(9)
    p = os.path.join(tmp, "rec.cs"); open(p, "w", encoding="utf-8").write(fill(TPL.replace("__PLAN__", plan)))
    for _ in range(3):   # retry only if the scene wasn't ready; never start a second recorder
        r = eval_file(p)
        if "recording" in r or "no gc" not in r: break
        time.sleep(3)
    print("   ", r)
    done = os.path.join(out, "done.txt")
    end = time.time() + 900
    while not os.path.exists(done) and time.time() < end: time.sleep(3)
    print("    frames:", open(done).read() if os.path.exists(done) else "TIMED OUT")


if __name__ == "__main__":
    out_root, names = sys.argv[1], sys.argv[2:]
    if names == ["all"]: names = list(CLIPS)
    for n in names:
        base, _, o = n.partition(":")       # "load_hk_sunset:p" records one orientation only
        for orient in (o or CLIPS[base][7]):
            record(out_root, base, orient)
    unity("command", "editor.playmode", "stop")
