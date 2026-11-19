"""
Play Games achievements for Dockside Dispatch: 512 x 512 icons in the app icon's flat, ink-outlined style, plus the
Play Console import zip (AchievementsMetadata.csv, AchievementsLocalizations.csv, AchievementsIconsMappings.csv, icons).
Each icon is a round badge: a category-coloured disc, a motif, and a ring whose colour gives the tier
(bronze / silver / gold for the level milestones, cream otherwise). Motifs stay inside the disc so Play's circular
crop never cuts them. No text in the icons (they are shared by every language).
Usage: python Tools/make_achievements.py   (run from the dockside-dispatch folder)
"""
import csv
import io
import math
import os
import zipfile

import numpy as np
from PIL import Image, ImageDraw

import make_icon as ic
from make_icon import C, INK, OUT, P, S, container, hexc, rrect, shade, stroke_poly, strut

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "store", "play-games")
ICON_DIR = os.path.join(OUT_DIR, "icons")
N = 1024 * S
CX = CY = 512
DISC_R = 404          # inside of the ring
RING_R = 470          # outside of the ring

TIER = dict(bronze=hexc("D98B4A"), silver=hexc("C9D3DC"), gold=hexc("F7C431"), plain=hexc("FFF4E2"))
BG = dict(
    load=(hexc("8FD3F5"), hexc("2F7FE0")),
    unload=(hexc("FFC27A"), hexc("E0782A")),
    world=(hexc("7FE0D0"), hexc("1B8C8A")),
    skill=(hexc("D3B4FF"), hexc("8A55D8")),
    night=(hexc("47507F"), hexc("121937")),
    total=(hexc("9FE8B8"), hexc("2DA06A")),
)

# (file stem, name, description, incremental, steps, initial state, points, category, tier, motif)
ACHIEVEMENTS = [
    ("a01_first_dispatch", "First Dispatch", "Win your first level.", False, None, "Revealed", 5, "load", "plain", "lift"),
    ("a02_harbour_hand", "Harbour Hand", "Complete Load level 10.", False, None, "Revealed", 10, "load", "bronze", "ship1"),
    ("a03_dock_master", "Dock Master", "Complete Load level 50.", False, None, "Revealed", 25, "load", "silver", "ship2"),
    ("a04_harbour_legend", "Harbour Legend", "Complete all 100 Load levels.", False, None, "Revealed", 50, "load", "gold", "ship3"),
    ("a05_change_of_shift", "Change of Shift", "Win your first Unload level.", False, None, "Revealed", 10, "unload", "plain", "truck1"),
    ("a06_yard_boss", "Yard Boss", "Complete Unload level 50.", False, None, "Revealed", 25, "unload", "silver", "truck2"),
    ("a07_unload_legend", "Unload Legend", "Complete all 100 Unload levels.", False, None, "Revealed", 50, "unload", "gold", "truck3"),
    ("a08_around_the_world", "Around the World", "Reach the 20th port (Seattle) in either mode.", False, None, "Revealed", 30, "world", "gold", "globe"),
    ("a09_clean_sheet", "Clean Sheet", "Win level 20 or later without using a booster.", False, None, "Revealed", 15, "skill", "plain", "sparkle"),
    ("a10_comeback_captain", "Comeback Captain", "Win a level after taking a second chance.", False, None, "Revealed", 10, "skill", "plain", "lifebuoy"),
    ("a11_night_shift", "Night Shift", "Win a level at night.", False, None, "Hidden", 10, "night", "plain", "night"),
    ("a12_crane_operator", "Crane Operator", "Use Crane Lift 25 times.", True, 25, "Revealed", 10, "skill", "plain", "hook"),
    ("a13_key_holder", "Key Holder", "Open 50 locked containers.", True, 50, "Revealed", 15, "skill", "plain", "key"),
    ("a14_mystery_solved", "Mystery Solved", "Reveal 100 mystery containers.", True, 100, "Revealed", 15, "skill", "plain", "mystery"),
    ("a15_long_haul", "Long Haul", "Dispatch 100 long containers.", True, 100, "Revealed", 15, "skill", "plain", "long"),
    ("a16_fleet_commander", "Fleet Commander", "Send off 500 ships.", True, 500, "Revealed", 25, "total", "plain", "fleet"),
    ("a17_convoy_leader", "Convoy Leader", "Fill 500 trucks.", True, 500, "Revealed", 25, "total", "plain", "convoy"),
]


