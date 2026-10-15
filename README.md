# Lizard Wizard (game-lab)

**Lizard Wizard**: an experiment in building a small game with AI help. It is a local co-op lizard action-adventure made with Godot 4 (C#).

- [docs/design.md](docs/design.md): what the game is, the lizards, and open design questions
- [docs/plan.md](docs/plan.md): milestones, from setup to a playable dungeon
- [docs/ideas.md](docs/ideas.md): the original idea shortlist

## Running the game
1. Start Godot: `C:\Dev\tools\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe`
2. Click **Import**, choose `game/project.godot`, then open it.
3. Press **▶** (F5).

### Controls
Up to 4 players. Each controller is a player, and the keyboard can be two players (left and right halves).
Press a button to join, pick a character with left / right and confirm with the primary button.
Hold the leave button for a second to drop out.

| | Keyboard (left) | Keyboard (right) | Controller | Lizard | Fox |
|---|---|---|---|---|---|
| Move | WASD | Arrows | Left stick or d-pad | | |
| Primary | J, Space or F | . or Numpad 1 | A | Punch | Bite |
| Secondary | K or G | / or Numpad 2 | X | Tongue (pulls deer in) | Throw bomb |
| Special | L or H | , or Numpad 3 | Y | Change colour | – |
| Dodge | Shift | Ctrl or Numpad . | RT | Dodge | Dodge |
| Previous / next | Q / E | ; / ' | LB / RB | Pick colour | Pick bomb type |
| Item | I | M or Numpad 0 | B | Eat a healing herb | Eat a healing herb |
| Menu | Tab | Enter | Start | Ring menu | Ring menu |
| Leave | hold Backspace | hold Delete | hold Back | | |

The lizard also hides (camouflage) by standing still for a moment. Its tongue sticks to deer and reels them in
(the Giant Beetle is too heavy).

**Dodge:** a quick dash the way you're pushing; nothing can hurt you during it. Watch for a deer flashing
yellow: that's its kick coming. Works even with the ring menu open.

**More heroes (warriors you helped):** the **moose** (slow, tough, big antler bash), the **peacock** and the
**beetle**.
- The **moose** is a **Summoner**: A antler bash, X summons a small creature (LB / RB pick: **squirrel swarm**,
  **spirit wolf**, **bee swarm** (poison), **owl** (swoops), **element wisps** (zap from a distance) or **antler
  totem** (heals heroes next to it)), Y summons a big one, and **B switches** the big one: the **treant** (a tough
  walking tree enemies go for first) or the **stampede** (ghost deer charge across, hitting everything). Each
  summon has a wait before the next one. Up to 4 creatures at once (a new one sends the oldest away); they fight
  the enemy the moose hit last, follow it through doors, and stay until they're beaten. The Summoner eats herbs from
  the ring menu (Start), since B switches the big summon.
- The **beetle** starts as a **Roller**: A horn jab, X **rolls** (a fast ball that rams enemies and bounces off
  walls; steer it a little; hits hurt half as much while rolling), Y **hides in its shell** (blocks every hit for a
  moment), and its tough shell always makes hits hurt less. Its other class, **Warrior**, just jabs.
- The **peacock** starts as a **Grappler**: A shoots a fan of three **feathers** (LB / RB pick normal, poison,
  fire, ice, lightning or arcane), X fires the **grapple gun**, Y fires an **RPG** (a rocket with a big explosion
  that also breaks cracked rocks). The grapple **reels in** small enemies, **pulls you over** to big ones (bosses)
  and to **walls, rocks and bushes**, flying you over **water and pits** on the way. If something hits you
  mid-flight over water, you land back where you started. Its other class, **Warrior**, shoots a plain gun.
- **Secret:** in Scalebrook Meadow's top-left corner there's a chest across the water that only a grapple can reach.

**The overworld:** Scalebrook Meadow is in the middle, with the stone temple gate into The Old Burrow at the top
right (walk in through the arch), and paths out through gaps in the bushes (signs show the way):
west to **Scalebrook Village** (houses and the village painter, no enemies), east to **Antlerwood Forest** (thick
trees, deer, a chest in a clearing) and on north to **Frosttail Tundra** (snow, snowmen, a frozen lake), and south
to **Tailspin Swamp** (murky ponds, bare trees, and another chest only a grapple can reach).

**Classes (getting painted):** the lizard can be a **Chameleon** (camouflage, colour change), **Tongue Master**
(stronger, longer tongue), **Spitter** (special button spits poison; LB / RB pick poison, fire, ice, lightning
or arcane) or **Illusionist** (below). The fox can be a **Bomb Master**, **Gun Master** (shoots; LB / RB pick the
element) or **Weapon Master** (below). To change class, visit the **painter in Scalebrook Village**: stand on
its mat for a moment to switch to your next class (step off and on again for the one after).

**Weapon Master (fox):** LB / RB switch weapons. A attacks with the weapon (instead of biting), X does its trick:

| Weapon | A | X | Y |
|---|---|---|---|
| Sword & Shield | Slash | Raise the shield: blocks any attack for a moment | – |
| Javelin | Long jab | Throw the javelin | – |
| Flail | Wide swing | Spin attack: hits everything around you | – |
| Axe & Bow | Chop | Shoot an arrow | Change the arrow: normal, arcane, fire, ice, lightning, poison, explosive (a small blast that also breaks cracked rocks) |

**Illusionist (lizard):** 10 illusionist energy (the purple dots under your feet), which fills back up by
itself (1 a second). LB / RB pick a spell, Y casts it, and every spell lasts 5 seconds:
- **Clones** (5 energy): two copies of you that go and punch the enemy you hit last (or wander off if there
  isn't one). Enemies chase them, and a clone pops when it's hit.
- **Tail decoy** (4 energy): drop your tail; enemies attack it instead of you.
- **Charm** (2 energy for each enemy): nearby enemies get a heart and fight the other enemies for you.
Bosses can't be charmed and ignore decoys. A punches and X uses the tongue, as usual.

**Elements:** poison and fire keep hurting for a few seconds, ice slows enemies to half speed, and lightning
and arcane do double damage.

**Saving:** the game saves by itself when the team goes through a door, beats a boss or a player leaves, and
when you close the window. Next time you start in Scalebrook Meadow with everything you'd done (doors, levers,
chests, broken rocks, bosses, keys). Pick the same character as last time to get your class, weapon, armor and
herbs back. On the start screen, **hold N** for a new game. (The save is in
`%APPDATA%\Godot\app_userdata\Lizard Wizard\save.json`.)

**Enemies come back:** leave a room and come back in, and its enemies are there again. Bosses stay beaten,
and doors and chests stay open.

**Ring menu:** opens around your hero and **doesn't pause the game**. You can still walk (at half speed if an
enemy is after you) but can't attack. Previous / next turn the ring, special switches rings
(colours or bombs → armor → items), primary picks, secondary or menu closes it.

