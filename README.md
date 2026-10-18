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
Press a button to join, pick a character with left / right and its class with up / down, and confirm with the
primary button. The team starts in **Scalebrook Village**.
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
- The moose's other class is the **Tinkerer** (goggles), who works with **monster parts** (every beaten enemy drops
  its parts, below; they go in the team's bag).
  A antler bash, X **fuses** a part onto the closest teammate's main attack or armor (or the moose's own if nobody is
  close), Tears of the Kingdom style, and LB / RB pick the part and whether it goes on the weapon or the armor.
  Y **crafts** armor for the whole team (pick it in the Start menu's Craft ring): **Hide Vest** (2 hide, +1 heart),
  **Antler Helm** (2 antlers + 1 hide, +2 hearts), **Shell Plate** (1 shell + 2 hide, +3 hearts), **Fur Coat**
  (3 fur, +2 hearts), **Scale Mail** (3 scales + 1 tusk, +4 hearts).
  | Part | Dropped by | Fused onto a weapon or gun | Fused onto armor |
  |---|---|---|---|
  | Antler | deer | +2 damage | +1 heart |
  | Hide | deer | faster attacks | walk faster |
  | Beetle shell | Giant Beetle | +4 damage | +2 hearts |
  | Tusk | boar | +3 damage | +1 heart |
  | Fur | arctic fox | faster attacks | +1 heart |
  | Frog skin | frog | hits poison | walk faster |
  | Scale | snake | +2 damage | +2 hearts |

  Every hero has **3 fuse slots** each for guns, weapons and armor (one more of each for every boss beaten); a main
  attack that shoots counts as a gun. Switch between your fused weapons / guns and armor in the **Start** ring menu.
  When the slots are full, a new fusion replaces the one you're using.
- The beetle can also be a **Vehicle Master** (aviator cap): LB / RB pick a vehicle, X **builds it** and hops in to
  drive (X next to it climbs back in; far from it, or with another vehicle picked, a new one is built right there).
  Driving: the stick steers, A does the vehicle's trick, X hops out. Teammates climb on by walking up and pressing A,
  keep using their own attacks from the deck, and hop off with dodge (not over water or a hole). Hits on anyone riding
  hit the vehicle instead (it has its own health bar); when it breaks everyone tumbles off (back onto dry land), and the
  beetle needs 10 seconds before building another. Going through a door packs the vehicle away.

  | Vehicle | Seats | Speed | A (driver) | Good for |
  |---|---|---|---|---|
  | Tank | 4 | slow | cannon (explosive shells, breaks cracked rocks) | fighting; toughest |
  | Cart | 4 | fast | speed boost | rams enemies it drives into |
  | Boat | 4 | fast on water, slow on land | – | crossing water (not holes) |
  | Glider | 4 | medium | – | flying over water and holes for 8 seconds (a blue bar), then it lands |
  | Plane | 2 | very fast | machine gun | flying over water and holes as long as you like |
  | Hoversled | 4 | very fast on ice, normal on water, slow on land | – | a hovercraft snowmobile for frozen lakes and icy floors; **unlocked by beating Frostfang Fortress** |
- The **beetle** starts as a **Roller**: A horn jab, X **rolls** (a fast ball that rams enemies and bounces off
  walls; steer it a little; hits hurt half as much while rolling), Y **hides in its shell** (blocks every hit for a
  moment), and its tough shell always makes hits hurt less. Its other class is the **Vehicle Master** (above).
- The **peacock** starts as a **Grappler**: A shoots a fan of three **feathers** (LB / RB pick normal, poison,
  fire, ice, lightning or arcane), X fires the **grapple gun**, Y fires an **RPG** (a rocket with a big explosion
  that also breaks cracked rocks). The grapple **reels in** small enemies, **pulls you over** to big ones (bosses)
  and to **walls, rocks and bushes**, flying you over **water and pits** on the way. If something hits you
  mid-flight over water, you land back where you started. Its other class, **Warrior**, shoots a plain gun.
- The peacock can also be a **Thief** (bandit mask): A shoots its gun, X **steals** from the enemy right in front
  of you, and LB / RB pick what to steal: **gold** (coins for the team), **gear** (a healing herb, for now) or its
  **ability**. Only enemies that are awake can be robbed, each one once of each thing. A stolen ability goes on Y and
  you keep it (it never runs out, and the save remembers it): a deer gives you the **Deer Kick** (a big kick), the
  Giant Beetle the **Beetle Charge** (a dash that rams everything in the way). Stealing another enemy's ability swaps it.
- **Secret:** in Scalebrook Meadow's top-left corner there's a chest across the water that only a grapple can reach.

**Scalebrook Village:** walk up into a shop's door to go in. Each shop has a shopkeeper behind the counter who
says hello; step up to the counter for a moment to buy (coins come out of the team's purse):
- **Scalebrook Inn** (top left): a rest fills your hearts, 5 coins.
- **Herb Shop** (top middle): a healing herb, 10 coins.
- **Paint Shop** (top right): stand on the paint mat to change class (the painter's mat outside still works too, for
  playtesting).
- **Weapons & Armor** (bottom left): **weapon upgrades for the whole team**: every hero's main attack (A) hits
  harder. Wood → **Copper** (20 coins, +1 damage) → **Iron** (40, +2) → **Steel** (80, +3) → **Mithril** (160, +5).
  The counter's sign shows the next one; the attack's name shows it ("Iron Punch"), and so does the top of the screen.

The path north leads to **Scalebrook Castle**. Its **Castle Hall** has knights on guard (some stand, some walk
about); the top door goes to the **Throne Room** (the king and queen, and four knights; the king, the queen or both
welcome you), the right door to the **Castle Kitchen** (a cook, and a free herb on the table, once), and the left door
to the **armory**, which stays locked for now (a quest will open it). Villagers wander around the village.

**Frostfang Fortress** (the second dungeon): the temple gate in Frosttail Tundra's bottom-right corner. Inside: the
**Frozen Gate** (arctic foxes; the door opens once they're beaten), the **Ice Hall** (on ice you slide until you bump
into something, and the key is out on the ice: plan your slides), the **Frozen River** (hit the lever to make a bridge;
the door on is locked, so bring the key) and **the Frost Den**, where two bosses fight together: **Frostmane the
Moose** stomps the floor, sending out a ring of frost that slows you down (dodge through it!), and **Tusker the Boar**
charges whoever is slowed. Beat one and the other gets angry. Beating both gives a boss coin, one more fuse slot of each
kind, the title Knight, the beetle's **Hoversled**, and a portal back out to the tundra.

**Enemies:** animals tricked by Mossimus. Beat one and it falls asleep, then thanks you once the area is clear.
They come in mixed groups, and come back when you leave an area and return.

| Enemy | Where | How it fights |
|---|---|---|
| **Deer** | everywhere | walks up, flashes yellow, kicks (dodge!) |
| **Boar** | forest, canyon | the toughest: paws the ground (flashing), then charges in a straight line; after a charge it stops to catch its breath |
| **Arctic fox** | tundra (in packs) | fast: darts in, bites, runs off, comes back |
| **Frog** | swamp, lake, canyon | keeps its distance, hopping about, and spits slime blobs |
| **Snake** | meadow, forest, canyon, swamp, lake | hard to see in the grass until you're close; poison bite (keeps hurting for a few seconds) |

The Thief can steal their moves too: **Boar Charge**, **Fox Pounce** (a short, fast dash), **Frog Spit** (a poison
blob) and **Poison Bite**.

**Loot:** a beaten enemy drops something: usually a few **coins** (8 times out of 10), otherwise a **healing herb** (later
also weapons), and always each of its **monster parts** (a deer: an antler and a hide). Walk over it to pick it up. Coins go into the team's purse (top middle of the screen, kept in
the save); there's nothing to buy yet. The boss drops five things: a big **boss coin** (with a red star; the team's
boss coins show next to the coins) and four ordinary drops. Loot disappears after 30 seconds.

