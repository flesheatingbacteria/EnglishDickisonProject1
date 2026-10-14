"""Sprites for the Dickinson Flappy Bird levels. Needs Pillow.

Level 1 - "Hope" is the thing with feathers   (dawn)
Level 2 - The Gale                            (storm)
Level 3 - Extremity                           (frozen night sea)

Run from the repo root:  python Art/make_sprites.py
Writes the full art set to Art/sprites/, preview screenshots + GIFs to
Art/previews/, and the pieces the game loads to
Assets/Resources/DickinsonSprites/ (see PixelArt.cs).
"""
import math
import os
import random

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SPRITES = os.path.join(HERE, "sprites")
PREVIEWS = os.path.join(HERE, "previews")
GAME = os.path.join(HERE, "..", "Assets", "Resources", "DickinsonSprites")

W, H = 144, 256
GROUND_Y = 200
BLACK = (34, 34, 34, 255)
CLEAR = (0, 0, 0, 0)


def c(h):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


def draw_map(rows, pal):
    img = Image.new("RGBA", (max(len(r) for r in rows), len(rows)), CLEAR)
    px = img.load()
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in pal:
                px[x, y] = pal[ch]
    return img


def outline(img):
    px = img.load()
    w, h = img.size
    pts = [(x, y) for x in range(w) for y in range(h) if px[x, y][3] and px[x, y] != BLACK]
    for x, y in pts:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if 0 <= x + dx < w and 0 <= y + dy < h and px[x + dx, y + dy][3] == 0:
                px[x + dx, y + dy] = BLACK


def box(px, x0, y0, x1, y1, col, w=W, h=H):
    for y in range(max(0, y0), min(h, y1)):
        for x in range(max(0, x0), min(w, x1)):
            px[x, y] = col


def circle(px, cx, cy, r, col, w=W, h=H):
    for y in range(cy - r, cy + r + 1):
        for x in range(cx - r, cx + r + 1):
            if 0 <= x < w and 0 <= y < h and (x - cx) ** 2 + (y - cy) ** 2 <= r * r + r:
                px[x, y] = col


def paste(dst, src, x, y):
    dst.alpha_composite(src, (int(x), int(y)))


def big(img, k):
    return img.resize((img.width * k, img.height * k), Image.NEAREST)


# ------------------------------------------------------------------ bird
BIRD = [
    ".....KKKK......",
    "...KKBBBBKK....",
    "..KBBBBBWWWK...",
    ".KBBBBBBWWkWK..",
    ".KBBBBBBWWkWKK.",
    "KBBBBBBBBBBKOOK",
    "KBBBBBBBBBKOOK.",
    "KBBBBBBLLLLKK..",
    ".KKKBLLLLLLK...",
    "...KKLLLLLKK...",
    "....KKKKKK.....",
]
WING = {
    "up": (0, 1, ["KKK..", "KDDK.", "KDDDK", "KDDDD"]),
    "mid": (0, 5, ["KDDDD", "KDDDDD", "KDDDD"]),
    "down": (0, 6, ["KDDDD", "KDDDD", "KDDDK", ".KDK.", "..K.."]),
}
BIRD_PAL = {"K": BLACK, "B": c("#9b6a3c"), "L": c("#e8a65a"), "W": c("#ffffff"),
            "k": BLACK, "O": c("#f5b335"), "D": c("#6e4826")}


def make_bird(frame, hurt=False):
    rows = [list(r) for r in BIRD]
    ox, oy, wing = WING[frame]
    for dy, r in enumerate(wing):
        for dx, ch in enumerate(r):
            if ch != ".":
                rows[oy + dy][ox + dx] = ch
    if hurt:
        rows[3][10] = "W"
        rows[4][10] = "W"
        rows[3][8:11] = list("kkk")
    return draw_map(["".join(r) for r in rows], BIRD_PAL)


