# Dead Cargo: Roadmap

Each phase is small, ends in something playable, and is tested in the open scene before the next one starts.

| Phase | Name | Delivers | Status |
|---|---|---|---|
| 1 | Cargo hold | Cargo items, ship hold, pickups, load-based handling, cargo HUD | Done, tested 2026-11-16 |
| 2 | Jettison | Dump cargo from the hold as loose crates that can be re-collected | Done, tested 2026-11-16 (see Backlog #1) |
| 3 | Heat | Heat meter, tiers, decay, heat from weapons and cargo, heat HUD | Done, tested 2026-11-16 |
| 4 | Authority response | Authority ships spawn according to the heat tier | Done, tested 2026-11-16 |
| 5 | Extraction | Jump-drive spool, extraction result, cargo lost on death | Done, tested 2026-11-16 |
| 6 | Mothership hub | 6a: mothership scene, loading screen, launch and return. 6b: save data, stash, credits, selling | 6a done, tested 2026-11-16. 6b built, awaiting test |
| 7 | Station heist | Dock, on-foot vault, carry loot to the ship | Planned |
| 8 | Mining | Mining tool, ore, refinery value | Planned |
| 9 | Planetary cargo | Landing zones and contract pickups | Planned |
| 10 | Upgrades and crafting | Ship modules, mothership rooms | Planned |
| 11 | Rival syndicates | AI crews, mothership raids | Planned |
| 12 | Cycles | Season reset and persistent rank | Planned |

Phases 1 to 6 form the first complete run: launch, loot, escape, bank.

Tooling: the **God Menu** (`Dead Cargo > God Menu`) is the editor control panel for testing every phase. It gains a section whenever a phase adds a system.

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
On the ship's `CargoJettison` component: `Eject Offset`, `Eject Speed`, `Scatter Speed`, `Recollect Delay`.

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