**Bombs (fox):** bomb, smoke (enemies can't see you in it), sticky (sticks to an enemy), time (long fuse,
bigger blast), remote (waits on the ground; Y sets off all of them at once, so you can lay a few) and proximity (goes off when an enemy walks near).
The fox has all of these from the start; **bosses will unlock new ones** (boss drops still to be designed).
Locked ones show as "???" in the ring menu.

**Armor (one slot):** shadow cloak (lizard: stay hidden while walking), ember scales (lizard: resist fire while
hidden), bomb vest (fox: one more bomb out at a time), sturdy vest (anyone: +2 hearts).

### The overworld: Scalebrook Meadow
The game starts outside in **Scalebrook Meadow** (it's in `game/scenes/dungeons/dungeon_1.tscn`, as the
"Overworld" area, next to the dungeon rooms). There are deer, a pond, signs, and a dirt path up to the grassy
stairs of **The Old Burrow**. Walk onto the stairs to go down into the dungeon; the dungeon's first room has a
door at the bottom that leads back out. After beating the boss, an **exit portal** opens in the boss room: stand on it for a second to go back out to the
temple gate. It stays open, so you can look around the dungeon first (or come back to it later).

### The first dungeon: The Old Burrow
Walking through a door takes the whole team to the next room.
1. **Entrance Hall:** beat the deer and the door opens.
2. **Key Hall:** the north door is locked. There's a chest of herbs, and a door to the east.
3. **Side Room:** beat the deer and a chest with the key appears.
4. **Pit of Two Levers:** a bridge comes out when both levers are on. One is across the pit, where only the
   lizard's tongue reaches. The other is behind cracked rocks that only the fox's bombs can break.
   (Playing alone? Hold the leave button and rejoin as the other character. Levers stay on.)
5. **Beetle's Lair:** the Giant Beetle. Its shell shrugs off hits ("clank"). Dodge its charge so it runs into
   a wall, then hit it while it's dizzy.

