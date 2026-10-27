# LOT — a lying Rush Hour

Prototype 8 (difficulty). Drag cars to get the target car out of a 6×6 lot. No text, no hints.
One 75-second timer covers the whole run; die and you start again from level 1.

## Run it
Open `AmritaAlicePrototype/` in Unity 6000.3, open any scene (e.g. `Assets/Scenes/SampleScene`) and press Play.
The game boots itself (`LotGame.Boot`), builds its own camera setup, art and sounds from code. No prefabs or assets to wire.

Dev keys: `R` restart the run, `N` skip a level.

## The lies (always true, every level)
| Looks like | Actually |
|---|---|
| Cars of the same color are decoration | Same-colored cars always slide together |
| A painted gap in the wall is the exit | The real exit is the wall segment that's slightly **thicker** |
| Little dots on a car's roof are decoration | Fuel: each dot is one more drag. At zero the car goes dark and never moves again |
| The red car is the hero | The car that **breathes** (slow pulse) is the one that must leave |
| Cars slide both ways | Cars with headlight dots only drive the way the dots face |

Shoving a car into something it can't pass costs 2 seconds (the lost chunk falls off the time bar).
Making a level unwinnable (e.g. burning a car's last fuel in the wrong spot) costs 4 seconds and rewinds the level.
Each escape gives 6 seconds back. Tune all of these on the `LOT` object's `LotGame` component during Play.

## Levels
`AmritaAlicePrototype/Assets/Resources/Levels.txt` — plain-text format documented at the top of the file.
After editing, check every level is still solvable, how much each lie matters, and how many states are fuel dead ends:

```
python3 Tools/solve_levels.py
```

| # | Name | Teaches | Min drags |
|---|---|---|---|
| 1 | drive | drag the red car out | 3 |
| 2 | jam | plain Rush Hour | 7 |
| 3 | twins | color link | 3 |
| 4 | one way | headlights | 5 |
| 5 | fuel | roof pips (one-shot moves) | 3 |
| 6 | decoy | breathing target | 4 |
| 7 | fake door | thick-wall exit | 4 |
| 8 | pair lights | link + headlights | 4 |
| 9 | last drop | target with one move + fuel + link | 5 |
| 10 | everything | all of it | 6 |

## Code (`Assets/Scripts/Lot/`)
- `LotGame` — run loop, timer bar, level dots, death/win, camera fit + shake
- `LotBoard` — board visuals, drag input, rubber-band bumps, penalties, fuel + lock-out rewind, drive-out
- `LotCar` — car visuals and springs (follow, snap overshoot, squash/stretch, lift, breathing, headlight beam, fuel pips)
- `LotRules` — pure puzzle rules (mirrored by `Tools/solve_levels.py`)
- `LotLevel` — level file parser
- `LotArt` / `LotSfx` — procedural sprites, palette, particles, synthesized sounds
