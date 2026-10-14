"""Render hand-drawn ASCII pixel art to PNG strips.

Each frame is a block of text rows (all the same width); frames are separated by a blank line.
The frames are laid out left to right in one PNG, ready for SpriteSheet.cs.

usage: python tools/ascii_sprite.py <input.txt> <output.png>

Colours (Tiny Dungeon palette):
  .  transparent      o  outline (navy)    W  white
  L  light green      B  bright green      M  mid green      D  dark green
  Y  yellow           R  red / pink        P  pupil (navy)
  G  light grey       g  mid grey          b  blue-grey         k  pink (inner ear)
  F  fox orange       f  dark orange       n  dark brown (fox legs)
  H  moose brown      h  dark brown        T  tan (antler)      t  dark tan
  C  teal             c  dark teal         U  blue              u  dark blue
  V  violet           v  dark violet       A  lilac (shine)
"""
import struct
import sys
import zlib

COLOURS = {
    ".": (0, 0, 0, 0),
    "o": (20, 24, 46, 255),
    "P": (20, 24, 46, 255),
    "W": (245, 255, 232, 255),
    "L": (200, 212, 93, 255),
    "B": (99, 171, 63, 255),
    "M": (59, 125, 79, 255),
    "D": (47, 87, 83, 255),
    "Y": (240, 181, 65, 255),
    "R": (255, 82, 119, 255),
    "G": (223, 224, 232, 255),
    "g": (163, 167, 194, 255),
    "b": (104, 111, 153, 255),
    "k": (255, 194, 161, 255),
    "F": (237, 118, 54, 255),
    "f": (181, 74, 46, 255),
    "n": (66, 38, 46, 255),
    "H": (150, 95, 60, 255),
    "h": (96, 58, 44, 255),
    "T": (238, 214, 160, 255),
    "t": (196, 160, 104, 255),
    "C": (62, 190, 186, 255),
    "c": (30, 120, 130, 255),
    "U": (72, 120, 220, 255),
    "u": (44, 62, 150, 255),
    "V": (128, 84, 200, 255),
    "v": (74, 46, 128, 255),
    "A": (196, 168, 240, 255),
}


def read_frames(path):
    frames, current = [], []
    for line in open(path, encoding="utf-8"):
        line = line.rstrip("\n")
        if line.startswith("#"):
            continue
        if line.strip() == "":
            if current:
                frames.append(current)
                current = []
            continue
        current.append(line)
    if current:
        frames.append(current)
    w, h = len(frames[0][0]), len(frames[0])
    for i, f in enumerate(frames):
        assert len(f) == h and all(len(r) == w for r in f), f"frame {i} is not {w}x{h}"
    return frames, w, h


def write_png(path, rows):
    h, w = len(rows), len(rows[0])
    raw = b"".join(b"\x00" + bytes(c for p in row for c in p) for row in rows)

    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b""))


frames, w, h = read_frames(sys.argv[1])
rows = [[COLOURS[f[y][x]] for f in frames for x in range(w)] for y in range(h)]
write_png(sys.argv[2], rows)
print(f"wrote {sys.argv[2]}: {len(frames)} frame(s) of {w}x{h}")
