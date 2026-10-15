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
- The players are **ordinary lizards** who become **knights of the Lizard King** over the course of the game,
  earning **titles** as they go: each boss they defeat moves the story along and earns a new title.
- **Mossimus Maximus**, a deer with bad intentions, is turning the kingdoms against the lizards.
  He's the **final boss**.
- The deer and bugs you fight **aren't bad**. They've been **deceived**, and Mossimus convinces more
  animals to join him as the game goes on.
- **Beating a deceived animal breaks the trick.** It falls **asleep** until the area is clear
  (no enemies left awake), then wakes up and **thanks the heroes**.
- Other animals are **friendly**: they run shops and give quests.

## Rules we've decided
- **Local co-op only.** One computer, one screen, one controller (or the keyboard) per player. No online play.
- **Shared camera.** It zooms out to keep every player in view, up to a limit. Players can't walk off-screen.
- **Drop in / drop out.** Press a button on a controller to join the game.
- **No friendly fire.** Players can't hurt each other.
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
| 🦎 **Lizard** | (the players' home) | ? |
| 🦌 **Deer** | White-tailed deer, reindeer, moose | Forest; reindeer in the snow |
| 🦊 **Fox** | Red fox, arctic fox | Woods/hills; arctic foxes in the snow |
| 🦚 **Bird** | Peacocks | ? |
| 🪲 **Bug** | Beetles | ? |

- Some kingdoms have a few types of their animal, and a type can live somewhere different from
  the rest of its kingdom (e.g. **a snow area with arctic foxes and reindeer**).
- The "Possible area" column is Claude's guess. The designer decides.
- **Each kingdom has a king.** **Place names include the animal that lives there**:

| Kingdom | Places | King's home |
|---|---|---|
| 🦎 Lizard | Scalebrook, **Tailspin Swamp** | Tailspin Swamp |
| 🦌 Deer | Antlerwood Forest, Hoofhollow | ? |
| 🦊 Fox | Foxglove Hills, Frosttail Tundra (snow: arctic foxes and reindeer) | ? |
| 🦚 Bird | Plume Peaks, **Peacock Palace** | Peacock Palace |
| 🪲 Bug | Scarab Desert | ? |

- **Every village has a painter.** Getting painted changes your **class**, for every character.

### The overworld so far
| Area | Where | What's there |
|---|---|---|
| **Scalebrook Meadow** | The middle | The Old Burrow's temple gate (go in through the arch, from the bottom), a grapple-only chest, deer |
| **Scalebrook Village** | West of the meadow | Houses, the **village painter**, a herb chest. No enemies |
| **Antlerwood Forest** | East of the meadow | Thick trees, lots of deer, a chest in a clearing |
| **Frosttail Tundra** | North of the forest | Snow, snowy pines, snowmen, a frozen lake, deer, a chest |
| **Tailspin Swamp** | South of the meadow | Murky ponds, bare trees, deer, a grapple-only chest |

(These are first versions made by Claude from the place names above. The designer decides what really goes in each.)

## Combat numbers (starting values, to be tuned)
| | Damage | Health |
|---|---|---|
| Lizard **punch** | 5 | |
| Lizard **tongue** | 1 | |
| Starting enemies (e.g. deer) | ? | 10–15 (2–3 punches) |
| Players | | ? (Claude will start with 20) |

## Playable characters
Both playable characters change **class** by getting **painted in any village**. The paint also changes
how they look, so you can tell classes apart at a glance.

| Character | Kingdom | Starting class | Status |
|---|---|---|---|
| **Lizard** | Lizard | Chameleon | **First** |
| **Fox** (red fox) | Fox | Bomb Master | **Second** |
| **Moose** | Deer | **Summoner** | Playable; more classes to design |
| **Peacock** | Bird | **Grappler** (also Warrior, with a gun) | Playable; more classes to design |
| **Beetle** | Bug | **Roller** (also Warrior) | Playable; more classes to design |

- **The moose, peacock and beetle are warriors the heroes helped:** animals freed from Mossimus's trick who
  join the team.
- **Every hero can dodge:** a quick dash you can't be hurt during (RT / Shift / Ctrl).

### Lizard
Every lizard can **punch** (5 damage, the main attack) and use its **tongue** (1 damage, longer reach;
it **sticks to enemies and pulls them in**, though bosses are too heavy; also a tool for grabbing and swinging in later puzzles).

| Class | What it does | How you get it |
|---|---|---|
| **Chameleon** | **Camouflage:** hidden while standing still, seen when moving. **Colour change** for damage resistance: 🔵 blue = ice, 🔴 red = fire, 🟡 yellow = lightning, 🟣 purple = arcane | **Starting class** |
| **Tongue Master** | More tongue damage, **spiked tongue**, **elemental** tongue attacks, longer range | Learned from the **deer** |
| **Illusionist** (like Loki) | 10 **illusionist energy** that fills back up over time. **Clones** (5 energy; they attack the enemy you hit last), a **tail decoy** (4), **charm** enemies (2 each); every spell lasts 5 seconds | Learned from the **peacocks** |
| **Spitter** | Spits **poison** or **elements** | Learned from the **moose** |
| **Healer** | Heals and protects the team, and has **extra HP** | Learned from the **bugs** |

- **Armor** can still change how a class works, e.g. *shadow armor* lets a chameleon stay invisible while moving.
- Earlier species ideas (gecko wall-climbing, frilled-lizard scare, komodo tank, basilisk water-running,
  horned-lizard spikes) could become later classes or friendly lizards in the kingdom.

### Moose
**Summoner** (starting class): A antler bash. **X** summons a light creature (LB / RB pick which), **Y** summons a
heavy one, **B** switches the heavy one (herbs come from the ring menu instead: press Start).
Each summon has its own wait (cooldown). Up to **4** at once; a new one sends the oldest away. They stay until
they're beaten (enemies can hurt them), go after the enemy the moose hit last, and follow it through doors.

| Summon | Type | What it does | Wait |
|---|---|---|---|
| **Squirrel swarm** | Light | Fast little nibbles | 3 s |
| **Spirit wolf** | Light | Runs at enemies and bites hard | 4 s |
| **Bee swarm** | Light | Flies, stings with poison | 3 s |
| **Owl** | Light | Circles the moose and swoops at enemies | 4 s |
| **Element wisps** | Light | Zap enemies from a distance: fire, ice, lightning, arcane in turn | 4 s |
| **Antler totem** | Light | Stays put and heals heroes standing next to it | 10 s |
| **Treant** | Heavy | A big, tough walking tree; enemies go for it first | 15 s |
| **Stampede** | Heavy | Ghost deer charge across, hitting every enemy in the way, then fade | 12 s |

### Beetle
| Class | What it does |
|---|---|
| **Roller** (starting class) | A: horn jab. X: **roll** into a ball: fast, steer it a little, rams enemies (5 damage, knocks them away) and **bounces off walls**; hits hurt half as much while rolling. Y: **hide in the shell**, blocking every hit for a moment. Always on: a **tough shell**, so every hit hurts less (40% less) |
| **Warrior** | Horn jab only |

### Peacock
| Class | What it does |
|---|---|
| **Grappler** (starting class) | A: a fan of three **feathers**; LB / RB pick normal, poison, fire, ice, lightning or arcane. X: **grapple gun**: reels in small enemies, pulls you over to big ones (bosses) and to walls, rocks and bushes, **flying over water and pits**. Y: **RPG**, a rocket with a big explosion |
| **Warrior** | Shoots a plain gun |

- **Grapple spots:** some places can only be reached by grappling: a chest across the water in Scalebrook Meadow's
  top-left corner, and one behind water and a rock wall in Tailspin Swamp's bottom-right corner (each with rocks on
  the near shore to grapple back to).

