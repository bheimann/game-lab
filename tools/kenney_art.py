"""Builds the world art in game/assets/kenney/ from Kenney's CC0 "Tiny" packs (art-source/kenney/*/tilemap_packed.png,
16x16 tiles with no spacing) plus a few small pieces drawn here as ASCII grids (lever, torch, bomb sparks).

The room sheets keep the cell layout RoomBuilder.cs, Door.cs and tools/make_room_tileset.py expect (8px cells):
  floor          cells (2..3, 0..1), one 16x16 tile
  top wall       rows 2..5, cols 1..8: a row of cap stones, then 24px of brick face
  side walls     cols 1 and 8 (row 6 is the repeating middle)
  bottom wall    rows 10..12: cap stones, then 16px of brick face
  door pieces    x >= 96, see DOOR_PIECES (Door.cs uses the same rectangles)
Every room colour is the same stonework recoloured.

Run from the repo root:  python tools/kenney_art.py   (then python tools/make_room_tileset.py)
"""
import struct
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "art-source" / "kenney"
OUT = ROOT / "game" / "assets" / "kenney"
T = 16
CLEAR = (0, 0, 0, 0)


# ---------- PNG in / out ----------

def read_png(path):
    data = path.read_bytes()
    pos, idat, palette, alpha = 8, b"", [], b""
    while pos < len(data):
        ln, typ = struct.unpack(">I4s", data[pos:pos + 8])
        chunk = data[pos + 8:pos + 8 + ln]
        if typ == b"IHDR":
            w, h, depth, ctype, _, _, interlace = struct.unpack(">IIBBBBB", chunk)
            assert depth == 8 and interlace == 0 and ctype in (2, 3, 6), f"{path}: unsupported PNG"
        elif typ == b"PLTE":
            palette = [tuple(chunk[i:i + 3]) for i in range(0, len(chunk), 3)]
        elif typ == b"tRNS":
            alpha = chunk
        elif typ == b"IDAT":
            idat += chunk
        pos += 12 + ln
    bpp = {2: 3, 3: 1, 6: 4}[ctype]
    raw, stride = zlib.decompress(idat), w * bpp
    rows, prev, i = [], bytearray(stride), 0
    for _ in range(h):
        f, line = raw[i], bytearray(raw[i + 1:i + 1 + stride])
        i += 1 + stride
        for x in range(stride):
            a = line[x - bpp] if x >= bpp else 0
            b = prev[x]
            c = prev[x - bpp] if x >= bpp else 0
            if f == 1:
                line[x] = (line[x] + a) & 255
            elif f == 2:
                line[x] = (line[x] + b) & 255
            elif f == 3:
                line[x] = (line[x] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(bytes(line))
        prev = line
    img = []
    for r in rows:
        if ctype == 6:
            img.append([tuple(r[j:j + 4]) for j in range(0, stride, 4)])
        elif ctype == 2:
            img.append([tuple(r[j:j + 3]) + (255,) for j in range(0, stride, 3)])
        else:
            img.append([palette[v] + ((alpha[v] if v < len(alpha) else 255),) for v in r])
    return img


def write_png(path, img):
    h, w = len(img), len(img[0])
    raw = b"".join(b"\x00" + bytes(c for p in row for c in p) for row in img)

    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))
    print(f"wrote {path.relative_to(ROOT)} ({w}x{h})")


# ---------- image helpers (an image is a list of rows of RGBA tuples) ----------

def blank(w, h):
    return [[CLEAR] * w for _ in range(h)]


def crop(img, x, y, w, h):
    return [row[x:x + w] for row in img[y:y + h]]


def tile(sheet, col, row, w=1, h=1):
    return crop(sheet, col * T, row * T, w * T, h * T)


def paste(dst, src, x, y):
    for j, row in enumerate(src):
        for i, p in enumerate(row):
            if p[3] and 0 <= y + j < len(dst) and 0 <= x + i < len(dst[0]):
                dst[y + j][x + i] = p


