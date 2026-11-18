# Dead Cargo: Code Standards

## Layout
```
Assets/DeadCargo/
  Scripts/<Feature>/   runtime code, one feature per folder
  Scripts/UI/          HUD and menus
  Editor/              editor-only tooling
  Data/<Feature>/      ScriptableObject assets
  Resources/           assets found at runtime by name (HUD fonts, scene flow settings, cargo catalog)
  Shaders/             Dead Cargo's own shaders (DeadCargo/...)
  Materials/           materials, and data baked from them (Materials/Sky: one sky per sector)
  Scenes/              game scenes (Mothership, Polaris)
  DevTest/             test range, its template and the dev console
Docs/                  design, roadmap, standards
```

## Naming
| Element | Convention | Example |
|---|---|---|
| Namespace | `DeadCargo.<Feature>` | `DeadCargo.Cargo` |
| Type, method, property, event | PascalCase | `CargoHold`, `TotalMass` |
| Private and serialized field | camelCase | `massCapacity` |
| Constant | PascalCase | `DataFolder` |
| Data asset type | `<Thing>Definition` | `CargoItemDefinition` |
| Event | Past tense or state noun | `Changed`, `CapacityExceeded` |
| Bool | `Is`, `Has`, `Can` prefix | `IsEmpty` |

One public type per file. The file name matches the type.

## Classes
- `sealed` unless designed for inheritance.
- Serialized fields are `[SerializeField] private` with a `[Tooltip]` and a range or `[Min]` where a limit exists.
- State is exposed through read-only properties. Nothing public is settable without a method.
- Systems talk through C# events, not by polling each other in `Update`.
- No per-frame allocations in gameplay code.

## Comments
- XML `<summary>` on public types and non-obvious public members.
- Inline comments explain why, not what. No commented-out code.
- Units go in tooltips and summaries (tonnes, credits, seconds).

