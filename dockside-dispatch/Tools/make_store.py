"""
Google Play listing art for Dockside Dispatch, rendered with headless Edge.
Inputs:  game-view captures in store/raw/ (p*: phone 1080 x 1920; t*: tablet / Chromebook 2560 x 1440; tv*: Android
         TV 1920 x 1080 in TV mode; f*: foldable), store/raw/clean_hongkong_sunset_2x.png (HUD hidden), the game logo
         and app icon from Assets/_Game/Branding, and the Fredoka fonts. The captures come from the Editor in Play mode
         (see store/listing.md, "Graphics").
Outputs (all without alpha):
         store/play/screenshot_1..8.png       phone, 1080 x 1920, captioned, in a phone frame
         store/play/tablet/tablet_1..6.png    7" and 10" tablets and Chromebook, 2560 x 1440, captioned, in a tablet frame
         store/play/tv/tv_1..4.png            Android TV, 1920 x 1080, the real screen with no caption
         store/play/foldable/*.png            the real screen at foldable aspect ratios
         store/play/feature_graphic.png (1024 x 500), store/play/icon_512.png
Usage:   python Tools/make_store.py            (run from the dockside-dispatch folder)
"""
import os
import shutil
import subprocess
import tempfile

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RAW = os.path.join(ROOT, "store", "raw")
OUT = os.path.join(ROOT, "store", "play")
GAME = os.path.join(ROOT, "DocksideDispatch", "Assets", "_Game")
LOGO = os.path.join(GAME, "Branding", "DocksideDispatch_Logo.png")
ICON = os.path.join(GAME, "Branding", "DocksideDispatch_Icon.png")
FONTS = os.path.join(GAME, "Resources", "Fonts")
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"

# Phone: (raw capture, caption line 1, caption line 2, gradient top, gradient bottom). A capture named tv* is shown on a
# TV set instead of a phone.
SHOTS = [
    ("p1_load_singapore_sunset.png", "Think one", "move ahead", "#FF9F7E", "#B86A9A"),
    ("p2_hard_singapore_midday.png", "Every 5th level", "is a Hard level", "#F08A6E", "#C2453A"),
    ("tv1_load_singapore_sunset.png", "Play together", "on your TV", "#6F5A8E", "#2E2442"),
    ("p3_bay_mumbai_afternoon.png", "Plan ahead with", "the holding bay", "#FFD08F", "#E0A526"),
    ("p4_unload_singapore_morning.png", "Unload the ship", "onto the trucks", "#79C0EE", "#3F9FE3"),
    ("p6_load_amsterdam_night.png", "No timers,", "no rush", "#47507F", "#121937"),
    ("p7_unload_sydney_morning.png", "200 levels in", "20 world ports", "#B6DBF0", "#5DA3D6"),
    ("p5_menu.png", "Two ways", "to play", "#FFB36B", "#E07A2E"),
]

# Tablet / Chromebook (landscape): (raw capture, one-line caption, gradient start, gradient end)
TABLET = [
    ("t1_load_mumbai_sunset.png", "Think one move ahead", "#FF9F7E", "#B86A9A"),
    ("t3_hard_dubai_afternoon.png", "Every 5th level is a Hard level", "#F08A6E", "#C2453A"),
    ("t2_unload_dubai_midday.png", "Unload the ship onto the trucks", "#79C0EE", "#3F9FE3"),
    ("t5_load_shanghai_night.png", "No timers, no rush", "#47507F", "#121937"),
    ("t6_unload_la_afternoon.png", "Made for big screens", "#FFD08F", "#E0A526"),
    ("t4_menu.png", "Two ways to play", "#FFB36B", "#E07A2E"),
]

# Android TV: the real screen, full size (Play's TV screenshots show the app itself).
TV = ["tv1_load_singapore_sunset.png", "tv4_hard_dubai_midday.png", "tv3_unload_dubai_night.png", "tv2_menu.png"]

# Foldables: (raw capture, output name). Unfolded landscape screens use the landscape layout.
FOLDABLE = [
    ("f1_unfolded_1x1.png", "unfolded_1x1.png"),
    ("f2_unfolded_4x3_landscape.png", "unfolded_4x3_landscape.png"),
    ("f3_unfolded_3x4_portrait.png", "unfolded_3x4_portrait.png"),
    ("f4_folded_10x16.png", "folded_10x16.png"),
]

FONT_CSS = """
@font-face { font-family: Fredoka; src: url(Fredoka-Bold.ttf); font-weight: 700; }
@font-face { font-family: Fredoka; src: url(Fredoka-SemiBold.ttf); font-weight: 600; }
html, body { margin: 0; }
"""


