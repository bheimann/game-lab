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

| | Keyboard (left) | Keyboard (right) | Controller | Lizard | Arctic fox |
|---|---|---|---|---|---|
| Move | WASD | Arrows | Left stick or d-pad | | |
| Primary | J, Space or F | . or Numpad 1 | A | Punch | Bite |
| Secondary | K or G | / or Numpad 2 | X | Tongue | Throw bomb |
| Special | L or H | , or Numpad 3 | Y | Change colour | – |
| Leave | hold Backspace | hold Delete | hold Back | | |

The lizard also hides (camouflage) by standing still for a moment.

### Dev helpers
- `Godot_console.exe --path game -- --screenshot=out.png` saves a screenshot after half a second and quits.
- `Godot_console.exe --path game -- --autoplay=<folder>` lets two bots play the test room by pressing the real keys
  (P1 on WASD as the lizard, P2 on the arrows as the fox): they join and pick characters, test camouflage and
  colour change, clear the room, walk apart to test the camera, then P2 leaves.
  It logs everything and saves a screenshot every 2 seconds.

The C# project is `game/LizardWizard.csproj`, which targets .NET 8. It can also be opened in Visual Studio or Rider using `game/LizardWizard.sln`.
If you upgrade Godot, change the `Godot.NET.Sdk/4.7.2` version in the `.csproj` to the new editor version.

## Credits
- **Design:** the designer (6th grade) and dad
- **Dungeon art:** *Tiny Dungeon Pack* by **Vryell**, palette *Rosy 42* by **@PineTreePizza**
- **Deer art:** [*Deer Pixel Art*](https://mattar-productions.itch.io/deer-pixel-art) by **Ali Mattar (Mattar Productions)**; deer walk and run animations by [**babisaur**](https://babisaur.itch.io/assets)
- **Chameleon sprite:** pixelled by Claude from a Grok-generated reference

Full details and licenses: [game/assets/third_party/CREDITS.md](game/assets/third_party/CREDITS.md).
The game will also get an in-game credits screen (milestone M6).
