# Game design (working draft)

Title: **Lizard Wizard** (chosen by the designer)

## The pitch
A top-down action adventure for 1–4 players on one screen. Each player is a lizard or an ally
from a friendly kingdom, with their own special powers. Dungeons are built so that players have to
work together, because some paths need one character's power and some need another's.
Menus never pause the game: when one player is swapping spells, everyone else keeps playing.

Inspired by: Cat Quest 2/3 (feel, simple RPG), Secret of Mana (real-time menus, co-op),
Zelda / Four Swords (dungeons, puzzles, co-op), Tears of the Kingdom (fusing).

## Story
- The players are **ordinary lizards** who become **knights of the Lizard King** over the course of the game,
  earning **titles** as they go: each boss they defeat moves the story along and earns a new title.
- The **king and queen** live in **Scalebrook Castle**, at the top of Scalebrook Village, where the game starts.
- **Mossimus Maximus**, a deer with bad intentions, is turning the kingdoms against the lizards.
  He's the **final boss**.
- The deer and bugs you fight **aren't bad**. They've been **deceived**, and Mossimus convinces more
  animals to join him as the game goes on.
- **Beating a deceived animal breaks the trick.** It falls **asleep** until the area is clear
  (no enemies left awake), then wakes up and **thanks the heroes**.
- Other animals are **friendly**: they run shops and (later) give quests.
- **Planned:** before the heroes leave the village for the first time, a mini **tutorial and story with the king**,
  ending with each hero unlocking their **first class** in the paint shop. Until then, everyone picks any class when
  they join.

## Rules we've decided
- **Local co-op only.** One computer, one screen, one controller (or the keyboard) per player. No online play.
- **Shared camera.** It zooms out to keep every player in view, up to a limit. Players can't walk off-screen.
  Big areas scroll.
- **Drop in / drop out.** Press a button to join: left / right picks the character, up / down its class.
- **No friendly fire.** Players can't hurt each other.
- **Menus don't pause.** The game keeps running for everyone. Only the player with the menu open
  is affected: **if they're in a fight, their character slows down**; otherwise it moves at normal speed.
  - **"In a fight"** means an enemy can see you and is targeting you, even if nobody has been hit yet.
- **Don't make players open the menu if we can help it.** Common menu actions (switching powers,
  using a herb, and so on) also have a button or quick-swap (LB / RB), so the menu (Start) is for managing
  gear, not for surviving a fight.
- **Shared team stuff:** keys, coins, boss coins, **healing herbs** and monster parts belong to the whole team, so
  nobody can grab them all for themselves. Any hero can eat one of the team's herbs. Heroes start with none.
- **Art:** our own pixel art (drawn as text grids, see `tools/`) plus Kenney's free "Tiny" packs.

