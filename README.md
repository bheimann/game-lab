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
| Secondary | K or G | / or Numpad 2 | X | Tongue | Throw bomb |
| Special | L or H | , or Numpad 3 | Y | Change colour | – |
| Previous / next | Q / E | ; / ' | LB / RB | Pick colour | Pick bomb type |
| Item | I | M or Numpad 0 | B | Eat a healing herb | Eat a healing herb |
| Menu | Tab | Enter | Start | Ring menu | Ring menu |
| Leave | hold Backspace | hold Delete | hold Back | | |

The lizard also hides (camouflage) by standing still for a moment.

**Ring menu:** opens around your hero and **doesn't pause the game**. You can still walk (at half speed if an
enemy is after you) but can't attack. Previous / next turn the ring, special switches rings
(colours or bombs → armor → items), primary picks, secondary or menu closes it.

**Bombs (fox):** bomb, smoke (enemies can't see you in it), sticky (sticks to an enemy), time (long fuse,
bigger blast), remote (press the bomb button again to set it off) and proximity (goes off when an enemy walks near).

**Armor (one slot):** shadow cloak (lizard: stay hidden while walking), ember scales (lizard: resist fire while
hidden), bomb vest (fox: one more bomb out at a time), sturdy vest (anyone: +2 hearts).

### The first dungeon: The Old Burrow
The game starts in the first dungeon (`game/scenes/dungeons/dungeon_1.tscn`). Walking through a door takes the
whole team to the next room.
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
- `Godot_console.exe --path game -- --autoplay=<folder>` lets two bots (P1 on WASD as the lizard, P2 on the
  arrows as the fox) play through the dungeon by pressing the real keys, from the entrance to beating the boss.
  It logs everything and saves a screenshot every 2 seconds.
- `Godot_console.exe --path game res://scenes/rooms/test_room.tscn -- --autoplay=<folder>` runs the test-room bots
  instead: they join and pick characters, test camouflage and colour change, test leaving mid-chase, use the ring
  menu mid-fight, try the bomb types, clear the room, walk apart to test the camera, then P2 leaves.

The C# project is `game/LizardWizard.csproj`, which targets .NET 8. It can also be opened in Visual Studio or Rider using `game/LizardWizard.sln`.
If you upgrade Godot, change the `Godot.NET.Sdk/4.7.2` version in the `.csproj` to the new editor version.

## Credits
- **Design:** the designer (6th grade) and dad
- **Dungeon art:** *Tiny Dungeon Pack* by **Vryell**, palette *Rosy 42* by **@PineTreePizza**
- **Deer art:** [*Deer Pixel Art*](https://mattar-productions.itch.io/deer-pixel-art) by **Ali Mattar (Mattar Productions)**; deer walk and run animations by [**babisaur**](https://babisaur.itch.io/assets)
- **Chameleon sprite:** pixelled by Claude from a Grok-generated reference

Full details and licenses: [game/assets/third_party/CREDITS.md](game/assets/third_party/CREDITS.md).
The game will also get an in-game credits screen (milestone M6).
