"""Generate game/assets/tilesets/room_tileset.tres from the room sheets (made by tools/kenney_art.py).

Each colour sheet becomes one atlas source (8x8 tiles), with a tile for every
8x8 cell that has at least one visible pixel. Every tile except the floor gets a full-square collision shape on physics layer 1 ("world"). Source IDs match RoomColor in
scripts/Rooms/RoomBuilder.cs. Run from the repo root:  python tools/make_room_tileset.py
"""
import struct
import zlib
from pathlib import Path

GAME = Path(__file__).resolve().parent.parent / "game"
SHEETS = ["green", "blue", "brown", "gray", "red"]  # order = source id
CELL = 8
# Walkable cells (no collision): the 16x16 floor tile.
WALKABLE = {(x, y) for x in (2, 3) for y in (0, 1)}
SQUARE = "PackedVector2Array(-4, -4, 4, -4, 4, 4, -4, 4)"


def read_rgba(path):
    data = path.read_bytes()
    pos, idat = 8, b""
    while pos < len(data):
        ln, typ = struct.unpack(">I4s", data[pos:pos + 8])
        chunk = data[pos + 8:pos + 8 + ln]
        if typ == b"IHDR":
            w, h, depth, ctype, _, _, interlace = struct.unpack(">IIBBBBB", chunk)
            assert (depth, ctype, interlace) == (8, 6, 0), f"{path}: expected 8-bit RGBA"
        elif typ == b"IDAT":
            idat += chunk
        pos += 12 + ln
    raw, stride, bpp = zlib.decompress(idat), w * 4, 4
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
    return w, h, rows


def opaque_cells(path):
    w, h, rows = read_rgba(path)
    cells = []
    for cy in range(h // CELL):
        for cx in range(w // CELL):
            if any(rows[cy * CELL + y][(cx * CELL + x) * 4 + 3]
                   for y in range(CELL) for x in range(CELL)):
                cells.append((cx, cy))
    return cells


out = [f'[gd_resource type="TileSet" load_steps={len(SHEETS) * 2 + 1} format=3]', ""]
for i, name in enumerate(SHEETS):
    out.append(f'[ext_resource type="Texture2D" '
               f'path="res://assets/kenney/rooms/room_{name}.png" id="tex_{name}"]')
out.append("")
for i, name in enumerate(SHEETS):
    out += [f'[sub_resource type="TileSetAtlasSource" id="room_{name}"]',
            f'texture = ExtResource("tex_{name}")',
            f"texture_region_size = Vector2i({CELL}, {CELL})"]
    for x, y in opaque_cells(GAME / f"assets/kenney/rooms/room_{name}.png"):
        out.append(f"{x}:{y}/0 = 0")
        if (x, y) not in WALKABLE:
            out.append(f"{x}:{y}/0/physics_layer_0/polygon_0/points = {SQUARE}")
    out.append("")
out += ["[resource]", f"tile_size = Vector2i({CELL}, {CELL})", "physics_layer_0/collision_layer = 1"]
out += [f'sources/{i} = SubResource("room_{name}")' for i, name in enumerate(SHEETS)]

dest = GAME / "assets/tilesets/room_tileset.tres"
dest.parent.mkdir(parents=True, exist_ok=True)
dest.write_text("\n".join(out) + "\n", encoding="utf-8")
print(f"wrote {dest}")
