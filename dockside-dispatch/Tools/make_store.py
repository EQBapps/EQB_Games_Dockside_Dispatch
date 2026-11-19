"""
Google Play listing art for Dockside Dispatch, rendered with headless Edge.
Inputs:  store/raw/shot*.png (1080 x 1920 game-view captures), store/raw/clean_hongkong_sunset_2x.png (HUD hidden),
         the game logo and app icon from Assets/_Game/Branding, and the Fredoka fonts.
Outputs: store/play/screenshot_1..N.png (1080 x 1920), feature_graphic.png (1024 x 500), icon_512.png; all without alpha.
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

# (raw capture, caption line 1, caption line 2, gradient top, gradient bottom)
SHOTS = [
    ("shot1_load_hongkong_sunset.png", "Sort the cargo,", "fill the ships!", "#FF9F7E", "#B86A9A"),
    ("shot2_unload_singapore_midday.png", "Unload the ship", "onto the trucks!", "#79C0EE", "#3F9FE3"),
    ("shot3_modes_menu.png", "Two ways", "to play", "#FFB36B", "#E07A2E"),
    ("shot4_load_amsterdam_night.png", "From sunrise", "to starry nights", "#47507F", "#121937"),
    ("shot5_unload_sydney_morning.png", "Sail to famous", "ports worldwide", "#B6DBF0", "#5DA3D6"),
    ("shot6_load_shanghai_afternoon.png", "200 levels of", "harbour puzzles", "#FFD08F", "#E0A526"),
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


def main():
    os.makedirs(OUT, exist_ok=True)
    work = tempfile.mkdtemp(prefix="dd_store_")
    for f in ("Fredoka-Bold.ttf", "Fredoka-SemiBold.ttf"):
        shutil.copy(os.path.join(FONTS, f), work)
    shutil.copy(LOGO, os.path.join(work, "logo.png"))

    for i, (shot, line1, line2, c1, c2) in enumerate(SHOTS, 1):
        shutil.copy(os.path.join(RAW, shot), os.path.join(work, f"s{i}.png"))
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