def trim(img):
    """Crop to the visible pixels."""
    ys = [y for y, row in enumerate(img) if any(p[3] for p in row)]
    xs = [x for x in range(len(img[0])) if any(row[x][3] for row in img)]
    return crop(img, xs[0], ys[0], xs[-1] - xs[0] + 1, ys[-1] - ys[0] + 1)


def hstack(*imgs):
    return [sum((img[y] for img in imgs), []) for y in range(len(imgs[0]))]


def recolour(img, mapping):
    return [[mapping.get(p[:3], p[:3]) + (p[3],) if p[3] else p for p in row] for row in img]


def shade(img, factor):
    return [[(int(p[0] * factor), int(p[1] * factor), int(p[2] * factor), p[3]) if p[3] else p for p in row] for row in img]


def mirror(img):
    return [row[::-1] for row in img]


# Kenney Tiny palette (the colours we need by name).
OUTLINE = (63, 38, 49)
STONE = [(192, 203, 220), (139, 155, 180), (82, 96, 124)]  # light, mid, dark
DARK = (38, 43, 68)
SAND = (234, 165, 108)
MAROON = (118, 59, 54)
WOOD = (189, 108, 74)
RED, PINK = (232, 69, 55), (255, 112, 109)
ORANGE, YELLOW = (254, 174, 52), (254, 231, 97)
GREEN, GREEN_LIGHT = (99, 171, 63), (139, 216, 125)
WHITE = (255, 255, 255)

ASCII = {".": CLEAR, "o": OUTLINE + (255,), "c": STONE[0] + (255,), "d": STONE[1] + (255,), "e": STONE[2] + (255,),
         "w": WOOD + (255,), "m": MAROON + (255,), "R": RED + (255,), "P": PINK + (255,), "O": ORANGE + (255,),
         "Y": YELLOW + (255,), "W": WHITE + (255,), "G": GREEN + (255,), "g": GREEN_LIGHT + (255,), "s": SAND + (255,)}


def ascii_img(lines):
    assert len({len(r) for r in lines}) == 1, [len(r) for r in lines]
    return [[ASCII[ch] for ch in row] for row in lines]


# ---------- rooms ----------

# Stone colours for each room, light / mid / dark (gray is Kenney's own).
ROOM_COLOURS = {
    "green": [(176, 214, 160), (112, 168, 104), (58, 104, 72)],
    "blue": [(176, 200, 240), (110, 140, 210), (56, 76, 140)],
    "brown": [(222, 194, 156), (172, 130, 94), (106, 74, 58)],
    "gray": STONE,
    "red": [(236, 166, 160), (196, 98, 98), (122, 50, 64)],
}

# Door.cs cuts these out of the room sheet (x, y, w, h).
DOOR_PIECES = {
    "closed": (96, 16, 32, 24),  # top door: a strip of wall, then the door
    "open": (96, 48, 32, 24),
    "left_post": (136, 16, 8, 28),  # caps the side wall above and below a side doorway
    "right_post": (144, 16, 8, 28),
}


def cap_row(dungeon):
    """32x8 of the cap stones along the top of a wall (repeats every 16px), from the walled pit at tile (4, 0)."""
    frame = tile(dungeon, 4, 0, 2, 2)
    return [frame[y][10:26] * 2 for y in range(1, 9)]


def brick_face(dungeon, height):
    brick = tile(dungeon, 4, 3)
    return [brick[(1 + y) % 16] * 2 for y in range(height)]  # 32 wide


def side_column(height):
    """An 8px wide side wall seen from above."""
    return ascii_img(["oocccdoo"] * height)