### Fox
Player 2 is an ordinary **red fox**. (The arctic fox art is kept for the snow area's foxes.)

**One fox that changes masteries.** To switch, the fox gets **painted in any village**, and the paint
decides which skills it has. The paint also changes how the fox looks, so each mastery is easy to spot.

| Mastery | What it does | How you get it |
|---|---|---|
| **Bomb Master** | Throws bombs that hit groups of enemies and break walls (Tiny Dungeon has bomb art). **Switch between bomb types** (below) | **Starting mastery** |
| **Weapon Master** | Switches between weapons: **sword & shield** (X blocks), **javelin** (X throws it), **flail** (X spin attack), **axe & bow** (X shoots, Y changes the arrow: arcane, fire, ice, lightning, poison, explosive) | Learned from the **reindeer** (Deer kingdom) |
| **Gun Master** | Shoots from a distance | Learned from the **peacocks** |
| **Element Master** | **Spells** of fire, ice, lightning and arcane | Learned from the **moose** |
| **Wand Master** | A **ranged wand**; the **right stick** changes its element (fire, ice, lightning, arcane) | Learned from the **bugs** |

- **Elements for everything:** guns, weapons and bombs each come in **normal, fire, ice, lightning and arcane**
  versions (the same elements as the chameleon's colours and the spells).
- **What elements do:** **poison** and **fire** keep doing damage over time, **ice** slows enemies down,
  **lightning** and **arcane** do a lot more damage. (Starting numbers: poison 1 damage every half second for
  3 s, fire the same for 2 s, ice half speed for 2 s, lightning and arcane double damage.)
- **Each boss unlocks something new for the fox**: new weapons, guns or bombs. The fox **starts with the original
  ones** (all the bombs below); boss drops add new ones. Which boss drops what is still to be designed.
- **Bomb types** (the Bomb Master switches between them):

  | Bomb | What it does |
  |---|---|
  | **Bomb** | The normal bomb: short fuse, then a blast |
  | **Smoke bomb** | A cloud enemies can't see through (hides the team, like camouflage) |
  | **Sticky bomb** | Sticks to the enemy it hits and blows up on them |
  | **Time bomb** | A much longer fuse, for traps and setting up ahead |
  | **Remote bomb** | Doesn't go off until you press **Y**, which sets off every remote bomb you have out (up to 2 out at once, 3 with the bomb vest) |
  | **Proximity bomb** (maybe) | Sits on the ground and blows up when an enemy walks near |

- **Combos:** casting spells (Element Master) or wand shots (Wand Master) **in a certain order** sets off a combo.
  For example: ice then lightning = *shatter*, fire then arcane = *explosion*. The designer picks the real combos.

## Saving
- The game **saves by itself** and picks up where you left off next time (starting in the overworld).
- Hold N on the start screen for a new game.

## Enemies
- **Enemies come back** when you leave a room (or overworld area) and walk back in. Bosses stay beaten.
- **Regular enemies are animals, starting with deer.** Deer are about twice as tall as a lizard,
  so even ordinary enemies feel big. Our deer art only faces sideways; we flip it to face
  left or right and use it for walking up and down too.
- **Bosses are different animals**, e.g. a giant beetle from the bug kingdom.
  The **final boss is Mossimus Maximus**.
- There isn't much free animal pixel art, so expect to draw some of these ourselves.
  The Tiny Dungeon pack's slimes, bats, rats and skeletons can fill gaps.

## Questions for the designer
- **Titles:** what are they called (e.g. Squire → Knight → Royal Knight → ...), and which boss gives which?
- **Elements:** are elemental versions of weapons and bombs unlocked by bosses too?
- **The overworld:** what should the village, forest, tundra and swamp have in them (shops, friendly animals,
  quests, new enemies, dungeons)? Where do the other kingdoms go (Foxglove Hills, Plume Peaks, Scarab Desert)?
- **Boss drops:** which new bomb, weapon or gun does each boss drop?
- **Moose, peacock and beetle:** more classes? What should the peacock and beetle Warrior classes become?
- **Items and armor:** what else goes in the menu? (So far: healing herb, shadow cloak, ember scales, bomb vest, sturdy vest.)
- **Combos:** which element orders make which combos?
- **The first dungeon:** its name ("The Old Burrow" for now), the rooms, the puzzles and the boss are all placeholders to change.
- **Kings' homes:** where do the deer, fox and bug kings live? (Names should include the animal, like the other places.)
