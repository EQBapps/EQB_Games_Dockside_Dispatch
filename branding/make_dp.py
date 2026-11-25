"""EQB Games display picture (profile photo) without the ribbon: the EQB sticker and GAMES lettering,
cut out of eqb-games-logo-transparent.png, on the studio's night indigo with a few of the logo's confetti dots.

    python branding/make_dp.py      ->  branding/eqb-games-dp-1080.png (+ -400, and a light variant)

Square, with everything inside the centre circle, because Instagram, WhatsApp, YouTube, X and LinkedIn
crop profile pictures to a circle.
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "eqb-games-logo-transparent.png")


def wordmark():
    im = Image.open(SRC).convert("RGBA")
    a = np.array(im).astype(int)
    r, g, b, al = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    h, w = al.shape
    # 1) The sticker: its white outline is the largest white blob; filling it gives the whole sticker.
    white = (r > 225) & (g > 225) & (b > 225) & (al > 200)
    white[: h // 2] = False                                   # the wordmark is in the lower half
    lab, n = ndimage.label(white)
    sizes = ndimage.sum(white, lab, range(1, n + 1))
    ring = lab == (1 + int(np.argmax(sizes)))
    sticker = ndimage.binary_fill_holes(ring)
    ys, xs = np.nonzero(sticker)
    x0, x1, y1 = xs.min(), xs.max(), ys.max()
    # 2) GAMES: the teal letters below the sticker.
    teal = (g > 150) & (b > 140) & (r < 150) & (al > 60)
    teal[: y1] = False
    teal[:, : x0] = False
    games = ndimage.binary_dilation(teal, iterations=3)
    # The source's own shadows (a dark fringe round GAMES, grey marks under the sticker) are left out:
    # around the letters only teal-coloured pixels stay, fading with how teal they are. make() adds a clean shadow.
    teal_alpha = np.clip((g - 120) / 70.0, 0, 1) * np.clip((g - r - 40) / 60.0, 0, 1)   # teal, not grey haze
    out = a.copy()
    out[..., 3] = np.where(ndimage.binary_dilation(sticker, iterations=1), al,
                           np.where(games, (al * teal_alpha).astype(int), 0))
    cut = Image.fromarray(out.astype(np.uint8), "RGBA")
    return cut.crop(cut.getbbox())


def background(size, top, bottom, light=False):
    t = np.linspace(0, 1, size)[:, None, None]
    a, b = np.array(top, float), np.array(bottom, float)
    img = (a * (1 - t) + b * t).repeat(size, axis=1)
    yy, xx = np.mgrid[0:size, 0:size] / size
    pink = np.exp(-(((xx - 0.22) / 0.42) ** 2 + ((yy - 0.18) / 0.42) ** 2))[..., None] * np.array([236, 74, 160]) * (0.10 if light else 0.32)
    tide = np.exp(-(((xx - 0.82) / 0.40) ** 2 + ((yy - 0.86) / 0.40) ** 2))[..., None] * np.array([63, 216, 202]) * (0.10 if light else 0.26)
    return Image.fromarray(np.clip(img + pink + tide, 0, 255).astype(np.uint8), "RGB").convert("RGBA")


def confetti(img, size):
    """A few of the logo's dots and sparkles around the lettering, kept inside the circle."""
    d = ImageDraw.Draw(img)
    s = size / 1080
    dots = [(250, 300, 16, (255, 190, 80)), (830, 280, 13, (150, 110, 255)), (880, 380, 10, (63, 216, 202)),
            (205, 760, 14, (63, 216, 202)), (300, 830, 9, (255, 110, 150)), (860, 770, 11, (255, 190, 80))]
    for x, y, r, c in dots:
        d.ellipse(((x - r) * s, (y - r) * s, (x + r) * s, (y + r) * s), fill=c + (235,))
    for x, y, r, c in [(330, 225, 26, (255, 255, 255)), (770, 860, 20, (255, 214, 120)), (790, 205, 16, (255, 150, 210))]:
        x, y, r = x * s, y * s, r * s
        pts = [(x, y - r), (x + r * 0.22, y - r * 0.22), (x + r, y), (x + r * 0.22, y + r * 0.22),
               (x, y + r), (x - r * 0.22, y + r * 0.22), (x - r, y), (x - r * 0.22, y - r * 0.22)]
        d.polygon(pts, fill=c + (240,))


def make(size, mark, light=False):
    bg = background(size, (250, 246, 255) if light else (40, 26, 96), (236, 228, 250) if light else (20, 12, 58), light)
    confetti(bg, size)
    mw = int(size * 0.64)
    m = mark.resize((mw, round(mw * mark.height / mark.width)), Image.LANCZOS)
    x, y = (size - m.width) // 2, (size - m.height) // 2
    # a soft shadow so the sticker lifts off the background
    sh = Image.new("RGBA", m.size, (10, 4, 30, 0))
    sh.putalpha(m.split()[3].point(lambda v: int(v * (0.25 if light else 0.5))))
    sh = sh.filter(ImageFilter.GaussianBlur(size / 90))
    bg.alpha_composite(sh, (x, y + int(size / 70)))
    bg.alpha_composite(m, (x, y))
    return bg.convert("RGB")


if __name__ == "__main__":
    mark = wordmark()
    mark.save(os.path.join(HERE, "eqb-games-wordmark-transparent.png"))
    for size in (1080, 400):
        make(size, mark).save(os.path.join(HERE, f"eqb-games-dp-{size}.png"))
    make(1080, mark, light=True).save(os.path.join(HERE, "eqb-games-dp-light-1080.png"))
    print("wordmark", mark.size)