**The overworld:** the team starts in Scalebrook Village. Scalebrook Meadow is in the middle, with the stone temple gate into The Old Burrow at the top
right (walk in through the arch), and paths out through gaps in the bushes (signs show the way):
west to **Scalebrook Village** (shops and the castle, no enemies), east to **Antlerwood Forest** (thick trees, deer,
chests in clearings), from there north to **Frosttail Tundra** (snow, snowmen, two frozen lakes) and east to
**Red Rock Canyon** (a long chasm with one rock bridge, and a high ledge with a chest that only flying or a grapple
reaches), and south to **Tailspin Swamp** (murky ponds, bare trees, a chest only a grapple can reach) and on south to
**Glimmer Lake** (a sandy beach and a wide lake; a chest waits on the far shore: take the boat, fly, or grapple to a
bush). The forest, tundra and swamp are big: the camera scrolls around them.

**Classes (getting painted):** the lizard can be a **Chameleon** (camouflage, colour change), **Tongue Master**
(stronger, longer tongue), **Spitter** (special button spits poison; LB / RB pick poison, fire, ice, lightning
or arcane) or **Illusionist** (below). The fox can be a **Bomb Master**, **Gun Master** (shoots; LB / RB pick the
element) or **Weapon Master** (below). To change class, visit the **painter in Scalebrook Village**: stand on
its mat for a moment to switch to your next class (step off and on again for the one after). Each class has its
own look: the Tongue Master wears a red headband, the Spitter is teal, the Illusionist wears a wizard hat, the Gun
Master a cowboy hat and the peacock Warrior a knight's helmet (made by
`python tools/class_looks.py`).

**Weapon Master (fox):** LB / RB switch weapons. A attacks with the weapon (instead of biting), X does its trick:

| Weapon | A | X | Y |
|---|---|---|---|
| Sword & Shield | Slash | Raise the shield: blocks any attack for a moment | – |
| Javelin | Long jab | Lob the javelin in a high arc: it flies over things close by and lands on the enemy in front of you | – |
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
- `Godot_console.exe --path game -- --room=Overworld --autoplay-grapple=<folder>` checks the meadow's secret chest
  (`--room=Swamp` for the swamp's): the peacock can't walk over the water or round the side, grapples over to the chest
  and back, and lands back on the shore when stopped mid-flight.
- `Godot_console.exe --path game -- --room=FrostEntrance --autoplay=<folder>` plays Frostfang Fortress through (the
  bots are put on the ice key; they can't plan slides) and the boss fight, and goes out through the portal.
  `--room=IceHall --autoplay-ice=<folder>` solves the Ice Hall's slide puzzle with the real keys.
- `Godot_console.exe --path game -- --room=Lake --autoplay-beasts=<folder>` puts each kind of enemy next to the fox in
  turn and logs whether its attack landed and what it dropped.
- `Godot_console.exe --path game -- --room=Village --screenshot-showcase=village:<file.png>` takes a picture to
  share: four heroes in a scene. Scenes (with their room): `village` (Village), `throne` (ThroneRoom), `battle`
  (Forest), `lake` (Lake), `canyon` (Canyon), `boss` (BossRoom).
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
