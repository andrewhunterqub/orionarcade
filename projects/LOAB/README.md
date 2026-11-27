# Let's Own A Bar! (LOAB)

A cosy bar-management sim for Orion Arcade, built in Unity 6 for WebGL. The design brief is
[`LOAB_Synopsis_and_Build_Brief.md`](../../LOAB_Synopsis_and_Build_Brief.md) in the repo root. This project is the
**vertical slice** (brief section 15.1): the full Prep / Service / Close loop for one bar.

Play it at `arcade.oriondesign.xyz/play.html?game=loab` (or locally with `npm run serve`).

## What's in the slice

| Brief system | Implemented |
|---|---|
| Prep / Service / Close loop (5.1) | Untimed Prep; real-time 6pm to midnight Service (1x/2x/4x, pause, skip to close); nightly report |
| Building (6.2) | Grid placement, rotate, move, sell, undo/redo, reachability rules, floors and walls |
| Vibe (6.4) | 5 axes, coherence, 5 recognised themes, per-segment match |
| Drinks (6.6) | 20 drinks in 10 categories, 3 suppliers, pricing, stock, line cleaning, quality |
| Staff (6.9) | Bartenders and floor staff, 4 attributes, 8 traits, morale, levelling, weekly applicants |
| Customers (6.10) | 5 segments as individual agents with needs, utility choices, A* pathing, thoughts, regulars |
| Events (7) | Pub Quiz and Comedy Night: book acts, promote, tickets, live moments, hit/flop outcomes |
| Reputation (6.12) | BarHopper reviews generated from each customer's actual night, rating, reputation, tags |
| Director (6.15) | 13 opportunity/problem cards with trade-offs (yes, including the dog) |
| Career (8.2) | Act 1 goals with Maggie the mentor; Relaxed / Standard / Hardcore; Sandbox |
| Responsible design (18.2) | Responsible-service toggle, alcohol-free demand, incidents kept light |

### Visual direction

Follows [`LOAB – Visual Brief_ Diorama _ Miniature.md`](../../LOAB%20–%20Visual%20Brief_%20Diorama%20_%20Miniature.md):
the bar is a scale model on a plinth (cardboard, then MDF, oak, walnut and marble as reputation grows), shot with a
narrow 22 degree lens and a tilt-shift band that follows what you're looking at. Prep is cool studio daylight; when the
doors open the studio darkens and the bar's own lamps come on one by one. Cut walls get foam-board caps, the
neighbours are white-card massing models, and customers wear segment silhouettes (backpacks, ties, flat caps, scarves,
beanies). Not yet done from the brief: bevelled meshes, bloom, grain textures, stop-motion animation, URP.

Not in the slice: food, outdoor space, multiple venues, multiplayer, monetisation, localisation.

## Architecture

```
Assets/Scripts/Sim/    Pure C# simulation, no UnityEngine references (brief 14.2)
  Content.cs           All data: items, floors, walls, music, drinks, suppliers, segments, traits
  GameState.cs         Serialisable save state (JsonUtility-friendly)
  Layout.cs            Grid occupancy, placement rules, reachability, A*
  Vibe.cs              Vibe axes, coherence, themes, segment match
  Service.cs, Agents.cs  The evening: customers, staff, incidents, events (fixed 0.1-minute tick)
  Economy.cs           Footfall model, staff and act generation, review writer
  Director.cs          Storyteller cards, career goals, mentor tips
  Game.cs              Day flow and player commands (build commands are undoable)
Assets/Scripts/Game/   Unity presentation: reads the sim, sends commands
  GameRunner.cs        Main loop, input, save/load (PlayerPrefs -> IndexedDB on WebGL)
  WorldView.cs         Room, furniture, people, mess
  ModelFactory.cs      Every prop built from primitives at runtime (no art assets)
  Hud*.cs              UI Toolkit interface built in code; styles in Resources/UI/Loab.uss
  Sfx.cs               Synthesised sound effects
Assets/Editor/LoabBuild.cs   Command-line WebGL build
Tools/SimRunner/       Headless balance runner (dotnet), compiles the Sim folder directly
Tools/UnityCheck/      Fast compile check of all runtime scripts against Unity's DLLs
```

## Working on it

```sh
# Balance: simulate 28 nights with a scripted player and print nightly numbers
dotnet run --project projects/LOAB/Tools/SimRunner -- 28 42 sensible

# Compile-check the Unity scripts in seconds (no editor)
dotnet build projects/LOAB/Tools/UnityCheck

# Full WebGL build into site/games/loab, then update the arcade manifest
projects/LOAB/build.sh
```

Requires Unity 6000.2.9f1 with the WebGL module. The build uses gzip with decompression fallback so it works on
GitHub Pages.