def render(work, html, size, png):
    name = os.path.splitext(os.path.basename(png))[0] + ".html"
    with open(os.path.join(work, name), "w", encoding="utf-8") as f:
        f.write(html)
    url = "file:///" + os.path.join(work, name).replace("\\", "/")
    subprocess.run([EDGE, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--allow-file-access-from-files",
                    f"--window-size={size[0]},{size[1]}", f"--screenshot={png}", url],
                   check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    Image.open(png).convert("RGB").save(png)          # Play wants no alpha channel
    print("wrote", os.path.relpath(png, ROOT))


def tv_in_phone_slot(i, line1, line2, c1, c2):
    """A phone-listing screenshot that shows the TV version: the capture on a TV set, with two short notes under it."""
    return f"""<!doctype html><html><head><meta charset="utf-8"><style>{FONT_CSS}
.c {{ width:1080px; height:1920px; position:relative; overflow:hidden; font-family:Fredoka;
     background: linear-gradient(180deg, {c1} 0%, {c2} 100%); }}
.cap {{ position:absolute; left:0; right:0; top:70px; text-align:center; color:#FFF4E2; line-height:1.02;
       font-weight:700; font-size:104px; -webkit-text-stroke:16px #4E3F3B; paint-order:stroke fill;
       text-shadow:0 10px 0 #4E3F3B; letter-spacing:1px; }}
.glow {{ position:absolute; left:-80px; right:-80px; top:520px; height:820px;
        background:radial-gradient(ellipse 50% 50% at 50% 50%, rgba(255,190,120,0.35) 0%, rgba(255,190,120,0) 70%); }}
.tv {{ position:absolute; left:40px; top:640px; width:1000px; height:586px; border-radius:26px; background:#1E1716;
      box-shadow:0 18px 0 rgba(0,0,0,0.35); }}
.screen {{ position:absolute; left:16px; top:16px; width:968px; height:544px; border-radius:10px; overflow:hidden; }}
.screen img {{ width:968px; height:544px; display:block; }}
.neck {{ position:absolute; left:500px; top:1226px; width:80px; height:70px; background:#1E1716; }}
.foot {{ position:absolute; left:290px; top:1290px; width:500px; height:26px; border-radius:13px; background:#1E1716; }}
.chips {{ position:absolute; left:0; right:0; top:1440px; text-align:center; }}
.chip {{ display:inline-block; margin:14px 10px; padding:18px 38px; border-radius:48px; background:#4E3F3B;
        border:5px solid #2E2422; color:#FFF4E2; font-weight:600; font-size:50px; }}
</style></head><body><div class="c">
<div class="cap">{line1}<br>{line2}</div>
<div class="glow"></div>
<div class="tv"><div class="screen"><img src="s{i}.png"></div></div><div class="neck"></div><div class="foot"></div>
<div class="chips"><span class="chip">Play with the remote</span><br><span class="chip">No ads on TV</span></div>
</div></body></html>"""


def main():
    os.makedirs(OUT, exist_ok=True)
    work = tempfile.mkdtemp(prefix="dd_store_")
    for f in ("Fredoka-Bold.ttf", "Fredoka-SemiBold.ttf"):
        shutil.copy(os.path.join(FONTS, f), work)
    shutil.copy(LOGO, os.path.join(work, "logo.png"))

    for i, (shot, line1, line2, c1, c2) in enumerate(SHOTS, 1):
        shutil.copy(os.path.join(RAW, shot), os.path.join(work, f"s{i}.png"))
        if shot.startswith("tv"):
            render(work, tv_in_phone_slot(i, line1, line2, c1, c2), (1080, 1920), os.path.join(OUT, f"screenshot_{i}.png"))
            continue
        html = f"""<!doctype html><html><head><meta charset="utf-8"><style>{FONT_CSS}
.c {{ width:1080px; height:1920px; position:relative; overflow:hidden; font-family:Fredoka;
     background: linear-gradient(180deg, {c1} 0%, {c2} 100%); }}
.cap {{ position:absolute; left:0; right:0; top:70px; text-align:center; color:#FFF4E2; line-height:1.02;
       font-weight:700; font-size:104px; -webkit-text-stroke:16px #4E3F3B; paint-order:stroke fill;
       text-shadow:0 10px 0 #4E3F3B; letter-spacing:1px; }}
.phone {{ position:absolute; left:130px; top:360px; width:820px; height:1458px; border-radius:64px; background:#2E2422;
         box-shadow:0 22px 0 rgba(46,36,34,0.35); }}
.screen {{ position:absolute; left:22px; top:22px; width:776px; height:1414px; border-radius:46px; overflow:hidden; }}
.screen img {{ width:776px; height:1414px; display:block; object-fit:cover; object-position:center top; }}
</style></head><body><div class="c">
<div class="cap">{line1}<br>{line2}</div>
<div class="phone"><div class="screen"><img src="s{i}.png"></div></div>
</div></body></html>"""
        render(work, html, (1080, 1920), os.path.join(OUT, f"screenshot_{i}.png"))

    # Tablet and Chromebook: a landscape capture in a tablet frame under a one-line caption.
    tab_out = os.path.join(OUT, "tablet")
    os.makedirs(tab_out, exist_ok=True)
    for i, (shot, line, c1, c2) in enumerate(TABLET, 1):
        shutil.copy(os.path.join(RAW, shot), os.path.join(work, f"t{i}.png"))
        html = f"""<!doctype html><html><head><meta charset="utf-8"><style>{FONT_CSS}
.c {{ width:2560px; height:1440px; position:relative; overflow:hidden; font-family:Fredoka;
     background: linear-gradient(135deg, {c1} 0%, {c2} 100%); }}
.cap {{ position:absolute; left:0; right:0; top:48px; text-align:center; color:#FFF4E2; font-weight:700; font-size:112px;
       -webkit-text-stroke:16px #4E3F3B; paint-order:stroke fill; text-shadow:0 10px 0 #4E3F3B; letter-spacing:1px; }}
.tab {{ position:absolute; left:345px; top:250px; width:1870px; height:1094px; border-radius:56px; background:#2E2422;
       box-shadow:0 22px 0 rgba(46,36,34,0.35); }}
.screen {{ position:absolute; left:26px; top:26px; width:1818px; height:1042px; border-radius:34px; overflow:hidden; }}
.screen img {{ width:1818px; height:1042px; display:block; object-fit:cover; }}
</style></head><body><div class="c">
<div class="cap">{line}</div>
<div class="tab"><div class="screen"><img src="t{i}.png"></div></div>
</div></body></html>"""
        render(work, html, (2560, 1440), os.path.join(tab_out, f"tablet_{i}.png"))

    # Android TV: the real 1920 x 1080 screen.
    tv_out = os.path.join(OUT, "tv")
    os.makedirs(tv_out, exist_ok=True)
    for i, shot in enumerate(TV, 1):
        im = Image.open(os.path.join(RAW, shot)).convert("RGB")
        assert im.size == (1920, 1080), (shot, im.size)
        im.save(os.path.join(tv_out, f"tv_{i}.png"))
        print("wrote", os.path.relpath(os.path.join(tv_out, f"tv_{i}.png"), ROOT))

    # Foldables: the real screen at each aspect ratio.
    fold_out = os.path.join(OUT, "foldable")
    os.makedirs(fold_out, exist_ok=True)
    for shot, name in FOLDABLE:
        Image.open(os.path.join(RAW, shot)).convert("RGB").save(os.path.join(fold_out, name))
        print("wrote", os.path.relpath(os.path.join(fold_out, name), ROOT))

    # Feature graphic: the sunset skyline and glittering water (above the ships), with the logo centred.
    bg = Image.open(os.path.join(RAW, "clean_hongkong_sunset_2x.png")).convert("RGB")
    w = bg.width
    h = round(w * 500 / 1024)
    top = 1150 - h                                     # bottom edge just above the ships' bridges
    bg.crop((0, top, w, top + h)).resize((1024, 500), Image.LANCZOS).save(os.path.join(work, "bg.png"))
    html = f"""<!doctype html><html><head><meta charset="utf-8"><style>{FONT_CSS}
.f {{ width:1024px; height:500px; position:relative; overflow:hidden; }}
.bg {{ position:absolute; inset:0; width:1024px; height:500px; }}
.shade {{ position:absolute; inset:0; background:radial-gradient(ellipse 60% 75% at 50% 52%, rgba(42,27,51,0.28) 0%, rgba(42,27,51,0) 70%); }}
.logo {{ position:absolute; left:50%; top:50%; width:500px; transform:translate(-50%,-50%); filter:drop-shadow(0 10px 0 rgba(46,36,34,0.35)); }}
</style></head><body><div class="f"><img class="bg" src="bg.png"><div class="shade"></div><img class="logo" src="logo.png"></div></body></html>"""
    render(work, html, (1024, 500), os.path.join(OUT, "feature_graphic.png"))

    Image.open(ICON).convert("RGB").resize((512, 512), Image.LANCZOS).save(os.path.join(OUT, "icon_512.png"))
    print("wrote store/play/icon_512.png")
    shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main()
