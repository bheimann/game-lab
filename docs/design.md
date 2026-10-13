# Game design (working draft)

Title: **Lizard Wizard** (chosen by the designer)

## The pitch
A top-down action adventure for 1–4 players on one screen. Each player is a lizard or an ally
from a friendly kingdom, with their own special powers. Dungeons are built so that players have to
work together, because some paths need one character's power and some need another's.
Menus never pause the game: when one player is swapping spells, everyone else keeps playing.

Inspired by: Cat Quest 2/3 (feel, simple RPG), Secret of Mana (real-time menus, co-op),
Zelda / Four Swords (dungeons, puzzles, co-op).

## Story
- A **deer with bad intentions** is turning the kingdoms against the lizards. He's the **final boss**.
- The deer and bugs you fight **aren't bad**. They've been **deceived**, and the villain convinces more
  animals to join him as the game goes on.
- Other animals are **friendly**: they run shops and give quests.

## Rules we've decided
- **Local co-op only.** One computer, one screen, one controller (or the keyboard) per player. No online play.
- **Shared camera.** It zooms out to keep every player in view, up to a limit. Players can't walk off-screen.
- **Drop in / drop out.** Press a button on a controller to join the game.
- **Menus don't pause.** The game keeps running for everyone. Only the player with the menu open
  is affected: **if they're in a fight, their character slows down**; otherwise it moves at normal speed.
  - **"In a fight"** means an enemy can see you and is targeting you, even if nobody has been hit yet.
- **Don't make players open the menu if we can help it.** Common menu actions (switching powers,
  using a potion, and so on) should also have a button or quick-swap, so the menu is for managing
  gear, not for surviving a fight.
- **Art:** free pixel-art packs for now (16×16 tiles). We might switch to hand-drawn later.

## The world: five kingdoms
| Kingdom | Types | Possible area |
|---|---|---|
| 🦎 **Lizard** | Chameleon, … (the players' home) | ? |
| 🦌 **Deer** | White-tailed deer, reindeer | Forest; reindeer in the snow |
| 🦊 **Fox** | Red fox, arctic fox | Woods/hills; arctic foxes in the snow |
| 🦚 **Bird** | Peacocks | ? |
| 🪲 **Bug** | Beetles | ? |

- Some kingdoms have a few types of their animal, and a type can live somewhere different from
  the rest of its kingdom (e.g. **a snow area with arctic foxes and reindeer**).
- The "Possible area" column is Claude's guess. The designer decides.

## Combat numbers (starting values, to be tuned)
| | Damage | Health |
|---|---|---|
| Lizard **punch** | 5 | |
| Chameleon **tongue** | 1 | |
| Starting enemies (e.g. deer) | ? | 10–15 (2–3 punches) |
| Players | | ? (Claude will start with 20) |

## Playable characters
| Character | Kingdom | Main power | Status |
|---|---|---|---|
| **Chameleon** | Lizard | Camouflage (see below) | **First** |
| **Fox** | Fox | Comes as a Weapon Master, Gun Master or Bomb Master (see below) | **Second** |
| Gecko | Lizard | Climbs walls | Idea |
| Frilled lizard | Lizard | Frill flare that scares or stuns enemies | Idea |
| Komodo dragon | Lizard | Tank with a poison bite | Idea |
| Basilisk | Lizard | Runs on water, very fast | Idea |
| Horned lizard | Lizard | Shoots blood from its eyes, spikes for defence | Idea |

### Chameleon
- **Punch:** the main attack, **5 damage**.
- **Tongue:** a weak attack (**1 damage**) with a longer reach. It's also a tool (grab, pull, swing) for later puzzles.
- **Camouflage:** while the chameleon **stands still** it's hidden and enemies can't see it.
  **Moving** makes it visible again.
- **Armor changes how camouflage works.** For example:
  - *Fire armor:* changing colour also gives **fire resistance**.
  - *Shadow armor:* the chameleon stays **invisible even while moving**.

### Fox
Comes in three versions:
- **Weapon Master:** close-up fighting with swords, spears, etc.
- **Gun Master:** shoots from a distance.
- **Bomb Master:** throws bombs that hit groups of enemies and break walls (Tiny Dungeon has bomb art).

## Enemies
- **Regular enemies are animals, starting with deer.** Deer are about twice as tall as a lizard,
  so even ordinary enemies feel big. Our deer art only faces sideways; we flip it to face
  left or right and use it for walking up and down too.
- **Bosses are different animals**, e.g. a giant beetle from the bug kingdom.
  The **final boss is the villain deer**.
- There isn't much free animal pixel art, so expect to draw some of these ourselves.
  The Tiny Dungeon pack's slimes, bats, rats and skeletons can fill gaps.

## Questions for the designer
- **The fox's three versions:** are they three different characters to pick from, or one fox who can switch
  between them (e.g. by changing gear)?
- When you beat a deceived animal, does it get hurt, or does it **snap out of the trick** and run away
  (or even become a friend)? Since they're not bad, the second could be fun.
- Does the villain deer have a name?
- Where is the lizard kingdom? (desert, jungle, swamp, volcano?)
- Where do the peacocks and beetles live?
