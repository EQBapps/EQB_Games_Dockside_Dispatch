"""
Dockside Dispatch app icon (1024 x 1024), drawn with PIL at 4x and downsampled for clean edges.
Scene: sunset harbour; a cargo ship with a raked bow beside a quay; a ship-to-shore crane on the quay lowers a yellow
container in front of the sun (the focal point) onto the green one on deck. Flat game palette, bold dark outlines so
the shapes survive at launcher sizes.
Usage: python make_icon.py <out_1024.png> [<out_512.png>]
"""
import math
import sys

import numpy as np
from PIL import Image, ImageDraw

S = 4                  # supersampling
N = 1024 * S
INK = (62, 47, 44)     # outline / dark
OUT = 11 * S           # outline width
WATER = 712            # waterline


def hexc(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


C = dict(
    sky_top=hexc("B86A9A"), sky_mid=hexc("F0849A"), sky_low=hexc("FF9F7E"), horizon=hexc("FFD08F"),
    sun=hexc("FFF1C4"), sun_glow=hexc("FFE7A6"),
    sea_top=hexc("2FA39E"), sea_bot=hexc("1B7880"), sea_hi=hexc("A8E3DA"), foam=hexc("F3FBF6"),
    hull=hexc("E8463F"), hull_dark=hexc("A72E2A"), white=hexc("FFFFFF"), cream=hexc("FFF4E2"), window=hexc("5BB8D9"),
    red=hexc("E8463F"), blue=hexc("2F7FE0"), yellow=hexc("F7C431"), green=hexc("2DB37A"),
    crane=hexc("F2A23A"), crane_dark=hexc("C97D1E"), lamp=hexc("FFB547"), steel=hexc("4A4440"),
    concrete=hexc("CFC8BC"), concrete_dark=hexc("A39B8F"), concrete_face=hexc("BCB3A5"), stripe=hexc("E8C25A"),
    fender=hexc("2E2A2B"),
)


def P(x, y):
    return (x * S, y * S)


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def shade(c, k):
    return tuple(max(0, min(255, int(round(v * k)))) for v in c)


# ---------------------------------------------------------------- helpers
def stroke_poly(dr, pts, fill, width=OUT, outline=INK):
    dr.polygon(pts, fill=fill)
    dr.line(pts + [pts[0]], fill=outline, width=width, joint="curve")


def rrect(dr, x0, y0, x1, y1, r, fill, width=OUT, outline=INK):
    dr.rounded_rectangle([P(x0, y0), P(x1, y1)], radius=r * S, fill=fill, outline=outline, width=width)


def strut(dr, a, b, w, color):
    dr.line([P(*a), P(*b)], fill=INK, width=int((w + 10) * S))
    dr.line([P(*a), P(*b)], fill=color, width=int(w * S))


def symbol(dr, kind, cx, cy, s, color=(255, 255, 255)):
    if kind == "circle":
        dr.ellipse([P(cx - s, cy - s), P(cx + s, cy + s)], fill=color)
    elif kind == "triangle":
        dr.polygon([P(cx, cy - s * 1.05), P(cx + s * 1.1, cy + s * 0.85), P(cx - s * 1.1, cy + s * 0.85)], fill=color)
    elif kind == "square":
        dr.rounded_rectangle([P(cx - s * 0.85, cy - s * 0.85), P(cx + s * 0.85, cy + s * 0.85)], radius=3 * S, fill=color)
    elif kind == "diamond":
        dr.polygon([P(cx, cy - s * 1.15), P(cx + s * 1.15, cy), P(cx, cy + s * 1.15), P(cx - s * 1.15, cy)], fill=color)


def container(dr, x0, y0, w, h, color, kind):
    """Side view: rounded box, light top edge, darker lower lip (the game's front-face shading), ribs, castings, symbol."""
    r = 16
    rrect(dr, x0, y0, x0 + w, y0 + h, r, color)
    inset = OUT / S / 2 + 2
    dr.rounded_rectangle([P(x0 + inset, y0 + inset), P(x0 + w - inset, y0 + 17)], radius=(r - 8) * S, fill=shade(color, 1.12))
    dr.rounded_rectangle([P(x0 + inset, y0 + h * 0.72), P(x0 + w - inset, y0 + h - inset)], radius=(r - 6) * S, fill=shade(color, 0.8))
    n = max(4, int(w / 34))
    for i in range(1, n):
        x = x0 + w * i / n
        dr.line([P(x, y0 + 19), P(x, y0 + h * 0.7)], fill=shade(color, 0.86), width=5 * S)
    for cx in (x0 + 15, x0 + w - 15):
        for cy in (y0 + 14, y0 + h - 14):
            dr.rounded_rectangle([P(cx - 7, cy - 6), P(cx + 7, cy + 6)], radius=2 * S, fill=shade(color, 0.62))
    dr.rounded_rectangle([P(x0, y0), P(x0 + w, y0 + h)], radius=r * S, outline=INK, width=OUT)
    symbol(dr, kind, x0 + w / 2, y0 + h * 0.43, min(w, h) * 0.17)


# ---------------------------------------------------------------- background
def background():
    img = np.zeros((N, N, 3), dtype=np.float32)
    horizon = 640 * S
    stops = [(0.0, C["sky_top"]), (0.42, C["sky_mid"]), (0.78, C["sky_low"]), (1.0, C["horizon"])]
    for y in range(horizon):
        t = y / horizon
        for i in range(len(stops) - 1):
            if stops[i][0] <= t <= stops[i + 1][0]:
                u = (t - stops[i][0]) / (stops[i + 1][0] - stops[i][0])
                img[y, :, :] = lerp(stops[i][1], stops[i + 1][1], u)
                break
    for y in range(horizon, N):
        t = (y - horizon) / (N - horizon)
        img[y, :, :] = lerp(C["sea_top"], C["sea_bot"], t ** 0.9)
    cx, cy = 540 * S, 400 * S
    yy, xx = np.mgrid[0:horizon, 0:N]
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / S
    glow = np.clip(1.0 - d / 470.0, 0, 1) ** 1.6 * 0.55
    sky = img[:horizon]
    for k in range(3):
        sky[:, :, k] = sky[:, :, k] * (1 - glow) + C["sun_glow"][k] * glow
    im = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB")
    dr = ImageDraw.Draw(im)
    r = 215 * S
    dr.ellipse([cx - r, cy - r, cx + r, cy + r], fill=C["sun"])
    for i, (w, y) in enumerate([(220, 760), (160, 792), (100, 826)]):
        dr.rounded_rectangle([P(420 - w / 2, y), P(420 + w / 2, y + 9)], radius=5 * S, fill=lerp(C["sea_top"], C["sun"], 0.5 - i * 0.1))
    for x, y, w in [(60, 905, 150), (300, 962, 120), (560, 920, 130)]:
        dr.rounded_rectangle([P(x, y), P(x + w, y + 14)], radius=7 * S, fill=C["sea_hi"])
    return im


def quay(im):
    """Quay at the right: concrete top with a hazard edge, darker wall face with fenders facing the ship."""
    dr = ImageDraw.Draw(im)
    stroke_poly(dr, [P(740, 662), P(1040, 662), P(1040, 1040), P(740, 1040)], C["concrete"])
    dr.rectangle([P(746, 668), P(1036, 690)], fill=C["concrete_dark"])
    dr.rectangle([P(746, 690), P(1036, 1036)], fill=C["concrete_face"])

    for i in range(6):
        dr.rectangle([P(748 + i * 52, 663), P(772 + i * 52, 676)], fill=C["stripe"] if i % 2 == 0 else INK)
    dr.line([P(740, 662), P(740, 1040)], fill=INK, width=OUT)



# ---------------------------------------------------------------- scene
def hull_points():
    deck = 572
    pts = [P(130, deck), P(520, deck - 2)]
    # Sheer rising to a raised forecastle at the bow.
    for i in range(1, 9):
        t = i / 8
        pts.append(P(520 + 140 * t, deck - 2 - 36 * t * t))
    # Bow stem: flares forward at the top, then curves back and down below the waterline.
    top_x, top_y = 660, deck - 38
    for i in range(1, 17):
        t = i / 16
        x = top_x + 30 * math.sin(t * math.pi * 0.55) - 74 * t ** 2.1
        y = top_y + (WATER + 44 - top_y) * t
        pts.append(P(x, y))
    pts.append(P(160, WATER + 44))
    # Stern: a rounded transom from below the waterline up to the deck.
    for i in range(1, 17):
        a = math.radians(90 + 180 * i / 16)
        pts.append(P(130 + math.cos(a) * 34 * (1 - 0.35 * math.sin(math.radians(180 * i / 16))),
                     (deck + WATER + 44) / 2 + math.sin(a) * (WATER + 44 - deck) / 2))
    return pts


def draw_ship(im):
    dr = ImageDraw.Draw(im)
    pts = hull_points()
    stroke_poly(dr, pts, C["hull"])
    # Dark boot-top band just above the waterline (clipped to the hull).
    hull_mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(hull_mask).polygon(pts, fill=255)
    band = Image.new("L", im.size, 0)
    ImageDraw.Draw(band).rectangle([P(0, WATER - 32), P(1024, WATER + 60)], fill=255)
    mask = Image.fromarray(np.minimum(np.array(hull_mask), np.array(band)))
    im.paste(Image.new("RGB", im.size, C["hull_dark"]), (0, 0), mask)
    dr = ImageDraw.Draw(im)
    dr.line(pts + [pts[0]], fill=INK, width=OUT, joint="curve")
    # Sheer stripe following the deck line up into the raised bow.
    stripe = [P(134, 596), P(520, 594)] + [P(520 + 136 * (i / 8), 594 - 34 * (i / 8) ** 2) for i in range(1, 9)]
    dr.line(stripe, fill=C["cream"], width=13 * S, joint="curve")
    # Bridge tower at the stern: accommodation block, wider bridge with a window band, funnel, mast.
    rrect(dr, 142, 438, 232, 574, 14, C["white"])
    rrect(dr, 126, 404, 248, 454, 14, C["white"])
    for i in range(3):
        dr.rounded_rectangle([P(141 + i * 36, 420), P(165 + i * 36, 439)], radius=4 * S, fill=C["window"])
    for i in range(2):
        dr.ellipse([P(162 + i * 40, 486), P(180 + i * 40, 504)], fill=C["window"])
    rrect(dr, 166, 344, 208, 404, 8, C["white"])
    dr.rectangle([P(166 + OUT / S / 2, 362), P(208 - OUT / S / 2, 380)], fill=C["red"])
    dr.rounded_rectangle([P(164, 338), P(210, 352)], radius=4 * S, fill=INK)
    dr.line([P(232, 404), P(232, 344)], fill=INK, width=8 * S)

    # Containers on deck: red stacked on blue; green waiting for the yellow being lowered onto it.
    container(dr, 256, 466, 186, 106, C["blue"], "triangle")
    container(dr, 256, 360, 186, 106, C["red"], "circle")
    container(dr, 452, 466, 186, 106, C["green"], "diamond")


def draw_crane(im, bx1=1060):
    dr = ImageDraw.Draw(im)
    legL, legR, legTop, legBot = 808, 948, 256, 664
    boomY0, boomY1 = 206, 254
    for x in (legL, legR):
        rrect(dr, x - 20, legTop, x + 20, legBot, 6, C["crane"])
    strut(dr, (legL + 10, 452), (legR - 10, 300), 13, C["crane_dark"])
    strut(dr, (legL + 10, 300), (legR - 10, 452), 13, C["crane_dark"])
    rrect(dr, legL - 30, 440, legR + 30, 468, 6, C["crane"])
    for x in (legL, legR):
        rrect(dr, x - 38, 640, x + 38, 670, 10, C["steel"])
    # Lattice boom from over the ship to the backreach.
    bx0 = 300
    rrect(dr, bx0, boomY0, bx1, boomY1, 10, C["crane"])
    n = 9
    for i in range(n):
        xa = bx0 + 30 + (bx1 - bx0 - 60) * i / n
        xb = bx0 + 30 + (bx1 - bx0 - 60) * (i + 1) / n
        up = i % 2 == 0
        dr.line([P(xa, boomY1 - 9 if up else boomY0 + 9), P(xb, boomY0 + 9 if up else boomY1 - 9)], fill=C["crane_dark"], width=8 * S)
    dr.rounded_rectangle([P(bx0, boomY0), P(bx1, boomY1)], radius=10 * S, outline=INK, width=OUT)
    # A-frame, stays to the boom tip and the backreach, warning lamp at the apex.
    apex = (872, 84)
    strut(dr, (legL + 8, boomY0 + 4), apex, 22, C["crane"])
    strut(dr, (legR - 8, boomY0 + 4), apex, 22, C["crane"])
    dr.line([P(*apex), P(bx0 + 30, boomY0 + 4)], fill=INK, width=7 * S)
    dr.line([P(*apex), P(min(1040, bx1 - 24), boomY0 + 4)], fill=INK, width=7 * S)
    dr.ellipse([P(apex[0] - 15, apex[1] - 15), P(apex[0] + 15, apex[1] + 15)], fill=C["lamp"], outline=INK, width=8 * S)
    # Machinery house (clear of the rounded icon corner) and the operator cab under the boom.
    rrect(dr, 832, 150, 930, 206, 12, C["white"])
    dr.rectangle([P(846, 168), P(916, 180)], fill=C["crane_dark"])
    rrect(dr, 728, 254, 790, 306, 10, C["white"])
    dr.rounded_rectangle([P(738, 266), P(780, 286)], radius=4 * S, fill=C["window"])
    # Trolley, twin cables, spreader and the yellow container: the focal point, against the sun.
    tx = 545
    rrect(dr, tx - 52, boomY1 - 2, tx + 52, boomY1 + 24, 8, C["steel"])
    for dx in (-32, 32):
        dr.line([P(tx + dx, boomY1 + 22), P(tx + dx * 1.7, 282)], fill=INK, width=7 * S)
    rrect(dr, tx - 104, 278, tx + 104, 296, 6, C["crane"])
    container(dr, tx - 98, 296, 196, 108, C["yellow"], "square")


def water_over_hull(im, bg):
    """The hull sits in the water: repaint the sea below the waterline (left of the quay) and add a foam line."""
    arr = np.array(im)
    bga = np.array(bg)
    w = WATER * S
    arr[w:, :, :] = bga[w:, :, :]
    out = Image.fromarray(arr)
    dr = ImageDraw.Draw(out)
    dr.rounded_rectangle([P(84, WATER - 7), P(700, WATER + 7)], radius=7 * S, fill=C["foam"])
    dr.rounded_rectangle([P(726, WATER - 6), P(1024, WATER + 6)], radius=6 * S, fill=C["foam"])

    dr.rounded_rectangle([P(40, WATER + 24), P(170, WATER + 34)], radius=5 * S, fill=C["sea_hi"])
    return out


if __name__ == "__main__":
    bg = background()
    img = bg.copy()
    draw_crane(img)          # behind the ship (legs on the quay, boom over the deck)
    draw_ship(img)
    quay(img)
    img = water_over_hull(img, bg)
    # The lifted container and its rigging are drawn last so they sit in front of everything.
    # Centre the subject: shift the scene down, extending the sky at the top (the bottom loses only open sea).
    shift = 60 * S
    shifted = Image.new("RGB", img.size, C["sky_top"])
    shifted.paste(img, (0, shift))
    top = np.array(shifted)
    top[:shift, :, :] = np.array(img)[0:1, :, :]
    img = Image.fromarray(top)
    final = img.resize((1024, 1024), Image.LANCZOS)
    final.save(sys.argv[1])
    if len(sys.argv) > 2:
        final.resize((512, 512), Image.LANCZOS).save(sys.argv[2])
    print("saved", sys.argv[1])