# ---------------------------------------------------------------- shared shapes
def disc_background(top, bot):
    """Radial gradient from the top-left (light) to the bottom-right (deep), filling the whole square."""
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    d = np.sqrt((xx - 360 * S) ** 2 + (yy - 300 * S) ** 2) / (760 * S)
    t = np.clip(d, 0, 1)[..., None]
    img = np.array(top, np.float32) * (1 - t) + np.array(bot, np.float32) * t
    return Image.fromarray(img.astype(np.uint8), "RGB").convert("RGBA")


def circle_mask(r):
    m = Image.new("L", (N, N), 0)
    ImageDraw.Draw(m).ellipse([P(CX - r, CY - r), P(CX + r, CY + r)], fill=255)
    return m


def water(dr, y, top=None, bot=None):
    """A band of sea across the lower disc, with a foam line and a couple of highlights."""
    dr.rectangle([P(0, y), P(1024, 1024)], fill=top or C["sea_top"])
    dr.rectangle([P(0, y + 120), P(1024, 1024)], fill=bot or C["sea_bot"])
    dr.rounded_rectangle([P(80, y - 7), P(944, y + 7)], radius=7 * S, fill=C["foam"])
    for x, yy, w in [(230, y + 50, 140), (600, y + 70, 120), (390, y + 150, 160)]:
        dr.rounded_rectangle([P(x, yy), P(x + w, yy + 12)], radius=6 * S, fill=C["sea_hi"])


def ship(dr, x0, x1, deck, depth, stack):
    """Side-view ship, bow to the right: hull with a raised forecastle, sheer stripe, bridge at the stern, containers.
    stack: list of (column, row, colour, symbol) on a grid of 112 x 68 boxes starting just ahead of the bridge."""
    pts = [P(x0, deck), P(x1 - 90, deck)]
    for i in range(1, 7):
        t = i / 6
        pts.append(P(x1 - 90 + 70 * t, deck - 26 * t * t))
    pts += [P(x1 - 20 + 14, deck - 30), P(x1 - 50, deck + depth), P(x0 + 30, deck + depth)]
    for i in range(1, 9):
        a = math.radians(90 + 90 * i / 8)
        pts.append(P(x0 + 30 + math.cos(a) * 30, deck + depth - depth * (1 - math.sin(a))))
    stroke_poly(dr, pts, C["hull"])
    dr.line([P(x0 + 10, deck + 22), P(x1 - 90, deck + 22), P(x1 - 26, deck - 6)], fill=C["cream"], width=11 * S, joint="curve")
    # Bridge tower.
    rrect(dr, x0 + 14, deck - 120, x0 + 84, deck, 10, C["white"])
    rrect(dr, x0 + 2, deck - 150, x0 + 96, deck - 108, 10, C["white"])
    for i in range(3):
        dr.rounded_rectangle([P(x0 + 14 + i * 26, deck - 138), P(x0 + 32 + i * 26, deck - 122)], radius=3 * S, fill=C["window"])
    rrect(dr, x0 + 30, deck - 196, x0 + 64, deck - 150, 6, C["white"])
    dr.rectangle([P(x0 + 33, deck - 182), P(x0 + 61, deck - 168)], fill=C["red"])
    bx = x0 + 102
    for col, row, color, kind in stack:
        container(dr, bx + col * 116, deck - 70 - row * 70, 112, 70, color, kind)


def truck(dr, x0, base, color, cargo=None, cargo_kind="square", long_cargo=False):
    """Side-view truck facing right: flatbed in the truck colour, cab-over tractor, wheels; optional container on the bed."""
    bed_x0, bed_x1 = x0, x0 + 400
    if cargo:
        w = 390 if long_cargo else 260
        container(dr, bed_x0 + 8, base - 196, w, 104, cargo, cargo_kind)
    rrect(dr, bed_x0, base - 94, bed_x1, base - 62, 8, color)
    # Cab.
    cx0 = bed_x1 + 16
    stroke_poly(dr, [P(cx0, base - 60), P(cx0, base - 210), P(cx0 + 96, base - 210), P(cx0 + 128, base - 150), P(cx0 + 128, base - 60)], color)
    dr.polygon([P(cx0 + 62, base - 196), P(cx0 + 92, base - 196), P(cx0 + 114, base - 150), P(cx0 + 62, base - 150)], fill=C["window"])
    dr.rounded_rectangle([P(cx0 + 10, base - 128), P(cx0 + 118, base - 116)], radius=5 * S, fill=C["cream"])
    rrect(dr, bed_x1 - 4, base - 70, cx0 + 140, base - 50, 6, C["steel"])
    for wx in (bed_x0 + 70, bed_x0 + 150, cx0 + 70):
        dr.ellipse([P(wx - 40, base - 80), P(wx + 40, base)], fill=C["fender"], outline=INK, width=OUT)
        dr.ellipse([P(wx - 15, base - 55), P(wx + 15, base - 25)], fill=C["concrete"])


