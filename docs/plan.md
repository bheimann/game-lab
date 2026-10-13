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
- 🦎 Chameleon facing up and sideways, plus walk frames. Draw them in [Piskel](https://www.piskelapp.com), or describe them and Claude will draw them.
- ✅ 🦎 Choose a name for the game: **Lizard Wizard**.

**Done when:** pressing ▶ in Godot opens a window showing the art pack's tiles.

## M1: One chameleon in one room
- 🛠 8-direction movement with keyboard and controller.
- 🛠 Punch attack (5 damage) and tongue attack (longer reach, 1 damage).
- 🛠 Health, taking damage, and respawning.
- 🛠 One simple enemy (a deer, 10–15 health) that walks toward the player once it sees them.
- 🛠 Beaten deer fall asleep. When no enemies are left awake, they wake up and say thank you.
- 🦎 Paint the first room in the tilemap editor.
- 🦎 Tune movement speed, tongue range and enemy speed until it *feels* good.

**Done when:** you can run around a room and fight deer.

## M2: Second player
- 🛠 Per-player input: each controller and the keyboard is its own player.
  (Godot's input actions are shared by default, so we'll wrap them in a small `PlayerInput` class that tracks which device belongs to which player.)
- 🛠 Press a button to join.
- 🛠 Shared camera that zooms to fit everyone, and stops players walking off-screen.
- 🦎 Decide whether players can hurt each other (please say no 😄).

**Done when:** father and son can play in the same room at the same time.

## M3: Powers
- 🛠 A data-driven ability system: each power is a Godot `Resource` with settings
  (cooldown, duration, energy cost) that can be edited in the inspector without code.
- 🛠 Chameleon camouflage: hidden while standing still, seen again when moving. Enemies lose track of hidden lizards.
- 🛠 The second character: the arctic fox, as a **Bomb Master** (its starting mastery).
- ✅ 🦎 Pick the second character: **the fox**.
- ✅ 🦎 Decide how the fox's masteries work: one fox that gets painted in a village to switch.
  (Painting and the other masteries come later, after the first dungeon.)
- 🦎 Balance cooldowns and durations.

**Done when:** the chameleon and the fox feel different to play.

## M4: The menu that doesn't pause
- 🛠 A ring menu for each player (like Secret of Mana's) that opens next to that player's lizard.
- 🛠 Switch powers and items while the game keeps running.
- 🛠 Equipment slots (one or two to start). Armor changes how camouflage works (e.g. fire resistance, or invisible while moving).
- ✅ 🦎 Decide what happens to your lizard while your menu is open: it slows down only if you're in a fight.
- 🛠 Quick buttons for common actions, so you rarely need the menu mid-fight.
- 🦎 Design 3–4 items or equipment pieces.

**Done when:** one player can change their gear mid-fight while the other keeps fighting.

## M5: First dungeon
- 🛠 Moving between rooms, doors, keys, and switches.
- 🛠 A puzzle that needs both lizards' powers.
- 🛠 A small boss.
- 🦎 Draw the dungeon map on paper first, then build it in the editor.
- 🦎 Design the boss (a different animal, e.g. a giant beetle).

**Done when:** you can play from the start of the dungeon to beating the boss, together.

## M6: Polish and share
- 🛠 Title screen, credits screen, sound effects, music.
- 🛠 Export to a standalone `.exe` (needs Godot's export templates, about 1 GB).
- 🦎 Playtest with friends and write down what confuses them.

**Done when:** a friend can play it on their own computer.

---

## Later (once the first dungeon is fun)
More lizards so there are four, an overworld map, levelling up, saving the game,
more dungeons, hand-drawn art, and maybe split-screen.

## How we work with Claude
1. Pick one item from the current milestone.
2. The designer describes what it should do and feel like.
3. Claude writes the code; dad reviews it and runs it.
4. Playtest together, then adjust values or ask Claude for changes.
5. Commit when it works.
