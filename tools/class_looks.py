"""Builds each hero's class looks from its plain sprites: a hat, headband or new colours per class, so you can tell
the classes apart. Also builds the townsfolk (king, queen, knights, shopkeepers, villagers) the same way, from the
heroes' bodies recoloured and dressed up; those go to art-source/npcs/ and game/assets/sprites/npcs/ (see Npc.cs). Reads art-source/<folder>/<name>_<view>.txt, writes art-source/<folder>/<name>_<class>_<view>.txt
and renders them into game/assets/sprites/<folder>/ (Hero.cs picks them up by class name). A starting class keeps the
plain sprites. Hats need room above the head, so every class look gets PAD empty rows on top.
Run: python tools/class_looks.py   (add --preview <file.png> for a big sheet of every look)"""
import sys
from pathlib import Path

from ascii_sprite import COLOURS, read_frames, render, write_png

ROOT = Path(__file__).resolve().parent.parent
ART = ROOT / "art-source"
SPRITES = ROOT / "game" / "assets" / "sprites"
PAD = 6
VIEWS = ("down", "up", "side")

# Hats, drawn with the brim on the bottom row. "front" is used for down and up (the back of the hat: "back" recolours
# it), "side" faces right. '.' keeps whatever is underneath.
HATS = {
    "aviator": {
        "front": [
            "..oooooo..",
            ".oHHHHHHo.",
            "oHHHHHHHHo",
            "ogCgoogCgo",
            ".oooooooo.",
        ],
        "side": [
            ".oooooo.",
            "oHHHHHHo",
            "oHHHHHHo",
            "ohhogCgo",
            ".oooooo.",
        ],
    },
    "crown": {
        "front": [
            ".o..oo..o.",
            "oYooYYooYo",
            "oYYYYYYYYo",
            "oYRYYYYRYo",
            "oYYYYYYYYo",
            ".oooooooo.",
        ],
        "side": [
            "o..oo..o",
            "oYoYYoYo",
            "oYYYYYYo",
            "oYYRYYYo",
            "oYYYYYYo",
            ".oooooo.",
        ],
    },
    "tiara": {
        "front": [
            "...oo...",
            "..oRRo..",
            "ooYYYYoo",
            "oYYYYYYo",
            ".oooooo.",
        ],
        "side": [
            "..oo..",
            ".oRRo.",
            "oYYYYo",
            ".oooo.",
        ],
    },
    "beret": {
        "front": [
            ".....oo.....",
            "...ooRRoo...",
            ".ooRRRRRRoo.",
            "oRRRRRRRRRRo",
            ".oooooooooo.",
        ],
        "side": [
            "........oo..",
            "..ooooooRRo.",
            ".oRRRRRRRRRo",
            "oRRRRRRRRRo.",
            ".oooooooooo.",
        ],
    },
    "chef": {
        "front": [
            ".oo.oo.oo.",
            "oWWoWWoWWo",
            "oWWWWWWWWo",
            ".oWWWWWWo.",
            ".oWWWWWWo.",
            ".oGGGGGGo.",
            "..oooooo..",
        ],
        "side": [
            ".oo.oo.oo.",
            "oWWoWWoWWo",
            "oWWWWWWWWo",
            ".oWWWWWWo.",
            ".oWWWWWWo.",
            ".oGGGGGGo.",
            "..oooooo..",
        ],
    },
    "wizard": {
        "front": [
            "......o.....",
            ".....oVo....",
            ".....oVVo...",
            "....oVVVo...",
            "....oVYVvo..",
            "...oVYYYVvo.",
            "...oVVYVVvo.",
            "..oVVVVVVVvo",
            "oAAAAAAAAAAo",
            ".oooooooooo.",
        ],
        "side": [
            "oo..........",
            "oVoo........",
            ".oVVoo......",
            "..oVVVo.....",
            "..oVVYVo....",
            "..oVYYYVo...",
            "..oVVYVVvo..",
            "..oVVVVVVvo.",
            "oAAAAAAAAAAo",
            ".oooooooooo.",
        ],
        "back": {"Y": "V"},
    },
    "cowboy": {
        "front": [
            "....oooooo....",
            "...oHHHHHHo...",
            "...oHHhHHHo...",
            "...ohhhhhho...",
            "oo.oHHHHHHo.oo",
            "oHooooooooooHo",
            "ohHHHHHHHHHHho",
            ".oooooooooooo.",
        ],
        "side": [
            ".....ooooo....",
            "....oHHHHHo...",
            "....oHHHHHo...",
            "....ohhhhho...",
            "oo..oHHHHHo.oo",
            "oHooooooooooHo",
            "ohHHHHHHHHHHho",
            ".oooooooooooo.",
        ],
    },
    "knight": {
        "front": [
            "....oo....",
            "...oRRo...",
            "...oRRo...",
            "..ooRRoo..",
            ".oGGGGGgo.",
            "oGGGGGGGgo",
            "oGGGGGGGgo",
            "oggggggggo",
            ".oooooooo.",
        ],
        "side": [
            "...oo.....",
            "..oRRo....",
            ".oRRRo....",
            "oRRoRo....",
            "oooGGGgo..",
            ".oGGGGGGo.",
            "oGGGGGGGgo",
            "oggggggggo",
            ".oooooooo.",
        ],
    },
    "viking": {
        "front": [
            "o............o",
            "oWo........oWo",
            "oWWo.oooo.oWWo",
            ".oWWoGGGGoWWo.",
            "..ooGGGGGGoo..",
            "..oGGGGGGGgo..",
            "..ogYgYgYggo..",
            "...oooooooo...",
        ],
        "side": [
            "o..........o",
            "oWo......oWo",
            "oWWooooooWWo",
            ".oWWGGGGGWo.",
            "..oGGGGGGgo.",
            "..oGGGGGGgo.",
            "..ogYgYgYgo.",
            "...oooooooo.",
        ],
    },
}