def road(dr, y):
    dr.rectangle([P(0, y), P(1024, 1024)], fill=hexc("5A5250"))
    for x in range(60, 1000, 150):
        dr.rounded_rectangle([P(x, y + 46), P(x + 80, y + 58)], radius=6 * S, fill=hexc("F2E6C9"))
    dr.line([P(0, y), P(1024, y)], fill=INK, width=OUT)


def star(dr, cx, cy, r, color, points=5, inner=0.45, outline=True):
    pts = []
    for i in range(points * 2):
        a = math.radians(-90 + 180 * i / points)
        rr = r if i % 2 == 0 else r * inner
        pts.append(P(cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    if outline:
        stroke_poly(dr, pts, color, width=int(OUT * 0.8))
    else:
        dr.polygon(pts, fill=color)


def crown(dr, cx, cy, w):
    h = w * 0.62
    pts = [P(cx - w / 2, cy + h / 2), P(cx - w / 2, cy - h / 2), P(cx - w / 4, cy), P(cx, cy - h / 2 - 10),
           P(cx + w / 4, cy), P(cx + w / 2, cy - h / 2), P(cx + w / 2, cy + h / 2)]
    stroke_poly(dr, pts, C["yellow"])
    for x, y in [(cx - w / 2, cy - h / 2), (cx, cy - h / 2 - 10), (cx + w / 2, cy - h / 2)]:
        dr.ellipse([P(x - 16, y - 16), P(x + 16, y + 16)], fill=C["red"], outline=INK, width=int(OUT * 0.7))
    dr.rounded_rectangle([P(cx - w / 2 + 14, cy + h / 2 - 34), P(cx + w / 2 - 14, cy + h / 2 - 18)], radius=6 * S, fill=shade(C["yellow"], 0.82))


def hook(dr, cx, top, bottom, block=True):
    """Crane cables, block and an open hook whose tip sits at `bottom`."""
    for dx in (-22, 22):
        dr.line([P(cx + dx, top), P(cx + dx * 0.6, bottom - 150)], fill=INK, width=8 * S)
    if block:
        rrect(dr, cx - 46, bottom - 170, cx + 46, bottom - 110, 12, C["crane"])
        dr.ellipse([P(cx - 12, bottom - 152), P(cx + 12, bottom - 128)], fill=C["steel"])
    dr.line([P(cx, bottom - 110), P(cx, bottom - 64)], fill=INK, width=22 * S)
    dr.line([P(cx, bottom - 110), P(cx, bottom - 64)], fill=C["steel"], width=12 * S)
    box = [P(cx - 52, bottom - 100), P(cx + 52, bottom + 4)]
    dr.arc(box, 0, 200, fill=INK, width=30 * S)
    dr.arc(box, 0, 200, fill=C["steel"], width=18 * S)


# ---------------------------------------------------------------- motifs
def m_lift(dr):
    water(dr, 760)
    hook(dr, 512, 0, 330)
    rrect(dr, 362, 330, 662, 350, 6, C["crane"])
    container(dr, 372, 350, 280, 160, C["red"], "circle")
    ship(dr, 180, 860, 700, 70, [])


def m_ship(level):
    def draw(dr):
        water(dr, 720)
        stacks = {
            1: [(1, 0, C["blue"], "triangle")],
            2: [(0, 0, C["blue"], "triangle"), (1, 0, C["green"], "diamond"), (0, 1, C["red"], "circle")],
            3: [(0, 0, C["blue"], "triangle"), (1, 0, C["green"], "diamond"), (2, 0, C["yellow"], "square"),
                (0, 1, C["red"], "circle"), (1, 1, C["yellow"], "square")],
        }[level]
        ship(dr, 200, 830, 650, 80, stacks)
    return draw


def m_truck(level):
    def draw(dr):
        if level == 2:
            for i, (col, kind) in enumerate([(C["green"], "diamond"), (C["blue"], "triangle"), (C["yellow"], "square")]):
                container(dr, 140 + i * 250, 370, 236, 120, shade(col, 0.85), kind)
            container(dr, 260, 250, 236, 120, shade(C["red"], 0.85), "circle")
        road(dr, 690)
        truck(dr, 170, 700, C["red"] if level != 2 else C["blue"], cargo=C["yellow"] if level != 2 else C["red"],
              cargo_kind="square" if level != 2 else "circle")
    return draw


def m_globe(dr):
    cx, cy, r = 512, 470, 250
    dr.ellipse([P(cx - r, cy - r), P(cx + r, cy + r)], fill=hexc("4FA8E8"), outline=INK, width=OUT)
    land = hexc("5CC27C")
    for pts in ([(-170, -120), (-60, -170), (10, -110), (-30, -40), (-120, -10), (-190, -50)],
                ([(40, -60), (150, -110), (210, -20), (170, 80), (80, 60), (50, 0)]),
                ([(-140, 50), (-40, 60), (-20, 150), (-100, 200), (-160, 130)])):
        dr.polygon([P(cx + x, cy + y) for x, y in pts], fill=land)
    for k in (-0.55, 0, 0.55):
        w = r * math.cos(math.asin(abs(k))) if k else r
        dr.line([P(cx - w, cy + k * r), P(cx + w, cy + k * r)], fill=hexc("2F7FE0"), width=6 * S)
    dr.ellipse([P(cx - r * 0.45, cy - r), P(cx + r * 0.45, cy + r)], outline=hexc("2F7FE0"), width=6 * S)
    dr.ellipse([P(cx - r, cy - r), P(cx + r, cy + r)], outline=INK, width=OUT)
    # A small ship sailing around the globe's lower edge, with a dotted wake.
    for i in range(6):
        a = math.radians(200 + i * 14)
        x, y = cx + math.cos(a) * (r + 60), cy + math.sin(a) * (r + 60)
        dr.ellipse([P(x - 9, y - 9), P(x + 9, y + 9)], fill=C["cream"])
    ship(dr, 530, 860, 760, 56, [(0, 0, C["red"], "circle")])


def m_sparkle(dr):
    container(dr, 292, 400, 440, 250, C["green"], "diamond")
    for cx, cy, r in [(250, 300, 70), (770, 330, 52), (720, 720, 60), (300, 740, 40), (512, 250, 46)]:
        star(dr, cx, cy, r, C["sun"], points=4, inner=0.32)


def m_lifebuoy(dr):
    water(dr, 690)
    cx, cy, ro, ri = 512, 470, 270, 150
    box = [P(cx - ro, cy - ro), P(cx + ro, cy + ro)]
    for i in range(8):
        dr.pieslice(box, -90 + i * 45 + 22.5, -45 + i * 45 + 22.5, fill=C["red"] if i % 2 == 0 else C["white"])
    dr.ellipse([P(cx - ri, cy - ri), P(cx + ri, cy + ri)], fill=(0, 0, 0, 0))
    dr.ellipse(box, outline=INK, width=OUT)
    dr.ellipse([P(cx - ri, cy - ri), P(cx + ri, cy + ri)], outline=INK, width=OUT)
    for a in (45, 135, 225, 315):
        x, y = cx + math.cos(math.radians(a)) * (ro + ri) / 2, cy + math.sin(math.radians(a)) * (ro + ri) / 2
        dr.arc([P(x - 70, y - 70), P(x + 70, y + 70)], a + 60, a + 120, fill=C["cream"], width=12 * S)


def m_night(dr):
    dr.ellipse([P(560, 150), P(780, 370)], fill=hexc("FFF1C4"))
    dr.ellipse([P(620, 130), P(820, 330)], fill=(0, 0, 0, 0))
    for x, y, r in [(260, 230, 18), (390, 170, 12), (330, 340, 10), (800, 420, 14), (470, 300, 9), (230, 430, 10)]:
        star(dr, x, y, r * 2, hexc("FFF1C4"), points=4, inner=0.35, outline=False)
    water(dr, 760, top=hexc("1F4A6E"), bot=hexc("12304A"))
    for (x, y, col, kind) in [(250, 610, C["blue"], "triangle"), (512, 610, C["red"], "circle"), (380, 470, C["yellow"], "square")]:
        container(dr, x, y, 250, 140, col, kind)
    # Lit floodlight pool over the stack.
    dr.ellipse([P(700, 470), P(740, 510)], fill=C["lamp"], outline=INK, width=int(OUT * 0.7))


def m_hook(dr):
    rrect(dr, 120, 140, 904, 196, 10, C["crane"])
    for i in range(7):
        xa, xb = 150 + i * 106, 150 + (i + 1) * 106
        dr.line([P(xa, 186 if i % 2 == 0 else 150), P(xb, 150 if i % 2 == 0 else 186)], fill=C["crane_dark"], width=8 * S)
    dr.rounded_rectangle([P(120, 140), P(904, 196)], radius=10 * S, outline=INK, width=OUT)
    rrect(dr, 450, 192, 574, 222, 8, C["steel"])
    hook(dr, 512, 222, 640)
    container(dr, 330, 680, 364, 190, C["yellow"], "square")


def m_key(dr):
    # Padlock (open) behind, gold key in front.
    # Open shackle: the left leg is out of the body, as if the key just turned.
    dr.arc([P(540, 250), P(720, 430)], 180, 360, fill=INK, width=60 * S)
    dr.arc([P(540, 250), P(720, 430)], 180, 360, fill=C["concrete"], width=40 * S)
    dr.line([P(720, 340), P(720, 420)], fill=INK, width=60 * S)
    dr.line([P(720, 340), P(720, 420)], fill=C["concrete"], width=40 * S)
    dr.line([P(540, 340), P(540, 370)], fill=INK, width=60 * S)
    dr.line([P(540, 340), P(540, 370)], fill=C["concrete"], width=40 * S)
    rrect(dr, 470, 420, 790, 690, 30, C["steel"])
    dr.ellipse([P(610, 520), P(650, 560)], fill=INK)
    dr.rectangle([P(622, 550), P(638, 610)], fill=INK)
    gold, dark = C["yellow"], shade(C["yellow"], 0.75)
    dr.ellipse([P(150, 470), P(370, 690)], fill=gold, outline=INK, width=OUT)
    dr.ellipse([P(220, 540), P(300, 620)], fill=BG["skill"][1], outline=INK, width=OUT)
    stroke_poly(dr, [P(360, 560), P(700, 560), P(700, 600), P(360, 600)], gold)
    stroke_poly(dr, [P(600, 596), P(640, 596), P(640, 660), P(600, 660)], gold)
    stroke_poly(dr, [P(660, 596), P(700, 596), P(700, 640), P(660, 640)], gold)
    dr.line([P(380, 572), P(690, 572)], fill=shade(gold, 1.12), width=8 * S)
    _ = dark


def m_mystery(dr):
    container(dr, 170, 330, 420, 260, hexc("3B3436"), "square")
    # Cover the default symbol with a big question mark drawn as shapes.
    dr.rectangle([P(330, 380), P(430, 470)], fill=hexc("3B3436"))
    qx, qy = 380, 455
    dr.arc([P(qx - 62, qy - 90), P(qx + 62, qy + 30)], 180, 450, fill=C["white"], width=30 * S)
    dr.line([P(qx, qy + 30), P(qx, qy + 62)], fill=C["white"], width=30 * S)
    dr.ellipse([P(qx - 18, qy + 82), P(qx + 18, qy + 118)], fill=C["white"])
    # Magnifying glass.
    gx, gy, gr = 640, 580, 150
    strut(dr, (gx + 100, gy + 100), (gx + 230, gy + 230), 44, C["crane_dark"])
    dr.ellipse([P(gx - gr, gy - gr), P(gx + gr, gy + gr)], fill=hexc("CFEFFF"), outline=INK, width=OUT)
    dr.ellipse([P(gx - gr + 24, gy - gr + 24), P(gx + gr - 24, gy + gr - 24)], outline=C["crane"], width=22 * S)
    dr.arc([P(gx - 90, gy - 90), P(gx + 90, gy + 90)], 200, 260, fill=C["white"], width=14 * S)


def m_long(dr):
    road(dr, 700)
    truck(dr, 234, 710, C["blue"], cargo=C["green"], cargo_kind="diamond", long_cargo=True)
    for x, y in ((150, 470), (120, 540), (160, 610)):
        dr.line([P(x, y), P(x + 70, y)], fill=C["cream"], width=14 * S)


def m_fleet(dr):
    water(dr, 700)
    ship(dr, 420, 880, 530, 60, [(0, 0, C["yellow"], "square"), (1, 0, C["red"], "circle")])
    ship(dr, 150, 640, 700, 70, [(0, 0, C["blue"], "triangle"), (1, 0, C["green"], "diamond"), (0, 1, C["red"], "circle")])


def m_convoy(dr):
    road(dr, 710)
    truck(dr, -300, 720, C["yellow"], cargo=C["blue"], cargo_kind="triangle")
    truck(dr, 250, 720, C["green"], cargo=C["red"], cargo_kind="circle")


MOTIFS = dict(lift=m_lift, ship1=m_ship(1), ship2=m_ship(2), ship3=m_ship(3), truck1=m_truck(1), truck2=m_truck(2),
              truck3=m_truck(3), globe=m_globe, sparkle=m_sparkle, lifebuoy=m_lifebuoy, night=m_night, hook=m_hook,
              key=m_key, mystery=m_mystery, long=m_long, fleet=m_fleet, convoy=m_convoy)

# Scenes drawn at "world" scale are zoomed in so they read at small sizes: (zoom, focus point, where the focus lands).
ZOOM = dict(
    lift=(1.15, (512, 470), (512, 500)),
    ship1=(1.2, (515, 600), (512, 600)), ship2=(1.2, (515, 590), (512, 600)), ship3=(1.15, (515, 590), (512, 640)),
    truck1=(1.15, (448, 600), (512, 610)), truck3=(1.15, (448, 600), (512, 660)),
)
# Drawn after the zoom, on top of the scene.
OVERLAY = dict(ship3=lambda dr: crown(dr, 512, 250, 200), truck3=lambda dr: crown(dr, 512, 250, 200))


def zoomed(layer, motif):
    if motif not in ZOOM:
        return layer
    z, (fx, fy), (tx, ty) = ZOOM[motif]
    coeffs = (1 / z, 0, (fx - tx / z) * S, 0, 1 / z, (fy - ty / z) * S)
    return layer.transform((N, N), Image.AFFINE, coeffs, resample=Image.BICUBIC)


def badge(category, tier, motif):
    top, bot = BG[category]
    im = disc_background(top, bot)
    layer = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    MOTIFS[motif](ImageDraw.Draw(layer))
    layer = zoomed(layer, motif)
    if motif in OVERLAY:
        OVERLAY[motif](ImageDraw.Draw(layer))
    # Clip the motif to the disc so nothing crosses the ring.
    a = np.array(layer)
    a[..., 3] = np.minimum(a[..., 3], np.array(circle_mask(DISC_R)))
    im.alpha_composite(Image.fromarray(a))
    dr = ImageDraw.Draw(im)
    ring = TIER[tier]
    w = RING_R - DISC_R
    dr.ellipse([P(CX - RING_R, CY - RING_R), P(CX + RING_R, CY + RING_R)], outline=ring, width=w * S)
    dr.ellipse([P(CX - RING_R + 8, CY - RING_R + 8), P(CX + RING_R - 8, CY + RING_R - 8)], outline=shade(ring, 1.08), width=10 * S)
    dr.ellipse([P(CX - RING_R, CY - RING_R), P(CX + RING_R, CY + RING_R)], outline=INK, width=OUT)
    dr.ellipse([P(CX - DISC_R, CY - DISC_R), P(CX + DISC_R, CY + DISC_R)], outline=INK, width=OUT)
    return im.convert("RGB").resize((512, 512), Image.LANCZOS)


def csv_text(rows):
    buf = io.StringIO()
    w = csv.writer(buf, lineterminator="\n")
    for r in rows:
        w.writerow(r)
    return buf.getvalue()


def main():
    os.makedirs(ICON_DIR, exist_ok=True)
    meta, mapping = [], []
    for order, (stem, name, desc, incr, steps, state, pts, cat, tier, motif) in enumerate(ACHIEVEMENTS, 1):
        assert "," not in name and "," not in desc, name
        assert pts % 5 == 0 and 5 <= pts <= 200, name
        badge(cat, tier, motif).save(os.path.join(ICON_DIR, stem + ".png"), optimize=True)
        meta.append([name, desc, "True" if incr else "False", steps if incr else "", state, pts, order])
        mapping.append([name, stem + ".png"])
        print("icon", stem)
    assert sum(r[5] for r in meta) <= 1000
    zpath = os.path.join(OUT_DIR, "DocksideDispatch_achievements.zip")
    with zipfile.ZipFile(zpath, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("AchievementsMetadata.csv", csv_text(meta))
        z.writestr("AchievementsLocalizations.csv", "")      # English is the default language: nothing to add
        z.writestr("AchievementsIconsMappings.csv", csv_text(mapping))
        for stem, *_ in ACHIEVEMENTS:
            z.write(os.path.join(ICON_DIR, stem + ".png"), stem + ".png")
    print("wrote", os.path.relpath(zpath, ROOT), f"({len(meta)} achievements, {sum(r[5] for r in meta)} points)")


if __name__ == "__main__":
    main()