# ------------------------------------------------------------------ text
FONT = {
    "A": [".#.", "#.#", "###", "#.#", "#.#"], "B": ["##.", "#.#", "##.", "#.#", "##."],
    "C": [".##", "#..", "#..", "#..", ".##"], "D": ["##.", "#.#", "#.#", "#.#", "##."],
    "E": ["###", "#..", "##.", "#..", "###"], "F": ["###", "#..", "##.", "#..", "#.."],
    "G": [".##", "#..", "#.#", "#.#", ".##"], "H": ["#.#", "#.#", "###", "#.#", "#.#"],
    "I": ["###", ".#.", ".#.", ".#.", "###"], "K": ["#.#", "#.#", "##.", "#.#", "#.#"],
    "L": ["#..", "#..", "#..", "#..", "###"], "M": ["#...#", "##.##", "#.#.#", "#...#", "#...#"],
    "N": ["#..#", "##.#", "#.##", "#..#", "#..#"], "O": [".#.", "#.#", "#.#", "#.#", ".#."],
    "P": ["##.", "#.#", "##.", "#..", "#.."], "R": ["##.", "#.#", "##.", "#.#", "#.#"],
    "S": [".##", "#..", ".#.", "..#", "##."], "T": ["###", ".#.", ".#.", ".#.", ".#."],
    "U": ["#.#", "#.#", "#.#", "#.#", "###"], "V": ["#.#", "#.#", "#.#", "#.#", ".#."],
    "W": ["#...#", "#...#", "#.#.#", "##.##", "#...#"], "X": ["#.#", "#.#", ".#.", "#.#", "#.#"],
    "Y": ["#.#", "#.#", ".#.", ".#.", ".#."],
    "0": ["###", "#.#", "#.#", "#.#", "###"], "1": [".#.", "##.", ".#.", ".#.", "###"],
    "2": ["##.", "..#", ".#.", "#..", "###"], "3": ["##.", "..#", ".#.", "..#", "##."],
    "4": ["#.#", "#.#", "###", "..#", "..#"], "5": ["###", "#..", "##.", "..#", "##."],
    "6": [".##", "#..", "###", "#.#", "###"], "7": ["###", "..#", ".#.", ".#.", ".#."],
    "8": ["###", "#.#", "###", "#.#", "###"], "9": ["###", "#.#", "###", "..#", "##."],
    "-": ["...", "...", "###", "...", "..."], ",": [".", ".", ".", "#", "#"],
    ".": [".", ".", ".", ".", "#"], "'": ["#", "#", ".", ".", "."], '"': ["#.#", "#.#", "...", "...", "..."],
    " ": ["..", "..", "..", "..", ".."],
}


def text(s, col="#ffffff", scale=1, edge=True):
    glyphs = [FONT[ch] for ch in s]
    w = sum((len(g[0]) + 1) * scale for g in glyphs) + 2
    img = Image.new("RGBA", (w, 5 * scale + 2), CLEAR)
    px = img.load()
    x0 = 1
    for g in glyphs:
        for gy, row in enumerate(g):
            for gx, bit in enumerate(row):
                if bit == "#":
                    for sy in range(scale):
                        for sx in range(scale):
                            px[x0 + gx * scale + sx, 1 + gy * scale + sy] = c(col)
        x0 += (len(g[0]) + 1) * scale
    if edge:
        outline(img)
    return img