def room_sheet(dungeon, colour):
    sheet = blank(208, 160)
    cap, face24, face16 = cap_row(dungeon), brick_face(dungeon, 24), brick_face(dungeon, 16)

    paste(sheet, tile(dungeon, 0, 4), 16, 0)  # floor

    for x, w in ((16, 32), (48, 16)):  # cols 2..7: cap + face (top), cap + face (bottom)
        paste(sheet, crop(cap, 0, 0, w, 8), x, 16)
        paste(sheet, crop(face24, 0, 0, w, 24), x, 24)
        paste(sheet, crop(cap, 0, 0, w, 8), x, 80)
        paste(sheet, crop(face16, 0, 0, w, 16), x, 88)

    for x in (8, 64):  # side columns: wall top down the sides, brick face under the bottom corners
        paste(sheet, side_column(40), x, 16)
        paste(sheet, side_column(8), x, 80)
        paste(sheet, crop(face16, 0, 0, 8, 16), x, 88)
    for y in range(88, 104):  # outline the outer ends of the bottom face
        sheet[y][8] = sheet[y][71] = OUTLINE + (255,)
    for x in range(8, 72):  # and the top edge of the cap stones at the corners
        sheet[16][x] = sheet[80][x] = OUTLINE + (255,)

    # Door pieces: a strip of wall face over Kenney's double door (closed / open archway).
    for name, door in (("closed", tile(dungeon, 10, 3, 2, 1)), ("open", tile(dungeon, 10, 0, 2, 1))):
        x, y, w, h = DOOR_PIECES[name]
        paste(sheet, crop(face24, 0, 16, 32, 8), x, y)
        paste(sheet, door, x, y + 8)

    post = side_column(28)
    for y in range(6, 22):
        post[y] = [CLEAR] * 8
    post[5] = post[22] = [OUTLINE + (255,)] * 8
    paste(sheet, post, *DOOR_PIECES["left_post"][:2])
    paste(sheet, post, *DOOR_PIECES["right_post"][:2])

    return recolour(sheet, dict(zip(STONE, ROOM_COLOURS[colour])))


def pit(dungeon):
    """48x48 nine-patch (6px border): stone rim around a dark hole, with the far wall showing inside the top."""
    img = blank(48, 48)
    for y in range(48):
        img[y] = [DARK + (255,)] * 48
    inner = shade([row + row[:16] for row in brick_face(dungeon, 10)], 0.45)
    paste(img, inner, 0, 6)
    rim = cap_row(dungeon)
    for x in (0, 16, 32):
        paste(img, crop(rim, 0, 1, 16, 6), x, 0)
        paste(img, crop(rim, 0, 1, 16, 6), x, 42)
    side = ascii_img(["oocddo"] * 48)
    paste(img, side, 0, 0)
    paste(img, mirror(side), 42, 0)
    for x in range(48):
        img[0][x] = img[47][x] = OUTLINE + (255,)
    return img


# ---------- things we draw ourselves ----------

TORCH_FLAMES = [
    ["...Y....", "..YOY...", "..YRO...", ".YORRY..", ".YRRRY..", "..YRY..."],
    ["....Y...", "...YOY..", "..YORY..", "..YRRY..", ".YRRRY..", "..YRY..."],
    ["........", "...Y....", "..YOY...", ".YORRY..", ".YRRRY..", "..YRY..."],
]
TORCH_HANDLE = ["..owwo..", "..owmo..", "..owmo..", "...oo..."]

LEVER_OFF = [
    "..........",
    ".ooo......",
    "oRRPo.....",
    "oRRRo.....",
    "oRRRo.....",
    ".ooWo.....",
    "...owo....",
    "...owo....",
    "....owo...",
    "....owo...",
    ".ooooooo..",
    "occcowccco",
    "oddddddddo",
    "oddddddddo",
    "oeeeeeeeeo",
    ".oooooooo.",
]

# Bomb fuse sparks, drawn over the end of the fuse (top right of the bomb).
SPARKS = [
    ["..Y..", ".YWY.", "..Y.."],
    [".Y.Y.", "..W..", ".Y.Y."],
    ["Y.O.Y", ".OWO.", "Y.O.Y"],
    ["..O..", ".OYO.", "..O.."],
]


def lever():
    off = ascii_img(LEVER_OFF)
    on = mirror(ascii_img([row.replace("R", "G").replace("P", "g") for row in LEVER_OFF]))
    return hstack(off, on)