# A headband drawn straight onto the lizard (not a hat): rows of the plain sprite, '.' keeps what's there.
LIZARD_HEADBAND = {
    "down": (3, [
        ".....oRRRRo.....",
        "...ooRRRRRRooRo.",
        ".............oRo",
    ]),
    "up": (3, [
        ".....oRRRRo.....",
        "...ooRRRRRRoo...",
        "......oRRo......",
        ".....oRooRo.....",
        "....oRo..oRo....",
    ]),
    "side": (3, [
        "..ooRRRRRRoo........",
        "oRRo................",
        "oRo.................",
        "oo..................",
    ]),
}

# The Thief's bandit mask, drawn straight onto the peacock: a dark band over the eyes, knotted at the back.
PEACOCK_MASK = {
    "down": (5, [
        "....onnnnnno....",
        "....nWPnnPWn....",
    ]),
    "up": (3, [
        "......nnnn......",
        ".......nn.......",
    ]),
    "side": (4, [
        "........onnnnno.....",
        "......nnonWWPno.....",
        ".....n..............",
    ]),
}

# The Tinkerer's goggles, drawn straight onto the moose; a strap round the back.
MOOSE_GOGGLES = {
    "down": (6, [
        "....ogGoogGo....",
        "....oCPooPCo....",
        "....ogGoogGo....",
    ]),
    "up": (7, [
        "...ooggggggoo...",
    ]),
    "side": (6, [
        ".......ogGGo........",
        "......goCPCo........",
        ".......ogGGo........",
    ]),
}

# Where a hat sits on each hero, per view: (middle column, row of the top of the head) in the plain sprite.
HEADS = {
    "chameleon": {"down": (8, 4), "up": (8, 4), "side": (8, 3)},
    "red_fox": {"down": (8, 4), "up": (8, 4), "side": (8, 4)},
    "peacock": {"down": (8, 3), "up": (8, 3), "side": (11, 3)},
    "arctic_fox": {"down": (8, 4), "up": (8, 4), "side": (8, 4)},
    "beetle": {"down": (8, 5), "up": (8, 5), "side": (12, 6)},
    "moose": {"down": (8, 5), "up": (8, 5), "side": (9, 6)},
}

# Spitter: a teal lizard (poison spit).
TEAL = {"L": "q", "B": "C", "M": "c", "D": "u"}

# (sprite folder/name, class, hat, extra ASCII stamps per view, recolour)
LOOKS = [
    ("chameleon", "Tongue Master", None, LIZARD_HEADBAND, {}),
    ("chameleon", "Spitter", None, None, TEAL),
    ("chameleon", "Illusionist", "wizard", None, {}),
    ("red_fox", "Gun Master", "cowboy", None, {}),
    ("peacock", "Warrior", "knight", None, {}),
    ("peacock", "Thief", None, PEACOCK_MASK, {}),
    ("moose", "Tinkerer", None, MOOSE_GOGGLES, {}),
    ("beetle", "Vehicle Master", "aviator", None, {}),
]


