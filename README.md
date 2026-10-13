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
| | Keyboard | Controller |
|---|---|---|
| Move | WASD or arrows | Left stick or d-pad |
| Punch | J or Space | A (bottom button) |
| Tongue | K | X (left button) |

### Dev helpers
- `Godot_console.exe --path game -- --screenshot=out.png` saves a screenshot after half a second and quits.
- `Godot_console.exe --path game -- --autoplay=<folder>` lets a bot play the test room: it presses the real keys,
  logs everything, saves a screenshot every 2 seconds, and quits when every deer has said thanks.

The C# project is `game/LizardWizard.csproj`, which targets .NET 8. It can also be opened in Visual Studio or Rider using `game/LizardWizard.sln`.
If you upgrade Godot, change the `Godot.NET.Sdk/4.7.2` version in the `.csproj` to the new editor version.

## Credits
- **Design:** the designer (6th grade) and dad
- **Dungeon art:** *Tiny Dungeon Pack* by **Vryell**, palette *Rosy 42* by **@PineTreePizza**
- **Deer art:** [*Deer Pixel Art*](https://mattar-productions.itch.io/deer-pixel-art) by **Ali Mattar (Mattar Productions)**; deer walk and run animations by [**babisaur**](https://babisaur.itch.io/assets)
- **Chameleon sprite:** pixelled by Claude from a Grok-generated reference

Full details and licenses: [game/assets/third_party/CREDITS.md](game/assets/third_party/CREDITS.md).
The game will also get an in-game credits screen (milestone M6).