def torch():
    frames = [ascii_img(TORCH_FLAMES[i] + TORCH_HANDLE) for i in (0, 1, 2, 1, 0, 2)]
    return hstack(*frames)


def lit_bomb(bomb):
    frames = []
    for spark in SPARKS:
        frame = [row[:] for row in bomb]
        paste(frame, ascii_img(spark), 10, 0)
        frames.append(frame)
    return hstack(*frames)


# ---------- main ----------

def main():
    dungeon = read_png(SRC / "tiny-dungeon" / "tilemap_packed.png")
    town = read_png(SRC / "tiny-town" / "tilemap_packed.png")
    battle = read_png(SRC / "tiny-battle" / "tilemap_packed.png")
    farm = read_png(SRC / "tiny-farm" / "tilemap_packed.png")
    ski = read_png(SRC / "tiny-ski" / "tilemap_packed.png")

    for colour in ROOM_COLOURS:
        write_png(OUT / "rooms" / f"room_{colour}.png", room_sheet(dungeon, colour))

    write_png(OUT / "pit.png", pit(dungeon))
    write_png(OUT / "water.png", tile(battle, 0, 1, 3, 3))
    write_png(OUT / "chest.png", hstack(tile(dungeon, 5, 7), tile(dungeon, 6, 7), tile(dungeon, 7, 7)))
    write_png(OUT / "stairs_down.png", tile(dungeon, 3, 3))

    bomb = tile(town, 9, 8)
    write_png(OUT / "bomb.png", bomb)
    write_png(OUT / "bomb_lit.png", lit_bomb(bomb))

    silver_to_gold = {STONE[0]: YELLOW, STONE[1]: ORANGE}
    write_png(OUT / "key.png", trim(recolour(tile(town, 9, 9), silver_to_gold)))
    write_png(OUT / "heart.png", trim(tile(battle, 15, 10)))

    write_png(OUT / "bush.png", trim(tile(town, 5, 0)))
    write_png(OUT / "sign.png", trim(tile(town, 11, 6)))
    write_png(OUT / "box.png", trim(tile(farm, 4, 6)))
    write_png(OUT / "rock.png", trim(tile(ski, 9, 6)))
    write_png(OUT / "rock2.png", trim(tile(farm, 5, 7)))

    write_png(OUT / "lever.png", lever())
    write_png(OUT / "torch.png", torch())

    # Outdoor props for the overworld areas (Prop.cs: the file name is the PropKind, lower case).
    write_png(OUT / "tree.png", trim(tile(town, 4, 0, 1, 2)))
    write_png(OUT / "treeautumn.png", trim(tile(town, 3, 0, 1, 2)))
    write_png(OUT / "smalltree.png", trim(tile(town, 4, 2)))
    write_png(OUT / "pine.png", trim(tile(farm, 3, 0, 1, 2)))
    write_png(OUT / "snowpine.png", trim(tile(ski, 6, 0, 1, 2)))
    write_png(OUT / "deadtree.png", trim(tile(farm, 2, 0, 1, 2)))
    write_png(OUT / "mushroom.png", trim(tile(town, 5, 2)))
    write_png(OUT / "snowman.png", trim(tile(ski, 9, 5)))
    write_png(OUT / "fence.png", trim(hstack(tile(town, 8, 6), tile(town, 9, 6), tile(town, 10, 6))))
    write_png(OUT / "houseblue.png", house(town, 0))
    write_png(OUT / "housered.png", house(town, 4))


def house(town, col):
    """A 4x3-tile house: two rows of roof (with a dormer and gable on the right) over a wall with a doorway.
    The doorway is see-through in the pack, so it's filled in dark."""
    rows = [hstack(*[tile(town, col + c, r) for c in range(4)]) for r in (4, 5, 6)]
    img = [row[:] for band in rows for row in band]
    for y in range(2 * T, 3 * T):
        for x in range(T // 4, 4 * T - T // 4):
            if img[y][x][3] == 0:
                img[y][x] = (38, 28, 36, 255)
    return img


if __name__ == "__main__":
    main()
