"""
App Store screenshots for Dockside Dispatch, in the same style as the Play set (Tools/make_store.py), rendered with
headless Edge.

Apple needs one set per device family and scales it down for the smaller screens:
  - iPhone 6.9" (1320 x 2868 portrait): used for every iPhone, from the 4" iPhone SE up.
  - iPad 13"   (2752 x 2064 landscape): used for every iPad, from the iPad mini up.

Inputs:  Editor captures in store/raw/ios/ (i*: 1320 x 2868, a*: 2752 x 2064), taken in Play mode with the Game view
         at those sizes (see BUILD_IOS.md, "Screenshots").
Outputs: store/app-store/screenshots/iphone_6.9/01..07.png and store/app-store/screenshots/ipad_13/01..06.png
         (no alpha, in upload order).
Usage:   python Tools/make_appstore.py      (from the dockside-dispatch folder)
"""
import os
import shutil
import tempfile

from PIL import Image

from make_store import FONT_CSS, FONTS, ROOT, render

RAW = os.path.join(ROOT, "store", "raw", "ios")
OUT = os.path.join(ROOT, "store", "app-store", "screenshots")

# iPhone: (capture, caption line 1, line 2, gradient top, gradient bottom). Same story as the Play phone set, minus TV.
IPHONE = [
    ("i1_load_singapore_sunset.png", "Think one", "move ahead", "#FF9F7E", "#B86A9A"),
    ("i2_hard_singapore_midday.png", "Every 5th level", "is a Hard level", "#F08A6E", "#C2453A"),
    ("i3_bay_mumbai_afternoon.png", "Plan ahead with", "the holding bay", "#FFD08F", "#E0A526"),
    ("i4_unload_singapore_morning.png", "Unload the ship", "onto the trucks", "#79C0EE", "#3F9FE3"),
    ("i5_load_amsterdam_night.png", "No timers,", "no rush", "#47507F", "#121937"),
    ("i6_unload_sydney_morning.png", "200 levels in", "20 world ports", "#B6DBF0", "#5DA3D6"),
    ("i7_menu.png", "Two ways", "to play", "#FFB36B", "#E07A2E"),
]

# iPad (landscape): (capture, one-line caption, gradient start, gradient end)
IPAD = [
    ("a1_load_mumbai_sunset.png", "Think one move ahead", "#FF9F7E", "#B86A9A"),
    ("a2_hard_dubai_afternoon.png", "Every 5th level is a Hard level", "#F08A6E", "#C2453A"),
    ("a3_unload_dubai_midday.png", "Unload the ship onto the trucks", "#79C0EE", "#3F9FE3"),
    ("a4_load_shanghai_night.png", "No timers, no rush", "#47507F", "#121937"),
    ("a5_unload_la_afternoon.png", "Made for big screens", "#FFD08F", "#E0A526"),
    ("a6_menu.png", "Two ways to play", "#FFB36B", "#E07A2E"),
]

CAPTION = """color:#FFF4E2; font-weight:700; -webkit-text-stroke:{s}px #4E3F3B; paint-order:stroke fill;
       text-shadow:0 {d}px 0 #4E3F3B; letter-spacing:1px; text-align:center; position:absolute; left:0; right:0;"""


def iphone(i, line1, line2, c1, c2):
    # 1320 x 2868. The capture is the same shape as the screen, so it fills the frame without cropping.
    sw = 1000
    sh = round(sw * 2868 / 1320)
    return f"""<!doctype html><html><head><meta charset="utf-8"><style>{FONT_CSS}
.c {{ width:1320px; height:2868px; position:relative; overflow:hidden; font-family:Fredoka;
     background: linear-gradient(180deg, {c1} 0%, {c2} 100%); }}
.cap {{ {CAPTION.format(s=20, d=12)} top:120px; font-size:134px; line-height:1.04; }}
.phone {{ position:absolute; left:{(1320 - sw - 52) // 2}px; top:520px; width:{sw + 52}px; height:{sh + 52}px;
         border-radius:110px; background:#2E2422; box-shadow:0 26px 0 rgba(46,36,34,0.35); }}
.screen {{ position:absolute; left:26px; top:26px; width:{sw}px; height:{sh}px; border-radius:86px; overflow:hidden; }}
.screen img {{ width:{sw}px; height:{sh}px; display:block; }}
</style></head><body><div class="c">
<div class="cap">{line1}<br>{line2}</div>
<div class="phone"><div class="screen"><img src="i{i}.png"></div></div>
</div></body></html>"""


def ipad(i, line, c1, c2):
    # 2752 x 2064 (4:3). The capture is 4:3 too.
    sw = 2080
    sh = sw * 3 // 4
    return f"""<!doctype html><html><head><meta charset="utf-8"><style>{FONT_CSS}
.c {{ width:2752px; height:2064px; position:relative; overflow:hidden; font-family:Fredoka;
     background: linear-gradient(135deg, {c1} 0%, {c2} 100%); }}
.cap {{ {CAPTION.format(s=22, d=12)} top:70px; font-size:150px; }}
.tab {{ position:absolute; left:{(2752 - sw - 64) // 2}px; top:330px; width:{sw + 64}px; height:{sh + 64}px;
       border-radius:80px; background:#2E2422; box-shadow:0 26px 0 rgba(46,36,34,0.35); }}
.screen {{ position:absolute; left:32px; top:32px; width:{sw}px; height:{sh}px; border-radius:50px; overflow:hidden; }}
.screen img {{ width:{sw}px; height:{sh}px; display:block; }}
</style></head><body><div class="c">
<div class="cap">{line}</div>
<div class="tab"><div class="screen"><img src="a{i}.png"></div></div>
</div></body></html>"""


def main():
    work = tempfile.mkdtemp(prefix="dd_appstore_")
    for f in ("Fredoka-Bold.ttf", "Fredoka-SemiBold.ttf"):
        shutil.copy(os.path.join(FONTS, f), work)

    out = os.path.join(OUT, "iphone_6.9")
    os.makedirs(out, exist_ok=True)
    for i, (shot, l1, l2, c1, c2) in enumerate(IPHONE, 1):
        im = Image.open(os.path.join(RAW, shot))
        assert im.size == (1320, 2868), (shot, im.size)
        im.convert("RGB").save(os.path.join(work, f"i{i}.png"))
        render(work, iphone(i, l1, l2, c1, c2), (1320, 2868), os.path.join(out, f"{i:02d}.png"))

    out = os.path.join(OUT, "ipad_13")
    os.makedirs(out, exist_ok=True)
    for i, (shot, line, c1, c2) in enumerate(IPAD, 1):
        im = Image.open(os.path.join(RAW, shot))
        assert im.size == (2752, 2064), (shot, im.size)
        im.convert("RGB").save(os.path.join(work, f"a{i}.png"))
        render(work, ipad(i, line, c1, c2), (2752, 2064), os.path.join(out, f"{i:02d}.png"))

    # Apple rejects screenshots of the wrong size: check every output.
    for sub, size in (("iphone_6.9", (1320, 2868)), ("ipad_13", (2752, 2064))):
        for f in sorted(os.listdir(os.path.join(OUT, sub))):
            im = Image.open(os.path.join(OUT, sub, f))
            assert im.size == size and im.mode == "RGB", (sub, f, im.size, im.mode)
    shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main()
