# LOAB Development Plan: Weekend Sprint

**Window:** Friday 27 November 2026, 16:00 to Monday 30 November 2026, 08:00 (UK time, which is UTC in November).
**Who:** Claude Code, working autonomously in hourly sessions and pushing to `main`.
**Source of truth:** this file. Every session reads it first, works on the next unchecked task, ticks it off, and adds a line to the [progress log](#10-progress-log).

---

## 1. Goal

Take LOAB from a strong vertical slice (brief section 15.1, which it already exceeds) towards the **soft-launch scope** (brief 15.2). The weekend priorities are:

1. **A game that's actually balanced.** Difficulty should matter, failure should be possible, and the numbers players see should be believable.
2. **A deeper Prep / Service / Close loop.** More to do during Service, and clearer feedback after Close.
3. **Time that feels alive.** Seasons, a calendar of special nights, and weekly bills (brief 5.2 and 5.3).
4. **More content.** Themes, décor, drinks, crowds, event types and staff roles, moving towards the soft-launch numbers.
5. **Engineering that makes all of the above safe.** Automated simulation tests, a compile check in CI, save versioning, and a path to automatic WebGL builds.

---

## 2. Constraints and how work is verified

| Constraint | Consequence | Mitigation |
|---|---|---|
| No Unity editor in the cloud container, and no Unity licence | Can't make WebGL builds, or run or see the game | (a) `Tools/SimRunner`: runs the pure-C# simulation headless with dotnet. (b) `Tools/UnityCheck`: compiles **all** runtime scripts against Unity reference assemblies (2021.3 from NuGet), which catches type and API errors. (c) Task E4 adds a GameCI workflow that builds WebGL in GitHub Actions once the owner adds Unity licence secrets. |
| The live game at `/play.html?game=loab` is a committed build | Source pushed to `main` won't change the live game until someone rebuilds it | The owner runs `projects/LOAB/build.sh` locally, or E4 automates it. Every session's log line says whether the change still needs a build to be seen. |
| Visual and feel changes can't be checked by eye | Presentation work is riskier | Keep presentation changes small and consistent with existing `ModelFactory` and `Hud` patterns. Keep simulation-side logic testable. List anything that needs visual QA in [section 8](#8-needs-the-owner). |
| The reference assemblies are Unity 2021.3, not Unity 6 | Unity 6-only APIs would show up as false errors | Avoid new Unity 6-only APIs where an older one works. If one is needed, put it behind `#if UNITY_6000_0_OR_NEWER` with a fallback. |
| Pushing straight to `main` | Every push redeploys the arcade | Each push must pass the [quality gate](#6-session-protocol-every-hourly-session). Never commit generated builds or `bin/`/`obj/`. |

**Definition of done for any task:** the simulation builds; `SimRunner test` passes, including the balance bands once T1 lands; UnityCheck has 0 errors; new player-facing text matches the brief's tone (warm, witty, a bit British, never glamorising excess); and the plan and log are updated.

---

## 3. Current state (baseline, measured 27 Nov 2026)

### 3.1 What exists

The simulation and presentation code is about 9,000 lines of C#.

- **Loop:** untimed Prep; real-time Service from 6pm to midnight at 1x, 2x or 4x, with pause and skip; nightly report.
- **Building:** grid placement, undo/redo, reachability, walls and floors, knocking through next door.
- **Vibe:** 5 axes, coherence, and 5 themes (Traditional Pub, Speakeasy, Sports Bar, Dive Bar, Retro Arcade Bar).
- **Content:** 49 items, 20 drinks in 10 categories, 3 suppliers, 9 music styles, 12 floor and wall styles.
- **People:** 5 customer segments (individual agents with A* pathing and thoughts); 2 staff roles; 8 traits; regulars.
- **Events:** Pub Quiz, Comedy Night and Open Mic, with residencies.
- **Story:** 14 Director cards, 15 career goals (Act 1 and Act 2), mentor tips, 3 scenarios, a sandbox, and Relaxed, Standard and Hardcore difficulties.
- **Saving:** JSON through `PlayerPrefs`. On the arcade, `PlayerPrefs` is now saved per game and synced to the player's account automatically.

### 3.2 Balance baseline (scripted "sensible" player, 56 nights)

| Difficulty | Bankruptcies (5 seeds) | Cash at night 56 | Rating at night 56 |
|---|---|---|---|
| Relaxed | 0 / 5 | £54.9k – £61.3k | 3.98 – 4.61 |
| Standard | 0 / 5 | £50.0k – £55.2k | 3.84 – 4.52 |
| Hardcore | 0 / 5 | £42.3k – £50.6k | 3.80 – 4.64 |

**Problems found:**

1. **B1: Difficulty barely matters.** Hardcore ends only about 15% poorer than Relaxed, and nobody goes bankrupt. The brief (20.2) wants a low but non-zero bankruptcy rate on Standard in Year 1.
2. **B2: Money comes too fast.** The Act 1 finale (£15k in the bank) is reached around week 2–3, and players have about £50k by week 8 with nothing to spend it on.
3. **B3: "Dirty" dominates the thoughts.** 80–97% of customers think it's dirty every night, so the signal is saturated and not useful. The scripted player hires only one floor staff member, but even so, saturation at that level suggests the cleanliness model or the thought threshold is too harsh.
4. **B4: Stock-outs every night.** About 70–110 customers a night couldn't get the drink they wanted, even with Restock All. Either the starting stock levels are too low for the crowds or restocking doesn't scale with demand.
5. **B5: Sunday wipes out the profit.** Rent lands on Sunday, so Sunday shows £-964 to £+700 every week. That's realistic, but it feels like a penalty unless the report explains it.
6. **B6: Idle players never hit a crisis.** With no actions, cash drifts down without the game ever reaching a point of tension.
7. **B7: Event outcomes.** Open Mic results swing evenly between Flop and Viral hit. Viral hits should be rarer.

### 3.3 Gaps against the soft-launch scope (brief 15.2)

| Area | Now | Soft launch | Weekend target |
|---|---|---|---|
| Themes | 5 | 6 | **7** (add Craft Beer Hall, Wine & Cocktail Lounge) |
| Décor items | 49 | 150+ | **~85** |
| Drinks | 20 | 50+ | **~45**, plus a simple Cocktail Lab |
| Suppliers | 3 | 6 | **6** |
| Customer segments | 5 | 8 | **8** |
| Event types | 3 | 6 | **6** (add Live Music, Karaoke, Big Match Screening) |
| Staff roles | 2 | all except bar manager | **4** (add Door Security and Cleaner) |
| Career | Acts 1–2 | Acts 1–2 | Rebalance Acts 1–2 and add **Act 3** goals |
| Seasons and calendar | Season index only | Real seasonal effects | **Full seasons, plus 10 or more calendar nights** |

---

## 4. Workstreams and backlog

Priorities: **P0** (do first, makes the rest safe), **P1** (core of the weekend), **P2** (content breadth), **P3** (stretch).
Sizes: S (under 1 session), M (1–2 sessions), L (3 or more sessions).

### E: Engineering foundation (P0)

- [x] **E1 · Commit the tool project files (S).** `*.csproj` is gitignored. Add exceptions in `projects/LOAB/.gitignore` for `Tools/*/*.csproj`, commit `SimRunner.csproj` and `UnityCheck.csproj`, and update the README commands.
  *Done when:* a fresh clone runs `dotnet run --project projects/LOAB/Tools/SimRunner` and `dotnet build projects/LOAB/Tools/UnityCheck`.
- [x] **E2 · Automated simulation tests (M).** Add a `test` mode to SimRunner (no new dependencies), exiting non-zero on failure:
  - Determinism: same seed, same 14-night result, bit for bit.
  - Save round trip: serialise `GameState` with `System.Text.Json`, set to include fields as JsonUtility does, reload it, and check the simulation continues identically.
  - Invariants every night: cash finite, no negative stock, reachability holds, no agent stuck past closing, rating within 1–5, reputation within 0–100.
  - Smoke-test every scenario and every difficulty for 28 nights, with no exceptions.
  - Balance bands (T1), which start as warnings and become failures once B1–B4 are fixed.
- [x] **E3 · CI workflow `loab-ci.yml` (S).** On every push or pull request touching `projects/LOAB/**`: set up .NET 8, run SimRunner `test`, and build UnityCheck.
  *Done when:* it's green on `main`.
- [x] **E4 · Automatic WebGL builds in CI (M, needs the owner).** Add a GameCI (`game-ci/unity-builder`) workflow. It runs when the owner adds the `UNITY_LICENSE` (or `UNITY_EMAIL` and `UNITY_PASSWORD`) secrets; without them it skips with a clear message. It builds WebGL with the same settings as `LoabBuild.cs`, then commits the output to `site/games/loab/Build`. The commit uses `[skip loab-build]` so it doesn't trigger itself. Then it runs `npm run add-game -- loab`.
  *Done when:* the YAML lints and skips cleanly without secrets. A real build can only be confirmed after the owner adds the secrets.
- [x] **E5 · Save versioning (S).** Bump `GameState.version` whenever the state shape changes. Add a `Migrate(GameState)` step that fills defaults for new fields, and test that a version-1 save loads.
- [x] **E6 · Use the arcade's cloud save (S).** Call `PlayerPrefs.Save()` after every autosave in `GameRunner`, so the arcade picks up changes straight away. Optionally show "Saved to your account" in the pause menu using `OrionCloudSave.IsSignedIn`. Copy `unity/OrionArcade` into `Assets/Plugins/OrionArcade` only if it's actually used.

### T: Balance and economy (P1)

- [x] **T1 · Balance targets and metrics (S).** Define target bands in code (`Tools/SimRunner/Balance.cs`), measured over 5 seeds × 3 difficulties × 84 nights:
  - Act 1 finished by week 4–6 on Standard.
  - Cash at week 12, Standard: £15k–£35k.
  - Bankruptcy, scripted "careless" player: Relaxed 0%, Standard 20–60%, Hardcore over 60%.
  - Bankruptcy, "sensible" player: Hardcore 0–20%.
  - Most common thought under 60% of customers on a typical night.
  - Stock-outs under 5% of customers for a sensible player.
  - Event outcomes over many events: Flop 20–35%, Viral 5–12%.
- [x] **T2 · More scripted players (M).** Add `careless` (over-spends, ignores cleaning and staff), `themed:<id>` (builds towards one theme) and `cheap` / `premium` pricing strategies, so balance is measured against several kinds of player, not one.
- [x] **T3 · Fix B3, cleanliness (M).** Trace how mess builds up and how the dirty thought is triggered in `Service.cs`. Make the threshold depend on how messy it actually is (sticky tables vs a filthy floor). Make floor staff and the new Cleaner role (S2) clearly effective, and show the cleanliness trend in the report.
- [x] **T4 · Fix B4, stock (M).** Add a "par level" per drink: the stock to start each night with, driven by the last 7 nights' sales. Add a "Restock to par" action, and a supplier delivery limit that grows with reputation. Stock-outs should come from choices, not from the restock button not doing enough.
- [x] **T5 · Fix B1 and B2, difficulty and money (M).**
  - Difficulty should scale more: footfall, rent, wages, supplier prices, review harshness, how often incidents happen, and how forgiving the Director is.
  - Rent should rise with reputation and expansion, modelling business rates and the landlord's review.
  - Expansion, décor and plinth upgrades should become real money sinks.
  - Retune goal rewards.
- [x] **T6 · Fix B5, weekly bills (S).** Spread bills into a weekly statement (rent, utilities, wages, supplier invoices, loan) shown in Sunday's report as its own "Weekly bills" section, with a week-over-week comparison.
- [x] **T7 · Fix B7, event outcomes (S).** Rebalance the variance of event outcomes, and make promotion and act quality affect them more strongly than luck.
- [ ] **T8 · Fix B6, cash crisis (S).** Bring in warnings, the emergency loan and a bailiff Director card at clear thresholds, so that a struggling bar gets a story arc before game over.

### L: Core loop depth (P1, brief 5.1)

- [ ] **L1 · Owner interventions during Service (M).** Open Question 4 in the brief is resolved as tap commands; walking around is out of scope. Add three owner actions, each with a cooldown:
  - **Pull pints:** the owner works the bar for 10 minutes of game time.
  - **Clear up:** instantly clean one zone.
  - **Calm it down:** settle one rowdy group.

  Each one uses the owner's stamina for the night, with a fixed number per night.
- [ ] **L2 · Quick actions (S).** Add "Happy hour now" (from now until the next hour, at the set discount) and "Last orders early" (for safety on a rough night, which reduces incidents) to the Service bar.
- [ ] **L3 · Redeploy staff mid-service (S).** Switch any staff member between bar and floor duty during Service.
- [ ] **L4 · Simulate the night instantly (S).** A "Run the night" button that resolves Service headlessly, for players who prefer pure management (brief 5.1). The simulation is already decoupled, so this is a loop with no rendering.
- [ ] **L5 · A better report (M).**
  - A cost breakdown: drinks cost, wages, acts, promotion and bills.
  - 7-night sparklines for takings, customers, rating and cleanliness.
  - "Best moment" and "worst moment" headlines.
  - Each review linked to the cause behind it (for example "slow service" points to the bar queue).
- [ ] **L6 · Weekly summary screen (S).** After Sunday: the week's profit and loss, the regulars who came in, the best night, and next week's calendar.

### C: Calendar and seasons (P1, brief 5.2 and 5.3)

- [ ] **C1 · Seasons that matter (M).** Each season gets its own weather table and demand modifiers: summer favours cold drinks and makes staff tired faster; winter favours hot drinks and cosy, intimate vibes. Award a seasonal "trend" vibe bonus. Show the season in the HUD.
- [ ] **C2 · Calendar nights (M).** A `CalendarDef` list of special nights on fixed days of the 112-day year. Each has demand and segment modifiers, a suggested event, and a Director hook:
  - Freshers' Week, Halloween, Bonfire Night, the Christmas party season, Christmas Eve, New Year's Eve, Dry January, Valentine's, St Patrick's, Easter, the "Big Tournament" (generic, unlicensed, four match nights), summer solstice and a bank holiday.
- [ ] **C3 · Calendar UI (S).** A 4-week calendar strip in the Events tab, showing booked events, residencies and calendar nights, with booking straight from the calendar.

### S: Staff (P2, brief 6.9)

- [ ] **S1 · Door Security role (M).** Stops rowdy customers getting in and settles incidents faster. Has a trait synergy with "Gentle Giant". Becomes a requirement on Hardcore Saturdays (the licensing angle, brief 6.13).
- [ ] **S2 · Cleaner role (S).** Cleans after close, raising tomorrow's starting cleanliness and cutting floor-staff load. Linked to T3.
- [ ] **S3 · Rota and fatigue (M).** Set which nights each staff member works. Working too many nights in a row lowers morale and stamina, and staff can ask for nights off.
- [ ] **S4 · Training (S).** Pay to train an attribute during Prep. The staff member misses that night.
- [ ] **S5 · Four more traits (S).** For example Chatterbox, Mixologist, Night Owl and Clumsy-but-Loved, each with a clear mechanical effect.

### K: Content breadth (P2, brief 6.4–6.10)

- [ ] **K1 · Two new themes (M).**
  - **Craft Beer Hall:** industrial, long tables, chalkboard taps, craft drinks.
  - **Wine & Cocktail Lounge:** polished, intimate, cocktails and wine.

  Each gets its own theme definition, vibe signature, 6 or more items, music, a floor and wall style, and recognition rules.
- [ ] **K2 · Décor to about 85 items (L).** Add about 36 items across the themes. Each one needs a `ModelFactory` model built from primitives. Include "hero" pieces such as a copper still, a vinyl wall, a fireplace, a neon sign set, a tap wall and a dartboard alcove.
- [ ] **K3 · Drinks to about 45, with 6 suppliers (M).** Add craft cans, natural wine, premium spirits, seasonal drinks (mulled wine, Pimm's-style cups) and more alcohol-free options. Add three suppliers: craft, premium and budget wholesale, each with its own minimum order, quality and price.
- [ ] **K4 · Cocktail Lab (M).** Combine a base spirit, a modifier and a garnish style into a named signature cocktail. Its quality depends on bartender skill; it fits particular themes and appeals to particular segments. A signature drink can become a review tag.
- [ ] **K5 · Three new customer segments (M).** Each has its own needs, silhouette accessory in `WorldView`, preferred drinks and nights, and review voice:
  - **After-Work Crowd:** weekdays 6–8pm, wants polish and speed.
  - **Sports Fans:** match nights; want screens and pints.
  - **Tourists:** summer and weekends; want character, and are price-insensitive.
- [ ] **K6 · Three new event types (L).** Each fits the existing booking, promotion and live-moment pipeline:
  - **Live Music:** bands need a stage and speakers; there's a sound-check risk; volume affects neighbours.
  - **Karaoke:** cheap and chaotic; needs a screen.
  - **Big Match Screening:** needs screens; driven by the calendar (C2); sports fans attend.
- [ ] **K7 · Director cards from 14 to 30 or more (M).** New opportunity and problem cards tied to the new systems: a power cut, a burst keg, a stag do, a TV location scout, a council licensing review, a staff romance, a rival's poaching attempt, the local paper's "best quiz" vote, a heatwave, a snow day, a lost wedding ring and a busker turned star.

### P: Progression and modes (P2, brief 8)

- [ ] **P1 · Rebalance Acts 1–2 (S)** to the new economy from T5. Goals should have clear next steps and help from Maggie.
- [ ] **P2 · Act 3, "Bar of the Year" (M).** Six goals, finishing with a season-end awards night: a scripted special evening with judges whose verdict comes from the season's history, and a trophy that appears on the bar.
- [ ] **P3 · Three more scenarios (M).** "Dry January" (a sober-month challenge), "Match Day" (survive a tournament) and "The Inheritance" (a run-down bar, a big debt and 8 weeks).
- [ ] **P4 · Unlock pacing (S).** Show what unlocks next and at what reputation, as a reputation track in the Vibe panel.
- [ ] **P5 · Second venue (P3, L).** Stretch goal: only once everything above is green.

### U: UX, onboarding and accessibility (P1–P2, brief 13)

- [ ] **U1 · First-night flow (M).** Make sure a new player places a bar, sets prices and opens within 3 minutes. Contextual tips should point at the actual buttons, and the help is skippable.
- [ ] **U2 · Settings screen (S).** Service length (6 or 10 minutes), autosave, reduced motion (stop-motion and tilt-shift off), text size, colour-blind-safe status colours and keyboard shortcut help.
- [ ] **U3 · Keyboard shortcuts and touch (S).** Shortcuts for every main action. Larger touch targets under the mobile breakpoint.
- [ ] **U4 · Readability (S).** Every number in the HUD has a tooltip explaining where it comes from (brief 13.1: "every number explains itself").

### V: Presentation and audio (P3, brief 11–12; needs visual QA)

- [ ] **V1 · Ambient music (M).** Synthesised ambient loops per music style, built on the `Sfx.cs` approach, with volume tied to the bar volume setting.
- [ ] **V2 · Event moments (S).** Applause and laughter swells, a quiz round chime and a karaoke key change.
- [ ] **V3 · Visual brief extras (M).** Bevelled edges on hero props, a soft bloom on glowing items, a paper-grain overlay. Gate each one by quality level.

### R: Responsible design (all the time, brief 18.2)

- [ ] **R1 · Check new content against the brief.** No glamorising excess, alcohol-free options in every theme, incidents kept light, and the responsible-service toggle respected by new events (karaoke and stag do especially).

**Explicitly out of scope this weekend:** multiplayer and social, monetisation, the food system, outdoor space, localisation, and real art assets.

---

## 5. Schedule

There are about 64 hourly sessions. Times are UK time. If something runs over, carry it forward rather than skipping the quality gate.

| Block | Sessions | Focus | Tasks |
|---|---|---|---|
| Fri 16:00–20:00 | 4 | Foundation | E1, E2, E3, E5 |
| Fri 20:00–Sat 02:00 | 6 | Balance, part 1 | T1, T2, T3, T4 |
| Sat 02:00–08:00 | 6 | Balance, part 2 | T5, T6, T7, T8, E6 |
| Sat 08:00–14:00 | 6 | Loop depth | L1, L2, L3, L4 |
| Sat 14:00–20:00 | 6 | Report and calendar | L5, L6, C1, C2 |
| Sat 20:00–Sun 02:00 | 6 | Staff and calendar UI | C3, S1, S2, S3 |
| Sun 02:00–08:00 | 6 | Content, part 1 | K1, K3, K5 |
| Sun 08:00–14:00 | 6 | Content, part 2 | K4, K6 |
| Sun 14:00–20:00 | 6 | Content, part 3, and Director | K2, K7, S4, S5 |
| Sun 20:00–Mon 02:00 | 6 | Progression and UX | P1, P2, P3, P4, U1, U2 |
| Mon 02:00–06:00 | 4 | UX, polish, audio | U3, U4, V1, V2, E4 |
| Mon 06:00–08:00 | 2 | Stabilise and hand over | Full regression across all seeds and difficulties, README and changelog, handover notes in section 9 |

---

## 6. Session protocol (every hourly session)

1. **Sync:** `git fetch origin main && git checkout main && git reset --hard origin/main`. The owner may have pushed too, so never force-push.
2. **Tools:** if `dotnet` is missing, run `apt-get install -y dotnet-sdk-8.0`.
3. **Read** this plan, the progress log and the last commit messages. Pick the **first unchecked task** in schedule order. If the previous session left a task half done, finish that first.
4. **Work** in small, coherent commits. Keep the simulation and presentation separate: game logic goes in `Assets/Scripts/Sim` (no `UnityEngine`); visuals and UI go in `Assets/Scripts/Game`.
5. **Quality gate** before every push:
   - `dotnet run -c Release --project projects/LOAB/Tools/SimRunner -- test` passes. Before E2 exists, run a 28-night sim on 3 seeds with no exceptions instead.
   - `dotnet build projects/LOAB/Tools/UnityCheck` reports 0 errors.
   - `npm run check` passes.
   - For balance work, run the balance report and paste the key numbers into the log.
6. **Push** to `main`. If the push is rejected, run `git pull --no-rebase`, rerun the gate, then push again.
6b. **Visual check** (since the CI build works): when a push changes the Unity project, wait for the "LOAB WebGL build" run (about 11 minutes; it commits `LOAB: WebGL build from <sha>`). Then load `https://arcade.oriondesign.xyz/play.html?game=loab` in Playwright (Chromium with `--use-gl=swiftshader --enable-unsafe-swiftshader`), check there are no console errors, and screenshot the screens you changed. If the build fails, fixing it comes before any new task.
7. **Log:** tick the task in section 4 and add one line to the progress log: time, task, what changed, verification, and whether it needs a Unity build to be visible.
8. **Stop** at Monday 08:00 UK time: finish the current commit, write the handover in section 9, and cancel the scheduled task.

---

## 7. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Presentation code compiles but looks wrong or breaks in Unity 6 | Medium | Medium | Small, pattern-following changes. Flag anything for visual QA. The owner builds and plays Monday morning. |
| A balance change has unexpected effects elsewhere | High | Medium | Balance bands in tests across several strategies, seeds and difficulties. |
| Save shape changes break existing saves | Medium | High | E5 migration plus a round-trip test. Only add fields; never rename. |
| The `main` branch breaks the arcade deploy | Low | High | The gate includes `npm run check`. LOAB source isn't part of the deployed site. |
| Scope creep in the content tasks | High | Low | Content is P2. Engineering, balance and loop come first. |
| A container restart loses tools | Medium | Low | Step 2 of the protocol reinstalls dotnet. Project files are committed (E1). |

---

## 8. Needs the owner

- **To see the changes in the live game:** nothing to do. `.github/workflows/loab-build.yml` rebuilds WebGL in CI whenever `projects/LOAB/Assets`, `Packages` or `ProjectSettings` change on `main`, commits the build and redeploys the arcade. The owner added the Unity licence secrets on 27 Nov. `projects/LOAB/build.sh` still works for local builds.
- **Visual QA on Monday:** any task marked "needs visual QA" in the log.
- **Design decisions made autonomously** (to be confirmed by the owner) are recorded in the log with the reasoning, for example how Open Question 4 was resolved as tap commands.

---

## 9. Handover (written Monday morning)

_To be written by the final session._

---

## 10. Progress log

| When (UK) | Task | What changed | Verified | Needs build? |
|---|---|---|---|---|
| Fri 27 Nov 16:30 | Plan | Wrote this plan from the brief, a code review and a balance baseline (section 3.2) | `SimRunner` 56 nights × 5 seeds × 3 difficulties; UnityCheck 0 errors | No |
| Fri 27 Nov 16:40 | E1 | Committed `SimRunner.csproj` (net8.0, compiles `Assets/Scripts/Sim`) and `UnityCheck.csproj` (netstandard2.1 against `UnityEngine.Modules` 2021.3.33); `.gitignore` exception for `Tools/*/*.csproj` | Both build from a clean checkout; UnityCheck catches a planted type error | No |
| Fri 27 Nov 16:45 | E2 | `SimRunner -- test`: 17 tests covering determinism, save round trip (System.Text.Json with fields, like JsonUtility), per-night invariants on 3 difficulties × 3 seeds, idle player, every scenario, sandbox, and fuzzed service actions | 17/17 pass in about 11 s | No |
| Fri 27 Nov 16:45 | Bug fix | **The round-trip test found a real bug:** `Game.LastNight` wasn't restored on load, so a reloaded game made different decisions and the report and tips lost the previous night. It's now restored from `history` in the `Game` constructor. | Round trip now identical over 7 nights | Yes (fix is in the sim; the live game needs a rebuild) |
| Fri 27 Nov 16:45 | E3 | `.github/workflows/loab-ci.yml` runs the sim tests and UnityCheck on pushes and pull requests touching `projects/LOAB` | Runs locally; check CI on `main` next session | No |
| Fri 27 Nov 16:45 | Note | Several plan items already exist in part: owner behind the bar, happy hour now, emergency delivery, `ParLevel`, `Train` and `DeepClean`. L1, L2, T4 and S4 should **extend** these, not rebuild them. | n/a | n/a |
| Fri 27 Nov 16:29 | E4 | Added `.github/workflows/loab-build.yml`. It uses GameCI `unity-builder@v4` with the `unityci/editor:6000.2.9f1-webgl` image and runs `LoabBuild.BuildWebGL`. It checks for the four build files, commits them to `main` as `github-actions[bot]`, then dispatches `deploy.yml`, because pushes made with GITHUB_TOKEN don't trigger workflows. It only runs when Unity project files change; a newer push cancels an older build. | First CI run follows this push | Builds automatically from now on |
| Fri 27 Nov 16:45 | E4 verified | The first CI build succeeded in about 11 minutes. The bot committed `97109e1`, the arcade redeployed, and the live game loads with no console errors (Playwright screenshot of the title screen). Added step 6b, visual checks against the live build. | Live smoke test | Done automatically |
| Fri 27 Nov 16:48 | Rename | The owner renamed the game to **Let's Open A Bar!** (the LOAB acronym stays the same). Updated the in-game title, Unity product name (`ProjectSettings` and `LoabBuild`), arcade listing, READMEs, both briefs and workflow comments. Use the new name in all new text. | `npm run check`, UnityCheck 0 errors | Rebuilt by CI |
| Fri 27 Nov 16:52 | E5 | Added `Sim/SaveMigration.cs`, run from the `Game` constructor. It fills every missing list, resizes `awareness` and the nightly vibe arrays, adds drinks introduced later to the menu, and drops unknown items. `GameState.version` now starts at `SaveMigration.Current` (2). | New test loads a faked v1 save (missing lists, 3 awareness values, missing drink, retired item) and plays 5 nights. 18/18 pass, UnityCheck 0 errors | Yes (CI) |
| Fri 27 Nov 16:52 | E6 | Already done: `GameRunner.Save()` calls `PlayerPrefs.Save()`, so the arcade copies saves out within 10 seconds and syncs them for signed-in players. The plugin isn't needed for now. | Code review | No |
| Fri 27 Nov 16:55 | T1 | Added `Tools/SimRunner/Balance.cs` with `dotnet run -c Release -- balance [nights] [seeds]`. It plays sensible and idle careers on all 3 difficulties (5 seeds × 84 nights, about 45 s) and checks 11 target bands, reading complaint shares from `Service.NoteCounts`. **Baseline: 3/11 in range.** Act 1 finishes in week 3.9 (target 4–6). Cash at week 12 on Standard is £91.5k (target £15–35k). Hardcore cash is 82% of Relaxed's (target under 60%). "Dirty" is 84% of customers (target under 40%) and the top complaint every night. Stock-outs are 26% (target under 5%). Slow service is 47%. Event flops are 15% (target 20–35%) and viral hits 25% (target 5–12%). Idle bankruptcy is already right: Standard 5/5, Relaxed 0/5. | `balance 84 5` | No |
| Fri 27 Nov 17:02 | T2 | Added `careless` (splurges on décor, restocks every 3 days, never hires, cleans or books), `cheap` (sensible at 80% prices) and `premium` (sensible at 130% prices) players to the balance report, with 4 more bands. The `themed:<id>` player is deferred to K1. **New findings:** (1) **demand barely reacts to price.** Premium makes £120k by week 12 against £45k for cheap (263%); "Pricey" is the top complaint, but people still pay. (2) **Running the bar badly is never fatal.** Careless never goes bankrupt on any difficulty and still has £11.7k on Hardcore at week 12. Both feed into T5. **4/15 bands in range.** Times in earlier log lines are corrected to the real clock. | `balance 84 5` | No |
| Fri 27 Nov 17:10 | T3 | **Cause:** any single moment below 40% cleanliness, or passing any uncleaned spill, marked a customer "dirty" for the whole night. **Fix:** customers now build up `grime` per minute (below 45% cleanliness, or standing near a spill) and only complain after 8 grime, so they judge the whole visit. The satisfaction penalties are unchanged. Deep Clean now costs `80 + floor area` (£256 at the start, £322 after expanding) instead of a flat £60 that made cleanliness meaningless; the HUD label shows the real cost. The scripted player now hires floor staff and deep cleans when the report says "dirty", as a real player would. **Results:** "dirty" went from 84% to 33% (in band). Act 1 now finishes in week 4.3 (in band). **6/15 bands.** Remaining big ones: stock-outs (19%), pricing (premium earns 5.4× cheap), money (£71k at week 12), careless never bankrupt, event variance. | 18/18 tests, `balance 84 5`, UnityCheck 0 errors | Yes (CI) |
| Fri 27 Nov 17:16 | T4 | Restocking now learns. Each `MenuEntry` tracks `soldTonight` and `missedTonight` (people who wanted it but found it out of stock) and a running `demandAvg` (60/40 EMA). `ParLevel` takes the larger of the old footfall model and `demandAvg × 1.35`, with ×1.3 on Friday to Sunday. New fields are zero-safe for old saves. **Stock-outs went from 19% to 0.3%** (in band). The supplier delivery cap from the plan is deferred to K3 (suppliers). Also found the cause of the pricing problem for T5: price only shifts *which* drink is chosen (`Service` drink weights). It never reduces how much people drink, so raising every price is nearly free. **7/15 bands.** | 18/18 tests, `balance 84 5`, UnityCheck 0 errors | Yes (CI) |
| Fri 27 Nov 17:22 | T5 | **Economy retune.** (1) Wholesale costs ×1.55 (was ×1.25), giving about 55–65% gross margin like UK pub wet sales; the constant is `Content.WholesaleMarkup`. (2) Rent is now `Economy.WeeklyRent`: base £1,400 × difficulty × (1 + 0.9 × reputation/100) × (width/16), a landlord's review as the bar does well. The money panel shows the real figure. (3) Difficulty now changes supplier costs (0.92 / 1 / 1.07) and footfall (1.08 / 1 / 0.93), not just rent and patience. (4) Pricing: `PriceElasticity` 2.2 → 4.0 and `ValueDraw` 0.8 → 2.2. (5) The Act 1 finale is now £10k (was £15k). **Results: 11/15 bands** (was 7). Standard cash at week 12 is £27.5k (was £73.7k); Act 1 finishes in week 5.3; Hardcore ends with 25% of Relaxed's cash; careless goes bankrupt 0% / 40% / 100% by difficulty; sensible never does. **Still out:** (a) cheap pricing isn't viable. At 80% prices the bar floods with students the bar staff can't serve, so slow service drags the rating and margins fall. Premium earns 75× cheap at week 12. To revisit with L1 (owner pulls pints) and service capacity, with a design note that cheap should be a high-volume, high-staff strategy. (b) "Slow service" is the top complaint at 71% (target under 60%): bar throughput limits busy nights. (c) Event variance belongs to T7. | 18/18 tests, `balance 84 5`, UnityCheck 0 errors | Yes (CI) |
| Fri 27 Nov 17:28 | T6 | `NightRecord` now stores `rent`, `utilities`, `loan` and `other`. `Economy.LastWeek()` adds up the last 7 nights and gives last week's profit. Sunday's report shows a **Weekly statement** card: takings, drinks, wages, rent, utilities, loan and other costs, the week's profit, the change on last week, and a note that Sunday's own profit looks low because rent is paid then. | New test checks the lines add up to profit and that rent appears weekly. 19/19, UnityCheck 0 errors | Yes (CI). **Needs visual QA:** play to a Sunday and check the card's layout |
| Fri 27 Nov 17:33 | T7 | Going viral now needs a great show (performance at least 88) **and** a roll. The chance is 20% base, up to +35% from promotion (£600+) and up to +25% from a full room, so promoting matters. The Hit, Decent and Flop lines move to 66, 48 and below 48. **Viral hits 25% → 11%; flops 15% → 18%.** The flop band is now 15–35%, because the scripted players always book the best affordable act and so flop less than real players (design decision, open to change). **13/15 bands.** | 19/19 tests, `balance 84 5`, UnityCheck 0 errors | Yes (CI) |
