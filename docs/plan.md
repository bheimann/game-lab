# Build plan

Engine: **Godot 4, .NET edition**, with gameplay code in C#.
Each milestone ends with something you can actually play. Don't start the next
milestone until the current one is fun (or at least working).

Each step has a 🦎 **Designer** job for the 6th grader and a 🛠 **Code** job
(dad + Claude).

---

## M0: Setup
- 🛠 Download the Godot .NET build and unzip it to `C:\Dev\tools\godot` (no installer).
- 🛠 Create the Godot project in this repo, with a `.gitignore` for `.godot/`, and get an empty scene running.
- 🛠 Check that C# builds and runs from the Godot editor.
- 🦎 Choose a pixel-art pack (start with Kenney's free 16×16 packs, e.g. *Tiny Dungeon*;
  search itch.io for "free lizard sprite" to find the lizards).
- 🦎 Choose a name for the game.

**Done when:** pressing ▶ in Godot opens a window showing the art pack's tiles.

## M1: One chameleon in one room
- 🛠 8-direction movement with keyboard and controller.
- 🛠 Tongue attack (short range to start).
- 🛠 Health, taking damage, and respawning.
- 🛠 One simple enemy (a beetle?) that walks toward the player.
- 🦎 Paint the first room in the tilemap editor.
- 🦎 Tune movement speed, tongue range and enemy speed until it *feels* good.

**Done when:** you can run around a room and fight beetles.

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
- 🛠 Chameleon camouflage: enemies lose track of you while you're invisible.
- 🛠 The second lizard and its main power.
- 🦎 Pick the second lizard and design its power.
- 🦎 Balance cooldowns and durations.

**Done when:** the two lizards feel different to play.

## M4: The menu that doesn't pause
- 🛠 A ring menu for each player (like Secret of Mana's) that opens next to that player's lizard.
- 🛠 Switch powers and items while the game keeps running.
- 🛠 Equipment slots (one or two to start).
- 🦎 Decide what happens to your lizard while your menu is open.
- 🦎 Design 3–4 items or equipment pieces.

**Done when:** one player can change their gear mid-fight while the other keeps fighting.

## M5: First dungeon
- 🛠 Moving between rooms, doors, keys, and switches.
- 🛠 A puzzle that needs both lizards' powers.
- 🛠 A small boss.
- 🦎 Draw the dungeon map on paper first, then build it in the editor.
- 🦎 Design the boss.

**Done when:** you can play from the start of the dungeon to beating the boss, together.

## M6: Polish and share
- 🛠 Title screen, sound effects, music.
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