## The world: five kingdoms
| Kingdom | Types | Possible area |
|---|---|---|
| 🦎 **Lizard** | (the players' home) | Scalebrook |
| 🦌 **Deer** | White-tailed deer, reindeer, moose | Forest; reindeer in the snow |
| 🦊 **Fox** | Red fox, arctic fox | Woods/hills; arctic foxes in the snow |
| 🦚 **Bird** | Peacocks | ? |
| 🪲 **Bug** | Beetles | ? |

- Some kingdoms have a few types of their animal, and a type can live somewhere different from
  the rest of its kingdom (e.g. **a snow area with arctic foxes and reindeer**).
- **Each kingdom has a king.** **Place names include the animal that lives there**:

| Kingdom | Places | King's home |
|---|---|---|
| 🦎 Lizard | Scalebrook, **Tailspin Swamp** | **Scalebrook Castle** |
| 🦌 Deer | Antlerwood Forest, Hoofhollow | ? |
| 🦊 Fox | Foxglove Hills, Frosttail Tundra (snow: arctic foxes and reindeer) | ? |
| 🦚 Bird | Plume Peaks, **Peacock Palace** | Peacock Palace |
| 🪲 Bug | Scarab Desert | ? |

### The overworld so far
```
                 Frosttail Tundra
                        |
Village -- Meadow -- Antlerwood Forest -- Red Rock Canyon
             |
       Tailspin Swamp
             |
        Glimmer Lake
```

| Area | What's there |
|---|---|
| **Scalebrook Village** (start) | Shops, villagers, **Scalebrook Castle** up north, a painter's mat (for playtesting). No enemies |
| **Scalebrook Meadow** | The Old Burrow's temple gate (the first dungeon), a grapple-only chest, deer |
| **Antlerwood Forest** (big) | Thick trees, lots of deer, chests in clearings |
| **Frosttail Tundra** (big) | Snow, snowy pines, snowmen, two frozen lakes, arctic fox packs, chests, and the gate to **Frostfang Fortress** |
| **Red Rock Canyon** | A long chasm with one rock bridge; a high ledge with a chest that only flying (glider, plane) or a grapple reaches |
| **Tailspin Swamp** (big) | Murky ponds, bare trees, deer, chests, one only a grapple reaches |
| **Glimmer Lake** | A sandy beach, and a wide lake; a chest on the far shore (boat, fly, or grapple to a bush) |

(These are first versions made by Claude from the place names above. The designer decides what really goes in each.)

### Scalebrook Village and Castle
Walk up into a shop's door to go in. Each shop has a **shopkeeper** behind the counter who greets you; they wander a
little, and step up to the counter (and stay) when a hero is there. Stand at the counter a moment to buy.

| Place | What it does |
|---|---|
| **Scalebrook Inn** | Rest: full hearts, **5 coins** |
| **Herb Shop** | A healing herb, **10 coins** |
| **Paint Shop** | A paint mat to change class (later: where you unlock your first class) |
| **Weapons & Armor** | **Weapon upgrades** for the whole team (below) |
| **Castle Hall** | Knights (some guard, some walk about). Doors: up to the throne room, right to the kitchen, left to the **armory** (locked until a quest opens it) |
| **Throne Room** | The **king** (a lizard) and **queen** (a peahen), and four knights. The king, the queen or both welcome you. **Later:** quests |
| **Castle Kitchen** | A cook, and a **free herb** (once) |

## Dungeons
| Dungeon | Where | Rooms | Boss | Reward |
|---|---|---|---|---|
| **The Old Burrow** | Scalebrook Meadow | Entrance, key hall, side room, a lever-and-bridge puzzle | **Giant Beetle** | Title: Squire |
| **Frostfang Fortress** | Frosttail Tundra | Frozen Gate (foxes), **Ice Hall** (slide puzzle: on ice you slide until you bump into something; the key is out on the ice), **Frozen River** (a lever makes a bridge), the Frost Den | **Frostmane the Moose** and **Tusker the Boar**, together: the moose's stomp sends out a ring of frost that slows heroes, and the boar charges whoever is slowed; beat one and the other gets angry | Title: Knight, the **Hoversled** |

Every boss also gives a **boss coin** and **one more fuse slot** of each kind; a portal appears in the boss room to go
back out.

## Loot and money
- A beaten enemy drops **coins** (8 times out of 10) or a **healing herb**, plus **each of its monster parts**
  (a deer: an antler and a hide). Walk over loot to pick it up; it disappears after 30 seconds.
- **Coins** go into the team's purse (shown at the top). **Bosses** drop five things, always including a **boss coin**
  (a special coin, counted on its own).
- **Monster parts** go into the team's bag, for the moose Tinkerer: antler and hide (deer), beetle shell (Giant
  Beetle), tusk (boar), fur (arctic fox), frog skin (frog), scale (snake).

## Combat numbers (starting values, to be tuned)
| | Damage | Health |
|---|---|---|
| Lizard **punch** | 5 | |
| Lizard **tongue** | 1 | |
| Deer | kick 3 | 10–15 (2–3 punches) |
| Players | | 18–26 depending on the hero |