## Kit integration
- Kit files under `Assets/SpaceCombatKit/`, `Assets/FlatKit/` and `Assets/DrawDebugTools/` are never edited. Extend through components, events and public API.
- **Planet Forge** (with Space Graphics Toolkit and CW's shared code) lives under `Assets/Plugins/CW/` and is never edited either; Dead Cargo's planets are our own prefabs and components that use it. It compiles into its own assemblies (`CW.Common`, `CW.SimpleOriginSystem`, `CW.SimpleShaderSystem`, `SpaceGraphicsToolkit`, `PlanetForge`). It needs the **Shader Graph** package (its shaders are Shader Graph builds with a Built-in pipeline target) and **Collections**. The Input System package is not installed: the project runs on the classic Input Manager, which the kit needs, and CW's input falls back to it. Its demo scenes are for reference only; never save changes to them.
- The kit compiles into the default assembly, so Dead Cargo code does too. Assembly definitions are deferred until the kit is isolated.
- Exception: pure code with no kit or scene dependency may have its own assembly. `DeadCargo.Destruction.Core` (voxel maths and its Burst jobs, unsafe code allowed) and its editor tests, `DeadCargo.Destruction.Diagnostics`, are the first. Everything else stays in the default assembly.

## Scenes
- Work happens in the scene that is open. Setup tools act on the active scene, support Undo, and are safe to run twice.
- **The Dev Test Range stays blank.** Test content is spawned into it on demand from the God Menu **Range** tab, and removed when the test is done. Content comes from the template scene `Assets/DeadCargo/DevTest/Templates/TestRangeTemplate.unity`, a saved copy of the fully built range.
- To make new content spawnable on its own, add it to the template and add a `RangeGroup` line in `Editor/TestRange/TestRangeLibrary.cs`. Do not leave new test objects permanently in the range.
- After a phase adds objects to the player rig, spawn everything, run the phase setup, and save the scene as the template again so the template stays current.
- New scenes are created only when the design calls for one, through a setup tool. The game has a **Mothership** scene and **sector** scenes, and moves between them through `SceneFlow`, never by loading scenes directly.

## Hierarchy
Every scene follows one layout, so it stays readable as the game grows.

| Root | Holds |
|---|---|
| `Systems` | Dead Cargo scene services and managers: Heat Tracker, Authority Response, Run Tracker, EventSystem, dev bootstrap |
| `UI` | Canvases and screens: Extraction HUD, Status Strip, Run Result Screen, Mothership Hub |
| `Cameras` | Cameras that are not part of the kit's camera rig |
| `Environment` | Lights, probes, set dressing and visual effects (Space Dust) |
| Kit objects | `SceneObjects`, `VehicleCamera`, `Player`, ships and characters stay **at the root** |
| Range content | `Level`, `Cargo Pickups`, `Extraction Points` stay at the root, because the Range tab spawns them by root name |

- **Ships, characters and game agents must stay at the root.** The kit finds a ship's parts through `transform.root` for hit detection, radar and boarding. A ship inside a folder breaks all three. Ships spawned at runtime go at the root for the same reason.
- Folders are empty objects at the origin. They sit at the top of the hierarchy in the order above.
- Setup tools create objects through `SceneHierarchy.EnsureObject`, which files them into the right folder. Do not create loose root objects.
- **Dead Cargo > Tidy Hierarchy (Open Scene)**, also in the God Menu **Project** tab, files existing objects into folders. It supports Undo. Superseded objects are deleted, not hidden.

## Save data
- All saved progress lives in one `PlayerProfile`, changed only through `ProfileStore`, which saves after every change.
- Save data refers to assets by stable ID (`CargoItemDefinition.Id`), never by name or path, and resolves IDs through a catalog.
- Never change an item's ID once saves exist. Change the format by raising `PlayerProfile.CurrentVersion` and migrating in `ProfileStore`.

## Cargo
- **Change a hold only through `CargoHold`'s methods.** They keep the per-item stacks, which saves, the run report and the God Menu read, in step with the grid (`Placements`). Every unit of ordinary cargo has exactly one place on the grid; dark matter never has one.
- **Packing is first fit.** `CargoGrid` packs row by row from the top-left, upright before turned. `GetFreeUnits` counts with the same packing, so every unit it promises finds a space.
- **A screen that needs the mouse in flight borrows it through `KitControlsPause`.** It frees the cursor, holds the kit's steering and weapon triggers, and gives back only what it took. Screens never lock the cursor or switch kit input themselves.

## Units
| Quantity | Unit |
|---|---|
| Cargo mass | tonnes (t) |
| Value | credits (cr) |
| Distance | metres |
| Time | seconds |

## God Menu
The God Menu (`Dead Cargo > God Menu`, Ctrl+Alt+G) is the editor control panel for testing. Code lives in `Assets/DeadCargo/Editor/GodMenu/`.

- **One tab per system.** Each tab is a class deriving from `GodMenuTab` in `GodMenu/Tabs/`. The window discovers tabs automatically, so a new tab is one new file.
- **Tabs collapse.** Every tab is a header bar that folds its contents away. Only the first tab starts open. The button at the left of the window header collapses or expands them all.
- **Groups.** Every tab belongs to a headed group, and `Order` sets its place within the group, in steps of ten.

  | Group | Shown | Tabs |
  |---|---|---|
  | GAME | Always | Session 0, Mothership 10, Debug Overlay 20 |
  | LIVE RUN | In Play Mode, when the tab's system is in the scene | Player 0, Cargo 10, Heat 20, Authority 30, Extraction 40, World 50, Flight 55, Sector 60, Destruction 70 |
  | EDITOR | Outside Play Mode. Range only in the test range or a blank scene, never the mothership or a game sector | Range 0, Project 10 |

- **Show only what applies.** Override `IsAvailable` so a tab hides when its system is missing or the mode is wrong. Never show a tab full of disabled controls or "run the setup" warnings. The Project tab reports setup status.
- **Summaries.** Give each tab a `Summary`: a few words of live status on its header (heat tier, hold mass, spool countdown). This keeps collapsed tabs useful, so users can leave them closed.
- **Fewer, fuller tabs.** Extend an existing tab before adding a new one. A new tab must cover a whole system.
- **Cheap updates.** Scene searches go in `OnUpdate` (ten times a second), never in `OnGUI` or `Summary`, and they are skipped while the tab is hidden.
- **Sections** group controls inside a tab and also collapse. Wrap each group in `GodMenuGUI.SectionScope` and return early when it is not expanded. Keep a tab to a few sections.
- **Shared look.** Use `GodMenuGUI` for buttons, bars and status rows. Do not create styles inside a tab.
- **Fits a narrow window.** The window is often docked in a thin column. Lay out buttons with `GodMenuGUI.ButtonRow`, which wraps onto extra lines, and list rows with `BeginItemRow` / `EndItemRow`, which drop their buttons below the label when space is tight. Do not place several controls in a raw horizontal group, and do not give controls a fixed minimum width.
- **Shared state.** Look up the player, hold and assets through `GodMenuContext`.
- **A phase that adds a sector system** extends a tab (or adds one, if it must), and adds one step to `SectorSetup.Steps`. Its setup tool gets an `Apply()` that returns false after explaining the problem in a dialog. The Project tab shows one **Set Up Sector** button, never a row per phase.
- Tabs only call public game API. If a cheat needs something new, add a documented method to the system rather than reaching into private state.

## Sector generation
- Generated content is a pure function of the sector seed and a position. Never use `UnityEngine.Random`, time, or frame counts in a generator; use `SectorRandom` seeded from `SectorSpace.CellSeed`.
- Give each independent use of a cell its own stream number, so adding one kind of content never shifts another.
- Draw the same number of random values for every candidate, kept or rejected, so one result never shifts the next.
- Generators work in sector space. Convert through `SectorSpace.WorldToSector` and `SectorToWorld`, never by assuming the two are the same.
- Generation code is plain value types with no managed references, so it can run in Burst jobs.
- **One implementation for jobs and tests.** Generators read regions through `IRegionSet` and write through a buffer interface (such as `IRockBuffer`), so the same code runs in Burst jobs over native memory and in the self-test over lists. Density always comes from `SectorDensity`.
- **Content follows the region kind.** A generator fills only regions of its own kind (`SectorDensity.DensityOf`): rock in asteroid fields, wreckage in debris fields. Clearings thin every kind.
- **A cell stands alone.** Anything a cell needs from its neighbours, such as the giants small rocks keep out of, is worked out again from their seeds, never read from loaded cells, so a cell is the same whatever else is loaded.
- **Read the run's layout, not the definition.** Each run varies the regions and adds clearings round placed content (`SectorLayout`). Runtime code reads regions and density from `SectorSpace.Layout` or `SectorSpace.DensityAt`, never `SectorDefinition.Regions`, which is authoring data. Only editor tools and diagnostics read the definition.
- **The seed is the run's.** `SectorSpace.Seed` is new every load unless the definition pins it. Anything that should change between runs (layout, placement, the sky) derives from it with its own stream, so a pinned seed replays the whole run.
- **Places beyond the sector live in system space.** The star system's frame has its origin at the sector's arrival point and is kept in double precision. `SectorSpace.SystemOffset` says where the near world sits in it; convert with `WorldToSystem` and `SystemToWorld`. Never assume the near world sits at the system's origin: the cruise drive moves it.
- **The sector sleeps while the player is away.** `SectorPresence` switches off every root object that belongs to the place, and pauses the asteroid field and field dust. New sector content sleeps with it by living at the scene root, or must be added to what it pauses. Objects in the Systems, UI, Cameras and Environment folders stay awake, so they must not draw or act at sector positions while the player is away.
- **Scene content is placed by rules.** Hand-placed sector content that should move each run carries a `SectorPlacement` with a shared `SectorPlacementRule` asset. Placement runs before anything else wakes, so content may cache its position in `Awake`. Content spawned at runtime is never placed.

## Space visuals
- **Near objects: comic cel.** Ships, rocks and stations use `DeadCargo/Comic Surface`: hard colour steps, each softened over exactly one pixel with `fwidth`; a lit side, a tinted shadow side and a highlight; ink outlines; hatching in the shadows. Kit models get it by copying their Flat Kit materials (`ComicLookSetup`), never by editing kit materials.
- **The sky: deep space.** Mostly near-black void, sparse stars, muted desaturated gas with soft edges, dust lanes for depth. No saturated colour and no ink by default. Far things are flat and faint; only near things carry ink and strong light, which is what makes space read as endless.
- **Motion from time, never the CPU.** Animate in the shader from `_Time`, with a strength setting where 0 holds it still. Wrap growing angles with `fmod`.
- **Endless fields wrap in the shader.** Effects that fill space around the camera (space dust) use one static mesh that the vertex shader wraps around `_WorldSpaceCameraPos`, fading out before the wrap. No spawning, no per-object CPU work.
- **Every effect blends.** Each comic effect has a strength setting, so the look can be dialled back to plain cel shading.
- **One sun.** The scene's directional light is the sun, and the sky draws it where that light comes from. Lit shader passes read `_WorldSpaceLightPos0`; anything that sets the sun direction from the CPU asks `SceneSun`, which follows Unity's own rule (Sun Source, else the brightest directional light). Never aim at a sun of your own.
- **Instanced shaders read their own instance.** Any fragment code that reads an object's matrices (`unity_ObjectToWorld`, `unity_WorldToObject`, `UnityObjectToWorldNormal`) needs `UNITY_VERTEX_INPUT_INSTANCE_ID` in its varyings, `UNITY_TRANSFER_INSTANCE_ID` in the vertex shader and `UNITY_SETUP_INSTANCE_ID` in the fragment. Without them every instance of a batch is shaded as the first one, which lit the first asteroid field flat white.
- **Nothing pops.** Content that appears or changes detail blends: dissolve at the draw distance, grow in from nothing at the smallest size drawn, and morph between levels of detail (the comic surface's **Detail Morph**) or cross-fade through the dither.
- **Largest first.** Generated content that is culled or picked by size is kept largest first in its cell (`FieldSort`), so every reader stops at the first item too small to matter from the cell's nearest point. Never loop a whole cell where an early exit will do.
- **Detail from the camera's path.** Anything that picks detail by size on screen measures distance with `FieldRockShape.DetailDistance`, and its shader with `_FieldDetailAhead`, so the CPU and the shader agree and fast flight finds detail ready.
- **Solid through pooled stand-ins.** Instanced content turns solid by borrowing a pooled collider near the player, picked by a job, with shapes cooked on workers at load (`FieldRockColliders`). Nothing is cooked, created or destroyed in flight.
- **Far things live in the far world.** Anything beyond the main camera's 10 km reach (planets now; giants, stations and capital ships later) goes into `FarSpace`'s far world at 1:1000 through `FarSpace.Adopt`, on the Far Space layer, and is drawn only by the far camera. Never raise the main camera's far clip to show it. Place far content from sector-space directions and distances; the far world's origin follows the camera on its own.
- **Planet Forge sees one camera.** Planet Forge reads `Camera.main` for detail, atmospheres and oceans, so the far camera always stands where the main camera is. Never give the far camera a position of its own, and never tag it MainCamera.
- **A planet is drawn once.** Its far copy draws it at every height, down to the ground. Anything physical about a planet (colliders, gravity, the sea surface) lives on its invisible ground copy (`PlanetGround`), on the Planet Ground layer, which no camera draws. Never draw a second copy of a planet in the near world: two pictures of one planet will not line up.
- **Planets load by name.** Planet prefabs live in `Resources/Planets/` and the catalogue names them, so only the run's planets are loaded. Never reference planet prefabs directly from a scene or a loaded asset.
- **The autopilot has the ship while it flies.** From the moment the cruise drive charges until cruise ends (`CruiseDrive.State` is not `Ready`), it steers the ship and holds the kit's engine controls. Nothing else may steer, push or throttle the player's ship then. Flight rules the self-test should check belong in `CruiseAutopilot` or `CruiseMath`, as pure functions in system space.
- **Near a planet the air shapes the ship.** Out of cruise, `AtmosphereFlight` owns the player's ship's drag and adds thrust beside the kit's engines; nothing else sets the ship's drag. Anything that changes how fast the ship goes near a planet goes through it, and the planet's pull lives there too, not on the ground.
- **Level is the planet's horizon.** Anything that levels the ship, the camera or the HUD uses `AtmosphereFlight.Up`: away from the planet's centre within its reach, the sector's up in open space. Never `Vector3.up`, which over a planet is tilted against the ground. The kit's auto roll is off on the player for this reason; `LevelFlightInput` does its job.
- **The HUD is the comic Status Strip (phase 6.13).** New on-screen UI follows the chosen concept: leaning ink-outlined stickers with hard ink shadows (`HudSticker`), flat fills, big leaning figures, the centre kept clear, and things that matter only now shown only now. The kit's HUD parts it replaces are hidden with a CanvasGroup at zero alpha by Set Up Sector, never edited or deleted.
- **Depth through Space Depth.** Distance fade and the dissolve at the draw distance come from the `Space Depth` object's shader globals (`_SpaceDepth`, `_SpaceDepthColor`, `_SpaceDepthDissolve`). A new shader for near objects calls the comic surface's `DepthFade` and `DepthDissolve` the same way, so everything fades and dissolves alike and nothing pops at the edge of sight.
- **Sizes in pixels where crispness matters.** Stars and similar points are sized in pixels at 1080p and scale with screen height, never in world or angle units that blur or vanish.
- **Bake what is costly and fixed.** Noise that does not change at runtime is baked in the editor, and the shader thresholds the filtered value. Colours and thresholds stay live material settings.
- **Shader and baker agree.** Where C# and a shader share maths (such as `OctahedralMapping`), the C# names the shader function it must match, and the shader names the C#.
- **One material per sector**, named after the scene, with seeds from the name. Settings get tooltips in the material inspector.

## Destruction
- **One damage path.** Anything that changes shape when hit implements `IDestructible`. Kit weapons reach it through a `DestructionDamageReceiver` beside each of its colliders; other systems call `ApplyDamage` with a `DamageEvent`. Destructibles do not also carry a kit `Damageable`.
- **Grids belong to the scheduler.** Voxel grids are read by jobs through raw pointers, so only `VoxelScheduler` touches them, and only when no job is running. Everything else queues work through `VoxelBody.Carve`, `Cut` and `ApplyDamage`.
- **Deterministic rock.** Asteroid shape and ore follow the sector generation rules: a pure function of type, size and seed. Visual randomness (chips, piece spin) may use `UnityEngine.Random`.
- **Scale 1.** Voxel bodies are never scaled; cuts and grids work in metres.
- **Report what is removed.** Every cut raises `MaterialRemoved` with what it took, by material. Systems that care (mining, heat, missions) listen; they never read grids.
- **The main thread never waits.** Never call `JobHandle.Complete()` on a job that may still be running, except at teardown. Check `IsCompleted` and pick the result up next frame, or defer the work (a released body stays hidden until its jobs finish). When one body must wait, wait only for that body's jobs (`CompleteJobsFor`), never for every job in the scene.
- **Pay first-time costs at load.** A new path through the pipeline (a new cut shape, a new kind of piece) is also run by `DestructionWarmUp`, so the player's first use of it costs no more than the rest. The warm-up rock (`IsWarmUp`) raises no system events; tools and readouts skip it.
- **Budgets per second, not per frame**, where the cost or the look must not depend on frame rate (chips). Per-frame budgets are for work that must finish within the frame it was asked for (cuts, meshing).
- **Tested core.** A change to the voxel core comes with checks in `VoxelSelfTest`. Run **Dead Cargo > Diagnostics > Destruction Self-Test** before handing a change over, and the **Smoke Test** in the God Menu Destruction tab for anything touching the pipeline.

## AI
- **Dead Cargo pilots fly Dead Cargo ships.** Anything Dead Cargo spawns to fly is flown by a `PilotBrain` on a Dead Cargo pilot prefab in `Prefabs/<Feature>/`, never by the kit's demo pilots. Setup tools build these prefabs from the kit's, copying its tuning; the kit's prefabs are only read.
- **One brain, readable states.** The brain flies one `PilotState` at a time and starts and stops its behaviours itself, so behaviours keep **Update Every Frame** off.
- **Behaviours say where, `PilotFlight` flies.** A behaviour derives from the kit's `AIVehicleBehaviour` and tells the pilot's `PilotFlight` where to point and how fast to go, once a frame: speed as a share of top speed, boost as a request. Only `PilotFlight` sets engine inputs; no behaviour calls the kit's `Maneuvring` or sets steering or thrust itself. The kit's own behaviours are used only where nothing steers through them (the avoidance scan).
- **Ability is skill.** Anything that should differ between a rookie and a veteran (aim, reactions, steering used, gun discipline, missiles, when to break off) is a `PilotSkill` field, set per heat tier through a `PilotSkillDefinition`. Behaviours read the pilot's skill; they do not keep their own copies. The kit's guns aim themselves onto the lead within 3.3° of the nose, so aim error matters only beyond that.
- **Pilots fight as a wing.** Whoever sends pilots owns their `PilotWing`. Pilots join when they arrive and leave when they go, and coordination (fanning out, the line of fire) goes through the wing, never through pilots finding each other.
- **Avoidance blends, it does not take over.** `PilotFlight` bends every heading by the kit avoidance's risk, in every state. Readouts show it as a flag (`PilotBrain.IsAvoiding`), not a state.
- **No home at the origin, no flat arena.** Steer in full 3D, relative to the target, a remembered position or the ship itself. Never use the world's origin or a level plane as a reference: the cruise drive moves the world, and fights happen over planets.
- **Pure steering maths in `PilotMath`.** It is managed vector maths only, no engine calls, so it can be checked outside Unity.
- **Ships leave, they do not vanish.** A ship that stops hunting withdraws and is removed only out of view or far away. Only debug tools remove ships on the spot.
- **Switch a ship off before destroying it.** The kit's radar never forgets a trackable, and destroying one that any radar tracks makes that radar throw every frame from then on. The kit switches a ship off when it is killed; anything else that removes a trackable (a ship leaving, a despawn, a pool) calls `SetActive(false)` first and destroys it a moment later.
- **Show what each pilot is doing.** A new state or manoeuvre gets a name in `PilotStateNames`, which the debug overlay's AI channel and the God Menu's Authority tab show.

## Debugging
- Use Draw Debug Tools (`using DDT;`, `DrawDebugTools.DrawLine(...)`) for in-game debug drawing. Its calls are compiled out of release builds.
- **On-screen debugging goes through the debug overlay** (`Scripts/Debugging/`, phase 6.11). A system gets debug information by adding to it: a value in `DebugChannel` with its name, description and picker key in `DebugChannels`, a panel in `DebugReadouts`, and world drawing in `DebugWorldDraw`. Do not add separate debug canvases or OnGUI readouts.
- **Read, never change.** Readouts and world drawing use only public game API and never change game state.
- **Debug code is never shipped.** Wrap every debug-only file and reference in `#if UNITY_EDITOR || DEVELOPMENT_BUILD || DDT_ENABLED`. Game code never refers to debug code; the dependency only runs from debug to game.
- **No garbage per frame.** Build overlay text with the `DebugText` helpers, refresh it a few times a second, and build world labels at refresh time, moving them every frame.
- **Off by default**, except the frame rate counter. Channels are switched with F3 and F4 in game, the God Menu **Debug Overlay** tab and the dev console, all through `DebugSettings`.
- Drawing tied to one system's internals, like `SectorDebugView`, may still live in a component beside that system, switched from its God Menu tab. The overlay's channel can switch it too.
