"""
Rough preview of MediaPortal skin screens (no MediaPortal needed).

  python Skin/tools/preview.py <screen.xml> <out.png> [--res 1080|720] [--set "#Prop=value" ...] [--focus ID]

Draws images, labels, buttons, lists and checklists at their XML positions. Buttons and lists get a
generic look - in MediaPortal the skin's own references.xml decides how they really look.
"""
import argparse, os, re, sys, textwrap
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
MEDIA = os.path.join(HERE, "..", "Universal", "Media")
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
FONTB = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
STRINGS = {"14002": "Views", "230": "Filters", "96": "Sort by:", "137": "Search", "186": "OK", "222": "Cancel"}
FONT_PX = {"font10": 24, "font12": 29, "font14": 38}

SAMPLE_TORRENTS = [
    ("The Expanse S06E01 1080p WEB", "2.1 GB (100.00%)"),
    ("Ubuntu 26.04 Desktop amd64", "6.2 GB (62.40%)"),
    ("Debian 13 netinst", "754 MB (100.00%)"),
    ("Big Buck Bunny 4K", "1.1 GB (8.15%)"),
    ("Blender Open Movie - Sintel", "1.6 GB (100.00%)"),
    ("LibreOffice 26.2 Windows x64", "350 MB (41.00%)"),
    ("Fedora Workstation 44", "2.3 GB (100.00%)"),
    ("Linux Mint 23 Cinnamon", "2.9 GB (99.20%)"),
]
SAMPLE_FILES = [("Ubuntu 26.04 Desktop amd64.iso", "6.2 GB"), ("SHA256SUMS", "1 KB"), ("README.txt", "2 KB")]
SAMPLE_CHECK = [("Jackett (all indexers)", True), ("EZTV", True), ("YTS", False), ("My site: Example", True)]


def parse(path):
    txt = open(path, encoding="utf-8-sig").read()
    txt = re.sub(r"<!--Checksum.*", "", txt)
    return ET.fromstring(txt)


def controls(path):
    """Controls in draw order, following <import>s."""
    root = parse(path)
    out = []
    for el in root.find("controls"):
        if el.tag == "import":
            p = os.path.join(os.path.dirname(path), el.text.strip())
            if os.path.exists(p):
                out += controls(p)
        elif el.tag == "control":
            out.append(el)
    return out


def visible(expr, props, visible_ids):
    if not expr:
        return True
    expr = expr.strip()
    if expr.lower() in ("yes", "true"):
        return True
    for part in expr.split("+"):
        part = part.strip(); neg = part.startswith("!")
        if neg:
            part = part[1:]
        m = re.match(r"string\.equals\(([^,]*),([^)]*)\)", part, re.I)
        if m:
            a = sub(m.group(1).strip(), props); b = sub(m.group(2).strip(), props)
            r = a.lower() == b.lower()
        elif part.lower().startswith("player.hasmedia"):
            r = False
        elif part.lower().startswith("control.isvisible"):
            r = True
        else:
            r = True
        if neg:
            r = not r
        if not r:
            return False
    return True


def sub(text, props):
    text = STRINGS.get(text, text)
    for k in sorted(props, key=len, reverse=True):
        text = text.replace(k, props[k])
    return re.sub(r"#[A-Za-z][\w.]*", "", text) if "#" in text else text


def color(c, default=(255, 255, 255, 255)):
    if not c or len(c) != 8:
        return default
    a, r, g, b = (int(c[i:i + 2], 16) for i in (0, 2, 4, 6))
    return (r, g, b, a)