## Playable characters
Each character has a few **classes**. Pick the class when joining; the **paint shop** (or the painter's mat outside)
switches to the next one. **Each class looks different** (a hat, a mask, a new colour...).

| Character | Kingdom | Classes (starting class first) |
|---|---|---|
| **Lizard** | Lizard | Chameleon, Tongue Master (red headband), Spitter (teal), Illusionist (wizard hat) |
| **Fox** (red fox) | Fox | Bomb Master, Gun Master (cowboy hat), Weapon Master |
| **Moose** | Deer | Summoner, **Tinkerer** (goggles) |
| **Peacock** | Bird | Grappler, Warrior (knight's helmet), **Thief** (bandit mask) |
| **Beetle** | Bug | Roller, **Vehicle Master** (aviator cap) |

- **The moose, peacock and beetle are warriors the heroes helped:** animals freed from Mossimus's trick who
  join the team.
- **Every hero can dodge:** a quick dash you can't be hurt during (RT / Shift / Ctrl).
- **Classes still to build** (ideas): lizard **Healer** (learned from the bugs); fox **Element Master** and
  **Wand Master** (elements on the right stick, combos).

### Lizard
Every lizard can **punch** (5 damage, the main attack) and use its **tongue** (1 damage, longer reach;
it **sticks to enemies and pulls them in**, though bosses are too heavy).

| Class | What it does |
|---|---|
| **Chameleon** | **Camouflage:** hidden while standing still. **Colour change** for damage resistance: 🔵 blue = ice, 🔴 red = fire, 🟡 yellow = lightning, 🟣 purple = arcane |
| **Tongue Master** | A stronger, longer tongue |
| **Spitter** | Spits **poison** or **elements** (LB / RB pick) |
| **Illusionist** (like Loki) | 10 **illusionist energy** that fills back up. **Clones** (5 energy; they attack the enemy you hit last), a **tail decoy** (4), **charm** enemies (2 each); every spell lasts 5 seconds |

- **Armor** can change how a class works, e.g. the *shadow cloak* lets a chameleon stay hidden while moving.

### Fox
| Class | What it does |
|---|---|
| **Bomb Master** | Bombs, LB / RB switch type (below) |
| **Gun Master** | Shoots from a distance; LB / RB pick the element |
| **Weapon Master** | LB / RB switch weapons: **sword & shield** (X blocks), **javelin** (X **lobs it in a high arc** over things close by, landing on the enemy in front), **flail** (X spin attack), **axe & bow** (X shoots, Y changes the arrow: arcane, fire, ice, lightning, poison, explosive) |

- **Bomb types:** bomb, smoke bomb (enemies can't see through it), sticky bomb, time bomb, remote bomb (Y sets them
  all off; 2 out at once, 3 with the bomb vest). Maybe: proximity bomb.
- **Each boss is meant to unlock something new for the fox** (weapons, guns or bombs). Which boss drops what is
  still to be designed.

### Moose
**Summoner:** A antler bash. **X** summons a light creature (LB / RB pick), **Y** a heavy one, **B** switches the heavy
one. Up to **4** at once; they go after the enemy the moose hit last.

| Summon | Type | What it does | Wait |
|---|---|---|---|
| **Squirrel swarm** | Light | Fast little nibbles | 3 s |
| **Spirit wolf** | Light | Runs at enemies and bites hard | 4 s |
| **Bee swarm** | Light | Flies, stings with poison | 3 s |
| **Owl** | Light | Circles the moose and swoops at enemies | 4 s |
| **Element wisps** | Light | Zap enemies from a distance: fire, ice, lightning, arcane in turn | 4 s |
| **Antler totem** | Light | Stays put and heals heroes next to it | 10 s |
| **Treant** | Heavy | A big, tough walking tree; enemies go for it first | 15 s |
| **Stampede** | Heavy | Ghost deer charge across, hitting every enemy in the way | 12 s |

**Tinkerer:** A antler bash. **X fuses** a monster part onto the closest teammate's **main attack or armor** (or its
own), Tears of the Kingdom style; LB / RB pick the part and weapon / armor. **Y crafts** armor for the whole team
(picked in the Start menu).

| Part | Fused onto a weapon or gun | Fused onto armor |
|---|---|---|
| Antler | +2 damage | +1 heart |
| Hide | Faster attacks | Walk faster |
| Beetle shell | +4 damage | +2 hearts |
| Tusk | +3 damage | +1 heart |
| Fur | Faster attacks | +1 heart |
| Frog skin | Hits poison | Walk faster |
| Scale | +2 damage | +2 hearts |

| Crafted armor | Needs | Gives |
|---|---|---|
| Hide Vest | 2 hide | +1 heart |
| Antler Helm | 2 antlers, 1 hide | +2 hearts |
| Shell Plate | 1 beetle shell, 2 hide | +3 hearts |
| Fur Coat | 3 fur | +2 hearts |
| Scale Mail | 3 scales, 1 tusk | +4 hearts |

- **Fuse slots:** every hero has **3 each for guns, weapons and armor**, plus one more of each per boss beaten.
  A main attack that shoots counts as a gun. Switch fused weapons / armor in the Start menu; when the slots are
  full, a new fusion replaces the one in use.

### Peacock
| Class | What it does |
|---|---|
| **Grappler** | A: a fan of three **feathers** (LB / RB pick the element). X: **grapple gun**: reels in small enemies, pulls you to big ones and to walls, rocks and bushes, **flying over water and pits**. Y: **RPG** rocket |
| **Warrior** | Shoots a plain gun |
| **Thief** | A: shoots its gun. X: **steals** from the enemy in front (LB / RB: **gold** for the team, **gear** = a herb for now, or its **ability**). Awake enemies only, each thing once per enemy. Y: the **stolen ability** (deer: Deer Kick; Giant Beetle: Beetle Charge; boar: Boar Charge; arctic fox: Fox Pounce; frog: Frog Spit; snake: Poison Bite), kept until you steal another |

- **Grapple spots:** chests only a grapple (or flying) reaches, in the meadow, the swamp, the canyon and the lake.

### Beetle
| Class | What it does |
|---|---|
| **Roller** | A: horn jab. X: **roll** into a ball (fast, rams enemies, bounces off walls; hits hurt half as much). Y: **hide in the shell** (blocks every hit for a moment). Always on: a **tough shell** (hits hurt 40% less) |
| **Vehicle Master** | LB / RB pick a vehicle, X **builds it** and drives. Teammates climb on with A, keep attacking from the deck, hop off with dodge. Hits on riders hit the vehicle; when it breaks, 10 seconds before the next |

| Vehicle | Seats | A (driver) | Good for |
|---|---|---|---|
| **Tank** | 4 | Cannon (explosive shells) | Fighting; toughest |
| **Cart** | 4 | Speed boost | Fast; rams enemies |
| **Boat** | 4 | – | Crossing water |
| **Glider** | 4 | – | Flying over water and holes for 8 seconds |
| **Plane** | 2 | Machine gun | Flying anywhere, fast |
| **Hoversled** | 4 | – | A hovercraft snowmobile: very fast on ice, normal on water, slow on land. **Unlocked by beating Frostfang Fortress** |

## Elements
- Guns, weapons, bombs and spells come in **normal, poison, fire, ice, lightning and arcane** versions.
- **Poison** and **fire** keep doing damage over time, **ice** slows enemies down, **lightning** and **arcane** do
  double damage. (Poison 1 damage every half second for 3 s, fire the same for 2 s, ice half speed for 2 s.)

## Items and gear
- **Healing herbs** (B, or the Start menu): found in chests, dropped as loot, sold in the herb shop.
- **Armor** (one slot): shadow cloak, ember scales, bomb vest, sturdy vest, and the Tinkerer's crafted armor.
- **Weapon upgrades** at the Weapons & Armor shop, for coins, and one upgrade covers the **whole team's** main attacks:

  | Material | Price | Main attacks |
  |---|---|---|
  | Wood | (start) | – |
  | Copper | 20 coins | +1 damage |
  | Iron | 40 coins | +2 damage |
  | Steel | 80 coins | +3 damage |
  | Mithril | 160 coins | +5 damage |

## Saving
- The game **saves by itself** and picks up where you left off next time (starting in Scalebrook Village):
  the world (doors, chests, levers, broken rocks, bosses), keys, coins, boss coins, monster parts, crafted armor,
  and each player's class, weapon, armor, herbs, stolen ability and fusions.
- Hold N on the start screen for a new game.

## Enemies
- **Enemies come back** when you leave an area and walk back in. Bosses stay beaten.
- **Regular enemies are animals.** They come in **mixed groups** (e.g. frogs spitting from behind while snakes sneak
  in close). The **boar** is the toughest.

  | Enemy | Where | How it fights | Health | Drops |
  |---|---|---|---|---|
  | Deer | Everywhere | Walks up, flashes, kicks (3) | 10–15 | Antler, hide |
  | Boar | Forest, canyon | Paws the ground, then charges in a straight line (5); rests after a charge | 24–30 | Tusk |
  | Arctic fox | Tundra, in packs | Fast; darts in, bites (2), runs off | 7–9 | Fur |
  | Frog | Swamp, lake, canyon | Keeps its distance, hops, spits slime (2) | 6–8 | Frog skin |
  | Snake | Meadow, forest, canyon, swamp, lake | Faint until you're close; poison bite (2, then poison) | 8–10 | Scale |

- **Bosses are different animals**: the **Giant Beetle** guards The Old Burrow, **Frostmane and Tusker** guard
  Frostfang Fortress. The **final boss is Mossimus Maximus**.

## Questions for the designer
- **Titles:** what are they called (e.g. Squire → Knight → Royal Knight → ...), and which boss gives which?
- **The king's tutorial and quests:** what happens in the story at the start, and what quest opens the armory?
- **Titles after Knight**, and what the next dungeon is (where, which boss, what it unlocks)?
- **Boss drops:** which new bomb, weapon or gun does each boss drop?
- **Where the other kingdoms go:** Foxglove Hills, Plume Peaks, Scarab Desert, and the deer, fox and bug kings' homes.
- **Combos:** which element orders make which combos (for the Element / Wand Master)?
- **The first dungeon:** its name ("The Old Burrow" for now), the rooms, the puzzles and the boss are placeholders.
