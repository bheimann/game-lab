"""Builds the walk strips for the new enemies (side view, facing right; the game mirrors them for left):
boar, arctic fox (on four legs), frog and snake. Writes art-source/<name>/<name>_walk.txt and renders them into
game/assets/sprites/<name>/<name>_walk.png. Each body is drawn once; the legs (or the snake's wiggle) change per frame.
Run: python tools/beast_frames.py"""
from pathlib import Path

from ascii_sprite import render

ROOT = Path(__file__).resolve().parent.parent


def grid(rows, w, h):
    rows = [r.ljust(w, ".")[:w] for r in rows]
    return [list(r) for r in rows + ["." * w] * (h - len(rows))]


def paint(g, x, y, text):
    for i, c in enumerate(text):
        if c != "." and 0 <= y < len(g) and 0 <= x + i < len(g[0]):
            g[y][x + i] = c


def legs(g, top, xs, shifts, colour, hoof, length):
    """Four legs hanging from row `top`: a column of `colour` with an outline each side, ending in a `hoof` row."""
    for x, dx in zip(xs, shifts):
        for i in range(length):
            # The leg leans the way it's stepping, more towards the bottom.
            lean = round(dx * i / max(1, length - 1))
            paint(g, x + lean - 1, top + i, "o" + colour * 2 + "o")
        lean = dx
        paint(g, x + lean - 1, top + length, "o" + hoof * 2 + "o")
        paint(g, x + lean - 1, top + length + 1, "oooo")


def save(name, frames, comment):
    out = ROOT / "art-source" / name
    out.mkdir(exist_ok=True)
    txt = out / f"{name}_walk.txt"
    txt.write_text(f"# {comment} Made by tools/beast_frames.py.\n"
                   + "\n\n".join("\n".join("".join(r) for r in f) for f in frames) + "\n", encoding="utf-8")
    sprites = ROOT / "game" / "assets" / "sprites" / name
    sprites.mkdir(exist_ok=True)
    render(txt, sprites / f"{name}_walk.png")


# ---- Boar: big, brown and bristly, with white tusks. 32x24.
BOAR = [
    "",
    "",
    "......................oo",
    "........ooooooooooooooohoo",
    "......oohhHHHHHHHHHHHhhHHoo",
    ".....ohhHHHHHHHHHHHHHHHHHHHoo",
    "....ohHHHHHHHHHHHHHHHHHHHHPHHo",
    "...ohHHHHHHHHHHHHHHHHHHHHHHHkko",
    "..ohhHHHHHHHHHHHHHHHHHHHHHHkkPko",
    "..ohHHHHHHHHHHHHHHHHHHHHHHHkkkko",
    "..ohHHHHHHHHHHHHHHHHHHHHHHWWoooo",
    "..ohhHHHHHHHHHHHHHHHHHHHHHoWWo",
    "...ohhhHHHHHHHHHHHHHHHHHHhooo",
    "....ohhhhhhhhhhhhhhhhhhhho",
    ".....ooooooooooooooooooo",
]
frames = []
for shifts in ((0, 0, 0, 0), (1, -1, 1, -1), (-1, 1, -1, 1)):
    g = grid(BOAR, 32, 24)
    legs(g, 15, (6, 10, 18, 22), shifts, "h", "n", 5)
    frames.append(g)
save("boar", frames, "Boar enemy facing right, 32x24, feet on the bottom row.")

# ---- Arctic fox on four legs: white fur, a big bushy tail behind, pointy ears. 28x22.
FOX = [
    "",
    "....................o..o",
    "...................oGooGo",
    "..................oWWWWWWo",
    "..oooo...........oWWWWWPWWo",
    ".oWWWWo..........oWWWWWWWWPo",
    "oWWWWWWo........ooWWWWWWWoo",
    "oWWWWWWWoooooooooWWWWWWoo",
    "oWWWWWWWWWWWWWWWWWWWWWWo",
    ".oWWWWWWWWWWWWWWWWWWWWWo",
    "..oGWWWWWWWWWWWWWWWWWWGo",
    "...oGGWWWWWWWWWWWWWWWGo",
    "....ooGGGGGGGGGGGGGGGo",
    "......ooooooooooooooo",
]
frames = []
for shifts in ((0, 0, 0, 0), (1, -1, 1, -1), (-1, 1, -1, 1)):
    g = grid(FOX, 28, 22)
    legs(g, 13, (8, 11, 17, 20), shifts, "W", "G", 5)
    frames.append(g)
save("arctic_fox_enemy", frames, "Arctic fox enemy (on four legs) facing right, 28x22, feet on the bottom row.")

# ---- Frog: squat and green with big eyes on top; it sits, then stretches out mid-hop. 20x16.
SIT = [
    "",
    "",
    "",
    "",
    "..........oo..oo",
    ".........oWPooWPo",
    "........oBBBBBBBBo",
    ".......oBBBBBBBBBBo",
    "......oBBBBBBBBBRRo",
    "....ooBBBBBBBBBBBo",
    "...oMBBBBBLLLLLLo",
    "..oMMBBBBLLLLLLLo",
    "..oMMMBBBLLLLLLo",
    "..ooMMMMoooLLoo",
    ".oMMMMMoooMMMMo",
    ".ooooooo.ooooo",
]
HOP = [
    "",
    "",
    "..........oo..oo",
    ".........oWPooWPo",
    "........oBBBBBBBBo",
    ".......oBBBBBBBBBBo",
    "......oBBBBBBBBBRRo",
    "....ooBBBBBBBBBBBo",
    "...oMBBBBBLLLLLLo",
    "..oMMBBBBLLLLLLLo",
    ".oMMMBBBBLLLLLLoo",
    "oMMooMMoooLLooMMo",
    "oMo..oMo..ooo.oMo",
    "oo....oo.......oo",
]
save("frog", [grid(SIT, 20, 16), grid(HOP, 20, 16), grid(SIT, 20, 16)], "Frog enemy facing right, 20x16: sitting, mid-hop.")

# ---- Snake: a long orange-brown snake with dark stripes, wiggling along. 28x12.
def snake(phase):
    g = grid([], 28, 12)
    # The body: a wave along the ground; the head at the right end.
    import math
    for x in range(2, 22):
        y = 7 + round(1.5 * math.sin((x + phase * 3) * 0.6))
        stripe = "n" if x % 4 == 0 else "F"
        paint(g, x, y - 1, "o")
        paint(g, x, y, stripe)
        paint(g, x, y + 1, "f")
        paint(g, x, y + 2, "o")
    head_y = 7 + round(1.5 * math.sin((22 + phase * 3) * 0.6)) - 1
    paint(g, 21, head_y - 1, "oooo")
    paint(g, 20, head_y, "oFFFPo")
    paint(g, 20, head_y + 1, "oFFFFFo")
    paint(g, 20, head_y + 2, "offfoo")
    paint(g, 21, head_y + 3, "oooo.R")
    paint(g, 1, 7 + round(1.5 * math.sin((1 + phase * 3) * 0.6)), "o")
    return g


save("snake", [snake(0), snake(1), snake(2)], "Snake enemy facing right, 28x12, wiggling.")
