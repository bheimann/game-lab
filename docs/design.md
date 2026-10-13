# Game design (working draft)

Title: **Lizard Wizard** (chosen by the designer)

## The pitch
A top-down action adventure for 1–4 players on one screen. Each player is a different
kind of lizard with its own special powers. Dungeons are built so that lizards have to
work together, because some paths need one lizard's power and some need another's.
Menus never pause the game: when one player is swapping spells, everyone else keeps playing.

Inspired by: Cat Quest 2/3 (feel, simple RPG), Secret of Mana (real-time menus, co-op),
Zelda / Four Swords (dungeons, puzzles, co-op).

## Rules we've decided
- **Local co-op only.** One computer, one screen, one controller (or the keyboard) per player. No online play.
- **Shared camera.** It zooms out to keep every player in view, up to a limit. Players can't walk off-screen.
- **Drop in / drop out.** Press a button on a controller to join the game.
- **Menus don't pause.** The game keeps running for everyone. Only the player with the menu open
  is affected: **if they're in a fight, their lizard slows down**; otherwise it moves at normal speed.
- **Don't make players open the menu if we can help it.** Common menu actions (switching powers,
  using a potion, and so on) should also have a button or quick-swap, so the menu is for managing
  gear, not for surviving a fight.
- **Art:** free pixel-art packs for now (16×16 tiles). We might switch to hand-drawn later.

## The world: five kingdoms
| Kingdom | Example types | Possible area |
|---|---|---|
| 🦎 **Lizard** | (the players' home) | ? |
| 🦌 **Deer** | White-tailed deer, reindeer | Forest; reindeer in the snow |
| 🦊 **Fox** | Red fox, arctic fox | Woods/hills; arctic foxes in the snow |
| 🐦 **Bird** | ? | ? |
| 🐛 **Bug** | Beetles, ? | ? |

- Each kingdom has a few types of its animal, and a type can live in a different area from the rest
  of its kingdom (e.g. **a snow area with arctic foxes and reindeer**).
- The "Possible area" column is Claude's guess. The designer decides.

## Lizards
| Lizard | Main power | Other ideas | Status |
|---|---|---|---|
| **Chameleon** | Camouflage (see below) | Long tongue that grabs items, pulls enemies, swings from posts | **First lizard** |
| Gecko | Climbs walls | Sticky feet, drops onto enemies from above | Idea |
| Frilled lizard | Frill flare that scares or stuns enemies | | Idea |
| Komodo dragon | Tank with a poison bite | | Idea |
| Basilisk | Runs on water, very fast | | Idea |
| Horned lizard | Shoots blood from its eyes, spikes for defence | | Idea |

### Chameleon
- **Camouflage:** while the chameleon **stands still** it's hidden and enemies can't see it.
  **Moving** makes it visible again.
- **Armor changes how camouflage works.** For example:
  - *Fire armor:* changing colour also gives **fire resistance**.
  - *Shadow armor:* the chameleon stays **invisible even while moving**.
- **Tongue:** a weak attack that does **1 damage**. It's also a tool (grab, pull, swing) for later puzzles.

## Enemies
- **Regular enemies are animals, starting with deer.** Deer are about twice as tall as a lizard,
  so even ordinary enemies feel big. Our deer art only faces sideways; we flip it to face
  left or right and use it for walking up and down too.
- **Bosses are different animals**, e.g. a giant beetle from the bug kingdom.
- There isn't much free animal pixel art, so expect to draw some of these ourselves.
  The Tiny Dungeon pack's slimes, bats, rats and skeletons can fill gaps.

## Questions for the designer
- Why are the lizards fighting the other kingdoms? Is one kingdom the bad guy, or is someone
  causing trouble for everyone?
- Are all the other kingdoms enemies, or are some friendly (shops, helpers, quests)?
- What types live in the bird and bug kingdoms?
- Where is the lizard kingdom? (desert, jungle, swamp, volcano?)
- Which lizard comes second?
- How much health do the lizards and the deer have? (The tongue does 1 damage, so a deer with 3 health takes 3 hits.)
