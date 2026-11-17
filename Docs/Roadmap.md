# Dead Cargo: Roadmap

Each phase is small, ends in something playable, and is tested in the open scene before the next one starts.

| Phase | Name | Delivers | Status |
|---|---|---|---|
| 1 | Cargo hold | Cargo items, ship hold, pickups, load-based handling, cargo HUD | Done, tested 2026-11-16 |
| 2 | Jettison | Dump cargo from the hold as loose crates that can be re-collected | Done, tested 2026-11-16 (Backlog #1 fixed) |
| 3 | Heat | Heat meter, tiers, decay, heat from weapons and cargo, heat HUD | Done, tested 2026-11-16 |
| 4 | Authority response | Authority ships spawn according to the heat tier | Done, tested 2026-11-16 |
| 5 | Extraction | Jump-drive spool, extraction result, cargo lost on death | Done, tested 2026-11-16 |
| 6 | Mothership hub | 6a: mothership scene, loading screen, launch and return. 6b: save data, stash, credits, selling | Done, tested 2026-11-16 |
| 6.1 | Extraction stakes | Hits and speed hold the spool back; losing the ship costs credits | Done, tested 2026-11-16 |
| 6.2 | Dark matter | A weightless hold slot; the jump drive burns dark matter; leftovers bank and sell | Done, tested 2026-11-16 |
| 6.3 | Extraction point | A second way out: a point in the sector, opened by a timed uplink, open for a limited window | Done, tested 2026-11-16 |
| 6.4 | HUD markers | Hunter count; Dead Cargo's own screen markers for the extraction point, Authority ships and pickups | Done, tested 2026-11-16 |
| 6.5 | Object pooling | Reuse crates and Authority ships instead of creating and destroying them | Planned |
| 6.6 | Scene flow performance | Every scene change through one path, load hitches hidden behind the loading screen, load timing | Planned |
| 6.7 | Polaris sector | The game sector runs launch into, built up from an empty scene. 6.7a: the scene with the star sky. 6.7b: the player starts aboard the ship. 6.7c: pause menu Quit and Restart | Done, tested 2026-11-16 |
| 6.8 | Sector generation | Custom, seed-driven space generation for large sectors, in seven steps. Step 1: sector definition, seeded random, cell grid and debug view | Step 1 and its hardening done, tested 2026-11-16. Step 2 (asteroid field) built, awaiting test. Detail pass, solid rocks (step 5a), natural placement and field dust built and compile-checked, awaiting test. Step 7a (a new sector every load) built, awaiting test |
| 6.9 | Space visuals | Cel-shaded space to match the ships, in six steps. Step 1: a procedural sky | Step 1 built, awaiting test. Step 3b (distance fade) built, awaiting test. Steps 2a (the sun) and 2b (a new sun each run, darker space) built, awaiting test |
| 6.10 | Destruction | Rock that breaks the way you hit it, in five steps, built so later kinds of destruction plug in. Step 1: voxel asteroids that weapons carve and split, with ore veins for mining | Step 1 built and smoke-tested, awaiting test. Step 1b (random breaks, asteroid motion) built, awaiting test. Step 1c (rock trails, optimisation) built, awaiting test. Step 1d (performance pass) built, awaiting test |
| 6.11 | Debug overlay | A frame rate counter always on screen, and switchable channels of live readouts and world drawing for every main system, on Draw Debug Tools | Built and compile-checked, awaiting test |
| 6.12 | Planet flight | Fly from Polaris to Ixara and down to its surface with no loading screen, in three steps: 1 a cruise drive to orbit, 2 the descent to the ground, 3 the planets' painted look. First person on C | Step 1 and first person on C done, tested 2026-11-17. Step 2 (descent) built and compile-checked, awaiting test |
| 7 | Station heist | Dock, on-foot vault, carry loot to the ship | Planned |
| 8 | Mining | Mining tool, ore, refinery value | Planned |
| 9 | Planetary cargo | Landing zones and contract pickups | Planned |
| 10 | Upgrades and crafting | Ship modules, mothership rooms | Planned |
| 11 | Rival syndicates | AI crews, mothership raids | Planned |
| 12 | Cycles | Season reset and persistent rank | Planned |

Phases 1 to 6 form the first complete run: launch, loot, escape, bank. **Milestone reached 2026-11-16.**

Phases 6.1 to 6.6 improve the systems from the first run before phase 7 adds new ones. Phases 6.2 and 6.3 give a run two ways out: burn dark matter to jump anywhere, or reach an extraction point and open it.

Tooling: the **God Menu** (`Dead Cargo > God Menu`) is the editor control panel for testing every phase. It gains a section whenever a phase adds a system.

Setup: **Set Up Sector** in the God Menu **Project** tab runs every phase setup on the open scene in order, and is safe to re-run. The individual phase setups referred to in the test steps below are under `Dead Cargo > Setup`.

---

## Phase 1: Cargo hold

### Goal
Picking up cargo changes how the ship flies.

### Design
- A cargo item has a name, a mass per unit (tonnes) and a value per unit (credits).
- A hold has a mass capacity. Pickups that do not fully fit are partly collected and the rest stays in space.
- Load fraction is hold mass divided by capacity. At full load the ship reaches a set fraction of its empty top speed and turn rate. Defaults: 55% speed, 60% turn.
- The HUD shows mass, capacity, a load bar and the hold's value. It hides when the player is on foot.

### Implementation
| File | Role |
|---|---|
| `Scripts/Cargo/CargoItemDefinition.cs` | Item data asset |
| `Scripts/Cargo/CargoStack.cs` | Item and quantity pair |
| `Scripts/Cargo/CargoHold.cs` | Storage, capacity rules, change events |
| `Scripts/Cargo/CargoLoadHandling.cs` | Applies load to the ship's rigidbody |
| `Scripts/Cargo/CargoPickup.cs` | World pickup collected on contact |
| `Scripts/UI/ExtractionHud.cs` | Run HUD readout for the focused ship. Replaced the phase 1 `CargoHudPanel` |
| `Editor/CargoPhaseSetup.cs` | One-click setup in the open scene |

Handling uses the kit's own physics. Thrust is a force, so raising rigidbody mass lowers acceleration and top speed. Steering in the kit ignores mass, so turn rate is reduced through angular damping instead. No kit file is edited.

### Test steps
1. In Unity, run **Dead Cargo > Setup > Phase 1 - Cargo (Open Scene)**.
2. Press Play, board the fighter and launch.
3. Note top speed and turn rate with an empty hold.
4. Fly through the crates ahead of the ship. The HUD mass, bar and value should rise.
5. Confirm the ship is slower and turns wider as the hold fills.
6. Keep collecting until the hold is full. The HUD shows HOLD FULL and the last crate stays in space.
7. Open the dev console (F6) and press **Empty hold**. Handling returns to normal.

### Tuning
- `CargoHold > Mass Capacity` on the ship.
- `CargoLoadHandling > Full Load Speed Multiplier` and `Full Load Turn Multiplier` on the ship.
- Item mass and value on the assets in `Assets/DeadCargo/Data/Cargo/`.

---

## Phase 2: Jettison

### Goal
The player can trade cargo for speed at any moment.

### Design
- **J** ejects the heaviest stack in the hold as one crate. Heaviest first, because it frees the most handling.
- The crate leaves from behind the ship, inherits the ship's velocity, and drifts to a stop.
- A jettisoned crate cannot be collected for 2 seconds, so the ship does not scoop it straight back up.
- Loose crates stay in the world and can be collected again by any ship with a hold.
- Jettison only works in a vehicle that has the jettison component. It does nothing on foot.

### Implementation
| File | Role |
|---|---|
| `Scripts/Cargo/CargoJettison.cs` | Removes stacks from the hold and spawns crates |
| `Scripts/Cargo/CargoJettisonInput.cs` | Player key binding, built on the kit's vehicle input pattern |
| `Scripts/Cargo/CargoPickup.cs` | Gains a collection delay |
| `Prefabs/Cargo/CargoCrate.prefab` | Drifting crate, created by the setup tool |
| `Editor/JettisonPhaseSetup.cs` | One-click setup in the open scene |
| `Editor/SceneSetupUtility.cs` | Helpers shared by the setup tools |

The input script sits on a `CargoControls` child of the player's game agent. The kit hands it the current vehicle on entry, the same way it drives its own input scripts.

### Test steps
1. In Unity, run **Dead Cargo > Setup > Phase 2 - Jettison (Open Scene)**.
2. Press Play, board the fighter, launch and collect a few crates.
3. Press **J**. A crate appears behind the ship, the HUD mass drops and handling improves.
4. Press **J** until the hold is empty. Each press drops the heaviest remaining stack.
5. Turn around and fly through a dropped crate. It is collected again.
6. Stop the ship, press **J**, and reverse into the crate at once. It is not collected until 2 seconds have passed.
7. Fill the hold, open the dev console (F6) and press **Jettison all**. Every stack is ejected and the crates drift apart.
8. Exit the fighter and press **J** on foot. Nothing happens.

### Tuning
On the ship's `CargoJettison` component: `Eject Offset`, `Eject Speed`, `Scatter Speed`, `Dump Spacing`, `Recollect Delay`.

Each crate leaves at the full scatter speed, at right angles to its exit. Jettison All also fans the crates out around the ejection point, `Dump Spacing` apart, so a dump separates at once (Backlog #1).

---

## Phase 3: Heat

### Goal
The game measures how much attention the player has drawn. Nothing responds to it yet; that is phase 4.

### Design
- Heat runs from 0 to 100.
- Firing a weapon adds 0.15 heat per shot. Taking cargo aboard adds 0.5 heat per tonne.
- After 6 seconds with no new heat, heat decays at 2 per second.
- Tiers: CLEAR (0), NOTICED (25), WANTED (50), HUNTED (75). Each has a colour.
- The HUD shows a heat bar and the tier name, coloured by tier, above the cargo panel.

### Implementation
| File | Role |
|---|---|
| `Scripts/Heat/HeatTracker.cs` | Scene service: heat value, decay, tiers, change events |
| `Scripts/Heat/HeatTier.cs` | Tier data: label, threshold, colour |
| `Scripts/Heat/WeaponHeatSource.cs` | Adds heat when the ship's weapons fire |
| `Scripts/Heat/CargoHeatSource.cs` | Adds heat when cargo is taken aboard |
| `Scripts/Cargo/CargoHold.cs` | Gains an `Added` event |
| `Scripts/UI/ExtractionHud.cs` | Run HUD readout. Replaced the phase 3 `HeatHudPanel` |
| `Editor/HeatPhaseSetup.cs` | One-click setup in the open scene |

Heat sources sit on the player's ship, so AI ships never raise heat. `WeaponHeatSource.Rebind()` must be called if the ship's weapons are swapped at runtime.

### Test steps
1. In Unity, run **Dead Cargo > Setup > Phase 3 - Heat (Open Scene)**.
2. Press Play. The HEAT panel shows CLEAR with an empty bar.
3. Board the fighter, launch and fire. The bar rises while firing.
4. Stop firing. After about 6 seconds the bar starts to fall.
5. Collect a crate. Heat jumps by half the crate's mass in tonnes.
6. Open the God Menu or dev console and add heat in steps of 25. The tier changes to NOTICED, WANTED, then HUNTED, with a new colour each time.
7. Press **Clear heat**. The panel returns to CLEAR.

### Tuning
- `Heat Tracker` object: `Max Heat`, `Decay Delay`, `Decay Per Second`, `Tiers`.
- On the ship: `WeaponHeatSource > Heat Per Shot`, `CargoHeatSource > Heat Per Tonne`.

---

## Phase 4: Authority response

### Goal
Heat has consequences. The hotter the run, the more Authority ships hunt the player.

### Design
- Each heat tier sets how many Authority ships may be active and how often reinforcements arrive:

| Tier | Max ships | Reinforce every |
|---|---|---|
| CLEAR | 0 | n/a |
| NOTICED | 1 | 10 s |
| WANTED | 2 | 7 s |
| HUNTED | 4 | 5 s |

- When heat rises into a higher tier, the next ship arrives after 2 seconds.
- Ships arrive 400 m from the player, from a random direction above the horizon, facing the player.
- Destroying an Authority ship adds 10 heat, so fighting back escalates the response.
- When heat has been back at CLEAR for 10 seconds, the remaining ships withdraw.
- Authority ships use the kit's existing enemy pilot and fighter, so they fight with the kit's AI.

### Implementation
| File | Role |
|---|---|
| `Scripts/Authority/AuthorityResponse.cs` | Spawns, tracks and withdraws Authority ships by heat tier |
| `Scripts/Authority/AuthorityTierResponse.cs` | Per-tier data: ship limit and reinforcement interval |
| `Editor/AuthorityPhaseSetup.cs` | One-click setup in the open scene |
| `Editor/GodMenu/Tabs/AuthorityTab.cs` | God Menu tab |

The setup tool copies the pilot and ship prefabs from the AI spawn point already in the scene. Dedicated Authority ship types come later.

### Test steps
1. In the God Menu **Project** tab, press **Set Up** next to Phase 4 - Authority.
2. Press Play, board the fighter and launch.
3. In the God Menu **Heat** tab, click NOTICED. Within about 2 seconds one Authority ship arrives and attacks.
4. Click WANTED, then HUNTED. The active count climbs to 2, then 4, shown in the **Authority** tab.
5. Destroy a ship. Heat jumps by 10 and a replacement arrives after the tier's interval.
6. Tick **Invulnerable** in the **Player** tab if you need to survive long enough to watch.
7. Click **Clear** in the Heat tab. After 10 seconds the remaining ships vanish.
8. Untick **Auto Spawn** in the Authority tab, raise heat, and confirm nothing arrives. **Spawn Ship** still sends one by hand.

### Tuning
On the `Authority Response` object: `Tier Responses`, `Escalation Delay`, `Stand Down Delay`, `Spawn Distance`, `Heat Per Ship Destroyed`.

---

## Phase 5: Extraction

### Goal
A run has an ending. Spool the jump drive and survive to keep the hold, or lose the ship and lose everything in it.

### Design
- **H** starts spooling the jump drive. **H** again cancels. It only works in a ship that has a jump drive.
- Spool time is 12 seconds plus 0.2 seconds per tonne in the hold, fixed when the spool starts. A full 40 t hold spools for 20 seconds.
- A spooling drive emits 2 heat per second. Heat cannot decay during a spool, and a long spool can push heat into a higher tier, so extraction usually happens under fire.
- The spool cancels if the player leaves the ship.
- When the spool completes the ship jumps out and the run is **EXTRACTED**. The hold's contents are banked. Phase 6 stores them on the mothership.
- If the run ship is destroyed, or the player dies, the run is **SHIP LOST**. The hold is emptied and its contents are gone.
- The run ship is the last ship with a jump drive that the player boarded. Losing it while on foot still ends the run.
- When the run ends the player loses control. The report appears after a white jump flash (extraction) or after 2.5 seconds so the destruction plays out (loss). The world pauses behind it.
- The report replaces the kit's GAME OVER menu, which the kit opens when the player dies. Once the run ends all input is locked, including the kit's Esc pause menu, and the report blocks clicks to anything beneath it.
- The report shows the outcome, run time, peak heat tier, Authority ships destroyed, the cargo list, and the total banked or lost. **Enter** starts a new run by reloading the scene.
- The HUD gains a jump drive plate: READY with the spool time for the current load, or SPOOLING with a countdown and progress bar.

Not in this phase: a jump-out visual effect on the ship, and storing banked cargo (phase 6).

### Implementation
| File | Role |
|---|---|
| `Scripts/Extraction/JumpDrive.cs` | Spool timer, spool heat, jump event |
| `Scripts/Extraction/JumpDriveInput.cs` | Player key binding, built on the kit's vehicle input pattern |
| `Scripts/Extraction/RunTracker.cs` | Scene service: tracks the run ship, ends the run once, records the result, restarts |
| `Scripts/Extraction/RunResult.cs` | Outcome, stats and cargo of a finished run |
| `Scripts/Extraction/RunOutcome.cs` | Extracted or Lost |
| `Scripts/UI/RunResultScreen.cs` | Shows the report, flash and pause when the run ends |
| `Scripts/UI/RunResultView.cs` | Builds the report UI |
| `Scripts/UI/HudFactory.cs` | Shared HUD palette and element builders, moved out of the HUD view |
| `Scripts/UI/ExtractionHud.cs`, `ExtractionHudView.cs` | Gain the jump drive plate |
| `Editor/ExtractionPhaseSetup.cs` | One-click setup in the open scene |
| `Editor/GodMenu/Tabs/ExtractionTab.cs` | God Menu tab |

The jump drive and its input follow the jettison pattern: the drive sits on the ship, and the input script sits on a `JumpControls` child of the player's game agent.

### Test steps
1. Open the test range. In the God Menu **Range** tab, press **Spawn Everything** if the scene is blank.
2. In the God Menu **Project** tab, press **Set Up** next to Phase 5 - Extraction.
3. Press Play, board the fighter and launch. The JUMP DRIVE plate shows READY and 12 s.
4. Collect a few crates. The READY time rises with the load.
5. Press **H**. The plate shows SPOOLING with a countdown, a notice appears, and heat climbs steadily.
6. Press **H** again. The spool cancels and the plate returns to READY.
7. Press **H** and survive until the countdown ends. The screen flashes white and the report shows EXTRACTED with the run time, peak heat and cargo banked.
8. Press **Enter**. The scene reloads for a new run.
9. Collect cargo, then press **Destroy Vehicle** in the God Menu **Player** tab. After about 2.5 seconds the report shows SHIP LOST with the cargo lost, and the hold is empty. The kit's GAME OVER menu does not appear, and pressing **Esc** while the report is up does nothing.
10. Start a spool, press **F** to exit the ship, and confirm the spool cancels.
11. In the **Extraction** tab, **Jump Now** completes a jump at once, and **Restart Run** reloads the scene.
12. When the phase passes, save the open scene as the range template from the **Range** tab, so the template includes phase 5.

### Tuning
- On the ship's `JumpDrive`: `Base Spool Time`, `Spool Time Per Tonne`, `Heat Per Second`.
- On the `Run Result Screen` object: `Jump Reveal Delay`, `Loss Reveal Delay`.

---

## Phase 6: Mothership hub

### Goal
The run becomes a loop: start on the mothership, launch into a sector, come home. Phase 6 is built in two parts, each tested before the next.

- **6a, scene flow:** a Mothership scene with a hub screen, a loading screen between scenes, launch and return.
- **6b, profile:** save data, the cargo stash, credits and selling. Planned after 6a passes.

### Design (6a)
- The game has two kinds of scene: the **mothership** (`Assets/DeadCargo/Scenes/Mothership.unity`) and a **sector**, where runs happen. The Dev Test Range is the sector for now.
- The mothership is a hub screen for now. It shows the last run's outcome and stats, and a **LAUNCH RUN** button with the sector's name. Click it or press **Enter** to launch.
- Every scene change goes through a loading screen: fade to black, the destination title (JUMPING TO SECTOR, RETURNING TO MOTHERSHIP), the scene name and a progress bar, then fade in. It shows for at least 0.8 seconds so it never flickers, and it blocks input while it is up.
- The run report's **Enter** now returns to the mothership. Without a mothership scene set up it restarts the run, as in phase 5.
- The last run's result is carried to the mothership in memory. Phase 6b saves it.
- Play can start in either scene. Starting in the sector runs as before.
- The scene paths live in `Resources/SceneFlowSettings.asset`, so any scene can find them. The setup tool adds both scenes to the Build Settings, with the mothership first, because a build starts there.
- The dev console no longer pops open when a sector is entered through the loading screen. It still opens when Play starts in the range.

### Implementation (6a)
| File | Role |
|---|---|
| `Scripts/Flow/SceneFlow.cs` | Static entry point: go to the mothership, launch the sector, load any scene |
| `Scripts/Flow/SceneFlowSettings.cs` | Data asset holding the mothership and sector scene paths |
| `Scripts/Flow/LoadingScreen.cs` | Persistent loading screen: fade, async load, progress, fade back |
| `Scripts/Mothership/MothershipHub.cs` | Hub screen behaviour: last run, launch |
| `Scripts/UI/MothershipHubView.cs` | Builds the hub UI |
| `Scripts/Extraction/RunTracker.cs` | Keeps the last result, returns to the mothership |
| `Editor/MothershipPhaseSetup.cs` | Creates the mothership scene and settings, updates the Build Settings |
| `Editor/SceneFlowSettingsEditor.cs` | Scene pickers for the settings asset |
| `Editor/GodMenu/Tabs/MothershipTab.cs` | God Menu tab: open and load scenes |

### Test steps (6a)
1. Open the Dev Test Range with the phase 5 setup in it. In the God Menu **Project** tab, press **Tidy Open Scene**. The Dead Cargo objects move into the Systems, UI and Environment folders, and the kit objects stay at the root. Press Play once to confirm the range still works as before. (Passed 2026-11-16. The tidy also removed the superseded Cargo HUD and Heat HUD objects; that step has since been retired.)
2. In the **Project** tab, press **Set Up** next to Phase 6 - Mothership. A Mothership scene is created in `Assets/DeadCargo/Scenes/`, and both scenes appear in the Build Settings.
3. Press Play in the range. Run as normal, extract, and press **Enter** on the report. The loading screen shows RETURNING TO MOTHERSHIP and the hub appears with the last run's result.
4. Click **LAUNCH RUN**, or press **Enter**. The loading screen shows JUMPING TO SECTOR and the range loads. The dev console does not pop open.
5. Lose a run instead. The hub shows SHIP LOST.
6. In the God Menu **Mothership** tab, open the Mothership scene and press Play. The hub shows "No runs yet". Launch works from there too.
7. Press **Go To Mothership** and **Launch Sector** in the Mothership tab during play. Both go through the loading screen.
8. When it passes, reopen the range and save it as the template from the **Range** tab, so the template is tidy too.

### Tuning
Scene paths on `Assets/DeadCargo/Resources/SceneFlowSettings.asset`.

### Design (6b)
- The player has one **profile**, saved as JSON in Unity's persistent data folder (`profile.json`). It is saved straight after every change, so quitting at any point loses nothing.
- The profile holds credits, the **stash** (item and quantity), the count of runs extracted and lost, and a summary of the last run.
- When a run is **extracted**, the hold's cargo is added to the stash. A **lost** run banks nothing. Either way the run is counted and becomes the last run. This happens the moment the run ends, in any sector scene.
- The hub shows credits (top right), the last run and the service record (left), the stash (right) and the launch button (bottom right).
- Each stash row shows the item, quantity, mass and value, with a **SELL** button that sells the whole stack. **SELL ALL**, or **S**, sells everything. Items sell at their full unit value. Credits update at once.
- Cargo items gain a stable **ID** for save data, set from the asset name the first time. The **cargo catalog** (`Resources/CargoCatalog.asset`) lists every item so saves can find them. It refreshes on its own when cargo items are added or removed.
- A broken save file is kept beside the original as `profile.corrupt.json` and a fresh profile starts, so a bad file never blocks the game.
- The profile records a version number, so later phases can migrate old saves.

### Implementation (6b)
| File | Role |
|---|---|
| `Scripts/Profile/PlayerProfile.cs` | Saved data: credits, stash, record, last run |
| `Scripts/Profile/StashEntry.cs` | Item ID and quantity |
| `Scripts/Profile/RunSummary.cs` | Saved summary of a finished run |
| `Scripts/Profile/ProfileStore.cs` | Load, save, bank, sell, reset. Raises `Changed` |
| `Scripts/Cargo/CargoCatalog.cs` | Every cargo item, found by ID |
| `Scripts/Cargo/CargoItemDefinition.cs` | Gains `Id` |
| `Scripts/Extraction/RunTracker.cs` | Records each run and banks extracted cargo |
| `Scripts/Mothership/MothershipHub.cs`, `Scripts/UI/MothershipHubView.cs` | Credits, stash, selling, service record |
| `Editor/CargoCatalogBuilder.cs` | Keeps the catalog and item IDs current |
| `Editor/MothershipPhaseSetup.cs` | Also creates the catalog |
| `Editor/GodMenu/Tabs/MothershipTab.cs` | Profile section: credits, stash, reset, save folder |

### Test steps (6b)
1. With the range open, press **Re-run** next to Phase 6 - Mothership in the **Project** tab. `Assets/DeadCargo/Resources/CargoCatalog.asset` appears with the three cargo items.
2. Press Play in the range, collect cargo, extract and return to the mothership. The stash lists the cargo and credits read 0 cr.
3. Press **SELL** on one row. That stack disappears and credits rise by its value. Press **SELL ALL** (or **S**) to sell the rest.
4. Launch, collect cargo, and lose the ship. Back on the mothership the stash is unchanged and the service record counts one loss.
5. Stop Play and press Play in the Mothership scene. Credits, stash, record and last run are all still there.
6. In the God Menu **Mothership** tab, the Profile section shows the same numbers. **+1,000 cr** adds credits, and the hub updates during play. **Reset Profile** asks first, then clears everything. **Open Save Folder** shows `profile.json`.

---

## Phase 6.1: Extraction stakes

### Goal
Extraction is the tense climax of a run, and losing the ship hurts beyond the hold.

### Design
- **Hits hold the spool back.** Each hit on the ship's hull or shields sets the spool back 0.5 seconds, at most once every 0.5 seconds. Under constant fire the spool stalls, so the player has to break line of fire or fight back. Hits are read from the kit's damage receivers, gathered when the spool starts, so shield modules count and hits still register with the Invulnerable cheat on.
- **The spool needs low speed.** It only builds while the ship is below 50 m/s. Above that it holds (it does not cancel) and the jump plate reads TOO FAST, so the player has to commit to sitting still.
- The jump plate reads DISRUPTED for a moment after each knock-back. A notice explains the speed limit the first time the spool holds for speed.
- Heat keeps rising while a spool is held, because the drive is still charged.
- **Losing the ship costs credits.** When the run ship is destroyed, a 500 cr replacement fee is taken from the profile. It never takes credits below zero, so a broke player still gets a basic hull for free. Dying on foot with the ship intact charges nothing.
- The run report and the hub's last run show the fee, or "Basic hull issued" when the player could not pay.
- A run lost on foot with the ship intact now reads **RUN LOST** rather than SHIP LOST, on the report and the hub.
- No blockade: the Authority blockade at HUNTED is parked in the backlog's Later list.

### Implementation
| File | Role |
|---|---|
| `Scripts/Extraction/JumpDrive.cs` | Spool time builds only below the speed limit; hits knock it back |
| `Scripts/Extraction/RunTracker.cs` | Works out the ship-loss fee when the run ends |
| `Scripts/Extraction/RunResult.cs`, `Scripts/Profile/RunSummary.cs` | Carry whether the ship was lost and the fee paid |
| `Scripts/Profile/PlayerProfile.cs` | Takes the fee from credits when the run is recorded |
| `Scripts/UI/ExtractionHud.cs`, `ExtractionHudView.cs` | TOO FAST and DISRUPTED states on the jump plate |
| `Scripts/UI/RunResultView.cs`, `MothershipHubView.cs` | Show the fee |
| `Editor/GodMenu/Tabs/ExtractionTab.cs` | Shows the held and disrupted states |

### Test steps
1. Press Play in the range, board the fighter and launch. Fly faster than 50 m/s and press **H**. The jump plate reads TOO FAST, the countdown holds, a notice names the speed limit, and heat still rises.
2. Slow below 50 m/s. The countdown runs again.
3. Spawn an Authority ship (Heat tab, or Authority tab **Spawn Ship**) and let it hit you while spooling. The plate flashes DISRUPTED and the countdown climbs back. Under steady fire it stalls.
4. In the Mothership tab, give yourself **+1,000 cr**. Lose the ship. The report shows SHIP REPLACEMENT −500 cr, and the hub shows 500 cr left and the fee on the last run.
5. Spend down to 0 cr (or **Reset Profile**) and lose the ship again. The report shows BASIC HULL ISSUED, and credits stay at 0.
6. Exit the ship on foot and get the player killed while the ship is intact. The report reads RUN LOST and no fee is charged.

### Tuning
- On the ship's `JumpDrive`: `Max Spool Speed`, `Knockback Per Hit`, `Knockback Interval`.
- On the `Run Tracker` object: `Ship Replacement Cost`. It moves to the ship's hull data when hulls arrive in phase 10.

---

## Phase 6.2: Dark matter

### Goal
The jump drive needs fuel, and that fuel is worth money. Jumping out becomes a choice: burn dark matter to leave from anywhere, or keep it to sell and find another way out (phase 6.3).

### Design
- **Dark Matter** is a cargo item with its own **slot** in the hold, separate from the mass-limited hold. The slot holds up to 200 units.
- Dark matter is **weightless**. It does not slow the ship, add spool time, use hold mass, or raise heat when collected.
- It is **part of the hold**. It shows in the manifest, counts towards value at risk, is lost with the ship, banks into the stash on extraction, and sells at the mothership for 15 cr a unit.
- It **cannot be jettisoned**: the slot is sealed. J and Jettison All only dump ordinary cargo.
- **The jump drive burns 100 units per jump.** H only starts a spool with at least 100 units aboard. Otherwise a notice reads NOT ENOUGH DARK MATTER with the amount held. The units are burned when the jump completes, so a cancelled spool costs nothing. Whatever is left banks.
- The jump plate doubles as the fuel gauge. Short of fuel it reads DARK MATTER with "40 / 100" and a violet bar filling towards a jump. With enough fuel it reads READY and the spool time, as before.
- The phase 6.1 spool rules still apply: hits knock the spool back, and it needs low speed.
- **Dark matter pickups** are violet glowing spheres worth 25 units each. The sector setup scatters six around the test range (150 units). The World tab can spawn more.
- A jump burns 100 units, worth 1,500 cr, which makes the jump a real cost.

### Implementation
| File | Role |
|---|---|
| `Scripts/Cargo/CargoSlot.cs` | Hold or DarkMatter |
| `Scripts/Cargo/CargoItemDefinition.cs` | Gains `Slot`; dark matter weighs nothing |
| `Scripts/Cargo/CargoHold.cs` | Dark matter slot capacity, units, `ConsumeDarkMatter` |
| `Scripts/Cargo/CargoJettison.cs` | Skips the sealed dark matter slot |
| `Scripts/Extraction/JumpDrive.cs` | Needs and burns dark matter; raises `FuelShortage` |
| `Scripts/UI/ExtractionHud.cs`, `ExtractionHudView.cs`, `HudFactory.cs` | Fuel gauge on the jump plate, shortage notice, dark matter colour |
| `Editor/DarkMatterSetup.cs` | Creates the Dark Matter item, its glowing pickup prefab, and the range's pickups. Called by the extraction setup |
| `Editor/SectorSetup.cs` | The Extraction step also counts as missing until the scene has dark matter pickups |
| `Editor/GodMenu/Tabs/CargoTab.cs`, `ExtractionTab.cs`, `WorldTab.cs` | Dark matter readouts, and spawning it as its sphere |

The God Menu's **Jump Now** still forces a jump without fuel, for testing.

### Test steps
1. Open the range. The God Menu **Project** tab reads "Not a complete sector: Missing Extraction". Press **Set Up Sector**. A Dark Matter item appears in `Data/Cargo`, and six violet spheres appear in the range among the cargo pickups.
2. Press Play, board and launch. The jump plate reads DARK MATTER 0 / 100. Press **H**: a notice says there is not enough.
3. Fly through dark matter spheres. The plate's bar fills, the manifest lists Dark Matter, and handling and hold mass do not change.
4. Press **J** and **Jettison All**. Dark matter stays aboard.
5. With 100 or more units, press **H** and complete the jump. The report lists the remaining dark matter as banked cargo. On the mothership it can be sold at 15 cr a unit.
6. Cancel a spool part-way. No dark matter is burned.
7. When it passes, save the range as the template from the **Range** tab, so the template has the dark matter pickups.

### Tuning
- On the ship's `CargoHold`: `Dark Matter Capacity`.
- On the ship's `JumpDrive`: `Dark Matter Per Jump`.
- On `Data/Cargo/DarkMatter.asset`: unit value.

---

## Phase 6.3: Extraction point

### Goal
A second way out that costs no dark matter but has to be earned: reach the point, open it, hold out, and get back to it in time.

### Design
- An **extraction point** sits in the sector: an upright ring of lights 50 m across, facing the approach, with a beacon at its centre. It starts **dormant**. Entering means coming within 25 m of the centre.
- Within 150 m of it, in the run ship, press **U** to start the **uplink**. The uplink takes 45 seconds and broadcasts the player's position: +15 heat at once, then +0.5 heat per second while it runs. The player does not have to stay nearby.
- When the uplink completes, the point is **open** for 60 seconds. A beam rises from it. Flying the run ship into the ring, with the player aboard, extracts the run. No dark matter is burned, and any jump spool in progress is dropped.
- When the window ends, the point **closes** and goes dormant. It can be uplinked again.
- The point's colour shows its state: grey-blue dormant, pulsing amber while uplinking, green when open.
- The HUD gains an **EXTRACTION POINT** plate above the jump plate. It shows the state (DORMANT, IN RANGE, UPLINK, OPEN), the distance or the time left, a progress bar, and the **U** prompt. Notices announce the uplink starting, the point opening, and the point closing.
- The report says how the run got out: "Jump complete" or "Extraction point reached".
- Hits and speed still matter only for the jump drive. The extraction point has no spool, so it is the safer route for a damaged ship.

### Implementation
| File | Role |
|---|---|
| `Scripts/Extraction/ExtractionPoint.cs` | States, uplink and open timers, uplink heat, extraction on entry, state colours |
| `Scripts/Extraction/ExtractionPointState.cs` | Dormant, Uplinking, Open |
| `Scripts/Extraction/ExtractionRoute.cs` | Jump drive or extraction point |
| `Scripts/Extraction/UplinkInput.cs` | U starts the uplink of the nearest point in range |
| `Scripts/Extraction/RunTracker.cs`, `RunResult.cs` | Extraction at a point, and the route on the result |
| `Scripts/UI/ExtractionHud.cs`, `ExtractionHudView.cs` | Extraction point plate and notices |
| `Scripts/UI/RunResultView.cs` | Says which route got the run out |
| `Editor/ExtractionPointSetup.cs` | Builds the extraction point prefab, places one in the sector, adds the U input |
| `Editor/SectorSetup.cs`, `Editor/TestRange/TestRangeLibrary.cs` | New setup step; the point is spawnable range content |
| `Editor/GodMenu/Tabs/ExtractionTab.cs` | Extraction Point section: state, timers, uplink, open, close, fly there |

### Test steps
1. Open the range. The **Project** tab reads "Missing: Extraction Point". Press **Set Up Sector**. An **Extraction Points** object appears with a grey-blue ring about 700 m ahead of the ship. Move it anywhere you like. The **Range** tab lists it as spawnable content.
2. Press Play, board and launch. The EXTRACTION POINT plate shows DORMANT and the distance.
3. Fly within 150 m. The plate reads IN RANGE. Press **U**. The ring pulses amber, the plate counts down 45 s, a notice warns of the broadcast, and heat jumps by 15 then keeps rising.
4. When the uplink completes, the ring turns green, a beam rises, and the plate counts down 60 s. Fly into the ring. The run ends EXTRACTED, the report says "Extraction point reached", and no dark matter is burned.
5. Start another run, uplink, and stay away until the window ends. The point closes, a notice says so, and it can be uplinked again.
6. **Phase 6.1 re-check:** with 100 dark matter, spool the jump drive while an Authority ship shoots you, with **Invulnerable** on. The jump plate flashes DISRUPTED and the countdown climbs back.
7. In the **Extraction** tab, the Extraction Point section's **Go To Point**, **Start Uplink**, **Open Now** and **Close** buttons work.
8. When it passes, save the range as the template from the **Range** tab.

### Tuning
On the extraction point: `Entry Radius`, `Activation Range`, `Uplink Duration`, `Open Duration`, `Uplink Heat Burst`, `Uplink Heat Per Second`.

---

## Phase 6.4: HUD markers

### Goal
The player can see where to go and what is coming: the way out, the hunters, and loot nearby.

### Design
- **Dead Cargo draws its own markers** instead of using the kit's radar. The kit's radar, target boxes and target cycling all come from one tracker, so putting crates on it would box every crate and add them to target cycling. Combat targeting stays exactly as it was.
- **Extraction point:** always marked, at any distance. A diamond in the point's state colour, with its state and distance (EXTRACTION 820 m, UPLINK 820 m, OPEN 340 m). Off-screen, it becomes an arrow on the screen edge pointing the way.
- **Authority ships:** a red diamond with AUTHORITY and the distance. Off-screen, a red arrow on the edge. The kit's own target boxes and arrows stay as well.
- **Pickups within 800 m:** small diamonds, violet for dark matter and amber for cargo, shown only while on screen, with no label, so they never clutter the edges. They fade with distance.
- **Hunter count:** the AUTHORITY HEAT plate's title adds "· 2 HUNTERS" in red while Authority ships are active.
- Markers show only while the player flies a ship with a hold, and sit behind the HUD plates.
- Edge arrows keep clear of the plates. Towards the lower right an arrow stays on its bearing from the screen centre and stops just short of the plate stack, so the plates never hide it. This follows the stack as it grows (manifest, notices).
- Labels only rebuild when a shown number changes, so the markers make no per-frame allocations.

### Implementation
| File | Role |
|---|---|
| `Scripts/UI/HudMarkerLayer.cs` | Marker pool: projects world positions, clamps off-screen markers to the edge |
| `Scripts/UI/HudMarkerGlyph.cs` | Diamond and arrow shapes, drawn as a mesh like the other HUD graphics |
| `Scripts/UI/HudMarkerStyle.cs` | Size and off-screen behaviour for a kind of marker |
| `Scripts/UI/ExtractionHud.cs`, `ExtractionHudView.cs` | Feed the markers each frame; hunter count on the heat plate |
| `Scripts/Cargo/CargoPickup.cs` | Keeps a list of active pickups |
| `Scripts/Authority/AuthorityResponse.cs` | `Active` instance and the list of active ships |

### Test steps
1. Press Play in the range, board and launch. A marker labelled EXTRACTION with a distance sits over the extraction point. Turn away from it: it becomes an arrow on the screen edge pointing back.
2. Fly towards the pickups. Violet diamonds mark dark matter and amber ones mark cargo within 800 m. Collected pickups lose their marker.
3. Uplink the point. Its marker turns amber and reads UPLINK, then green and OPEN when it opens.
4. Raise heat to WANTED. Red AUTHORITY markers with distances appear over the hunters, and red arrows when they are behind you. The heat plate's title reads "· 2 HUNTERS".
5. Turn so the extraction point is off-screen to the lower right. Its arrow sits beside or above the HUD plates, never behind them. Press **I** to open the manifest: the arrow moves clear of the taller stack.
6. Exit the ship on foot. The markers disappear. Board again and they return.

### Tuning
Constants in `ExtractionHud`: `PickupMarkerRange` (800 m) and the marker styles (sizes, and whether off-screen arrows show). Colours come from `HudFactory`.

---

## Phase 6.7: Polaris sector

### Goal
The game gets its own sector scene, separate from the Dev Test Range. It is built up in small parts, starting from empty space.

### Design (6.7a)
- **Polaris** (`Assets/DeadCargo/Scenes/Polaris.unity`) is the first raid location and the game sector: the scene the player spawns into from the mothership. It was first created as "Deep Space" and is renamed automatically.
- It starts with only the sky: Dead Cargo's star skybox (`Materials/DeepSpaceSkybox.mat`: the kit's plain star texture on all six sides, so there is no bright horizon band), flat ambient light matching the test range, and a sun (a directional light in the `Environment` folder) so ships are lit when they arrive. No floor, no player, no systems.
- The mothership still launches into the Dev Test Range. Polaris becomes the launch target when it has a player rig and has been set up as a sector.
- The Dev Test Range stays the place for testing systems.

### Implementation (6.7a)
| File | Role |
|---|---|
| `Editor/SectorSceneSetup.cs` | Creates the Polaris scene the first time, and opens it after that |
| `Editor/GodMenu/Tabs/MothershipTab.cs` | **Open Polaris** button in the Scene Flow section |

### Test steps (6.7a)
1. Outside Play Mode, open the God Menu **Mothership** tab and press **Open Polaris** (or run **Dead Cargo > Setup > Open Polaris Scene**). Unity offers to save the open scene first.
2. `Polaris.unity` appears in `Assets/DeadCargo/Scenes/` and opens. The Scene view shows the star sky. The hierarchy holds only `Environment > Directional Light`.
3. Press the button again from another scene. The existing Polaris scene opens unchanged.
4. Open the Mothership scene, press Play and launch. The run still starts in the Dev Test Range.

### Design (6.7b)
- In Polaris the player **starts the run aboard the ship**. There is no on-foot start, no floor and no boarding step.
- The player rig (player, ship, cameras, kit scene objects, the run systems and the HUD) comes from the test range template, so it always matches the tested range. The range's own objects are left out: the on-foot character, the floor, the enemy spawn and the dev bootstrap. Polaris keeps its own sky lighting.
- The ship starts at the origin, in the state its prefab gives it. The kit's landing system is untouched; parked starts become their own system later.
- With no character aboard, the exit key does nothing in Polaris.
- Setting up the rig also makes Polaris the sector the mothership launches into, and adds it to the Build Settings. The Dev Test Range is still played by pressing Play in it.
- Polaris has no cargo, dark matter or extraction point yet, so a run there has no way out. Use the God Menu (**Extraction > Jump Now**, or **Mothership > Go To Mothership**) to leave until content arrives.
- The pause menu **Quit** and **Restart** buttons are wired up in 6.7c.

### Implementation (6.7b)
| File | Role |
|---|---|
| `Editor/SectorRigSetup.cs` | Spawns the rig from the template, strips the range objects, starts the player aboard the ship, makes the scene the launch target |
| `Editor/GodMenu/Tabs/ProjectTab.cs` | **Spawn Player In Ship** button, shown for a game sector with no player |

### Test steps (6.7b)
1. Open Polaris (**Mothership** tab, **Open Polaris**). The **Project** tab reads "Game sector: no player yet". Press **Spawn Player In Ship**.
2. The hierarchy gains `Systems`, `UI`, `Player`, `Fighter`, `SceneObjects` and `VehicleCamera`. There is no `Level`, `FirstPersonCharacter`, `AI Fighter Spawn` or `Dev Test Range` object, and only one `Directional Light`. The **Mothership** tab's Sector field reads Polaris.
3. Press Play in Polaris. You are in the cockpit at once, flying among the stars, with the HUD up. The dev console does not open.
4. Stop, open the Mothership scene, press Play and launch. The loading screen reads JUMPING TO SECTOR, POLARIS, and you arrive aboard the ship.
5. In the God Menu **Extraction** tab press **Jump Now**. The report shows EXTRACTED and **Enter** returns to the mothership.
6. Open the Dev Test Range and press Play. It still starts on foot, as before.

### Design (6.7c)
- The kit's pause menu works in a game sector. **Quit** gives up the run and returns to the mothership. **Restart** gives up the run and starts a new one in the same sector.
- Giving up counts as a **lost run**: the hold is forfeit and the service record counts a loss. The ship is intact, so there is no replacement fee, and the hub's last run reads RUN LOST. This stops quitting being a free way out of a bad run.
- No report is shown for an abandoned run. The loading screen takes over at once.
- The run tracker binds the buttons itself when the scene starts, so every sector with a run tracker gets this with no setup step.
- The Dev Test Range rebinds them afterwards: Quit stops play and Restart reloads the range, and neither counts as a loss.

### Implementation (6.7c)
| File | Role |
|---|---|
| `Scripts/Extraction/RunTracker.cs` | `AbandonToMothership` and `AbandonAndRestart`; binds the kit menus' Quit and Restart buttons to them when the scene starts |
| `Scripts/UI/RunResultScreen.cs` | Stays hidden while a scene is loading |
| `DevTest/DevTestRange.cs` | Rebinds both buttons for the range, after the run tracker |

### Test steps (6.7c)
1. Launch from the mothership, collect nothing, press **Esc** and press **Quit**. The loading screen reads RETURNING TO MOTHERSHIP. The hub's last run reads RUN LOST with no fee, and the record counts one more loss.
2. Launch again, press **Esc** and press **Restart**. The loading screen reads RESTARTING RUN and a new run starts in Polaris, aboard the ship. The record counts another loss.
3. In the Dev Test Range, **Esc** then **Quit** still stops play.

---

## Phase 6.8: Sector generation

### Goal
Polaris, and every later raid location, is generated from a seed: large, dense, and cheap to run. All generation code is Dead Cargo's own.

### Design
- **Sector definition.** Each raid location has a data asset: seed, size, cell size and regions. Polaris is 40 x 6 x 40 km with 1 km cells.
- **Deterministic.** Content is computed from the seed and a position. The same seed always gives the same sector, so nothing generated is saved.
- **Cells.** Space is a grid of cells. Only cells near the ship exist; they load and unload as it flies.
- **Regions.** Ellipsoids with a kind (asteroid field, debris field, clearing), a density and a soft edge. Where fields overlap the strongest decides the content. A clearing thins every field it overlaps.
- **Two tiers of object.** Far objects are drawn in GPU-instanced batches with no GameObjects. Near objects become pooled GameObjects with colliders.
- **Sector space.** Generation works in sector space, converted to and from world space in one place, so a floating origin can be added without touching generators.
- **Layout.** Polaris keeps one layout so it can be learned. Loot and points of interest vary per run.
- **Target.** 100,000 visible rocks at 60 fps, under 1 ms a frame spent on streaming, no per-frame allocations.

### Steps
| Step | Delivers | Status |
|---|---|---|
| 1. Foundation | Sector definition, seeded random, cell grid, debug view | Done, tested 2026-11-16 |
| 2. Asteroid field | Rocks from boulders to kilometre-wide giants, generated per cell in Burst jobs from the seed and regions, drawn GPU-instanced in the comic cel style. Adds Unity's Burst and Mathematics packages | Built, compile-checked and self-tested, awaiting test |
| 3. Far world | A second camera draws a 1:1000 copy of space behind the real one. Planets, giant asteroids, stations and capital ships are seen from hundreds of kilometres away and grow smoothly as you approach, then hand over to the real object up close. Distant parts of the belt show too | Far camera and far world built with phase 6.9 step 5b, for planets. Giants, stations and the hand-over planned |
| 4. Streaming | Cells load and unload around the ship, spread across frames | Planned |
| 5. Physical asteroids | Near rocks become pooled objects with colliders and damage. Covers phase 6.5 pooling | Planned |
| 6. Large-scale precision | Floating origin | Planned |
| 7. Content placement | Pickups, dark matter, extraction points, points of interest and planets placed by rules. 7a: a new sector every load (run seed, varied layout, extraction points and pickups placed by rules) | 7a built, compile-checked and self-tested, awaiting test |
| 8. Optimisation pass | Distance LODs, far impostors, profiling against the target | Planned |

**Why a far world.** The camera draws only 10 km out, so without it a large object appears suddenly at full size instead of growing out of the distance, and planets cannot be shown at all. Perspective does not depend on scale: a 50 km planet 200 km away looks exactly like a 50 m model 200 m away. So distant giants live in a scaled-down copy of space drawn first by a far camera, and the main camera draws the real, near world on top. The far camera can see a thousand times further than the main one, and nothing ever pops in.

### Implementation (step 1)
| File | Role |
|---|---|
| `Scripts/Sectors/SectorDefinition.cs` | Data asset: seed, size, cell size, regions, density at a position |
| `Scripts/Sectors/SectorRegion.cs`, `SectorRegionKind.cs` | An ellipsoid region and what it holds |
| `Scripts/Sectors/SectorHash.cs` | Stable integer hashing; the seed for each cell |
| `Scripts/Sectors/SectorRandom.cs` | Seeded random number generator, one per cell or job |
| `Scripts/Sectors/CellCoord.cs`, `SectorGrid.cs` | Cell addresses and the grid maths |
| `Scripts/Sectors/SectorSpace.cs` | Scene service: definition, seed, grid, world and sector space conversion |
| `Scripts/Sectors/SectorDebugView.cs` | Draws bounds, regions, cells and sample points with Draw Debug Tools |
| `Editor/SectorSpaceSetup.cs` | Adds the Sector Space object and creates the scene's sector definition |
| `Editor/GodMenu/Tabs/SectorTab.cs` | God Menu tab: sector readout, seed, debug view toggles |
| `Assets/DrawDebugTools/` | Third-party debug drawing (namespace `DDT`). Its calls are compiled out of release builds |

The hashing and random code is plain value maths with no shared state, so it moves into Burst jobs in step 2 unchanged.

Sample points are a preview of generation: each cell tests a fixed number of candidate points and keeps those whose random roll falls under the density there. Real content uses the same rule.

### Test steps (step 1)
1. Click into Unity and let it import Draw Debug Tools and compile. The Console should be free of errors.
2. Open Polaris and run **Dead Cargo > Setup > Phase 6.8 - Sector Space (Open Scene)**. A `Sector Space` object appears under `Systems`, and `Assets/DeadCargo/Data/Sectors/Polaris.asset` is created with three starter regions. Save the scene.
3. Select `Sector Space` outside Play Mode. The Scene view shows the sector bounds and the three regions as gizmos.
4. Press Play. In the God Menu, the **Sector** tab shows the size, the cell count, the player's cell, and the region and density at the ship.
5. Press **Show All** in the Debug View section. White and grey boxes mark the cells around the ship, and coloured points appear where content would be generated. Around the arrival point there are few or none, because it is a clearing.
6. Fly towards the Main Belt (ahead and to the right, a few kilometres out). Amber points appear and thicken as the density bar rises. The cell boxes follow the ship.
7. Stop and start Play again. The points are in the same places. Press **Random Seed**: they all move. Press **Reset Seed**: they return.
8. The camera sees 10 km, so in the Game view distant bounds and regions are cut off. Look at the Scene view during Play to see the whole sector.

### Tuning
On `Data/Sectors/Polaris.asset`: seed, size, cell size, and each region's centre, radii, density and falloff. Restart Play after changing the size or cell size.

### Hardening (step 1)
Added after step 1 passed its first test, to make the foundation as solid as it can be before content is built on it.

| File | Role |
|---|---|
| `Editor/Diagnostics/SectorSelfTest.cs` | 23 checks of the maths: pinned hash values, hash avalanche, even spread, ranges, unique and unrelated cell seeds, grid round trips, region falloff. Also times each operation |
| `Editor/Diagnostics/SectorDiagnostics.cs` | Runs the self-test, checks the open sector's definition, and estimates how many objects it will generate |
| `Editor/SectorSpaceEditor.cs` | Edits the definition under the Sector Space inspector, with handles to move and resize regions in the Scene view |

- **Pinned values.** The self-test holds four reference outputs of the hashing. If a change breaks them, every seed would build a different sector, so that check must never be "fixed" by updating the numbers once sectors are in use.
- **Verified outside Unity.** The same checks were compiled and run on their own: all 23 pass.

### Test steps (hardening)
1. Run **Dead Cargo > Diagnostics > Sector Self-Test** with Polaris open. The Console shows one line per check, all "pass", then an estimate of the objects Polaris will generate, in total and per region.
2. Select `Sector Space` outside Play Mode. The definition appears under the component. In the Scene view, drag a region's move handle, and drag the dots on its surface to resize it. **Ctrl+Z** undoes each edit.
3. Drag a region partly outside the sector bounds and run the self-test again. It fails with "extends past the bounds". Undo.
4. In Play Mode, the **Sector** tab's Self-Test section runs the same test and shows the result.

### Design (step 2: asteroid field)
- **Rock only where the regions say.** Asteroid-field regions hold rock; debris fields wait for wreckage of their own, and clearings thin both. In Polaris that is the Main Belt, with open space round the arrival point and the Wreck Field left empty for now.
- **Generated per cell, from the seed.** Each 1 km cell tests a fixed number of candidate points and keeps each by the density there, drawing the same random values for every candidate, kept or not. A cell is the same every time it loads, and nothing is saved. Cells no field reaches are skipped without a single test.
- **Sizes like a real belt.** From 4 m boulders to 1.2 km giants on a power law: the count of rocks over a size falls as the square of that size. At the default 1,000 rocks per cubic kilometre, rocks sit about 100 m apart, mostly small; a full cell holds about 1.6 rocks over 100 m, and a giant over 500 m turns up about once in 19 cubic kilometres.
- **Giants first.** Rocks of 100 m and up are placed from their own random stream, and smaller rocks keep out of them and of their neighbours' giants, to 1.3 times their radius (stretch included). No boulder sits inside a giant, and a cell can work this out without its neighbours being loaded.
- **Shapes of its own.** Eight lumpy shapes, built at load from the sector seed: three layers of noise (broad lobes, bumps, grit) set a radius for every direction, with exact normals from the noise's own slope. Each rock turns, stretches up to 30% along each axis and tumbles about its own axis from its seed: a 10 m rock at 4° a second, smaller ones faster, giants barely at all. Rocks never drift, so the layout stays learnable.
- **Six levels of detail**, from 20 triangles to 20,480, all sampling the same surface. A rock's level comes from its size on screen, so triangle edges stay near 12 pixels. Rocks under 1.5 pixels across are not drawn.
- **Nothing pops (after the first test).** The first test showed rocks bright white and popping. The rocks' projected texture turned normals into world space in the pixel shader, which never learned which instance it was drawing, so every rock in a batch was lit as the first one. That flipped as batches changed, and lit some rocks flat white. Now:
  - **Lit right.** The instance is carried to the pixel shader, so every rock is lit as itself.
  - **Detail morphs.** Each vertex a level of detail adds starts where the level before draws it, halfway along the edge it splits, and slides to its own place as the rock grows on screen. So a change of detail never moves the outline. The culling job and the shader measure a rock's size on screen the same way, from its mean scale and distance, so the morph starts from nothing at the switch.
  - **Grow in.** The smallest rocks grow in from nothing between 1.5 and 4.5 pixels across, rather than appearing.
- **The comic rock look.** `Materials/Comic/ComicFieldRock.mat` is the voxel rock's material (projected texture, three tones, outline) with instancing on and a small shade per rock. Field rocks and carved voxel asteroids match, ready for near rocks to become voxel asteroids (6.10 step 3).
- **Solid near the player.** Added with step 5a, below.

### Performance (step 2)
- **Streaming.** When the camera enters a new cell, cells within draw distance (plus the reach of the largest rock) that a field reaches are generated in one Burst job, nearest first, up to 128 cells a batch, into fixed slots; cells left behind free theirs. The main thread never waits: a batch still running is picked up next frame, and streaming waits for it.
- **Drawing.** Each frame three Burst jobs, started in update and finished in late update, decide which rocks are drawn and write their matrices batch by batch: one culls by view, distance and size on screen and picks each rock's batch (shape, detail, shadow), one lays the batches out, one writes the matrices, every cell slot in parallel. Late update draws each batch with GPU instancing, 1,023 rocks a call.
- **Small data.** A rock is 20 bytes (position, radius, seed); its shape, turn, stretch and tumble come from its seed when drawn. Polaris holds 726 cell slots of 1,004 rocks, about 15 MB, plus 6 MB of matrices for up to 100,000 rocks a frame.
- **Shadows** only from rocks within the shadow distance; every rock receives them.
- **Readouts.** The God Menu **Sector** tab and the debug overlay's Sector panel show cells, rocks loaded and drawn, draw calls and main-thread time. Profiler markers: `DeadCargo.Field.Stream` and `DeadCargo.Field.Draw`.
- **Room to grow.** If draw calls ever cost too much, step 8 can draw each batch with one indirect call from a GPU buffer; the jobs already lay the data out that way.

### Implementation (step 2)
| File | Role |
|---|---|
| `Scripts/Sectors/RegionVolume.cs`, `IRegionSet.cs`, `SectorRegionSet.cs`, `SectorDensity.cs` | Regions as plain values, and the one density implementation behind the definition, the debug view and every job |
| `Scripts/Sectors/Field/AsteroidFieldDefinition.cs`, `FieldSettings.cs` | Data asset: rock density, sizes, size spread, stretch, keep-out, shapes and tumble; the per-cell counts worked out for jobs |
| `Scripts/Sectors/Field/FieldGeneration.cs`, `FieldRock.cs`, `IRockBuffer.cs` | The rules: a cell's large rocks, then its small rocks keeping out of them |
| `Scripts/Sectors/Field/FieldRockShape.cs`, `Icosphere.cs`, `RockShapeJob.cs`, `FieldRockMeshes.cs` | Rock shapes and their six levels of detail; each rock's turn, stretch and tumble |
| `Scripts/Sectors/Field/FieldCellJob.cs`, `NativeRegionSet.cs`, `SlotRockBuffer.cs` | Generating a batch of cells |
| `Scripts/Sectors/Field/FieldCullJob.cs`, `FieldOffsetJob.cs`, `FieldScatterJob.cs` | Each frame: what is drawn, in which batch, and where |
| `Scripts/Sectors/Field/AsteroidField.cs` | On the Sector Space object: streaming, the jobs, drawing, readouts |
| `Editor/AsteroidFieldSetup.cs` | Setup step **Asteroid Field**: the component, `Data/Sectors/PolarisField.asset`, the material, and the distance fade |
| `Editor/GodMenu/Tabs/SectorTab.cs`, `Scripts/Debugging/DebugReadouts.cs` | The field's readouts and controls |
| `Editor/Diagnostics/SectorSelfTest.cs` | Ten new checks: 33 in all |
| `Shaders/ComicSurface.shader` | **Detail Morph** (`_GEOMORPH`): vertices slide from the coarser level's place as a field rock grows on screen, in the surface, ink and shadow passes. The surface pass carries the instance to the pixel shader |

### Verified outside Unity (step 2)
- Every Dead Cargo assembly compiles with Unity's own compiler and settings, with no new warnings.
- The sector self-test, now 31 checks, passes run on its own:
  - the same seed builds the same rocks, and a new seed moves them
  - density sets the count: 1,000 of 1,000 rocks at full density and 488 of 500 at half
  - no rock in a clearing, a debris field or outside the grid
  - no small rock inside a large one: 978 small rocks checked against 47 large ones
  - sizes follow the spread: 24.6% of small rocks over twice the smallest, against 24.9% expected
  - rock shapes stay in bounds, with normals facing out
  - each rock is placed at its centre and tumbles on the spot
  - all six mesh levels are closed and wound to face out, which the ink outline needs
  - after the first test, two more (33 in all): every vertex a level adds splits an edge of the level before, and the culling job's size for detail matches the mean scale the shader reads from the matrix to within 0.0001% of the radius. The comic surface's 64 programs, the detail morph among them, compile with the DirectX compiler

### Test steps (step 2)
1. Click into Unity and let it compile. The Console should be free of errors.
2. Open Polaris. The God Menu **Project** tab shows one step missing; press **Set Up Sector**. The `Sector Space` object gains an **Asteroid Field** component, `Data/Sectors/PolarisField.asset` and `Materials/Comic/ComicFieldRock.mat` appear, and a `Space Depth` object appears under `Environment`. Save the scene.
3. Run **Dead Cargo > Diagnostics > Sector Self-Test**: 35 checks pass.
4. Press Play. Around the arrival point space is clear. Fly towards the Main Belt (ahead and to the right, a few kilometres out). Rocks come out of the void, dark and dissolving in, and brighten as you close in; nothing pops. They thicken as you go in: mostly small, a few large ones, now and then a giant.
5. Inside the belt, look around. Rocks of eight shapes, each turned and stretched its own way, in the comic rock look with outlines, each with its own lit side and shadow side; none is flat white. Small rocks tumble slowly; giants barely move. Fly close to a giant: no boulder is buried inside it, and its outline grows smoother as you approach with no jumps.
6. Open the God Menu **Sector** tab, **Asteroid Field** section: cells loaded, rocks loaded and drawn, draw calls and main-thread time. Fly on: cells load ahead and free behind, and **generating** shows briefly. Untick **Draw Field** to compare.
7. Stop and press Play again, and fly to the same place: the same rocks are in the same places. Press **Random Seed** in the **Seed** section: the field is rebuilt, with new shapes and new rocks. **Reset Seed** brings the first field back.
8. Rocks near you are solid from step 5a, below.

### Tuning (step 2)
- Rocks, on `Data/Sectors/PolarisField.asset`: rocks per cubic kilometre, smallest, largest and "large" size, size spread, stretch, keep-out, number of shapes and tumble. A change rebuilds every rock, so treat it like the seed once a layout has been learned. Press **Rebuild Field** in the Sector tab to see a change in Play Mode.
- Drawing, on the **Asteroid Field** component: draw distance (0 uses the camera's far clip), detail bias, smallest size drawn, most rocks a frame and shadows.
- Look, on `Materials/Comic/ComicFieldRock.mat`: every comic rock setting, including **Instance Variation**, the shade each rock takes.

### Design (detail pass, after the 10 km test)
The test at a 10 km draw distance showed detail arriving too late, worst at speed. Three changes, which supersede the figures above:
- **Detail looks ahead.** A rock's size on screen is judged from the nearest point of the camera's path over the next 1.5 seconds (**Detail Look Ahead**), not only from where the camera is. At speed, the rocks ahead reach full detail, and the smallest finish growing in, before the camera gets to them. The camera's velocity is eased over a quarter of a second, and a cut to a new place counts as no motion. The shader's detail morph reads the same path (`_FieldDetailAhead`), so both still agree on when a level starts.
- **Detail never jumps.** A level now morphs from the one before across its whole span on screen, not the first half, so a rock's outline is always easing as it grows.
- **Finer sooner.** Levels start at 12 pixels across instead of 19, so triangle edges stay near 8 pixels. The coarsest shape, 20 triangles, is now only used under 12 pixels.

### Performance (detail pass)
- **Cells are kept largest first**, sorted once when generated. Culling skips a cell wholly out of view or range with one test, and inside a cell stops at the first rock too small to draw from the cell's nearest point. A far cell costs a few giants, not a thousand tests, which is what a 10 km draw distance needs: 3,750 cell slots, 75 MB of rock.
- The matrix job only visits the rocks culling reached.

### Design (step 5a: solid rocks)
- **Solid near the player.** A rock is solid while its surface is within 200 m of the player's vehicle, plus 20 m for every metre of its radius: a 4 m boulder from 240 m, a 20 m rock from 400 m, a 100 m rock from 1.2 km, and a giant from anywhere in sight. Nearest first, at most 256 at once.
- **Its own shape.** Each solid rock borrows a pooled stand-in: a kinematic body with a mesh collider of the rock's own shape, stretched and turned as it is drawn, and turning as it tumbles. The collider's detail follows the rock's size, so triangles stay about 3 m long until the finest level: a boulder is 80 triangles, a rock of 50 m radius or more uses the finest, 20,480, the same surface the eye sees up close.
- **Weapons stop on them.** Stand-ins carry the destruction damage receiver. Hits are raised as `AsteroidField.RockDamaged` with the rock they hit; nothing carves yet. That event is where a field rock becomes a voxel asteroid (6.10 step 3).
- **Rock on hull** has no friction and little bounce, as trail rocks do. The kit still deals collision damage by impact speed.
- **Only the player, for now.** Other ships pass through rock that is not near the player. They need to steer round rock before they should hit it.

### Performance (step 5a)
- **Cooked once.** Every shape's five collider levels are cooked on worker threads when the field is built (40 meshes). A rock turning solid cooks nothing: it is handed a shape already cooked.
- **Picked by a job** ten times a second, alongside the frame's drawing jobs. Cells being largest first, a far cell costs one test. At most 32 rocks turn solid in one pass, nearest first, so a burst never costs a frame.
- **No flicker.** A solid rock stays solid 40 m past its range.
- **Pooled.** 256 stand-ins are made at load and reused; nothing is created or destroyed in flight.

### Implementation (detail pass and step 5a)
| File | Role |
|---|---|
| `Scripts/Sectors/Field/FieldSort.cs` | An in-place heap sort for jobs, and the largest-first order |
| `Scripts/Sectors/Field/FieldCellJob.cs` | Leaves each cell largest first |
| `Scripts/Sectors/Field/FieldCullJob.cs`, `FieldScatterJob.cs` | Whole-cell tests, the early exit, and detail judged from the camera's path |
| `Scripts/Sectors/Field/FieldRockShape.cs` | `DetailDistance`, and `Motion` and `Rotation`, shared by drawing and colliders |
| `Scripts/Sectors/Field/FieldSolidJob.cs` | Picks the rocks near enough to be solid, nearest first |
| `Scripts/Sectors/Field/FieldRockColliders.cs`, `FieldRockProxy.cs` | The pool of stand-ins, cooking, and applying each pass |
| `Scripts/Sectors/Field/AsteroidField.cs` | Camera velocity, the solid pass, `RockDamaged`, and the new settings |
| `Shaders/ComicSurface.shader` | The detail morph reads the camera's path and the morph's span |
| `Editor/GodMenu/Tabs/SectorTab.cs`, `Scripts/Debugging/DebugReadouts.cs` | **Solid Rocks** switch and count |
| `Editor/Diagnostics/SectorSelfTest.cs` | Two new checks: 35 in all |

### Verified outside Unity (detail pass and step 5a)
Every Dead Cargo assembly compiles with Unity's own compiler, with no new warnings, and the comic surface's 80 programs compile with the DirectX compiler. Nothing here has been run in Unity: Burst compilation of the new jobs, the look in motion and the feel of a collision are for the test.

### Test steps (detail pass)
1. Click into Unity and let it compile. The Console should be free of errors.
2. Run **Dead Cargo > Diagnostics > Sector Self-Test**: 35 checks pass.
3. Press Play in Polaris and fly into the Main Belt at cruising speed. Rocks ahead should already be in full shape when you reach them, and no outline should jump.
4. Boost through the belt. Detail should hold up as well as at cruising speed.
5. On the **Asteroid Field** component, set **Detail Look Ahead** to 0 and fly the same line to compare, then back to 1.5. Raise **Detail Bias** if rocks still look coarse; lower **Min Pixels** if small rocks still appear too near.
6. In the God Menu **Sector** tab, note **Main Thread** and the frame rate against the last test.

### Test steps (step 5a)
1. In the God Menu **Sector** tab, **Asteroid Field** section, **Solid Now** shows a count once you are in the belt, and changes as you fly.
2. Fly slowly into a boulder, then a large rock. The ship stops on the surface you see, not short of it and not inside it, and takes collision damage by speed.
3. Fly along the surface of a giant, close in. The ship follows its lumps.
4. Shoot a rock near you: shots stop on it. Shoot a small rock 500 m off: shots pass through, as it is not solid from there.
5. Untick **Solid Rocks**: **Solid Now** drops to 0 and you fly through rock. Tick it again.
6. Hit a rock at full boost. If the ship ends up inside it, note it: that is the backlog's "Fast ships and small rocks" entry.

### Tuning (detail pass and step 5a)
On the **Asteroid Field** component: **Detail Look Ahead**, **Solid Rocks**, **Solid Range**, **Solid Range Per Radius** and **Max Solid Rocks**.

### Design (natural placement and field dust)
The user asked for dust in the field, "so it just doesn't feel like rocks floating in the sky", and for fields that look "more realistic and naturally placed".
- **Clumps and gaps.** Rock gathers into clumps about 700 m across with gaps between, from two layers of noise taken from the sector seed. Clumps keep the field's full density; gaps thin towards nothing, so at the default **Clumping** of 0.75 the field holds roughly 40% less rock overall. Raise **Rocks Per Cubic Kilometre** to fill it back. Large rocks follow the clumps too.
- **Rubble round large rocks.** A share of a cell's small rocks (**Rubble**, 0.2) gathers just outside its large rocks, nearer more often than further. Giants of 200 m radius or more gather their full share; a 100 m rock a quarter. Rubble stays with its rock whether that sits in a clump or a gap.
- **Still one cell at a time.** Both are pure functions of position and seed, so a cell is still built without its neighbours and comes back the same. Rubble that would land outside its cell stays where it was.
- **The layout changed.** Every rock moved with this change, as it would with a new seed.
- **Field dust.** Soft banks of dust hang through the fields, thick where the rock is thick, clumps and gaps included, and absent in open space. Banks between the camera and the sun catch a cool grey light; the rest are near black, so the haze reads as dark smoke (the first test's warm tan was too bright for the scene). Rocks behind a bank are veiled, so depth inside the field reads at a glance. Wisps drift slowly. A bank fades out as the camera enters it, so flying through never shows an edge. Soft and un-inked: dust belongs with the sky, not the comic look.
- **Grit in the air.** Inside a field, more of the space dust's motes show (**Amount In Asteroid Fields**, 0.7 against 0.2 in open space), in step with how thick the field is at the camera.

### Performance (natural placement and field dust)
- **Placement** costs two noise samples per candidate rock, in the generation job.
- **Density map.** The fields' density, clumps included, is baked once in a Burst job into a small volume texture (160 × 24 × 160 in Polaris, 0.6 MB) and published to shaders. Any effect can ask how thick the field is anywhere for one lookup.
- **Field dust** is one static mesh and one draw call, with no CPU work per frame. The vertex shader places each bank, reads the density map, and drops banks in open space before any pixel is drawn. The cost is overdraw: large soft quads over much of the screen. **Bank Count** (1,200) and the material's **Amount** and **Opacity** trade fullness against that cost; it has not been measured.

### Implementation (natural placement and field dust)
| File | Role |
|---|---|
| `Scripts/Sectors/Field/FieldGeneration.cs` | `Clump`, and rubble gathered round large rocks |
| `Scripts/Sectors/Field/FieldSettings.cs`, `AsteroidFieldDefinition.cs` | **Clumping**, **Clump Size** and **Rubble** |
| `Scripts/Sectors/Field/FieldDensityMap.cs` | Bakes and publishes the density map; the density at the camera |
| `Scripts/Sectors/Field/AsteroidField.cs` | Builds the map with the field; `DensityHere` |
| `Shaders/FieldDust.shader`, `Scripts/Visuals/FieldDust.cs` | The dust banks |
| `Shaders/SpaceDust.shader` | More motes inside a field |
| `Editor/FieldDustSetup.cs`, `Editor/AsteroidFieldSetup.cs` | Setup adds a `Field Dust` object and `Materials/FieldDust.mat` |
| `Editor/GodMenu/Tabs/SectorTab.cs` | **Field here** bar: the density at the camera |

### Verified outside Unity (natural placement and field dust)
Every Dead Cargo assembly compiles with Unity's own compiler, and the field dust and space dust shaders compile with the DirectX compiler. The look has not been seen: colours, opacity and bank size are first guesses to tune.

### Test steps (natural placement and field dust)
1. Click into Unity and let it compile. With Polaris open, the God Menu **Project** tab shows one step missing; press **Set Up Sector**. A `Field Dust` object appears under `Environment` and `Materials/FieldDust.mat` is created. Save the scene.
2. Press Play and fly to the Main Belt. From outside, the belt should show as uneven clumps with gaps, in a faint haze, not an even scatter.
3. Fly in. Rocks should thin out and thicken as you cross gaps and clumps, and large rocks should have small rocks gathered round them. In the God Menu **Sector** tab, **Field here** rises in clumps and falls in gaps.
4. Look towards the sun, then away. Dust towards the sun glows; away from it, it is dim. Distant rocks are veiled by it.
5. Fly through a bank: it thins round you with no edge and no pop. At speed, more motes streak past inside the field than in open space.
6. Disable the `Field Dust` object to compare, and note the frame rate with and without it.

### Tuning (natural placement and field dust)
- Placement, on `Data/Sectors/PolarisField.asset`: **Clumping**, **Clump Size**, **Rubble**. Press **Rebuild Field** in the Sector tab to see a change in Play Mode.
- Dust, on `Materials/FieldDust.mat`: lit and shadow colours, **Opacity**, **Amount**, **Sun Scatter**, **Bank Size**, **Field Size** (how far out banks are drawn), the density where dust starts and is full, wisps and drift. **Bank Count** is on the `Field Dust` object.
- Motes, on `Materials/SpaceDust.mat`: **Amount In Asteroid Fields**.

### Design (step 7a: a new sector every load)
The user asked for Polaris to be different every time it loads, extraction included.
- **A run seed.** Each load of Polaris, from the mothership or by Restart, draws a new seed. Rocks, their shapes, clumps and gaps, the layout, placed content and the sky all come from it, so one number is the whole run. **Pin Seed** in the God Menu keeps a run you liked for every load until **Unpin**: it writes the seed to the sector definition and switches off **New Seed Each Load**.
- **The layout moves.** Each region carries limits on how far it may change from run to run: **Orbit** swings it round the arrival point to any side while keeping its distance, turning with the swing so it shows the same face to the arrival point; **Drift** moves it a little; **Turn** adds its own turn; **Resize** grows or shrinks each radius. In Polaris the Main Belt and the Wreck Field swing all the way round, so the belt can lie ahead, behind or to either side, while the Arrival Point never moves. A region that would reach out of the sector is moved back inside.
- **Regions can turn.** A region is now an ellipsoid turned about the vertical axis (**Yaw**), so a belt can lie at any angle. The handles in the Scene view and the debug view follow the turn.
- **Content is placed by rules.** Every extraction point and pickup in the scene carries a **Sector Placement** with a shared rule: how far from the arrival point, how high, how deep into a field, how far from other content, and whether to face the arrival point. Extraction points go 4 to 9 km out, in open space or at a field's thin edge, 3 km apart, facing the way the player comes, with rock cleared 400 m round the ring. Pickups go 1.2 to 8 km out, anywhere, fields included, 400 m apart, each in a 60 m pocket. Each piece tries up to 96 spots from its own stream and keeps the first that meets its rule, or the closest miss, so content is never lost.
- **Never buried.** The cleared radius is also a shelter: the field drops any rock whose surface, at its most stretched and lumpiest, could reach into it, giants centred far outside included. So no pickup or gate ever ends up inside rock. Rocks that are not near placed content are unchanged.
- **Before anything wakes.** The sector space runs first in the scene, so content is already in place when the extraction HUD, the pickups' markers and the asteroid field start. Jettisoned cargo and anything else spawned in play is never moved.
- **The player still starts at the arrival point**, the sector's origin, in open space. Authority ships already arrive from a random direction each time.

### Performance (step 7a)
- Building the layout and placing a dozen objects takes well under a millisecond, once per load, behind the loading screen.
- A region's strength now skips anything outside its bounding box before any other maths, which also pays for the turn on every rock candidate the field tests.
- Each piece of placed content adds one clearing the field jobs test, 13 in Polaris. A region outside a candidate's reach costs three comparisons, and a shelter test one distance per kept rock.

### Implementation (step 7a)
| File | Role |
|---|---|
| `Scripts/Sectors/RegionVolume.cs`, `SectorDensity.cs` | Regions turn about the vertical axis; bounding box worked out once; early out |
| `Scripts/Sectors/SectorRegion.cs`, `RegionVariation.cs` | **Yaw**, and the per-run limits: **Orbit**, **Drift**, **Turn**, **Resize** |
| `Scripts/Sectors/SectorLayout.cs`, `LayoutRegionSet.cs` | The run's regions: varied by the seed, kept in the sector, plus clearings round placed content |
| `Scripts/Sectors/SectorDefinition.cs` | **New Seed Each Load** and **Vary Layout** |
| `Scripts/Sectors/SectorSpace.cs` | Draws the run seed, builds the layout and places content first in the scene; again on a seed change |
| `Scripts/Sectors/Placement/` | `SectorPlacementRule` (the rule asset), `SectorPlacement` (on placed content), `SectorPlacer` (picks the spot), `PlacementSettings` |
| `Scripts/Sectors/Field/AsteroidField.cs`, `SectorDebugView.cs`, `Scripts/Debugging/DebugReadouts.cs` | Read the run's layout instead of the definition |
| `Scripts/Sectors/Field/FieldGeneration.cs` | Shelters: no rock reaches into placed content |
| `Scripts/Visuals/SpaceSkyRandomizer.cs` | The sky follows the sector seed, and bakes again when it changes |
| `Editor/SectorPlacementSetup.cs`, `Editor/SectorSetup.cs` | Setup step **Content Placement**: the rules in `Data/Sectors/Placement/`, and a Sector Placement on every extraction point and pickup |
| `Editor/GodMenu/Tabs/SectorTab.cs` | **Seed** section: next load, **Pin Seed** and **Unpin**. **Layout** section: where each region and kind of content lies this run |
| `Editor/SectorSpaceEditor.cs` | Scene view handles follow a region's turn |
| `Editor/Diagnostics/SectorSelfTest.cs`, `SectorDiagnostics.cs` | Eleven new checks, and two more for the open sector |
| `Data/Sectors/Polaris.asset` | The Main Belt and Wreck Field given their limits |

### Verified outside Unity (step 7a)
- Every Dead Cargo assembly compiles with Unity's own compiler and settings, with no new warnings.
- The new self-test checks pass, run outside Unity against the compiled code:
  - a region turned a quarter matches one with its axes swapped, its bounding box holds it at every turn, and its turn matches Unity's yaw
  - the same seed builds the same layout; a new seed moves and turns the fields; a fixed region and Vary Layout off keep regions as authored
  - over 500 seeds no varied region reaches outside the sector, the arrival point is always clear, and the belt's centre lies 8.2 to 10.8 km out at every bearing
  - a clearing added for placed content empties the field round it
  - over 300 runs of one extraction point and twelve pickups, all 3,900 spots meet their rule's distance, height, density and spacing, every gate faces the arrival point, and none fell back to a miss
  - of 25,469 rocks round a 150 m shelter in a full field, none reaches into it; 10 would without the shelter
- The field's own checks give the same rocks as before the change, so turning regions did not disturb generation.
- Not run in Unity: the load order, the placement in the scene, the sky following the seed, and the God Menu.

### Test steps (step 7a)
1. Click into Unity and let it compile. With Polaris open, the God Menu **Project** tab shows one step missing, **Content Placement**; press **Set Up Sector**. `Data/Sectors/Placement/` gains **Extraction Points** and **Pickups**, and the extraction point and every pickup gain a **Sector Placement**. Save the scene.
2. Run **Dead Cargo > Diagnostics > Sector Self-Test**: every check passes, among them the new Turned region, Layout and Placement checks, and "arrival point stays clear" and "content finds room" for Polaris.
3. From the Mothership, launch into Polaris. Open the God Menu **Sector** tab: **Seed** says "new this load"; **Layout** lists where the Main Belt, the Wreck Field, the extraction point and the pickups lie, by distance and bearing. Turn on **Regions** in **Debug View** to see the belt's outline.
4. Fly to the extraction point by its HUD marker. It stands 4 to 9 km out, facing you, with no rock on the ring. Fly to a pickup that lies inside the belt: it sits in a small pocket, never inside a rock.
5. Return to the mothership and launch again, or use the pause menu's **Restart**. The belt lies elsewhere, the rocks, the extraction point, the pickups and the sky have all changed, and the **Layout** numbers are new.
6. Press **Pin Seed**, then Restart: the same run comes back, sky included. Press **Unpin**, Restart: a new run again.
7. In Play Mode press **Random Seed**: the field rebuilds and the extraction point and pickups jump to new spots. **Reset Seed** brings this load's run back.

### Tuning (step 7a)
- Per run, on the sector definition (shown under the `Sector Space` object): **New Seed Each Load**, **Vary Layout**, and on each region **Yaw** and **Variation** (**Orbit**, **Drift**, **Turn**, **Resize**).
- Placement, on `Data/Sectors/Placement/Extraction Points.asset` and `Pickups.asset`: order, distance, height, field density, spacing, cleared radius and facing. Rules apply at the next load, or straight away with **Random Seed**.
- To keep one object where it was put, remove its **Sector Placement**.

---

## Phase 6.9: Space visuals

### Goal
Space feels like deep space: an endless, dark void. Near objects (ships, rocks, stations) carry the game's comic cel look (lit and shadowed sides, bold ink lines, hatching), while the sky stays muted, soft and far away, so the two read at different depths.

### Steps
| Step | Delivers | Status |
|---|---|---|
| 1. Sky | Procedural deep-space sky: an endless dark void, a soft muted nebula band cut by dust lanes, a far layer and galaxies for depth, sparse stars, cheap shader-only motion. Comic ink and hatching available but off | Built, awaiting test |
| 2. Sun and light | A distant star in the sky, painted to match the scene; the scene light aimed from it, with matching light and shadow colours. 2a: the star drawn where the scene light comes from, lighting the clouds. 2b: a new sun each run, and the darkness of space. 2c: comic shadows from the sun | 2a and 2b built, awaiting test. 2c planned |
| 3. Depth | 3a: space dust streaking past the ship, for speed and scale. 3b: distance fade in colour steps towards the sky colour | 3a and 3b built, awaiting test |
| 4. Comic look | Dead Cargo's comic surface for near objects: two-tone light, ink outlines and hatching on the player ship and rocks | Built, awaiting test |
| 5. Backdrop bodies | Planets, moons and rings, built on sector generation's far world (6.8 step 3), using Planet Forge (Space Graphics Toolkit). 5a: install Planet Forge and the packages it needs. 5b: a far space camera that draws massive planets behind the near world. 5c: planets chosen and placed from the run seed. 5d: a look pass to match Dead Cargo's art | 5a installed. 5b and 5c built and tested 2026-11-17, quality fixes awaiting test. 5d moved to phase 6.12 step 3, so the look is built to hold from orbit to the ground |
| 6. Sector identity | A colour palette per sector, with a tint per region | Planned |

### Design (step 1)
- **One sky material per sector**, named after the scene (`Materials/Sky/PolarisSky.mat`), using Dead Cargo's own shader `DeadCargo/Space Sky`. It replaces the 6.7a star texture sky. Star and nebula seeds come from the scene name, so each new sector starts with a different sky.
- **Galactic plane.** The whole sky is organised around one plane. The **Rotation** setting turns it; Polaris has it crossing the starting view diagonally.
- **Backdrop.** Flat colour bands step from the void up to the plane colour, with edges bent by the nebula's hue field so they are not straight lines.
- **An endless void.** Most of the sky is near-black. The nebula gathers in a narrow band, like a galaxy's spine seen edge-on.
- **Nebula.** Muted gas in three steps (faint, mid, core) gathered along the plane, in one of two palettes where the hue field crosses a split: cold blue-grey, or dusty warm brown. **Softness** blends the steps from crisp one-pixel cel bands (0) into soft gas (the default).
- **Volume.** Each cloud is lit softly from one direction: a lit side, a shadow side tinted towards blue, and a faint highlight on the edges facing the light. **Dust lanes**, dark filaments from the map turned a third way, cut across the gas along the plane and hide the stars behind them. Layering the gas behind dust is what gives it depth.
- **Depth.** Behind the near nebula sits a far layer (the same map, turned another way, flat and faint), and behind that small distant galaxies. As in a real sky, galaxies are rare along the galactic plane, where dust hides them.
- **Comic option.** Ink outlines (heavier on the shadow side, with lines between steps and palettes) and hatching anchored to the sky are built in, but off by default: the sky stays far and soft, and the comic look belongs to near objects. Ink Width and Hatching turn them on.
- **Motion, at almost no cost.** Everything moves from the time value alone, with no CPU work and a few extra instructions per pixel. Cloud edges creep as a slow wave crosses the hue field, while light and shadow hold steady. The far layer drifts slowly against the near clouds, and that difference in motion reads as depth. Stars twinkle on their own beats, and the large stars' glints pulse with them. Each motion has a setting in the **Motion** section, and 0 holds it still.
- **Being in space.** The strongest sense of moving through endless space comes from things passing the ship. That is step 3's dust streaks.
- **Stars.** Three layers (small, medium, large) of fixed pixel sizes, so they stay crisp at any resolution and field of view. They are sparse (most of the sky is empty), thicken towards the plane, and dim behind the nebula and dust. Most are white, with a few cool and warm ones; the few large ones get small four-pointed glints aligned to the screen. Each star sits inside its own cell, so one cell lookup per layer draws them with no gaps or cut-off stars.
- **Cel edges.** Every hard edge is softened over exactly one pixel, using screen-space derivatives, so steps are clean at any resolution with no blur and no stair-stepping.
- **Cheap at runtime.** The nebula's shape is noise that would be costly per pixel, so it is baked once into a 1024 x 1024 two-channel map (density and hue, 4 MB). The shader thresholds the filtered value, which keeps edges sharp although the map is small. At runtime the sky costs three reads of that one map (near clouds, lighting, far layer) and a little maths per pixel; nothing runs on the CPU.
- **Seamless.** The map uses an octahedral layout with a one-texel border holding the far side of each fold, so there are no seams anywhere in the sky.
- **Live editing.** Colours, steps, stars and rotation update instantly. Changing the nebula's shape re-bakes the map on its own a moment after the slider stops (about 0.2 seconds on 16 threads).
- **New each load.** The `Space Sky` object's randomizer gives every load a new sky: it bakes a fresh nebula from the authored shape with a new seed, places new stars and galaxies, and tilts the galactic band, far layer and dust lanes to new angles. The bake runs while the scene loads, behind the loading screen. The authored material is never changed; the run uses a copy, and the editor always shows the authored sky. Each option can be switched off, and a fixed seed brings back a sky seen before. The sky is cosmetic, so this does not touch Polaris's fixed layout.

### Implementation (step 1)
| File | Role |
|---|---|
| `Shaders/SpaceSky.shader` | The sky shader |
| `Scripts/Visuals/NebulaShape.cs` | The settings that need a bake, and their hash |
| `Scripts/Visuals/NebulaNoise.cs` | Seeded 3D gradient noise |
| `Scripts/Visuals/NebulaField.cs` | Cloud density and hue at a direction: swirled fractal noise gathered into the band |
| `Scripts/Visuals/OctahedralMapping.cs` | Direction to map coordinates and back, including the border folds. Matches the shader |
| `Scripts/Visuals/NebulaBaker.cs` | Bakes the field on every core. No Unity calls, so it can be checked outside the editor |
| `Scripts/Visuals/NebulaMap.cs` | Turns a bake into the shader's texture, for the editor and at load |
| `Scripts/Visuals/SpaceSkyRandomizer.cs` | A new nebula, stars and band angle each load, on a copy of the sky material |
| `Editor/Visuals/SpaceSkyBaker.cs` | Turns a bake into the material's `_Nebula.asset` texture |
| `Editor/Visuals/SpaceSkyShaderGUI.cs` | The material inspector: grouped settings, labelled sliders, automatic re-bake |
| `Scripts/Visuals/SpaceSkyProperties.cs` | Shader property names |
| `Editor/SpaceSkySetup.cs` | Creates and bakes a scene's sky material and makes it the skybox, with undo |
| `Editor/SectorSceneSetup.cs` | New Polaris scenes start with the space sky |
| `Editor/GodMenu/Tabs/ProjectTab.cs` | **Space** section for game sectors: Apply Space Sky, Edit Sky, or Bake Nebula when the map is out of date |

### Verified outside Unity
- The shader's vertex and fragment programs compile with the DirectX 11 compiler against Unity's own shader includes.
- Octahedral mapping round trip, border folds and shader lookups match the baked texels to within a texel.
- Bakes are identical bit for bit for the same shape, and a new seed changes the shape.
- The filtered map is as smooth across the folds as anywhere else, so there are no seams.
- Stars are evenly spread: density varies by under 2% between face centres and edges.
- Preview renders of Polaris from a line-for-line CPU copy of the shader were used to tune the defaults.

### Test steps (step 1)
1. Click into Unity and let it compile. The Console should be free of errors, including shader errors.
2. Open Polaris. In the God Menu **Project** tab, the **Space** section reads "Sky: not the space sky". Press **Apply Space Sky**. `Materials/Sky/PolarisSky.mat` and `PolarisSky_Nebula.asset` appear, the material is selected, and the Scene view shows the new sky. Save the scene. If the section reads "nebula out of date" instead, press **Bake Nebula**; it turns green when the map is current.
3. **Ctrl+Z** puts the old sky back; **Ctrl+Y** restores the space sky.
4. A material made earlier keeps its old values. Select it and press **Reset Look to Defaults** at the top of the inspector: everything but the seeds returns to the current defaults, and the nebula re-bakes. **Ctrl+Z** undoes the reset.
5. Press Play. Look around: mostly near-black void with sparse stars, a soft, muted nebula band crossing the view diagonally, lit on one side, with dark dust lanes across it. Behind it sit a faint far layer and small galaxies. A few large stars glint.
6. With the material selected, change colours, steps, star settings and **Rotation**. The sky updates at once, in the Scene view and in Play Mode. Set **Softness** to 0 and **Ink Width** to 2 for the crisp comic look; back again for soft gas.
7. Turn **Light Direction**. The lit and shadowed sides of every cloud move with it.
8. Watch the sky in Play Mode for half a minute: cloud edges creep slowly, the far layer drifts against the near clouds, and stars shimmer faintly. In the Scene view, motion shows when **Always Refresh** is on in the effects menu. Set every **Motion** setting to 0: the sky holds still.
9. In **Nebula Shape**, drag **Swirl** or press **New** beside the seed. The inspector shows "Re-baking in a moment", then the clouds change shape.
10. Change the Game view resolution. Stars keep the same crisp look.
11. **New each load.** If Polaris already had the space sky, the **Space** section reads "Sky: needs the new-each-load update". Press **Apply Space Sky**: a `Space Sky` object appears under `Environment`, and nothing else changes. Save the scene.
12. Press Play several times, or launch from the mothership several times. Each run has a different nebula, star field and band angle, after the usual short load. Stop Play: the editor shows the authored sky again, and `PolarisSky.mat` is unchanged.
13. On the `Space Sky` object, set **Fixed Seed** to any number other than 0. Every run now shows the same sky. Set it back to 0.

### Tuning
On `Materials/Sky/PolarisSky.mat`: every setting, grouped as Sky, Motion, Backdrop, Depth, Nebula, Comic, Stars and Nebula Shape. Tooltips explain each one. A material keeps the values it was tuned to; **Reset Look to Defaults** picks up the current defaults and keeps the seeds.

### Design (step 2a: the sun)
Step 2 comes in three small parts: **2a** the sun in the sky (this part), **2b** a new sun each run, and **2c** comic shadows from the sun.
- **One sun, one light.** The scene's directional light is the sun. The sky draws the star exactly where that light comes from, the same light the comic surface lights ships and rocks with, so the sky and everything near agree. Turn the light and the star moves with it, in the editor and in play. No script: the shader reads the light Unity hands every sky.
- **Which light.** The Sun Source in the Lighting window, or else the brightest directional light, which is Unity's own rule. **Set Up Sun** makes the scene's directional light its Sun Source. Field dust asked only for the Sun Source and, with none set in Polaris, glowed straight up; it now uses the same rule (`SceneSun`).
- **A distant star, not a disc (after the first test).** The first try, a flat disc with stepped glow rings, read as fake, as did the kit's demo lens flare on the light, which drew a flat streak across it. The user asked for the sun to look like a very distant body, as in their references, with textures that match the scene. Now:
  - **A sphere.** Each pixel of the star knows where on its face it looks, so the surface wraps round a ball, and turns slowly about its own axis (**Sun Spin**).
  - **Boiling gas.** Cells of hot gas churn as a slow field bends them, cut by a few bright veins (**Granules**, **Veins**, **Sun Boil**).
  - **Painted like the nebula.** The surface is painted in a few tones, hot, surface and cool (**Tone Steps**), blended by **Softness** as the nebula's steps are, so the star belongs to the same sky.
  - **Round.** It darkens and reddens towards the limb (**Limb Darkening**), which is what makes a disc read as a sphere.
  - **Corona.** A tight halo hugs the limb; beyond it a soft glow whose reach varies in fine streamers round the rim, and a few tongues of flame (**Flares**).
  - **No lens flare.** **Set Up Sun** takes the kit's flare off the light.
- **Massive and close (after the second test).** The user wanted the sun "a lot closer", to look like a planet: massive, though in the distance. It now spans 28° of sky, roughly half the screen's height when looked at, against 6° before. It is still drawn at infinity, which is right for a body that size: across a whole sector it would not shift by a pixel. At that size the surface needs more detail, so there are more cells (**Granules** 9) and a fourth, fine layer of grain, and the corona is kept tight (5°) so the star reads as a solid body rather than a glow.
- **Matching colour.** The star's colours take on the light's colour by **Light Colour**, so a warmer or cooler sun light warms or cools it.
- **The clouds face the sun.** The nebula's lit side, shadow side and highlights come from the sun (**Lights the Clouds**, 1). At 0 they use the authored **Light Direction**, as before.
- **In front of everything** and outside the sky's **Exposure**, so a darker sky never dims the sun.
- **Cost.** The noise runs only on the pixels near the star, inside a dynamic branch, and the surface only on the pixels the star covers: about a dozen noise lookups there, and one arc cosine everywhere else. Nothing on the CPU.

### Implementation (step 2a)
| File | Role |
|---|---|
| `Shaders/SpaceSky.shader` | `SunLayer` and `SunNear`: the star's surface and corona, from `_WorldSpaceLightPos0` and `_LightColor0`; the clouds lit from the sun |
| `Scripts/Visuals/SpaceSkyProperties.cs`, `Editor/Visuals/SpaceSkyShaderGUI.cs` | The **Sun** section of the sky material (Surface, Corona, Light), and **Sun Boil** and **Sun Spin** in Motion |
| `Scripts/Visuals/SceneSun.cs` | The sun light by Unity's rule, remembered between frames |
| `Scripts/Visuals/FieldDust.cs` | Dust glows towards that same sun |
| `Editor/SunSetup.cs` | **Set Up Sun**: the directional light becomes the Sun Source and loses its lens flare, with undo |
| `Editor/GodMenu/Tabs/ProjectTab.cs` | **Visuals** section: the sun's status, and **Set Up Sun** or **Select Sun** |

### Verified outside Unity (step 2a)
- The sky's vertex and fragment programs compile with the DirectX 11 compiler against Unity's shader includes, with no warnings; a copy with a deliberate error fails, so the check is real.
- Every Dead Cargo assembly compiles with Unity's own compiler, with no new warnings.
- The defaults were tuned from preview renders of a line-for-line CPU copy of the star's maths, at about the size it shows at 1080p, against the user's references. Unity's own render will differ a little: bloom, the GPU's noise hash and the nebula behind it.

### Test steps (step 2a)
1. Click into Unity and let it compile. The Console should be free of errors, including shader errors.
2. Open Polaris. In the God Menu **Project** tab, **Visuals** reads "Sun: Directional Light needs setting up". Press **Set Up Sun**: it turns green, and the button becomes **Select Sun**. Save the scene. **Ctrl+Z** puts the flare back.
3. Press **Select Sun** and look towards the sun in the Scene view (it lies opposite the way the light's arrow points). A massive orange star fills a large part of the view: a mottled, slowly churning surface with bright patches, darker and redder at its edge, with a tight halo and soft streamers. No flat streak across it. If it still shows small, the material kept the earlier size: set **Size** to 28 on `Materials/Sky/PolarisSky.mat`.
4. Rotate the `Directional Light`. The star follows, and the nebula's lit side turns to face it.
5. Press Play and find the sun. Your ship and the rocks are lit from the side facing it. In the Main Belt, look towards the sun, then away: the field dust glows on the sun's side.
6. Watch the star for half a minute: the gas churns and the star turns slowly. **Sun Boil** and **Sun Spin** 0 hold it still.
7. Select `Materials/Sky/PolarisSky.mat` and open **Sun**. Try **Size** (larger reads closer), the **Hot**, **Surface** and **Cool** colours (deep red and orange give a red giant), **Granules**, **Veins**, **Softness** 0 for crisp comic tones, **Corona Strength** and **Flares**.
8. Change the `Directional Light`'s colour: the star warms or cools with it (**Light Colour** sets how much). Put the colour back.

### Tuning (step 2a)
On `Materials/Sky/PolarisSky.mat`, **Sun** section: size; the surface's colours, brightness, tone steps, softness, granules, veins and limb darkening; the corona's colour, strength, size and flares; light colour, and whether the sun lights the clouds. **Sun Boil** and **Sun Spin** under Motion. Where the sun sits is the `Directional Light`'s rotation.

### Design (step 2b: a new sun each run, and the darkness of space)
After testing 2a the user kept the massive sun, and asked for its place to change every run, away from where the player starts, and for the scene to be darker: "a little bright for space ... doesn't feel dark even though we are close to the sun".
- **A new sun each run.** **Space Lighting**, on the sun light, turns the light to a new direction each load, drawn from the sector's seed, so a pinned seed brings the same sun back and **Random Seed** in the God Menu moves it too. The sky draws the star where the light comes from, so the star, the light on every ship and rock, the clouds and the field dust all move together.
- **Not on top of the player, nor behind them.** The sun's centre stays 70° to 125° from where the player first looks (**Clear Of Start View**, **Furthest From Start View**). With the star 28° across, the whole of it sits outside the opening view, to one side: it is found by turning. It never sits straight behind, because a sun behind the camera lights only the side of everything the player looks at, which the first test of this step showed reads flat and bright. From the side, every ship and rock shows a lit half and a dark half.
- **Round the plane.** It sits within 50° of the sector's plane (**Max Elevation**), so it lights the belt from the side, which suits the comic tones best.
- **Why it was bright.** Comic surfaces paint the side away from the light in fixed tones from their materials, so shadowed sides never went dark; a pale blue rim lit every silhouette; the ambient light lifted everything not in the comic look; and the sky kept its full exposure beside a massive sun.
- **The darkness of space.** Space has no air to scatter light: what the sun strikes is bright, the rest falls towards black. **Space Lighting** darkens, together and in Play Mode only: the shadowed sides of ships and rocks (**Shadow Darkening**, 0.8), which also pulls their mid tone towards the shadow so light falls off fast past the edge facing the sun; their rims (**Rim Dimming**, 0.8); the sky but never the sun (**Sky Darkening**, 0.6); and the ambient light (**Ambient Darkening**, 0.85). The sunlit sides keep their full brightness, so contrast rises rather than everything dimming. Without Space Lighting, as in the mothership, nothing changes. The first defaults (0.5, 0.5, 0.35, 0.6) still felt "way too light", so they were raised.
- **Set Up Sun** now also adds Space Lighting to the sun light.

### Implementation (step 2b)
| File | Role |
|---|---|
| `Scripts/Visuals/SpaceLighting.cs` | The sun's direction each run (`PickDirection`), and the darkness: the `_SpaceShade` global and the ambient light |
| `Shaders/ComicSurface.shader` | Shadow and mid tones darkened by `_SpaceShade.x`, rims dimmed by `_SpaceShade.z` |
| `Shaders/SpaceSky.shader` | The sky, not the sun, darkened by `_SpaceShade.y` |
| `Editor/SunSetup.cs` | **Set Up Sun** adds Space Lighting |
| `Editor/Diagnostics/SectorSelfTest.cs` | Three sun checks |

### Verified outside Unity (step 2b)
- Every Dead Cargo assembly compiles with Unity's own compiler, and both shaders compile with the DirectX 11 compiler in every variant the scene uses (the comic surface with and without projected texture, instancing, detail morph and cross-fade; its ink and shadow passes).
- The sun checks pass outside Unity: over 2,000 runs the sun always sits 70° to 125° from the opening view and within 50° of the plane, about evenly to the left and the right; the same seed gives the same sun.
- Not seen yet: how dark the defaults feel. They are first guesses to tune live in Play Mode.

### Test steps (step 2b)
1. Click into Unity and let it compile. In Polaris, the God Menu **Project** tab's **Visuals** reads "Sun: Directional Light needs setting up". Press **Set Up Sun**, then save the scene. The `Directional Light` now carries **Space Lighting**. If it already had Space Lighting from an earlier try, use **Reset** in the component's menu to pick up the darker defaults.
2. Press Play. The sun is not in view at the start; it lies off to one side. Your ship shows a lit half facing it and a near-black half away from it.
3. Stop and Play again, or launch from the mothership: the sun is somewhere else. In the God Menu **Sector** tab press **Random Seed**: the sun moves with the rocks. **Pin Seed** and Restart: the same sun comes back.
4. Compare the darkness: select the `Directional Light` in Play Mode and set every **Darkness** slider to 0, then back. The sky darkens but the star does not; ships and rocks keep their lit sides and lose their grey shadows.
5. Tune the sliders live until it feels right, note the values, and set them after Play stops (Play Mode changes are not kept).
6. Load the mothership: it looks exactly as before.

### Tuning (step 2b)
On the `Directional Light`'s **Space Lighting**: **New Direction Each Load**, **Clear Of Start View**, **Furthest From Start View** and **Max Elevation**; **Shadow Darkening**, **Rim Dimming**, **Sky Darkening** and **Ambient Darkening**. Darkness changes show live in Play Mode.

### Design (step 3a: space dust)
- **Why.** A sky never moves as the ship flies, so on its own space feels like the inside of a sphere. Faint motes streaming past are the strongest cue of speed and of the size of the space around you.
- **An endless field.** Motes fill a cube around the camera, 160 m across by default. The shader repeats the cube through space and draws each mote's copy nearest the camera, so the field never runs out, never needs spawning, and every camera, including the Scene view, gets its own.
- **Streaks.** A mote smears along the camera's motion, a fixed fraction of a second long, capped in metres and in pixels. Still, it is a round dot; flying, a streak. Motes brighten with speed, so fast feels fast.
- **No popping.** Motes fade in beyond a few metres, judged by the nearer end of the streak so none sweeps across the screen as it passes, and fade out before the cube's edge, so wrapping is never seen.
- **A hint of the comic look.** Each mote is a fleck of rock in one of two flat tones: warm sand where it catches the sun (about two thirds of them), cool charcoal where it is in shadow, matching the rocks. A thin ink rim, under a pixel wide, outlines each one. Streaks keep a fixed pixel width with edges softened over one pixel.
- **Sparse.** The mesh holds 4,000 motes; the material's **Amount** shows a share of them, a fifth by default (about 800, roughly one every 17 m). Amount changes live, with no rebuild. At rest motes are barely visible.
- **Cost.** One draw of one static mesh. The CPU only measures the camera's velocity each frame. Jumps, such as a scene load or a respawn, draw no streak.

### Implementation (step 3a)
| File | Role |
|---|---|
| `Shaders/SpaceDust.shader` | Wraps, streaks and fades the motes |
| `Scripts/Visuals/SpaceDust.cs` | Builds the mote mesh once, measures the main camera's velocity, draws for each camera |
| `Editor/SpaceDustSetup.cs` | Adds a `Space Dust` object to the Environment folder and creates `Materials/SpaceDust.mat` |
| `Editor/GodMenu/Tabs/ProjectTab.cs` | The Sky section becomes **Space**: sky and dust status, with Add Space Dust or Edit Dust |
| `Editor/SceneHierarchy.cs` | Tidy Hierarchy files visual effects into Environment |

### Verified outside Unity (step 3a)
- Both shader stages compile with the DirectX 11 compiler against Unity's shader includes, and the scripts compile.
- The wrap maths: motes always surround the camera, stay still as it moves, and are fully faded at the moment they wrap (20,000 trials, 40 km from the origin).

### Test steps (step 3a)
1. Open Polaris. In the God Menu **Project** tab, the **Space** section reads "Space dust: none". Press **Add Space Dust**. A `Space Dust` object appears under `Environment`. Save the scene.
2. In the Scene view, faint dots fill the space around the view.
3. Press Play and fly. Motes stream past and stretch into streaks with speed, brightening as you go faster. Stop: they shrink back to faint dots.
4. Turn hard while flying. Streaks follow the motion with no flicker.
5. Watch the edge of the field, and fly close past motes. Nothing pops in or out, and no streak sweeps across the screen.
6. Disable the `Space Dust` object while flying, then enable it again, to compare the sense of speed with and without it.

### Tuning (step 3a)
- Look, on `Materials/SpaceDust.mat`: amount, lit and shadow colours and the lit share, brightness, ink (colour, width, strength), field size, width, streak time and caps, fades, rest visibility and full speed. **Amount** is the quickest way to make the dust sparser or thicker; Ink Width 0 removes the rim.
- Count and layout, on the `Space Dust` object: mote count, seed, velocity response and jump distance.

### Design (step 3b: distance fade)
- **Why.** Space has no haze, so without help a rock 2 km away reads as a small rock nearby, and anything at the draw distance pops in or out. Depth needs a cue that is cheap and suits the comic look.
- **Fade towards the void.** From 400 m, every comic surface (ships, field rocks, voxel asteroids, trail rocks) fades towards the sky's void colour, reaching 80% of the way by the time it starts to dissolve. By default the fade is one smooth blend, which the user asked for after the first test. **Band Softness** 0 turns it into four crisp comic bands, each edge one pixel wide.
- **Dissolve, never pop.** Over the last 700 m before the draw distance (300 m before the first test), objects dissolve into the sky through a fine dither, the same one the detail cross-fade uses, so the two never fight. The fade ends at the main camera's far clip, 10 km in Polaris (2 km before the second test), where the asteroid field also stops.
- **One switch for the whole scene.** A `Space Depth` object in the Environment folder sets three shader globals that the comic surface reads. Without it, surfaces are unchanged, so the mothership and test range are untouched. It follows the sky's own void colour, so a new sky needs no retuning.
- **Play Mode only by default.** The Scene view often looks across the whole sector, where everything would dissolve, so the fade shows outside Play Mode only with **Preview In Editor** on.
- **Cost.** A few instructions per pixel and a dither test; nothing on the CPU beyond three global values a frame.

### Implementation (step 3b)
| File | Role |
|---|---|
| `Shaders/ComicSurface.shader` | `DepthFade` in the surface pass and `DepthDissolve` in every pass (surface, ink, and the shadow caster, which also draws the camera's depth), reading `_SpaceDepth`, `_SpaceDepthColor` and `_SpaceDepthDissolve`. The surface cuts its pixels last, after every texture read and derivative |
| `Scripts/Visuals/SpaceDepth.cs` | Sets the globals from its settings, the sky's void colour and the camera's far clip |
| `Editor/SpaceDepthSetup.cs` | Adds the `Space Depth` object; also run by the asteroid field's setup |
| `Editor/GodMenu/Tabs/ProjectTab.cs` | **Visuals** section: distance fade status, with Add Distance Fade or Edit Fade |

### Verified outside Unity (step 3b)
All 80 programs of the comic surface compile with the DirectX 11 compiler against Unity's shader includes: every pass (surface, ink, shadow caster for both depth and point-light shadows) with and without the detail cross-fade, instancing, the detail morph and, for the surface, projected texturing. The same check fails on a copy with a deliberate error, so it is not passing by accident.

### Test steps (step 3b)
1. Open Polaris. If **Set Up Sector** has run for step 2, a `Space Depth` object is already under `Environment`. Otherwise press **Add Distance Fade** in the God Menu **Project** tab's **Visuals** section. Save the scene.
2. Press Play and fly into the Main Belt. Near rocks look as before. Further out, rocks grow darker towards the void in one smooth blend. At the edge of sight rocks dissolve in and out with no pop.
3. Watch an Authority ship fly away: it darkens with distance in the same way, and dissolves at the draw distance.
4. Select `Space Depth` in Play Mode and change **Fade Start**, **Strength**, **Dissolve Range** and **Band Softness**: the look updates at once. Band Softness 0 gives crisp comic bands (as many as **Steps**); Strength 0 turns the colour fade off. Disable the object: every surface is back to the plain look.
5. Outside Play Mode, tick **Preview In Editor** to see the fade in the Scene view, then untick it.

### Tuning (step 3b)
On the `Space Depth` object: fade start, steps, band softness, strength, dissolve range, end (0 uses the camera's far clip), sky colour or a colour of your own, and the editor preview.

### Test steps (after the first test: white rocks and popping)
1. Click into Unity and let it compile. The God Menu **Project** tab shows one step missing again: the field material needs the detail morph. Press **Set Up Sector** and save the scene. `Space Depth` picks up the new defaults by itself: a smooth fade and a 700 m dissolve.
2. Press Play and fly into the Main Belt. No rock is flat white: each has a lit and a shadow side, and keeps them as it moves across the screen.
3. Fly slowly towards a large rock from far away. It comes out of the void dark and dissolving, brightens smoothly, and its outline grows smoother without ever jumping.
4. Watch the small rocks near the edge of sight: they grow in from nothing rather than appearing.

### Design (step 4: comic look)
- **One shader for near objects.** `DeadCargo/Comic Surface` gives ships, rocks and later stations a Borderlands-like look with a hint of cel: the painted texture and its normal map lit in three soft tones (a cool charcoal shadow, a mid tone and the lit colour), a highlight band and a flat glint facing the light, a rim along edges seen side-on, and a bold ink outline round the silhouette. **Cel Softness** sets how soft the tone edges are.
- **No hand-drawn marks.** Hatching and texture ink (inking the texture's darkest details) are built in but off on every material, by choice after testing: the look is the outline and the tones, not drawn lines.
- **Depth through ink.** Ink and hatching fade out with distance, so far objects read as flat and far, like the sky. Each material sets its own fade range.
- **Anchored strokes.** Hatching is drawn in the object's own space, in metres, so it moves with the object and is the same size on any mesh. It fades where strokes would be closer than three pixels.
- **Outlines.** The object's back faces are pushed out by a fixed pixel width and drawn behind it. On rounded shapes such as rocks, **Ink Gap Fill** pushes out from the centre instead, which closes the gaps hard-edged meshes leave at corners.
- **Instanced.** The shader supports GPU instancing, so the asteroid field can draw thousands of rocks in a few batches. Each instance gets a small shade variation of its own.
- **The ship.** Converting the ship copies each of its Flat Kit materials into a comic material, keeping its lit colour, texture and shadow size, and gives the copies to the ship in the open scene. Each copy's shadow colour is its lit colour tinted towards the same cool charcoal as the rocks, so ship and rocks shade alike. Kit files are never changed, and a copy that already exists is reused, so hand-tuned looks are kept. Screens, canopy, shield and effects keep their own shaders.
- **Rocks.** `Materials/Comic/ComicRock.mat`: dusty warm stone against the cold void, cool charcoal shadows, the kit's rock texture for grit and its normal map for relief. The asteroid field (6.8 step 2) draws with it.

### Implementation (step 4)
| File | Role |
|---|---|
| `Shaders/ComicSurface.shader` | The comic surface: lighting, glint, rim, hatching, ink outline pass, shadow caster |
| `Editor/ComicLookSetup.cs` | Converts the ship's Flat Kit materials, creates the rock material, places and removes a test rock |
| `Editor/GodMenu/Tabs/ProjectTab.cs` | The Space section becomes **Visuals**: sky, dust and ship status, with Comic Ship and Place Test Rock |

### Verified outside Unity (step 4)
All three passes compile with the DirectX 11 compiler against Unity's shader includes, plain, GPU-instanced and with shadows, and the scripts compile. Unity has imported the shader without errors.

### Test steps (step 4)
1. Open Polaris. In the God Menu **Project** tab, the **Visuals** section reads "Ship: Flat Kit look". Press **Comic Ship**. Comic copies of the ship's materials appear in `Materials/Comic`. Save the scene.
2. Press Play. The ship is lit in three soft tones with a black ink outline and no lines on its surface. Turn the ship against the light: the shading follows.
3. Back in the editor, press **Place Test Rock**. A comic rock appears ahead of the ship. Fly round it in Play Mode: gritty texture, craters catching the light, violet shadows, outline, rim and highlight. Fly away: past a few hundred metres the outline fades and the rock goes flat.
4. Select a comic material (**Edit Ship Look**, or `ComicRock.mat`) and adjust colours, the tone edges and Cel Softness, texture and normal strength, and the ink.
5. **Ctrl+Z** after Comic Ship puts the Flat Kit materials back.
6. Press **Remove Test Rock** when done.

### Tuning (step 4)
On each comic material: colours, mid tone, shadow and lit edges, cel softness, texture and normal strength, highlight, glint, rim, and ink (width, gap fill, fade range). Hatching and texture ink stay at 0.

### Design (step 5a: Planet Forge installed)
The user chose Planet Forge, by Carlos Wilkes, for the planet system. It is built on his Space Graphics Toolkit (SGT): procedural planets with landscapes, oceans, clouds, atmospheres and volumetrics, which can be seen from orbit or landed on, and 30 demo planets.
- **Where.** `Assets/Plugins/CW/`: `Planet Forge`, `SpaceGraphicsToolkit` and CW's `Shared` code, placed exactly as Unity's own package import would (every file at its recorded path with its `.meta`), 837 MB. Never edited (see Code Standards).
- **Works on our pipeline.** SGT's main shaders are Shader Graph builds with a Built-in pipeline target, so they need the **Shader Graph** package (17.3.0, bundled with this editor; it brings the render-pipelines core library). Its jobs need **Collections** (2.6.5). Its URP and HDRP code only switches on when those pipelines are installed, so it stays off.
- **No Input System.** The project runs on the classic Input Manager, which Space Combat Kit needs; CW's input falls back to it, so the Input System package is not installed.
- **Size.** No file is over GitHub's 100 MB limit (the largest are two 64 MB heightmaps). The repository grows by about 837 MB.

### Verified outside Unity (step 5a: Planet Forge)
- All five CW assemblies (`CW.Common`, `CW.SimpleShaderSystem`, `CW.SimpleOriginSystem`, `SpaceGraphicsToolkit`, `PlanetForge`) compile with Unity's own compiler and settings, with no errors or warnings, and every Dead Cargo assembly still compiles beside them.
- Unity began importing while the files were still being copied, so its first compile missed two files and two shaders missed an include. The files were all in place afterwards; their timestamps were refreshed so Unity compiles and imports them again.

### Test steps (step 5a: Planet Forge)
1. Click into Unity. It finishes importing Planet Forge (a few minutes for its textures) and recompiles. The Console should be free of errors; press **Clear** first to drop any from the interrupted first import.
2. Open `Assets/Plugins/CW/Planet Forge/Scenes/Alythar - Earth Sized (Orbit).unity` (or any planet scene) and press Play. The planet renders with its landscape, atmosphere and clouds: nothing pink. Stop Play, and do not save the demo scene.
3. Open Polaris and press Play: everything works as before.

### Design (step 5b: far space)
The main camera draws 10 km. A planet thousands of kilometres across, thousands of kilometres away, needs a second view of space. This is the first part of sector generation's far world (6.8 step 3).
- **A scale copy of space.** The **far world** is space at 1:1000: one unit is a kilometre. Perspective does not depend on scale, so a 500 km planet 2,000 km away looks exactly like a 500 unit sphere 2,000 units away. The far world holds only distant things, on its own layer (**Far Space**).
- **A far camera.** It draws first, the space sky and then the far world, into a screen-sized HDR texture. The main camera shows that texture as its sky, through a skybox of its own (`DeadCargo/Far Space Backdrop`), and draws the near world on top. Passing the picture as an HDR texture, rather than stacking the two cameras, keeps the sun and the atmospheres brighter than white, so bloom still catches them: a stacked camera's picture is cut to plain brightness before the next camera sees it. Every frame, just before it draws, the far camera copies the main camera's position, turn and field of view, so the two never disagree, at any speed or zoom. The main camera, and every other game camera, leaves the Far Space layer out.
- **The two cameras stand in the same place.** Planet Forge works things out from the main camera: detail on the landscape, the atmosphere, the ocean. So the far camera sits exactly where the main camera is, and the far world moves to match: its origin follows the camera at 999/1000 of the camera's own motion. Flying a kilometre moves you a metre through the far world, which is what 1:1000 means. With no camera of its own to find, every Planet Forge feature works unchanged.
- **Planet Forge on the far camera.** The far camera carries Planet Forge's volume camera (atmospheres and clouds) and opaque texture (oceans), and the Far Space object its volume manager.
- **A far sun.** A camera ignores lights on layers it does not draw, and Planet Forge lights a planet only with lights whose culling mask holds the planet's layer. So the far world has its own sun on its own layer, with Planet Forge's light (`SgtLight`), copying the scene's sun every frame: its turn, colour and brightness. Planets, atmospheres and clouds are lit from the same sun as the ships and rocks, and turn with it when Space Lighting places it each run. The far sun is on only while the far camera draws, and the scene's sun leaves the far layer out, so nothing is lit twice. The scene's own Directional Light is not changed.
- **Cost.** One extra camera, which draws the sky instead of the main camera, so the sky is not drawn twice; one screen-sized texture; one full-screen read where the main camera shows its sky. Atmospheres and clouds are drawn a ninth of their pixels a frame (**Volumetric Checkerboard** 3), built up over frames. No planet means no planet cost. Nothing is generated per frame on the CPU: the far world's origin, the far camera and the far sun are each set once a frame.
- **Off is a switch.** With Far Space disabled, the main camera draws the scene's own sky again, exactly as before this step. The God Menu toggles it live.
- **In the Scene view.** The far world surrounds the camera, so during Play the Scene view shows the planets as huge spheres a few kilometres from the ship. That is the scale copy; only the far camera draws them in the game.

### Design (step 5c: planets from the run seed)
The user shared an article on how Elite Dangerous generates its galaxy. Two of its ideas shape this step: a top-down hierarchy, where each body's seed comes from its parent's, so the star decides its planets and each planet its moons, and adding one body never changes another; and keeping huge distances out of single-precision maths. Its catalogue of real stars and its landable-planet terrain pipeline do not apply here (Planet Forge already builds the terrain).
- **A planet catalogue.** The 26 standard Planet Forge planets (Alythar to Zephyrus, each 500 km in radius) are copied into Dead Cargo prefabs in `Resources/Planets/`, with only what is seen from orbit: landscape, atmosphere, clouds and ocean. Underwater rays, debris and sound are removed. `Data/Planets/PlanetCatalog.asset` lists them, each with its roles and a weight. Ocean worlds can be the primary or a distant planet; dry worlds with air can be any body; airless worlds (Lunara) are moons or distant. The roles and weights can be changed by hand and are kept when the catalogue is built again.
- **Loaded on demand.** A planet's textures are about 17 MB, so the catalogue names its prefabs instead of referencing them, and only the run's planets are loaded, as Polaris loads.
- **Two planets a run.** The user asked for two planets, not all of them. Each run loads exactly two from the catalogue; the other 24 stay on disk. From the run seed, in the order Elite Dangerous uses, parent before child:
  - **The primary**, the world this sector lies near: massive, 16° to 34° across, set at 60° to 160° from the sun, so it is lit from the side or behind the player and shows a broad lit face rather than a dark disc. It lies within 40° of the sector's plane, like the sun.
  - **A second planet**, smaller and further off: 3° to 8° across, elsewhere in the same band. It is always there; if no spot meets every rule it takes the closest.
  - **Moons** are built in but off (**Max Moons** 0): up to four, near the primary in the sky, for later sectors.
  - Every body keeps 6° clear of the sun's edge and 2° clear of the other. The two are never the same planet.
- **Real size, real distance.** A body's place in the sky is a direction and a size. Its distance in the far world follows from its radius: a 500 km planet 30° across stands 1,932 km away, a moon 4° across 14,300 km. Flying from one side of the 40 km sector to the other shifts that planet by about a degree, as a real one would, so it reads as truly vast.
- **Lit by the sun.** Each planet is lit by the run's sun, so its lit face turns towards wherever the sun was placed. A planet near the sun in the sky shows a crescent; one across the sky from it, a full face.
- **Slow life.** Each body turns slowly about a tilted axis (0.02° to 0.12° a second), enough to see clouds and coasts drift over a minute. Turning a transform is the only per-frame work.
- **New each run, pinned with the sector.** Planets come from the sector seed with their own stream, so **Random Seed** gives a new system, a pinned seed brings the same one back, and the sky, the sun and the planets always belong to the same run.

### Design (after the first test of 5b and 5c)
The user saw both planets lit and placed, but "not the best quality" and "some weird glow around them". Three causes:
- **Atmospheres and clouds were coarse.** They were drawn at half size with no smoothing over frames. Planet Forge samples them with noise that is meant to be smoothed over frames, so they showed speckles along the line between day and night, and the half-size layer, stretched back over the full picture, left a blocky halo round each planet's edge. They now use Planet Forge's own demo settings: full size, a checkerboard of 3 (a ninth of the pixels drawn each frame) and smoothing 0.6 (**Volumetric Downscale**, **Volumetric Checkerboard**, **Volumetric Smoothing**).
- **Planets were far too bright.** Polaris's main camera uses the kit's post-processing, which brightens the whole picture by 1.5 stops (about 2.8 times) with an ACES tone curve, for Dead Cargo's look. Planet Forge's planets were made without any, so they washed out to white, and with bloom set to start at 1 and spread wide (diffusion 7) every bright edge bloomed into a broad glow. The far sun now shines at **Planet Light** 0.35 of the scene's sun on planets only, which brings them back under the bloom threshold and keeps their colours. The sky, ships and rocks are unchanged.
- **Edges were jagged.** The far picture now uses the quality level's anti-aliasing (4 samples when it has none).
- The first test also showed three bodies: **Add Planets** had run before the two-planets change compiled, so the component kept the earlier moon and distant-planet values. Removing `Far Space` and adding planets again picks up the current defaults.

### Implementation (steps 5b and 5c)
| File | Role |
|---|---|
| `Scripts/Sectors/Far/FarSpace.cs` | The far world, the far camera and the far sun: follows the main camera, shows the far picture as its sky, keeps every other camera off the layer |
| `Shaders/FarSpaceBackdrop.shader` | The main camera's sky: the far camera's picture, pixel for pixel, in HDR |
| `Scripts/Sectors/Far/BackdropBodies.cs` | Builds the run's planets in the far world from the seed, the sun and the catalogue; turns them |
| `Scripts/Sectors/Far/BackdropLayout.cs` | Plans the run's system: pure maths, checked by the self-test |
| `Scripts/Sectors/Far/BackdropSettings.cs`, `BackdropPlan.cs` | The rules for each kind of body, and one planned body |
| `Scripts/Sectors/Far/PlanetCatalog.cs`, `PlanetEntry.cs`, `BodyRoles.cs` | The catalogue of planets, each with its roles, weight and radius |
| `Editor/Visuals/PlanetCatalogBuilder.cs` | Copies Planet Forge's 26 planets into `Resources/Planets/` and builds the catalogue. Opens each demo scene beside the open one and closes it unsaved |
| `Editor/PlanetsSetup.cs` | **Add Planets**: the Far Space layer, the Far Space object with its planets and backdrop shader, and the catalogue |
| `Editor/SceneHierarchy.cs` | **Tidy Hierarchy** files far space with the visuals, in Environment |
| `Editor/GodMenu/Tabs/ProjectTab.cs` | **Visuals**: the planets' status, **Add Planets** or **Edit Planets** |
| `Editor/GodMenu/Tabs/SectorTab.cs` | **Planets** section in Play Mode: Far Space on or off, and each body's size, place and angle from the sun |
| `Editor/Diagnostics/SectorSelfTest.cs` | Backdrop checks |

### Verified outside Unity (steps 5b and 5c)
- Every Dead Cargo assembly compiles with Unity's own compiler and settings, against Planet Forge's assemblies, with no new warnings.
- The backdrop shader's vertex and fragment programs compile with the DirectX 11 compiler against Unity's shader includes; a deliberately broken copy fails, so the check is real.
- The seven new backdrop checks pass, run outside Unity against the compiled code, over 1,000 runs with Polaris's sun (60° across, with a 4° corona):
  - with Polaris's rules, every run has exactly two planets, the primary and one other, never the same planet, both meeting every rule
  - with moons switched on, so every rule is exercised: every run has a primary, and every body meets every rule; none settled for a closest miss
  - nothing comes nearer the sun's edge than 6.3° (6 needed), and no two bodies overlap
  - 1,051 moons, every one beside its primary; 504 distant planets; all within the 40° band
  - no planet plays a part it is not given, a weight of 0 is never chosen, and no planet repeats within a run
  - the same seed gives the same system; taking the moons away leaves the primary exactly where it was
  - a 500 km planet 30° across stands 1,932 km away
- Not run in Unity: copying the planets from the demo scenes, the far camera, its picture as the main camera's sky, the far sun, Planet Forge drawing in the far world, and the God Menu.

### Test steps (steps 5b and 5c)
1. Click into Unity and let it compile. The Console should be free of errors, shader errors included.
2. Open Polaris. In the God Menu **Project** tab, **Visuals** reads "Planets: none". Press **Add Planets**, then **Copy Planets**. A progress bar copies Planet Forge's 26 planets, a minute or two. Afterwards `Resources/Planets/` holds 26 prefabs, `Data/Planets/PlanetCatalog.asset` lists them, a `Far Space` object sits under `Environment`, and **Visuals** reads "Planets: new each run, from 26 in the catalogue". Project Settings > Tags and Layers shows layer 12, **Far Space**. Save the scene. Planet Forge itself is unchanged: its demo scenes were only read.
3. Press Play and look around. Two planets hang in the sky: a massive one, 16° to 34° across, and a smaller one, 3° to 8°, elsewhere. Each is lit on the side facing the sun, with its atmosphere and clouds. Neither touches the sun. Ships, rocks and dust draw in front of them.
4. Open the God Menu **Sector** tab, **Planets** section: two lines, **Primary** and **Distant**, with each planet's name, size, angle from the sun and bearing. Turn to each by its bearing (clockwise from where you started) and height.
5. Fly at full boost and turn hard. Planets hold still in the sky, like the sun, with no shake or lag.
6. The sun still glows as before: switch **Far Space** off in the **Planets** section and back on, and compare. Off, the planets vanish and the sky is exactly as it was.
7. Press **Random Seed**: a new sun and a new system. **Pin Seed**, then Restart: the same planets come back. **Unpin**.
8. Watch a planet for a minute: its clouds and coasts drift slowly as it turns.
9. Load the mothership: it looks as before.
10. If the frame rate drops, note it with Far Space on and off. On the `Far Space` object, a higher **Volumetric Checkerboard** makes atmospheres and clouds cheaper.

**After the first test (quality and glow).** Planets made by an earlier **Add Planets** keep its values, so start fresh:
1. Click into Unity and let it compile. Stop Play. Delete `Environment/Far Space`, press **Add Planets** in the God Menu **Project** tab (the catalogue is already built, so it is quick), and save the scene.
2. Press Play. Two planets only: the **Planets** section lists **Primary** and **Distant**.
3. Each planet keeps its own colours instead of washing out to white, with no wide glow round it, and its edge is smooth.
4. The line between day and night is clean, without coloured speckles. Turn fast: atmospheres and clouds hold, with no smearing trails.
5. Select `Far Space` in Play Mode and drag **Planet Light**: lower is darker and moodier, higher brighter. Note the value you like and set it after Play stops.

### Tuning (steps 5b and 5c)
- On `Far Space`'s **Far Space**: scale, near and far clip; **Planet Light** (how bright the sun is on planets, live in Play Mode); **Volumetric Downscale**, **Checkerboard** and **Smoothing**; **Anti Aliasing**.
- On `Far Space`'s **Backdrop Bodies**: the primary's chance, size, angle from the sun and clearance from the opening view; how many moons (0) and distant planets (1) and their sizes; band, sun and body clearance; spin. Changes rebuild the system at once in Play Mode.
- On `Data/Planets/PlanetCatalog.asset`: each planet's roles and weight. A weight of 0 takes a planet out of every run.
- Each planet's look, on its prefab in `Resources/Planets/` (Planet Forge's own components). Matching them to Dead Cargo's darker space is step 5d.

---

## Phase 6.10: Destruction

### Goal
Rock breaks the way you hit it. Asteroids are made of voxels: guns pit them, missiles blast craters, saw cuts slice them apart, and what breaks away drifts off as a rock of its own. Ore runs through the rock in veins, so mining (phase 8) digs the same voxels. Every kind of destruction shares one damage path, so later steps plug in without changing this one.

### Steps
| Step | Delivers | Status |
|---|---|---|
| 1. Voxel asteroids | Smooth voxel rock built from a seed, carved by every kit weapon, pieces that break away and drift, ore veins and yields by material, rock chips, God Menu tab, self-tests | Built and smoke-tested, awaiting test |
| 1b. Breaking and motion | Cracks that break a small, medium or large chunk off anywhere, with ragged faces. Every rock drifts and tumbles on its real mass and inertia; both parts of a split push apart; hits push rocks | Built and compile-checked, awaiting test |
| 1c. Rock trails and speed | Trails of small rocks behind large asteroids that ships collide with and weapons shatter; half-detail meshes for distant rock; cuts across whole rocks made on a worker thread; a God Menu benchmark | Built and compile-checked, awaiting test |
| 1d. Performance pass | A hidden warm-up at load so the first hit of a run is as cheap as the rest; quarter detail for far rock and closer detail switches; no main-thread waits when rock is spawned or freed; chips budgeted per second so the pool never fills; pools filled at load; a benchmark with percentiles, GPU time, garbage, a steady load and one seed | Built, compile-checked and self-tested, awaiting test |
| 2. Mining tool | A mining beam that cuts precisely and puts what it cuts into the hold, by material. Merges with phase 8 | Planned |
| 3. Asteroid field | The field's near rocks become voxel asteroids, far rocks stay instanced, cuts last for the run. With 6.8 steps 2 and 5 | Planned |
| 4. Shatter | Ships, crates and stations break into pre-cut pieces through the same damage path | Planned |
| 5. Optimisation | Giant asteroids streamed in chunks, voxel memory for distant rock, profiling against the 6.8 targets. Half detail for distant rock and cuts across whole rocks off the main thread came early, in step 1c; quarter detail, the warm-up and the waits removed in step 1d | Planned |

### Design (step 1)
- **Smooth voxel rock.** An asteroid is a grid of samples, each holding the distance to the rock's surface in one byte. The surface is rebuilt from it with Surface Nets: one vertex per cell the surface crosses, with smooth normals from the distance field. It keeps the comic rock look (painted texture, three tones, ink outline); cubes never show.
- **Built from a seed.** An asteroid type sets the shape (stretch, lumps, craters, roughness), the voxel size and up to four ores. A type, a diameter and a seed build one rock; the same seed always builds the same rock.
- **Ore veins.** Each ore runs through the rock as thin sheets, only deeper than its minimum depth, and shows as coloured veins drawn in the same three comic tones. Two types ship: **Rocky Asteroid** (bare rock, a trace of iron) and **Ore Asteroid** (iron veins, cobalt crystal deep inside).
- **Damage carves.** Every kit weapon hit carves a crater sized by its damage, dug in along the shot: 0.02 m³ per point by default, so a gun hit (100) pits about 0.8 m and a missile (2,500) blasts about 2.3 m. Explosions remove 1.6 times as much. Crater edges are ragged. Saw cuts (`Cut`) remove a flat disc.
- **Breaking apart.** After a cut, a search finds any piece no longer joined to the rock. Large pieces become rocks of their own; pieces under 60 voxels crumble into chips. A piece keeps exactly its place, shape, ore, texture and the motion it had as part of the rock. The piece and the rock then push apart with equal and opposite pushes, so the lighter one moves more: a chip flies off a big asteroid that barely stirs, and two halves part at the same speed. They twist apart the same way.
- **Cracks (step 1b).** `VoxelBody.Fracture` breaks a chunk off along a ragged crack, the way rock splits. Pick a direction and a share, or a size: **small** (4 to 10% of the rock), **medium** (12 to 25%) or **large** (30 to 50%, up to a clean break in two) in a random direction. The crack is placed so the chunk holds that share of the rock, whatever its shape. Its faces are bumpy (`Crack Roughness`) and its gap is never under 2.5 voxels, so the parts always come apart.
- **Motion (step 1b).** Every rock drifts and tumbles. Mass, centre of mass and inertia come from the rock's own voxels and follow every crater, crack and lost piece exactly. A rock keeps its angular momentum rather than a fixed spin axis, so a lopsided one wobbles as it tumbles, as real asteroids do. Each type sets a drift and tumble range; smaller rocks turn faster. The same seed always gives the same motion. Rocks are moved kinematically, so they push ships but ships never push them, and rocks pass through each other (see the backlog).
- **Hits push.** Weapon hits and explosions push a rock where they land, by `Push Per Damage` (0.5 tonne metres per second per point), shared out by real mass: steady gunfire shoves an 8 m rock along, pebble-sized pieces fly off, and a 40 m asteroid barely notices a missile. Off-centre hits set it turning. Mining and scripted cuts never push.
- **Rock trails (step 1c).** Every asteroid at least 30 m across sheds a trail of small rocks, like a comet's tail of rubble: about 40 behind a 40 m asteroid, up to three times as many behind larger ones, each 0.4 to 1.6 m (bigger behind bigger asteroids), tumbling and uneven so no two look alike. They leave from the asteroid's back and ride along with it, sliding slowly back along the trail (0.3 m/s relative to the asteroid) until they are 2.5 times its diameter behind, then shrink away. Each rock's velocity eases towards its asteroid's own over about four seconds, so the trail follows wherever the asteroid drifts and swings round behind it when it turns or is pushed, like rubble held loosely by the rock's gravity. Rocks that wander wider than the asteroid ease back towards the trail's line. Whenever an asteroid comes into trail range (2.5 km, far beyond sight), its trail is topped up to full, as if it had been shedding all along: the first time it is seen, and again when its trail thinned out while it was out of range. A piece that breaks away starts with none.
- **Solid near you (step 1c).** Trail rocks within 150 m of the camera are solid: up to 64 at once, nearest first. Each borrows a pooled stand-in with a rigidbody and a ball collider, so your ship hits them and knocks them flying, and they bounce off asteroids and each other. A rock knocked off its trail stays off and flies on. A weapon hit shatters one into chips. Further out, rocks are moved by the job alone. Rock mass follows its size and density, scaled against the kit's ships, which weigh 1: a 1 m rock weighs about 0.006 and a 2 m rock 0.04. On top of that, **Ship Kick** (0.15) softens every hit inside the physics step itself: in a contact between a trail rock and anything else that moves, the other body acts about seven times heavier and harder to turn. The rock still flies off as physics says, but a ship barely moves, even at full speed. Their surface has no friction and little bounce, so a glancing hit does not twist the ship round, and a rock never turns solid while touching something. The kit still deals collision damage by impact speed alone (2.5 per m/s), whatever the rock weighs.
- **Half detail far away (step 1c).** Rock further than 200 m from the camera (300 m before step 1d, which also adds quarter detail beyond 400 m) (for 0.5 m voxels; larger voxels switch proportionally further) meshes from every other sample: a quarter of the triangles, with lighter colliders. The switch has 10% of slack either side so it never flickers; coming closer remeshes at once, moving away in the background. Each chunk **cross-fades** to its new mesh over half a second (`Detail Fade`): the old mesh stays on a second renderer while the new one takes over pixel by pixel through a fine dither, ink outline and shadow included, so a switch never pops and neighbouring chunks never show a seam. The rock's motion smoothing stays on at every detail. Rock under 16 voxels across always stays at full detail, and a rock that half detail would miss entirely is remeshed at full detail rather than treated as dug out.
- **Accounting.** Every cut reports what it removed, by material, in cubic metres: on the body (`VoxelBody.MaterialRemoved`) and for the whole scene (`DestructionSystem.MaterialRemoved`). Mining will turn this into cargo. The God Menu shows the running totals in m³ and tonnes.
- **Chips.** Every cut throws tumbling rock chips in the colour of what was hit, which shrink away. One particle system serves the whole scene.
- **One damage path.** `IDestructible` and `DamageEvent` are the shared entry point. Kit weapons reach any destructible through `DestructionDamageReceiver` on its colliders; other systems call `ApplyDamage` directly. Ship break-up and shatter (step 4) implement the same interface.
- **Limits.** At most 24 free pieces at once (past that the smallest crumble), and pieces more than 3 km from the camera are removed.

### Performance
- All voxel work runs in Burst-compiled jobs: generation in parallel slices, meshing in parallel batches written straight into mesh data, piece searches and moving pieces out of their rock on a worker thread, and collider cooking on worker threads.
- The main thread never waits for a job. It applies finished results and runs cuts (small Burst jobs); a rock that a job is still meshing, searching or emptying of pieces takes its next cuts a frame later. Buffers and meshes are pooled, with no per-frame allocations.
- Budgets per frame: 32 cuts (the rest wait, oldest first, rocks taking turns), 48 rebuilt chunks for cuts and 8 for new rock, 16 colliders switched to their new shape (the rest keep their previous shape a frame longer). Chips have a budget per second instead (step 1d). At most 512 cuts may wait on one rock; only fire far beyond any real fight reaches that, and further hits are dropped.
- Colliders are cooked on workers with light options (mesh cleaning and the fast midphase, no welding), so switching one costs about 0.01 ms.
- Saw cuts visit only the thin slab they remove, not the box around it. Ragged crater walls come from three broad waves around the sphere, not noise per voxel.
- Rocks are moved by hand from their own mass and inertia, so Unity never recomputes either as colliders change. Mass is tracked as volume moments: every cut, crumb and piece reports exactly the moments it removed, so a rock is never measured twice. Generation measures it in the same parallel pass that fills it.
- Placing a crack reads at most about 110,000 samples (the largest rock is read every fourth voxel) and walks only the thin slab it removes; its ragged face costs two noise samples per row, not per voxel.
- **Cuts across whole rocks run on a worker (step 1c).** Cracks, saw cuts and blasts over 24 voxels across are cut by a job on a worker thread and land a frame later; craters stay inline, where they cost well under 0.1 ms. One heavy cut runs at a time, and the rock takes no other cut, mesh or piece search until it is done. This removes the last main-thread spike (15 ms under extreme stress in step 1).
- **Half detail (step 1c)** quarters the triangles, draw cost and collider size of distant rock. The cross-fade costs a second draw of a chunk for half a second; normal rendering never uses the dither, which is a separate shader variant.
- **Softer ship hits (step 1c)** cost nothing outside a hit: Unity's contact hook only runs for contacts involving a trail rock's collider, and changes two numbers per contact.
- **Trail rocks (step 1c)** live in one native array: no object per rock. One Burst job moves them all and writes their matrices; it is started early in the frame and finished in late update, so it runs alongside the game's scripts. All trail rocks draw with GPU instancing, up to 1,023 a draw call, without shadows. Only the solid ones near the camera have physics, at most 64, and the nearest are picked ten times a second. At most 1,536 trail rocks exist at once, and asteroids over 2.5 km away grow none.
- **Benchmark (step 1c).** The God Menu **Benchmark** builds eight asteroids of 30 to 70 m and for ten seconds fires six hits a frame at them (one in twenty a missile) and cracks a chunk off one every second, then reports frame time and destruction main-thread time (average, worst, per stage, trails) to the Console. Reworked in step 1d, below.
- **Warm-up at load (step 1d).** The step 1c benchmark ran at 0.48 ms of destruction a frame on average, but its worst frames reached 8 ms, always the same: cuts 6.4 ms, pieces 2.9 ms. That is the cost of code running for the first time (the editor compiling it, job set-up, the first piece and chunk objects), paid by the player's first hit and first break. Now, when a scene builds its first asteroid, the `Destruction System` also builds a 12 m rock 50 km below the play space, craters it, cracks it in two and frees both parts (`DestructionWarmUp`, about half a second). Meanwhile it fills the mesh pool for a full urgent batch and adds four pooled pieces with 16 spare chunk objects each, one a frame. The warm-up rock raises no system events, never counts against the piece budget, and tools and readouts skip it. **Warm Up On Load** on the system turns it off.
- **Quarter detail (step 1d).** Rock beyond 400 m meshes every fourth sample: a sixteenth of the triangles, with lighter colliders. Half detail now starts at 200 m, not 300 m. Both distances are for 0.5 m voxels and scale with voxel size. At either switch a cell spans about 5 pixels at 1080p; at 300 m a full-detail voxel was under 2 pixels, so most of the far triangles were wasted. The step 1c benchmark field drew 1.69 million triangles, each drawn up to three times (surface, ink outline, shadow). Rock under 32 voxels across never goes below half detail. Switches keep the 10% slack and the cross-fade.
- **Meshed once, at the right detail (step 1d).** A new asteroid, and a new piece, picks its detail from its distance before it is first meshed. Before, far rock was meshed at full detail, then meshed again at half detail on the next distance check, with a cross-fade it did not need. Fixing that took the benchmark field's build from 1.39 s to 1.19 s. The step 1c figure of 0.55 s is not comparable: back then each spawn finished every other rock's generation on the main thread, so the benchmark's spawn loop built seven of its eight rocks in one stalled frame, before its build timer started. Generation now runs on workers while the game carries on, and the timer counts it. The warm-up also makes the cross-fade material, which a first switch would otherwise make mid-game.
- **No main-thread waits (step 1d).** Building an asteroid used to finish every running job in the scene first, so each spawn stalled the frame until all meshing, collider cooking, piece searches and heavy cuts were done. Now it waits only for jobs on that rock, and a new rock waits for nothing. Freeing a rock (dug out, crumbled, too far away, removed) used to wait for any job still using its grid. Now it stays hidden and is freed a frame later, once the job is done. Destroying a rock no longer finishes every job in the scene either.
- **Chips per second (step 1d).** Chips had a budget of 96 a frame, so a fast machine threw more per second than a slow one. Under heavy fire the 2,048-chip pool stayed full, and Unity drops new chips when it is full, so the newest hits threw none. Now chips have a budget per second (750), banked for a tenth of a second through quiet moments, so one big blast still throws its full burst. It is capped below what the pool can hold over a chip's longest life, so the pool never fills. When cuts in a frame want more than the budget, each gets its share and at least one chip. Chips are queued and thrown at the start of the next frame, the same frame the crater's new mesh shows, so chips and crater appear together and the particle system is never changed while its own update job runs.
- **Benchmark (step 1d).** Waits for the field, the warm-up and three seconds of trail growth, then measures. The load is a steady 800 hits a second whatever the frame rate, at most 64 in a frame. Every choice (rock sizes, rotations, shot sides, crack sizes and directions) comes from one seed, and the engine's own randomness is seeded for the run and restored after. A ninth, 40 m rock sits about 130 m off to the side, so its trail rocks turn solid and their physics is measured too. It reports average, median, p95, p99 and worst for frame time, GPU time (when Unity reports it), destruction time and every stage, trails and chips. It also breaks down the worst frame stage by stage, and reports garbage a frame, collections, and the detail mix.
- Each stage's main-thread cost is timed, shown in the God Menu **Cost** section, and marked for the Unity Profiler (`DeadCargo.Voxels.*`).
- A piece search starts at the cut and stops as soon as only one part is still growing: a crater that splits nothing visits a few hundred voxels; a split costs about twice the smaller piece.
- Memory is two bytes per voxel. A 40 m asteroid at 0.5 m voxels takes about 3.5 MB; larger rocks get larger voxels, so no grid passes 192³.
- Each chunk of 32³ voxels is one renderer and one collider; empty chunks have neither.

### Implementation
| File | Role |
|---|---|
| `Scripts/Destruction/Core/` | Assembly `DeadCargo.Destruction.Core`: pure voxel maths and its Burst jobs, with no kit or scene code |
| `Core/VoxelGrid.cs`, `VoxelEncoding.cs` | The voxel grid and how distance is stored |
| `Core/AsteroidShape.cs`, `OreLayer.cs`, `VoxelGeneration.cs` | Asteroid shape and ore as a distance field; filling a grid from it |
| `Core/SurfaceNets.cs`, `MeshBuffers.cs`, `VoxelVertex.cs` | Seamless smooth meshing per chunk, into a 20-byte vertex; at full, half (step 1c) or quarter detail (step 1d) |
| `Scripts/Destruction/DestructionWarmUp.cs` | The hidden rock cut once at load, and the pools filled meanwhile (step 1d) |
| `Core/VoxelCarving.cs`, `CarveRequest.cs`, `CarveShape.cs` | Sphere and slab cuts, with what was removed by material |
| `Core/VoxelIslands.cs`, `IslandBuffers.cs`, `IslandInfo.cs` | Finding pieces that broke away, moving them into their own grid, measuring rock |
| `Core/MassMoments.cs` | Volume and its first and second moments: centre of mass and inertia, added and subtracted exactly (step 1b) |
| `Core/VoxelFracture.cs` | Places a crack so a chosen share of the rock lies beyond it (step 1b) |
| `Scripts/Destruction/Voxels/FractureSize.cs` | Small, medium and large chunk sizes (step 1b) |
| `Core/TrailRock.cs`, `Core/TrailOwner.cs`, `Core/Jobs/TrailRockJob.cs` | A trail rock, the asteroid it follows, and the Burst job that moves every trail rock with its asteroid and writes its matrix (step 1c) |
| `Scripts/Destruction/TrailRocks.cs` | Trails behind large asteroids: emission, the per-frame job, instanced drawing, solid stand-ins near the camera (step 1c) |
| `Scripts/Destruction/TrailRockProxy.cs` | A pooled stand-in that makes a trail rock solid; weapons shatter it (step 1c) |
| `Scripts/Destruction/Voxels/CarveBatch.cs` | A batch of cuts and its job buffers: one run inline, one on a worker for heavy cuts (step 1c) |
| `DevTest/DestructionBenchmark.cs` | The God Menu benchmark (step 1c) |
| `Core/Jobs/` | The Burst jobs: fill, mesh, carve, piece search, moving pieces out, collider cooking |
| `Scripts/Destruction/IDestructible.cs`, `DamageEvent.cs`, `DamageKind.cs` | The shared damage path |
| `Scripts/Destruction/DestructionDamageReceiver.cs` | Kit weapon hits to `DamageEvent` |
| `Scripts/Destruction/DestructionSystem.cs` | Scene service: runs the pipeline, pools, chips, events, cost readouts |
| `Scripts/Destruction/DebrisParticles.cs`, `EffectPool.cs` | Rock chips in one particle system; pooled effect prefabs |
| `Scripts/Destruction/Voxels/VoxelBody.cs`, `VoxelChunk.cs` | A carvable rock and its chunk objects; its mass, drift, tumble, pushes and cracks; a chunk's cross-fade between detail levels |
| `Scripts/Destruction/Voxels/VoxelScheduler.cs` | The once-a-frame job pipeline for every body |
| `Scripts/Destruction/Voxels/AsteroidDefinition.cs`, `VoxelMaterialDefinition.cs`, `OreVein.cs` | Asteroid types and rock materials |
| `Scripts/Destruction/Voxels/VoxelAsteroid.cs` | Places an asteroid in a scene |
| `Scripts/Destruction/Voxels/VoxelYield.cs` | What one cut removed, by material |
| `Shaders/ComicSurface.shader` | Gains **Triplanar**: projected texture and normal map for voxel rock, and ore veins from vertex colour. Off by default, so existing materials are unchanged. Step 1c adds a dithered cross-fade (`_DITHER_FADE`, set only while a mesh changes detail) to the surface, ink and shadow passes |
| `Editor/Destruction/DestructionSetup.cs` | Setup step: the system and the default asteroid types, materials and looks |
| `Editor/GodMenu/Tabs/DestructionTab.cs` | God Menu tab |
| `Editor/Diagnostics/Voxels/` | Assembly `DeadCargo.Destruction.Diagnostics`: the core self-test |
| `Editor/Diagnostics/DestructionDiagnostics.cs`, `DevTest/DestructionSmokeTest.cs` | Menu entries for the core self-test and the in-game smoke test |

### Verified before handover
- **Step 1d:** every Dead Cargo assembly compiles with Unity's own compiler and settings, with no new warnings. The core self-test, now 46 checks, passes outside Unity. The six new checks: half and quarter detail meshes of a 20 m sphere are closed across chunk seams, face outward, and keep its volume (-0.3% and -1.2%) and surface (within 0.02 m of a 1 m cell and 0.18 m of a 2 m cell). They also carry a quarter (14,940) and a sixteenth (3,660) of the full detail's 60,156 triangles.
- **Step 1d in Unity (editor, the user's run):** destruction 0.45 ms a frame on average, p99 0.94 ms, worst 2.06 ms (was 8.0 ms). Cuts worst 1.20 ms (was 6.4) and pieces 0.16 ms (was 2.9), confirming the old worst frames were first-run costs. Triangles 0.46 million (was 1.69), GPU 1.4 ms a frame, chips at most 1,458 in the air, up to 10 solid trail rocks. The one remaining spike is a 1.56 ms mesh upload after a crack (p99 0.23 ms). That run led to meshing new rock at the right detail at once.
- **After that fix (the user's second run):** destruction 0.37 ms a frame on average, p99 0.67 ms, worst 1.72 ms (still a mesh upload after a crack, p99 0.15 ms). Every stage is under 0.1 ms on average. 0.42 million triangles, field built in 1.19 s, garbage 15.3 KB a frame with no collections, no errors or warnings.
- **Core self-test, 40 checks, run outside Unity** on the same code: sphere volume within 0.1%, surfaces closed across chunk seams, faces outward, surface within 7 mm of a true sphere; cratered rocks closed and within bounds; same seed same rock, also when filled in slices; crater volume within 0.1% of the true overlap and every bit removed accounted for; a bar cut frees exactly the smaller rock with no voxel lost or doubled and both parts closed; a saw cut gives two equal halves; a cut across a whole rock searched in full; ore veins present and reported by material; an oversized mesh cut short with nothing written past its buffers; a rock dug out entirely leaves nothing.
- **Every Dead Cargo script compiles** with Unity's compiler, both new assemblies included.
- **The comic shader compiles**: 20 programs (plain, triplanar, shadowed, instanced, ink, shadow caster) with the DirectX compiler against Unity's includes.
- **Headless Unity run on a copy of the project** (Unity 6000.3.12f1, Burst on, editor safety checks on, no rendering): the core self-test passes in Unity's own runtime (40 of 40); the setup tool builds every asset correctly on a fresh scene and is safe to run twice; the in-game smoke test passes (13 of 13, below); the God Menu tab shows; a stress run logs no errors. The run found and fixed one real bug: the kit's weapon receiver threw on every hit when added from code.
- **In-game smoke test**: an ore asteroid builds in 0.03 s (26 chunks, 50,680 triangles), every surface chunk collides, a ray reaches its weapon receiver, a kit weapon hit carves a crater whose volume is accounted to the thousandth of a cubic metre and which physics feels (the surface moves back 1.56 m), digging reports ore by material, a saw cut splits it with no rock lost or doubled, the piece drifts away, and a rock dug out entirely is removed.
- **Stress run**, five asteroids of 30 to 70 m (663,284 triangles, 34 MB of voxels) built in 0.3 s at a worst 0.47 ms of main-thread time a frame. Realistic fire (twin guns and a missile a second): 0.00 ms a frame on average, 0.15 ms at worst. Extreme fire (20 hits a frame up to missile strength, and saw cuts through whole rocks twice a second, about 6,700 hits a second): 1.0 ms on average, 15 ms at worst, with every queue drained afterwards and no negative volumes.

### Test steps (step 1)
1. Click into Unity and let it compile. The Console should be free of errors.
2. Open Polaris. In the God Menu **Project** tab the summary shows one step missing; press **Set Up Sector**. A `Destruction System` appears under `Systems`; asteroid types appear in `Data/Destruction`, and `ComicVoxelRock.mat` in `Materials/Comic`. Save the scene.
3. Run **Dead Cargo > Diagnostics > Destruction Self-Test**. The Console reports every check passed (46 since step 1d).
4. Press Play. In the God Menu **Destruction** tab, under **Asteroids**, pick **Ore Asteroid**, 40 m, 120 m and press **Spawn Ahead**. The rock appears within a few frames: comic tones and outline, orange iron veins and, deeper in, teal crystal.
5. Fly up and shoot it. Every hit pits the rock where it landed and throws chips in the colour of what it hit. Fire a missile: a much bigger crater.
6. Fly gently into a crater: the ship collides with the new shape.
7. Keep shooting a thin part until a piece breaks away. Shoot the piece: it carves too, and can split again. Small crumbs burst into chips.
8. Under **Target Rock**, try **Crater**, **Blast** and **Crumble** too.
9. Open **Cost**: main-thread time in all and by stage, chunks, memory, and the rock dug out so far by material, in m³ and tonnes.
10. Under **Self-Test**, press **Smoke Test**. After a few seconds the Console reports its checks; the test asteroid and its sawn-off piece stay in view.
11. **Remove All** clears every asteroid and piece.

### Test steps (step 1b)
1. Click into Unity and let it compile. The Console should be free of errors. No setup is needed: the asteroid types pick up the new settings with their defaults.
2. Press Play in Polaris and spawn a **Rocky Asteroid**, 40 m, from the **Destruction** tab. Watch it for half a minute: it tumbles slowly (one turn every one to three minutes) and drifts a little. A lopsided rock wobbles as it turns rather than spinning on a fixed axis. **Target Rock** shows its mass in tonnes and its drift and turn speed.
   - **Which rock the buttons act on:** the rock your ship points at ("in your sights"). Look away and it stays the target, so breaking a chunk off never moves the target onto the chunk. To work on a chunk, point at it. A newly spawned rock becomes the target. With no target yet, the nearest rock by its surface is used.
3. Press **Small Chunk** several times. Each press cracks a small chunk off a different, random place. Crack faces are bumpy, not flat. The chunk flies off; the rock barely stirs.
4. Press **Medium Chunk**, then **Large Chunk**. The bigger the chunk, the more the rock recoils. A large chunk can be close to half the rock: then **both** halves drift apart at about the same speed and twist away from each other.
5. **Random Chunk** picks a size for you. **Shatter** cracks three chunks of random sizes off at once.
6. Spawn a small rock (**Diameter** 8 m) and hold fire on it: it shoves along, and off-centre hits set it spinning. Pebble-sized pieces fly off when hit. Shoot the 40 m rock: it barely notices.
7. **Push** shoves the target away from you at 3 m/s; **Spin** sets it turning at 20°/s about a random axis; **Stop** holds it still.
8. Fly into a drifting rock: it pushes your ship, and your ship never pushes it.

### Test steps (step 1c)
1. Click into Unity and let it compile. The Console should be free of errors.
2. Optional, outside Play Mode: press **Set Up Sector** in the God Menu **Project** tab again. It adds `Materials/Comic/ComicTrailRock.mat` (the rock look with instancing on and a finer texture) and hands it to the `Destruction System`, which keeps trail rocks working in builds. Without it, trails use the asteroid's own material. Save the scene.
3. Press Play in Polaris and spawn a **Rocky Asteroid**, 60 m. A trail of small tumbling rocks streams out behind it, against its drift, about two and a half times its length. Only asteroids 30 m or more across have one: a 20 m rock has none.
4. Fly into the trail, slowly at first, then at full speed. The rocks are solid: your ship knocks them flying with a light tap, even at full speed, and they tumble off and do not come back to the trail. **Ship Kick** on the `Destruction System` sets how much you feel them. **Cost > Trail rocks** shows how many drift and how many are solid near you.
5. Shoot a trail rock: it bursts into chips.
6. Watch the asteroid drift: its trail travels with it and stays behind it. Press **Push** on the asteroid: it moves off at 3 m/s, its trail follows a few seconds behind and swings round to stream behind its new path.
7. Fly 400 m or more away from a 40 m rock. **Cost > Detail** counts it at half detail, and its triangle count falls by about three quarters. It still looks the same from there: the switch dissolves over half a second, with no pop. Fly back: it dissolves back to full detail, with the same craters, and stays exactly where it was, with no jump.
8. **Push** an asteroid away, let it drift past 300 m, then chase it. As you close in it switches to full detail without a jump or flash, and its trail is still behind it.
9. Under **Target Rock**, press **Large Chunk** on a 100 m rock. The crack lands a frame later, and **Cost > Main thread** stays low with no spike. **Heavy cuts** counts it.
10. Under **Self-Test**, press **Benchmark**. Eight asteroids appear ahead and are shot at for ten seconds. The Console then reports frame time and destruction main-thread time, average and worst, by stage and for trails. The field stays in place; **Remove All** clears it.

### Test steps (step 1d)
1. Click into Unity and let it compile. The Console should be free of errors. No setup is needed. The `Destruction System` picks up the new settings at their defaults: **Warm Up On Load** on, **Half Detail Start** 200, **Quarter Detail Start** 400, **Max Chips Per Second** 750.
2. Run **Dead Cargo > Diagnostics > Destruction Self-Test**: 46 checks pass, six of them new, for half and quarter detail.
3. Press Play in Polaris, fly nowhere, and under **Self-Test** press **Benchmark**. Nine rocks appear, one of them close off to the side with its trail. A few seconds later the shooting starts; ten seconds after that the Console reports. Check:
   - **Start-up** says *warmed up before the run*.
   - **Destruction**: worst and p99 well below the old 8 ms worst frame. **Worst frame** shows which stage it was.
   - **Trails** says some rocks were solid.
   - **Chips**: the peak in the air stays below 2,048.
   - **Scene**: the triangle count is well below the old 1.69 million, with rocks at half and quarter detail.
   - **Field** builds in about 1.2 s, generation included. Far rocks are meshed once, straight at half detail.
4. Stop and press Play again, then run **Benchmark** twice in a row, in the same Play session. The two reports should be close: the first run no longer pays for code running for the first time.
5. Spawn a 40 m rock and fly away from it. **Cost > Detail** moves it from full to half at about 220 m, and to quarter at about 440 m (from its surface). Each switch dissolves over half a second with no pop, and the triangle count drops each time. Fly back: it returns to full detail, craters intact.
6. Hold fire on a rock. Chips fly from every hit, including the latest, however long you keep firing. **Cost > Chips** shows how many are in the air and the per-second cap.
7. In a fresh Play session, spawn a rock, press **Reset Stats** under **Cost**, then fire the first shot at it and crack the first chunk off. The **Main thread** peak should stay low, with no spike from either.
8. To compare without the warm-up: turn off **Warm Up On Load** on the `Destruction System` before pressing Play, and run the benchmark. **Start-up** then says there was none, and the worst frame should be back near the old numbers.

### Tuning
- Asteroid types, `Data/Destruction/RockyAsteroid.asset` and `OreAsteroid.asset`: voxel size, shape, ore veins (material, spacing, thickness, depth), damage (rock removed per damage point, explosion multiplier, crater limits and roughness, push per damage point), motion (drift speed range, tumble range for a 40 m rock), trail (rocks behind a 40 m asteroid, smallest asteroid with a trail, length, drift speed back along the trail, rock size range), and breaking apart (piece speed and twist, crack width and roughness, optional effect).
- Materials, `Data/Destruction/Materials/`: vein colour and strength, chip colour, density in tonnes per cubic metre, and the cargo item mining will yield.
- Look, `Materials/Comic/ComicVoxelRock.mat`: a copy of `ComicRock.mat` with **Triplanar** on; tile size, sharpness and ore shadow under Triplanar. It is created once and then left alone, so retune it by hand.
- On the `Destruction System`: chunks rebuilt per frame, cuts and colliders per frame, half-detail distance (0 keeps full detail everywhere), detail fade time, smallest piece, most free pieces, piece range, chips (material, count, size, per cut and per frame), and trail rocks (most at once, range, solid range, most solid at once, solid rock mass per tonne, ship kick, material).
- Trail look, `Materials/Comic/ComicTrailRock.mat`: created once by the setup, then left alone. Instancing must stay on.

---

## Phase 6.11: Debug overlay

### Goal
See what every system is doing while playing, the way a studio's development builds do: a frame rate counter always on screen, and channels of live readouts and world drawing for each main system, switched on and off in game or from the God Menu. Built on Draw Debug Tools and, like it, compiled out of release builds.

### Design
- **Frame rate counter.** Always in the corner while the overlay shows: frames per second and frame time, green at 60 fps or better, yellow down to 30, red below. Any error logged this session adds a red error count beside it, so a problem is never missed.
- **Switching.** **F3** shows or hides the whole overlay. **F4** opens the channel picker; while it is open, keys **1** to **0** and **-** switch the channels. The God Menu **Debug Overlay** tab (GAME group) and the dev console (**F6**, test range) switch the same settings, which are kept between sessions, so channels can be chosen before pressing Play. Only the counter is on by default.
- **Frame rate cap.** The God Menu **Debug Overlay** tab's **Frame Rate** section caps the frame rate (10 to 360, with presets), kept between sessions and applied in Play Mode. Vsync overrides a cap, so setting one switches vsync off; switching the cap off leaves the frame rate free, and vsync returns when Play stops. The tab shows the live vsync and target values while playing, and the Rendering panel shows the cap.
- **Channels.** Each shows a panel of readouts; most also draw in the world.

  | Key | Channel | Panel | In the world |
  |---|---|---|---|
  | 1 | Performance | Frame rate and 1% low, frame time with min and max, CPU main and render thread, GPU, destruction and overlay cost, and a frame time graph with 60 and 30 fps lines | |
  | 2 | Rendering | Batches, SetPass calls, draw calls, shadow casters, triangles, vertices, resolution, quality, vsync | |
  | 3 | Memory | Total, managed, graphics and system memory, garbage per frame (any at all shows yellow), collections | |
  | 4 | Physics | Awake and total dynamic bodies, awake kinematic bodies, contacts, physics step | Every rigidbody within 250 m of the camera: collider bounds in green (awake), grey (asleep) or blue (kinematic), and velocity |
  | 5 | Camera | Position, facing, speed, field of view, clip planes | |
  | 6 | Player | Ship, speed, mass, position, each health type, target and range, cargo | Axes, facing, velocity arrow, distance line to the target, label |
  | 7 | Gameplay | Scene, run clock or outcome, jump drive state and fuel, heat and tier, cargo value, extraction points and the nearest, Authority ships | Extraction points (entry sphere, activation ring, state and range) and cargo pickups (item and quantity) |
  | 8 | AI | Authority ships against the tier's limit, spawning, each ship's range and speed | Each Authority ship's label, velocity and line to the player |
  | 9 | Sector | Name, seed, cell, region and density at the player | The sector's own debug view: bounds, regions and cells |
  | 0 | Destruction | Rocks and pieces, main-thread time and peak, meshing, triangles, memory, chips | Each rock's centre of mass, drift (drawn as ten seconds of travel), spin axis, and mass, speed and spin |
  | - | Log | Error and warning counts and the latest six messages | |

- **Placement.** Any corner, at 0.75 to 2 times size, from the God Menu. The default, top left, keeps clear of the run HUD on the right. Panels stack away from the corner.
- **Cheap.** Panel text refreshes five times a second and is built without allocating (numbers are written digit by digit); the frame graph and world drawing run every frame. Engine counters come from Unity's profiler counters, which run without the Profiler window. Labels for world drawing are built at refresh time and moved every frame, so they never lag a fast ship. The overlay times itself and shows it in the Performance panel.
- **Never in a release build.** Every overlay file sits inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD || DDT_ENABLED`, matching Draw Debug Tools, and game code never refers to it. It starts itself in Play Mode and development builds; there is nothing to place in a scene.

### Implementation
| File | Role |
|---|---|
| `Scripts/Debugging/DebugOverlay.cs` | Starts itself, builds the canvas and panels, keys, refresh and drawing loop |
| `Scripts/Debugging/DebugChannel.cs` | The channels, with names, descriptions and picker keys |
| `Scripts/Debugging/DebugSettings.cs` | What shows, kept in PlayerPrefs, shared by the keys, God Menu and dev console |
| `Scripts/Debugging/DebugFrameStats.cs` | Frame history, frame rate, 1% low, and Unity's profiler counters and frame timing |
| `Scripts/Debugging/DebugReadouts.cs` | Each channel's panel text |
| `Scripts/Debugging/DebugWorldDraw.cs` | Each channel's world drawing, through Draw Debug Tools |
| `Scripts/Debugging/DebugFrameGraph.cs` | The frame time graph |
| `Scripts/Debugging/DebugContext.cs`, `DebugText.cs`, `DebugLog.cs` | Shared lookups, allocation-free text, warning and error capture |
| `Editor/GodMenu/Tabs/DebugTab.cs` | God Menu **Debug Overlay** tab |
| `DevTest/DevTestConsole.cs` | Gains a Debug overlay section and the F3 hint |

### Verified before handover
- All four assemblies compile with Unity's own compiler settings (its response files from `Library/Bee`), with no warnings in Dead Cargo code.
- The game code also compiles with release-build defines, which proves the overlay strips out completely and nothing depends on it.

### Test steps
1. Click into Unity and let it compile. The Console should be free of errors.
2. Press Play in Polaris. Top left: the frame rate counter, for example **60 FPS 16.7 ms** in green, with a hint line for the first ten seconds.
3. Press **F3**: the overlay hides. Press it again: it returns.
4. Press **F4**: the channel picker lists every channel with its key. Press **1**: the Performance panel opens with the frame graph. Press **4**, **6**, **7** and **0** for Physics, Player, Gameplay and Destruction. Press **F4** again to close the picker; the channels stay on.
5. Fly about with **Player** on: axes on the ship, a facing line, a green velocity arrow and a label. Lock a target: a red line with its range.
6. With **Gameplay** on, the extraction points show as spheres with an activation ring and a label; cargo pickups show their item and quantity.
7. With **Destruction** on, spawn an asteroid from the God Menu Destruction tab: a dot at its centre of mass, a green drift arrow, a pink spin axis, and its mass, drift and spin.
8. With **AI** on, raise heat in the God Menu Heat tab until Authority ships arrive: each gets a label, a velocity arrow and a line to you.
9. With **Physics** on, boxes appear round every rigidbody near the camera, coloured by state. With **Sector** on, the sector's bounds, regions and cells show.
10. Open the God Menu **Debug Overlay** tab: the same switches, plus corner and size. Change the corner and size and see the overlay follow. Stop and start Play: the settings are kept.
11. In the test range, press **F6**: the dev console has a Debug overlay section with the same switches.
12. In the God Menu **Debug Overlay** tab, open **Frame Rate** and press **60** while playing: the counter settles at 60 or below. Press **Off**: it rises again. Stop and start Play: the cap is kept.

### Tuning
- Corner and size in the God Menu **Debug Overlay** tab.
- GPU and render-thread times come from Unity's frame timing, which needs **Frame Timing Stats** on (Player Settings > Other Settings). Without it they read n/a; everything else works.
- Ranges, colours and the refresh rate are constants at the top of `DebugOverlay.cs` and `DebugWorldDraw.cs`.

---

## Phase 6.12: Planet flight

### Goal
Fly from Polaris to Ixara and down to its surface, with no loading screen: point the ship at the planet, engage a cruise drive, watch the belt fall behind and the planet grow until it fills the view, then fly down through its clouds to the ground. Built on the far world (phase 6.9 steps 5b and 5c, sector generation step 3).

### Steps
| Step | Delivers | Status |
|---|---|---|
| 1. Cruise drive | Engage with **B**: a short charge, then cruise where the ship points, fast in open space and slower near a surface, so leaving Polaris takes about 12 seconds and reaching Ixara's orbit under a minute. Drops out on its own 20 km above a planet, at Polaris on the way home, or on **B**. Polaris sleeps while you are away and wakes as you return. Cruise plate and planet markers on the HUD; God Menu **Flight** tab | Done, tested 2026-11-17 |
| 2. Descent | Cruise drops you 3 km above the ground, under the clouds; fly down and land. The far copy keeps drawing Ixara at every height, and an invisible copy at true scale gives the ground you touch, matched triangle for triangle. Gravity pulls you down; the sea is solid for now | Built and compile-checked, awaiting test |
| 3. Planet look | Ixara and Halonix painted like the sun (6.9 step 5d, moved here): soft tone bands, a cool charcoal night side, colours pulled into Dead Cargo's warm and teal palette, holding from orbit to the ground | Planned |

### Design (step 1: cruise drive)
The user asked to fly to a planet and down to its surface, seamlessly, and chose to build the flight before the planets' look, so the look is built once and holds at every distance. Ixara is the run's primary planet and Halonix the second, the only two in the catalogue for now.
- **The star system.** System space is the star system's frame, with its origin at Polaris's arrival point, so Polaris's own sector space is part of it. `SectorSpace.SystemOffset` says where the near world sits in the system: zero while you are in Polaris. The far world follows the camera's system position, so as the offset grows the planets come closer, at their true scale.
- **Engaging.** **B** charges the drive for 3 seconds; **B** again cancels. Then the ship cruises where its nose points, and you steer as usual. The HUD names the destination ahead: the planet or, on the way home, Polaris.
- **Speed follows the space around you.** The drive's speed is a fraction of the distance to the nearest surface (**Rate**, 0.2 a second): a planet's surface, or Polaris's edge. Out in the open it climbs, as you near a surface it falls, so you never overshoot and long trips take not much longer than short ones: about 12 seconds to get clear of Polaris, 51 seconds from the belt to Ixara's orbit, 39 seconds back, 84 seconds to a planet 11,000 km out. It never drops below 2.5 km/s or climbs above 400 km/s, and eases rather than jumps (**Response**). The kit's engines fly against heavy drag, so drag is off while cruising and back on after.
- **Leaving Polaris.** Inside Polaris and out to 10 km past its edge, the ship really flies through the near world at cruise speed, so the rocks stream past and fall behind. At 10 km out, beyond the camera's draw distance, so nothing visible changes, the ship and camera are moved back to the world's origin, the offset takes up the difference, and **Polaris sleeps**.
- **Away.** The ship holds still in the world and the offset carries it through the system. Space dust streams past from the cruise speed (capped for the eye at 1.5 km/s), and the planets grow.
- **Polaris sleeps.** Its asteroid field and field dust stop, and everything that belongs to the place is switched off where it stands: extraction points, pickups, voxel rocks, loose cargo, Authority ships. The player's ship, the player, the cameras, the kit's managers and the Systems, UI, Cameras and Environment folders stay. Heat, the run and the jump drive keep going. It wakes in exactly the state it slept.
- **Coming home.** Cruising towards Polaris, at 10 km from its edge the ship is put back at its true place in Polaris, the offset returns to zero, Polaris wakes beyond the draw distance, and the drive drops out.
- **Dropping out.** **B** at any time; 20 km above a planet (**Planet Floor**), which is orbit, just above Ixara's clouds; or on arriving home. You leave cruise at 300 m/s (**Exit Speed**) in normal flight. A collision in cruise also drops you out. Below a planet's floor the drive will not engage.
- **Orbit for now.** After step 1 you can fly on below orbit at normal speed, but there is no ground to touch yet, and below about 1 km the far camera cuts the terrain. Step 2 hands the planet over to the near world for that.
- **HUD.** A **Cruise Drive** plate (key **B**): READY and the destination ahead, CHARGING with seconds, CRUISE with speed in km/s, or TOO CLOSE. Planets get markers with their altitude in km, and Polaris gets one while you are away.
- **God Menu.** A **Flight** tab: the drive's state, speed, nearest surface and destination; where you are in the system; and debug jumps: **Orbit Ixara**, 25 km above the primary facing it, and **Back to Polaris**.

### Implementation (step 1)
| File | Role |
|---|---|
| `Scripts/Sectors/SectorSpace.cs` | `SystemOffset`, `WorldToSystem`, `SystemToWorld`, `SetSystemOffset` |
| `Scripts/Flight/CruiseDrive.cs` | The drive on the player's ship: charge, cruise, the move between near and away, drop-out, debug jumps |
| `Scripts/Flight/CruiseMath.cs`, `CruiseSettings.cs` | Speed from clearance, distance to a body or to Polaris's box: pure, checked by the self-test |
| `Scripts/Flight/CruiseDriveInput.cs` | **B** for the drive, under the player like the kit's input scripts |
| `Scripts/Flight/SectorPresence.cs` | Polaris asleep while away, awake on return |
| `Scripts/Flight/RigShift.cs` | Moves the ship and camera rig to a new place in the world, trails and world-space particles included |
| `Scripts/Sectors/Far/FarSpace.cs` | Follows the camera's system position |
| `Scripts/Sectors/Far/BackdropBodies.cs` | Each planet's centre and radius in system space |
| `Scripts/Visuals/SpaceDust.cs`, `Shaders/SpaceDust.shader` | Dust streams past from cruise speed while the ship holds still |
| `Scripts/UI/ExtractionHud.cs`, `ExtractionHudView.cs`, `HudMarkerLayer.cs`, `HudMarkerStyle.cs` | Cruise plate, planet and Polaris markers in km; sleeping Authority ships unmarked |
| `Editor/PlanetFlightSetup.cs`, `Editor/SectorSetup.cs` | Setup step **Planet Flight** |
| `Editor/GodMenu/Tabs/FlightTab.cs` | The **Flight** tab |
| `Editor/Diagnostics/SectorSelfTest.cs` | Cruise checks |

### Verified outside Unity (step 1)
- Every Dead Cargo assembly compiles with Unity's own compiler, with no warnings from Dead Cargo code. Unity itself also compiled an earlier pass of this step and its API updater moved the drive to Unity 6's `linearVelocity`.
- The four new cruise checks pass, run outside Unity against the compiled code, flying the drive's own rule at 60 frames a second:
  - speed holds to its limits and rises with clearance; distances to the sector's box and to a sphere are exact
  - from Polaris's belt to Ixara (500 km radius, 30° across): out of the sector's reach in 12.4 s, in orbit at exactly 20.0 km after 51.1 s, fastest 130 km/s
  - from Ixara's orbit back within 10 km of Polaris after 38.7 s
  - to a planet 11,464 km out: orbit after 83.7 s, at the 400 km/s limit at most
- The backdrop checks still pass with Ixara and Halonix the only planets.
- Not run in Unity: the cruise itself, the move between near and away, Polaris sleeping and waking, the dust, the HUD plate and markers, and the Flight tab.

### Test steps (step 1)
1. Click into Unity and let it compile. In Polaris, the God Menu **Project** tab shows one step missing, **Planet Flight**: press **Set Up Sector**. The ship gains a **Cruise Drive**, the player a `CruiseControls` object, and `Sector Space` a **Sector Presence**. Save the scene.
2. Press Play. The HUD has a **Cruise Drive** plate (B), and Ixara and Halonix have markers with their height in km. Turn towards Ixara: the plate reads READY  IXARA and its marker turns teal.
3. Press **B**: CHARGING counts down 3 s. Press **B** again to cancel: CRUISE CANCELLED. Press it again and wait.
4. Cruise: the speed climbs from 2.5 km/s, rocks stream past and fall behind, then the belt is gone. Ixara grows; dust streams past. Steer: the ship turns as usual. The marker's km falls fast, then slows.
5. About a minute later the drive drops out 20 km above Ixara: ORBIT  IXARA, and you fly on at 300 m/s, the planet filling the view with clouds below.
6. Open the God Menu **Flight** tab: **Where** shows the sector asleep and your height above Ixara. Fly down a little at normal speed; there is no ground yet (step 2).
7. Turn back towards the **POLARIS** marker, press **B**, and cruise home: the drive drops out at Polaris's reach, ARRIVED  POLARIS, and the extraction marker, pickups and rocks are back as they were.
8. In the **Flight** tab, **Jumps**: **Orbit Ixara** puts you 25 km above its sunlit side at once; **Back To Polaris** returns you to the arrival point.
9. Cruise into a rock inside the belt: CRUISE INTERRUPTED, and you are back in normal flight.
10. Engage with heat raised and Authority ships about: away, their markers vanish; back home they are where they were.

### Tuning (step 1)
- On the ship's **Cruise Drive**: charge time; **Rate**, slowest and fastest speed, **Response**; **Planet Floor** (orbit height), **Home Margin**, **Exit Speed**; **Dust Speed**.
- The planets' places and sizes come from the run seed (phase 6.9 step 5c); **Orbit** jumps in the Flight tab are the quickest way to look at one.

### Design (after the test of step 1: first person on C)
The user tested cruise ("works fine") and asked for first person on **C**, so the cruise drive moved to **B**.
- **First person on C.** **C** switches the ship's camera to first person, the kit's Interior view from the cockpit, and **C** again goes back to the view you came from. The kit's own keys still work beside it: **1** first person, **2** the chase view, **]** and **[** to cycle. It runs only in a vehicle that offers the first-person view.
- **Cruise on B.** B was free in the kit, the player's controls and the Input Manager, and is the quantum-drive key in other space games. The drive's input field was renamed, so Polaris's saved C is dropped and B applies with no setup.

| File | Role |
|---|---|
| `Scripts/Controls/FirstPersonInput.cs` | **C**: first person and back, under the player like the kit's input scripts |
| `Editor/FirstPersonSetup.cs`, `Editor/SectorSetup.cs` | Setup step **First Person**: the controls under the player, pointed at the kit's Interior view |
| `Scripts/Flight/CruiseDriveInput.cs`, `Scripts/UI/ExtractionHudView.cs` | The drive on **B**, and the plate's key |

### Test steps (first person on C)
1. Click into Unity and let it compile. In Polaris, the God Menu **Project** tab shows one step missing, **First Person**: press **Set Up Sector**. The player gains `FirstPersonControls`. Save the scene.
2. Press Play and press **C**: the view moves into the cockpit. **C** again: back to the chase view.
3. Press **2** for the chase view, then **C** and **C**: you return to the chase view. **1**, **2**, **]** and **[** still work.
4. The **Cruise Drive** plate shows **B**. **B** charges and engages the drive, and **C** no longer touches it. Cruise in first person: the cockpit holds steady while the dust streams past.

### Design (step 2: descent)
The user tested cruise and first person ("works fine") and asked for the descent.
- **One picture at every height.** Ixara is drawn only by its copy in the far world, from orbit down to the ground. There is no visual hand-over to a second copy, so nothing can jump, change colour or tear at a seam. At 1:1000 a ship two metres up is two thousandths of a unit from the far copy's ground, and floating point is as precise at that scale as at true scale, so this holds all the way down.
- **Ground you can touch.** Physics needs the planet at true scale, in the near world. Below 40 km (**Ground Range**) and out of cruise, an invisible copy of the planet is built at true scale around the ship (the **ground copy**): the same prefab, without its atmosphere, clouds or sea, on a layer no camera draws (**Planet Ground**), with Planet Forge's landscape collider. Both copies see the camera from the same place in their own scale, so Planet Forge builds the same triangles for both, and the colliders match what you see. It is removed above 50 km and during cruise.
- **Detail to land on.** On the planet you are near, both copies make triangles down to 2 m (**Ground Detail**), against 10 m from orbit. It returns to the planet's own setting when you leave.
- **The sea is solid for now.** A sphere at sea level stops the ship, so landing on water rests on its surface. Swimming and floating come later.
- **Gravity.** Out of cruise and below Ground Range, the ship is pulled towards the planet's centre: 6 m/s² at the surface (**Surface Gravity**), weaker with the square of the distance. The kit's ships fly against heavy drag, so on its own gravity is a gentle sink, a couple of metres a second; you land by flying down onto the ground.
- **Cruise drops you lower.** The drive now drops out 3 km above a planet (**Drop Height**), under Ixara's cloud layer (7 to 12 km up), so you fly through the clouds in cruise and come out beneath them with the ground in view. Below 3 km the drive will not engage: climb out first. The setting has a new name, so Polaris's saved 20 km is replaced by the new 3 km with no setup.
- **The far camera reaches the ground.** While a ground copy exists the far camera draws from 0.5 m (**Ground Near Clip**) instead of 1 km, and its picture keeps a 32-bit depth buffer, so the near ground is not cut away and distant terrain does not flicker.
- **Planets stop turning while you are away.** A planet's slow spin would move its surface under you at hundreds of metres a second, so every body holds still while the sector sleeps, and turns again when you come home.
- **Altitude.** Below the drop height the Cruise Drive plate reads ALTITUDE with your height above the ground in metres, measured by a ray to the ground copy, or above sea level before the ground is ready.
- **Limits.** Planet terrain is drawn behind the near world, so it cannot hide near objects: a ship behind a hill shows through it, and your ship casts no shadow on the ground. The far copy keeps no trees or rocks, which Planet Forge's demo planets place with spawners that were left out. These belong with step 3, the look.

### Implementation (step 2)
| File | Role |
|---|---|
| `Scripts/Flight/PlanetGround.cs` | The ground copy below Ground Range, gravity, ground detail on both copies, the far camera's near clip, the height above ground |
| `Scripts/Flight/PlanetPhysics.cs` | Gravity at a distance: pure, checked by the self-test |
| `Scripts/Flight/CruiseDrive.cs`, `CruiseSettings.cs`, `CruiseExit.cs` | **Drop Height** 3 km; arriving at a planet |
| `Scripts/Sectors/Far/FarSpace.cs` | A near clip set from outside, 32-bit depth, a layer kept from every camera |
| `Scripts/Sectors/Far/BackdropBodies.cs` | Bodies hold still while the sector sleeps; each body's catalogue entry |
| `Scripts/UI/ExtractionHud.cs` | ALTITUDE on the Cruise Drive plate |
| `Editor/PlanetFlightSetup.cs`, `Editor/ProjectLayers.cs`, `Editor/PlanetsSetup.cs` | **Planet Flight** adds the ground to Far Space and the **Planet Ground** layer; layers are made in one place |
| `Editor/GodMenu/Tabs/FlightTab.cs` | **Ground** section, and **Low Over** jumps 6 km above a planet |
| `Editor/Diagnostics/SectorSelfTest.cs` | Gravity checks; the cruise trips to the new drop height |

### Verified outside Unity (step 2)
- Every Dead Cargo assembly compiles with Unity's own compiler, with no warnings from Dead Cargo code.
- The cruise checks pass with the new drop height, flying the drive's own rule: out of Polaris's reach in 12.4 s, dropped out exactly 3.0 km above Ixara after 56.7 s, home in 45.2 s, a planet 11,464 km out in 89.3 s.
- The new gravity check passes: 6.00 m/s² at the surface, 5.93 at the drop height, a quarter at twice the radius, no stronger below the surface, none without a planet.
- Not run in Unity: the ground copy and its colliders, landing, gravity on the ship, the far camera reaching the ground, and the Ground section.

### Test steps (step 2)
1. Click into Unity and let it compile. In Polaris, the God Menu **Project** tab shows **Planet Flight** missing: press **Set Up Sector**. `Far Space` gains a **Planet Ground**, and Project Settings > Tags and Layers a **Planet Ground** layer (13). Save the scene.
2. Press Play, point at Ixara and press **B**. Cruise now carries you down through the cloud layer and drops out 3 km up: ARRIVED  IXARA, with the ground in view under the clouds.
3. The **Cruise Drive** plate reads ALTITUDE with your height above the ground in metres, and **B** does nothing until you climb back above 3 km.
4. Open the God Menu **Flight** tab, **Ground** section: "Ground solid  ·  Ixara", your height above sea level and above the ground, and the pull.
5. Fly down. The ground comes up with no jump or change on screen, and nothing near the ship is cut away. With the engines idle the ship sinks slowly.
6. Touch down: the ship rests on the ground. Slide along it: it follows hills and valleys. Over the sea, the ship rests on the water's surface.
7. Fly along the ground for a while, and in first person (**C**). Hills and coasts hold steady.
8. Climb above 3 km and press **B**: cruise takes you back up and out; turn to the **POLARIS** marker for home.
9. In the **Flight** tab, **Jumps**: **Low Over Ixara** puts you 6 km up, level, facing the horizon, for a quick descent.
10. If distant coastlines or cloud edges flicker while you are low, raise **Ground Near Clip** on Planet Ground; if the ground near you looks coarse, lower **Ground Detail**.

### Tuning (step 2)
- On `Far Space`'s **Planet Ground**: **Ground Range**, **Ground Detail**, **Surface Gravity**, **Ground Near Clip**.
- On the ship's **Cruise Drive**: **Drop Height**.
