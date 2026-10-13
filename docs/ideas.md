# Game ideas: shortlist

Status: brainstorming. Goal: narrow to one, then plan an MVP.

**Decided:**
- All co-op is local (one machine, multiple controllers). No online play.
- Art: start with free pixel-art packs (e.g. Kenney, CC0). Hand-drawn art may replace them later.
- Team: dad (experienced C#/.NET developer) and son (6th grade, game designer and ideas).

**Leaning:** Godot 4 (.NET edition), with gameplay code in C#.

| # | Idea | Inspiration | Core hook | Scope | Main risk |
|---|------|-------------|-----------|-------|-----------|
| 1 | Lizard Quest | Cat Quest 2/3 | Open-world action RPG, lizards, simple spells | Medium | Needs a lot of content (map, quests, enemies) |
| 2 | Real-time menu ARPG | Secret of Mana | Ring menus that don't pause; better spell and gear management | Medium | Menu UX is the whole point and needs lots of iteration |
| 3 | Co-op Zelda | Zelda / Four Swords | 1–4 players exploring dungeons together | Medium–High | Puzzles must be designed for a variable number of players |
| 4 | Armed Mario 3 | SMB3 / Mega Man | Platformer with guns and unlockable abilities | Small–Medium | Platformer feel needs careful tuning |
| 5 | Commander + heroes | C&C / Natural Selection / Savage | One player runs the RTS, others play heroes in a shooter | High | Two games in one; commander and heroes need separate views on one display; asymmetric balance |

## Observations
- Ideas 1–3 are the same genre: a top-down action RPG/adventure. They could be one game:
  lizard heroes, menus that don't pause, and couch co-op.
- Local-only co-op removes networking, which was the biggest cost in this list.
  The remaining multiplayer work is shared-camera or split-screen, plus per-player input.
- #4 gets to something fun and playable fastest.
- #5 is now possible without networking, but it's still two games in one and needs
  split views (e.g. commander's map on one half, heroes on the other). Keep it for a sequel.