def render(path, out, res, props, focus):
    W, H = (1920, 1080) if res == "1080" else (1280, 720)
    k = W / 1920
    img = Image.new("RGBA", (W, H), (0, 0, 0, 255))
    font = lambda name, bold=False: ImageFont.truetype(FONTB if bold else FONT, max(10, int(FONT_PX.get(name, 29) * k)))
    for c in controls(path):
        g = lambda t, d="": (c.findtext(t) or d).strip()
        if not visible(g("visible"), props, None):
            continue
        t = g("type"); x = int(g("posX", "0") or 0); y = int(g("posY", "0") or 0)
        w = int(g("width", "0") or 0); h = int(g("height", "0") or 0)
        cid = g("id", "0")
        layer = Image.new("RGBA", (W, H)); d = ImageDraw.Draw(layer)
        if t == "image":
            tex = sub(g("texture"), props)
            p = os.path.join(MEDIA, tex)
            if tex.startswith("banner:"):
                d.rectangle((x, y, x + w, y + h), fill=(60, 70, 90, 255))
                d.text((x + w / 2, y + h / 2), tex[7:], font=font("font14", True), fill="white", anchor="mm")
            elif os.path.exists(p) and w and h:
                im = Image.open(p).convert("RGBA").resize((w, h))
                layer.paste(im, (x, y), im)
        elif t in ("label", "fadelabel"):
            txt = sub(g("label"), props)
            f = font(g("font", "font12"))
            anchor = {"left": "la", "right": "ra", "center": "ma"}.get(g("align", "left"), "la")
            if w and anchor == "la":
                while txt and d.textlength(txt, font=f) > w:
                    txt = txt[:-2] + "…" if not txt.endswith("…") else txt[:-2] + "…"
            d.text((x, y), txt, font=f, fill=color(g("textcolor")), anchor=anchor)
        elif t in ("button", "checkbutton"):
            foc = cid == focus
            d.rounded_rectangle((x, y, x + w, y + h), radius=int(8 * k), fill=(40, 170, 180, 235) if foc else (255, 255, 255, 22),
                                outline=(120, 220, 228, 200) if foc else (255, 255, 255, 40), width=max(1, int(2 * k)))
            txt = sub(g("label"), props)
            tx = x + int(22 * k)
            if t == "checkbutton":
                s = int(26 * k); cy = y + h // 2
                d.rectangle((x + w - s - int(20 * k), cy - s // 2, x + w - int(20 * k), cy + s // 2), outline="white", width=max(1, int(2 * k)))
                if props.get("#MyTorrents.SeriesInfo.Specials", "").lower() in ("yes", "true"):
                    d.line((x + w - s - int(16 * k), cy, x + w - int(20 * k) - s // 2, cy + s // 3, x + w - int(22 * k), cy - s // 3), fill="white", width=max(2, int(4 * k)))
            d.text((tx, y + h / 2), txt, font=font("font12"), fill="white", anchor="lm")
        elif t in ("listcontrol", "checklistcontrol"):
            rows = {"208": SAMPLE_TORRENTS, "502": SAMPLE_FILES}.get(cid, SAMPLE_CHECK if t == "checklistcontrol" else SAMPLE_TORRENTS)
            rh = int(64 * k); f = font("font12")
            x1 = int(g("textXOff", "20") or 20); x2 = int(g("textXOff2", str(w - 20)) or (w - 20))
            for i, row in enumerate(rows):
                ry = y + i * rh
                if ry + rh > y + h:
                    break
                foc = (i == 1 and focus == cid)
                if foc:
                    d.rounded_rectangle((x, ry + 3, x + w, ry + rh - 3), radius=int(8 * k), fill=(40, 170, 180, 220))
                else:
                    d.line((x + 10, ry + rh - 1, x + w - 10, ry + rh - 1), fill=(255, 255, 255, 25))
                if t == "checklistcontrol":
                    s = int(28 * k); cy = ry + rh // 2
                    d.rectangle((x + x1, cy - s // 2, x + x1 + s, cy + s // 2), outline="white", width=max(1, int(2 * k)))
                    if row[1]:
                        d.line((x + x1 + 5 * k, cy, x + x1 + s * .45, cy + s * .3, x + x1 + s - 4 * k, cy - s * .35), fill="white", width=max(2, int(4 * k)))
                    d.text((x + x1 + s + 18 * k, cy), row[0], font=f, fill="white", anchor="lm")
                else:
                    if cid == "208":  # status dot like MP's play/pause icons
                        col = (60, 200, 110) if "100.00" in row[1] else (240, 190, 60)
                        r = int(9 * k); cy = ry + rh // 2
                        d.ellipse((x + x1, cy - r, x + x1 + 2 * r, cy + r), fill=col + (255,))
                        lx = x + x1 + 2 * r + int(18 * k)
                    else:
                        lx = x + x1
                    d.text((lx, ry + rh / 2), row[0], font=f, fill="white", anchor="lm")
                    d.text((x + x2, ry + rh / 2), row[1], font=font("font10"), fill=(200, 225, 230, 255), anchor="rm")
        elif t.startswith("textbox"):
            txt = sub(g("label"), props); f = font(g("font", "font10"))
            lines = []
            for para in txt.split("\n"):
                lines += textwrap.wrap(para, width=max(10, int(w / (f.size * 0.55))))
            for i, line in enumerate(lines):
                if (i + 1) * f.size * 1.35 > h:
                    break
                d.text((x, y + i * f.size * 1.35), line, font=f, fill=(225, 235, 238, 255))
        img = Image.alpha_composite(img, layer)
    img.convert("RGB").save(out)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("screen"); ap.add_argument("out")
    ap.add_argument("--res", default="1080"); ap.add_argument("--focus", default="208")
    ap.add_argument("--set", action="append", default=[])
    a = ap.parse_args()
    props = {"#MyTorrents.FolderBrowser.Visible": "no", "#MyTorrents.TorrentDetails.Visible": "no", "#MyTorrents.SeriesInfo": "no",
             "#MyTorrents.IsConnected": "true", "#MyTorrents.Count": "8", "#MyTorrents.CombinedDownloadSpeed": "4.2 MB/s",
             "#MyTorrents.CombinedUploadSpeed": "512 KB/s", "#MyTorrents.Downloads.Count": "4", "#MyTorrents.Uploads.Count": "4",
             "#time": "7:42 PM", "#date": "Tuesday 20 October", "#selecteditem": "D:\\Downloads\\TV"}
    for s in a.set:
        key, val = s.split("=", 1); props[key] = val
    render(a.screen, a.out, a.res, props, a.focus)
    print("preview:", a.out)
