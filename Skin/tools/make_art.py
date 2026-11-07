"""Original artwork for the MyTorrents universal skin screens (2026). Run: python Skin/tools/make_art.py"""
import math, os, random
from PIL import Image, ImageDraw, ImageFilter
OUT = os.path.join(os.path.dirname(__file__), "..", "Universal", "Media")
os.makedirs(OUT, exist_ok=True)
random.seed(7)

def background():
    W, H = 1920, 1080
    img = Image.new("RGB", (W, H))
    px = img.load()
    for y in range(H):
        for x in range(0, W):
            t = y / H; u = x / W
            r = int(8 + 10 * u); g = int(18 + 22 * (1 - t) + 10 * u); b = int(34 + 40 * (1 - t))
            px[x, y] = (r, g, b)
    # soft teal glow
    glow = Image.new("L", (W, H), 0); d = ImageDraw.Draw(glow)
    d.ellipse((1100, -300, 2300, 700), fill=120); d.ellipse((-400, 700, 700, 1500), fill=60)
    glow = glow.filter(ImageFilter.GaussianBlur(220))
    img = Image.composite(Image.new("RGB", (W, H), (20, 140, 150)), img, glow.point(lambda v: v * 0.55))
    # faint "swarm" network: nodes + links
    net = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(net)
    nodes = [(random.randint(900, 1900), random.randint(40, 1040)) for _ in range(46)]
    for i, a in enumerate(nodes):
        for b in nodes[i + 1:]:
            dist = math.dist(a, b)
            if dist < 230:
                d.line([a, b], fill=(120, 220, 230, int(55 * (1 - dist / 230))), width=2)
    for (x, y) in nodes:
        r = random.choice([3, 4, 5])
        d.ellipse((x - r, y - r, x + r, y + r), fill=(150, 235, 240, 90))
    img = Image.alpha_composite(img.convert("RGBA"), net.filter(ImageFilter.GaussianBlur(0.6))).convert("RGB")
    img.save(os.path.join(OUT, "mytorrents_universal_bg.jpg"), quality=88)

def flat(name, w, h, rgba, top_line=None):
    im = Image.new("RGBA", (w, h), rgba)
    if top_line:
        ImageDraw.Draw(im).rectangle((0, 0, w, 2), fill=top_line)
    im.save(os.path.join(OUT, name))

def header():
    w, h = 64, 150
    im = Image.new("RGBA", (w, h))
    for y in range(h):
        a = int(200 * (1 - y / h) ** 1.4)
        ImageDraw.Draw(im).line((0, y, w, y), fill=(0, 0, 0, a))
    im.save(os.path.join(OUT, "mytorrents_universal_header.png"))

def icon(size=512):
    S = size * 4  # supersample
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    c = S / 2
    # round tile with teal gradient
    tile = Image.new("RGBA", (S, S)); td = ImageDraw.Draw(tile)
    for y in range(S):
        t = y / S
        td.line((0, y, S, y), fill=(int(20 + 30 * t), int(170 - 70 * t), int(180 - 40 * t), 255))
    mask = Image.new("L", (S, S), 0); ImageDraw.Draw(mask).rounded_rectangle((S*.06, S*.06, S*.94, S*.94), radius=S*.22, fill=255)
    im.paste(tile, (0, 0), mask)
    # peers: small dots linked to the centre
    peers = [(.24, .26), (.76, .24), (.18, .56), (.82, .54)]
    for (x, y) in peers:
        d.line((x*S, y*S, c, S*.40), fill=(255, 255, 255, 110), width=int(S*.018))
    for (x, y) in peers:
        r = S * .045; d.ellipse((x*S-r, y*S-r, x*S+r, y*S+r), fill=(255, 255, 255, 230))
    # big download arrow into a tray
    w = S * .13
    d.rectangle((c - w/2, S*.30, c + w/2, S*.56), fill="white")
    d.polygon([(c - S*.17, S*.53), (c + S*.17, S*.53), (c, S*.71)], fill="white")
    d.rounded_rectangle((S*.24, S*.74, S*.76, S*.80), radius=S*.03, fill=(255, 255, 255, 235))
    im = im.resize((size, size), Image.LANCZOS)
    im.save(os.path.join(OUT, "mytorrents_universal_logo.png"))


def hover_background():
    """hover_mytorrents.png: full-screen 1920x1080 background, as Titan-style skins expect."""
    bg = Image.open(os.path.join(OUT, "mytorrents_universal_bg.jpg")).convert("RGBA")
    logo = Image.open(os.path.join(OUT, "mytorrents_universal_logo.png")).resize((460, 460), Image.LANCZOS)
    faded = logo.copy()
    faded.putalpha(logo.getchannel("A").point(lambda a: int(a * 0.28)))
    bg.alpha_composite(faded, (1300, 330))
    bg.convert("RGB").save(os.path.join(OUT, "hover_mytorrents.png"), optimize=True)

def status(name, rgb):
    S = 192; im = Image.new("RGBA", (S, S)); d = ImageDraw.Draw(im)
    d.ellipse((12, 12, S-12, S-12), fill=rgb + (255,))
    shine = Image.new("RGBA", (S, S)); ImageDraw.Draw(shine).ellipse((44, 26, S-44, S*0.48), fill=(255, 255, 255, 80))
    im = Image.alpha_composite(im, shine)
    im.resize((48, 48), Image.LANCZOS).save(os.path.join(OUT, name))

background()
flat("mytorrents_universal_panel.png", 8, 1080, (6, 12, 22, 200), top_line=(90, 200, 210, 120))
flat("mytorrents_universal_panel2.png", 8, 1080, (4, 9, 16, 236), top_line=(90, 200, 210, 160))
flat("mytorrents_universal_bar.png", 8, 1080, (0, 0, 0, 170), top_line=(90, 200, 210, 90))
header()
icon()
hover_background()
status("status_green.png", (40, 190, 90))
status("status_red.png", (210, 60, 50))
print("art written to", os.path.abspath(OUT))


def installer_art():
    """Images for the MediaPortal Extension Installer pages (left panel 168x306, header 64x64)."""
    from PIL import ImageFont
    out = os.path.join(os.path.dirname(__file__), "..", "..", "Installer", "art")
    os.makedirs(out, exist_ok=True)
    bg = Image.open(os.path.join(OUT, "mytorrents_universal_bg.jpg")).convert("RGBA")
    W, H = 336, 612
    left = bg.crop((1280, 200, 1280 + 400, 200 + 728)).resize((W, H))
    ic = Image.open(os.path.join(OUT, "mytorrents_universal_logo.png")).resize((200, 200), Image.LANCZOS)
    left.paste(ic, ((W - 200) // 2, 150), ic)
    d = ImageDraw.Draw(left)
    f1 = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 40)
    f2 = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 24)
    d.text((W / 2, 390), "MyTorrents", font=f1, fill="white", anchor="mm")
    d.text((W / 2, 436), "for MediaPortal", font=f2, fill=(170, 215, 220, 255), anchor="mm")
    left.convert("RGB").save(os.path.join(out, "installer_left.png"))
    Image.open(os.path.join(OUT, "mytorrents_universal_logo.png")).resize((128, 128), Image.LANCZOS).save(os.path.join(out, "installer_icon.png"))


installer_art()