def textbox(lines, col="#ffffff", hi=None, hi_col="#ffd84a"):
    parts = [text(l, hi_col if i == hi else col, edge=False) for i, l in enumerate(lines)]
    w = max(p.width for p in parts) + 8
    h = len(parts) * 8 + 6
    img = Image.new("RGBA", (w, h), BLACK)
    px = img.load()
    box(px, 1, 1, w - 1, h - 1, c("#ffffff"), w, h)
    box(px, 2, 2, w - 2, h - 2, c("#3a3a4a"), w, h)
    for i, p in enumerate(parts):
        paste(img, p, (w - p.width) // 2, 4 + i * 8)
    return img


# ------------------------------------------------------------------ pipes
def make_pipe(length, top, body, body_hi, cap, cap_hi, extra=None):
    bw, cw, ch = 22, 26, 8
    img = Image.new("RGBA", (cw + 2, length + 8), CLEAR)
    px = img.load()
    cap_y = length - ch if top else 0
    b0, b1 = (0, cap_y) if top else (ch, length)
    box(px, 3, b0, 3 + bw, b1, c(body), cw + 2, length + 8)
    box(px, 5, b0, 8, b1, c(body_hi), cw + 2, length + 8)
    box(px, 1, cap_y, 1 + cw, cap_y + ch, c(cap), cw + 2, length + 8)
    box(px, 3, cap_y + 1, 6, cap_y + ch - 1, c(cap_hi), cw + 2, length + 8)
    if extra:
        extra(px, cap_y, b0, b1)
    if not top:
        padded = Image.new("RGBA", img.size, CLEAR)
        padded.paste(img.crop((0, 0, img.width, img.height - 1)), (0, 1))
        img = padded
    outline(img)
    return img


# In-game pipes are 9-slice tiled sprites (see PixelArt.cs): the plain body
# repeats, the cap end stays fixed. Both pieces keep exactly BODY_TILE rows of body.
BODY_TILE = 18
TOP_OVERHANG = 5  # rows of drips/spikes kept below the top pipe's gap edge


def game_pipes(fn):
    top = fn(80, True)  # cap occupies rows 72..79, gap edge outline at row 80
    top = top.crop((0, 72 - BODY_TILE, top.width, 81 + TOP_OVERHANG))
    bottom = fn(80, False)  # outline row 0, cap rows 1..8
    bottom = bottom.crop((0, 0, bottom.width, 9 + BODY_TILE))
    return top, bottom


# level 1: green vine w/ purple flowers on top, birch tree on bottom
def l1_pipe(length, top):
    if top:
        def drips(px, cy, b0, b1):
            for x, n in ((6, 4), (12, 2), (19, 5)):
                box(px, x, cy + 8, x + 2, cy + 8 + n, c("#a77fd8"), 28, length + 8)
        return make_pipe(length, True, "#4fa040", "#78c860", "#a77fd8", "#c9a8f0", drips)

    def marks(px, cy, b0, b1):
        rnd = random.Random(length)
        for y in range(b0 + 5, b1, 9):
            x = rnd.choice((6, 10, 14))
            box(px, x, y, x + 5, y + 1, BLACK, 28, length + 8)
    return make_pipe(length, False, "#eeeeee", "#ffffff", "#5cb84a", "#8ad870", marks)


# level 2: grey storm cloud on top, rock with white foam on bottom
def l2_pipe(length, top):
    if top:
        def bumps(px, cy, b0, b1):
            for x in range(1, 27, 6):
                box(px, x, cy + 8, x + 4, cy + 10, c("#9aa1b0"), 28, length + 8)
        return make_pipe(length, True, "#7d8597", "#9aa1b0", "#9aa1b0", "#c0c6d2", bumps)
    return make_pipe(length, False, "#4a4f5c", "#62687a", "#f0f4f8", "#ffffff")


# level 3: icicle hanging on top, ice pillar w/ snow on bottom
def l3_pipe(length, top):
    if top:
        def spikes(px, cy, b0, b1):
            for x0 in (2, 10, 18):
                for i in range(4):
                    box(px, x0 + i, cy + 8 + i, x0 + 7 - i, cy + 9 + i, c("#c8ecff"), 28, length + 8)
        return make_pipe(length, True, "#a8d8f0", "#d8f0ff", "#c8ecff", "#ffffff", spikes)

    def snow(px, cy, b0, b1):
        for x in range(2, 26, 7):
            box(px, x, 0, x + 4, 2, c("#ffffff"), 28, length + 8)

    img = make_pipe(length + 2, False, "#7fc0e0", "#a8d8f0", "#ffffff", "#ffffff", snow)
    return img


# ------------------------------------------------------------------ backgrounds
def cloud(px, x, y, col):
    box(px, x + 4, y, x + 14, y + 4, col)
    box(px, x, y + 3, x + 22, y + 9, col)
    box(px, x + 12, y - 2, x + 19, y + 4, col)


def house(px, x, y, wall, roof, win):
    box(px, x, y, x + 16, y + 12, wall)
    for i in range(7):
        box(px, x - 1 + i, y - 1 - i, x + 17 - i, y - i, roof)
    box(px, x + 12, y - 9, x + 15, y - 3, roof)
    box(px, x + 3, y + 3, x + 6, y + 6, win)
    box(px, x + 10, y + 3, x + 13, y + 6, win)
    box(px, x + 7, y + 6, x + 10, y + 12, c("#4a2a1a"))


def l1_background():
    img = Image.new("RGBA", (W, H), c("#c7b3ea"))
    px = img.load()
    circle(px, 108, 60, 14, c("#ffe680"))
    cloud(px, 10, 40, c("#ffffff"))
    cloud(px, 70, 90, c("#ffffff"))
    cloud(px, 112, 130, c("#ffffff"))
    circle(px, 30, 215, 50, c("#6cbf5a"))
    circle(px, 120, 225, 55, c("#5aa84a"))
    house(px, 14, 166, c("#a0603a"), c("#c0392b"), c("#ffe680"))
    for i in range(4):  # smoke
        circle(px, 28 + i, 150 - i * 7, 2, c("#eeeeee"))
    return img


def l2_background(flash=False):
    img = Image.new("RGBA", (W, H), c("#4a5270" if flash else "#2d3348"))
    px = img.load()
    for x, y in ((-6, 6), (30, 0), (70, 10), (108, 2)):
        cloud(px, x, y, c("#555d72"))
    rnd = random.Random(2)
    for _ in range(40):
        x, y = rnd.randrange(W), rnd.randrange(30, 180)
        for i in range(4):
            if 0 <= x - i // 2 < W:
                px[x - i // 2, y + i] = c("#56607e")
    pts = [(100, 20), (92, 46), (102, 46), (90, 80)]
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        n = max(abs(x1 - x0), abs(y1 - y0))
        for t in range(n + 1):
            box(px, round(x0 + (x1 - x0) * t / n), round(y0 + (y1 - y0) * t / n),
                round(x0 + (x1 - x0) * t / n) + 2, round(y0 + (y1 - y0) * t / n) + 1, c("#ffe866"))
    box(px, 0, 176, W, GROUND_Y, c("#233048"))
    for x in range(0, W, 12):
        box(px, x + 2, 174, x + 8, 177, c("#dfe8f2"))
    box(px, 0, 150, 40, GROUND_Y, c("#1a1d28"))
    house(px, 10, 138, c("#1a1d28"), c("#1a1d28"), c("#ffd860"))
    for i in range(4):  # smoke blown sideways
        circle(px, 20 - i * 6, 126 - i, 2, c("#555d72"))
    return img


def l3_background():
    img = Image.new("RGBA", (W, H), c("#10183a"))
    px = img.load()
    rnd = random.Random(3)
    for _ in range(45):
        px[rnd.randrange(W), rnd.randrange(150)] = c("#ffffff")
    for band, col, y0 in ((0, "#3fe0a0", 50), (1, "#2aa0c0", 60)):  # aurora
        for x in range(W):
            y = y0 + int(math.sin(x / 14 + band) * 8)
            box(px, x, y, x + 1, y + 4, c(col))
    circle(px, 116, 104, 11, c("#f4f4ff"))
    circle(px, 112, 101, 2, c("#c8c8dc"))
    circle(px, 119, 108, 3, c("#c8c8dc"))
    box(px, 0, 168, W, GROUND_Y, c("#1c3a6a"))
    for x0, w0, h0 in ((8, 26, 22), (70, 18, 14), (104, 34, 30)):  # icebergs
        for i in range(h0):
            half = w0 // 2 * (i + 1) // h0
            cx = x0 + w0 // 2
            box(px, cx - half, 168 - h0 + i, cx, 169 - h0 + i, c("#e6f4ff"))
            box(px, cx, 168 - h0 + i, cx + half, 169 - h0 + i, c("#a8cce8"))
    for x in range(0, W, 10):
        box(px, x, 178 + (x // 10) % 3 * 6, x + 4, 179 + (x // 10) % 3 * 6, c("#3a6aa0"))
    return img


def ground(top, top_dark, fill, speck, width=W + 24):
    img = Image.new("RGBA", (width, H - GROUND_Y), c(fill))
    px = img.load()
    box(px, 0, 0, width, 1, BLACK, width, H)
    box(px, 0, 1, width, 7, c(top), width, H)
    for x in range(0, width, 8):  # stripes
        box(px, x, 1, x + 4, 7, c(top_dark), width, H)
    box(px, 0, 7, width, 8, BLACK, width, H)
    rnd = random.Random(1)
    for _ in range(30):
        x, y = rnd.randrange(0, width - 2), rnd.randrange(12, H - GROUND_Y - 2)
        x -= x % 24 if x > width - 24 else 0
        box(px, x, y, x + 2, y + 1, c(speck), width, H)
    return img


# ------------------------------------------------------------------ pickups
NOTE = draw_map(["...##", "...#.#", "...#..", ".###..", "####..", ".##..."], {"#": c("#ffd84a")})
FEATHER = draw_map(["....#", "...##", "..##.", ".##..", "#...."], {"#": c("#ffd84a")})
CRUMB = draw_map([".##.", "#oo#", "#o##", ".##."], {"#": c("#c88a3a"), "o": c("#f0c070")})


def with_outline(p):
    out = Image.new("RGBA", (p.width + 2, p.height + 2), CLEAR)
    paste(out, p, 1, 1)
    outline(out)
    return out


NOTE, FEATHER, CRUMB = with_outline(NOTE), with_outline(FEATHER), with_outline(CRUMB)


# ------------------------------------------------------------------ levels
LEVELS = [
    dict(
        folder="level1_hope", n=1, title="HOPE", title_col="#ffb84a",
        bg=l1_background, ground=lambda: ground("#8ccf5a", "#74b84a", "#c89b6a", "#a87a4a"),
        pipe=l1_pipe, pickup=NOTE,
        stanza=['"HOPE" IS THE THING', "WITH FEATHERS -", "THAT PERCHES IN THE SOUL -",
                "AND SINGS THE TUNE", "WITHOUT THE WORDS -", "AND NEVER STOPS - AT ALL -"],
        over="NEVER STOPS", over_lines=["- AT ALL -", "TAP TO SING AGAIN"],
    ),
    dict(
        folder="level2_gale", n=2, title="THE GALE", title_col="#cfd8e8",
        bg=l2_background, ground=lambda: ground("#7a8aa8", "#66768f", "#4a4458", "#3a3448"),
        pipe=l2_pipe, pickup=FEATHER, flash=lambda: l2_background(True),
        stanza=["AND SWEETEST - IN THE GALE -", "IS HEARD -", "AND SORE MUST BE THE STORM -",
                "THAT COULD ABASH THE", "LITTLE BIRD", "THAT KEPT SO MANY WARM -"],
        over="ABASHED", over_lines=["AND SORE MUST BE THE STORM -", "THAT COULD ABASH", "THE LITTLE BIRD"],
    ),
    dict(
        folder="level3_extremity", n=3, title="EXTREMITY", title_col="#a8e8ff",
        bg=l3_background, ground=lambda: ground("#f4f8ff", "#d8e8f8", "#9cc4e4", "#7aaad0"),
        pipe=l3_pipe, pickup=CRUMB,
        stanza=["I'VE HEARD IT IN THE", "CHILLEST LAND -", "AND ON THE STRANGEST SEA -",
                "YET - NEVER - IN EXTREMITY,", "IT ASKED A CRUMB - OF ME."],
        over="NOT A CRUMB", over_lines=["YET - NEVER - IN EXTREMITY,", "IT ASKED A CRUMB - OF ME."],
    ),
]


def pipe_pair(fn, x, gap_y, gap=60):
    return [(fn(gap_y, True), x, 0), (fn(GROUND_Y - gap_y - gap, False), x, gap_y + gap)]


def build(level, birds, hurt):
    out = os.path.join(SPRITES, level["folder"])
    prev = os.path.join(PREVIEWS, level["folder"])
    os.makedirs(out, exist_ok=True)
    os.makedirs(prev, exist_ok=True)
    bg, gr, fn = level["bg"](), level["ground"](), level["pipe"]
    title = text(level["title"], level["title_col"], scale=3 if len(level["title"]) < 9 else 2)

    bg.save(f"{out}/background.png")
    gr.save(f"{out}/ground.png")
    fn(160, True).save(f"{out}/pipe_top.png")
    fn(160, False).save(f"{out}/pipe_bottom.png")
    level["pickup"].save(f"{out}/pickup.png")
    game = os.path.join(GAME, level["folder"])
    os.makedirs(game, exist_ok=True)
    bg.save(f"{game}/background.png")
    gr.save(f"{game}/ground.png")
    gtop, gbottom = game_pipes(fn)
    gtop.save(f"{game}/pipe_top.png")
    gbottom.save(f"{game}/pipe_bottom.png")
    title.save(f"{out}/title.png")
    textbox(level["stanza"], hi=len(level["stanza"]) - 1).save(f"{out}/stanza.png")

    # title screen
    t = bg.copy()
    paste(t, gr, 0, GROUND_Y)
    lv = text(f"LEVEL {level['n']}")
    paste(t, lv, (W - lv.width) // 2, 20)
    paste(t, title, (W - title.width) // 2, 30)
    tb = textbox(level["stanza"], hi=len(level["stanza"]) - 1)
    paste(t, tb, (W - tb.width) // 2, 62)
    paste(t, birds["mid"], (W - 15) // 2, 130)
    big(t, 4).save(f"{prev}/title_screen.png")

    # gameplay
    g = bg.copy()
    for img, x, y in pipe_pair(fn, 60, 62) + pipe_pair(fn, 132, 92):
        paste(g, img, x, y)
    paste(g, gr, 0, GROUND_Y)
    paste(g, level["pickup"], 100, 120)
    paste(g, birds["mid"], 26, 104)
    s = text("7", scale=3)
    paste(g, s, (W - s.width) // 2, 14)
    big(g, 4).save(f"{prev}/gameplay.png")

    # game over
    o = bg.copy()
    for img, x, y in pipe_pair(fn, 44, 56) + pipe_pair(fn, 118, 90):
        paste(o, img, x, y)
    paste(o, gr, 0, GROUND_Y)
    ov = text(level["over"], level["title_col"], scale=2)
    paste(o, ov, (W - ov.width) // 2, 64)
    ob = textbox(level["over_lines"], hi=0 if level["n"] == 1 else None)
    paste(o, ob, (W - ob.width) // 2, 82)
    paste(o, hurt, 64, GROUND_Y - hurt.height + 1)
    big(o, 4).save(f"{prev}/game_over.png")

    # gif
    seq = ["up", "mid", "down", "mid"]
    pairs = [pipe_pair(fn, 0, gy) for gy in (60, 94, 74)]
    flash = level["flash"]() if "flash" in level else None
    frames = []
    for i in range(60):
        f = (flash if flash and i in (34, 36) else bg).copy()
        off = i * 2
        for k, pp in enumerate(pairs):
            x = ((150 + k * 72 - off + 60) % 216) - 60
            for img, _, y in pp:
                paste(f, img, x, y)
        paste(f, gr, -(off % 24), GROUND_Y)
        paste(f, birds[seq[(i // 3) % 4]], 26, 100 + int(math.sin(i / 60 * math.tau * 2) * 14))
        s = text(str(3 + i // 20), scale=3)
        paste(f, s, (W - s.width) // 2, 14)
        frames.append(big(f, 3).convert("RGB").quantize(colors=64, dither=Image.NONE))
    frames[0].save(f"{prev}/gameplay.gif", save_all=True, append_images=frames[1:], duration=60, loop=0)


def main():
    birds = {f: make_bird(f) for f in ("up", "mid", "down")}
    hurt = make_bird("down", hurt=True)
    os.makedirs(os.path.join(SPRITES, "bird"), exist_ok=True)
    for f, b in birds.items():
        b.save(os.path.join(SPRITES, "bird", f"bird_{f}.png"))
    hurt.save(os.path.join(SPRITES, "bird", "bird_hurt.png"))
    os.makedirs(os.path.join(GAME, "bird"), exist_ok=True)
    for f, b in list(birds.items()) + [("hurt", hurt)]:
        b.save(os.path.join(GAME, "bird", f"bird_{f}.png"))
    for level in LEVELS:
        build(level, birds, hurt)
        print("built", level["folder"])


if __name__ == "__main__":
    main()