The old test room is still at `game/scenes/rooms/test_room.tscn` (open it in Godot and press F6 to play it).

### Dev helpers
- `Godot_console.exe --path game -- --screenshot=out.png` saves a screenshot after half a second and quits.
  Add `--screenshot-world` to hide the HUD and menus, and `--room=PuzzleRoom` to start in another room.
- `python tools/kenney_art.py` then `python tools/make_room_tileset.py` rebuild the world art from the Kenney packs.
- `Godot_console.exe --path game -- --autoplay=<folder>` lets two bots (P1 on WASD as the lizard, P2 on the
  arrows as the fox) play by pressing the real keys: they clear the meadow, go down the stairs, play the dungeon to the boss, and take the exit portal back outside. It logs everything and saves a
  screenshot every 2 seconds.
- In the test room (`res://scenes/rooms/test_room.tscn` before the `--`), add `--autoplay-pick=2,3` after the `--`
  to make the bots play other characters (0 lizard, 1 fox, 2 moose, 3 peacock, 4 beetle).
- Add `"--autoplay-classes=Illusionist,Weapon Master"` (test room) to paint the bots into those classes and run a
  scripted check of their moves first (block, javelin, spin, explosive arrow, charm, clones, tail decoy).
  `--autoplay-pick=2,1 --autoplay-classes=Summoner` checks the moose's summons (wait, limit of 4, B switch,
  stampede, totem healing, treant drawing the deer).
  `--autoplay-pick=4,3 "--autoplay-classes=Roller,Grappler"` checks the beetle (roll, shell, tough shell, bounce)
  and the peacock (fire feathers, grapple pull, rocket, zip to a wall).
- `Godot_console.exe --path game -- --autoplay-grapple=<folder>` checks the meadow's secret chest (add
  `--room=Swamp` for the swamp's): the peacock can't walk over the water or round the side, grapples over to the chest
  and back, and lands back on the shore when stopped mid-flight.
- `Godot_console.exe --path game -- --autoplay-tour=<folder>` walks through every path between the overworld areas,
  there and back, and checks each one leads to the right area (and that the village painter paints you).
- New overworld areas are `DungeonRoom`s with an `OutdoorBuilder` layout (Ground: meadow, forest, swamp or snow);
  a `Door` with Path on the area's edge makes a gap in the border, and `DirtPath` nodes draw extra paths.
- Bot runs never touch the real save. Add `--save-file=user://test_save.json` to let them save to a test file.
- `Godot_console.exe --path game res://scenes/rooms/test_room.tscn -- --autoplay=<folder>` runs the test-room bots
  instead: they join and pick characters, test camouflage and colour change, test leaving mid-chase, use the ring
  menu mid-fight, try the bomb types, clear the room, walk apart to test the camera, then P2 leaves.

The C# project is `game/LizardWizard.csproj`, which targets .NET 8. It can also be opened in Visual Studio or Rider using `game/LizardWizard.sln`.
If you upgrade Godot, change the `Godot.NET.Sdk/4.7.2` version in the `.csproj` to the new editor version.

## Credits
- **Design:** the designer (6th grade) and dad
- **World art:** *Tiny Dungeon*, *Tiny Town*, *Tiny Battle*, *Tiny Farm* and *Tiny Ski* by **Kenney** ([kenney.nl](https://www.kenney.nl), CC0)
- **Palette:** *Rosy 42* by **@PineTreePizza**
- **Chameleon sprite:** pixelled by Claude from a Grok-generated reference

Full details and licenses: [game/assets/CREDITS.md](game/assets/CREDITS.md).
The game will also get an in-game credits screen (milestone M6).

## Running older commits
Early versions used art we're not allowed to share, so it was taken out of the git history. To run a commit from
before that art was replaced, add it back yourself (and don't commit it):

- **Before "Replace the Tiny Dungeon art with Kenney's CC0 Tiny packs":** buy the *Tiny Dungeon Pack* by
  [Vryell](https://vryell.itch.io) and unzip it into `game/assets/third_party/tiny_dungeon/`, so that you get
  `game/assets/third_party/tiny_dungeon/Misc/`, `.../Tilesets/`, `.../Weapons/` and so on.
- **Before "Replace the third-party deer art with our own walk cycle":** download
  [*deer walk & running cycle*](https://babisaur.itch.io/assets) by babisaur and put its small walk strip
  (8 frames of 40x42) in `game/assets/third_party/deer/` as `deer-walk-32x32.png`.

Then open the project in Godot once so it imports the files.
