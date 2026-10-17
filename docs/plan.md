# Build plan

Engine: **Godot 4, .NET edition**, with gameplay code in C#.
Each milestone ends with something you can actually play. Don't start the next
milestone until the current one is fun (or at least working).

Each step has a 🦎 **Designer** job for the 6th grader and a 🛠 **Code** job
(dad + Claude).

---

## M0: Setup
- ✅ 🛠 Download the Godot .NET build (4.7.2) and unzip it to `C:\Dev\tools\Godot\Godot_v4.7.2-stable_mono_win64\` (no installer).
- ✅ 🛠 Create the Godot project in `game/`, with a `.gitignore` for `.godot/`, and get a test scene running.
- ✅ 🛠 Check that C# builds and runs. Art check scene: a room built from the tileset, torches, a chest, the stand-in chameleon, and a walking deer.
- ✅ 🦎 Choose a pixel-art pack: Vryell's *Tiny Dungeon Pack* (rooms, items, small enemies) plus deer.
- ✅ Chameleon sprite, facing down (16×28, hand-pixelled from `chameleon-humanoid.jpg`).
- ✅ Chameleon facing up and sideways, plus 2-step walk cycles for each view (`art-source/chameleon/*.txt`).
- ✅ 🦎 Choose a name for the game: **Lizard Wizard**.

**Done when:** pressing ▶ in Godot opens a window showing the art pack's tiles.

## M1: One lizard in one room
- ✅ 🛠 8-direction movement with keyboard and controller. (Controller support is untested so far.)
- ✅ 🛠 Punch attack (5 damage) and tongue attack (longer reach, 1 damage, aim assist).
- ✅ 🛠 Health (hearts), taking damage with knockback and a short flicker, getting back up at the start when knocked out.
- ✅ 🛠 Deer (10–15 health) wander, notice a lizard within sight, chase it, and kick after a yellow wind-up flash.
  A deer that's winding up can't be interrupted, so you have to dodge.
- ✅ 🛠 Beaten deer fall asleep. When no enemies are left awake, they wake up and say thank you.
- 🦎 Paint the first room in the tilemap editor (open `scenes/rooms/test_room.tscn`, select `Room/Floor` or `Room/Walls`).
  If both layers are empty, the room builds itself when the game starts.
- 🦎 Tune the numbers until it *feels* good: click `Lizard` or a `Deer` in the scene and change speed, damage,
  health, sight range, wind-up time and so on in the inspector.
- 🦎 Playtest! Write down what's fun, what's annoying, and what's confusing.

**Done when:** you can run around a room and fight deer.

## M2: Second player
- ✅ 🛠 Per-player input: each controller is its own player, and the keyboard splits in two (WASD side and arrows side).
- ✅ 🛠 Press a button to join (up to 4 players); hold Back / Backspace / Delete for a second to leave.
  Unplugging a controller also removes that player.
- ✅ 🛠 Shared camera: zooms from 2× (together) out to 1× (spread out), never shows past the room's edges,
  and stops players walking off-screen once it's fully zoomed out.
- ✅ 🛠 Player colours (P1 blue, P2 red, P3 yellow, P4 pink): a ring at the feet, an arrow over the head, and a HUD corner each.
- ✅ 🛠 A knocked-out lizard gets back up next to a teammate who's still standing.
- ✅ 🦎 Decide whether players can hurt each other: **no**.

**Done when:** father and son can play in the same room at the same time.

## M3: Powers
- ✅ 🛠 Ability system: every power is a child node of the character (`scripts/Abilities/`), with its numbers
  (damage, range, cooldown, ...) editable in the inspector. A character = `Hero` + a set of abilities,
  so changing class later (painting) means swapping ability nodes.
- ✅ 🛠 Three buttons: primary (A / J / .), secondary (X / K / /), special (Y / L / ,).
- ✅ 🛠 Lizard (Chameleon class): punch, tongue, **camouflage** (stand still ~0.6 s to fade out; deer lose track
  and show "?"; moving, attacking or getting hit reveals you), **colour change** (cycles blue/red/yellow/purple;
  half damage from ice/fire/lightning/arcane; no elemental enemies yet).
- ✅ 🛠 Fox (Bomb Master; a red fox, first drawn as an arctic fox): bite (4 damage) and **bombs** (thrown in an arc, 1 s fuse, 8 damage in a blast,
  max 2 out at once, a little screen shake).
- ✅ 🛠 Pick your character when you join: left / right, then A.
- ✅ 🦎 Pick the second character: **the fox**.
- ✅ 🦎 Decide how the fox's masteries work: one fox that gets painted in a village to switch.
  (Painting and the other masteries come later, after the first dungeon.)
- 🦎 Balance cooldowns, damage and how long you have to stand still to hide.
- 🦎 Look at the fox and say what to change (ears, tail, face...).

**Done when:** the chameleon and the fox feel different to play.

## M4: The menu that doesn't pause ✅
- ✅ 🛠 A ring menu for each player (like Secret of Mana's) that opens around that player's hero:
  Start / Tab / Enter. Rings: colours or bombs, armor, items.
- ✅ 🛠 Switch powers and items while the game keeps running. While your menu is open you can walk but not attack.
- ✅ 🛠 One armor slot. Armor changes how powers work: **shadow cloak** (stay hidden while walking),
  **ember scales** (resist fire while hidden), **bomb vest** (one more bomb), **sturdy vest** (+2 hearts).
- ✅ 🦎 Decide what happens to your lizard while your menu is open: it slows down only if you're in a fight.
- ✅ 🛠 Quick buttons: LB / RB (Q / E, ; / ') pick the bomb type or colour, B (I, M) eats a healing herb.
- ✅ 🛠 Fox bomb types: bomb, smoke, sticky, time, remote, proximity.
- 🦎 Design more items and armor, and say which bomb types are there from the start and which are found or bought.

**Done when:** one player can change their gear mid-fight while the other keeps fighting.

## M5: First dungeon ✅
- ✅ 🛠 Moving between rooms (the whole team goes through together), doors (open, locked, opening when the
  room is cleared or when levers are on), keys (shared by the team), levers, chests, pits and a bridge.
- ✅ 🛠 A puzzle that needs both characters' powers: the lizard's tongue reaches a lever across a pit, and the
  fox's bombs break the rocks in front of the other lever.
- ✅ 🛠 A small boss: the **Giant Beetle**. It charges, gets dizzy when it hits a wall, and has a shell that only
  dizzy hits get through. It has more health with more players.
- ✅ 🛠 The first dungeon, "The Old Burrow": 5 rooms, from the entrance to the boss.
- 🦎 Draw the dungeon map on paper first, then build it in the editor. (A first version is built; redesign it as you like!)
- 🦎 Design the boss (a different animal, e.g. a giant beetle). (A first beetle is in; change its moves, look and name.)

**Done when:** you can play from the start of the dungeon to beating the boss, together.

## M6: Polish and share
- 🛠 Title screen, credits screen, sound effects, music.
- 🛠 Export to a standalone `.exe` (needs Godot's export templates, about 1 GB).
- 🦎 Playtest with friends and write down what confuses them.

**Done when:** a friend can play it on their own computer.

---

## After M5: extras the designer asked for
- ✅ 🛠 The tongue sticks to enemies and pulls them in (bosses are too heavy).
- ✅ 🛠 Dodge for every hero (RT / Shift / Ctrl), with a short moment where nothing can hurt you.
- ✅ 🛠 Three more playable heroes, warriors the team helped: moose, peacock (with a gun) and beetle.
- ✅ 🛠 Classes: lizard (Chameleon, Tongue Master, Spitter) and fox (Bomb Master, Gun Master, Weapon Master),
  with a temporary test painter in the dungeon's first room (later replaced by the village painter).
- ✅ 🛠 Elements: poison and fire hurt over time, ice slows, lightning and arcane hit harder.
- ✅ 🛠 Bosses can unlock new bombs, weapons and guns (the boss drops are still to be designed).
- ✅ 🛠 Enemies come back when you leave a room and return.
- ✅ 🛠 A first overworld area, Scalebrook Meadow, with the stairs down into The Old Burrow.
- ✅ 🛠 Weapon Master weapons: sword & shield, javelin, flail, axe & bow (with 7 arrow types).
- ✅ 🛠 The lizard's Illusionist class: clones, tail decoy and charm, using illusionist energy.
- ✅ 🛠 Saving: the game saves by itself and picks up where you left off; hold N for a new game.
- ✅ 🛠 Beetle **Roller** class (roll, shell block, tough shell) and peacock **Grappler** class (elemental
  feathers, grapple gun, RPG), plus a secret chest in the meadow that only the grapple reaches.
- ✅ 🛠 Moose **Summoner** class: six light summons on X, treant or stampede on Y (B switches), up to 4 at once.
- ✅ 🛠 A bigger overworld: Scalebrook Village (with a painter), Antlerwood Forest, Frosttail Tundra and Tailspin
  Swamp around the meadow, joined by paths.
- ✅ 🛠 The village painter replaces the dungeon's test painter, The Old Burrow gets a temple gate, and beating
  the boss opens an exit portal (stand on it for a second) instead of taking the team out straight away.
- ✅ 🛠 The team starts in Scalebrook Village, picks a class when joining (up / down), and every class has its
  own look (hats, headband, colours).
- ✅ 🛠 Loot: beaten enemies drop coins (80%) or a healing herb (20%), and the team has a purse. The peacock's
  **Thief** class steals gold, gear and abilities (Deer Kick, Beetle Charge).
- 🦎 Design the boss drops and what goes in the new areas.
- ✅ 🛠 A real village: an inn (rest, 5 coins), a herb shop (10 coins), a paint shop, a weapons & armor shop (not
  open yet), shopkeepers who greet you, villagers, and Scalebrook Castle (hall with knights, throne room with the king
  and queen, kitchen with a free herb, and a locked armory).
- ✅ 🛠 Monster parts (antlers, hide, beetle shell) as loot, and the moose **Tinkerer**: fuses parts onto any hero's
  weapon, gun or armor (3 slots each, +1 per boss) and crafts armor for the team.
- ✅ 🛠 Beetle **Vehicle Master**: builds a tank the whole team can ride (the beetle drives and fires the cannon,
  passengers keep attacking).
- 🦎 More vehicles (a boat for water? a glider?).
- 🦎 What coins buy (a shop in the village?), and weapons as loot once there are more weapons.
- 🦎 The king and queen's quests (one of them opens the armory), what the weapons & armor shop sells, and unlocking the
  first class in the paint shop (then the painter's mat outside goes away).
- 🦎 A start in the village with a story: a mini tutorial and the king, ending with each hero unlocking their first
  class, before they leave the village. Until then everyone can pick any class when joining.

## Later (once the first dungeon is fun)
More lizards so there are four, more of the overworld, levelling up,
more dungeons, hand-drawn art, and maybe split-screen.

## How we work with Claude
1. Pick one item from the current milestone.
2. The designer describes what it should do and feel like.
3. Claude writes the code; dad reviews it and runs it.
4. Playtest together, then adjust values or ask Claude for changes.
5. Commit when it works.