# Townsfolk: (name, hero body, hat, recolour). Recolours make them clearly not one of the heroes.
STEEL_FOX = {"F": "G", "f": "g", "n": "b", "k": "g"}
STEEL_BEETLE = {"V": "g", "v": "b", "A": "G"}
STEEL_LIZARD = {"B": "G", "M": "g", "L": "b"}
STEEL_MOOSE = {"H": "G", "h": "g", "n": "b"}
STEEL_PEACOCK = {"U": "G", "u": "g", "c": "b"}
# Each kingdom's castle is guarded by knights of its own kind.
NPCS = [
    ("king", "chameleon", "crown", {"L": "B", "B": "M", "M": "D"}),
    ("queen", "peacock", "tiara", {"U": "V", "u": "v", "C": "A", "c": "v"}),
    ("knight_fox", "red_fox", "knight", STEEL_FOX),
    ("knight_beetle", "beetle", "knight", STEEL_BEETLE),
    ("knight_lizard", "chameleon", "knight", STEEL_LIZARD),
    ("knight_moose", "moose", "knight", STEEL_MOOSE),
    ("knight_peacock", "peacock", "knight", STEEL_PEACOCK),
    ("innkeeper", "moose", None, {"H": "E", "h": "e"}),
    ("herbalist", "beetle", None, {"V": "B", "v": "M", "A": "L"}),
    ("paint_seller", "chameleon", "beret", {"L": "Y", "B": "F", "M": "f", "D": "n"}),
    ("smith", "red_fox", "viking", {"F": "H", "f": "h"}),
    ("cook", "arctic_fox", "chef", {}),
    ("villager_fox", "arctic_fox", None, {}),
    ("villager_moose", "moose", None, {"H": "g", "h": "b"}),
    ("villager_lizard", "chameleon", None, {"L": "q", "B": "U", "M": "u", "D": "v"}),
    ("villager_peacock", "peacock", None, {"U": "B", "u": "M", "C": "Y", "c": "f"}),
    # The other kingdoms' royals and villagers.
    ("moose_king", "moose", "crown", {"H": "h", "h": "n"}),
    ("moose_queen", "moose", "tiara", {"H": "E", "h": "e"}),
    ("fox_king", "red_fox", "crown", {}),
    ("fox_queen", "arctic_fox", "tiara", {}),
    ("peacock_king", "peacock", "crown", {}),
    ("beetle_king", "beetle", "crown", {}),
    ("beetle_queen", "beetle", "tiara", {"V": "Y", "v": "f", "A": "W"}),
    ("villager_redfox", "red_fox", None, {"F": "E", "f": "e"}),
    ("villager_beetle", "beetle", None, {"V": "C", "v": "c", "A": "q"}),
    ("villager_deer", "moose", None, {"H": "E", "h": "e", "T": "e", "t": "n"}),
]


def stamp(frame, rows, left, top):
    for y, row in enumerate(rows):
        for x, c in enumerate(row):
            if c != "." and 0 <= top + y < len(frame) and 0 <= left + x < len(frame[0]):
                frame[top + y][left + x] = c


def dress(name, hat, stamps, recolour, view):
    frames, w, _ = read_frames(ART / name / f"{name}_{view}.txt")
    out = []
    for f in frames:
        grid = [["."] * w for _ in range(PAD)] + [[recolour.get(c, c) for c in row] for row in f]
        if stamps:
            top, rows = stamps[view]
            stamp(grid, rows, 0, PAD + top)
        if hat:
            rows = HATS[hat]["side" if view == "side" else "front"]
            if view == "up":
                back = HATS[hat].get("back", {})
                rows = ["".join(back.get(c, c) for c in r) for r in rows]
            cx, head = HEADS[name][view]
            stamp(grid, rows, cx - len(rows[0]) // 2, PAD + head - len(rows) + 1)
        out.append(["".join(r) for r in grid])
    return out


def main():
    preview = []
    for name, cls, hat, stamps, recolour in LOOKS:
        slug = cls.lower().replace(" ", "_")
        for view in VIEWS:
            frames = dress(name, hat, stamps, recolour, view)
            txt = ART / name / f"{name}_{slug}_{view}.txt"
            txt.write_text(f"# {name} as a {cls}, facing {view}. Made by tools/class_looks.py from {name}_{view}.txt; edit that"
                           " script, not this file.\n" + "\n\n".join("\n".join(f) for f in frames) + "\n", encoding="utf-8")
            render(txt, SPRITES / name / f"{name}_{slug}_{view}.png")
            preview.append(frames[0])

    (ART / "npcs").mkdir(exist_ok=True)
    (SPRITES / "npcs").mkdir(exist_ok=True)
    for npc, body, hat, recolour in NPCS:
        for view in VIEWS:
            frames = dress(body, hat, None, recolour, view)
            txt = ART / "npcs" / f"{npc}_{view}.txt"
            txt.write_text(f"# The {npc.replace('_', ' ')} (townsfolk), facing {view}. Made by tools/class_looks.py from"
                           f" {body}_{view}.txt; edit that script, not this file.\n"
                           + "\n\n".join("\n".join(f) for f in frames) + "\n", encoding="utf-8")
            render(txt, SPRITES / "npcs" / f"{npc}_{view}.png")
            preview.append(frames[0])

    if "--preview" in sys.argv:
        # Every look's idle frames in a row (down, up, side), one look per line, scaled up.
        scale, h = 6, max(len(f) for f in preview)
        lines = [preview[i:i + 3] for i in range(0, len(preview), 3)]
        rows = []
        for line in lines:
            for y in range(h):
                px = []
                for f in line:
                    for c in (f[y] if y < len(f) else "." * len(f[0])) + "..":
                        px += [COLOURS[c] if c != "." else (90, 100, 120, 255)] * scale
                rows += [px] * scale
        width = max(len(r) for r in rows)
        rows = [r + [(90, 100, 120, 255)] * (width - len(r)) for r in rows]
        write_png(sys.argv[sys.argv.index("--preview") + 1], rows)


main()
