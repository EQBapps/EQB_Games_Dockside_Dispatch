"""
Dockside Dispatch game logo (1006 x 817, transparent), shown on the boot splash over the dusk-plum background.
Top: an emblem in the app icon's style (make_icon.py): the ship with a raised bow and its containers, the ship-to-shore
crane lowering the yellow container, a gradient sun behind and a rounded band of water. Below: the "DOCKSIDE" wordmark
(cream Fredoka Bold, thick dark outline, drop shadow) and "DISPATCH" on a tilted red ribbon with folded tails.
Usage: python make_logo.py <out.png> [<preview_on_dark.png>]
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

import make_icon as ic

S = ic.S
W, H = 1006, 817
FONT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "DocksideDispatch", "Assets", "_Game", "Resources", "Fonts", "Fredoka-Bold.ttf")
INK = ic.INK
CREAM = ic.hexc("FFF4E2")
RIBBON = ic.hexc("E8463F")
RIBBON_DARK = ic.hexc("B02E2A")
SHADOW = ic.hexc("4E3F3B")


def premul_resize(im, size):
    return im.convert("RGBa").resize(size, Image.LANCZOS).convert("RGBA")


def emblem():
    """The icon scene without its sky: sun disc, crane, ship, quay strip and a rounded water band (icon coordinates)."""
    n = ic.N
    im = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    # Gradient sun disc behind the lifted container.
    cx, cy, r = 545, 400, 250
    yy, xx = np.mgrid[0:n, 0:n]
    d = np.sqrt((xx - cx * S) ** 2 + (yy - cy * S) ** 2) / (r * S)
    inside = d <= 1.0
    t = np.clip(d, 0, 1) ** 1.4
    col = np.zeros((n, n, 4), dtype=np.uint8)
    a, b = np.array(ic.C["sun"], dtype=np.float32), np.array(ic.hexc("FFB98A"), dtype=np.float32)
    for k in range(3):
        col[:, :, k] = (a[k] + (b[k] - a[k]) * t).astype(np.uint8)
    col[:, :, 3] = np.where(inside, 255, 0).astype(np.uint8)
    im = Image.fromarray(col, "RGBA")
    ic.draw_crane(im, bx1=1000)
    ic.draw_ship(im)
    # Quay top strip with its hazard edge.
    dr = ImageDraw.Draw(im)
    ic.stroke_poly(dr, [ic.P(740, 662), ic.P(1000, 662), ic.P(1000, 716), ic.P(740, 716)], ic.C["concrete"])
    for i in range(5):
        dr.rectangle([ic.P(748 + i * 50, 663), ic.P(770 + i * 50, 676)], fill=ic.C["stripe"] if i % 2 == 0 else INK)
    # Water band: covers everything below the waterline, rounded ends, foam on top, a few highlights.
    band = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    bd = ImageDraw.Draw(band)
    x0, x1, y0, y1 = 70, 1010, ic.WATER - 6, ic.WATER + 86
    bd.rounded_rectangle([ic.P(x0, y0), ic.P(x1, y1)], radius=46 * S, fill=ic.C["sea_top"], outline=INK, width=ic.OUT)
    bd.rounded_rectangle([ic.P(x0 + 12, y0 + 10), ic.P(x1 - 12, y0 + 22)], radius=6 * S, fill=ic.C["foam"])
    for x, y, w in [(150, 752, 120), (430, 770, 150), (760, 750, 110)]:
        bd.rounded_rectangle([ic.P(x, y), ic.P(x + w, y + 11)], radius=5 * S, fill=ic.C["sea_hi"])
    # Hide the hull and quay below the waterline (inside the band) by compositing the band over them.
    arr = np.array(im)
    arr[(ic.WATER + 6) * S:, :, 3] = 0
    im = Image.fromarray(arr, "RGBA")
    im.alpha_composite(band)
    return im.crop(im.getbbox())


def wordmark(scale):
    """'DOCKSIDE' in cream with a thick outline and a drop shadow, at the given supersampling scale."""
    font = ImageFont.truetype(FONT, 190 * scale)
    text = "DOCKSIDE"
    stroke = 17 * scale
    tmp = ImageDraw.Draw(Image.new("RGBA", (10, 10)))
    l, t, r, b = tmp.textbbox((0, 0), text, font=font, stroke_width=stroke)
    w, h = r - l, b - t
    pad = 20 * scale
    im = Image.new("RGBA", (w + pad * 2, h + pad * 2 + 14 * scale), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.text((pad - l, pad - t + 14 * scale), text, font=font, fill=SHADOW, stroke_width=stroke, stroke_fill=SHADOW)
    d.text((pad - l, pad - t), text, font=font, fill=CREAM, stroke_width=stroke, stroke_fill=SHADOW)
    return im


def ribbon(scale, width):
    """'DISPATCH' in white on a red banner with folded tails, tilted slightly upwards to the right."""
    h = 150 * scale
    tail = 70 * scale
    im = Image.new("RGBA", (width + tail * 2, h + 60 * scale), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    o = 9 * scale
    # Folded tails behind the banner.
    for side in (-1, 1):
        if side < 0:
            pts = [(0, 40 * scale), (tail + 40 * scale, 40 * scale), (tail + 40 * scale, h + 40 * scale), (0, h + 40 * scale), (tail * 0.45, h * 0.5 + 40 * scale)]
        else:
            X = width + tail * 2
            pts = [(X, 40 * scale), (X - tail - 40 * scale, 40 * scale), (X - tail - 40 * scale, h + 40 * scale), (X, h + 40 * scale), (X - tail * 0.45, h * 0.5 + 40 * scale)]
        d.polygon(pts, fill=RIBBON_DARK)
        d.line(pts + [pts[0]], fill=INK, width=o, joint="curve")
    # Banner with a shadow lip.
    d.rounded_rectangle([tail, 26 * scale, tail + width, h + 26 * scale], radius=22 * scale, fill=RIBBON_DARK, outline=INK, width=o)
    d.rounded_rectangle([tail, 8 * scale, tail + width, h + 8 * scale], radius=22 * scale, fill=RIBBON, outline=INK, width=o)
    font = ImageFont.truetype(FONT, 108 * scale)
    text = "DISPATCH"
    l, t, r, b = d.textbbox((0, 0), text, font=font)
    tx = tail + (width - (r - l)) / 2 - l
    ty = 8 * scale + (h - (b - t)) / 2 - t
    d.text((tx, ty + 5 * scale), text, font=font, fill=RIBBON_DARK)
    d.text((tx, ty), text, font=font, fill=(255, 255, 255))
    return im.rotate(1.8, resample=Image.BICUBIC, expand=True)


def build():
    k = S   # supersampling for the whole logo
    canvas = Image.new("RGBA", (W * k, H * k), (0, 0, 0, 0))
    # Emblem at the top, centred.
    em = emblem()
    ew = 660 * k
    eh = int(em.height * ew / em.width)
    if eh > 410 * k:
        eh = 410 * k
        ew = int(em.width * eh / em.height)
    em = premul_resize(em, (ew, eh))
    canvas.alpha_composite(em, ((W * k - ew) // 2, 10 * k))
    # Ribbon first (it sits behind the wordmark's shadow), then the wordmark overlapping the emblem's water slightly.
    rb = ribbon(k, 680 * k)
    canvas.alpha_composite(rb, ((W * k - rb.width) // 2, 580 * k))
    wm = wordmark(k)
    wm_w = 960 * k
    wm = premul_resize(wm, (wm_w, int(wm.height * wm_w / wm.width)))
    canvas.alpha_composite(wm, ((W * k - wm_w) // 2, 380 * k))
    return premul_resize(canvas, (W, H))


if __name__ == "__main__":
    logo = build()
    logo.save(sys.argv[1])
    if len(sys.argv) > 2:
        bg = Image.new("RGBA", (W, H), (42, 27, 51, 255))
        bg.alpha_composite(logo)
        bg.convert("RGB").save(sys.argv[2])
    print("saved", sys.argv[1])
