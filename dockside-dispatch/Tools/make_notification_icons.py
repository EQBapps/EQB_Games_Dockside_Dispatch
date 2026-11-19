"""
Android notification icons for Dockside Dispatch.
- Small (status bar): a white silhouette on transparency, as Android requires: a shipping container hanging from a crane hook.
- Large: the app icon at 256 px.
Usage: python Tools/make_notification_icons.py   (run from the dockside-dispatch folder)
"""
import os

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BRANDING = os.path.join(ROOT, "DocksideDispatch", "Assets", "_Game", "Branding")
SS = 4          # supersampling
N = 256


def small_icon():
    n = N * SS
    im = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(im)
    s = lambda v: int(v * SS)
    white = 255
    airgap = 18
    # Hook: cable from the top, a ring and two slings down to the container's top corners.
    d.rectangle([s(122), s(6), s(134), s(52)], fill=white)
    d.ellipse([s(112), s(46), s(144), s(78)], fill=white)
    d.ellipse([s(120), s(54), s(136), s(70)], fill=0)
    d.line([(s(122), s(72)), (s(60), s(112))], fill=white, width=s(10))
    d.line([(s(134), s(72)), (s(196), s(112))], fill=white, width=s(10))
    # Container: a solid box with four corrugation slots cut out (few and wide, so they still read at 24 px).
    x0, y0, x1, y1 = 22, 108, 234, 236
    d.rounded_rectangle([s(x0), s(y0), s(x1), s(y1)], radius=s(14), fill=white)
    for i in range(4):
        cx = 53 + i * 44
        d.rounded_rectangle([s(cx), s(130), s(cx + airgap), s(214)], radius=s(9), fill=0)
    alpha = im.resize((N, N), Image.LANCZOS)
    out = Image.new("RGBA", (N, N), (255, 255, 255, 0))
    out.putalpha(alpha)
    return out


def main():
    small = small_icon()
    small.save(os.path.join(BRANDING, "Notification_Small.png"))
    large = Image.open(os.path.join(BRANDING, "DocksideDispatch_Icon.png")).convert("RGB").resize((N, N), Image.LANCZOS)
    large.save(os.path.join(BRANDING, "Notification_Large.png"))
    print("wrote Notification_Small.png and Notification_Large.png")


if __name__ == "__main__":
    main()
